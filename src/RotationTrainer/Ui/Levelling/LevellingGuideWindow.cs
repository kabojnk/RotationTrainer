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

namespace RotationTrainer.Ui.Levelling;

/// <summary>
/// The levelling guide (<c>/rtrain level</c>): pick a job and level and see that band's short rotations, the cooldowns
/// and what unlocks in the band. "Pin as card" opens the compact <see cref="ReferenceCardWindow"/>.
/// </summary>
public sealed class LevellingGuideWindow : Window
{
    private enum Tab { Dungeon, Trial, Unlocks }

    private const string InfoPopup = "##levellingInfo";

    /// <summary>The Information popup: how the cards are put together, heading then text.</summary>
    private static readonly (string Heading, string Text)[] Info =
    {
        ("Game data",
            "Every ability's name, icon and unlock level come from the game's own data files. A card never lists an "
            + "ability above its band, and anything you haven't reached yet is greyed out with the level it unlocks at."),
        ("Combos and cooldowns",
            "The game data also links each combo step to the one before it, which sets the order of the loops. "
            + "Recast times decide what goes in Cooldowns: use those whenever they are ready."),
        ("Potency",
            "The game data does not store potencies in a form the plugin can read; they only appear in tooltip text. "
            + "So the order inside each band is written by hand from each job's tooltips at that level: finish the "
            + "combo, spend gauge before it caps, and use cooldowns on time."),
        ("Single target or AoE",
            "\"3 or more enemies\" is the enemy count where the AoE buttons deal more damage than the single-target "
            + "combo at that level. Below it, stay on single target."),
        ("Gear and stats",
            "Cards assume average gear for your level. While levelling, gear changes how fast you press buttons "
            + "(skill and spell speed), not which buttons are best, so the cards don't change with it."),
        ("Keybinds",
            "Keybinds are read from your hotbars, keyboard or controller, the same way the trail reads them."),
        ("Checks",
            "Each build tests every card against the game data, so names, unlock levels and level bands stay correct "
            + "after a patch."),
    };

    private static readonly (string Role, string[] Jobs)[] Roles =
    {
        ("Tank", new[] { "PLD", "WAR", "DRK", "GNB" }),
        ("Healer", new[] { "WHM", "SCH", "AST", "SGE" }),
        ("Melee", new[] { "MNK", "DRG", "NIN", "SAM", "RPR", "VPR" }),
        ("Ranged", new[] { "BRD", "MCH", "DNC" }),
        ("Caster", new[] { "BLM", "SMN", "RDM", "PCT" }),
    };

    private readonly Configuration _config;
    private readonly LevellingState _state;
    private readonly IAbilityCatalog _catalog;
    private readonly ITextureProvider _textures;
    private readonly CardChips _chips;
    private readonly Action _saveConfig;
    private Tab _tab = Tab.Dungeon;
    private IDisposable? _theme;

    public LevellingGuideWindow(Configuration config, LevellingState state, IAbilityCatalog catalog,
        ITextureProvider textures, CardChips chips, Action saveConfig)
        : base("Levelling guide###RotationTrainerLevelling",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _config = config;
        _state = state;
        _catalog = catalog;
        _textures = textures;
        _chips = chips;
        _saveConfig = saveConfig;
        Size = new Vector2(640, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(480, 420) };
    }

    private static float Scale => ImGuiHelpers.GlobalScale;

    public override void PreDraw() =>
        _theme = GlassTheme.PushWindow(_config.GlassOpacity, GlassTheme.AccentColor(_config.Accent));

    public override void PostDraw()
    {
        _theme?.Dispose();
        _theme = null;
    }

