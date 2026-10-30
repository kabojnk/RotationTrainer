using RotationTrainer.Core;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class TrailLayoutTests
{
    private static TrailInput G(int i, double t = 0) => new(i, true, t);
    private static TrailInput W(int i, double t = 0) => new(i, false, t);

    [Fact]
    public void RailPutsCurrentAtMarker()
    {
        var items = TrailLayout.Layout(TrailStyle.Rail, TrailSize.Big, new[] { G(0), G(1) }, 0, 110f);
        Assert.Equal(110f, items[0].CenterX);
        Assert.True(items[0].IsCurrent);
        Assert.False(items[1].IsCurrent);
    }

    [Fact]
    public void RailSpacing()
    {
        var items = TrailLayout.Layout(TrailStyle.Rail, TrailSize.Big, new[] { G(0), W(1), W(2), G(3) }, 0, 110f);
        Assert.Equal(new[] { 110f, 190f, 238f, 298f }, items.Select(i => i.CenterX).ToArray());
    }

    [Fact]
    public void CompactRailUsesGap22AndScalesCurrent()
    {
        var items = TrailLayout.Layout(TrailStyle.CompactRail, TrailSize.Big, new[] { G(0), G(1) }, 0, 110f);
        Assert.Equal(110f, items[0].CenterX);
        Assert.Equal(188f, items[1].CenterX);
        Assert.Equal(1.3f, items[0].Scale);
        Assert.Equal(1f, items[1].Scale);
    }

    [Fact]
    public void TimelineUsesPixelsPerSecond()
    {
        var items = TrailLayout.Layout(TrailStyle.Timeline, TrailSize.Big, new[] { G(0, 0), G(1, 2.5) }, 0, 110f);
        Assert.Equal(110f, items[0].CenterX);
        Assert.Equal(290f, items[1].CenterX);
    }

    [Fact]
    public void WeavesUseWeaveRowAndSize()
    {
        var big = TrailLayout.Layout(TrailStyle.Rail, TrailSize.Big, new[] { G(0), W(1) }, 0, 0f);
        Assert.Equal(TrailRow.Gcd, big[0].Row);
        Assert.Equal(56f, big[0].Size);
        Assert.Equal(TrailRow.Weave, big[1].Row);
        Assert.Equal(40f, big[1].Size);
        var small = TrailLayout.Layout(TrailStyle.Rail, TrailSize.Small, new[] { G(0), W(1) }, 0, 0f);
        Assert.Equal(28f, small[1].Size);
    }

    [Fact]
    public void SmallMetrics()
    {
        Assert.Equal(new TrailMetrics(38f, 28f, 48f), TrailMetrics.For(TrailSize.Small));
    }

    [Fact]
    public void EmptyReturnsEmpty()
    {
        Assert.Empty(TrailLayout.Layout(TrailStyle.Rail, TrailSize.Big, new TrailInput[0], 0, 0f));
    }

    [Fact]
    public void FlowPutsAStepOnTheMarkerAtItsExpectedTime()
    {
        var items = TrailLayout.Flow(TrailStyle.Rail, TrailSize.Big, new[] { G(0, 0), G(1, 2.5), G(2, 5) }, 1, 2.5, 110f);
        Assert.Equal(new[] { 1, 2 }, items.Select(i => i.Index).ToArray());
        Assert.Equal(new[] { 110f, 198f }, items.Select(i => i.CenterX).ToArray());
        Assert.True(items[0].IsCurrent);
    }

    [Fact]
    public void FlowScrollsEvenlyBetweenSteps()
    {
        var items = TrailLayout.Flow(TrailStyle.Rail, TrailSize.Big, new[] { G(0, 0), G(1, 2.5), G(2, 5) }, 1, 1.25, 110f);
        Assert.Equal(154f, items[0].CenterX);
    }

    [Fact]
    public void FlowHoldsTheFirstStepOnTheMarkerBeforeItsTime()
    {
        var items = TrailLayout.Flow(TrailStyle.Rail, TrailSize.Big, new[] { G(0, -4.5), G(1, 0) }, 0, -10, 110f);
        Assert.Equal(110f, items[0].CenterX);
    }

    [Fact]
    public void FlowPassesTheMarkerWhileTheCurrentStepIsLate()
    {
        var items = TrailLayout.Flow(TrailStyle.Rail, TrailSize.Big, new[] { G(0, 0), G(1, 2.5) }, 0, 1.25, 110f);
        Assert.Equal(66f, items[0].CenterX);
        Assert.True(items[0].IsCurrent);
    }

    [Fact]
    public void FlowScalesTheCurrentStepInCompactRail()
    {
        var items = TrailLayout.Flow(TrailStyle.CompactRail, TrailSize.Big, new[] { G(0, 0), G(1, 2.5), G(2, 5) }, 1, 0, 110f);
        Assert.Equal(1.3f, items[0].Scale);
        Assert.Equal(1f, items[1].Scale);
    }
}
