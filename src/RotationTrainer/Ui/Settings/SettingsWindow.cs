using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace RotationTrainer.Ui.Settings;

/// <summary>Placeholder until the settings UI is built.</summary>
public sealed class SettingsWindow : Window
{
    public SettingsWindow() : base("Rotation Trainer")
    {
        Size = new System.Numerics.Vector2(480, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() => ImGui.TextUnformatted("Settings are coming soon.");
}
