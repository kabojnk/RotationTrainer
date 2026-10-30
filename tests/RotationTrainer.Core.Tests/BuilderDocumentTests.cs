using RotationTrainer.Core.Tests.Fakes;

namespace RotationTrainer.Core.Tests;

public class BuilderDocumentTests
{
    private static readonly FakeCatalog Cat = new();
    private static readonly Step Pull = new(StepType.Pull, 0, "Pull");
    private static Step A(uint id, string name) => new(StepType.Action, id, name);
    private static readonly Step Rainbow = A(1, "Rainbow Drip");
    private static readonly Step Wing = A(2, "Wing Motif");
    private static readonly Step Hammer = A(3, "Hammer Stamp");
    private static readonly Step Fire = A(4, "Fire in Red");
    private static readonly Step Pom = A(10, "Pom Muse");
    private static readonly Step Striking = A(11, "Striking Muse");
    private static readonly Step Starry = A(12, "Starry Muse");
    private static readonly Step Potion = new(StepType.Item, 49235, "Gemdraught of Intelligence");

    private static BuilderDocument Opener() => new("PCT", RotationKind.Opener, "Test", Cat);

    [Fact]
    public void NewOpenerStartsWithPull()
    {
        var d = Opener();
        Assert.Equal(new[] { Pull }, d.Steps);
        Assert.Equal(0, d.PullIndex);
        Assert.Null(d.Id);
    }

    [Fact]
    public void NewLoopStartsEmptyAndRejectsPull()
    {
        var d = new BuilderDocument("PCT", RotationKind.Loop, "Loop", Cat);
        Assert.Empty(d.Steps);
        Assert.Equal(-1, d.PullIndex);
        d.Insert(0, Pull);
        Assert.Empty(d.Steps);
    }

    [Fact]
    public void SecondPullIsIgnored()
    {
        var d = Opener();
        d.Insert(1, Pull);
        Assert.Single(d.Steps);
    }

    [Fact]
    public void InsertAndMove()
    {
        var d = new BuilderDocument("PCT", RotationKind.Loop, "L", Cat);
        d.Insert(0, Rainbow); d.Insert(1, Wing); d.Insert(2, Hammer); d.Insert(3, Fire);
        d.Move(3, 1);
        Assert.Equal(new[] { Rainbow, Fire, Wing, Hammer }, d.Steps);
        d.Move(1, 1);
        d.Move(1, 2);
        Assert.Equal(new[] { Rainbow, Fire, Wing, Hammer }, d.Steps);

        var e = new BuilderDocument("PCT", RotationKind.Loop, "L", Cat);
        e.Insert(0, Rainbow); e.Insert(1, Wing); e.Insert(2, Hammer); e.Insert(3, Fire);
        e.Move(0, 3);
        Assert.Equal(new[] { Wing, Hammer, Rainbow, Fire }, e.Steps);
    }

    [Fact]
    public void ThreeWeavesAfterGcdWarns()
    {
        var d = Opener();
        d.Insert(1, Wing); d.Insert(2, Pom); d.Insert(3, Striking); d.Insert(4, Starry);
        var w = Assert.Single(d.Warnings());
        Assert.Equal("3 weaves after Wing Motif may clip your GCD", w.Message);
        Assert.Equal(2, w.StepIndex);
        Assert.Equal(new[] { 2, 3, 4 }, d.FlaggedSteps().OrderBy(i => i));
    }

    [Fact]
    public void TwoWeavesDoNotWarn()
    {
        var d = Opener();
        d.Insert(1, Wing); d.Insert(2, Pom); d.Insert(3, Striking);
        Assert.Empty(d.Warnings());
        Assert.Empty(d.FlaggedSteps());
    }

    [Fact]
    public void WeavesRightAfterPullDoNotWarn()
    {
        var d = Opener();
        d.Insert(1, Potion); d.Insert(2, Pom); d.Insert(3, Striking);
        Assert.Empty(d.Warnings());
    }

    [Fact]
    public void OpenerWithoutPullWarns()
    {
        var d = Opener();
        d.Remove(d.PullIndex);
        var w = Assert.Single(d.Warnings());
        Assert.Equal("No Pull marker. Openers need one to line up the countdown.", w.Message);
        Assert.Null(w.StepIndex);
    }

    [Fact]
    public void KindSwitchAddsAndRemovesPull()
    {
        var d = Opener();
        d.Kind = RotationKind.Loop;
        Assert.Empty(d.Steps);
        d.Kind = RotationKind.Opener;
        Assert.Equal(new[] { Pull }, d.Steps);
    }

    [Fact]
    public void ToRotationKeepsIdWhenEditing()
    {
        var src = new Rotation("abc", "PCT", 100, RotationKind.Opener, "Mine",
            new[] { Pull, Rainbow }, "src", "7.5");
        var d = BuilderDocument.Edit(src, Cat);
        Assert.Equal("abc", d.Id);
        var r = d.ToRotation(() => "new");
        Assert.Equal("abc", r.Id);
        Assert.Equal("src", r.Source);
        Assert.Equal("7.5", r.Patch);
        Assert.Equal(100, r.Level);
        Assert.Equal("new", Opener().ToRotation(() => "new").Id);
    }

    [Fact]
    public void ToRotationDropsAtAfterPull()
    {
        var d = Opener();
        d.Insert(0, Potion);   // prepull
        d.Insert(2, Rainbow);  // after pull
        Assert.True(d.IsPrepull(0));
        Assert.False(d.IsPrepull(2));
        d.SetAt(0, -5.0);
        d.SetAt(2, 1.0);
        Assert.Null(d.Steps[2].At);
        var r = d.ToRotation(() => "x");
        Assert.Equal(-5.0, r.Steps[0].At);
        Assert.All(r.Steps.Skip(1), s => Assert.Null(s.At));

        d.Move(0, 3); // potion now after pull; At must drop
        Assert.All(d.ToRotation(() => "x").Steps, s => Assert.Null(s.At));
    }

    [Fact]
    public void LoopOmitsPullAndAt()
    {
        var src = new Rotation("l", "PCT", 100, RotationKind.Opener, "n",
            new[] { Potion with { At = -3 }, Pull, Rainbow });
        var d = BuilderDocument.Edit(src, Cat);
        d.Kind = RotationKind.Loop;
        var r = d.ToRotation(() => "x");
        Assert.Equal(new[] { Potion, Rainbow }, r.Steps);
    }
}
