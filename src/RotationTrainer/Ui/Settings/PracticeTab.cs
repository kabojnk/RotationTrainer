using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RotationTrainer.Core;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>Picks the opener and loop for the current job and the practice toggles.</summary>
internal sealed class PracticeTab(Plugin plugin, Action importFile)
{
    private const int PreviewSteps = 11;

    public void Draw()
    {
        GlassWidgets.SectionHeader("Practice", "Choose what to drill on your current job.");
        var config = plugin.Config;
        var job = plugin.Session.Job;

        using (GlassWidgets.Card("selection"))
        {
            if (job == null)
                ImGui.TextWrapped("Log in to a combat job to pick rotations.");
            else
                DrawSelection(job);
        }

        using (GlassWidgets.Card("toggles"))
        {
            var potions = config.UsePotions;
            if (GlassWidgets.Toggle("potions", "Use potions",
                    "Adds potion weaves to openers that have them. Off by default because potions are consumed.",
                    ref potions))
            {
                config.UsePotions = potions;
                plugin.SaveConfig();
            }
            if (config.UsePotions)
            {
                SettingsWindow.RowLabel("Potion counts");
                var match = config.PotionMatch;
                if (GlassWidgets.Segmented("potionMatch", ref match,
                        (PotionMatch.AnyPotion, "Any potion"), (PotionMatch.Exact, "Exact potion")))
                {
                    config.PotionMatch = match;
                    plugin.SaveConfig();
                }
                if (ImGui.IsItemHovered())
                    GlassWidgets.Tooltip("Any potion: any main-stat tincture or gemdraught counts, so you can practise with "
                        + "cheap ones.\nExact potion: only the one the rotation names counts.");
            }

            var countdown = config.StartWithCountdown;
            if (GlassWidgets.Toggle("countdown", "Start with in-game countdown",
                    "Typing /countdown lines up pre-pull steps like Rainbow Drip at 4.5.", ref countdown))
            {
                config.StartWithCountdown = countdown;
                plugin.SaveConfig();
            }

            var reset = config.ResetOnLeavingCombat;
            if (GlassWidgets.Toggle("reset", "Reset when leaving combat", null, ref reset))
            {
                config.ResetOnLeavingCombat = reset;
                plugin.SaveConfig();
            }
        }

        if (GlassWidgets.PrimaryButton(config.ShowTrail ? "Hide trail" : "Show trail"))
        {
            config.ShowTrail = !config.ShowTrail;
            // Visibility only: save without rebuilding, so the current run is kept (like /rtrain trail).
            plugin.PluginInterface.SavePluginConfig(config);
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(plugin.Session.Engine == null))
        {
            if (ImGui.Button("Restart opener")) plugin.Session.Restart();
        }
    }

    private void DrawSelection(string job)
    {
        var library = plugin.Library;
        SettingsWindow.RowLabel("Job");
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(job);
        SettingsWindow.DimAfter("follows your current job");

        var openers = library.ForJob(job, RotationKind.Opener).ToList();
        var loops = library.ForJob(job, RotationKind.Loop).ToList();
        if (openers.Count == 0 && loops.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped($"No rotations for {job} yet. Import a file or open the builder.");
            if (ImGui.Button("Import JSON…")) importFile();
            ImGui.SameLine();
            if (ImGui.Button("Open builder"))
                plugin.OpenBuilder(new BuilderDocument(job, RotationKind.Opener, "New opener", plugin.Catalog));
            return;
        }

        var selection = plugin.Config.Selections.GetValueOrDefault(job) ?? new JobSelection();
        var (opener, loop) = selection.Resolve(library, job);

        SettingsWindow.RowLabel("Opener");
        if (Picker("##opener", openers, opener, out var pickedOpener))
            Save(job, selection, s => s.OpenerId = pickedOpener?.Id);

        SettingsWindow.RowLabel("Then loop");
        if (Picker("##loop", loops, loop, out var pickedLoop))
            Save(job, selection, s => s.LoopId = pickedLoop?.Id);

        if (opener != null) DrawPreview(job, opener);
    }

    private void Save(string job, JobSelection selection, Action<JobSelection> change)
    {
        change(selection);
        plugin.Config.Selections[job] = selection;
        plugin.SaveConfig();
    }

    /// <summary>A combo of the rotations plus "None". Returns true when the user picked a different one.</summary>
    private static bool Picker(string id, IReadOnlyList<Rotation> rotations, Rotation? current, out Rotation? picked)
    {
        picked = current;
        ImGui.SetNextItemWidth(-1);
        using var combo = ImRaii.Combo(id, current?.Name ?? "None");
        if (!combo.Success) return false;

        var changed = false;
        if (ImGui.Selectable("None", current == null) && current != null)
        {
            picked = null;
            changed = true;
        }
        foreach (var r in rotations)
        {
            using var rowId = ImRaii.PushId(r.Id);
            if (ImGui.Selectable(r.Name, r.Id == current?.Id) && r.Id != current?.Id)
            {
                picked = r;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>The opener's first steps as icons (weaves smaller), then "+N more".</summary>
    private void DrawPreview(string job, Rotation opener)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gcdSize = new Vector2(34, 34) * scale;
        var weaveSize = new Vector2(26, 26) * scale;
        var abilities = opener.Steps
            .Where(s => s.Type != StepType.Pull)
            .Select(s => s.Type == StepType.Item
                ? plugin.Catalog.FindItem(ItemIds.Normalize(s.Id))
                : plugin.Catalog.FindAction(job, s.Id))
            .OfType<Ability>()
            .ToList();

        ImGui.Spacing();
        var rowTop = ImGui.GetCursorPosY();
        foreach (var a in abilities.Take(PreviewSteps))
        {
            var size = a.IsGcd ? gcdSize : weaveSize;
            ImGui.SetCursorPosY(rowTop + (gcdSize.Y - size.Y) / 2);
            var wrap = plugin.Textures.GetFromGameIcon(new GameIconLookup(a.IconId)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, size);
            if (ImGui.IsItemHovered()) GlassWidgets.Tooltip(a.Name);
            ImGui.SameLine(0, 4 * scale);
        }
        if (abilities.Count > PreviewSteps)
        {
            ImGui.SetCursorPosY(rowTop + (gcdSize.Y - ImGui.GetTextLineHeight()) / 2);
            GlassWidgets.Chip($"+{abilities.Count - PreviewSteps} more", GlassTheme.Dim);
        }
        // Ends the row with an item, so the strip's full height counts toward the card.
        ImGui.SetCursorPosY(rowTop);
        ImGui.Dummy(new Vector2(0, gcdSize.Y));
    }
}
