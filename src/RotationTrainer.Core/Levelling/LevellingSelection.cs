namespace RotationTrainer.Core;

/// <summary>
/// The job and level the levelling guide shows. It follows the player until they pick another job or level,
/// and goes back to following them on <see cref="UsePlayer"/>.
/// </summary>
public sealed class LevellingSelection
{
    private const int MinLevel = 1;
    private const int MaxLevel = 100;

    public string? Job { get; private set; }
    public int Level { get; private set; } = MinLevel;
    public bool FollowsPlayer { get; private set; } = true;

    /// <summary>Call each frame with the player's job and level; ignored while the player has picked their own.</summary>
    public void Follow(string? job, int level)
    {
        if (!FollowsPlayer || job == null) return;
        Job = job;
        Level = Math.Clamp(level, MinLevel, MaxLevel);
    }

    /// <summary>Shows a job and stops following; a level below the job's start level is raised to it.</summary>
    public void Select(string job, int startLevel = MinLevel)
    {
        Job = job;
        Level = Math.Max(Level, startLevel);
        FollowsPlayer = false;
    }

    public void SetLevel(int level)
    {
        Level = Math.Clamp(level, MinLevel, MaxLevel);
        FollowsPlayer = false;
    }

    public void UsePlayer() => FollowsPlayer = true;
}
