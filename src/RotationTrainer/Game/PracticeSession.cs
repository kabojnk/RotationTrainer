using System.Collections.Concurrent;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using RotationTrainer.Core;

namespace RotationTrainer.Game;

/// <summary>
/// Drives a <see cref="PracticeEngine"/> from the game: used actions, the countdown, combat state, the
/// current job and its hotbars. All engine and keybind work happens on <see cref="IFramework.Update"/>.
/// </summary>
public sealed class PracticeSession : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(500);
    private const float StickDeadzone = 0.3f;
    /// <summary>A countdown that rises by more than this (or appears) is a new countdown.</summary>
    private const double CountdownRise = 0.05;
    /// <summary>A countdown that vanishes with more than one frame plus this left was cancelled, not finished.</summary>
    private const double CountdownEndSlack = 0.1;

    private static readonly GamepadButtons[] Buttons =
        Enum.GetValues<GamepadButtons>().Where(b => b != GamepadButtons.None).ToArray();

    private readonly Configuration _config;
    private readonly RotationLibrary _library;
    private readonly IAbilityCatalog _catalog;
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IPlayerState _player;
    private readonly ICondition _condition;
    private readonly IFramework _framework;
    private readonly IGamepadState _gamepad;
    private readonly IKeyState _keys;
    private readonly VirtualKey[] _keyboardKeys;
    private readonly IPluginLog _log;
    private readonly ActionListener _listener;
    private readonly CountdownSource _countdown;
    private readonly HotbarScanner _scanner = new();
    private readonly GameTimingSource _gameTiming;
    private readonly ConcurrentQueue<(StepType Type, uint Id)> _pending = new();
    private readonly ConcurrentQueue<(StepType Type, uint Id)> _attempts = new();

    private uint _jobRowId;
    private double? _lastCountdown;
    private bool _wasInCombat;
    private DateTime _nextScan = DateTime.MinValue;
    /// <summary>The last device the player pressed something on (Auto keybinds); Keyboard until a pad is used.</summary>
    private InputMode _lastDevice = InputMode.Keyboard;

    public PracticeSession(Configuration config, RotationLibrary library, IAbilityCatalog catalog, IDataManager data,
        IClientState clientState, IPlayerState player, ICondition condition, IFramework framework,
        IGamepadState gamepad, IKeyState keys, IPluginLog log, ActionListener listener, CountdownSource countdown)
    {
        _config = config;
        _library = library;
        _catalog = catalog;
        _data = data;
        _clientState = clientState;
        _player = player;
        _condition = condition;
        _framework = framework;
        _gamepad = gamepad;
        _keys = keys;
        // Mouse buttons are excluded: a controller player may still click UI.
        _keyboardKeys = keys.GetValidVirtualKeys()
            .Where(k => k is not (VirtualKey.NO_KEY or VirtualKey.LBUTTON or VirtualKey.RBUTTON or VirtualKey.MBUTTON
                or VirtualKey.XBUTTON1 or VirtualKey.XBUTTON2))
            .ToArray();
        _log = log;
        _listener = listener;
        _countdown = countdown;
        _gameTiming = new GameTimingSource(() => Job == null ? Array.Empty<Ability>() : _catalog.Palette(Job));
        Timing = _gameTiming;

        _listener.ActionUsed += OnActionUsed;
        _listener.ActionAttempted += OnActionAttempted;
        _clientState.ClassJobChanged += OnClassJobChanged;
        _library.Changed += Rebuild;
        _framework.Update += OnUpdate;
    }

    /// <summary>The current job's abbreviation (e.g. "PCT"), or null when not logged in or not a combat job.</summary>
    public string? Job { get; private set; }
    public PracticeSequence Sequence { get; private set; } = PracticeSequence.Empty;
    /// <summary>Null when there is no job or nothing is selected for it.</summary>
    public PracticeEngine? Engine { get; private set; }
    public KeybindIndex Keybinds { get; } = new();
    public InputMode InputMode { get; private set; }
    /// <summary>The timing the current engine schedules with.</summary>
    public ITimingSource Timing { get; private set; }
    public event Action<EngineEvent>? EngineEvent;
    /// <summary>The player pressed the current step's action (index given), whether or not it was ready yet.</summary>
    public event Action<int>? StepPressed;

    /// <summary>Timing as the game computes it for the current character, whatever the speed source setting.</summary>
    public ITimingSource GameTiming => _gameTiming;

    /// <summary>A 2.5 s GCD from the current job's kit, for showing the GCD; null without a job.</summary>
    public Ability? ReferenceGcd => Job == null
        ? null
        : _catalog.Palette(Job).FirstOrDefault(a => a.IsGcd && a.Group == PaletteGroup.Gcd && a.BaseRecastMs == 2500);

    /// <summary>Builds a fresh engine from the current job, selection, library and configuration.</summary>
    public void Rebuild()
    {
        if (Engine != null) Engine.Event -= OnEngineEvent;
        Engine = null;
        Sequence = PracticeSequence.Empty;
        _pending.Clear();
        _attempts.Clear();
        // An active countdown is re-applied to the new engine on the next frame.
        _lastCountdown = null;

        Timing = _config.UseCustomSpeed
            ? new StatTimingSource(new SpeedProfile(_config.CustomSpellSpeed, _config.CustomSkillSpeed))
            : _gameTiming;

        Job = ResolveJob(_jobRowId);
        if (Job == null) return;

        var selection = _config.Selections.GetValueOrDefault(Job) ?? new JobSelection();
        var (opener, loop) = selection.Resolve(_library, Job);
        Sequence = PracticeSequence.Build(opener, loop, _config.UsePotions, _catalog);
        if (Sequence.IsEmpty) return;

        var job = Job;
        var latency = _config.LatencyMs / 1000.0;
        Engine = new PracticeEngine(Sequence, new Scheduler(Timing, 0.6 + latency), _config.Advance,
            (type, id) => type == StepType.Item
                ? _catalog.FindItem(ItemIds.Normalize(id)) != null
                : _catalog.FindAction(job, id) != null,
            (used, step) => _catalog.CanonicalId(job, used) == _catalog.CanonicalId(job, step),
            latency);
        Engine.Event += OnEngineEvent;
    }

    /// <summary>Starts the current run over from the first step.</summary>
    public void Restart()
    {
        _pending.Clear();
        _attempts.Clear();
        Engine?.Reset();
    }

    // Raised on the game's thread by the hook; handled in OnUpdate.
    private void OnActionUsed(StepType type, uint id) => _pending.Enqueue((type, id));

    private void OnActionAttempted(StepType type, uint id) => _attempts.Enqueue((type, id));

    private void OnClassJobChanged(uint classJobId) => SetJob(classJobId);

    private void OnEngineEvent(EngineEvent e) => EngineEvent?.Invoke(e);

    private void OnUpdate(IFramework framework)
    {
        try
        {
            var now = DateTime.UtcNow;
            var dt = framework.UpdateDelta.TotalSeconds;

            // Covers login/logout too, which raise no ClassJobChanged.
            SetJob(_player.IsLoaded ? _player.ClassJob.RowId : 0);

            UpdateInputMode();
            if (now >= _nextScan)
            {
                _nextScan = now + ScanInterval;
                Keybinds.Update(_scanner.Scan());
            }

            Engine?.Tick(dt);
            PollCountdown(dt);
            while (_attempts.TryDequeue(out var pressed))
            {
                if (Engine is { IsFinished: false } engine && engine.IsStep(engine.CurrentIndex, pressed.Type, pressed.Id))
                    StepPressed?.Invoke(engine.CurrentIndex);
            }
            while (_pending.TryDequeue(out var used))
                Engine?.OnActionUsed(used.Type, used.Id);

            var inCombat = _condition[ConditionFlag.InCombat];
            if (_wasInCombat && !inCombat && _config.ResetOnLeavingCombat) Engine?.Reset();
            _wasInCombat = inCombat;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Practice session update failed.");
        }
    }

    private void SetJob(uint classJobId)
    {
        if (classJobId == _jobRowId) return;
        _jobRowId = classJobId;
        Keybinds.Clear();
        _nextScan = DateTime.MinValue;
        Rebuild();
    }

    private void PollCountdown(double dt)
    {
        _countdown.Update();
        var remaining = _countdown.SecondsRemaining;
        if (_config.StartWithCountdown && Engine != null)
        {
            if (remaining != null && (_lastCountdown == null || remaining > _lastCountdown + CountdownRise))
            {
                Engine.OnCountdown(remaining.Value);
            }
            else if (remaining == null && _lastCountdown > dt + CountdownEndSlack && Engine.Clock < 0)
            {
                // Vanished well before reaching zero: cancelled. Running out at zero is the pull, not a cancel.
                // (Judged by the countdown itself, because in Auto the engine clock may be paused before zero.)
                Engine.OnCountdownCancelled();
            }
        }
        _lastCountdown = remaining;
    }

    /// <summary>
    /// Auto follows the last device used and is sticky: gamepad input switches to Controller, a keyboard key
    /// switches to Keyboard, and idling (e.g. through a countdown) never changes it, so labels never flip to the
    /// other device's bars on their own.
    /// </summary>
    private void UpdateInputMode()
    {
        if (_gamepad.LeftStick.Length() > StickDeadzone || _gamepad.RightStick.Length() > StickDeadzone
            || Buttons.Any(b => _gamepad.Raw(b) > 0))
            _lastDevice = InputMode.Controller;
        else if (_keyboardKeys.Any(k => _keys[k]))
            _lastDevice = InputMode.Keyboard;

        InputMode = _config.Keybinds switch
        {
            KeybindMode.Controller => InputMode.Controller,
            KeybindMode.Auto => _lastDevice,
            _ => InputMode.Keyboard,
        };
    }

    /// <summary>English abbreviation for the catalog, whatever the client language.</summary>
    private string? ResolveJob(uint classJobId)
    {
        if (classJobId == 0) return null;
        var row = _data.GetExcelSheet<ClassJob>(ClientLanguage.English).GetRowOrDefault(classJobId);
        var abbreviation = row?.Abbreviation.ExtractText();
        return abbreviation != null && _catalog.IsKnownJob(abbreviation) ? abbreviation : null;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        _library.Changed -= Rebuild;
        _clientState.ClassJobChanged -= OnClassJobChanged;
        _listener.ActionUsed -= OnActionUsed;
        _listener.ActionAttempted -= OnActionAttempted;
        if (Engine != null) Engine.Event -= OnEngineEvent;
    }
}
