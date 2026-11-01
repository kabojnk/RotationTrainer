using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using RotationTrainer.Core;
using RotationTrainer.Game;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Levelling;

/// <summary>
/// The pinned levelling card: the current band's loops as icons with their keybinds, small enough to sit beside a
/// hotbar. Follows the guide's job and level. Locked, it can't be moved and clicks pass through.
/// </summary>
public sealed class ReferenceCardWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;

    private const float IconSize = 30f;
    private const float MinWidth = 240f;

    private readonly Configuration _config;
    private readonly LevellingState _state;
    private readonly IClientState _clientState;
    private readonly CardChips _chips;
    private readonly Action _openGuide;
    private readonly Action _saveConfig;
    private IDisposable? _theme;

    public ReferenceCardWindow(Configuration config, LevellingState state, IClientState clientState, CardChips chips,
        Action openGuide, Action saveConfig)
        : base("Levelling card###RotationTrainerLevellingCard", BaseFlags)
    {
        _config = config;
        _state = state;
        _clientState = clientState;
        _chips = chips;
        _openGuide = openGuide;
        _saveConfig = saveConfig;
        IsOpen = true;
        RespectCloseHotkey = false;
        AllowPinning = false;
        AllowClickthrough = false;
    }

    private static float Scale => ImGuiHelpers.GlobalScale;

    public override bool DrawConditions() => _config.ShowLevellingCard && _clientState.IsLoggedIn;

    public override void PreDraw()
    {
        Flags = BaseFlags | (_config.LockLevellingCard ? ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMove : 0);
        _theme = GlassTheme.PushWindow(_config.LevellingCardOpacity, GlassTheme.AccentColor(_config.Accent));
    }

    public override void PostDraw()
    {
        _theme?.Dispose();
        _theme = null;
    }

    public override void Draw()
    {
        var s = Scale;
        GlassWidgets.DrawGlassBackground(_config.LevellingCardOpacity);
        var card = _state.Card;
        var level = _state.Selection.Level;
        var width = MinWidth * s;
        if (card != null)
        {
            var most = new[] { card.Single.Steps.Count, card.Pull.Steps.Count, card.Weave.Count, card.Mitigation.Count }.Max();
            width = Math.Max(width, most * (IconSize + 6) * s);
        }

        DrawTitle(width, level);
        if (card == null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                ImGui.TextUnformatted(_state.Selection.Job is { } job && !_state.Deck.HasJob(job)
                    ? "Cards for this job are coming."
                    : "No card for this level.");
            return;
        }

        Row("Single target", "loop", card.Single.Steps, level);
        Row(card.Pull.Threshold > 0 ? $"{card.Pull.Threshold}+ enemies" : "Pull", "loop", card.Pull.Steps, level);
        Row("Cooldowns", "when ready", card.Weave, level);
        Row("Mitigation", "stagger", card.Mitigation, level);
        if (card.Tip.Length > 0)
        {
            ImGui.Separator();
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            using (ImRaii.TextWrapPos(ImGui.GetCursorPosX() + width))
                ImGui.TextUnformatted(card.Tip);
        }
    }

    private void DrawTitle(float width, int level)
    {
        var start = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(_state.DisplayJob ?? "");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            ImGui.TextUnformatted($"Lv {level}");

        if (_config.LockLevellingCard) return;
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        using var colors = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero).Push(ImGuiCol.Text, GlassTheme.Dim);
        var cog = FontAwesomeIcon.Cog.ToIconString();
        var close = FontAwesomeIcon.Times.ToIconString();
        var pad = ImGui.GetStyle().FramePadding.X * 2;
        var buttons = ImGui.CalcTextSize(cog).X + ImGui.CalcTextSize(close).X + pad * 2 + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine(start + width - buttons);
        if (ImGui.Button(cog)) _openGuide();
        ImGui.SameLine();
        if (ImGui.Button(close))
        {
            _config.ShowLevellingCard = false;
            _saveConfig();
        }
    }

    private void Row(string label, string hint, IReadOnlyList<Ability> abilities, int level)
    {
        if (abilities.Count == 0) return;
        var s = Scale;
        ImGui.TextColored(GlassTheme.Mute, label.ToUpperInvariant());
        ImGui.SameLine();
        ImGui.TextColored(GlassTheme.Mute, hint);
        for (var i = 0; i < abilities.Count; i++)
        {
            if (i > 0) ImGui.SameLine(0, 6 * s);
            _chips.Icon(abilities[i], IconSize, level);
        }
        ImGui.Dummy(new Vector2(0, 4 * s));
    }
}
