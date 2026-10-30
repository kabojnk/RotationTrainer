using System.Collections.Concurrent;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.GamePad;
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
    /// <summary>Auto keybinds show controller buttons while the gamepad was used within this window.</summary>
    private static readonly TimeSpan GamepadRecent = TimeSpan.FromSeconds(10);
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
    private readonly IPluginLog _log;
    private readonly ActionListener _listener;
    private readonly CountdownSource _countdown;
    private readonly HotbarScanner _scanner = new();
    private readonly GameTimingSource _gameTiming = new();
    private readonly ConcurrentQueue<(StepType Type, uint Id)> _pending = new();

    private uint _jobRowId;
    private double? _lastCountdown;
    private bool _wasInCombat;
    private DateTime _nextScan = DateTime.MinValue;
    private DateTime _lastGamepadUse = DateTime.MinValue;

    public PracticeSession(Configuration config, RotationLibrary library, IAbilityCatalog catalog, IDataManager data,
        IClientState clientState, IPlayerState player, ICondition condition, IFramework framework,
        IGamepadState gamepad, IPluginLog log, ActionListener listener, CountdownSource countdown)
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
        _log = log;
        _listener = listener;
        _countdown = countdown;
        Timing = _gameTiming;

        _listener.ActionUsed += OnActionUsed;
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

    /// <summary>Builds a fresh engine from the current job, selection, library and configuration.</summary>
    public void Rebuild()
    {
        if (Engine != null) Engine.Event -= OnEngineEvent;
        Engine = null;
        Sequence = PracticeSequence.Empty;
        _pending.Clear();
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
        Engine = new PracticeEngine(Sequence, new Scheduler(Timing, 0.6 + _config.LatencyMs / 1000.0), _config.Advance,
            (type, id) => type == StepType.Item
                ? _catalog.FindItem(ItemIds.Normalize(id)) != null
                : _catalog.FindAction(job, id) != null);
        Engine.Event += OnEngineEvent;
    }

    /// <summary>Starts the current run over from the first step.</summary>
    public void Restart()
    {
        _pending.Clear();
        Engine?.Reset();
    }

    // Raised on the game's thread by the hook; handled in OnUpdate.
    private void OnActionUsed(StepType type, uint id) => _pending.Enqueue((type, id));

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

            UpdateInputMode(now);
            if (now >= _nextScan)
            {
                _nextScan = now + ScanInterval;
                Keybinds.Update(_scanner.Scan());
            }

            Engine?.Tick(dt);
            PollCountdown(dt);
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

    private void UpdateInputMode(DateTime now)
    {
        if (_gamepad.LeftStick.Length() > StickDeadzone || _gamepad.RightStick.Length() > StickDeadzone
            || Buttons.Any(b => _gamepad.Raw(b) > 0))
            _lastGamepadUse = now;

        InputMode = _config.Keybinds switch
        {
            KeybindMode.Controller => InputMode.Controller,
            KeybindMode.Auto when now - _lastGamepadUse < GamepadRecent => InputMode.Controller,
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
        if (Engine != null) Engine.Event -= OnEngineEvent;
    }
}
