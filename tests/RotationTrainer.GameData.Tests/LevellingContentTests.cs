using RotationTrainer.Core;

namespace RotationTrainer.GameData.Tests;

[Collection("GameData")]
public class LevellingContentTests(GameDataFixture fixture)
{
    private static readonly string CardsPath = Path.Combine(ExampleFileTests.RepoRoot(), "presets", "levelling-cards.json");

    [SqpackFact]
    public void CardsCoverEveryWrittenJob()
    {
        var (deck, errors) = LevellingDeck.Parse(File.ReadAllText(CardsPath), fixture.Catalog);
        Assert.Empty(errors.Select(e => $"{e.Card}: {e.Message}"));
        Assert.NotEmpty(deck.All);

        foreach (var job in deck.All.Select(c => c.Job).Distinct())
        {
            var cards = deck.ForJob(job);
            var start = fixture.Catalog.StartLevel(job);
            Assert.True(cards[0].MinLevel == start, $"{job}: first card starts at {cards[0].MinLevel}, job starts at {start}.");
            for (var i = 1; i < cards.Count; i++)
                Assert.True(cards[i].MinLevel == cards[i - 1].MaxLevel + 1, $"{job}: gap or overlap before {cards[i].Label}.");
            foreach (var card in cards.SkipLast(1))
                Assert.True(card.MaxLevel % 10 == 9, $"{card.Label} should end on a level ending in 9.");
            Assert.True(cards[^1] is { MinLevel: 100, MaxLevel: 100 }, $"{job}: the last card should be 100-100.");
        }
    }

    [SqpackFact]
    public void CopyHasNoDashesOrExclamations()
    {
        var text = File.ReadAllText(CardsPath);
        Assert.DoesNotContain("—", text);
        Assert.DoesNotContain("!", text);
    }
}
