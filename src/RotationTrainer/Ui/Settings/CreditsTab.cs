using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>Who made the plugin, and where the built-in rotations come from.</summary>
internal sealed class CreditsTab(Plugin plugin)
{
    private const string BalanceUrl = "https://www.thebalanceffxiv.com";

    public void Draw()
    {
        GlassWidgets.SectionHeader("Credits", "Rotation Trainer is made by Big Bear.");

        using (GlassWidgets.Card("balance"))
        {
            ImGui.TextUnformatted("The Balance");
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            {
                ImGui.TextWrapped($"The {plugin.BuiltInRotations.Count} built-in openers and loops are transcribed from "
                    + "the opener charts in The Balance's job guides. Each one names the chart it came from in its "
                    + "source. Thank you to The Balance's guide writers; read their guides for the reasoning behind "
                    + "each opener and how to adapt it to a fight.");
            }
            ImGui.Spacing();
            if (ImGui.Button("Open thebalanceffxiv.com")) Util.OpenLink(BalanceUrl);
            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute)) ImGui.TextUnformatted(BalanceUrl);
        }
    }
}
