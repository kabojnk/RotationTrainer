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

    [Fact]
    public void AddBuiltInsAddsEachOnlyOnce()
    {
        var lib = new RotationLibrary(new[] { R("mine") });
        var offered = new HashSet<string>();
        var changes = 0;
        lib.Changed += () => changes++;

        Assert.Equal(2, lib.AddBuiltIns(new[] { R("x"), R("y") }, offered));
        Assert.Equal(new[] { "mine", "x", "y" }, lib.All.Select(r => r.Id));
        Assert.Equal(new HashSet<string> { "x", "y" }, offered);

        // A deleted built-in stays deleted; a new one in a later version is added.
        lib.Delete("x");
        Assert.Equal(1, lib.AddBuiltIns(new[] { R("x"), R("y"), R("z") }, offered));
        Assert.Equal(new[] { "mine", "y", "z" }, lib.All.Select(r => r.Id));
        Assert.Equal(3, changes); // two adds and the delete

        Assert.Equal(0, lib.AddBuiltIns(new[] { R("x"), R("y"), R("z") }, offered));
        Assert.Equal(3, changes);
    }

    [Fact]
    public void AddBuiltInsKeepsAnEditedCopy()
    {
        var lib = new RotationLibrary(new[] { R("x", "My edit") });
        var offered = new HashSet<string>();
        Assert.Equal(0, lib.AddBuiltIns(new[] { R("x") }, offered));
        Assert.Equal("My edit", lib.Get("x")!.Name);
        Assert.Contains("x", offered);
    }

    [Fact]
    public void RestoreBuiltInsReAddsDeletedOnesWithoutTouchingEdits()
    {
        var lib = new RotationLibrary(new[] { R("x", "My edit") });
        Assert.Equal(1, lib.RestoreBuiltIns(new[] { R("x"), R("y") }));
        Assert.Equal("My edit", lib.Get("x")!.Name);
        Assert.NotNull(lib.Get("y"));
        Assert.Equal(0, lib.RestoreBuiltIns(new[] { R("x"), R("y") }));
    }
}
