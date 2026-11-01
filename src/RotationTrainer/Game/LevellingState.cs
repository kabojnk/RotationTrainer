using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using RotationTrainer.Core;

namespace RotationTrainer.Game;

/// <summary>
/// The player's class, jobs and synced level, and the job and level the levelling windows show. Call
/// <see cref="Update"/> once a frame before drawing them.
/// </summary>
public sealed class LevellingState(IPlayerState player, IDataManager data, IAbilityCatalog catalog, LevellingDeck deck)
{
    private uint _classJobId;
    // The job picked among the player's own (ACN: SMN or SCH); picking it keeps following the player's level.
    private string? _preferredJob;

    public LevellingDeck Deck => deck;
    public LevellingSelection Selection { get; } = new();

    /// <summary>The player's class or job abbreviation (GLA, PLD…), or null when not logged in.</summary>
    public string? PlayerClass { get; private set; }

    /// <summary>The jobs the player's class or job has cards under (ACN: SMN, SCH).</summary>
    public IReadOnlyList<string> PlayerJobs { get; private set; } = Array.Empty<string>();

    /// <summary>The level the player can play at right now (the synced level in a synced duty).</summary>
    public int PlayerLevel { get; private set; }

    public LevellingCard? Card => Selection.Job is { } job ? deck.Find(job, Selection.Level) : null;

    /// <summary>"PLD", or the class while the guide follows a player on one ("GLA").</summary>
    public string? DisplayJob =>
        Selection.FollowsPlayer && PlayerClass != null && PlayerJobs.Contains(Selection.Job) ? PlayerClass : Selection.Job;

    public void Update()
    {
        if (!player.IsLoaded)
        {
            PlayerClass = null;
            PlayerJobs = Array.Empty<string>();
            PlayerLevel = 0;
            return;
        }
        var id = player.ClassJob.RowId;
        if (id != _classJobId)
        {
            _classJobId = id;
            PlayerClass = data.GetExcelSheet<ClassJob>(ClientLanguage.English).GetRowOrDefault(id)?.Abbreviation.ExtractText();
            PlayerJobs = PlayerClass == null ? Array.Empty<string>() : catalog.JobsForClass(PlayerClass);
            if (_preferredJob != null && !PlayerJobs.Contains(_preferredJob)) _preferredJob = null;
        }
        PlayerLevel = player.EffectiveLevel;
        Selection.Follow(_preferredJob ?? PlayerJobs.FirstOrDefault(), PlayerLevel);
    }

    /// <summary>Shows a job. One of the player's own keeps following their level; any other stops following.</summary>
    public void PickJob(string job)
    {
        if (Selection.FollowsPlayer && PlayerJobs.Contains(job))
        {
            _preferredJob = job;
            Selection.Follow(job, PlayerLevel);
        }
        else
        {
            Selection.Select(job, catalog.StartLevel(job));
        }
    }

    /// <summary>The band the selected level falls in: the card's, or the 10-level band it would have.</summary>
    public (int Min, int Max) Band()
    {
        if (Card is { } card) return (card.MinLevel, card.MaxLevel);
        var level = Selection.Level;
        if (level >= 100) return (100, 100);
        var start = Selection.Job is { } job ? catalog.StartLevel(job) : 1;
        return (Math.Max(start, level < 10 ? 1 : level / 10 * 10), level < 10 ? 9 : level / 10 * 10 + 9);
    }
}
