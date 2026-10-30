using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using RotationTrainer.Core;
using RotationTrainer.Ui.Glass;

namespace RotationTrainer.Ui.Builder;

/// <summary>
/// The rotation builder (<c>/rtrain builder</c>): a palette of the job's abilities, a timeline to drag them onto, an
/// inspector for the selected step and the live export JSON. Edits a <see cref="BuilderDocument"/>.
/// Sizes are unscaled pixels from the approved mockup; everything is multiplied by the UI scale when drawn.
/// </summary>
public sealed class BuilderWindow : Window
{
    private const string AbilityPayload = "RT_ABILITY";
    private const string StepPayload = "RT_STEP";

    private const float PaletteWidth = 210f;
    private const float PaletteTile = 38f;
    private const float PaletteWeaveTile = 32f;
    private const float PaletteGap = 7f;
    private const float TimelineHeight = 160f;
    private const float TimelinePad = 18f;
    private const float ItemGap = 6f;
    private const float GcdTile = 46f;
    private const float WeaveTile = 34f;
    private const float GcdTop = TimelineHeight - 94f;
    private const float WeaveTop = TimelineHeight - 122f;
    private const float LineCenter = TimelineHeight - 53f;
    private const float InspectorLabelWidth = 130f;
    private const float BinHeight = 34f;
    private const double FeedbackSeconds = 1.5;

    private static readonly (PaletteGroup Group, string Label)[] Groups =
    {
        (PaletteGroup.Gcd, "GCD"), (PaletteGroup.Weave, "Weaves"), (PaletteGroup.Role, "Role"),
        (PaletteGroup.Medicine, "Medicine"),
    };

    private static readonly Vector4 Gold = GlassTheme.AccentColor(Accent.Gold);
    private static readonly Vector4 Ok = new(155 / 255f, 231 / 255f, 176 / 255f, 1f);
    private static readonly Vector4 JsonText = new(207 / 255f, 224 / 255f, 1f, 1f);

    private readonly Plugin _plugin;
    private readonly IReadOnlyList<string> _jobs;

    private BuilderDocument? _doc;
    private int? _selected;
    private int _version;
    private int _jsonVersion = -1;
    private string _json = "";
    private string _cleanJson = "";
    private (string Message, Action OnYes)? _confirm;
    private double _savedAt = double.NegativeInfinity;
    private double _copiedAt = double.NegativeInfinity;
    private string _atBuffer = "";
    private bool _atActive;
    private int _atIndex = -1;
    private IDisposable? _theme;

    public BuilderWindow(Plugin plugin, IReadOnlyList<string> jobs) : base("Rotation Builder###RotationTrainerBuilder",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoNavInputs)
    {
        _plugin = plugin;
        _jobs = jobs;
        // Escape must not close the window past the unsaved-changes question.
        RespectCloseHotkey = false;
        Size = new Vector2(1000, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(760, 520) };
    }

    private static float Scale => ImGuiHelpers.GlobalScale;

    private static uint U32(Vector4 color) => ImGui.GetColorU32(color);

    private BuilderDocument Doc => _doc!;

    private bool Dirty => _json != _cleanJson;

    /// <summary>Opens the builder on a document, asking first if that would discard unsaved changes.</summary>
    public void Open(BuilderDocument document)
    {
        if (IsOpen && _doc != null && Dirty)
            _confirm = ("Discard unsaved changes?", () => Load(document));
        else
            Load(document);
        IsOpen = true;
        BringToFront();
    }

    public override bool DrawConditions() => _doc != null;

    public override void PreDraw() =>
        _theme = GlassTheme.PushWindow(_plugin.Config.GlassOpacity, GlassTheme.AccentColor(_plugin.Config.Accent));

    public override void PostDraw()
    {
        _theme?.Dispose();
        _theme = null;
    }

