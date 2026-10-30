using RotationTrainer.Core;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class RotationLibraryTests
{
    private static Rotation R(string id, string name = "2nd GCD Starry Opener", string job = "PCT",
        RotationKind kind = RotationKind.Opener) =>
        new(id, job, 100, kind, name, new List<Step> { new(StepType.Action, 1, "Rainbow Drip") });

    [Fact]
    public void ConflictsReturnsIncomingWithExistingIds()
    {
        var lib = new RotationLibrary(new[] { R("a"), R("b") });
        var c = lib.Conflicts(new[] { R("b"), R("c") });
        Assert.Equal(new[] { "b" }, c.Select(x => x.Id));
    }

    [Fact]
    public void ReplaceOverwritesInPlace()
    {
        var lib = new RotationLibrary(new[] { R("a"), R("b") });
        lib.Add(new[] { R("a", "New name") }, _ => ConflictChoice.Replace, () => "gen-1");
        Assert.Equal(2, lib.All.Count);
        Assert.Equal("New name", lib.Get("a")!.Name);
        Assert.Equal("a", lib.All[0].Id);
    }

    [Fact]
    public void KeepBothAssignsNewIdAndCopySuffix()
    {
        var lib = new RotationLibrary(new[] { R("a") });
        lib.Add(new[] { R("a") }, _ => ConflictChoice.KeepBoth, () => "gen-1");
        Assert.Equal(2, lib.All.Count);
        var copy = lib.Get("gen-1")!;
        Assert.Equal("2nd GCD Starry Opener (copy)", copy.Name);
        Assert.Equal("2nd GCD Starry Opener", lib.Get("a")!.Name);
    }

    [Fact]
    public void AddAppendsNonConflictingWithoutAsking()
    {
        var lib = new RotationLibrary(new[] { R("a") });
        lib.Add(new[] { R("b") }, _ => throw new InvalidOperationException(), () => "gen-1");
        Assert.Equal(new[] { "a", "b" }, lib.All.Select(x => x.Id));
    }

    [Fact]
    public void SaveUpsertsById()
    {
        var lib = new RotationLibrary(new[] { R("a") });
        var changes = 0;
        lib.Changed += () => changes++;
        lib.Save(R("a", "Renamed"));
        lib.Save(R("b"));
        Assert.Equal(2, lib.All.Count);
        Assert.Equal("Renamed", lib.Get("a")!.Name);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void DeleteRemovesAndRaisesChanged()
    {
        var lib = new RotationLibrary(new[] { R("a"), R("b") });
        var changes = 0;
        lib.Changed += () => changes++;
        Assert.True(lib.Delete("a"));
        Assert.Null(lib.Get("a"));
        Assert.Equal(1, changes);
        Assert.False(lib.Delete("zzz"));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ForJobFiltersByJobAndKind()
    {
        var lib = new RotationLibrary(new[]
        {
            R("a"), R("b", kind: RotationKind.Loop), R("c", job: "BLM"),
        });
        Assert.Equal(new[] { "a", "b" }, lib.ForJob("PCT").Select(x => x.Id));
        Assert.Equal(new[] { "b" }, lib.ForJob("PCT", RotationKind.Loop).Select(x => x.Id));
    }
}
