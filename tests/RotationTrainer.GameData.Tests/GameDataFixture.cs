using Lumina.Excel.Sheets;
using RotationTrainer.GameData;

namespace RotationTrainer.GameData.Tests;

/// <summary>A [Fact] that is skipped unless FFXIV_SQPACK points at a game install's sqpack folder.</summary>
public sealed class SqpackFactAttribute : FactAttribute
{
    public SqpackFactAttribute()
    {
        if (string.IsNullOrEmpty(GameDataFixture.SqpackPath))
            Skip = "FFXIV_SQPACK is not set.";
    }
}

/// <summary>Loads the game data once for every test (opening sqpack is slow).</summary>
public sealed class GameDataFixture
{
    public static string? SqpackPath => Environment.GetEnvironmentVariable("FFXIV_SQPACK");

    private readonly Lazy<(LuminaCatalog Catalog, string[] CombatJobs, Lumina.Excel.ExcelModule Excel)> _data = new(() =>
    {
        var gameData = new Lumina.GameData(SqpackPath!);
        var jobs = gameData.Excel.GetSheet<ClassJob>(Lumina.Data.Language.English)
            .Where(j => j.JobIndex > 0 && j.DohDolJobIndex < 0)
            .Select(j => j.Abbreviation.ExtractText())
            .ToArray();
        return (new LuminaCatalog(gameData.Excel), jobs, gameData.Excel);
    });

    public LuminaCatalog Catalog => _data.Value.Catalog;
    public string[] CombatJobs => _data.Value.CombatJobs;
    public Lumina.Excel.ExcelModule Excel => _data.Value.Excel;
}

[CollectionDefinition("GameData")]
public sealed class GameDataCollection : ICollectionFixture<GameDataFixture>;
