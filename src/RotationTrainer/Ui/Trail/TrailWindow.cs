using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using RotationTrainer.Core;
using RotationTrainer.Game;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Trail;

/// <summary>
/// The on-screen trail of upcoming steps (<c>/rtrain trail</c>). Geometry comes from <see cref="TrailLayout"/>;
/// this window adds the icons, keybind chips, the slide, pop, press and flash animations and the caption.
/// In Auto every style scrolls with the clock, and popped steps ride the track off to the left as they fade.
/// Sizes are unscaled pixels from the approved mockup; everything is multiplied by the UI scale when drawn.
/// </summary>
public sealed class TrailWindow : Window, IDisposable
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNav;

    /// <summary>The title bar buttons' and Skip button's own windows: just the buttons, always taking the mouse.</summary>
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize;

    private const float MarkerX = 96f;
    /// <summary>The window is the glass panel itself: no margin above or beside it.</summary>
    private const float TrackTop = 0f;
    /// <summary>The strip's top band holding the title and the title bar buttons, above the keybind chips.</summary>
    private const float TitleBar = 26f;
    private const float RowTop = 34f;
    private const float StripInset = 0f;
    private const float StripRounding = 20f;
    private const float FooterHeight = 40f;
    /// <summary>The glass panel ends this far above the window's bottom; the caption and Skip sit this far in.</summary>
    private const float PanelBottomInset = 0f, CaptionInset = 18f;
    private const float MinWidth = MarkerX + 360f;
    private const float ChipHeight = 16f;
    private const float ChipGap = 3f;
    private const float CompactChipLift = 10f;
    private const float TitleInset = 10f;
    private const int CompactMaxAhead = 5;
    /// <summary>How many popped steps back Auto looks for the step the clock is on.</summary>
    private const int FlowLookBack = 16;

    private const double SlideSeconds = 0.28;
    private const float WidthEaseSeconds = 0.07f;
    private const double PopSeconds = 0.22;
    /// <summary>Auto: how long a popped step takes to fade as it slides off.</summary>
    private const double FadeSeconds = 0.9;
    private const double PressSeconds = 0.3;
    private const float PressDip = 0.88f;
    private const float PopScale = 1.35f;
    private const float PopLift = 6f;
    private const double FlashSeconds = 0.4;
    private const double StartSeconds = 0.8;
    private const double PulseHalfPeriod = 0.6;

    /// <summary>How far the glass strip's gradient leans toward the accent, at the top and bottom.</summary>
    private const float TintTop = 0.22f, TintBottom = 0.1f;
    private const float CaptionPx = 13f;
    /// <summary>The controller glyph in a keybind chip, and its gap after the trigger.</summary>
    private const float GlyphSize = 11f, GlyphGap = 3f;

    private static readonly Vector4 Gold = new(1f, 210 / 255f, 122 / 255f, 1f);

    private sealed record Pop(PlannedStep Step, TrailItem Item, double Start)
    {
        /// <summary>Center in layout coordinates; follows the track as later steps pop.</summary>
        public float X { get; set; } = Item.CenterX;
    }

    private readonly Configuration _config;
    private readonly PracticeSession _session;
    private readonly ITextureProvider _textures;
    private readonly IClientState _clientState;
    private readonly Action _openSettings;
    private readonly Action _saveConfig;
    // Engine events arrive on the framework update; they are applied on the next draw.
    private readonly ConcurrentQueue<EngineEvent> _events = new();
    private readonly ConcurrentQueue<int> _presses = new();

    private double _time;
    private PracticeEngine? _engine;
    private TrailStyle _style;
    private TrailSize _size;
    private int _current = -1;
    private Dictionary<int, TrailItem> _last = new();
    private readonly List<Pop> _pops = new();
    private float _slideFrom;
    private double _slideStart = double.NegativeInfinity;
    private int _flashIndex = -1;
    private double _flashStart = double.NegativeInfinity;
    private int _pressIndex = -1;
    private double _pressStart = double.NegativeInfinity;
    private double? _lastClock;
    private double _startShown = double.NegativeInfinity;
    private float _width;
    private float? _pullX;
    private bool _hidden = true;

    // This frame's model, built in PreDraw and drawn in Draw.
    private IReadOnlyList<TrailItem> _items = Array.Empty<TrailItem>();
    private double _reference;
    private float _offset;
    private IDisposable? _windowStyle;
    /// <summary>Screen positions of the title bar's right end and the caption line's right end, both at mid-height.</summary>
    private Vector2 _titleAnchor, _skipAnchor;
    private bool _showSkip;

    /// <param name="saveConfig">Saves the configuration without rebuilding the session, so the run is kept.</param>
    public TrailWindow(Configuration config, PracticeSession session, ITextureProvider textures, IClientState clientState,
        Action openSettings, Action saveConfig)
        : base("Rotation Trainer Trail###RotationTrainerTrail", BaseFlags)
    {
        _config = config;
        _session = session;
        _textures = textures;
        _clientState = clientState;
        _openSettings = openSettings;
        _saveConfig = saveConfig;
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        DisableFadeInFadeOut = true;
        AllowPinning = false;
        AllowClickthrough = false;
        _session.EngineEvent += OnEngineEvent;
        _session.StepPressed += OnStepPressed;
    }

    private static float Scale => ImGuiHelpers.GlobalScale;

    private TrailMetrics Metrics => TrailMetrics.For(_config.Size);

    private float Raise => (_config.Style == TrailStyle.Timeline ? 0.95f : 0.6f) * Metrics.Gcd;

    private float GcdTop => TrackTop + TitleBar + RowTop + Raise;

    private float StripHeight => TitleBar + RowTop + Raise + Metrics.Gcd + (_config.Style == TrailStyle.Timeline ? 26f : 22f);

    private float WindowHeight => TrackTop + StripHeight + FooterHeight;

    private Vector4 Accent => GlassTheme.AccentColor(_config.Accent);

    private bool FollowsClock => _config.Advance == AdvanceMode.Auto && _engine?.Clock != null;

    private double PopDuration => _config.Advance == AdvanceMode.Auto ? FadeSeconds : PopSeconds;

    public override bool DrawConditions()
    {
        var visible = _config.ShowTrail && _clientState.IsLoggedIn;
        if (!visible) _hidden = true;
        return visible;
    }

    private void OnEngineEvent(EngineEvent e)
    {
        if (e.Kind is EngineEventKind.Popped or EngineEventKind.WrongPress) _events.Enqueue(e);
    }

    private void OnStepPressed(int index) => _presses.Enqueue(index);

    public override void PreDraw()
    {
        var dt = ImGui.GetIO().DeltaTime;
        _time += dt;
        if (_hidden)
        {
            // Shown again: drop anything that happened while hidden instead of replaying it.
            _hidden = false;
            _width = 0;
            ResetAnimation();
        }
        Flags = BaseFlags | (_config.LockTrail ? ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove : 0);
        UpdateModel(dt);

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos + new Vector2(viewport.Size.X * 0.5f, viewport.Size.Y * 0.62f),
            ImGuiCond.FirstUseEver, new Vector2(0.5f, 0f));
        ImGui.SetNextWindowSize(new Vector2(_width, WindowHeight) * Scale, ImGuiCond.Always);
        _windowStyle = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero)
            .Push(ImGuiStyleVar.WindowBorderSize, 0f);
    }

    public override void PostDraw()
    {
        _windowStyle?.Dispose();
        _windowStyle = null;
        DrawOverlays();
    }

    /// <summary>
    /// The title bar buttons, right-aligned in the strip's top band like a window's minimize and close buttons, and
    /// the Skip button at the caption's right end. Each is a separate little window drawn over the trail, so it stays
    /// clickable when the trail is locked and the trail itself lets the mouse through to the game.
    /// </summary>
    private void DrawOverlays()
    {
        var s = Scale;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero)
            .Push(ImGuiStyleVar.WindowBorderSize, 0f)
            .Push(ImGuiStyleVar.FrameRounding, 6 * s)
            .Push(ImGuiStyleVar.FrameBorderSize, 0f)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(2, 0) * s);
        using var colors = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero)
            .Push(ImGuiCol.ButtonHovered, GlassTheme.WithAlpha(Accent, 0.35f))
            .Push(ImGuiCol.ButtonActive, GlassTheme.WithAlpha(Accent, 0.7f))
            .Push(ImGuiCol.Text, GlassTheme.Fg(0.75f));

        var buttons = new (FontAwesomeIcon Icon, string Tip, bool Enabled, Action Click, bool Close)[]
        {
            (FontAwesomeIcon.Redo, "Restart the run", _session.Engine != null, _session.Restart, false),
            (_config.LockTrail ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen,
                _config.LockTrail ? "Unlock: drag the trail to move it" : "Lock: clicks pass through to the game",
                true, () => { _config.LockTrail = !_config.LockTrail; _saveConfig(); }, false),
            (FontAwesomeIcon.Cog, "Settings (/rtrain)", true, _openSettings, false),
            (FontAwesomeIcon.Ban, "Hide the trail (/rtrain trail shows it again)", true,
                () => { _config.ShowTrail = false; _saveConfig(); }, true),
        };
        var pad = new Vector2(5, 3) * s;
        var row = new Vector2(-2 * s, 0);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            foreach (var b in buttons)
            {
                var size = ImGui.CalcTextSize(b.Icon.ToIconString()) + pad * 2;
                row = new Vector2(row.X + size.X + 2 * s, Math.Max(row.Y, size.Y));
            }
        }
        ImGui.SetNextWindowPos(_titleAnchor - new Vector2(row.X + TitleInset * s, row.Y / 2));
        if (ImGui.Begin("##RotationTrainerTrailTitle", OverlayFlags))
        {
            using var framePad = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, pad);
            for (var i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                if (i > 0) ImGui.SameLine();
                bool clicked;
                using (ImRaii.Disabled(!b.Enabled))
                using (ImRaii.PushColor(ImGuiCol.ButtonHovered, GlassTheme.WithAlpha(GlassTheme.Danger, 0.7f), b.Close))
                using (ImRaii.PushFont(UiBuilder.IconFont))
                    clicked = ImGui.Button($"{b.Icon.ToIconString()}##title{i}");
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) GlassWidgets.Tooltip(b.Tip);
                if (clicked) b.Click();
            }
        }
        ImGui.End();

        if (!_showSkip) return;
        var skipSize = SkipSize();
        ImGui.SetNextWindowPos(_skipAnchor - new Vector2(skipSize.X, skipSize.Y / 2));
        using var skipStyle = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, SkipPad)
            .Push(ImGuiStyleVar.FrameBorderSize, s);
        // A chip like the keybind chips, warming to the accent on hover and filling with it when pressed.
        var chip = GlassTheme.Palette.Chip;
        using var skipColors = ImRaii.PushColor(ImGuiCol.Button, chip)
            .Push(ImGuiCol.ButtonHovered, Vector4.Lerp(chip, Accent with { W = 1 }, 0.4f))
            .Push(ImGuiCol.ButtonActive, Accent with { W = 1 })
            .Push(ImGuiCol.Border, GlassTheme.WithAlpha(Accent, 0.55f))
            .Push(ImGuiCol.Text, GlassTheme.Fg(0.95f));
        if (ImGui.Begin("##RotationTrainerTrailSkip", OverlayFlags))
        {
            if (ImGui.Button(SkipLabel)) _session.Skip();
            if (ImGui.IsItemHovered())
                GlassWidgets.Tooltip("Skip this step, e.g. for an ability that isn't on your hotbar or keybound.");
        }
        ImGui.End();
    }

    private const string SkipLabel = "Skip";

    private static Vector2 SkipPad => new Vector2(8, 2) * Scale;

    /// <summary>The Skip button's size on screen, including its border.</summary>
    private static Vector2 SkipSize() => ImGui.CalcTextSize(SkipLabel) + SkipPad * 2 + new Vector2(2 * Scale);

    /// <summary>Builds this frame's items and advances the slide, pop and flash state.</summary>
    private void UpdateModel(float dt)
    {
        var engine = _session.Engine;
        if (!ReferenceEquals(engine, _engine) || _config.Style != _style || _config.Size != _size)
        {
            // A rebuilt session or a new look: start clean, without sliding from the old positions.
            _engine = engine;
            _style = _config.Style;
            _size = _config.Size;
            _width = 0;
            ResetAnimation();
        }

        _items = Array.Empty<TrailItem>();
        if (engine == null || _session.Sequence.IsEmpty)
        {
            _width = MinWidth;
            return;
        }

        var current = engine.CurrentIndex;
        if (current < _current) ResetAnimation(); // the run restarted

        var clock = engine.Clock;
        if (_lastClock is < 0 && clock >= 0) _startShown = _time;
        _lastClock = clock;

        var extent = (IReadOnlyList<TrailItem>)Array.Empty<TrailItem>();
        if (!engine.IsFinished)
        {
            var ahead = _config.Style == TrailStyle.CompactRail
                ? Math.Min(_config.AbilitiesAhead, CompactMaxAhead)
                : _config.AbilitiesAhead;
            var inputs = new List<TrailInput>(ahead + 1);
            for (var i = current; i <= current + ahead && _session.Sequence[i] is { } step; i++)
                inputs.Add(Input(engine, i));
            var auto = _config.Advance == AdvanceMode.Auto && clock != null;
            _reference = auto ? clock!.Value : engine.ExpectedTime(current);
            if (auto && _config.Style != TrailStyle.Timeline)
            {
                // Back up to the step the clock is on, so the track scrolls from it even after early presses.
                var first = current;
                while (first > 0 && current - first < FlowLookBack && engine.ExpectedTime(first) > clock) first--;
                var steps = Enumerable.Range(first, current - first).Select(i => Input(engine, i)).Concat(inputs).ToList();
                _items = TrailLayout.Flow(_config.Style, _config.Size, steps, current, clock!.Value, MarkerX);
            }
            else
            {
                _items = TrailLayout.Layout(_config.Style, _config.Size, inputs, _reference, MarkerX);
            }
            // In Auto the window is sized from the layout at the current step's expected time, so a track following
            // the clock (e.g. early in a long countdown) never widens it; the clip rect hides the overflow.
            extent = _config.Advance == AdvanceMode.Auto
                ? TrailLayout.Layout(_config.Style, _config.Size, inputs, engine.ExpectedTime(current), MarkerX)
                : _items;
        }

        // How far the track moved since last frame, measured on the current step.
        var shift = 0f;
        if (_items.Count > 0 && _last.TryGetValue(_items[0].Index, out var before))
            shift = before.CenterX - _items[0].CenterX;
        if (Math.Abs(shift) > 0.01f)
        {
            if (!FollowsClock)
            {
                _slideFrom = CurrentSlide() + shift;
                _slideStart = _time;
            }
            foreach (var pop in _pops) pop.X -= shift;
            _pullX -= shift;
        }
        if (_config.Style != TrailStyle.Timeline)
        {
            for (var i = 1; i < _items.Count; i++)
            {
                var a = _items[i - 1];
                var b = _items[i];
                if (_session.Sequence[a.Index]?.IsPrepull == true && _session.Sequence[b.Index]?.IsPrepull == false)
                    _pullX = (a.CenterX + a.Size * a.Scale / 2 + b.CenterX - b.Size * b.Scale / 2) / 2;
            }
        }

        while (_events.TryDequeue(out var e))
        {
            if (e.Kind == EngineEventKind.Popped)
            {
                if (_last.TryGetValue(e.Index, out var item) && _session.Sequence[e.Index] is { } step)
                    _pops.Add(new Pop(step, item, _time) { X = item.CenterX - shift });
            }
            else if (_config.FlashWrong)
            {
                _flashIndex = e.Index;
                _flashStart = _time;
            }
        }
        _pops.RemoveAll(p => _time - p.Start >= PopDuration);
        while (_presses.TryDequeue(out var pressed))
        {
            _pressIndex = pressed;
            _pressStart = _time;
        }

        _current = current;
        _last = _items.ToDictionary(i => i.Index);
        _offset = FollowsClock ? 0f : CurrentSlide();
        if (_pullX + _offset < 0) _pullX = null; // scrolled off the left edge

        var right = MinWidth;
        foreach (var item in extent)
            right = Math.Max(right, item.CenterX + item.Size * item.Scale / 2 + 24f + StripInset);
        // Grows and shrinks with the content, easing so a pop does not make the strip jump.
        _width = _width <= 0 ? right : _width + (right - _width) * (1f - MathF.Exp(-dt / WidthEaseSeconds));
    }

    private TrailInput Input(PracticeEngine engine, int index) =>
        new(index, _session.Sequence[index]!.Ability.IsGcd, engine.ExpectedTime(index));

    private void ResetAnimation()
    {
        _current = -1;
        _last = new Dictionary<int, TrailItem>();
        _pops.Clear();
        _slideFrom = 0;
        _slideStart = double.NegativeInfinity;
        _flashIndex = -1;
        _pressIndex = -1;
        _lastClock = null;
        _startShown = double.NegativeInfinity;
        _pullX = null;
        _events.Clear();
        _presses.Clear();
    }

    private float CurrentSlide()
    {
        var t = (_time - _slideStart) / SlideSeconds;
        return t >= 1 ? 0f : _slideFrom * (1f - EaseOut((float)t));
    }

    private static float EaseOut(float t)
    {
        var u = 1f - Math.Clamp(t, 0f, 1f);
        return 1f - u * u * u;
    }

    public override void Draw()
    {
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetWindowPos();
        var s = Scale;
        var width = _width;
        _titleAnchor = origin + new Vector2(width - StripInset, TrackTop + TitleBar / 2 + 2) * s;
        _showSkip = false;

        var stripMin = origin + new Vector2(StripInset, TrackTop) * s;
        var stripMax = origin + new Vector2(width - StripInset, TrackTop + StripHeight) * s;
        if (_config.TrailGlassStrip)
        {
            // One glass panel behind the track and the caption line, so the caption never sits on the bare world.
            DrawStrip(dl, stripMin, origin + new Vector2(width - StripInset, WindowHeight - PanelBottomInset) * s);
            dl.AddLine(new Vector2(stripMin.X + 14 * s, stripMax.Y), new Vector2(stripMax.X - 14 * s, stripMax.Y),
                U32(GlassTheme.Fg(0.08f)), s);
        }
        if (!_config.LockTrail)
        {
            // Unlocked (draggable): an accent outline around the panel.
            dl.AddRect(origin, origin + new Vector2(width, WindowHeight) * s, U32(GlassTheme.WithAlpha(Accent, 0.7f)),
                StripRounding * s, ImDrawFlags.None, 1.5f * s);
        }
        DrawTitle(dl, stripMin, stripMax);

        var engine = _session.Engine;
        if (engine == null || _session.Sequence.IsEmpty)
        {
            const string empty = "Pick an opener and loop with /rtrain";
            var size = TextSize(empty, 15f);
            DrawText(dl, (stripMin + stripMax) / 2 - size / 2, 15f, GlassTheme.Fg(0.85f), empty);
            return;
        }

        dl.PushClipRect(stripMin - new Vector2(StripInset, TrackTop) * s, stripMax + new Vector2(StripInset, 0) * s, true);
        if (_config.Style == TrailStyle.Rail) DrawRail(dl, origin, stripMin, stripMax);
        if (_config.Style == TrailStyle.Timeline) DrawTicks(dl, origin, stripMin, stripMax);
        DrawPull(dl, origin);

        foreach (var pop in _pops)
        {
            var item = pop.Item with { CenterX = pop.X, IsCurrent = false };
            if (_config.Advance == AdvanceMode.Auto)
            {
                // Rides the track off to the left, fading with time and as it nears the left edge.
                var fade = 1f - (float)((_time - pop.Start) / FadeSeconds);
                var edge = Math.Clamp((pop.X + _offset + item.Size / 2) / MarkerX, 0f, 1f);
                DrawStep(dl, origin, pop.Step, item, Math.Clamp(fade, 0f, 1f) * edge, 0f, false);
            }
            else
            {
                var t = EaseOut((float)((_time - pop.Start) / PopSeconds));
                DrawStep(dl, origin, pop.Step, item, 1f - t, t, false);
            }
        }
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (_session.Sequence[item.Index] is { } step) DrawStep(dl, origin, step, item, 1f, 0f, true);
        }
        if (_config.Style == TrailStyle.Timeline) DrawMarker(dl, origin);
        dl.PopClipRect();

        _showSkip = !engine.IsFinished;
        var lineHeight = TextSize("Ag", CaptionPx).Y;
        _skipAnchor = origin + new Vector2((width - CaptionInset) * s, WindowHeight * s - 10 * s - lineHeight / 2);
        DrawCaption(dl, origin, engine);
        DrawCountdown(dl, origin, engine.Clock, _showSkip ? SkipSize().X + 10 * s : 0);
    }

    /// <summary>The title at the left of the strip's top band, and a faint line under the band.</summary>
    private void DrawTitle(ImDrawListPtr dl, Vector2 stripMin, Vector2 stripMax)
    {
        var s = Scale;
        var title = _session.Job is { } job ? "Rotation Trainer · " + job : "Rotation Trainer";
        var size = TextSize(title, 11f);
        DrawText(dl, new Vector2(stripMin.X + 16 * s, stripMin.Y + (TitleBar * s - size.Y) / 2 + 2 * s), 11f,
            GlassTheme.Fg(0.5f), title);
        var y = stripMin.Y + (TitleBar + 2) * s;
        dl.AddLine(new Vector2(stripMin.X + 14 * s, y), new Vector2(stripMax.X - 14 * s, y), U32(GlassTheme.Fg(0.08f)), s);
    }

    private void DrawStrip(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var o = Math.Clamp(_config.TrailOpacity, 0f, 1f);
        var s = Scale;
        var rounding = StripRounding * s;

        // Shadow 0 10px 30px rgba(0,0,0,.35), outside the strip only (and outside the window, so unclipped).
        const int rings = 10;
        var reach = 30 * s;
        dl.PushClipRect(min - new Vector2(reach), max + new Vector2(reach, reach + 10 * s), false);
        for (var i = 1; i <= rings; i++)
        {
            var t = i / (float)rings;
            var grow = reach * t * 0.5f;
            var offset = new Vector2(0, 10 * s * t);
            var alpha = 0.35f * o * (1 - t) * (1 - t) / 2.5f;
            dl.AddRect(min - new Vector2(grow) + offset, max + new Vector2(grow) + offset, U32(new Vector4(0, 0, 0, alpha)),
                rounding + grow, ImDrawFlags.None, reach / rings + Scale);
        }
        dl.PopClipRect();

        // A dark glass base whose alpha is the opacity (1 = opaque), then the highlight on top.
        var baseStart = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, U32(new Vector4(1, 1, 1, o)), rounding);
        // The theme's gradient tinted with the accent, more at the top.
        var palette = GlassTheme.Palette;
        ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(dl, baseStart, dl.VtxBuffer.Size, min, new Vector2(min.X, max.Y),
            U32(Vector4.Lerp(palette.GlassTop, Accent with { W = 1 }, TintTop * palette.Tint)),
            U32(Vector4.Lerp(palette.GlassBottom, Accent with { W = 1 }, TintBottom * palette.Tint)));

        // Vertical gradient rgba(255,255,255,.14) → .05, drawn as whole-pixel bands of the same rounded shape.
        const int bands = 8;
        for (var k = 0; k < bands; k++)
        {
            var y0 = MathF.Round(min.Y + (max.Y - min.Y) * k / bands);
            var y1 = MathF.Round(min.Y + (max.Y - min.Y) * (k + 1) / bands);
            var alpha = (0.14f + (0.05f - 0.14f) * (k + 0.5f) / bands) * o;
            dl.PushClipRect(new Vector2(min.X, y0), new Vector2(max.X, y1), true);
            dl.AddRectFilled(min, max, U32(GlassTheme.Gloss(alpha)), rounding);
            dl.PopClipRect();
        }
        dl.AddRect(min, max, U32(Vector4.Lerp(GlassTheme.Gloss(0.2f), GlassTheme.WithAlpha(Accent, 0.45f), 0.5f) * new Vector4(1, 1, 1, o)),
            rounding, ImDrawFlags.None, s);
        dl.AddLine(new Vector2(min.X + rounding, min.Y + s), new Vector2(max.X - rounding, min.Y + s),
            U32(Vector4.Lerp(GlassTheme.Gloss(0.35f), GlassTheme.WithAlpha(Accent, 0.5f), 0.4f) * new Vector4(1, 1, 1, o)), s);
    }

    private void DrawRail(ImDrawListPtr dl, Vector2 origin, Vector2 stripMin, Vector2 stripMax)
    {
        var s = Scale;
        var y = origin.Y + (GcdTop + Metrics.Gcd / 2 - 3) * s;
        dl.AddRectFilled(new Vector2(stripMin.X, y), new Vector2(stripMax.X, y + 6 * s), U32(GlassTheme.Fg(0.18f)), 3 * s);
    }

    private void DrawTicks(ImDrawListPtr dl, Vector2 origin, Vector2 stripMin, Vector2 stripMax)
    {
        var s = Scale;
        var pps = Metrics.PixelsPerSecond;
        var baseY = GcdTop + Metrics.Gcd;
        var first = (int)Math.Floor(_reference - (MarkerX + _offset) / pps);
        var last = (int)Math.Ceiling(_reference + (_width - MarkerX - _offset) / pps);
        for (var t = first; t <= last; t++)
        {
            var x = origin.X + (MarkerX + (float)((t - _reference) * pps) + _offset) * s;
            if (x < stripMin.X || x > stripMax.X) continue;
            var big = t % 5 == 0;
            var top = origin.Y + (baseY + (big ? 2 : 6)) * s;
            dl.AddRectFilled(new Vector2(x, top), new Vector2(x + s, top + (big ? 12 : 6) * s), U32(GlassTheme.Fg(0.18f)));
            if (big && t != 0)
                DrawText(dl, new Vector2(x + 4 * s, top - 16 * s), 11f, GlassTheme.Fg(0.55f),
                    t.ToString(CultureInfo.InvariantCulture) + "s");
        }
    }

    private void DrawPull(ImDrawListPtr dl, Vector2 origin)
    {
        float? x = null;
        if (_config.Style == TrailStyle.Timeline)
        {
            if (_session.Sequence[0]?.IsPrepull == true)
                x = MarkerX + (float)((0 - _reference) * Metrics.PixelsPerSecond);
        }
        else
        {
            x = _pullX; // anchored on the track, so it outlives the last pre-pull step until it scrolls off
        }
        if (x == null) return;

        var s = Scale;
        var left = origin.X + (x.Value + _offset - 1) * s;
        var top = origin.Y + (TrackTop + TitleBar + 8) * s;
        dl.AddRectFilled(new Vector2(left, top), new Vector2(left + 2 * s, top + (StripHeight - TitleBar - 14) * s),
            U32(GlassTheme.Fg(0.4f)));
        DrawText(dl, new Vector2(left + 6 * s, top - 2 * s), 12f, GlassTheme.Fg(0.85f), "Pull");
    }

    private void DrawMarker(ImDrawListPtr dl, Vector2 origin)
    {
        var s = Scale;
        var min = origin + new Vector2(MarkerX - 1, TrackTop + TitleBar + 6) * s;
        var max = min + new Vector2(2, StripHeight - TitleBar - 12) * s;
        DrawGlow(dl, min, max, s, Accent, 12 * s, 0.9f);
        dl.AddRectFilled(min, max, U32(GlassTheme.Fg(0.55f)), s);
    }

    /// <param name="alpha">Overall opacity (fades popped steps).</param>
    /// <param name="pop">0..1 progress of the pop animation; 0 for live steps.</param>
    /// <param name="live">Not yet popped: gets the current-step ring, the press and the wrong flash.</param>
    private void DrawStep(ImDrawListPtr dl, Vector2 origin, PlannedStep step, TrailItem item, float alpha, float pop,
        bool live)
    {
        var s = Scale;
        var top = item.Row == TrailRow.Gcd ? GcdTop : GcdTop - Raise;
        var center = origin + new Vector2(item.CenterX + _offset, top + item.Size / 2) * s;
        // A press dips the tile and springs it back, like the game's hotbar.
        var press = live && item.Index == _pressIndex && _time - _pressStart < PressSeconds
            ? 1f - (float)((_time - _pressStart) / PressSeconds)
            : 0f;
        var dip = press > 0 ? 1f - (1f - PressDip) * MathF.Sin(MathF.PI * (1f - press)) : 1f;
        var tileScale = item.Scale * (1 + (PopScale - 1) * pop) * dip;
        var half = item.Size * tileScale / 2 * s;
        var min = center - new Vector2(half);
        var max = center + new Vector2(half);
        var rounding = item.Size * tileScale * 0.22f * s;

        // Drop shadow, then the current-step ring and glow (gold and pulsing once a pre-pull step is due).
        if (alpha > 0.01f) DrawGlow(dl, min + new Vector2(0, 3 * s), max + new Vector2(0, 3 * s), rounding, Vector4.Zero, 8 * s, 0.45f * alpha);
        if (live && item.IsCurrent)
        {
            var clock = _engine?.Clock;
            var due = step.IsPrepull && step.At is { } at && clock >= at;
            var p = due ? (float)(0.5 - 0.5 * Math.Cos(Math.PI * _time / PulseHalfPeriod)) : 0f;
            var ring = Vector4.Lerp(GlassTheme.Fg(0.95f), Gold, p);
            var glow = Vector4.Lerp(Accent, Gold, p);
            DrawGlow(dl, min, max, rounding, glow, (22 + 6 * p) * s, 0.9f);
            dl.AddRect(min - new Vector2(1.5f * s), max + new Vector2(1.5f * s), U32(ring), rounding + 1.5f * s,
                ImDrawFlags.None, 3 * s);
        }

        var wrap = _textures.GetFromGameIcon(new GameIconLookup(step.Ability.IconId)).GetWrapOrEmpty();
        dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, U32(new Vector4(1, 1, 1, alpha)), rounding);
        dl.AddRect(min, max, U32(GlassTheme.Fg(0.35f * alpha)), rounding, ImDrawFlags.None, s);

        if (press > 0)
        {
            // White flash on the icon and an accent ring that bursts outward.
            dl.AddRectFilled(min, max, U32(GlassTheme.Gloss(0.45f * press * press)), rounding);
            var burst = (1f - press) * 10f * s;
            dl.AddRect(min - new Vector2(burst), max + new Vector2(burst), U32(GlassTheme.WithAlpha(Accent, press)),
                rounding + burst, ImDrawFlags.None, 2.5f * s);
        }

        if (live && item.Index == _flashIndex && _time - _flashStart < FlashSeconds)
        {
            var f = 1f - (float)((_time - _flashStart) / FlashSeconds);
            dl.AddRectFilled(min, max, U32(GlassTheme.WithAlpha(GlassTheme.Danger, 0.55f * f)), rounding);
            dl.AddRect(min - new Vector2(1.5f * s), max + new Vector2(1.5f * s),
                U32(GlassTheme.WithAlpha(GlassTheme.Danger, f)), rounding + 1.5f * s, ImDrawFlags.None, 3 * s);
        }

        // Keybind chip above the tile and the pre-pull tag below it; both fade and rise as the step pops.
        var textAlpha = alpha;
        var lift = PopLift * pop * s;
        if (_config.Keybinds != KeybindMode.Off)
        {
            var label = _session.KeybindLabel(step.Ability.Type, step.Ability.Id);
            var pad = PadButton.TryParse(label, out var button) ? button : (PadButton?)null;
            // On another cross hotbar set: "S3 LT", then the glyph.
            if (pad is { } p) label = p.Set is { } set ? $"S{set} {p.Trigger}" : p.Trigger;
            var textSize = TextSize(label, 11f);
            var glyph = pad != null ? GlyphSize * s : 0f;
            var contentWidth = textSize.X + (pad != null ? GlyphGap * s + glyph : 0f);
            var chip = new Vector2(Math.Max(16 * s, contentWidth + 10 * s), ChipHeight * s);
            var chipLift = _config.Style == TrailStyle.CompactRail && item.IsCurrent ? CompactChipLift : 0f;
            var chipMin = new Vector2(center.X - chip.X / 2,
                origin.Y + (top - ChipGap - ChipHeight - chipLift) * s - lift);
            dl.AddRectFilled(chipMin, chipMin + chip, U32(GlassTheme.Palette.Chip * new Vector4(1, 1, 1, textAlpha)), 5 * s);
            dl.AddRect(chipMin, chipMin + chip, U32(GlassTheme.Fg(0.22f * textAlpha)), 5 * s, ImDrawFlags.None, s);
            var textPos = new Vector2(chipMin.X + (chip.X - contentWidth) / 2, chipMin.Y + (chip.Y - textSize.Y) / 2);
            DrawText(dl, textPos, 11f, GlassTheme.Fg(textAlpha), label);
            if (pad is { } padButton)
                DrawPadGlyph(dl, new Vector2(textPos.X + textSize.X + GlyphGap * s + glyph / 2, chipMin.Y + chip.Y / 2),
                    glyph, padButton, textAlpha);
        }
        if (step.IsPrepull && step.At is { } prepullAt)
        {
            var tag = "−" + Math.Abs(prepullAt).ToString("0.0##", CultureInfo.InvariantCulture) + "s";
            var tagSize = TextSize(tag, 10f);
            var bottom = center.Y + item.Size * item.Scale / 2 * s;
            DrawText(dl, new Vector2(center.X - tagSize.X / 2, bottom + ChipGap * s - lift), 10f,
                GlassTheme.WithAlpha(Gold, textAlpha), tag);
        }
    }

    /// <summary>
    /// The game's cross hotbar glyph: four D-pad arms (a plus) or four face buttons (a diamond of dots), with the one
    /// to press lit and the others dim.
    /// </summary>
    private static void DrawPadGlyph(ImDrawListPtr dl, Vector2 center, float size, PadButton button, float alpha)
    {
        var s = Scale;
        var reach = size / 2 - s;
        for (var d = 0; d < 4; d++)
        {
            var direction = (PadDirection)d;
            var offset = direction switch
            {
                PadDirection.Left => new Vector2(-1, 0),
                PadDirection.Up => new Vector2(0, -1),
                PadDirection.Right => new Vector2(1, 0),
                _ => new Vector2(0, 1),
            };
            var lit = direction == button.Direction;
            var color = U32(GlassTheme.Fg((lit ? 1f : 0.3f) * alpha));
            if (button.Face)
            {
                var dot = center + offset * (reach * 0.62f);
                dl.AddCircleFilled(dot, reach * (lit ? 0.42f : 0.32f), color);
            }
            else
            {
                // An arm from the hub outward, as wide as a third of the glyph.
                var half = new Vector2(Math.Abs(offset.Y), Math.Abs(offset.X)) * (size / 6);
                var inner = center + offset * (size / 6);
                var outer = center + offset * reach;
                dl.AddRectFilled(Vector2.Min(inner, outer) - half, Vector2.Max(inner, outer) + half, color, s);
            }
        }
        if (!button.Face)
            dl.AddRectFilled(center - new Vector2(size / 6), center + new Vector2(size / 6), U32(GlassTheme.Fg(0.3f * alpha)));
    }

    private void DrawCaption(ImDrawListPtr dl, Vector2 origin, PracticeEngine engine)
    {
        var s = Scale;
        const float textPx = CaptionPx;
        var lineHeight = TextSize("Ag", textPx).Y;
        var pos = origin + new Vector2(CaptionInset * s, WindowHeight * s - 10 * s - lineHeight);

        if (engine.IsFinished)
        {
            DrawText(dl, pos, textPx, GlassTheme.Fg(1f), "Opener complete");
            return;
        }
        var step = _session.Sequence[engine.CurrentIndex];
        if (step == null) return;

        var kind = step.Ability.Category == AbilityCategory.Item ? "POTION" : step.Ability.IsGcd ? "GCD" : "WEAVE";
        var kindSize = TextSize(kind, 10f);
        var pad = new Vector2(6, 1) * s;
        var chipMin = new Vector2(pos.X, pos.Y + (lineHeight - kindSize.Y) / 2 - pad.Y);
        dl.AddRectFilled(chipMin, chipMin + kindSize + pad * 2, U32(GlassTheme.Fg(0.14f)), 5 * s);
        DrawText(dl, chipMin + pad, 10f, GlassTheme.Fg(0.85f), kind);

        var x = chipMin.X + kindSize.X + pad.X * 2 + 10 * s;
        x = DrawText(dl, new Vector2(x, pos.Y), textPx, GlassTheme.Fg(0.85f), "Next: ");
        var name = step.Ability.Category == AbilityCategory.Item && _config.PotionMatch == PotionMatch.AnyPotion
            ? "any potion"
            : step.Ability.Name;
        x = DrawText(dl, new Vector2(x, pos.Y), textPx, GlassTheme.Fg(1f), name);
        if (step.IsPrepull && step.At is { } at && engine.Clock is { } clock && clock < at)
            DrawText(dl, new Vector2(x, pos.Y), textPx, GlassTheme.Fg(0.85f),
                " · cast in " + (at - clock).ToString("0.0", CultureInfo.InvariantCulture) + "s");
    }

    /// <param name="rightGap">Room kept free at the right end for the Skip button, in screen pixels.</param>
    private void DrawCountdown(ImDrawListPtr dl, Vector2 origin, double? clock, float rightGap)
    {
        string? text = null;
        if (clock < 0) text = Math.Ceiling(-clock.Value).ToString(CultureInfo.InvariantCulture);
        else if (_time - _startShown < StartSeconds) text = "Start";
        if (text == null) return;

        var s = Scale;
        const float px = 34f;
        var size = TextSize(text, px);
        var pos = origin + new Vector2(_width * s - CaptionInset * s - rightGap - size.X, WindowHeight * s - 6 * s - size.Y);
        // A soft accent glow behind the number.
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4;
            var d = new Vector2(MathF.Cos(angle), MathF.Sin(angle) + 0.5f) * 3 * s;
            DrawText(dl, pos + d, px, GlassTheme.WithAlpha(Accent, 0.22f), text);
        }
        DrawText(dl, pos, px, GlassTheme.Fg(1f), text);
    }

    /// <summary>Soft rings outside a rounded rect, fading out over <paramref name="reach"/>.</summary>
    private static void DrawGlow(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, Vector4 color, float reach,
        float alpha)
    {
        const int rings = 8;
        for (var i = 1; i <= rings; i++)
        {
            var t = i / (float)rings;
            var grow = reach * t;
            var a = alpha * (1 - t) * (1 - t) / 2f;
            dl.AddRect(min - new Vector2(grow), max + new Vector2(grow), U32(GlassTheme.WithAlpha(color, a)),
                rounding + grow, ImDrawFlags.None, reach / rings + Scale);
        }
    }

    private static Vector2 TextSize(string text, float px) => ImGui.CalcTextSize(text) * (px * Scale / ImGui.GetFontSize());

    /// <summary>Draws text at <paramref name="px"/> (unscaled) and returns the x just after it.</summary>
    private static float DrawText(ImDrawListPtr dl, Vector2 pos, float px, Vector4 color, string text)
    {
        dl.AddText(ImGui.GetFont(), px * Scale, pos, U32(color), text);
        return pos.X + TextSize(text, px).X;
    }

    private static uint U32(Vector4 color) => ImGui.GetColorU32(color);

    public void Dispose()
    {
        _session.EngineEvent -= OnEngineEvent;
        _session.StepPressed -= OnStepPressed;
    }
}
