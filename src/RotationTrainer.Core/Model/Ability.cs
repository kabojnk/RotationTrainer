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
}

public static class ItemIds
{
    public static uint Normalize(uint id) => id % 1_000_000;
}
