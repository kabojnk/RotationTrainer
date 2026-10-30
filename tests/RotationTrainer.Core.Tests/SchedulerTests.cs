using RotationTrainer.Core.Tests.Fakes;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class SchedulerTests
{
    private static readonly FakeCatalog Cat = new();
    private static Step A(uint id, double? at = null) => new(StepType.Action, id, "x", at);
    private static readonly Step Pull = new(StepType.Pull, 0, "Pull");
    private static readonly Step Potion = new(StepType.Item, 49235, "Gemdraught");

    private static Scheduler Make() => new(new StatTimingSource(new SpeedProfile(420, 420)), 0.65);

    private static PracticeSequence Seq(params Step[] steps) =>
        PracticeSequence.Build(new Rotation("o", "PCT", 100, RotationKind.Opener, "o", steps), null, true, Cat);

    private static List<double> Run(Scheduler s, PracticeSequence seq) =>
        Enumerable.Range(0, seq.OpenerLength).Select(i => s.Next(seq[i]!)).ToList();

    [Fact]
    public void OpenerTimesMatchRules()
    {
        var seq = Seq(A(1, -4.5), Pull, Potion, A(10), A(11), A(2), A(12), A(3));
        var t = Run(Make(), seq);
        double[] expected = { -4.5, 0.0, 0.65, 1.30, 1.95, 4.95, 5.95 };
        Assert.Equal(expected.Length, t.Count);
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], t[i], 6);
    }

    [Fact]
    public void PrepullWithoutAtFollowsPrevious()
    {
        var t = Run(Make(), Seq(A(1, -4.5), A(10), Pull, A(3)));
        Assert.Equal(-4.5, t[0], 6);
        Assert.Equal(-0.5, t[1], 6); // previous + max(cast 4.0, lock)
    }

    [Fact]
    public void FirstPrepullWithoutAtIsJustBeforePull()
    {
        var t = Run(Make(), Seq(A(4), Pull, A(3)));
        Assert.Equal(-0.65, t[0], 6);
    }

    [Fact]
    public void ResetStartsOver()
    {
        var s = Make();
        var seq = Seq(A(1, -4.5), Pull, A(3));
        var first = Run(s, seq);
        s.Reset();
        Assert.Equal(first, Run(s, seq));
    }

    [Fact]
    public void PostPullFirstStepStartsAtZero()
    {
        var t = Run(Make(), Seq(A(4), A(3)));
        Assert.Equal(0.0, t[0], 6);
        Assert.Equal(2.5, t[1], 6); // GCD timer (Fire in Red recast)
    }

    [Fact]
    public void LoopOnlySequenceStartsAtZero()
    {
        var loop = new Rotation("l", "PCT", 100, RotationKind.Loop, "l", new[] { A(4), A(3) });
        var seq = PracticeSequence.Build(null, loop, true, Cat);
        var s = Make();
        Assert.Equal(0.0, s.Next(seq[0]!), 6);
        Assert.Equal(2.5, s.Next(seq[1]!), 6);
    }

    [Fact]
    public void GcdTimerFollowsRecastActionTimerFollowsCastOrLock()
    {
        // Fire in Red: cast 1.5, recast 2.5. Weave waits for cast end, next GCD for recast end.
        var t = Run(Make(), Seq(A(4), A(10), A(4)));
        Assert.Equal(0.0, t[0], 6);
        Assert.Equal(1.5, t[1], 6);
        Assert.Equal(2.5, t[2], 6);
    }
}
