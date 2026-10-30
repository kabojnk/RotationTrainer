using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>The glass settings window (<c>/rtrain</c>): a sidebar of tabs and the selected tab's contents.</summary>
public sealed class SettingsWindow : Window
{
    private enum Tab { Practice, Trail, Timing, Library, Appearance }

    private const float SidebarWidth = 180f;
    private const float LabelWidth = 150f;

    private static readonly (Tab Tab, string Label, FontAwesomeIcon Icon)[] Tabs =
    {
        (Tab.Practice, "Practice", FontAwesomeIcon.Play),
        (Tab.Trail, "Trail", FontAwesomeIcon.Route),
        (Tab.Timing, "Speed & timing", FontAwesomeIcon.Stopwatch),
        (Tab.Library, "Rotation library", FontAwesomeIcon.Book),
        (Tab.Appearance, "Appearance", FontAwesomeIcon.Palette),
    };

    private readonly Plugin _plugin;
    private readonly PracticeTab _practice;
    private readonly TrailTab _trail;
    private readonly TimingTab _timing;
    private readonly LibraryTab _library;
    private readonly AppearanceTab _appearance;
    private readonly string _version;
    private Tab _tab = Tab.Practice;
    private IDisposable? _theme;

    public SettingsWindow(Plugin plugin) : base("Rotation Trainer###RotationTrainerSettings",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        _library = new LibraryTab(plugin);
        _practice = new PracticeTab(plugin, () =>
        {
            _tab = Tab.Library;
            _library.StartFileImport();
        });
        _trail = new TrailTab(plugin);
        _timing = new TimingTab(plugin);
        _appearance = new AppearanceTab(plugin);

        var v = typeof(Plugin).Assembly.GetName().Version;
        _version = v == null ? "" : $"v{v.Major}.{v.Minor}.{v.Build}";

        Size = new Vector2(820, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(620, 400) };
    }

    /// <summary>Draws the file dialogs; call every frame outside the window, so they keep the default style.</summary>
    public void DrawDialogs() => _library.DrawDialogs();

    public override void PreDraw() =>
        _theme = GlassTheme.PushWindow(_plugin.Config.GlassOpacity, GlassTheme.AccentColor(_plugin.Config.Accent));

    public override void PostDraw()
    {
        _theme?.Dispose();
        _theme = null;
    }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        GlassWidgets.DrawGlassBackground(_plugin.Config.GlassOpacity);
        DrawTitle();

        var avail = ImGui.GetContentRegionAvail();
        using (var side = ImRaii.Child("##sidebar", new Vector2(SidebarWidth * scale, avail.Y)))
        {
            if (side.Success) DrawSidebar();
        }

        ImGui.SameLine();
        var lineX = ImGui.GetCursorScreenPos().X;
        var top = ImGui.GetCursorScreenPos().Y;
        ImGui.GetWindowDrawList().AddLine(new Vector2(lineX, top), new Vector2(lineX, top + avail.Y),
            ImGui.GetColorU32(GlassTheme.White(0.10f)), scale);
        ImGui.SetCursorScreenPos(new Vector2(lineX + scale, top));

        using var main = ImRaii.Child("##content", ImGui.GetContentRegionAvail(), false,
            ImGuiWindowFlags.AlwaysUseWindowPadding);
        if (!main.Success) return;
        switch (_tab)
        {
            case Tab.Practice: _practice.Draw(); break;
            case Tab.Trail: _trail.Draw(); break;
            case Tab.Timing: _timing.Draw(); break;
            case Tab.Library: _library.Draw(); break;
            case Tab.Appearance: _appearance.Draw(); break;
        }
    }

    /// <summary>A dim label at the start of a settings row; the control follows on the same line.</summary>
    internal static void RowLabel(string label)
    {
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextUnformatted(label);
        ImGui.SameLine(LabelWidth * ImGuiHelpers.GlobalScale);
    }

    /// <summary>Dim text that follows the previous item on the same line.</summary>
    internal static void DimAfter(string text)
    {
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            ImGui.TextUnformatted(text);
    }

    private void DrawTitle()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Rotation Trainer");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            ImGui.TextUnformatted(_version);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var icon = FontAwesomeIcon.Times.ToIconString();
            var buttonWidth = ImGui.CalcTextSize(icon).X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - buttonWidth);
            using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero).Push(ImGuiCol.Text, GlassTheme.Dim))
            {
                if (ImGui.Button(icon)) IsOpen = false;
            }
        }

        var y = ImGui.GetCursorScreenPos().Y;
        var min = ImGui.GetWindowPos();
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, y), new Vector2(min.X + ImGui.GetWindowSize().X, y),
            ImGui.GetColorU32(GlassTheme.White(0.10f)), scale);
        ImGui.Dummy(new Vector2(0, 4 * scale));
    }

    private void DrawSidebar()
    {
        foreach (var (tab, label, icon) in Tabs)
        {
            if (GlassWidgets.SidebarTab(label, icon, _tab == tab)) _tab = tab;
        }

        ImGui.Dummy(new Vector2(0, 10 * ImGuiHelpers.GlobalScale));
        var session = _plugin.Session;
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextUnformatted("Current job");
        ImGui.TextUnformatted(session.Job ?? "None");
        if (session.ReferenceGcd is { } gcd)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
                ImGui.TextWrapped($"GCD {session.Timing.RecastSeconds(gcd):0.00}s");
        }
    }
}
