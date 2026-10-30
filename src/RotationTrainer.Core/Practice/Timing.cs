namespace RotationTrainer.Core;

public sealed record SpeedProfile(int SpellSpeed, int SkillSpeed);

public static class SpeedMath
{
    public const int Sub = 420;
    public const int Div = 2780;

    public static double AdjustSeconds(int baseMs, int speed)
    {
        var f = FloorDiv(130L * (speed - Sub), Div) + 1000;
        var ms = FloorDiv((2000 - f) * baseMs, 1000);
        return FloorDiv(ms, 10) / 100.0;
    }

    private static long FloorDiv(long a, long b)
    {
        var q = a / b;
        return a % b != 0 && (a < 0) != (b < 0) ? q - 1 : q;
    }
}

public interface ITimingSource
{
    double CastSeconds(Ability a);
    double RecastSeconds(Ability a);
}

public sealed class StatTimingSource(SpeedProfile profile) : ITimingSource
{
    public double CastSeconds(Ability a) => Scale(a, a.BaseCastMs);
    public double RecastSeconds(Ability a) => Scale(a, a.BaseRecastMs);

    private double Scale(Ability a, int baseMs) => a.Category switch
    {
        AbilityCategory.Spell => SpeedMath.AdjustSeconds(baseMs, profile.SpellSpeed),
        AbilityCategory.Weaponskill => SpeedMath.AdjustSeconds(baseMs, profile.SkillSpeed),
        _ => baseMs / 1000.0,
    };
}