    public override void Draw()
    {
        GlassWidgets.DrawGlassBackground(_plugin.Config.GlassOpacity);
        RefreshJson();
        DrawTitle();

        var avail = ImGui.GetContentRegionAvail();
        var palettePos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(palettePos, palettePos + new Vector2(PaletteWidth * Scale, avail.Y),
            U32(new Vector4(0, 0, 0, 0.12f)), GlassTheme.CardRounding * Scale);
        using (var palette = ImRaii.Child("##palette", new Vector2(PaletteWidth * Scale, avail.Y), false,
                   ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (palette.Success) DrawPalette();
        }

        ImGui.SameLine();
        using (var main = ImRaii.Child("##main", ImGui.GetContentRegionAvail(), false,
                   ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar))
        {
            if (main.Success)
            {
                DrawTopRow();
                DrawTimeline();
                DrawLowerRow();
            }
        }

        HandleKeys();
    }

    private void Load(BuilderDocument document)
    {
        _doc = document;
        _selected = null;
        _atActive = false;
        _confirm = null;
        _savedAt = double.NegativeInfinity;
        Changed();
        RefreshJson();
        _cleanJson = _json;
    }

    /// <summary>Marks the document edited, so the JSON is rebuilt on the next refresh.</summary>
    private void Changed() => _version++;

    private void RefreshJson()
    {
        if (_jsonVersion == _version) return;
        _jsonVersion = _version;
        _json = RotationJson.Export(new[] { Doc.ToRotation(() => Doc.Id ?? "unsaved") });
    }

    private Ability? Find(Step step) => step.Type switch
    {
        StepType.Item => _plugin.Catalog.FindItem(step.Id),
        StepType.Action => _plugin.Catalog.FindAction(Doc.Job, step.Id),
        _ => null,
    };

    // ---------------------------------------------------------------- title

    private void DrawTitle()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Rotation Builder");
        if (Dirty)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
                ImGui.TextUnformatted("unsaved changes");
        }

