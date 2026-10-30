using RotationTrainer.Core;
using RotationTrainer.Core.Tests.Fakes;

namespace RotationTrainer.Core.Tests;

public class RotationJsonImportTests
{
    private static readonly FakeCatalog Catalog = new();

    private static ImportResult Import(string json, Func<string>? newId = null) =>
        RotationJson.Import(json, Catalog, newId ?? (() => "gen-1"));

    private const string PctOpener =
        """{"id":"pct-op","job":"PCT","level":100,"kind":"opener","name":"PCT Opener","steps":[{"action":1,"at":-4.5},{"pull":true},{"action":2}]}""";
    private const string BlmOpener =
        """{"id":"blm-op","job":"blm","kind":"Opener","name":"BLM Opener","steps":[{"action":30}]}""";

    private static string PctWith(string steps, string kind = "opener", string level = "100") =>
        $$"""{"job":"PCT","level":{{level}},"kind":"{{kind}}","name":"X","steps":[{{steps}}]}""";

    [Fact]
    public void ImportsWrappedBatch()
    {
        var r = Import($$"""{"format":"rotation-trainer/1","rotations":[{{PctOpener}},{{BlmOpener}}]}""");
        Assert.Equal(2, r.Rotations.Count);
        Assert.Empty(r.Errors);
        Assert.Equal("BLM", r.Rotations[1].Job);
        Assert.Equal(RotationKind.Opener, r.Rotations[1].Kind);
    }

    [Fact]
    public void ImportsSingleRotationObject()
    {
        var r = Import(PctOpener);
        Assert.Single(r.Rotations);
        Assert.Empty(r.Errors);
        Assert.Equal(3, r.Rotations[0].Steps.Count);
        Assert.Equal(StepType.Pull, r.Rotations[0].Steps[1].Type);
    }

    [Fact]
    public void ImportsBareArray()
    {
        var r = Import($"[{PctOpener}]");
        Assert.Single(r.Rotations);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void GeneratesIdWhenMissing()
    {
        var r = Import("""{"job":"PCT","kind":"loop","name":"L","steps":[{"action":1}]}""");
        Assert.Equal("gen-1", r.Rotations.Single().Id);
        Assert.Equal(100, r.Rotations.Single().Level);
        Assert.Equal("pct-op", Import(PctOpener).Rotations.Single().Id);
    }

    [Fact]
    public void ResolvesStepByNameWhenIdMissing()
    {
        var r = Import(PctWith("""{"name":"rainbow  DRIP","at":-4.5}"""));
        Assert.Empty(r.Errors);
        Assert.Equal(new Step(StepType.Action, 1, "Rainbow Drip", -4.5), r.Rotations.Single().Steps[0]);
    }

    [Fact]
    public void NormalizesHqItemId()
    {
        var r = Import(PctWith("""{"item":1049235}"""));
        Assert.Empty(r.Errors);
        Assert.Equal(new Step(StepType.Item, 49235, "Gemdraught of Intelligence"), r.Rotations.Single().Steps[0]);
    }

    [Fact]
    public void SkipsRotationWithUnknownActionAndNamesStep()
    {
        var r = Import(PctWith("""{"action":1},{"action":30}"""));
        Assert.Empty(r.Rotations);
        var e = Assert.Single(r.Errors);
        Assert.Equal(2, e.Step);
        Assert.Contains("not a PCT action", e.Message);
    }

    [Fact]
    public void KeepsOtherRotationsWhenOneFails()
    {
        var bad = PctWith("""{"action":30}""");
        var r = Import($"[{PctOpener},{bad}]");
        Assert.Single(r.Rotations);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void RejectsAtAfterPull()
    {
        var r = Import(PctWith("""{"pull":true},{"action":1,"at":-2}"""));
        Assert.Empty(r.Rotations);
        Assert.Contains("only allowed before the pull", r.Errors.Single().Message);
    }

    [Fact]
    public void RejectsNonNegativeAtBeforePull()
    {
        var r = Import(PctWith("""{"action":1,"at":1.0},{"pull":true}"""));
        Assert.Empty(r.Rotations);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void RejectsSecondPullAndPullInLoop()
    {
        var two = Import(PctWith("""{"pull":true},{"pull":true}"""));
        Assert.Empty(two.Rotations);
        Assert.Single(two.Errors);

        var loop = Import(PctWith("""{"action":1},{"pull":true}""", kind: "loop"));
        Assert.Empty(loop.Rotations);
        Assert.Single(loop.Errors);
    }

    [Fact]
    public void RejectsLevelOtherThan100()
    {
        var r = Import(PctWith("""{"action":1}""", level: "90"));
        Assert.Empty(r.Rotations);
        Assert.Contains("level 100", r.Errors.Single().Message);
    }

    [Fact]
    public void RejectsUnknownJobAndKind()
    {
        var job = Import("""{"job":"XYZ","kind":"opener","name":"X","steps":[{"action":1}]}""");
        Assert.Empty(job.Rotations);
        Assert.Single(job.Errors);

        var kind = Import(PctWith("""{"action":1}""", kind: "burst"));
        Assert.Empty(kind.Rotations);
        Assert.Single(kind.Errors);
    }

    [Fact]
    public void RejectsUnknownFormat()
    {
        var r = Import("""{"format":"other/2","rotations":[]}""");
        Assert.Empty(r.Rotations);
        Assert.Equal("(file)", r.Errors.Single().Rotation);
    }

    [Fact]
    public void MalformedJsonReturnsFileError()
    {
        var r = Import("{");
        Assert.Empty(r.Rotations);
        Assert.Equal("(file)", r.Errors.Single().Rotation);
    }
}
