using Xunit;

namespace RotationTrainer.Core.Tests;

public class TimingTests
{
    [Theory]
    [InlineData(2500, 420, 2.50)]
    [InlineData(2500, 744, 2.46)]
    [InlineData(2500, 1020, 2.43)]
    [InlineData(1500, 420, 1.50)]
    public void AdjustSecondsMatchesGameFormula(int baseMs, int speed, double expected) =>
        Assert.Equal(expected, SpeedMath.AdjustSeconds(baseMs, speed), 6);

    private static Ability Make(AbilityCategory c, bool gcd, int cast, int recast) =>
        new(StepType.Action, 1, "x", 1, c, gcd, cast, recast, PaletteGroup.Gcd);

    [Fact]
    public void SpellsUseSpellSpeedWeaponskillsUseSkillSpeed()
    {
        var src = new StatTimingSource(new SpeedProfile(1020, 420));
        var spell = Make(AbilityCategory.Spell, true, 2500, 2500);
        var ws = Make(AbilityCategory.Weaponskill, true, 2500, 2500);
        Assert.Equal(2.43, src.CastSeconds(spell), 6);
        Assert.Equal(2.43, src.RecastSeconds(spell), 6);
        Assert.Equal(2.50, src.CastSeconds(ws), 6);
        Assert.Equal(2.50, src.RecastSeconds(ws), 6);
    }

    [Fact]
    public void AbilitiesAndItemsAreNotScaled()
    {
        var src = new StatTimingSource(new SpeedProfile(1020, 1020));
        var ab = Make(AbilityCategory.Ability, false, 0, 60000);
        var item = Make(AbilityCategory.Item, false, 0, 2500);
        Assert.Equal(60.0, src.RecastSeconds(ab), 6);
        Assert.Equal(2.5, src.RecastSeconds(item), 6);
        Assert.Equal(0.0, src.CastSeconds(ab), 6);
    }
}