        if (_confirm is { } confirm)
        {
            ImGui.SameLine(0, 24 * Scale);
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Warning))
                ImGui.TextUnformatted(confirm.Message);
            ImGui.SameLine();
            if (ImGui.SmallButton("Yes"))
            {
                _confirm = null;
                confirm.OnYes();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("No")) _confirm = null;
        }

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var icon = FontAwesomeIcon.Times.ToIconString();
            var buttonWidth = ImGui.CalcTextSize(icon).X + ImGui.GetStyle().FramePadding.X * 2;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - buttonWidth);
            using (ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero).Push(ImGuiCol.Text, GlassTheme.Dim))
            {
                if (ImGui.Button(icon)) RequestClose();
            }
        }

        var y = ImGui.GetCursorScreenPos().Y;
        var min = ImGui.GetWindowPos();
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, y), new Vector2(min.X + ImGui.GetWindowSize().X, y),
            U32(GlassTheme.White(0.10f)), Scale);
        ImGui.Dummy(new Vector2(0, 4 * Scale));
    }

    private void RequestClose()
    {
        if (Dirty)
            _confirm = ("Discard unsaved changes?", () => IsOpen = false);
        else
            IsOpen = false;
    }

    // ---------------------------------------------------------------- palette

    private void DrawPalette()
    {
        var palette = _plugin.Catalog.Palette(Doc.Job);
        var dl = ImGui.GetWindowDrawList();
        foreach (var (group, label) in Groups)
        {
            var abilities = palette.Where(a => a.Group == group).ToList();
            if (abilities.Count == 0) continue;
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
                ImGui.TextUnformatted(label.ToUpperInvariant());

            var tile = PaletteTile * Scale;
            var gap = PaletteGap * Scale;
            var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + gap) / (tile + gap)));
            var origin = ImGui.GetCursorScreenPos();
            for (var i = 0; i < abilities.Count; i++)
            {
                var a = abilities[i];
                var pos = origin + new Vector2(i % columns, i / columns) * (tile + gap);
                ImGui.SetCursorScreenPos(pos);
                using var id = ImRaii.PushId($"{a.Type}{a.Id}");
                var clicked = ImGui.InvisibleButton("##tile", new Vector2(tile));
                var hovered = ImGui.IsItemHovered();
                if (ImGui.BeginDragDropSource())
                {
                    SetPayload(AbilityPayload, (int)a.Type, (int)a.Id);
                    DrawDragPreview(a, a.Name);
                    ImGui.EndDragDropSource();
                }
                else if (hovered)
                {
                    ImGui.SetTooltip(a.Name);
                }
                if (clicked) InsertAfterSelection(new Step(a.Type, a.Id, a.Name));

                var size = (a.IsGcd ? PaletteTile : PaletteWeaveTile) * Scale;
                var min = pos + new Vector2((tile - size) / 2);
                DrawIcon(dl, a, min, size, (a.IsGcd ? 9 : 12) * Scale, 1f);
                if (hovered)
                    dl.AddRect(min, min + new Vector2(size), U32(GlassTheme.White(0.6f)), (a.IsGcd ? 9 : 12) * Scale,
                        ImDrawFlags.None, Scale);
            }
            var rows = (abilities.Count + columns - 1) / columns;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(columns * (tile + gap) - gap, rows * (tile + gap) - gap));
            ImGui.Spacing();
        }
    }

    private void InsertAfterSelection(Step step)
    {
        var at = _selected is { } s ? s + 1 : Doc.Steps.Count;
        Doc.Insert(at, step);
        Select(at);
        Changed();
    }

    // ---------------------------------------------------------------- top row

    private void DrawTopRow()
    {
        DimLabel("Job");
        ImGui.SetNextItemWidth(80 * Scale);
        using (var combo = ImRaii.Combo("##job", Doc.Job))
        {
            if (combo.Success)
            {
                foreach (var job in _jobs)
                {
                    if (ImGui.Selectable(job, job == Doc.Job) && job != Doc.Job) ChangeJob(job);
                }
            }
        }

        ImGui.SameLine();
        DimLabel("Name");
        ImGui.SetNextItemWidth(220 * Scale);
        var name = Doc.Name;
        if (ImGui.InputText("##name", ref name, 100))
        {
            Doc.Name = name;
            Changed();
        }

        ImGui.SameLine();
        var kind = Doc.Kind;
        // Assign only on a real change: setting Opener again would re-insert a Pull marker.
        if (GlassWidgets.Segmented("kind", ref kind, (RotationKind.Opener, "Opener"), (RotationKind.Loop, "Loop")))
        {
            Select(null);
            Doc.Kind = kind;
            Changed();
        }

        var saved = ImGui.GetTime() - _savedAt < FeedbackSeconds;
        var saveLabel = saved ? "Saved###save" : "Save to library###save";
        var style = ImGui.GetStyle();
        var buttonsWidth = ImGui.CalcTextSize("Clear").X + ImGui.CalcTextSize("Save to library").X
            + style.FramePadding.X * 4 + style.ItemSpacing.X;
        var right = ImGui.GetContentRegionMax().X - buttonsWidth;
        ImGui.SameLine();
        if (right > ImGui.GetCursorPosX()) ImGui.SetCursorPosX(right);
        if (ImGui.Button("Clear")) Clear();
        ImGui.SameLine();
        // Anything the importer would reject is never saved: it would be dropped on the next load.
        var problems = Doc.Validate();
        using (ImRaii.Disabled(problems.Count > 0))
        {
            if (GlassWidgets.PrimaryButton(saveLabel)) Save();
        }

        if (problems.Count > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Warning))
                ImGui.TextWrapped("Can't save yet: " + string.Join(" ", problems));
        }

        if (_plugin.Store.SaveBlocked)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Warning))
            {
                ImGui.TextWrapped("Your rotation library couldn't be read, so changes won't be saved this session. "
                    + "Check /xllog for details.");
            }
        }
    }

    private static void DimLabel(string text)
    {
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            ImGui.TextUnformatted(text);
        ImGui.SameLine();
    }

    private void ChangeJob(string job)
    {
        void Start() => Load(new BuilderDocument(job, Doc.Kind, Doc.Name, _plugin.Catalog));
        if (Doc.Steps.Any(s => s.Type != StepType.Pull))
            _confirm = ($"Start a new {job} rotation? Abilities differ per job, so this clears the timeline.", Start);
        else
            Start();
    }

    private void Clear()
    {
        Select(null);
        for (var i = Doc.Steps.Count - 1; i >= 0; i--)
        {
            if (Doc.Steps[i].Type != StepType.Pull) Doc.Remove(i);
        }
        Changed();
    }

    private void Save()
    {
        CommitAt();
        if (Doc.Validate().Count > 0) return;
        var rotation = Doc.ToRotation(() => Guid.NewGuid().ToString());
        _plugin.Library.Save(rotation);
        if (Doc.Id == null)
        {
            // From now on this edits the saved rotation, so saving again replaces it instead of adding a copy.
            var selected = _selected;
            _doc = BuilderDocument.Edit(rotation, _plugin.Catalog);
            _selected = selected is { } s && s < Doc.Steps.Count ? s : null;
            Changed();
        }
        RefreshJson();
        _cleanJson = _json;
        _savedAt = ImGui.GetTime();
    }

    // ---------------------------------------------------------------- timeline

    private readonly record struct Slot(float X, float Width, Ability? Ability);

    private void DrawTimeline()
    {
        var steps = Doc.Steps;
        var flagged = Doc.FlaggedSteps();
        var slots = new Slot[steps.Count];
        var x = TimelinePad * Scale;
        for (var i = 0; i < steps.Count; i++)
        {
            var ability = Find(steps[i]);
            var width = steps[i].Type == StepType.Pull
                ? Math.Max(ImGui.CalcTextSize("Pull").X + 8 * Scale, 30 * Scale)
                : (ability?.IsGcd == true ? GcdTile : WeaveTile) * Scale;
            slots[i] = new Slot(x, width, ability);
            x += width + ItemGap * Scale;
        }
        var contentWidth = x - ItemGap * Scale + TimelinePad * Scale;
        var height = TimelineHeight * Scale;

        var outerPos = ImGui.GetCursorScreenPos();
        var outerSize = new Vector2(ImGui.GetContentRegionAvail().X, height + ImGui.GetStyle().ScrollbarSize);
        var outerDl = ImGui.GetWindowDrawList();
        outerDl.AddRectFilled(outerPos, outerPos + outerSize, U32(new Vector4(0, 0, 0, 0.22f)),
            GlassTheme.CardRounding * Scale);
        outerDl.AddRect(outerPos, outerPos + outerSize, U32(GlassTheme.White(0.10f)), GlassTheme.CardRounding * Scale,
            ImDrawFlags.None, Scale);

        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##timeline", outerSize, false, ImGuiWindowFlags.HorizontalScrollbar))
        {
            if (!child.Success) return;
            var dl = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var visibleRight = ImGui.GetWindowPos().X + ImGui.GetWindowSize().X;
            var lineHalf = 3 * Scale;
            dl.AddRectFilled(new Vector2(ImGui.GetWindowPos().X, origin.Y + LineCenter * Scale - lineHalf),
                new Vector2(Math.Max(visibleRight, origin.X + contentWidth), origin.Y + LineCenter * Scale + lineHalf),
                U32(GlassTheme.White(0.14f)), lineHalf);

            if (steps.Count == 0)
            {
                var text = "Drag abilities here";
                var size = ImGui.CalcTextSize(text);
                dl.AddText(origin + new Vector2(TimelinePad * Scale, (height - size.Y) / 2), U32(GlassTheme.Mute), text);
            }

            var gcdNumber = 0;
            var afterPull = Doc.PullIndex < 0;
            var clickedStep = -1;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var slot = slots[i];
                using var id = ImRaii.PushId(i);
                if (step.Type == StepType.Pull)
                {
                    afterPull = true;
                    DrawPull(dl, origin, slot);
                    continue;
                }
                var isGcd = slot.Ability?.IsGcd == true;
                var tile = (isGcd ? GcdTile : WeaveTile) * Scale;
                var min = origin + new Vector2(slot.X, (isGcd ? GcdTop : WeaveTop) * Scale);
                ImGui.SetCursorScreenPos(min);
                if (ImGui.InvisibleButton("##step", new Vector2(tile))) clickedStep = i;
                var hovered = ImGui.IsItemHovered();
                var dragging = false;
                if (ImGui.BeginDragDropSource())
                {
                    dragging = true;
                    SetPayload(StepPayload, i, 0);
                    DrawDragPreview(slot.Ability, step.Name);
                    ImGui.EndDragDropSource();
                }
                else if (hovered)
                {
                    ImGui.SetTooltip(step.Name);
                }

                var rounding = (isGcd ? 10 : 12) * Scale;
                if (_selected == i)
                {
                    dl.AddRect(min - new Vector2(6 * Scale), min + new Vector2(tile + 6 * Scale),
                        U32(GlassTheme.WithAlpha(GlassTheme.CurrentAccent, 0.45f)), rounding + 6 * Scale,
                        ImDrawFlags.None, 6 * Scale);
                    dl.AddRect(min - new Vector2(2 * Scale), min + new Vector2(tile + 2 * Scale), U32(Vector4.One),
                        rounding + 2 * Scale, ImDrawFlags.None, 3 * Scale);
                }
                else if (flagged.Contains(i))
                {
                    dl.AddRect(min - new Vector2(1.5f * Scale), min + new Vector2(tile + 1.5f * Scale), U32(Gold),
                        rounding + 1.5f * Scale, ImDrawFlags.None, 2 * Scale);
                }
                DrawIcon(dl, slot.Ability, min, tile, rounding, dragging ? 0.35f : 1f, step.Name);

                if (Doc.IsPrepull(i) && step.At is { } at)
                {
                    var cap = $"{at.ToString("0.##", CultureInfo.InvariantCulture)}s";
                    var capSize = ImGui.CalcTextSize(cap);
                    dl.AddText(new Vector2(min.X + (tile - capSize.X) / 2, min.Y - capSize.Y - 2 * Scale), U32(Gold), cap);
                }
                if (isGcd && afterPull)
                {
                    var num = (++gcdNumber).ToString(CultureInfo.InvariantCulture);
                    var numSize = ImGui.CalcTextSize(num);
                    dl.AddText(new Vector2(min.X + (tile - numSize.X) / 2, min.Y + tile + 2 * Scale), U32(GlassTheme.Mute),
                        num);
                }
            }

            DrawDropTargets(dl, origin, slots, Math.Max(visibleRight, origin.X + contentWidth));

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(contentWidth, height));
            if (clickedStep >= 0) Select(clickedStep);
        }
    }

    private void DrawPull(ImDrawListPtr dl, Vector2 origin, Slot slot)
    {
        var min = origin + new Vector2(slot.X, 0);
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton("##pull", new Vector2(slot.Width, TimelineHeight * Scale));
        var hovered = ImGui.IsItemHovered();
        var dragging = false;
        if (ImGui.BeginDragDropSource())
        {
            dragging = true;
            SetPayload(StepPayload, Doc.PullIndex, 0);
            ImGui.TextUnformatted("Pull");
            ImGui.EndDragDropSource();
        }
        else if (hovered)
        {
            ImGui.SetTooltip("Drag to move the pull. Steps before it are pre-pull.");
        }

        var color = U32(GlassTheme.White(dragging ? 0.25f : hovered ? 1f : 0.85f));
        var textSize = ImGui.CalcTextSize("Pull");
        var top = 12 * Scale;
        dl.AddText(new Vector2(min.X + (slot.Width - textSize.X) / 2, min.Y + top), color, "Pull");
        var barX = min.X + slot.Width / 2;
        dl.AddLine(new Vector2(barX, min.Y + top + textSize.Y + 2 * Scale),
            new Vector2(barX, min.Y + (TimelineHeight - 12) * Scale), U32(GlassTheme.White(dragging ? 0.2f : 0.55f)),
            2 * Scale);
    }

    /// <summary>
    /// One drop target per gap (before each step and after the last), each reaching from the middle of the step on
    /// its left to the middle of the step on its right, with an accent bar where the drop would land.
    /// </summary>
    private void DrawDropTargets(ImDrawListPtr dl, Vector2 origin, Slot[] slots, float right)
    {
        var n = slots.Length;
        var half = ItemGap * Scale / 2;
        for (var gap = 0; gap <= n; gap++)
        {
            var left = gap == 0 ? origin.X : origin.X + slots[gap - 1].X + slots[gap - 1].Width / 2;
            var end = gap == n ? right : origin.X + slots[gap].X + slots[gap].Width / 2;
            var barX = n == 0 ? origin.X + TimelinePad * Scale
                : gap == n ? origin.X + slots[n - 1].X + slots[n - 1].Width + half
                : origin.X + slots[gap].X - half;
            var rect = new ImRect(new Vector2(left, origin.Y), new Vector2(end, origin.Y + TimelineHeight * Scale));
            if (!ImGuiP.BeginDragDropTargetCustom(rect, ImGui.GetID($"##gap{gap}"))) continue;

            const ImGuiDragDropFlags flags = ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect;
            var ability = ImGui.AcceptDragDropPayload(AbilityPayload, flags);
            var step = ability.IsNull ? ImGui.AcceptDragDropPayload(StepPayload, flags) : ImGuiPayloadPtr.Null;
            var payload = ability.IsNull ? step : ability;
            if (!payload.IsNull)
            {
                var top = origin.Y + 10 * Scale;
                var bottom = origin.Y + (TimelineHeight - 10) * Scale;
                var accent = GlassTheme.CurrentAccent;
                dl.AddRectFilled(new Vector2(barX - 6 * Scale, top), new Vector2(barX + 6 * Scale, bottom),
                    U32(GlassTheme.WithAlpha(accent, 0.18f)), 6 * Scale);
                dl.AddRectFilled(new Vector2(barX - 2 * Scale, top), new Vector2(barX + 2 * Scale, bottom), U32(accent),
                    2 * Scale);
                if (payload.IsDelivery())
                {
                    var (a, b) = ReadPayload(payload);
                    if (!ability.IsNull) DropAbility((StepType)a, (uint)b, gap);
                    else MoveStep(a, gap);
                }
            }
            ImGui.EndDragDropTarget();
        }
    }

    private void DropAbility(StepType type, uint id, int insertBefore)
    {
        var ability = type == StepType.Item ? _plugin.Catalog.FindItem(id) : _plugin.Catalog.FindAction(Doc.Job, id);
        if (ability == null) return;
        Doc.Insert(insertBefore, new Step(ability.Type, ability.Id, ability.Name));
        Select(insertBefore);
        Changed();
    }

    private void MoveStep(int from, int insertBefore)
    {
        if (from < 0 || from >= Doc.Steps.Count || insertBefore == from || insertBefore == from + 1) return;
        var to = from < insertBefore ? insertBefore - 1 : insertBefore;
        Doc.Move(from, insertBefore);
        if (_selected is { } s)
        {
            if (s == from) s = to;
            else
            {
                if (s > from) s--;
                if (s >= to) s++;
            }
            _selected = s;
        }
        Changed();
    }

    // ---------------------------------------------------------------- keys

    private void HandleKeys()
    {
        if (_selected is not { } i || _confirm != null || ImGui.GetIO().WantTextInput
            || !ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
            return;
        if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow) && i > 0)
            MoveStep(i, i - 1);
        else if (ImGui.IsKeyPressed(ImGuiKey.RightArrow) && i < Doc.Steps.Count - 1)
            MoveStep(i, i + 2);
        else if (ImGui.IsKeyPressed(ImGuiKey.Delete))
            RemoveStep(i);
    }

    private void RemoveStep(int index)
    {
        if (index < 0 || index >= Doc.Steps.Count || Doc.Steps[index].Type == StepType.Pull) return;
        _atActive = false;
        Doc.Remove(index);
        var next = Math.Min(index, Doc.Steps.Count - 1);
        _selected = next >= 0 && Doc.Steps[next].Type != StepType.Pull ? next : null;
        Changed();
    }

    private void Select(int? index)
    {
        CommitAt();
        _selected = index is { } i && i >= 0 && i < Doc.Steps.Count && Doc.Steps[i].Type != StepType.Pull ? i : null;
    }

    // ---------------------------------------------------------------- inspector and JSON

    private void DrawLowerRow()
    {
        ImGui.Spacing();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = ImGui.GetContentRegionAvail().X - spacing;
        var leftWidth = width * 1.1f / 2.1f;
        var height = ImGui.GetContentRegionAvail().Y;

        using (ImRaii.PushColor(ImGuiCol.ChildBg, GlassTheme.White(0.055f)).Push(ImGuiCol.Border, GlassTheme.White(0.10f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, Scale)
                   .Push(ImGuiStyleVar.WindowPadding, new Vector2(14, 12) * Scale))
        {
            using (var inspector = ImRaii.Child("##inspector", new Vector2(leftWidth, height), true))
            {
                if (inspector.Success) DrawInspector();
            }
            ImGui.SameLine();
            using (var json = ImRaii.Child("##json", new Vector2(width - leftWidth, height), true,
                       ImGuiWindowFlags.NoScrollbar))
            {
                if (json.Success) DrawJson();
            }
        }
    }

    private void DrawInspector()
    {
        if (_selected is not { } i || i >= Doc.Steps.Count)
        {
            ImGui.TextUnformatted("Step");
            using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
                ImGui.TextWrapped("Select a step to edit it. Arrow keys move it and Delete removes it.");
        }
        else
        {
            var step = Doc.Steps[i];
            var ability = Find(step);
            ImGui.TextUnformatted(step.Name);
            Row("Type");
            ImGui.TextUnformatted(step.Type == StepType.Item ? "Potion (weave)" : ability?.IsGcd == true ? "GCD" : "Weave");

            if (Doc.IsPrepull(i))
            {
                Row("Use at countdown");
                DrawAtInput(i, step);
            }
            if (step.Type == StepType.Item)
            {
                Row("Shown when");
                ImGui.TextUnformatted("\"Use potions\" is on");
            }
            Row("Position");
            ImGui.TextUnformatted($"Step {i + 1} of {Doc.Steps.Count}");

            if (ImGui.Button("Remove")) RemoveStep(i);
        }

        ImGui.Spacing();
        var warnings = Doc.Warnings();
        if (warnings.Count == 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Ok))
                ImGui.TextUnformatted("No problems found");
        }
        foreach (var w in warnings)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Gold))
                ImGui.TextWrapped(w.Message);
        }
    }

    private static void Row(string label)
    {
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextUnformatted(label);
        ImGui.SameLine(InspectorLabelWidth * Scale);
    }

    /// <summary>Shows the countdown time as positive seconds left; stores it negative when the edit is committed.</summary>
    private void DrawAtInput(int index, Step step)
    {
        if (!_atActive)
            _atBuffer = step.At is { } at ? (-at).ToString("0.##", CultureInfo.InvariantCulture) : "";
        ImGui.SetNextItemWidth(60 * Scale);
        ImGui.InputText($"##at{index}", ref _atBuffer, 8, ImGuiInputTextFlags.CharsDecimal);
        if (ImGui.IsItemActivated()) _atIndex = index;
        _atActive = ImGui.IsItemActive();
        if (ImGui.IsItemDeactivatedAfterEdit()) CommitAt(force: true);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextUnformatted("seconds left");
    }

    /// <summary>Applies a countdown edit still in progress, e.g. when the selection moves away mid-edit.</summary>
    private void CommitAt(bool force = false)
    {
        if (!_atActive && !force) return;
        _atActive = false;
        var index = _atIndex;
        if (!Doc.IsPrepull(index)) return;
        // Empty and 0 both mean "no countdown time".
        if (!BuilderDocument.TryParseCountdown(_atBuffer, out var value)) return;
        if (Doc.Steps[index].At == value) return;
        Doc.SetAt(index, value);
        Changed();
    }

    private void DrawJson()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Export JSON");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Mute))
            ImGui.TextUnformatted("updates as you edit");

        var copied = ImGui.GetTime() - _copiedAt < FeedbackSeconds;
        var label = copied ? "Copied###copy" : "Copy###copy";
        var buttonWidth = ImGui.CalcTextSize("Copied").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetContentRegionMax().X - buttonWidth));
        if (ImGui.Button(label, new Vector2(buttonWidth, 0)))
        {
            ImGui.SetClipboardText(_json);
            _copiedAt = ImGui.GetTime();
        }

        var binHeight = BinHeight * Scale;
        var textHeight = Math.Max(ImGui.GetContentRegionAvail().Y - binHeight - ImGui.GetStyle().ItemSpacing.Y,
            ImGui.GetTextLineHeight() * 3);
        var json = _json;
        using (ImRaii.PushColor(ImGuiCol.Text, JsonText).Push(ImGuiCol.FrameBg, new Vector4(0, 0, 0, 0.25f)))
        {
            ImGui.InputTextMultiline("##jsontext", ref json, json.Length + 1,
                new Vector2(ImGui.GetContentRegionAvail().X, textHeight), ImGuiInputTextFlags.ReadOnly);
        }

        DrawBin(binHeight);
    }

    private void DrawBin(float height)
    {
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, height);
        ImGui.InvisibleButton("##bin", size);
        var over = false;
        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload(StepPayload,
                ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
            var index = payload.IsNull ? -1 : ReadPayload(payload).A;
            // The Pull marker can be moved but not removed.
            if (index >= 0 && index < Doc.Steps.Count && Doc.Steps[index].Type != StepType.Pull)
            {
                over = true;
                if (payload.IsDelivery()) RemoveStep(index);
            }
            ImGui.EndDragDropTarget();
        }

        var dl = ImGui.GetWindowDrawList();
        var rounding = 10 * Scale;
        if (over) dl.AddRectFilled(pos, pos + size, U32(GlassTheme.WithAlpha(GlassTheme.Danger, 0.08f)), rounding);
        dl.AddRect(pos, pos + size, U32(over ? GlassTheme.Danger : GlassTheme.White(0.25f)), rounding, ImDrawFlags.None,
            Scale);
        const string text = "Drop a step here to remove it";
        var textSize = ImGui.CalcTextSize(text);
        dl.AddText(pos + (size - textSize) / 2, U32(over ? GlassTheme.Danger : GlassTheme.Dim), text);
    }

    // ---------------------------------------------------------------- drawing and payload helpers

    private void DrawIcon(ImDrawListPtr dl, Ability? ability, Vector2 min, float size, float rounding, float alpha,
        string? fallbackName = null)
    {
        var max = min + new Vector2(size);
        if (ability != null)
        {
            var wrap = _plugin.Textures.GetFromGameIcon(new GameIconLookup(ability.IconId)).GetWrapOrEmpty();
            dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, U32(new Vector4(1, 1, 1, alpha)),
                rounding);
        }
        else
        {
            // Not in this job's kit: a plain tile with the name's initials.
            dl.AddRectFilled(min, max, U32(GlassTheme.White(0.12f * alpha)), rounding);
            var name = fallbackName ?? "?";
            var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => w[0]));
            var textSize = ImGui.CalcTextSize(initials);
            dl.AddText(min + (new Vector2(size) - textSize) / 2, U32(GlassTheme.White(alpha)), initials);
        }
        dl.AddRect(min, max, U32(GlassTheme.White(0.35f * alpha)), rounding, ImDrawFlags.None, Scale);
    }

    private void DrawDragPreview(Ability? ability, string name)
    {
        var size = 32 * Scale;
        var pos = ImGui.GetCursorScreenPos();
        DrawIcon(ImGui.GetWindowDrawList(), ability, pos, size, 8 * Scale, 1f, name);
        ImGui.Dummy(new Vector2(size));
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(name);
    }

    private static void SetPayload(string type, int a, int b)
    {
        Span<byte> data = stackalloc byte[8];
        BitConverter.TryWriteBytes(data, a);
        BitConverter.TryWriteBytes(data[4..], b);
        ImGui.SetDragDropPayload(type, data, ImGuiCond.None);
    }

    private static unsafe (int A, int B) ReadPayload(ImGuiPayloadPtr payload)
    {
        if (payload.DataSize < 8) return (-1, -1);
        var data = new ReadOnlySpan<byte>(payload.Data, 8);
        return (BitConverter.ToInt32(data), BitConverter.ToInt32(data[4..]));
    }
}
