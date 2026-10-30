using RotationTrainer.Core;

namespace RotationTrainer.GameData.Tests;

[Collection("GameData")]
public class LuminaCatalogTests(GameDataFixture data)
{
    private IAbilityCatalog Catalog => data.Catalog;

    [SqpackFact]
    public void PctKitHasRainbowDripAsGcd()
    {
        var drip = Catalog.FindActionByName("PCT", "Rainbow Drip");
        Assert.NotNull(drip);
        Assert.True(drip.IsGcd);
        Assert.Equal(AbilityCategory.Spell, drip.Category);
        Assert.Equal(PaletteGroup.Gcd, drip.Group);
        Assert.Equal(4000, drip.BaseCastMs);
        Assert.Equal(6000, drip.BaseRecastMs);
        Assert.Same(drip, Catalog.FindAction("PCT", drip.Id));
    }

    [SqpackFact]
    public void ChargedGcdIsGcd()
    {
        var drill = Catalog.FindActionByName("MCH", "Drill");
        Assert.NotNull(drill);
        Assert.True(drill.IsGcd);
        Assert.Equal(PaletteGroup.Gcd, drill.Group);
    }

    [SqpackFact]
    public void PctMusesAreWeaves()
    {
        var pom = Catalog.FindActionByName("PCT", "pom   MUSE");
        Assert.NotNull(pom);
        Assert.False(pom.IsGcd);
        Assert.Equal(AbilityCategory.Ability, pom.Category);
        Assert.Equal(PaletteGroup.Weave, pom.Group);
    }

    [SqpackFact]
    public void RoleActionsIncluded()
    {
        var swift = Catalog.FindActionByName("PCT", "Swiftcast");
        Assert.NotNull(swift);
        Assert.Equal(PaletteGroup.Role, swift.Group);
        Assert.Null(Catalog.FindActionByName("PCT", "Second Wind"));
    }

    [SqpackFact]
    public void OtherJobsActionsExcluded()
    {
        Assert.Null(Catalog.FindActionByName("PCT", "Fire IV"));
        var fire4 = Catalog.FindActionByName("BLM", "Fire IV");
        Assert.NotNull(fire4);
        Assert.Null(Catalog.FindAction("PCT", fire4.Id));
    }

    [SqpackFact]
    public void MedicineFoundByNameAndNormalizedId()
    {
        var byName = Catalog.FindItemByName("Grade 4 Gemdraught of Intelligence");
        Assert.NotNull(byName);
        Assert.Equal(StepType.Item, byName.Type);
        Assert.Equal(AbilityCategory.Item, byName.Category);
        Assert.Equal(PaletteGroup.Medicine, byName.Group);

        var hqId = 1_000_000 + byName.Id;
        Assert.Same(byName, Catalog.FindItem(ItemIds.Normalize(hqId)));
        Assert.Null(Catalog.FindItemByName("Fire IV"));
    }

    [SqpackFact]
    public void PaletteGroupsPopulated()
    {
        var palette = Catalog.Palette("PCT");
        foreach (var group in Enum.GetValues<PaletteGroup>())
            Assert.Contains(palette, a => a.Group == group);

        var medicine = palette.Where(a => a.Group == PaletteGroup.Medicine).ToList();
        Assert.All(medicine, m => Assert.Contains("Intelligence", m.Name));
        Assert.StartsWith("Grade 4 Gemdraught", medicine[0].Name);
        Assert.Equal(palette.Count, palette.Select(a => a.Name).Distinct().Count());
    }

    [SqpackFact]
    public void AllCombatJobsKnown()
    {
        Assert.NotEmpty(data.CombatJobs);
        Assert.Contains("PCT", data.CombatJobs);
        Assert.All(data.CombatJobs, job => Assert.True(Catalog.IsKnownJob(job), job));
        Assert.False(Catalog.IsKnownJob("CRP"));
        Assert.False(Catalog.IsKnownJob("THM"));
    }
}
