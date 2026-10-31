using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RotationTrainer.Core;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>The GCD the game reports, and custom speed stats for practicing another gear set.</summary>
internal sealed class TimingTab(Plugin plugin)
{
    private const int BaseGcdMs = 2500;
    private const int MaxSpeed = 9999;
    private const int MaxLatencyMs = 500;

    public void Draw()
    {
        GlassWidgets.SectionHeader("Speed & timing", "Used by Auto advance and the Timeline style.");
        var config = plugin.Config;
        var half = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2;

        using (GlassWidgets.Card("read", half))
        {
            Label("Read from your character");
            var gcd = plugin.Session.ReferenceGcd;
            var dl = ImGui.GetWindowDrawList();
            var fontSize = ImGui.GetFontSize() * 1.6f;
            var text = gcd == null ? "-" : $"{plugin.Session.GameTiming.RecastSeconds(gcd):0.00}s";
            dl.AddText(ImGui.GetFont(), fontSize, ImGui.GetCursorScreenPos(), ImGui.GetColorU32(GlassTheme.Ink), text);
            ImGui.Dummy(new Vector2(ImGui.CalcTextSize(text).X * 1.6f, fontSize));
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            {
                ImGui.TextWrapped(gcd == null
                    ? "Log in to a combat job to read your GCD."
                    : $"GCD of {gcd.Name} · updates when you change gear");
            }
        }

        ImGui.SameLine();
        using (GlassWidgets.Card("source", half))
        {
            Label("Source");
            var custom = config.UseCustomSpeed;
            if (GlassWidgets.Segmented("source", ref custom, (false, "My character"), (true, "Custom")))
            {
                config.UseCustomSpeed = custom;
                plugin.SaveConfig();
            }
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
                ImGui.TextWrapped("Custom lets you practise another gear set's speed.");
        }

        using (GlassWidgets.Card("stats"))
        {
            Label("Custom stats");
            using (ImRaii.Disabled(!config.UseCustomSpeed))
            {
                SpeedInput("Spell speed", "##spell", config.CustomSpellSpeed, v => config.CustomSpellSpeed = v);
                SpeedInput("Skill speed", "##skill", config.CustomSkillSpeed, v => config.CustomSkillSpeed = v);
            }

            NumberInput("Latency allowance", "##latency", config.LatencyMs, 0, MaxLatencyMs, v => config.LatencyMs = v);
            SettingsWindow.DimAfter("ms per weave");
        }
    }

    private void SpeedInput(string label, string id, int current, Action<int> set)
    {
        var value = NumberInput(label, id, current, SpeedMath.Sub, MaxSpeed, set);
        SettingsWindow.DimAfter($"→ GCD {SpeedMath.AdjustSeconds(BaseGcdMs, Math.Clamp(value, SpeedMath.Sub, MaxSpeed)):0.00}s");
    }

    /// <summary>
    /// A number field that writes while typing and clamps and saves once the edit finishes (saving rebuilds
    /// the practice session). Returns the value shown.
    /// </summary>
    private int NumberInput(string label, string id, int current, int min, int max, Action<int> set)
    {
        SettingsWindow.RowLabel(label);
        var value = current;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(id, ref value, 0, 0)) set(value);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            value = Math.Clamp(value, min, max);
            set(value);
            plugin.SaveConfig();
        }
        return value;
    }

    private static void Label(string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextUnformatted(text);
    }
}
