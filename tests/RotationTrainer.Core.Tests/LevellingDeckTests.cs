using RotationTrainer.Core;
using RotationTrainer.Core.Tests.Fakes;
using Xunit;

namespace RotationTrainer.Core.Tests;

public class LevellingDeckTests
{
    private static readonly FakeCatalog Catalog = new();

    private static string Card(string job, int min, int max, string single = "\"Fast Blade\", \"Riot Blade\"") =>
        $$"""
        { "job": "{{job}}", "minLevel": {{min}}, "maxLevel": {{max}},
          "pull": { "threshold": 3, "steps": ["Total Eclipse"], "note": "Pull note." },
          "single": { "steps": [{{single}}], "note": "Single note." },
          "weave": ["Fight or Flight"], "mitigation": ["Rampart"], "tip": "A tip." }
        """;

    private static IEnumerable<string> Names(IEnumerable<CardStep> steps) => steps.Select(s => s.Options[0].Name);

    private static (LevellingDeck Deck, IReadOnlyList<CardError> Errors) Parse(params string[] cards) =>
        LevellingDeck.Parse("[" + string.Join(",", cards) + "]", Catalog);

    [Fact]
    public void ParsesValidCardAndResolvesSteps()
    {
        var (deck, errors) = Parse(Card("PLD", 60, 69, "\"Fast Blade\", \"Riot Blade\", \"Royal Authority\""));
        Assert.Empty(errors);
        var card = deck.Find("PLD", 64)!;
        Assert.Equal(new[] { "Fast Blade", "Riot Blade", "Royal Authority" }, Names(card.Single.Steps));
        Assert.Equal(3, card.Pull.Threshold);
        Assert.Equal("Pull note.", card.Pull.Note);
        Assert.Equal(new[] { "Fight or Flight" }, Names(card.Weave));
        Assert.Equal(new[] { "Rampart" }, Names(card.Mitigation));
        Assert.Equal("A tip.", card.Tip);
    }

    [Fact]
    public void MitigationAndTipAreOptional()
    {
        var json = """
            [{ "job": "PLD", "minLevel": 1, "maxLevel": 9,
               "pull": { "steps": ["Fast Blade"], "note": "No AoE yet." },
               "single": { "steps": ["Fast Blade"], "note": "" }, "weave": [] }]
            """;
        var (deck, errors) = LevellingDeck.Parse(json, Catalog);
        Assert.Empty(errors);
        var card = deck.Find("PLD", 5)!;
        Assert.Empty(card.Mitigation);
        Assert.Equal("", card.Tip);
        Assert.Equal(0, card.Pull.Threshold);
    }

    [Fact]
    public void FindReturnsNullOutsideEveryBand()
    {
        var (deck, _) = Parse(Card("PLD", 60, 69));
        Assert.Null(deck.Find("PLD", 59));
        Assert.Null(deck.Find("PLD", 70));
        Assert.NotNull(deck.Find("PLD", 60));
        Assert.NotNull(deck.Find("PLD", 69));
    }

    [Fact]
    public void FindReturnsNullForJobWithoutCards()
    {
        var (deck, _) = Parse(Card("PLD", 60, 69));
        Assert.Null(deck.Find("BLM", 50));
        Assert.False(deck.HasJob("BLM"));
        Assert.True(deck.HasJob("PLD"));
    }

    [Fact]
    public void ForJobIsOrderedByLevel()
    {
        var (deck, _) = Parse(Card("PLD", 60, 69), Card("PLD", 10, 19));
        Assert.Equal(new[] { 10, 60 }, deck.ForJob("PLD").Select(c => c.MinLevel));
        Assert.Empty(deck.ForJob("BLM"));
    }

    [Fact]
    public void UnknownStepDropsOnlyThatCard()
    {
        var (deck, errors) = Parse(Card("PLD", 50, 59, "\"Nonsense Blade\""), Card("PLD", 60, 69));
        var error = Assert.Single(errors);
        Assert.Equal("PLD 50-59", error.Card);
        Assert.Contains("Nonsense Blade", error.Message);
        Assert.Single(deck.All);
    }

    [Fact]
    public void StepAboveBandIsAnError()
    {
        var (deck, errors) = Parse(Card("PLD", 50, 59, "\"Royal Authority\""));
        var error = Assert.Single(errors);
        Assert.Contains("Royal Authority", error.Message);
        Assert.Contains("60", error.Message);
        Assert.Empty(deck.All);
    }

    [Fact]
    public void OverlappingBandsAreAnError()
    {
        var (deck, errors) = Parse(Card("PLD", 60, 69), Card("PLD", 65, 74));
        Assert.Equal("PLD 65-74", Assert.Single(errors).Card);
        Assert.Single(deck.All);
    }

    [Fact]
    public void UnknownJobAndBadRangeAreErrors()
    {
        var (deck, errors) = Parse(Card("XYZ", 1, 9), Card("PLD", 70, 60), Card("PLD", 0, 9));
        Assert.Equal(3, errors.Count);
        Assert.Empty(deck.All);
    }

    [Fact]
    public void EmptyStepsAreAnError()
    {
        var (_, errors) = Parse(Card("PLD", 60, 69, ""));
        Assert.Single(errors);
    }

    [Fact]
    public void MalformedJsonReturnsErrorNotThrow()
    {
        var (deck, errors) = LevellingDeck.Parse("{not json", Catalog);
        Assert.Empty(deck.All);
        Assert.Single(errors);
    }

    [Fact]
    public void NonObjectCardsAndNonStringStepsAreErrorsNotThrows()
    {
        var (deck, errors) = Parse("1", "\"PLD\"", Card("PLD", 50, 59, "42"), Card("PLD", 60, 69));
        Assert.Equal(3, errors.Count);
        Assert.Single(deck.All);
    }

    [Fact]
    public void UpgradeChainShowsTheStepUnlockedAtTheLevel()
    {
        var json = """
            [{ "job": "PLD", "minLevel": 60, "maxLevel": 69,
               "pull": { "steps": ["Total Eclipse"], "note": "" },
               "single": { "steps": ["Fast Blade"], "note": "" },
               "weave": [["Requiescat", "Fight or Flight"]] }]
            """;
        var (deck, errors) = LevellingDeck.Parse(json, Catalog);
        Assert.Empty(errors);
        var step = deck.Find("PLD", 60)!.Weave[0];
        Assert.Equal("Fight or Flight", step.At(64).Name);
        Assert.Equal("Requiescat", step.At(68).Name);
        Assert.Equal("Fight or Flight", step.At(1).Name);
        Assert.Equal("Fast Blade", deck.Find("PLD", 60)!.Single.Steps[0].At(60).Name);
    }

    [Fact]
    public void UpgradeChainMustListTheNewestFirst()
    {
        var (deck, errors) = Parse(Card("PLD", 60, 69, "[\"Fast Blade\", \"Royal Authority\"]"));
        Assert.Contains("newest first", Assert.Single(errors).Message);
        Assert.Empty(deck.All);
    }

    [Fact]
    public void UpgradeChainOfNonStringsIsAnError()
    {
        var (_, errors) = Parse(Card("PLD", 60, 69, "[1, 2]"));
        Assert.Single(errors);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[[\"Fast Blade\"]]")]
    public void EmptyOrNestedChainIsAnError(string step)
    {
        var (deck, errors) = Parse(Card("PLD", 60, 69, step));
        Assert.NotEmpty(errors);
        Assert.Empty(deck.All);
    }
}
