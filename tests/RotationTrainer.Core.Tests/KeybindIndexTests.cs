using Xunit;

namespace RotationTrainer.Core.Tests;

public class KeybindIndexTests
{
    private const uint Fire = 100, Blizzard = 200, Other = 300;

    private static HotbarObservation Obs(int bar, int slot, uint command, uint apparent, string? hint, bool cross = false) =>
        new(bar, slot, cross, command, apparent, hint);

    private static KeybindIndex With(params HotbarObservation[] obs)
    {
        var index = new KeybindIndex();
        index.Update(obs);
        return index;
    }

    [Fact]
    public void ApparentMatchReturnsHint() =>
        Assert.Equal("1", With(Obs(0, 0, Fire, Fire, "1")).Label(Fire, InputMode.Keyboard));

    [Fact]
    public void LearnsReplacedAction()
    {
        var index = With(Obs(0, 0, Fire, Blizzard, "1"));
        index.Update(new[] { Obs(0, 0, Fire, Fire, "1") });
        Assert.Equal("1", index.Label(Blizzard, InputMode.Keyboard));
    }

    [Fact]
    public void LearnedSlotForgottenWhenCommandChanges()
    {
        var index = With(Obs(0, 0, Fire, Blizzard, "1"));
        index.Update(new[] { Obs(0, 0, Other, Other, "1") });
        Assert.Equal("?", index.Label(Blizzard, InputMode.Keyboard));
    }

    [Fact]
    public void CommandIdMatchWhenNotApparent() =>
        Assert.Equal("2", With(Obs(0, 1, Fire, Blizzard, "2")).Label(Fire, InputMode.Keyboard));

    [Fact]
    public void NotOnAnyBarIsQuestionMark() =>
        Assert.Equal("?", With(Obs(0, 0, Fire, Fire, "1")).Label(Other, InputMode.Keyboard));

    [Fact]
    public void ZeroActionIsQuestionMark() =>
        Assert.Equal("?", With(Obs(0, 0, 0, 0, "1")).Label(0, InputMode.Keyboard));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyHintIsQuestionMark(string? hint) =>
        Assert.Equal("?", With(Obs(0, 0, Fire, Fire, hint)).Label(Fire, InputMode.Keyboard));

    [Fact]
    public void EmptyHintDoesNotFallThrough() =>
        Assert.Equal("?", With(Obs(0, 0, Fire, Fire, ""), Obs(1, 0, Other, Fire, "9")).Label(Fire, InputMode.Keyboard));

    [Fact]
    public void ModeFiltersBarType()
    {
        var index = With(Obs(0, 0, Fire, Fire, "K"), Obs(0, 0, Fire, Fire, "C", cross: true), Obs(1, 0, Other, Other, "X", cross: true));
        Assert.Equal("K", index.Label(Fire, InputMode.Keyboard));
        Assert.Equal("C", index.Label(Fire, InputMode.Controller));
        Assert.Equal("?", index.Label(Other, InputMode.Keyboard));
    }

    [Fact]
    public void PrefersLowestBarThenSlot()
    {
        var index = With(Obs(2, 0, Fire, Fire, "c"), Obs(1, 5, Fire, Fire, "b"), Obs(1, 3, Fire, Fire, "a"));
        Assert.Equal("a", index.Label(Fire, InputMode.Keyboard));
    }

    [Fact]
    public void ApparentBeatsCommandMatch() =>
        Assert.Equal("app", With(Obs(0, 0, Fire, Other, "cmd"), Obs(5, 5, Other, Fire, "app")).Label(Fire, InputMode.Keyboard));

    [Fact]
    public void ClearForgetsLearned()
    {
        var index = With(Obs(0, 0, Fire, Blizzard, "1"));
        index.Update(new[] { Obs(0, 0, Fire, Fire, "1") });
        index.Clear();
        index.Update(new[] { Obs(0, 0, Fire, Fire, "1") });
        Assert.Equal("?", index.Label(Blizzard, InputMode.Keyboard));
        index.Clear();
        Assert.Equal("?", index.Label(Fire, InputMode.Keyboard));
    }
}
