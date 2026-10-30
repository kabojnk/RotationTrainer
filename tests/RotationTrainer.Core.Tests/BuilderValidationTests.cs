using RotationTrainer.Core.Tests.Fakes;

namespace RotationTrainer.Core.Tests;

public class BuilderValidationTests
{
    private static readonly FakeCatalog Cat = new();
    private static readonly Step Pull = new(StepType.Pull, 0, "Pull");
    private static Step A(uint id, string name, double? at = null) => new(StepType.Action, id, name, at);
    private static readonly Step Rainbow = A(1, "Rainbow Drip");
    private static readonly Step Hammer = A(3, "Hammer Stamp");
    private static readonly Step Potion = new(StepType.Item, 49235, "Gemdraught of Intelligence");

    private static BuilderDocument Doc(RotationKind kind, string name, params Step[] steps)
    {
        var d = new BuilderDocument("PCT", kind, name, Cat);
        foreach (var s in steps) d.Insert(d.Steps.Count, s);
        return d;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNameIsInvalid(string name)
    {
        var d = Doc(RotationKind.Loop, name, Hammer);
        Assert.Contains(d.Validate(), m => m.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoopWithNoStepsIsInvalid() =>
        Assert.NotEmpty(Doc(RotationKind.Loop, "L").Validate());

    [Fact]
    public void OpenerWithOnlyPullIsInvalid() =>
        Assert.NotEmpty(Doc(RotationKind.Opener, "O").Validate());

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.0)]
    [InlineData(1.5)]
    public void PrepullAtNotNegativeIsInvalid(double at)
    {
        var d = BuilderDocument.Edit(new Rotation("r", "PCT", 100, RotationKind.Opener, "O",
            new[] { A(1, "Rainbow Drip", at), Pull, Hammer }), Cat);
        Assert.NotEmpty(d.Validate());
    }

    [Fact]
    public void ValidDocumentHasNoReasons()
    {
        var d = Doc(RotationKind.Opener, "O");
        d.Insert(0, Rainbow);
        d.SetAt(0, -4.5);
        d.Insert(d.Steps.Count, Hammer);
        Assert.Empty(d.Validate());
    }

    [Fact]
    public void SetAtZeroClearsTheCountdown()
    {
        var d = Doc(RotationKind.Opener, "O");
        d.Insert(0, Rainbow);
        d.SetAt(0, -4.5);
        d.SetAt(0, -0.0);
        Assert.Null(d.Steps[0].At);
        d.SetAt(0, 0.0);
        Assert.Null(d.Steps[0].At);
    }

    [Theory]
    [InlineData("", true, null)]
    [InlineData("  ", true, null)]
    [InlineData("0", true, null)]
    [InlineData("0.00", true, null)]
    [InlineData("-0", true, null)]
    [InlineData("4.5", true, -4.5)]
    [InlineData("-3", true, -3.0)]
    [InlineData("abc", false, null)]
    public void ParseCountdownTreatsEmptyAndZeroAsNoTime(string text, bool ok, double? expected)
    {
        Assert.Equal(ok, BuilderDocument.TryParseCountdown(text, out var at));
        Assert.Equal(expected, at);
    }

    public static IEnumerable<object[]> EdgeDocuments()
    {
        yield return new object[] { Doc(RotationKind.Loop, "", Hammer) };
        yield return new object[] { Doc(RotationKind.Loop, " Padded name ", Hammer) };
        yield return new object[] { Doc(RotationKind.Loop, "Empty") };
        yield return new object[] { Doc(RotationKind.Opener, "Only pull") };
        yield return new object[] { Doc(RotationKind.Opener, "No time", Hammer) };
        yield return new object[] { Doc(RotationKind.Opener, "Potion", Potion, Hammer) };

        var noPull = Doc(RotationKind.Opener, "No pull", Rainbow, Hammer);
        noPull.Remove(0);
        yield return new object[] { noPull };

        var timed = Doc(RotationKind.Opener, "Timed");
        timed.Insert(0, Rainbow);
        timed.SetAt(0, -4.5);
        timed.Insert(timed.Steps.Count, Hammer);
        yield return new object[] { timed };

        // A timed step moved after the pull loses its time.
        var moved = Doc(RotationKind.Opener, "Moved");
        moved.Insert(0, Rainbow);
        moved.SetAt(0, -4.5);
        moved.Insert(moved.Steps.Count, Hammer);
        moved.Move(0, moved.Steps.Count);
        yield return new object[] { moved };

        var zero = Doc(RotationKind.Opener, "Zero");
        zero.Insert(0, Rainbow);
        zero.SetAt(0, -0.0);
        zero.Insert(zero.Steps.Count, Hammer);
        yield return new object[] { zero };

        var positive = BuilderDocument.Edit(new Rotation("p", "PCT", 100, RotationKind.Opener, "Positive",
            new[] { A(1, "Rainbow Drip", 2), Pull, Hammer }), Cat);
        yield return new object[] { positive };

        var toLoop = Doc(RotationKind.Opener, "To loop");
        toLoop.Insert(0, Rainbow);
        toLoop.SetAt(0, -4.5);
        toLoop.Kind = RotationKind.Loop;
        yield return new object[] { toLoop };
    }

    [Theory]
    [MemberData(nameof(EdgeDocuments))]
    public void EveryValidDocumentSurvivesExportAndImport(BuilderDocument doc)
    {
        if (doc.Validate().Count > 0) return;
        var rotation = doc.ToRotation(() => "new-id");
        var result = RotationJson.Import(RotationJson.Export(new[] { rotation }), Cat, () => "other");
        Assert.Empty(result.Errors);
        var back = Assert.Single(result.Rotations);
        Assert.Equal(rotation.Steps, back.Steps);
        Assert.Equal(rotation.Name, back.Name);
    }

    [Fact]
    public void EdgeDocumentsIncludeSomeValidOnes() =>
        Assert.Contains(EdgeDocuments(), d => ((BuilderDocument)d[0]).Validate().Count == 0);
}
