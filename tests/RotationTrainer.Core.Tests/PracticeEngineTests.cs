using RotationTrainer.Core.Tests.Fakes;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class PracticeEngineTests
{
    private static readonly FakeCatalog Cat = new();
    private static Step A(uint id, double? at = null) => new(StepType.Action, id, "x", at);
    private static readonly Step Pull = new(StepType.Pull, 0, "Pull");
    private static readonly Step Potion = new(StepType.Item, 49235, "Gemdraught");

    private const uint RD = 1, Pom = 10, Striking = 11, Wing = 2, Starry = 12, Hammer = 3, FireInRed = 4, Swiftcast = 20, Sprint = 3;

    private static PracticeSequence Opener() => PracticeSequence.Build(
        new Rotation("o", "PCT", 100, RotationKind.Opener, "o",
            new[] { A(RD, -4.5), Pull, Potion, A(Pom), A(Striking), A(Wing), A(Starry), A(Hammer) }),
        null, true, Cat);

    /// <summary>Instant steps only: Pom Muse at -4.5, then Hammer Stamp after the pull.</summary>
    private static PracticeSequence InstantOpener() => PracticeSequence.Build(
        new Rotation("i", "PCT", 100, RotationKind.Opener, "i", new[] { A(Pom, -4.5), Pull, A(Hammer) }),
        null, true, Cat);

    private static PracticeSequence LoopSeq() => PracticeSequence.Build(
        null,
        new Rotation("l", "PCT", 100, RotationKind.Loop, "l", new[] { A(FireInRed) }),
        true, Cat);

    private static (PracticeEngine e, List<EngineEvent> ev) Make(AdvanceMode mode = AdvanceMode.Manual,
        PracticeSequence? seq = null, Func<StepType, uint, bool>? relevant = null,
        Func<uint, uint, bool>? sameAction = null, double latency = 0)
    {
        var e = new PracticeEngine(seq ?? Opener(),
            new Scheduler(new StatTimingSource(new SpeedProfile(420, 420)), 0.65), mode,
            relevant ?? ((t, id) => id != Sprint), sameAction, latency);
        var ev = new List<EngineEvent>();
        e.Event += ev.Add;
        return (e, ev);
    }

    private static void Use(PracticeEngine e, uint id) => e.OnActionUsed(StepType.Action, id);

    [Fact]
    public void ManualPopsOnCorrectAction()
    {
        var (e, ev) = Make();
        Use(e, RD);
        Assert.Equal(new EngineEvent(EngineEventKind.Popped, 0), Assert.Single(ev));
        Assert.Equal(1, e.CurrentIndex);
    }

    [Fact]
    public void ManualWrongActionFlashesAndWaits()
    {
        var (e, ev) = Make();
        Use(e, Swiftcast);
        Assert.Equal(new EngineEvent(EngineEventKind.WrongPress, 0), Assert.Single(ev));
        Assert.Equal(0, e.CurrentIndex);
        Use(e, RD);
        Assert.Equal(1, e.RunLog[0].WrongPresses);
    }

    [Fact]
    public void IrrelevantActionIgnored()
    {
        var (e, ev) = Make();
        Use(e, Sprint);
        Assert.Empty(ev);
        Assert.Equal(0, e.CurrentIndex);
    }

    [Fact]
    public void SameAbilityTwiceAdvancesOneStepPerUse()
    {
        var (e, _) = Make(seq: LoopSeq());
        for (var i = 1; i <= 3; i++)
        {
            Use(e, FireInRed);
            Assert.Equal(i, e.CurrentIndex);
        }
    }

    [Fact]
    public void HqPotionMatches()
    {
        var (e, ev) = Make();
        Use(e, RD);
        e.OnActionUsed(StepType.Item, 1049235);
        Assert.Equal(new EngineEvent(EngineEventKind.Popped, 1), ev[^1]);
        Assert.Equal(2, e.CurrentIndex);
    }

    [Fact]
    public void FirstPressStartsClockWhenTheCastLands()
    {
        // Rainbow Drip (4 s cast) is expected at -4.5; its effect arrives when the cast ends.
        var (e, _) = Make();
        Assert.Null(e.Clock);
        Use(e, RD);
        Assert.Equal(-0.5, e.Clock!.Value, 6);
    }

    [Fact]
    public void FirstInstantPressStartsClockAtItsExpectedTime()
    {
        var (e, _) = Make(seq: InstantOpener());
        Use(e, Pom);
        Assert.Equal(-4.5, e.Clock!.Value, 6);
    }

    [Fact]
    public void WrongPressBeforeStartDoesNotStartClock()
    {
        var (e, _) = Make();
        Use(e, Swiftcast);
        Assert.Null(e.Clock);
    }

    [Fact]
    public void TickBeforeStartIsNoOp()
    {
        var (e, _) = Make(AdvanceMode.Auto);
        e.Tick(1);
        Assert.Null(e.Clock);
    }

    [Fact]
    public void CountdownSetsClock()
    {
        var (e, _) = Make();
        e.OnCountdown(5);
        Assert.Equal(-5, e.Clock!.Value, 6);
    }

    [Fact]
    public void AutoPausesWhenInstantStepReachesMarker()
    {
        var (e, ev) = Make(AdvanceMode.Auto, InstantOpener(), latency: 0.1);
        e.OnCountdown(5);
        e.Tick(0.5);
        Assert.True(e.IsPaused);
        Assert.Equal(new EngineEvent(EngineEventKind.Paused, 0), Assert.Single(ev));
        e.Tick(1);
        Assert.Equal(-4.5, e.Clock!.Value, 6);
        Assert.Single(ev);
    }

    [Fact]
    public void AutoResumesAfterLatePress()
    {
        var (e, ev) = Make(AdvanceMode.Auto, InstantOpener());
        e.OnCountdown(5);
        e.Tick(0.5);
        e.Tick(1);
        Use(e, Pom);
        Assert.Equal(EngineEventKind.Popped, ev[1].Kind);
        Assert.Equal(new EngineEvent(EngineEventKind.Resumed, 0), ev[2]);
        Assert.False(e.IsPaused);
        e.Tick(0.2);
        Assert.Equal(-4.3, e.Clock!.Value, 6);
    }

    [Fact]
    public void AutoEarlyPressPopsImmediately()
    {
        var (e, ev) = Make(AdvanceMode.Auto);
        e.OnCountdown(5);
        Use(e, RD);
        Assert.Equal(new EngineEvent(EngineEventKind.Popped, 0), Assert.Single(ev));
        Assert.Equal(-5, e.Clock!.Value, 6);
    }

    [Fact]
    public void AutoPausesAgainWhenNextStepAlreadyDue()
    {
        var (e, ev) = Make(AdvanceMode.Auto);
        e.OnCountdown(5);
        e.Tick(0.5);
        Use(e, RD); // clock -4.5 (an early pop), next step (Gemdraught) expected at 0.0
        e.Tick(10);
        Assert.True(e.IsPaused);
        Assert.Equal(0.0, e.Clock!.Value, 6);
        Assert.Equal(new EngineEvent(EngineEventKind.Paused, 1), ev[^1]);
    }

    [Fact]
    public void ManualClockNeverPauses()
    {
        var (e, _) = Make();
        e.OnCountdown(5);
        e.Tick(2);
        Assert.Equal(-3, e.Clock!.Value, 6);
        Assert.False(e.IsPaused);
    }

    [Fact]
    public void PrepullPressDuringCountdownPops()
    {
        var (e, ev) = Make();
        e.OnCountdown(5);
        e.Tick(0.3);
        Use(e, RD);
        Assert.Equal(new EngineEvent(EngineEventKind.Popped, 0), Assert.Single(ev));
    }

    [Fact]
    public void CountdownCancelledResets()
    {
        var (e, _) = Make();
        e.OnCountdown(5);
        Use(e, RD);
        e.OnCountdownCancelled();
        Assert.Equal(0, e.CurrentIndex);
        Assert.Null(e.Clock);
        Assert.Empty(e.RunLog);
    }

    [Fact]
    public void NewCountdownMidRunRestarts()
    {
        var (e, _) = Make();
        Use(e, RD);
        e.OnActionUsed(StepType.Item, 49235);
        Use(e, Pom);
        Assert.Equal(3, e.CurrentIndex);
        e.OnCountdown(5);
        Assert.Equal(0, e.CurrentIndex);
        Assert.Equal(-5, e.Clock!.Value, 6);
        Assert.Empty(e.RunLog);
        Assert.Equal(-4.5, e.ExpectedTime(0), 6); // scheduler cache restarted
    }

    [Fact]
    public void AutoCastStepPoppedWhenItLandsDoesNotPauseOrDrift()
    {
        // Rainbow Drip: expected at -4.5, 4 s cast, so it lands at -0.5; due (with 0.1 s allowance) at -0.4.
        var (e, ev) = Make(AdvanceMode.Auto, latency: 0.1);
        e.OnCountdown(5);
        for (var i = 0; i < 45; i++) e.Tick(0.1);
        Assert.False(e.IsPaused);
        Use(e, RD);
        Assert.Equal(-0.5, e.Clock!.Value, 6);
        Assert.DoesNotContain(ev, x => x.Kind == EngineEventKind.Paused);
        e.Tick(0.3);
        Assert.Equal(-0.2, e.Clock!.Value, 6); // still the real time since the countdown started
    }

    [Fact]
    public void AutoLateCastStepPausesAtItsDueTimeAndResumesFromThere()
    {
        var (e, ev) = Make(AdvanceMode.Auto, latency: 0.1);
        e.OnCountdown(5);
        e.Tick(4.4);
        Assert.False(e.IsPaused);
        e.Tick(2);
        Assert.True(e.IsPaused);
        Assert.Equal(-0.4, e.Clock!.Value, 6);
        Assert.Equal(new EngineEvent(EngineEventKind.Paused, 0), Assert.Single(ev));
        Use(e, RD);
        Assert.False(e.IsPaused);
        Assert.Equal(-0.4, e.Clock!.Value, 6);
        e.Tick(0.2);
        Assert.Equal(-0.2, e.Clock!.Value, 6);
    }

    [Fact]
    public void AutoPopNeverMovesTheClockBack()
    {
        var (e, _) = Make(AdvanceMode.Auto, latency: 0.1);
        e.OnCountdown(5);
        e.Tick(4.45);
        Use(e, RD);
        Assert.Equal(-0.55, e.Clock!.Value, 6);
    }

    [Fact]
    public void VariantIdPopsTheStep()
    {
        const uint variant = 99;
        static uint Canonical(uint id) => id == variant ? RD : id;
        var (e, ev) = Make(sameAction: (a, b) => Canonical(a) == Canonical(b));
        Use(e, variant);
        Assert.Equal(new EngineEvent(EngineEventKind.Popped, 0), Assert.Single(ev));
        Assert.Equal(RD, e.RunLog[0].AbilityId);
    }

    [Fact]
    public void WithoutVariantsADifferentIdIsWrong()
    {
        var (e, ev) = Make();
        Use(e, 99);
        Assert.Equal(new EngineEvent(EngineEventKind.WrongPress, 0), Assert.Single(ev));
    }

    private static void PlayOpener(PracticeEngine e)
    {
        Use(e, RD);
        e.OnActionUsed(StepType.Item, 49235);
        foreach (var id in new[] { Pom, Striking, Wing, Starry, Hammer }) Use(e, id);
    }

    [Fact]
    public void FinishedWhenNoLoop()
    {
        var (e, ev) = Make();
        PlayOpener(e);
        Assert.True(e.IsFinished);
        Assert.Equal(1, ev.Count(x => x.Kind == EngineEventKind.Finished));
        Assert.Equal(new EngineEvent(EngineEventKind.Finished, 6), ev[^1]);
        var count = ev.Count;
        Use(e, RD);
        Use(e, Swiftcast);
        e.Tick(1);
        Assert.Equal(count, ev.Count);
    }

    [Fact]
    public void RunLogRecordsExpectedAndActual()
    {
        var (e, _) = Make();
        e.OnCountdown(5);
        e.Tick(1);
        Use(e, RD);
        Assert.Equal(e.ExpectedTime(0), e.RunLog[0].Expected, 6);
        Assert.Equal(-4.0, e.RunLog[0].Actual, 6);
        Assert.Equal(RD, e.RunLog[0].AbilityId);
        e.Tick(4.65);
        e.OnActionUsed(StepType.Item, 49235);
        Assert.Equal(0.0, e.RunLog[1].Expected, 6);
        Assert.Equal(0.65, e.RunLog[1].Actual, 6);
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var (e, _) = Make(AdvanceMode.Auto);
        e.OnCountdown(5);
        e.Tick(5);
        Assert.True(e.IsPaused);
        Use(e, Swiftcast);
        e.Reset();
        Assert.Equal(0, e.CurrentIndex);
        Assert.Null(e.Clock);
        Assert.False(e.IsPaused);
        Assert.False(e.IsFinished);
        Assert.Empty(e.RunLog);
        Use(e, RD);
        Assert.Equal(0, e.RunLog[0].WrongPresses);
    }
}
