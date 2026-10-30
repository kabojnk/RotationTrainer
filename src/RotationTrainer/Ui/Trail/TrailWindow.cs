using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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
/// this window adds the icons, keybind chips, the slide, pop and flash animations and the caption.
/// Sizes are unscaled pixels from the approved mockup; everything is multiplied by the UI scale when drawn.
/// </summary>
public sealed class TrailWindow : Window, IDisposable
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNav;

    private const float MarkerX = 110f;
    private const float TrackTop = 12f;
    private const float RowTop = 34f;
    private const float StripInset = 14f;
    private const float StripRounding = 20f;
    private const float FooterHeight = 40f;
    private const float MinWidth = MarkerX + 360f;
    private const float ChipHeight = 16f;
    private const float ChipGap = 3f;
    private const float FocusChipLift = 10f;
    private const int FocusMaxAhead = 5;

    private const double SlideSeconds = 0.28;
    private const float WidthEaseSeconds = 0.07f;
    private const double PopSeconds = 0.22;
    private const float PopScale = 1.35f;
    private const float PopLift = 6f;
    private const double FlashSeconds = 0.4;
    private const double StartSeconds = 0.8;
    private const double PulseHalfPeriod = 0.6;

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
    // Engine events arrive on the framework update; they are applied on the next draw.
    private readonly ConcurrentQueue<EngineEvent> _events = new();

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

    public TrailWindow(Configuration config, PracticeSession session, ITextureProvider textures, IClientState clientState)
        : base("Rotation Trainer Trail###RotationTrainerTrail", BaseFlags)
    {
        _config = config;
        _session = session;
        _textures = textures;
        _clientState = clientState;
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        DisableFadeInFadeOut = true;
        AllowPinning = false;
        AllowClickthrough = false;
        _session.EngineEvent += OnEngineEvent;
    }

    private static float Scale => ImGuiHelpers.GlobalScale;

    private TrailMetrics Metrics => TrailMetrics.For(_config.Size);

    private float Raise => (_config.Style == TrailStyle.Timeline ? 0.95f : 0.6f) * Metrics.Gcd;

    private float GcdTop => TrackTop + RowTop + Raise;

    private float StripHeight => RowTop + Raise + Metrics.Gcd + (_config.Style == TrailStyle.Timeline ? 26f : 22f);

    private float WindowHeight => TrackTop + StripHeight + FooterHeight;

    private Vector4 Accent => GlassTheme.AccentColor(_config.Accent);

    private bool FollowsClock =>
        _config.Style == TrailStyle.Timeline && _config.Advance == AdvanceMode.Auto && _engine?.Clock != null;

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
    }

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
            var ahead = _config.Style == TrailStyle.Focus
                ? Math.Min(_config.AbilitiesAhead, FocusMaxAhead)
                : _config.AbilitiesAhead;
            var inputs = new List<TrailInput>(ahead + 1);
            for (var i = current; i <= current + ahead && _session.Sequence[i] is { } step; i++)
                inputs.Add(new TrailInput(i, step.Ability.IsGcd, engine.ExpectedTime(i)));
            _reference = _config.Advance == AdvanceMode.Auto && clock != null ? clock.Value : engine.ExpectedTime(current);
            _items = TrailLayout.Layout(_config.Style, _config.Size, inputs, _reference, MarkerX);
            // The window is sized from the layout at the current step's expected time, so a Timeline following
            // the clock (e.g. early in a long countdown) never widens it; the clip rect hides the overflow.
            extent = _config.Style == TrailStyle.Timeline && _config.Advance == AdvanceMode.Auto
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
        _pops.RemoveAll(p => _time - p.Start >= PopSeconds);

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

    private void ResetAnimation()
    {
        _current = -1;
        _last = new Dictionary<int, TrailItem>();
        _pops.Clear();
        _slideFrom = 0;
        _slideStart = double.NegativeInfinity;
        _flashIndex = -1;
        _lastClock = null;
        _startShown = double.NegativeInfinity;
        _pullX = null;
        _events.Clear();
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

        if (!_config.LockTrail)
        {
            var max = origin + new Vector2(width, WindowHeight) * s;
            dl.AddRectFilled(origin, max, U32(GlassTheme.White(0.04f)), 10 * s);
            dl.AddRect(origin, max, U32(GlassTheme.WithAlpha(Accent, 0.55f)), 10 * s, ImDrawFlags.None, 1.5f * s);
        }

        var stripMin = origin + new Vector2(StripInset, TrackTop) * s;
        var stripMax = origin + new Vector2(width - StripInset, TrackTop + StripHeight) * s;
        if (_config.TrailGlassStrip) DrawStrip(dl, stripMin, stripMax);

        var engine = _session.Engine;
        if (engine == null || _session.Sequence.IsEmpty)
        {
            const string empty = "Pick an opener and loop with /rtrain";
            var size = TextSize(empty, 15f);
            DrawText(dl, (stripMin + stripMax) / 2 - size / 2, 15f, GlassTheme.White(0.85f), empty);
            return;
        }

        dl.PushClipRect(stripMin - new Vector2(StripInset, TrackTop) * s, stripMax + new Vector2(StripInset, 0) * s, true);
        if (_config.Style == TrailStyle.Rail) DrawRail(dl, origin, stripMin, stripMax);
        if (_config.Style == TrailStyle.Timeline) DrawTicks(dl, origin, stripMin, stripMax);
        DrawPull(dl, origin);

        foreach (var pop in _pops)
        {
            var t = EaseOut((float)((_time - pop.Start) / PopSeconds));
            DrawStep(dl, origin, pop.Step, pop.Item with { CenterX = pop.X }, 1f - t, t);
        }
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];
            if (_session.Sequence[item.Index] is { } step) DrawStep(dl, origin, step, item, 1f, 0f);
        }
        if (_config.Style == TrailStyle.Timeline) DrawMarker(dl, origin);
        dl.PopClipRect();

        DrawCaption(dl, origin, engine);
        DrawCountdown(dl, origin, engine.Clock);
    }

    private void DrawStrip(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var o = Math.Clamp(_config.GlassOpacity, 0f, 1f);
        var s = Scale;
        var rounding = StripRounding * s;

        // Shadow 0 10px 30px rgba(0,0,0,.35), outside the strip only.
        const int rings = 10;
        var reach = 30 * s;
        for (var i = 1; i <= rings; i++)
        {
            var t = i / (float)rings;
            var grow = reach * t * 0.5f;
            var offset = new Vector2(0, 10 * s * t);
            var alpha = 0.35f * o * (1 - t) * (1 - t) / 2.5f;
            dl.AddRect(min - new Vector2(grow) + offset, max + new Vector2(grow) + offset, U32(new Vector4(0, 0, 0, alpha)),
                rounding + grow, ImDrawFlags.None, reach / rings + Scale);
        }

        // Vertical gradient rgba(255,255,255,.14) → .05, drawn as whole-pixel bands of the same rounded shape.
        const int bands = 8;
        for (var k = 0; k < bands; k++)
        {
            var y0 = MathF.Round(min.Y + (max.Y - min.Y) * k / bands);
            var y1 = MathF.Round(min.Y + (max.Y - min.Y) * (k + 1) / bands);
            var alpha = (0.14f + (0.05f - 0.14f) * (k + 0.5f) / bands) * o;
            dl.PushClipRect(new Vector2(min.X, y0), new Vector2(max.X, y1), true);
            dl.AddRectFilled(min, max, U32(GlassTheme.White(alpha)), rounding);
            dl.PopClipRect();
        }
        dl.AddRect(min, max, U32(GlassTheme.White(0.2f * o)), rounding, ImDrawFlags.None, s);
        dl.AddLine(new Vector2(min.X + rounding, min.Y + s), new Vector2(max.X - rounding, min.Y + s),
            U32(GlassTheme.White(0.35f * o)), s);
    }

    private void DrawRail(ImDrawListPtr dl, Vector2 origin, Vector2 stripMin, Vector2 stripMax)
    {
        var s = Scale;
        var y = origin.Y + (GcdTop + Metrics.Gcd / 2 - 3) * s;
        dl.AddRectFilled(new Vector2(stripMin.X, y), new Vector2(stripMax.X, y + 6 * s), U32(GlassTheme.White(0.18f)), 3 * s);
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
            dl.AddRectFilled(new Vector2(x, top), new Vector2(x + s, top + (big ? 12 : 6) * s), U32(GlassTheme.White(0.18f)));
            if (big && t != 0)
                DrawText(dl, new Vector2(x + 4 * s, top - 16 * s), 11f, GlassTheme.White(0.55f),
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
        var top = origin.Y + (TrackTop + 14) * s;
        dl.AddRectFilled(new Vector2(left, top), new Vector2(left + 2 * s, top + (StripHeight - 20) * s),
            U32(GlassTheme.White(0.4f)));
        DrawText(dl, new Vector2(left + 6 * s, top - 2 * s), 12f, GlassTheme.White(0.85f), "Pull");
    }

    private void DrawMarker(ImDrawListPtr dl, Vector2 origin)
    {
        var s = Scale;
        var min = origin + new Vector2(MarkerX - 1, 18) * s;
        var max = min + new Vector2(2, StripHeight - 12) * s;
        DrawGlow(dl, min, max, s, Accent, 12 * s, 0.9f);
        dl.AddRectFilled(min, max, U32(GlassTheme.White(0.55f)), s);
    }

    /// <param name="alpha">Overall opacity (fades popped steps).</param>
    /// <param name="pop">0..1 progress of the pop animation; 0 for live steps.</param>
    private void DrawStep(ImDrawListPtr dl, Vector2 origin, PlannedStep step, TrailItem item, float alpha, float pop)
    {
        var s = Scale;
        var top = item.Row == TrailRow.Gcd ? GcdTop : GcdTop - Raise;
        var center = origin + new Vector2(item.CenterX + _offset, top + item.Size / 2) * s;
        var tileScale = item.Scale * (1 + (PopScale - 1) * pop);
        var half = item.Size * tileScale / 2 * s;
        var min = center - new Vector2(half);
        var max = center + new Vector2(half);
        var rounding = item.Size * tileScale * 0.22f * s;
        var live = pop == 0f;

        // Drop shadow, then the current-step ring and glow (gold and pulsing once a pre-pull step is due).
        if (alpha > 0.01f) DrawGlow(dl, min + new Vector2(0, 3 * s), max + new Vector2(0, 3 * s), rounding, Vector4.Zero, 8 * s, 0.45f * alpha);
        if (live && item.IsCurrent)
        {
            var clock = _engine?.Clock;
            var due = step.IsPrepull && step.At is { } at && clock >= at;
            var p = due ? (float)(0.5 - 0.5 * Math.Cos(Math.PI * _time / PulseHalfPeriod)) : 0f;
            var ring = Vector4.Lerp(GlassTheme.White(0.95f), Gold, p);
            var glow = Vector4.Lerp(Accent, Gold, p);
            DrawGlow(dl, min, max, rounding, glow, (22 + 6 * p) * s, 0.9f);
            dl.AddRect(min - new Vector2(1.5f * s), max + new Vector2(1.5f * s), U32(ring), rounding + 1.5f * s,
                ImDrawFlags.None, 3 * s);
        }

        var wrap = _textures.GetFromGameIcon(new GameIconLookup(step.Ability.IconId)).GetWrapOrEmpty();
        dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, U32(new Vector4(1, 1, 1, alpha)), rounding);
        dl.AddRect(min, max, U32(GlassTheme.White(0.35f * alpha)), rounding, ImDrawFlags.None, s);

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
            var label = _session.Keybinds.Label(step.Ability.Type, step.Ability.Id, _session.InputMode);
            var textSize = TextSize(label, 11f);
            var chip = new Vector2(Math.Max(16 * s, textSize.X + 10 * s), ChipHeight * s);
            var chipLift = _config.Style == TrailStyle.Focus && item.IsCurrent ? FocusChipLift : 0f;
            var chipMin = new Vector2(center.X - chip.X / 2,
                origin.Y + (top - ChipGap - ChipHeight - chipLift) * s - lift);
            dl.AddRectFilled(chipMin, chipMin + chip, U32(new Vector4(10 / 255f, 12 / 255f, 20 / 255f, 0.7f * textAlpha)), 5 * s);
            dl.AddRect(chipMin, chipMin + chip, U32(GlassTheme.White(0.22f * textAlpha)), 5 * s, ImDrawFlags.None, s);
            DrawText(dl, chipMin + (chip - textSize) / 2, 11f, GlassTheme.White(textAlpha), label);
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

    private void DrawCaption(ImDrawListPtr dl, Vector2 origin, PracticeEngine engine)
    {
        var s = Scale;
        const float textPx = 13f;
        var lineHeight = TextSize("Ag", textPx).Y;
        var pos = origin + new Vector2(18 * s, WindowHeight * s - 10 * s - lineHeight);

        if (engine.IsFinished)
        {
            DrawText(dl, pos, textPx, GlassTheme.White(1f), "Opener complete");
            return;
        }
        var step = _session.Sequence[engine.CurrentIndex];
        if (step == null) return;

        var kind = step.Ability.Category == AbilityCategory.Item ? "POTION" : step.Ability.IsGcd ? "GCD" : "WEAVE";
        var kindSize = TextSize(kind, 10f);
        var pad = new Vector2(6, 1) * s;
        var chipMin = new Vector2(pos.X, pos.Y + (lineHeight - kindSize.Y) / 2 - pad.Y);
        dl.AddRectFilled(chipMin, chipMin + kindSize + pad * 2, U32(GlassTheme.White(0.14f)), 5 * s);
        DrawText(dl, chipMin + pad, 10f, GlassTheme.White(0.85f), kind);

        var x = chipMin.X + kindSize.X + pad.X * 2 + 10 * s;
        x = DrawText(dl, new Vector2(x, pos.Y), textPx, GlassTheme.White(0.85f), "Next: ");
        x = DrawText(dl, new Vector2(x, pos.Y), textPx, GlassTheme.White(1f), step.Ability.Name);
        if (step.IsPrepull && step.At is { } at && engine.Clock is { } clock && clock < at)
            DrawText(dl, new Vector2(x, pos.Y), textPx, GlassTheme.White(0.85f),
                " · cast in " + (at - clock).ToString("0.0", CultureInfo.InvariantCulture) + "s");
    }

    private void DrawCountdown(ImDrawListPtr dl, Vector2 origin, double? clock)
    {
        string? text = null;
        if (clock < 0) text = Math.Ceiling(-clock.Value).ToString(CultureInfo.InvariantCulture);
        else if (_time - _startShown < StartSeconds) text = "Start";
        if (text == null) return;

        var s = Scale;
        const float px = 34f;
        var size = TextSize(text, px);
        var pos = origin + new Vector2(_width * s - 22 * s - size.X, WindowHeight * s - 6 * s - size.Y);
        // A soft accent glow behind the number.
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4;
            var d = new Vector2(MathF.Cos(angle), MathF.Sin(angle) + 0.5f) * 3 * s;
            DrawText(dl, pos + d, px, GlassTheme.WithAlpha(Accent, 0.22f), text);
        }
        DrawText(dl, pos, px, GlassTheme.White(1f), text);
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

    public void Dispose() => _session.EngineEvent -= OnEngineEvent;
}
