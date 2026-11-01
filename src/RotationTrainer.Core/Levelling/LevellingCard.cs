namespace RotationTrainer.Core;

/// <summary>
/// One button on a card. <paramref name="Options"/> is an upgrade chain, newest first (Stone II, then Stone), so
/// the card shows the one the player has at their level; most steps have a single option.
/// </summary>
public sealed record CardStep(IReadOnlyList<Ability> Options)
{
    /// <summary>The newest option unlocked by this level, or the oldest (shown as locked) when none is.</summary>
    public Ability At(int level) => Options.FirstOrDefault(a => a.Level <= level) ?? Options[^1];
}

/// <summary>One short rotation on a levelling card: the buttons in order, and a one-line note.</summary>
/// <param name="Threshold">For the pull vignette, how many enemies make the AoE worth using (0 when not set).</param>
public sealed record CardVignette(IReadOnlyList<CardStep> Steps, string Note, int Threshold = 0);

/// <summary>What to press while levelling a job through one band of levels.</summary>
public sealed record LevellingCard(string Job, int MinLevel, int MaxLevel, CardVignette Pull, CardVignette Single,
    IReadOnlyList<CardStep> Weave, IReadOnlyList<CardStep> Mitigation, string Tip)
{
    public string Label => $"{Job} {MinLevel}-{MaxLevel}";
}

/// <param name="Card">The card the error is about, e.g. "PLD 60-69", or "(file)".</param>
public sealed record CardError(string Card, string Message);
