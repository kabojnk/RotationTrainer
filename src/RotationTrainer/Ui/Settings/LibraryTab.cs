using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RotationTrainer.Core;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Settings;

/// <summary>Lists the rotation library, and imports, exports, edits and deletes rotations.</summary>
internal sealed class LibraryTab(Plugin plugin)
{
    private const string ConflictPopup = "Already in your library###rtImportConflicts";
    private static readonly Vector4 PotionColor = GlassTheme.AccentColor(Accent.Gold);

    private sealed record PendingImport(string Source, ImportResult Result, IReadOnlyList<Rotation> Conflicts,
        Dictionary<string, ConflictChoice> Choices);

    private readonly FileDialogManager _dialogs = new();
    private readonly HashSet<string> _selected = new();
    private string? _confirmDeleteId;
    private PendingImport? _pending;
    private bool _openConflicts;
    private string? _statusTitle;
    private readonly List<(string Text, Vector4 Color)> _status = new();

    /// <summary>Draws the file dialogs; call every frame, outside any window.</summary>
    public void DrawDialogs() => _dialogs.Draw();

    /// <summary>Opens the file picker for a JSON import.</summary>
    public void StartFileImport() =>
        _dialogs.OpenFileDialog("Import rotations", ".json", (ok, path) =>
        {
            if (!ok) return;
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetStatus($"Import: {Path.GetFileName(path)}", ($"Could not read the file: {ex.Message}", GlassTheme.Danger));
                return;
            }
            Import(json, Path.GetFileName(path));
        });

    public void Draw()
    {
        GlassWidgets.SectionHeader("Rotation library", "Every opener and loop you've imported or built.");
        var library = plugin.Library;

        if (plugin.Store.SaveBlocked)
        {
            using (GlassWidgets.Card("blocked"))
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Warning))
            {
                ImGui.TextWrapped("Your rotation library couldn't be read, so changes won't be saved this session. "
                    + "Check /xllog for details.");
            }
        }

        if (plugin.Store.LastLoad is { } report) DrawLoadReport(report);

        DrawButtons(library);
        ImGui.Spacing();
        using (GlassWidgets.Card("table"))
        {
            if (library.All.Count == 0)
                ImGui.TextWrapped("No rotations yet. Import a file or open the builder.");
            else
                DrawTable(library);
        }
        DrawStatus();
        DrawConflicts(library);
    }

    private static void DrawLoadReport(LibraryLoadReport report)
    {
        using (GlassWidgets.Card("load-report"))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Warning))
            {
                var what = report.Dropped > 0
                    ? $"{report.Dropped} rotation(s) couldn't be loaded and were left out."
                    : "Your rotation library file couldn't be loaded, so its rotations were left out.";
                var backup = report.BackupPath != null
                    ? $" A backup of the original file is at {report.BackupPath}."
                    : " No backup of the original file could be made.";
                ImGui.TextWrapped(what + backup);
            }
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            {
                foreach (var e in report.Errors)
                    ImGui.TextWrapped($"{e.Rotation}{(e.Step is { } step ? $", step {step}" : "")}: {e.Message}");
            }
        }
    }

    private void DrawButtons(RotationLibrary library)
    {
        var job = plugin.Session.Job;
        using (ImRaii.Disabled(job == null))
        {
            if (GlassWidgets.PrimaryButton("New in builder") && job != null)
                plugin.OpenBuilder(new BuilderDocument(job, RotationKind.Opener, "New opener", plugin.Catalog));
        }
        if (job == null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Log in to a combat job to build a rotation for it.");

        ImGui.SameLine();
        if (ImGui.Button("Import JSON…")) StartFileImport();

        ImGui.SameLine();
        if (ImGui.Button("Paste from clipboard"))
        {
            var text = ImGui.GetClipboardText();
            if (string.IsNullOrWhiteSpace(text))
                SetStatus("Import: clipboard", ("The clipboard is empty.", GlassTheme.Warning));
            else
                Import(text, "clipboard");
        }

        ImGui.SameLine();
        _selected.RemoveWhere(id => library.Get(id) == null);
        using (ImRaii.Disabled(_selected.Count == 0))
        {
            if (ImGui.Button("Export selected"))
                Export(library.All.Where(r => _selected.Contains(r.Id)).ToList(), "rotations");
        }
        if (_selected.Count == 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Click rows to select them.");
    }

    private void DrawTable(RotationLibrary library)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Action? afterTable = null;
        using (var table = ImRaii.Table("##rotations", 5,
                   ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
        {
            if (!table.Success) return;
            ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 56 * scale);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 3);
            ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthFixed, 64 * scale);
            ImGui.TableSetupColumn("Notes", ImGuiTableColumnFlags.WidthStretch, 2);
            ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed, 170 * scale);
            ImGui.TableHeadersRow();

            foreach (var r in library.All)
            {
                using var rowId = ImRaii.PushId(r.Id);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                GlassWidgets.Chip(r.Job, r.Job == plugin.Session.Job ? GlassTheme.CurrentAccent : GlassTheme.Dim);

                ImGui.TableNextColumn();
                var selected = _selected.Contains(r.Id);
                if (ImGui.Selectable(r.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
                {
                    if (selected) _selected.Remove(r.Id); else _selected.Add(r.Id);
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(r.Kind.ToString());

                ImGui.TableNextColumn();
                DrawNotes(r);

                ImGui.TableNextColumn();
                var action = DrawActions(r);
                afterTable ??= action;
            }
        }
        // Changes to the library wait until it is no longer being iterated.
        afterTable?.Invoke();
    }

    private static void DrawNotes(Rotation r)
    {
        var chips = new List<(string, Vector4)>();
        if (r.Steps.Any(s => s.Type == StepType.Item)) chips.Add(("potion", PotionColor));
        var prePull = r.Steps.Where(s => s.At != null).Select(s => s.At!.Value).DefaultIfEmpty().Min();
        if (prePull < 0) chips.Add(($"{prePull:0.##}s", GlassTheme.Dim));
        for (var i = 0; i < chips.Count; i++)
        {
            if (i > 0) ImGui.SameLine(0, 4 * ImGuiHelpers.GlobalScale);
            GlassWidgets.Chip(chips[i].Item1, chips[i].Item2);
        }
    }

    /// <summary>Draws the row's buttons; returns the library change to apply after the table, if any.</summary>
    private Action? DrawActions(Rotation r)
    {
        if (_confirmDeleteId == r.Id)
        {
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Danger))
                ImGui.TextUnformatted("Delete?");
            ImGui.SameLine();
            if (ImGui.SmallButton("Yes"))
            {
                _confirmDeleteId = null;
                _selected.Remove(r.Id);
                return () => plugin.Library.Delete(r.Id);
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("No")) _confirmDeleteId = null;
            return null;
        }

        if (ImGui.SmallButton("Edit")) plugin.OpenBuilder(BuilderDocument.Edit(r, plugin.Catalog));
        ImGui.SameLine();
        if (ImGui.SmallButton("Export")) Export(new[] { r }, $"{r.Job}-{r.Name}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Delete")) _confirmDeleteId = r.Id;
        return null;
    }

    private void DrawStatus()
    {
        if (_statusTitle == null) return;
        ImGui.Spacing();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextUnformatted(_statusTitle);
        foreach (var (text, color) in _status)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, color))
                ImGui.TextWrapped(text);
        }
    }

    private void DrawConflicts(RotationLibrary library)
    {
        if (_openConflicts)
        {
            ImGui.OpenPopup(ConflictPopup);
            _openConflicts = false;
        }

        using var popup = ImRaii.PopupModal(ConflictPopup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup.Success) return;
        if (_pending == null)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        ImGui.TextUnformatted(_pending.Conflicts.Count == 1
            ? "This rotation is already in your library."
            : $"These {_pending.Conflicts.Count} rotations are already in your library.");
        ImGui.Spacing();
        foreach (var r in _pending.Conflicts)
        {
            using var rowId = ImRaii.PushId(r.Id);
            var choice = _pending.Choices[r.Id];
            if (GlassWidgets.Segmented("choice", ref choice,
                    (ConflictChoice.Replace, "Replace"), (ConflictChoice.KeepBoth, "Keep both")))
                _pending.Choices[r.Id] = choice;
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{r.Job} · {r.Name} ({r.Kind})");
        }

        ImGui.Spacing();
        if (GlassWidgets.PrimaryButton("Import"))
        {
            Finish(library, _pending);
            _pending = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel") && _pending != null)
        {
            SetStatus($"Last import: {_pending.Source}", ("Import cancelled; nothing was added.", GlassTheme.Dim));
            _pending = null;
            ImGui.CloseCurrentPopup();
        }
    }

    private void Import(string json, string source)
    {
        var result = RotationJson.Import(json, plugin.Catalog, NewId);
        var conflicts = plugin.Library.Conflicts(result.Rotations);
        var pending = new PendingImport(source, result, conflicts,
            conflicts.DistinctBy(r => r.Id).ToDictionary(r => r.Id, _ => ConflictChoice.Replace));
        if (conflicts.Count == 0)
        {
            Finish(plugin.Library, pending);
            return;
        }
        _pending = pending;
        _openConflicts = true;
    }

    private void Finish(RotationLibrary library, PendingImport import)
    {
        var rotations = import.Result.Rotations;
        if (rotations.Count > 0)
            library.Add(rotations, r => import.Choices.GetValueOrDefault(r.Id, ConflictChoice.Replace), NewId);

        var lines = new List<(string, Vector4)> { ($"{rotations.Count} rotations added.", GlassTheme.Ink) };
        lines.AddRange(import.Result.Errors.Select(e =>
            ($"\"{e.Rotation}\"{(e.Step is { } step ? $" step {step}" : "")}: {e.Message}", GlassTheme.Warning)));
        SetStatus($"Last import: {import.Source}", lines.ToArray());
    }

    private void Export(IReadOnlyList<Rotation> rotations, string defaultName)
    {
        var fileName = string.Concat(defaultName.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c));
        _dialogs.SaveFileDialog("Export rotations", ".json", fileName + ".json", ".json", (ok, path) =>
        {
            if (!ok) return;
            try
            {
                File.WriteAllText(path, RotationJson.Export(rotations));
                SetStatus("Export", ($"{rotations.Count} rotations exported to {path}.", GlassTheme.Ink));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetStatus("Export", ($"Could not write {path}: {ex.Message}", GlassTheme.Danger));
            }
        });
    }

    private void SetStatus(string title, params (string Text, Vector4 Color)[] lines)
    {
        _statusTitle = title;
        _status.Clear();
        _status.AddRange(lines);
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}
