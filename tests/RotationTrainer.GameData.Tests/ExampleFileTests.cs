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

    [SqpackFact]
    public void BuilderRebuildsExampleOpener()
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "examples", "pct-2nd-gcd-starry.json"));
        var example = Assert.Single(RotationJson.Import(json, data.Catalog, () => "unused").Rotations);
        var catalog = data.Catalog;
        Step Action(string name)
        {
            var a = catalog.FindActionByName("PCT", name);
            Assert.NotNull(a);
            return new Step(a.Type, a.Id, a.Name);
        }

        // As in the builder: a new opener holds only the Pull; palette clicks append after the selection.
        var doc = new BuilderDocument("PCT", RotationKind.Opener, "2nd GCD Starry Opener", catalog);
        doc.Insert(0, Action("Rainbow Drip"));
        doc.SetAt(0, -4.5);
        var potion = catalog.FindItemByName("Grade 4 Gemdraught of Intelligence");
        Assert.NotNull(potion);
        doc.Insert(doc.Steps.Count, new Step(potion.Type, potion.Id, potion.Name));
        foreach (var name in new[]
                 {
                     "Pom Muse", "Striking Muse", "Wing Motif", "Starry Muse", "Hammer Stamp", "Subtractive Palette",
                     "Blizzard in Cyan", "Stone in Yellow", "Thunder in Magenta", "Comet in Black", "Winged Muse",
                     "Mog of the Ages", "Star Prism", "Hammer Brush", "Polishing Hammer", "Rainbow Drip", "Fire in Red",
                     "Swiftcast", "Aero in Green",
                 })
            doc.Insert(doc.Steps.Count, Action(name));

        Assert.Empty(doc.Warnings());
        var built = doc.ToRotation(() => example.Id) with { Source = example.Source, Patch = example.Patch };
        Assert.Equal(example.Steps.Select(s => (s.Type, s.Id, s.At)), built.Steps.Select(s => (s.Type, s.Id, s.At)));
        Assert.Equal(json, RotationJson.Export(new[] { built }) + "\n");
    }

    [SqpackFact]
    public void BuiltInRotationsImportCleanly()
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "presets", "balance-openers.json"));

        var result = RotationJson.Import(json, data.Catalog, () => Guid.NewGuid().ToString());

        Assert.Empty(result.Errors);
        Assert.Equal(34, result.Rotations.Count);
        Assert.Equal(result.Rotations.Count, result.Rotations.Select(r => r.Id).Distinct().Count());
        Assert.All(result.Rotations, r => Assert.StartsWith("The Balance", r.Source));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "RotationTrainer.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("RotationTrainer.sln not found above the test output directory.");
    }
}
