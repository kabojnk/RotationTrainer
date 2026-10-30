using RotationTrainer.Core.Tests.Fakes;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class PracticeSequenceTests
{
    private static readonly FakeCatalog Cat = new();

    private static Step A(uint id, double? at = null) => new(StepType.Action, id, "x", at);
    private static readonly Step Pull = new(StepType.Pull, 0, "Pull");
    private static readonly Step Potion = new(StepType.Item, 49235, "Gemdraught");

    private static Rotation Rot(string id, RotationKind kind, params Step[] steps) =>
        new(id, "PCT", 100, kind, id, steps);

    private static Rotation Opener5() => Rot("o", RotationKind.Opener, A(2, -4.5), Pull, A(1), A(10), A(3), A(4));
    private static Rotation Loop3() => Rot("l", RotationKind.Loop, A(4), A(11), A(3));

    [Fact]
    public void OpenerThenLoopRepeats()
    {
        var s = PracticeSequence.Build(Opener5(), Loop3(), true, Cat);
        Assert.Equal(5, s.OpenerLength);
        Assert.True(s.IsEndless);
        Assert.True(s[5]!.FromLoop);
        Assert.Equal(4u, s[5]!.Ability.Id);
        Assert.Equal(4u, s[8]!.Ability.Id);
        Assert.Equal(11u, s[9]!.Ability.Id);
        Assert.False(s[4]!.FromLoop);
    }

    [Fact]
    public void PullMarkerIsNotAStep()
    {
        var s = PracticeSequence.Build(Opener5(), null, true, Cat);
        Assert.Equal(5, s.OpenerLength);
        Assert.DoesNotContain(Enumerable.Range(0, 5), i => s[i]!.Ability.Type == StepType.Pull);
    }

    [Fact]
    public void PrepullStepsCarryAtAndFlag()
    {
        var s = PracticeSequence.Build(Opener5(), null, true, Cat);
        Assert.Equal(-4.5, s[0]!.At);
        Assert.True(s[0]!.IsPrepull);
        Assert.False(s[1]!.IsPrepull);
    }

    [Fact]
    public void PotionsHiddenWhenOff()
    {
        var o = Rot("o", RotationKind.Opener, Potion, A(2, -4.5), Pull, A(1));
        var s = PracticeSequence.Build(o, null, false, Cat);
        Assert.Equal(2, s.OpenerLength);
        Assert.DoesNotContain(Enumerable.Range(0, 2), i => s[i]!.Ability.Category == AbilityCategory.Item);
    }

    [Fact]
    public void PotionsShownWhenOn()
    {
        var o = Rot("o", RotationKind.Opener, Potion, A(2, -4.5), Pull, A(1));
        var s = PracticeSequence.Build(o, null, true, Cat);
        Assert.Equal(3, s.OpenerLength);
        Assert.Equal(AbilityCategory.Item, s[0]!.Ability.Category);
        Assert.Null(s[0]!.GcdNumber);
    }

    [Fact]
    public void GcdNumbersSkipWeavesAndPrepull()
    {
        var s = PracticeSequence.Build(Opener5(), Loop3(), true, Cat);
        Assert.Null(s[0]!.GcdNumber);   // prepull GCD
        Assert.Equal(1, s[1]!.GcdNumber);
        Assert.Null(s[2]!.GcdNumber);   // weave
        Assert.Equal(2, s[3]!.GcdNumber);
        Assert.Equal(3, s[4]!.GcdNumber);
        Assert.Equal(1, s[5]!.GcdNumber); // loop restarts
        Assert.Null(s[6]!.GcdNumber);
        Assert.Equal(2, s[7]!.GcdNumber);
        Assert.Equal(1, s[8]!.GcdNumber);
    }

    [Fact]
    public void NoOpenerStartsWithLoop()
    {
        var s = PracticeSequence.Build(null, Loop3(), true, Cat);
        Assert.Equal(0, s.OpenerLength);
        Assert.True(s[0]!.FromLoop);
        Assert.Equal(4u, s[0]!.Ability.Id);
    }

    [Fact]
    public void NoLoopEndsAfterOpener()
    {
        var s = PracticeSequence.Build(Opener5(), null, true, Cat);
        Assert.False(s.IsEndless);
        Assert.NotNull(s[s.OpenerLength - 1]);
        Assert.Null(s[s.OpenerLength]);
    }

    [Fact]
    public void BothNullIsEmpty()
    {
        var s = PracticeSequence.Build(null, null, true, Cat);
        Assert.True(s.IsEmpty);
        Assert.Null(s[0]);
        Assert.True(PracticeSequence.Empty.IsEmpty);
    }

    [Fact]
    public void UnknownAbilitiesAreDropped()
    {
        var o = Rot("o", RotationKind.Opener, A(999), A(1));
        var s = PracticeSequence.Build(o, null, true, Cat);
        Assert.Equal(1, s.OpenerLength);
    }

    [Fact]
    public void DanglingSelectionResolvesToNull()
    {
        var lib = new RotationLibrary(new[] { Opener5() });
        var sel = new JobSelection { OpenerId = "gone", LoopId = null };
        var (o, l) = sel.Resolve(lib, "PCT");
        Assert.Null(o);
        Assert.Null(l);
        Assert.NotNull(new JobSelection { OpenerId = "o" }.Resolve(lib, "PCT").Opener);
    }

    [Fact]
    public void SelectionForOtherJobResolvesToNull()
    {
        var blm = new Rotation("b", "BLM", 100, RotationKind.Opener, "b", new[] { A(30) });
        var lib = new RotationLibrary(new[] { blm });
        Assert.Null(new JobSelection { OpenerId = "b" }.Resolve(lib, "PCT").Opener);
    }
}
