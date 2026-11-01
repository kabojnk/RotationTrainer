namespace RotationTrainer.Core;

public enum AbilityCategory { Spell, Weaponskill, Ability, Item }

public enum PaletteGroup { Gcd, Weave, Role, Medicine }

/// <param name="Level">The level the action unlocks at (0 for items and when unknown).</param>
public sealed record Ability(StepType Type, uint Id, string Name, uint IconId, AbilityCategory Category,
    bool IsGcd, int BaseCastMs, int BaseRecastMs, PaletteGroup Group, int Level = 0);

public interface IAbilityCatalog
{
    bool IsKnownJob(string job);
    /// <summary>Null if not in that job's level-100 kit (including role actions).</summary>
    Ability? FindAction(string job, uint id);
    Ability? FindActionByName(string job, string name);
    /// <summary>Medicine only; id already normalised.</summary>
    Ability? FindItem(uint id);
    Ability? FindItemByName(string name);
    IReadOnlyList<Ability> Palette(string job);
    /// <summary>
    /// One id per button: variants a button can fire (e.g. NIN's second mudra, DNC's Double Standard Finish)
    /// map to the id of the action they stand for. Unknown ids map to themselves.
    /// </summary>
    uint CanonicalId(string job, uint id) => id;
    /// <summary>Whether this (normalised) item id is any Medicine item: potions, ethers, elixirs, tinctures…</summary>
    bool IsMedicine(uint itemId) => FindItem(itemId) != null;
    /// <summary>
    /// The action ids of the hotbar button(s) this action appears on when it isn't a button of its own (Living Muse
    /// for Pom Muse, Fire in Red for Aero in Green). Empty when it has its own button or none is known.
    /// </summary>
    IReadOnlyList<uint> ButtonHosts(string job, uint id) => Array.Empty<uint>();
    /// <summary>The level a job starts at: 1 for jobs with a base class, otherwise 30, 50, 60, 70 or 80.</summary>
    int StartLevel(string job) => 1;
    /// <summary>The combat jobs a class grows into (ACN: SMN and SCH). A job maps to itself; others to none.</summary>
    IReadOnlyList<string> JobsForClass(string classAbbreviation) => Array.Empty<string>();
    /// <summary>The job's actions (role actions aside) and traits that unlock in this level range, by level then name.</summary>
    IReadOnlyList<Unlock> Unlocks(string job, int minLevel, int maxLevel) => Array.Empty<Unlock>();
}

/// <summary>An action or trait a job learns at a level.</summary>
public sealed record Unlock(string Name, int Level, uint IconId, bool IsTrait);

public static class ItemIds
{
    public static uint Normalize(uint id) => id % 1_000_000;
}
