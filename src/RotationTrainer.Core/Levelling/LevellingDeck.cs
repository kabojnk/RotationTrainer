using System.Text.Json;

namespace RotationTrainer.Core;

/// <summary>
/// The levelling cards, read from JSON and checked against the game data. A card with any error is left out;
/// the rest still load.
/// </summary>
public sealed class LevellingDeck
{
    private const int MaxLevel = 100;

    private readonly List<LevellingCard> _cards;

    private LevellingDeck(List<LevellingCard> cards) =>
        _cards = cards.OrderBy(c => c.Job).ThenBy(c => c.MinLevel).ToList();

    public IReadOnlyList<LevellingCard> All => _cards;

    public bool HasJob(string job) => _cards.Any(c => c.Job == job);

    public IReadOnlyList<LevellingCard> ForJob(string job) => _cards.Where(c => c.Job == job).ToList();

    /// <summary>The card whose band holds this level, or null when none does.</summary>
    public LevellingCard? Find(string job, int level) =>
        _cards.FirstOrDefault(c => c.Job == job && level >= c.MinLevel && level <= c.MaxLevel);

    public static (LevellingDeck Deck, IReadOnlyList<CardError> Errors) Parse(string json, IAbilityCatalog catalog)
    {
        var cards = new List<LevellingCard>();
        var errors = new List<CardError>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new CardError(RotationJson.FileScope, "Expected an array of cards."));
                return (new LevellingDeck(cards), errors);
            }
            var index = 0;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                index++;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new CardError($"(card {index})", "Expected a card object."));
                    continue;
                }
                var card = ReadCard(item, catalog, out var label, out var problems);
                if (card != null && cards.FirstOrDefault(c => c.Job == card.Job
                        && card.MinLevel <= c.MaxLevel && c.MinLevel <= card.MaxLevel) is { } other)
                    problems.Add($"Overlaps {other.Label}.");
                if (problems.Count > 0)
                    errors.AddRange(problems.Select(p => new CardError(label, p)));
                else
                    cards.Add(card!);
            }
        }
        catch (JsonException e)
        {
            errors.Add(new CardError(RotationJson.FileScope, $"Invalid JSON: {e.Message}"));
        }
        return (new LevellingDeck(cards), errors);
    }

    private static LevellingCard? ReadCard(JsonElement item, IAbilityCatalog catalog, out string label,
        out List<string> problems)
    {
        problems = new List<string>();
        var job = Str(item, "job");
        var min = Int(item, "minLevel");
        var max = Int(item, "maxLevel");
        label = $"{job} {min}-{max}";

        if (!catalog.IsKnownJob(job))
        {
            problems.Add($"Unknown job \"{job}\".");
            return null;
        }
        if (min < 1 || max > MaxLevel || min > max)
            problems.Add($"Levels must run from 1 to {MaxLevel}, low to high.");

        var errs = problems;
        IReadOnlyList<Ability> Steps(JsonElement parent, string name)
        {
            var result = new List<Ability>();
            if (!parent.TryGetProperty(name, out var list) || list.ValueKind != JsonValueKind.Array) return result;
            foreach (var step in list.EnumerateArray())
            {
                if (step.ValueKind != JsonValueKind.String)
                {
                    errs.Add($"Steps in \"{name}\" must be ability names.");
                    continue;
                }
                var stepName = step.GetString()!;
                if (catalog.FindActionByName(job, stepName) is not { } ability)
                    errs.Add($"\"{stepName}\" is not a {job} action.");
                else if (ability.Level > max)
                    errs.Add($"\"{ability.Name}\" unlocks at {ability.Level}, above this band.");
                else
                    result.Add(ability);
            }
            return result;
        }

        CardVignette Vignette(string name)
        {
            var element = item.TryGetProperty(name, out var v) ? v : default;
            if (element.ValueKind != JsonValueKind.Object)
            {
                errs.Add($"Missing \"{name}\".");
                return new CardVignette(Array.Empty<Ability>(), "");
            }
            if (!element.TryGetProperty("steps", out var raw) || raw.ValueKind != JsonValueKind.Array
                || raw.GetArrayLength() == 0)
                errs.Add($"\"{name}\" has no steps.");
            return new CardVignette(Steps(element, "steps"), Str(element, "note"), Int(element, "threshold"));
        }

        var card = new LevellingCard(job, min, max, Vignette("pull"), Vignette("single"),
            Steps(item, "weave"), Steps(item, "mitigation"), Str(item, "tip"));
        return problems.Count == 0 ? card : null;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;
}
