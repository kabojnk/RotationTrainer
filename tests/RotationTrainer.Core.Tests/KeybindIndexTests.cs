using Xunit;

namespace RotationTrainer.Core.Tests;

public class KeybindIndexTests
{
    private const uint Fire = 100, Blizzard = 200, Other = 300;

    private static HotbarObservation Obs(int bar, int slot, uint command, uint apparent, string? hint, bool cross = false,
        StepType type = StepType.Action) =>
        new(bar, slot, cross, type, command, apparent, hint);

    private static KeybindIndex With(params HotbarObservation[] obs)
    {
        var index = new KeybindIndex();
        index.Update(obs);
        return index;
    }

    [Fact]
    public void ApparentMatchReturnsHint() =>
        Assert.Equal("1", With(Obs(0, 0, Fire, Fire, "1")).Label(StepType.Action, Fire, InputMode.Keyboard));

    [Fact]
    public void LearnsReplacedAction()
    {
        var index = With(Obs(0, 0, Fire, Blizzard, "1"));
        index.Update(new[] { Obs(0, 0, Fire, Fire, "1") });
        Assert.Equal("1", index.Label(StepType.Action, Blizzard, InputMode.Keyboard));
    }

    [Fact]
    public void LearnedSlotForgottenWhenCommandChanges()
    {
        var index = With(Obs(0, 0, Fire, Blizzard, "1"));
        index.Update(new[] { Obs(0, 0, Other, Other, "1") });
        Assert.Equal("?", index.Label(StepType.Action, Blizzard, InputMode.Keyboard));
    }

    [Fact]
    public void CommandIdMatchWhenNotApparent() =>
        Assert.Equal("2", With(Obs(0, 1, Fire, Blizzard, "2")).Label(StepType.Action, Fire, InputMode.Keyboard));

    [Fact]
    public void NotOnAnyBarIsQuestionMark() =>
        Assert.Equal("?", With(Obs(0, 0, Fire, Fire, "1")).Label(StepType.Action, Other, InputMode.Keyboard));

    [Fact]
    public void ZeroActionIsQuestionMark() =>
        Assert.Equal("?", With(Obs(0, 0, 0, 0, "1")).Label(StepType.Action, 0, InputMode.Keyboard));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyHintIsQuestionMark(string? hint) =>
        Assert.Equal("?", With(Obs(0, 0, Fire, Fire, hint)).Label(StepType.Action, Fire, InputMode.Keyboard));

    [Fact]
    public void EmptyHintDoesNotFallThrough() =>
        Assert.Equal("?", With(Obs(0, 0, Fire, Fire, ""), Obs(1, 0, Other, Fire, "9")).Label(StepType.Action, Fire, InputMode.Keyboard));

    [Fact]
    public void ModeFiltersBarType()
    {
        var index = With(Obs(0, 0, Fire, Fire, "K"), Obs(0, 0, Fire, Fire, "C", cross: true), Obs(1, 0, Other, Other, "X", cross: true));
        Assert.Equal("K", index.Label(StepType.Action, Fire, InputMode.Keyboard));
        Assert.Equal("C", index.Label(StepType.Action, Fire, InputMode.Controller));
        Assert.Equal("?", index.Label(StepType.Action, Other, InputMode.Keyboard));
    }

    [Fact]
    public void PrefersLowestBarThenSlot()
    {
        var index = With(Obs(2, 0, Fire, Fire, "c"), Obs(1, 5, Fire, Fire, "b"), Obs(1, 3, Fire, Fire, "a"));
        Assert.Equal("a", index.Label(StepType.Action, Fire, InputMode.Keyboard));
    }

    [Fact]
    public void ApparentBeatsCommandMatch() =>
        Assert.Equal("app", With(Obs(0, 0, Fire, Other, "cmd"), Obs(5, 5, Other, Fire, "app")).Label(StepType.Action, Fire, InputMode.Keyboard));

    [Fact]
    public void ClearForgetsLearned()
    {
        var index = With(Obs(0, 0, Fire, Blizzard, "1"));
        index.Update(new[] { Obs(0, 0, Fire, Fire, "1") });
        index.Clear();
        index.Update(new[] { Obs(0, 0, Fire, Fire, "1") });
        Assert.Equal("?", index.Label(StepType.Action, Blizzard, InputMode.Keyboard));
        index.Clear();
        Assert.Equal("?", index.Label(StepType.Action, Fire, InputMode.Keyboard));
    }

    [Fact]
    public void ItemAndActionWithSameIdDoNotShareLabel()
    {
        var index = With(Obs(0, 0, Fire, Fire, "item", type: StepType.Item), Obs(1, 0, Blizzard, Blizzard, "action"));
        Assert.Equal("?", index.Label(StepType.Action, Fire, InputMode.Keyboard));
        Assert.Equal("item", index.Label(StepType.Item, Fire, InputMode.Keyboard));
        Assert.Equal("?", index.Label(StepType.Item, Blizzard, InputMode.Keyboard));
        Assert.Equal("action", index.Label(StepType.Action, Blizzard, InputMode.Keyboard));
    }

    [Fact]
    public void ItemMatchedOnNormalizedId() =>
        Assert.Equal("p", With(Obs(0, 0, 1_049_237, 1_049_237, "p", type: StepType.Item)).Label(StepType.Item, 49_237, InputMode.Keyboard));

    [Fact]
    public void ZeroCommandIsNeverMatchedOrLearned()
    {
        // A macro slot (no action command) that once showed Blizzard teaches nothing.
        var index = With(Obs(0, 0, 0, Blizzard, "1"));
        index.Update(new[] { Obs(0, 0, 0, Fire, "1") });
        Assert.Equal("?", index.Label(StepType.Action, Blizzard, InputMode.Keyboard));
    }
}
