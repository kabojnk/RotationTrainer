using RotationTrainer.Core;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace RotationTrainer.GameData.Tests;

/// <summary>GCD locks of charged GCDs, button variants and actions that never belong in a kit.</summary>
[Collection("GameData")]
public class KitRulesTests(GameDataFixture data)
{
    private IAbilityCatalog Catalog => data.Catalog;

    private Ability Find(string job, string name) =>
        Catalog.FindActionByName(job, name) ?? throw new Xunit.Sdk.XunitException($"{job} {name} not found");

    [SqpackFact]
    public void ChargedGcdUsesTheGcdRecast()
    {
        var drill = Find("MCH", "Drill");
        Assert.True(drill.IsGcd);
        Assert.Equal(2500, drill.BaseRecastMs);
        Assert.Equal(2500, Find("GNB", "Gnashing Fang").BaseRecastMs);
        Assert.Equal(2500, Find("MCH", "Air Anchor").BaseRecastMs);
    }

    [SqpackFact]
    public void SchedulerSpacesAChargedGcdLikeAnyGcd()
    {
        var drill = Find("MCH", "Drill");
        var shot = Find("MCH", "Heated Split Shot");
        var scheduler = new Scheduler(new StatTimingSource(new SpeedProfile(420, 420)), 0.65);
        var first = scheduler.Next(new PlannedStep(drill, null, false, 1, false));
        var second = scheduler.Next(new PlannedStep(shot, null, false, 2, false));
        Assert.Equal(2.5, second - first, 2);
    }

    [SqpackFact]
    public void DancerStepsHaveTheirOwnGcdLocks()
    {
        Assert.Equal(1500, Find("DNC", "Standard Step").BaseRecastMs);
        Assert.Equal(1500, Find("DNC", "Technical Step").BaseRecastMs);
        foreach (var step in new[] { "Emboite", "Entrechat", "Jete", "Pirouette" })
            Assert.Equal(1000, Find("DNC", step).BaseRecastMs);
        Assert.Equal(2500, Find("DNC", "Standard Finish").BaseRecastMs);
        Assert.Equal(2500, Find("DNC", "Technical Finish").BaseRecastMs);
    }

    [SqpackFact]
    public void MudrasLockTheGcdBriefly()
    {
        var ten = Find("NIN", "Ten");
        Assert.True(ten.IsGcd);
        Assert.Equal(500, ten.BaseRecastMs);
    }

    [SqpackFact]
    public void NinjaMudraVariantsShareACanonicalId()
    {
        var ten = Find("NIN", "Ten");
        Assert.NotNull(Catalog.FindAction("NIN", 18805));
        Assert.Equal(Catalog.CanonicalId("NIN", ten.Id), Catalog.CanonicalId("NIN", 18805));
        Assert.Equal(Catalog.CanonicalId("NIN", 2261), Catalog.CanonicalId("NIN", 18806));
        Assert.Equal(Catalog.CanonicalId("NIN", 2263), Catalog.CanonicalId("NIN", 18807));
        Assert.NotEqual(Catalog.CanonicalId("NIN", 2259), Catalog.CanonicalId("NIN", 2261));
    }

    [SqpackFact]
    public void DancerFinishVariantsShareACanonicalId()
    {
        var technical = Find("DNC", "Technical Finish");
        var quadruple = Find("DNC", "Quadruple Technical Finish");
        Assert.Equal(Catalog.CanonicalId("DNC", technical.Id), Catalog.CanonicalId("DNC", quadruple.Id));
        var standard = Find("DNC", "Standard Finish");
        var dbl = Find("DNC", "Double Standard Finish");
        Assert.Equal(Catalog.CanonicalId("DNC", standard.Id), Catalog.CanonicalId("DNC", dbl.Id));
        Assert.NotEqual(Catalog.CanonicalId("DNC", standard.Id), Catalog.CanonicalId("DNC", technical.Id));
    }

    [SqpackFact]
    public void UnknownIdIsItsOwnCanonicalId() =>
        Assert.Equal(999_999u, Catalog.CanonicalId("NIN", 999_999));

    [SqpackFact]
    public void GeneralActionsAreInNoKit()
    {
        // Sprint, Teleport, Return, auto-attack (Attack/Shot).
        var general = new uint[] { 3, 5, 6, 7, 8 };
        var limitBreaks = data.Excel.GetSheet<LuminaAction>()
            .Where(a => a.ActionCategory.RowId == 9)
            .Select(a => a.RowId)
            .ToList();
        Assert.NotEmpty(limitBreaks);
        foreach (var job in data.CombatJobs)
        {
            foreach (var id in general.Concat(limitBreaks))
                Assert.True(Catalog.FindAction(job, id) == null, $"{job} kit has action {id}");
            foreach (var name in new[] { "Sprint", "Teleport", "Return", "Attack", "Shot" })
                Assert.True(Catalog.FindActionByName(job, name) == null, $"{job} kit has {name}");
        }
    }

    [SqpackTheory]
    [InlineData("PCT", "Pom Muse", "Living Muse")]        // shared charges
    [InlineData("PCT", "Starry Muse", "Scenic Muse")]
    [InlineData("PCT", "Hammer Brush", "Hammer Stamp")]   // combo chain
    [InlineData("PCT", "Polishing Hammer", "Hammer Stamp")]
    [InlineData("PCT", "Water in Blue", "Fire in Red")]   // curated table
    [InlineData("PCT", "Pom Motif", "Creature Motif")]
    [InlineData("VPR", "Hunter's Sting", "Steel Fangs")]
    [InlineData("GNB", "Jugular Rip", "Continuation")]
    [InlineData("SAM", "Midare Setsugekka", "Iaijutsu")]
    [InlineData("NIN", "Hyosho Ranryu", "Ninjutsu")]
    [InlineData("RDM", "Enchanted Zwerchhau", "Zwerchhau")] // the table beats the combo chain (which leads to Riposte)
    public void ReplacementsKnowTheirButton(string job, string action, string host) =>
        Assert.Contains(Find(job, host).Id, Catalog.ButtonHosts(job, Find(job, action).Id));

    [SqpackFact]
    public void ButtonsOfTheirOwnHaveNoHost()
    {
        Assert.Empty(Catalog.ButtonHosts("PCT", Find("PCT", "Fire in Red").Id));
        Assert.Empty(Catalog.ButtonHosts("PCT", Find("PCT", "Living Muse").Id));
        Assert.Empty(Catalog.ButtonHosts("RDM", Find("RDM", "Riposte").Id));
    }
}
