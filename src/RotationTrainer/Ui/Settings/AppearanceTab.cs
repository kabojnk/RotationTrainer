using Dalamud.Bindings.ImGui;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>Accent color, window and trail opacity, and the trail's backing.</summary>
internal sealed class AppearanceTab(Plugin plugin)
{
    private const int MinOpacityPercent = 20;
    private const float SakuraMinOpacity = 0.9f;

    public void Draw()
    {
        GlassWidgets.SectionHeader("Appearance", "The glass look for the trail and this window. 100% opacity is fully opaque.");
        var config = plugin.Config;

        using (GlassWidgets.Card("appearance"))
        {
            SettingsWindow.RowLabel("Theme");
            var theme = config.Theme;
            if (GlassWidgets.Segmented("theme", ref theme,
                    (Theme.Midnight, "Midnight"), (Theme.Graphite, "Graphite"), (Theme.Sakura, "Sakura")))
            {
                config.Theme = theme;
                if (theme == Theme.Sakura)
                {
                    // Made for its pink accent, and light glass turns muddy when the dark world shows through it.
                    // Both can be changed again afterwards.
                    config.Accent = Accent.Sakura;
                    config.GlassOpacity = Math.Max(config.GlassOpacity, SakuraMinOpacity);
                    config.TrailOpacity = Math.Max(config.TrailOpacity, SakuraMinOpacity);
                }
                plugin.SaveConfig();
            }

            SettingsWindow.RowLabel("Accent");
            var accent = config.Accent;
            if (GlassWidgets.Segmented("accent", ref accent,
                    (Accent.Sky, "Sky"), (Accent.Violet, "Violet"), (Accent.Mint, "Mint"), (Accent.Gold, "Gold"),
                    (Accent.Sakura, "Sakura")))
            {
                config.Accent = accent;
                plugin.SaveConfig();
            }

            SettingsWindow.RowLabel("Window opacity");
            var window = config.GlassOpacity;
            if (OpacitySlider("##windowOpacity", ref window)) config.GlassOpacity = window;

            SettingsWindow.RowLabel("Trail opacity");
            var trail = config.TrailOpacity;
            if (OpacitySlider("##trailOpacity", ref trail)) config.TrailOpacity = trail;

            SettingsWindow.RowLabel("Trail backing");
            var strip = config.TrailGlassStrip;
            if (GlassWidgets.Segmented("backing", ref strip, (true, "Glass strip"), (false, "None")))
            {
                config.TrailGlassStrip = strip;
                plugin.SaveConfig();
            }
        }
    }

    /// <summary>A 20–100% slider; saves when the drag ends.</summary>
    private bool OpacitySlider(string id, ref float opacity)
    {
        var percent = (int)MathF.Round(opacity * 100);
        ImGui.SetNextItemWidth(-1);
        var changed = ImGui.SliderInt(id, ref percent, MinOpacityPercent, 100, "%d%%");
        if (changed) opacity = Math.Clamp(percent, MinOpacityPercent, 100) / 100f;
        if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SaveConfig();
        return changed;
    }
}