    public override void Draw()
    {
        GlassWidgets.DrawGlassBackground(_config.GlassOpacity);
        DrawTitle();
        DrawJobs();
        var job = _state.Selection.Job;
        if (job == null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                ImGui.TextWrapped("Log in to a combat job, or pick one above.");
            return;
        }

        DrawLevel(job);
        ImGui.Spacing();
        GlassWidgets.Segmented("tab", ref _tab, (Tab.Dungeon, "Dungeon"), (Tab.Trial, "Trial / boss"), (Tab.Unlocks, "Unlocks"));
        ImGui.Spacing();

        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        using (var body = ImRaii.Child("##body", new Vector2(0, -footer)))
        {
            if (body.Success)
            {
                if (_tab == Tab.Unlocks) DrawUnlocks(job);
                else if (_state.Card is { } card) DrawCard(card);
                else
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                        ImGui.TextWrapped(_state.Deck.HasJob(job)
                            ? "No card for this level."
                            : "Cards for this job are coming.");
                }
            }
        }
        DrawFooter();
    }

    private void DrawTitle()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Levelling guide");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            ImGui.TextUnformatted("Rotation Trainer");
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var info = FontAwesomeIcon.InfoCircle.ToIconString();
            var close = FontAwesomeIcon.Times.ToIconString();
            var pad = ImGui.GetStyle().FramePadding.X * 2;
            var width = ImGui.CalcTextSize(info).X + ImGui.CalcTextSize(close).X + pad * 2 + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - width);
            using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero).Push(ImGuiCol.Text, GlassTheme.Dim))
            {
                if (ImGui.Button(info)) ImGui.OpenPopup(InfoPopup);
                if (ImGui.IsItemHovered()) GlassWidgets.Tooltip("How the cards are chosen");
                ImGui.SameLine();
                if (ImGui.Button(close)) IsOpen = false;
            }
        }
        DrawInfo();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void DrawInfo()
    {
        using var popup = ImRaii.Popup(InfoPopup);
        if (!popup) return;
        var width = 420 * Scale;
        ImGui.TextUnformatted("How the cards are chosen");
        ImGui.Spacing();
        using (ImRaii.TextWrapPos(ImGui.GetCursorPosX() + width))
        {
            foreach (var (heading, text) in Info)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.CurrentAccent))
                    ImGui.TextUnformatted(heading);
                using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                    ImGui.TextUnformatted(text);
                ImGui.Spacing();
            }
        }
    }

    private void DrawJobs()
    {
        var s = Scale;
        var labelWidth = 56 * s;
        foreach (var (role, jobs) in Roles)
        {
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
                ImGui.TextUnformatted(role);
            ImGui.SameLine(labelWidth);
            foreach (var job in jobs)
            {
                var selected = _state.Selection.Job == job;
                var hasCards = _state.Deck.HasJob(job);
                bool clicked;
                if (selected)
                    clicked = GlassWidgets.PrimaryButton(job);
                else
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, hasCards ? GlassTheme.Ink : GlassTheme.Mute))
                        clicked = ImGui.Button(job);
                }
                if (!hasCards && ImGui.IsItemHovered()) GlassWidgets.Tooltip("Cards for this job are coming.");
                if (clicked) _state.PickJob(job);
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }
        ImGui.Spacing();
    }

    private void DrawLevel(string job)
    {
        var selection = _state.Selection;
        var (min, max) = _state.Band();
        var pos = ImGui.GetCursorScreenPos();
        var size = ImGui.GetFontSize() * 1.6f;
        var text = selection.Level.ToString();
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), size, pos, ImGui.GetColorU32(GlassTheme.Ink), text);
        ImGui.Dummy(new Vector2(ImGui.CalcTextSize(text).X * 1.6f, size));
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        var band = min == max ? $"Level {min}" : $"Levels {min}-{max}";
        var whose = selection.FollowsPlayer ? "your level" : _state.PlayerLevel > 0 ? $"you are {_state.PlayerLevel}" : null;
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextUnformatted(whose == null ? band : $"{band} · {whose}");
        if (!selection.FollowsPlayer && _state.PlayerLevel > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Use my level")) _state.Selection.UsePlayer();
        }

        var level = selection.Level;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##level", ref level, _catalog.StartLevel(job), 100)) selection.SetLevel(level);
    }

    private void DrawCard(LevellingCard card)
    {
        var level = _state.Selection.Level;
        var pull = new Section("Trash pull", card.Pull.Threshold > 0 ? $"{card.Pull.Threshold} or more enemies" : "packs",
            card.Pull.Steps, card.Pull.Note);
        var single = new Section("Single target", "bosses, last enemy left", card.Single.Steps, card.Single.Note);
        var order = _tab == Tab.Trial ? new[] { single, pull } : new[] { pull, single };
        foreach (var section in order) DrawSection(section, level);

        DrawList("Cooldowns", "use when ready", card.Weave, level);
        // Expanded on the Trial tab; one click away on the Dungeon tab.
        if (_tab == Tab.Trial)
            DrawList("Mitigation", "stagger these through the fight", card.Mitigation, level);
        else if (card.Mitigation.Count > 0 && ImGui.TreeNode("Mitigation##mit"))
        {
            DrawList("Mitigation", "stagger these", card.Mitigation, level);
            ImGui.TreePop();
        }
        if (card.Tip.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                ImGui.TextWrapped(card.Tip);
        }
    }

    private sealed record Section(string Title, string Subtitle, IReadOnlyList<CardStep> Steps, string Note);

    private void DrawSection(Section section, int level)
    {
        using (GlassWidgets.Card(section.Title))
        {
            Heading(section.Title, section.Subtitle);
            CardChips.Flow(section.Steps.Select(st => st.At(level)).ToList(), a => _chips.ChipWidth(a, level),
                a => _chips.Chip(a, level), "›");
            if (section.Note.Length > 0)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                    ImGui.TextWrapped(section.Note);
            }
        }
    }

    private void DrawList(string title, string subtitle, IReadOnlyList<CardStep> steps, int level)
    {
        if (steps.Count == 0) return;
        var abilities = steps.Select(st => st.At(level)).ToList();
        Heading(title, subtitle);
        CardChips.Flow(abilities, a => _chips.ChipWidth(a, level), a => _chips.Chip(a, level));
        ImGui.Spacing();
    }

    private static void Heading(string title, string subtitle)
    {
        ImGui.TextUnformatted(title);
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            ImGui.TextUnformatted(subtitle);
    }

    private void DrawUnlocks(string job)
    {
        var (min, max) = _state.Band();
        var level = _state.Selection.Level;
        var unlocks = _catalog.Unlocks(job, min, max);
        if (unlocks.Count == 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                ImGui.TextWrapped("Nothing new in this band.");
            return;
        }
        var s = Scale;
        foreach (var unlock in unlocks)
        {
            var done = unlock.Level <= level;
            var wrap = _textures.GetFromGameIcon(new GameIconLookup(unlock.IconId)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new Vector2(24 * s), Vector2.Zero, Vector2.One, new Vector4(1, 1, 1, done ? 1f : 0.35f));
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, done ? GlassTheme.CurrentAccent : GlassTheme.Mute))
                ImGui.TextUnformatted($"{unlock.Level,3}");
            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, done ? GlassTheme.Ink : GlassTheme.Dim))
                ImGui.TextUnformatted(unlock.IsTrait ? $"{unlock.Name} (trait)" : unlock.Name);
        }
    }

    private void DrawFooter()
    {
        if (GlassWidgets.PrimaryButton(_config.ShowLevellingCard ? "Unpin card" : "Pin as card"))
        {
            _config.ShowLevellingCard = !_config.ShowLevellingCard;
            _saveConfig();
        }
        if (!_config.ShowLevellingCard) return;
        ImGui.SameLine();
        var locked = _config.LockLevellingCard;
        if (ImGui.Checkbox("Lock card", ref locked))
        {
            _config.LockLevellingCard = locked;
            _saveConfig();
        }
        if (ImGui.IsItemHovered()) GlassWidgets.Tooltip("Locked: the card can't be moved and clicks pass through to the game.");
    }
}
