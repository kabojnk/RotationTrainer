namespace RotationTrainer.Core;

/// <summary>One short rotation on a levelling card: the buttons in order, and a one-line note.</summary>
/// <param name="Threshold">For the pull vignette, how many enemies make the AoE worth using (0 when not set).</param>
public sealed record CardVignette(IReadOnlyList<Ability> Steps, string Note, int Threshold = 0);

/// <summary>What to press while levelling a job through one band of levels.</summary>
public sealed record LevellingCard(string Job, int MinLevel, int MaxLevel, CardVignette Pull, CardVignette Single,
    IReadOnlyList<Ability> Weave, IReadOnlyList<Ability> Mitigation, string Tip)
{
    public string Label => $"{Job} {MinLevel}-{MaxLevel}";
}

/// <param name="Card">The card the error is about, e.g. "PLD 60-69", or "(file)".</param>
public sealed record CardError(string Card, string Message);
