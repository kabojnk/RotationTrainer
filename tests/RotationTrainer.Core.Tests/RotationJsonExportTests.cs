using System.Text.Json;
using RotationTrainer.Core;
using RotationTrainer.Core.Tests.Fakes;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class RotationJsonExportTests
{
    private static Rotation Sample(RotationKind kind = RotationKind.Opener, string? source = "src", string? patch = "7.2") =>
        new("r1", "PCT", 100, kind, "2nd GCD Starry Opener", new List<Step>
        {
            new(StepType.Action, 1, "Rainbow Drip", -4.5),
            new(StepType.Item, 49235, "Gemdraught of Intelligence", -2),
            new(StepType.Pull, 0, "Pull"),
            new(StepType.Action, 4, "Fire in Red"),
        }, source, patch);

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ExportWritesWrapperAndFormat()
    {
        var root = Parse(RotationJson.Export(new[] { Sample() }));
        Assert.Equal("rotation-trainer/1", root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("rotations").GetArrayLength());
    }

    [Fact]
    public void ExportWritesIdAndNameForEachStep()
    {
        var steps = Parse(RotationJson.Export(new[] { Sample() })).GetProperty("rotations")[0].GetProperty("steps");
        Assert.Equal(1, steps[0].GetProperty("action").GetInt32());
        Assert.Equal("Rainbow Drip", steps[0].GetProperty("name").GetString());
        Assert.Equal(-4.5, steps[0].GetProperty("at").GetDouble());
        Assert.Equal(49235, steps[1].GetProperty("item").GetInt32());
        Assert.Equal("Gemdraught of Intelligence", steps[1].GetProperty("name").GetString());
        Assert.True(steps[2].GetProperty("pull").GetBoolean());
        Assert.False(steps[2].TryGetProperty("name", out _));
        Assert.False(steps[3].TryGetProperty("at", out _));
    }

    [Fact]
    public void ExportOmitsNullSourceAndPatch()
    {
        var rot = Parse(RotationJson.Export(new[] { Sample(source: null, patch: null) })).GetProperty("rotations")[0];
        Assert.False(rot.TryGetProperty("source", out _));
        Assert.False(rot.TryGetProperty("patch", out _));
    }

    [Fact]
    public void ExportWritesKindLowercase()
    {
        var root = Parse(RotationJson.Export(new[] { Sample(), Sample(RotationKind.Loop) }));
        Assert.Equal("opener", root.GetProperty("rotations")[0].GetProperty("kind").GetString());
        Assert.Equal("loop", root.GetProperty("rotations")[1].GetProperty("kind").GetString());
    }

    [Fact]
    public void RoundTripPreservesRotation()
    {
        var r = Sample();
        var result = RotationJson.Import(RotationJson.Export(new[] { r }), new FakeCatalog(), () => "x");
        Assert.Empty(result.Errors);
        var back = result.Rotations.Single();
        Assert.Equal(r.Id, back.Id);
        Assert.Equal(r.Job, back.Job);
        Assert.Equal(r.Level, back.Level);
        Assert.Equal(r.Kind, back.Kind);
        Assert.Equal(r.Name, back.Name);
        Assert.Equal(r.Source, back.Source);
        Assert.Equal(r.Patch, back.Patch);
        Assert.True(r.Steps.SequenceEqual(back.Steps));
    }
}
