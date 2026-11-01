namespace RotationTrainer.GameData.Tests;

[Collection("GameData")]
public class LevellingDataTests(GameDataFixture fixture)
{
    private LuminaCatalog Catalog => fixture.Catalog;

    [SqpackFact]
    public void AbilitiesCarryUnlockLevel()
    {
        Assert.Equal(1, Catalog.FindActionByName("PLD", "Fast Blade")!.Level);
        Assert.Equal(26, Catalog.FindActionByName("PLD", "Rage of Halone")!.Level);
        Assert.Equal(60, Catalog.FindActionByName("PLD", "Royal Authority")!.Level);
    }

    [SqpackFact]
    public void StartLevelsMatchSheet()
    {
        Assert.Equal(1, Catalog.StartLevel("PLD"));
        Assert.Equal(30, Catalog.StartLevel("DRK"));
        Assert.Equal(50, Catalog.StartLevel("SAM"));
        Assert.Equal(60, Catalog.StartLevel("GNB"));
        Assert.Equal(70, Catalog.StartLevel("RPR"));
        Assert.Equal(80, Catalog.StartLevel("PCT"));
    }

    [SqpackFact]
    public void ClassesMapToJobs()
    {
        Assert.Equal(new[] { "PLD" }, Catalog.JobsForClass("GLA"));
        Assert.Equal(new[] { "SMN", "SCH" }, Catalog.JobsForClass("ACN"));
        Assert.Equal(new[] { "PLD" }, Catalog.JobsForClass("PLD"));
        Assert.Empty(Catalog.JobsForClass("CRP"));
    }

    [SqpackFact]
    public void UnlocksIncludeActionsAndTraits()
    {
        var unlocks = Catalog.Unlocks("PLD", 60, 69);
        Assert.Contains(unlocks, u => u is { Name: "Royal Authority", Level: 60, IsTrait: false });
        Assert.Contains(unlocks, u => u is { Name: "Divine Magic Mastery", Level: 64, IsTrait: true });
        Assert.Contains(unlocks, u => u is { Name: "Requiescat", Level: 68, IsTrait: false });
        Assert.All(unlocks, u => Assert.InRange(u.Level, 60, 69));
        Assert.Equal(unlocks.OrderBy(u => u.Level).ThenBy(u => u.Name).ToList(), unlocks);
    }

    [SqpackFact]
    public void ClassTraitsCountForTheJob()
    {
        // Gladiator learns these before Paladin exists.
        Assert.Contains(Catalog.Unlocks("PLD", 1, 29), u => u.IsTrait);
        Assert.DoesNotContain(Catalog.Unlocks("PLD", 1, 100), u => u.Name == "Swiftcast");
    }
}
