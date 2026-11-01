using System.Text.RegularExpressions;
using RotationTrainer.Core;

namespace RotationTrainer.Core.Tests.Fakes;

public sealed class FakeCatalog : IAbilityCatalog
{
    private static Ability Gcd(uint id, string name, int cast, int recast) =>
        new(StepType.Action, id, name, id, AbilityCategory.Spell, true, cast, recast, PaletteGroup.Gcd);

    private static Ability Weave(uint id, string name) =>
        new(StepType.Action, id, name, id, AbilityCategory.Ability, false, 0, 0, PaletteGroup.Weave);

    private static readonly Ability Swiftcast =
        new(StepType.Action, 20, "Swiftcast", 20, AbilityCategory.Ability, false, 0, 0, PaletteGroup.Role);

    private static readonly Ability Gemdraught =
        new(StepType.Item, 49235, "Gemdraught of Intelligence", 49235, AbilityCategory.Item, false, 0, 0, PaletteGroup.Medicine);

    private static Ability Leveled(uint id, string name, bool gcd, int level) =>
        new(StepType.Action, id, name, id, gcd ? AbilityCategory.Weaponskill : AbilityCategory.Ability, gcd, 0,
            gcd ? 2500 : 0, gcd ? PaletteGroup.Gcd : PaletteGroup.Weave, level);

    private readonly Dictionary<string, List<Ability>> _jobs = new()
    {
        ["PLD"] = new()
        {
            Leveled(100, "Fast Blade", true, 1), Leveled(101, "Riot Blade", true, 4),
            Leveled(102, "Total Eclipse", true, 6), Leveled(103, "Fight or Flight", false, 2),
            Leveled(104, "Rampart", false, 8), Leveled(105, "Royal Authority", true, 60),
            Leveled(106, "Requiescat", false, 68),
        },
        ["PCT"] = new()
        {
            Gcd(1, "Rainbow Drip", 4000, 6000), Gcd(2, "Wing Motif", 3000, 4000),
            Gcd(3, "Hammer Stamp", 0, 2500), Gcd(4, "Fire in Red", 1500, 2500),
            Weave(10, "Pom Muse"), Weave(11, "Striking Muse"), Weave(12, "Starry Muse"),
            Swiftcast,
        },
        ["BLM"] = new()
        {
            Gcd(30, "Fire IV", 2000, 2500),
            Swiftcast,
        },
    };

    private static string Norm(string s) => Regex.Replace(s.Trim(), @"\s+", " ").ToLowerInvariant();

    public bool IsKnownJob(string job) => _jobs.ContainsKey(job);

    public Ability? FindAction(string job, uint id) =>
        _jobs.TryGetValue(job, out var l) ? l.FirstOrDefault(a => a.Id == id) : null;

    public Ability? FindActionByName(string job, string name) =>
        _jobs.TryGetValue(job, out var l) ? l.FirstOrDefault(a => Norm(a.Name) == Norm(name)) : null;

    public Ability? FindItem(uint id) => id == Gemdraught.Id ? Gemdraught : null;

    public Ability? FindItemByName(string name) => Norm(name) == Norm(Gemdraught.Name) ? Gemdraught : null;

    public IReadOnlyList<Ability> Palette(string job) =>
        _jobs.TryGetValue(job, out var l) ? l.Append(Gemdraught).ToList() : Array.Empty<Ability>();
}
