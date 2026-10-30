using Dalamud.Bindings.ImGui;
using RotationTrainer.Core;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>How the trail looks and behaves on screen.</summary>
internal sealed class TrailTab(Plugin plugin)
{
    public void Draw()
    {
        GlassWidgets.SectionHeader("Trail", "How the trail looks on screen.");
        var config = plugin.Config;

        using (GlassWidgets.Card("look"))
        {
            SettingsWindow.RowLabel("Style");
            var style = config.Style;
            if (GlassWidgets.Segmented("style", ref style,
                    (TrailStyle.Rail, "Rail"), (TrailStyle.CompactRail, "Compact Rail"), (TrailStyle.Timeline, "Timeline")))
            {
                config.Style = style;
                plugin.SaveConfig();
            }

            SettingsWindow.RowLabel("Size");
            var size = config.Size;
            if (GlassWidgets.Segmented("size", ref size, (TrailSize.Big, "Big"), (TrailSize.Small, "Small")))
            {
                config.Size = size;
                plugin.SaveConfig();
            }

            SettingsWindow.RowLabel("Advance");
            var advance = config.Advance;
            if (GlassWidgets.Segmented("advance", ref advance,
                    (AdvanceMode.Manual, "When I use it"), (AdvanceMode.Auto, "Auto")))
            {
                config.Advance = advance;
                plugin.SaveConfig();
            }

            SettingsWindow.RowLabel("Abilities ahead");
            var ahead = config.AbilitiesAhead;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderInt("##ahead", ref ahead, 3, 15)) config.AbilitiesAhead = Math.Clamp(ahead, 3, 15);
            if (ImGui.IsItemDeactivatedAfterEdit()) plugin.SaveConfig();
        }

        using (GlassWidgets.Card("behavior"))
        {
            SettingsWindow.RowLabel("Keybind labels");
            var keybinds = config.Keybinds;
            if (GlassWidgets.Segmented("keybinds", ref keybinds, (KeybindMode.Auto, "Auto-detect"),
                    (KeybindMode.Keyboard, "Keyboard"), (KeybindMode.Controller, "Controller"), (KeybindMode.Off, "Off")))
            {
                config.Keybinds = keybinds;
                plugin.SaveConfig();
            }

            var flash = config.FlashWrong;
            if (GlassWidgets.Toggle("flash", "Flash red on a wrong ability", "The trail waits for the right one.",
                    ref flash))
            {
                config.FlashWrong = flash;
                plugin.SaveConfig();
            }

            var locked = config.LockTrail;
            if (GlassWidgets.Toggle("lock", "Lock position", "Unlock to drag the trail anywhere on screen.", ref locked))
            {
                config.LockTrail = locked;
                plugin.SaveConfig();
            }
        }
    }
}
