using RotationTrainer.Core;

namespace RotationTrainer.GameData.Tests;

[Collection("GameData")]
public class ExampleFileTests(GameDataFixture data)
{
    [SqpackFact]
    public void ExampleOpenerImportsCleanly()
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "examples", "pct-2nd-gcd-starry.json"));

        var result = RotationJson.Import(json, data.Catalog, () => Guid.NewGuid().ToString());

        Assert.Empty(result.Errors);
        var opener = Assert.Single(result.Rotations);
        Assert.Equal("PCT", opener.Job);
        Assert.Equal(RotationKind.Opener, opener.Kind);
        Assert.Equal(22, opener.Steps.Count);
        Assert.Equal(json, RotationJson.Export(result.Rotations) + "\n");
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "RotationTrainer.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("RotationTrainer.sln not found above the test output directory.");
    }
}
