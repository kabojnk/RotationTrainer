using System.Text.Json;

namespace RotationTrainer.Core;

public sealed record ImportError(string Rotation, int? Step, string Message);

public sealed record ImportResult(IReadOnlyList<Rotation> Rotations, IReadOnlyList<ImportError> Errors);

public static class RotationJson
{
    public const string Format = "rotation-trainer/1";

    private const string FileScope = "(file)";

    public static ImportResult Import(string json, IAbilityCatalog catalog, Func<string> newId)
    {
        var rotations = new List<Rotation>();
        var errors = new List<ImportError>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            IEnumerable<JsonElement> items;

            switch (root.ValueKind)
            {
                case JsonValueKind.Array:
                    items = root.EnumerateArray();
                    break;
                case JsonValueKind.Object:
                    var hasFormat = root.TryGetProperty("format", out var fmt);
                    var hasList = root.TryGetProperty("rotations", out var list);
                    if (hasFormat || hasList)
                    {
                        if (!hasFormat || fmt.ValueKind != JsonValueKind.String || fmt.GetString() != Format)
                        {
                            errors.Add(new ImportError(FileScope, null,
                                $"Unsupported or missing format (expected \"{Format}\")."));
                            return new ImportResult(rotations, errors);
                        }
                        if (!hasList || list.ValueKind != JsonValueKind.Array)
                        {
                            errors.Add(new ImportError(FileScope, null, "\"rotations\" must be an array."));
                            return new ImportResult(rotations, errors);
                        }
                        items = list.EnumerateArray();
                    }
                    else
                    {
                        items = new[] { root };
                    }
                    break;
                default:
                    errors.Add(new ImportError(FileScope, null, "Expected a rotation object or an array of rotations."));
                    return new ImportResult(rotations, errors);
            }

            var n = 0;
            foreach (var item in items)
            {
                n++;
                var rot = ParseRotation(item, n, catalog, newId, errors);
                if (rot != null) rotations.Add(rot);
            }
        }
        catch (JsonException ex)
        {
            rotations.Clear();
            errors.Clear();
            errors.Add(new ImportError(FileScope, null, $"Invalid JSON: {ex.Message}"));
        }
        return new ImportResult(rotations, errors);
    }

    public static string Export(IEnumerable<Rotation> rotations) => throw new NotImplementedException();

    private static Rotation? ParseRotation(JsonElement el, int index, IAbilityCatalog catalog, Func<string> newId,
        List<ImportError> errors)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ImportError($"(unnamed #{index})", null, "Rotation must be an object."));
            return null;
        }

        var name = GetString(el, "name");
        var label = string.IsNullOrWhiteSpace(name) ? $"(unnamed #{index})" : name!;
        var before = errors.Count;
        void Fail(string msg, int? step = null) => errors.Add(new ImportError(label, step, msg));

        if (string.IsNullOrWhiteSpace(name)) Fail("Rotation has no name.");

        var job = GetString(el, "job")?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(job) || !catalog.IsKnownJob(job))
            Fail($"Unknown job \"{GetString(el, "job")}\".");

        var level = 100;
        if (el.TryGetProperty("level", out var lv))
        {
            if (lv.ValueKind != JsonValueKind.Number || !lv.TryGetInt32(out level))
            {
                level = -1;
            }
        }
        if (level != 100) Fail("Only level 100 rotations are supported (level 100).");

        RotationKind kind = default;
        var kindText = GetString(el, "kind");
        if (kindText == null || !Enum.TryParse(kindText, true, out kind) || !Enum.IsDefined(kind))
            Fail($"Unknown kind \"{kindText}\" (expected opener or loop).");

        var steps = new List<Step>();
        if (!el.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array
            || stepsEl.GetArrayLength() == 0)
        {
            Fail("Rotation has no steps.");
        }
        else if (!string.IsNullOrEmpty(job) && catalog.IsKnownJob(job))
        {
            var seenPull = false;
            var i = 0;
            foreach (var s in stepsEl.EnumerateArray())
            {
                i++;
                var step = ParseStep(s, i, job, kind, seenPull, catalog, out var err);
                if (err != null) Fail(err, i);
                if (step == null) continue;
                if (step.Type == StepType.Pull) seenPull = true;
                steps.Add(step);
            }
        }

        if (errors.Count > before) return null;

        var id = GetString(el, "id");
        if (string.IsNullOrWhiteSpace(id)) id = newId();
        return new Rotation(id, job!, level, kind, name!, steps, GetString(el, "source"), GetString(el, "patch"));
    }

    private static Step? ParseStep(JsonElement s, int index, string job, RotationKind kind, bool seenPull,
        IAbilityCatalog catalog, out string? error)
    {
        error = null;
        if (s.ValueKind != JsonValueKind.Object)
        {
            error = "Step must be an object.";
            return null;
        }

        var hasAction = s.TryGetProperty("action", out var action);
        var hasItem = s.TryGetProperty("item", out var item);
        var isPull = s.TryGetProperty("pull", out var pull) && pull.ValueKind == JsonValueKind.True;
        var nameText = GetString(s, "name");

        // A step with only "name" resolves as an action by name.
        var kinds = (hasAction ? 1 : 0) + (hasItem ? 1 : 0) + (isPull ? 1 : 0);
        if (kinds > 1 || kinds == 0 && string.IsNullOrWhiteSpace(nameText))
        {
            error = "Step must have exactly one of action, item or pull.";
            return null;
        }

        double? at = null;
        if (s.TryGetProperty("at", out var atEl) && atEl.ValueKind != JsonValueKind.Null)
        {
            if (atEl.ValueKind != JsonValueKind.Number)
            {
                error = "\"at\" must be a number.";
                return null;
            }
            at = atEl.GetDouble();
            if (kind == RotationKind.Loop || seenPull || isPull)
            {
                error = "\"at\" is only allowed before the pull.";
                return null;
            }
            if (at >= 0)
            {
                error = "\"at\" before the pull must be negative.";
                return null;
            }
        }

        if (isPull)
        {
            if (kind == RotationKind.Loop) { error = "A pull step is not allowed in a loop."; return null; }
            if (seenPull) { error = "Only one pull step is allowed."; return null; }
            return new Step(StepType.Pull, 0, "Pull");
        }

        if (hasItem)
        {
            Ability? found;
            if (item.ValueKind == JsonValueKind.Number && item.TryGetUInt32(out var raw))
                found = catalog.FindItem(ItemIds.Normalize(raw));
            else
                found = !string.IsNullOrWhiteSpace(nameText) ? catalog.FindItemByName(nameText) : null;
            if (found == null) { error = $"Item {Describe(item, nameText)} is not a known medicine."; return null; }
            return new Step(StepType.Item, found.Id, found.Name, at);
        }

        Ability? ab = null;
        if (hasAction && action.ValueKind == JsonValueKind.Number && action.TryGetUInt32(out var aid))
            ab = catalog.FindAction(job, aid);
        else if (!string.IsNullOrWhiteSpace(nameText))
            ab = catalog.FindActionByName(job, nameText);
        if (ab == null)
        {
            error = $"Action {(hasAction ? Describe(action, nameText) : $"\"{nameText}\"")} is not a {job} action.";
            return null;
        }
        return new Step(StepType.Action, ab.Id, ab.Name, at);
    }

    private static string Describe(JsonElement id, string? name) =>
        id.ValueKind == JsonValueKind.Number ? id.GetRawText() : $"\"{name}\"";

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
