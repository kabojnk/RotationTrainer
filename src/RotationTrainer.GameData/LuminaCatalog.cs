using System.Text.RegularExpressions;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using RotationTrainer.Core;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace RotationTrainer.GameData;

/// <summary>
/// Level-100 combat kits and medicine read from the game's Excel sheets. Names come from the
/// module's default language; job abbreviations and the medicine filter use English so the
/// keys stay stable whatever the client language.
/// </summary>
public sealed class LuminaCatalog : IAbilityCatalog
{
    private const int MaxLevel = 100;
    private const byte GcdCooldownGroup = 58;

    // ActionCategory rows.
    private const uint SpellCategory = 2;
    private const uint WeaponskillCategory = 3;
    private const uint AbilityCategoryRow = 4;

    private sealed class Kit
    {
        public readonly Dictionary<uint, Ability> ById = new();
        public readonly Dictionary<string, Ability> ByName = new();
        public IReadOnlyList<Ability> Palette = Array.Empty<Ability>();
    }

    private readonly Dictionary<string, Kit> _kits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, Ability> _itemsById = new();
    private readonly Dictionary<string, Ability> _itemsByName = new();

    public LuminaCatalog(ExcelModule excel)
    {
        var jobs = excel.GetSheet<ClassJob>(Language.English)
            .Where(j => j.JobIndex > 0 && j.DohDolJobIndex < 0)
            .ToList();
        Jobs = jobs.Select(j => j.Abbreviation.ExtractText()).ToList();
        var medicine = LoadMedicine(excel, jobs);
        var actions = excel.GetSheet<LuminaAction>()
            .Where(a => !a.IsPvP && a.ClassJobLevel is >= 1 and <= MaxLevel && a.CooldownGroup != 0
                        && a.ClassJobCategory.RowId != 0
                        && a.ActionCategory.RowId is SpellCategory or WeaponskillCategory or AbilityCategoryRow)
            .ToList();
        var categories = excel.GetSheet<RawRow>(null, "ClassJobCategory");
        var combatJobIds = jobs.Select(j => j.RowId).ToList();
        var shared = new Dictionary<uint, Ability>();

        // ClassJobCategory has one bool column per ClassJob row, after the Name column.
        bool Covers(uint category, uint classJob) =>
            (bool)categories.GetRow(category).ReadColumn((int)classJob + 1);

        foreach (var job in jobs)
        {
            var kit = new Kit();
            var inKit = actions
                .Where(a => Covers(a.ClassJobCategory.RowId, job.RowId))
                // Actions only reachable as replacements (e.g. Pom Muse) are not player actions;
                // keep those only when their category is job-specific, not "All Classes" filler.
                .Where(a => a.IsPlayerAction || combatJobIds.Any(id => !Covers(a.ClassJobCategory.RowId, id)))
                .OrderByDescending(a => a.IsPlayerAction)
                .ThenBy(a => a.RowId);

            foreach (var row in inKit)
            {
                if (!shared.TryGetValue(row.RowId, out var ability))
                    shared[row.RowId] = ability = ToAbility(row);
                kit.ById[row.RowId] = ability;
                kit.ByName.TryAdd(Norm(ability.Name), ability);
            }

            var primary = PrimaryStatName(excel, job);
            kit.Palette = kit.ByName.Values
                .OrderBy(a => a.Group)
                .ThenBy(a => a.Id)
                .Concat(medicine.Where(m => m.Stat == primary).Select(m => m.Ability))
                .ToList();
            _kits[job.Abbreviation.ExtractText()] = kit;
        }

        foreach (var (ability, _) in medicine)
        {
            _itemsById[ability.Id] = ability;
            _itemsByName.TryAdd(Norm(ability.Name), ability);
        }
    }

    /// <summary>The combat job abbreviations, in ClassJob sheet order.</summary>
    public IReadOnlyList<string> Jobs { get; }

    public bool IsKnownJob(string job) => _kits.ContainsKey(job);

    public Ability? FindAction(string job, uint id) =>
        _kits.TryGetValue(job, out var kit) ? kit.ById.GetValueOrDefault(id) : null;

    public Ability? FindActionByName(string job, string name) =>
        _kits.TryGetValue(job, out var kit) ? kit.ByName.GetValueOrDefault(Norm(name)) : null;

    public Ability? FindItem(uint id) => _itemsById.GetValueOrDefault(id);

    public Ability? FindItemByName(string name) => _itemsByName.GetValueOrDefault(Norm(name));

    public IReadOnlyList<Ability> Palette(string job) =>
        _kits.TryGetValue(job, out var kit) ? kit.Palette : Array.Empty<Ability>();

    private static Ability ToAbility(LuminaAction row)
    {
        // Charged GCDs (e.g. MCH Drill) keep their charge timer in CooldownGroup and the GCD in
        // AdditionalCooldownGroup.
        var isGcd = row.CooldownGroup == GcdCooldownGroup || row.AdditionalCooldownGroup == GcdCooldownGroup;
        var category = row.ActionCategory.RowId switch
        {
            SpellCategory => AbilityCategory.Spell,
            WeaponskillCategory => AbilityCategory.Weaponskill,
            _ => AbilityCategory.Ability,
        };
        var group = row.IsRoleAction ? PaletteGroup.Role : isGcd ? PaletteGroup.Gcd : PaletteGroup.Weave;
        return new Ability(StepType.Action, row.RowId, row.Name.ExtractText(), row.Icon, category,
            isGcd, row.Cast100ms * 100, row.Recast100ms * 100, group);
    }

    private static string PrimaryStatName(ExcelModule excel, ClassJob job) =>
        excel.GetSheet<BaseParam>(Language.English).GetRowOrDefault(job.PrimaryStat)?.Name.ExtractText() ?? "";

    /// <summary>Main-stat tinctures and gemdraughts, newest grade first, tagged with their stat.</summary>
    private static List<(Ability Ability, string Stat)> LoadMedicine(ExcelModule excel, IEnumerable<ClassJob> jobs)
    {
        var mainStats = jobs.Select(j => PrimaryStatName(excel, j)).Where(s => s.Length > 0).ToHashSet();
        var localized = excel.GetSheet<Item>();
        var result = new List<(Ability Ability, string Stat, uint ItemLevel)>();

        foreach (var item in excel.GetSheet<Item>(Language.English))
        {
            if (item.ItemUICategory.ValueNullable?.Name.ExtractText() != "Medicine") continue;
            var english = item.Name.ExtractText();
            if (!english.Contains("Gemdraught") && !english.Contains("Tincture")) continue;
            var stat = mainStats.FirstOrDefault(s => english.EndsWith(" of " + s, StringComparison.Ordinal));
            if (stat == null) continue;

            var name = localized.GetRowOrDefault(item.RowId)?.Name.ExtractText() ?? english;
            var ability = new Ability(StepType.Item, item.RowId, name, item.Icon, AbilityCategory.Item,
                false, 0, item.Cooldowns * 1000, PaletteGroup.Medicine);
            result.Add((ability, stat, item.LevelItem.RowId));
        }

        return result
            .OrderByDescending(m => m.ItemLevel)
            .ThenByDescending(m => m.Ability.Id)
            .Select(m => (m.Ability, m.Stat))
            .ToList();
    }

    private static string Norm(string s) => Regex.Replace(s.Trim(), @"\s+", " ").ToLowerInvariant();
}
