namespace RotationTrainer.Core;

public enum AbilityCategory { Spell, Weaponskill, Ability, Item }

public enum PaletteGroup { Gcd, Weave, Role, Medicine }

public sealed record Ability(StepType Type, uint Id, string Name, uint IconId, AbilityCategory Category,
    bool IsGcd, int BaseCastMs, int BaseRecastMs, PaletteGroup Group);

public interface IAbilityCatalog
{
    bool IsKnownJob(string job);
    /// <summary>Null if not in that job's level-100 kit (including role actions).</summary>
    Ability? FindAction(string job, uint id);
    Ability? FindActionByName(string job, string name);
    /// <summary>Medicine only; id already normalized.</summary>
    Ability? FindItem(uint id);
    Ability? FindItemByName(string name);
    IReadOnlyList<Ability> Palette(string job);
    /// <summary>
    /// One id per button: variants a button can fire (e.g. NIN's second mudra, DNC's Double Standard Finish)
    /// map to the id of the action they stand for. Unknown ids map to themselves.
    /// </summary>
    uint CanonicalId(string job, uint id) => id;
}

public static class ItemIds
{
    public static uint Normalize(uint id) => id % 1_000_000;
}
