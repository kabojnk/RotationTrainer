using Dalamud.Bindings.ImGui;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>Accent color, glass opacity and the trail's backing.</summary>
internal sealed class AppearanceTab(Plugin plugin)
{
    private const int MinOpacityPercent = 20;

    public void Draw()
    {
        GlassWidgets.SectionHeader("Appearance", "The glass look for the trail and this window.");
        var config = plugin.Config;

        using (GlassWidgets.Card("appearance"))
        {
            SettingsWindow.RowLabel("Accent");
            var accent = config.Accent;
            if (GlassWidgets.Segmented("accent", ref accent,
                    (Accent.Sky, "Sky"), (Accent.Violet, "Violet"), (Accent.Mint, "Mint"), (Accent.Gold, "Gold")))
            {
                config.Accent = accent;
                plugin.SaveConfig();
            }

            SettingsWindow.RowLabel("Glass opacity");
            var percent = (int)MathF.Round(config.GlassOpacity * 100);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderInt("##opacity", ref percent, MinOpacityPercent, 100, "%d%%"))
                config.GlassOpacity = Math.Clamp(percent, MinOpacityPercent, 100) / 100f;
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SaveConfig();

            SettingsWindow.RowLabel("Trail backing");
            var strip = config.TrailGlassStrip;
            if (GlassWidgets.Segmented("backing", ref strip, (true, "Glass strip"), (false, "None")))
            {
                config.TrailGlassStrip = strip;
                plugin.SaveConfig();
            }
        }
    }
}
