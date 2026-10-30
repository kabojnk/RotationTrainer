namespace RotationTrainer.Core;

public sealed record PlannedStep(Ability Ability, double? At, bool IsPrepull, int? GcdNumber, bool FromLoop);

public sealed class PracticeSequence
{
    private readonly List<PlannedStep> _opener;
    private readonly List<PlannedStep> _loop;

    public static readonly PracticeSequence Empty = new(new(), new());

    private PracticeSequence(List<PlannedStep> opener, List<PlannedStep> loop)
    {
        _opener = opener;
        _loop = loop;
    }

    public bool IsEmpty => _opener.Count == 0 && _loop.Count == 0;
    public int OpenerLength => _opener.Count;
    public bool IsEndless => _loop.Count > 0;

    public PlannedStep? this[int index]
    {
        get
        {
            if (index < 0) return null;
            if (index < _opener.Count) return _opener[index];
            if (_loop.Count == 0) return null;
            return _loop[(index - _opener.Count) % _loop.Count];
        }
    }

    public static PracticeSequence Build(Rotation? opener, Rotation? loop, bool usePotions, IAbilityCatalog catalog) =>
        new(Plan(opener, false, usePotions, catalog), Plan(loop, true, usePotions, catalog));

    private static List<PlannedStep> Plan(Rotation? rotation, bool fromLoop, bool usePotions, IAbilityCatalog catalog)
    {
        var result = new List<PlannedStep>();
        if (rotation == null) return result;

        var hasPull = rotation.Steps.Any(s => s.Type == StepType.Pull);
        var beforePull = hasPull;
        var gcd = 0;
        foreach (var step in rotation.Steps)
        {
            if (step.Type == StepType.Pull) { beforePull = false; continue; }

            var ability = step.Type == StepType.Item
                ? catalog.FindItem(ItemIds.Normalize(step.Id))
                : catalog.FindAction(rotation.Job, step.Id);
            if (ability == null) continue;
            if (ability.Category == AbilityCategory.Item && !usePotions) continue;

            int? number = null;
            if (!beforePull && ability.IsGcd) number = ++gcd;
            result.Add(new PlannedStep(ability, step.At, beforePull, number, fromLoop));
        }
        return result;
    }
}
