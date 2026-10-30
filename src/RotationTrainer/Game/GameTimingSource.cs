using FFXIVClientStructs.FFXIV.Client.Game;
using RotationTrainer.Core;
using GameActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace RotationTrainer.Game;

/// <summary>
/// Cast and recast times as the game computes them for the current character (speed, haste traits).
/// Items use their base timings. Call on the framework thread only.
/// </summary>
/// <param name="currentKit">The current job's palette, used to find how the game scales a 2.5 s GCD.</param>
public sealed unsafe class GameTimingSource(Func<IReadOnlyList<Ability>> currentKit) : ITimingSource
{
    private const int BaseGcdMs = 2500;

    public double CastSeconds(Ability a) =>
        a.Type == StepType.Item ? a.BaseCastMs / 1000.0 : Seconds(ActionManager.GetAdjustedCastTime(GameActionType.Action, a.Id, false, null), a.BaseCastMs);

    public double RecastSeconds(Ability a)
    {
        if (a.Type == StepType.Item) return a.BaseRecastMs / 1000.0;
        var ms = Recast(a.Id);
        // Speed and haste only ever shorten a GCD. Longer means the game reported a charge or cooldown timer
        // (Drill, Gnashing Fang, Standard Step…): scale the catalog's GCD lock the way the game scales a plain GCD.
        if (a.IsGcd && ms > a.BaseRecastMs) return a.BaseRecastMs * GcdScale(a.Category) / 1000.0;
        return Seconds(ms, a.BaseRecastMs);
    }

    private static int Recast(uint id) => ActionManager.GetAdjustedRecastTime(GameActionType.Action, id, true);

    /// <summary>Adjusted / base recast of a plain 2.5 s GCD of the same category; 1 when there is none.</summary>
    private double GcdScale(AbilityCategory category)
    {
        if (category is not (AbilityCategory.Spell or AbilityCategory.Weaponskill)) return 1;
        foreach (var gcd in currentKit())
        {
            if (!gcd.IsGcd || gcd.Category != category || gcd.BaseRecastMs != BaseGcdMs) continue;
            var ms = Recast(gcd.Id);
            if (ms > 0 && ms <= BaseGcdMs) return ms / (double)BaseGcdMs;
        }
        return 1;
    }

    // A negative result means the game could not compute it (e.g. while loading); fall back to the base time.
    private static double Seconds(int ms, int baseMs) => (ms >= 0 ? ms : baseMs) / 1000.0;
}
