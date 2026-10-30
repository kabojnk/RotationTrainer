namespace RotationTrainer.Core;

public sealed record BuilderWarning(int? StepIndex, string Message);

/// <summary>Editable rotation model behind the builder window.</summary>
public sealed class BuilderDocument
{
    private readonly IAbilityCatalog _catalog;
    private readonly List<Step> _steps = new();
    private RotationKind _kind;
    private string? _source;
    private string? _patch;

    public BuilderDocument(string job, RotationKind kind, string name, IAbilityCatalog catalog)
    {
        Job = job;
        Name = name;
        _kind = kind;
        _catalog = catalog;
        if (kind == RotationKind.Opener) _steps.Add(PullStep);
    }

    public static BuilderDocument Edit(Rotation rotation, IAbilityCatalog catalog)
    {
        var d = new BuilderDocument(rotation.Job, rotation.Kind, rotation.Name, catalog)
        {
            Id = rotation.Id,
            _source = rotation.Source,
            _patch = rotation.Patch,
        };
        d._steps.Clear();
        d._steps.AddRange(rotation.Steps);
        return d;
    }

    private static readonly Step PullStep = new(StepType.Pull, 0, "Pull");

    public string? Id { get; private init; }
    public string Job { get; }
    public string Name { get; set; }

    public RotationKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            if (value == RotationKind.Loop) _steps.RemoveAll(s => s.Type == StepType.Pull);
            else if (PullIndex < 0) _steps.Insert(0, PullStep);
        }
    }

    public IReadOnlyList<Step> Steps => _steps;

    public int PullIndex => _steps.FindIndex(s => s.Type == StepType.Pull);

    public void Insert(int index, Step step)
    {
        if (step.Type == StepType.Pull && (Kind == RotationKind.Loop || PullIndex >= 0)) return;
        _steps.Insert(Math.Clamp(index, 0, _steps.Count), step);
    }

    public void Move(int from, int insertBefore)
    {
        if (from < 0 || from >= _steps.Count) return;
        insertBefore = Math.Clamp(insertBefore, 0, _steps.Count);
        if (insertBefore == from || insertBefore == from + 1) return;
        var step = _steps[from];
        _steps.RemoveAt(from);
        if (from < insertBefore) insertBefore--;
        _steps.Insert(insertBefore, step);
    }

    public void Remove(int index)
    {
        if (index >= 0 && index < _steps.Count) _steps.RemoveAt(index);
    }

    public void SetAt(int index, double? at)
    {
        if (IsPrepull(index)) _steps[index] = _steps[index] with { At = at };
    }

    public bool IsPrepull(int index)
    {
        var pull = PullIndex;
        return Kind == RotationKind.Opener && pull >= 0 && index >= 0 && index < pull;
    }

    private bool IsGcd(Step s) =>
        s.Type == StepType.Action && _catalog.FindAction(Job, s.Id)?.IsGcd == true;

    /// <summary>Runs of 3+ weaves directly after a post-pull GCD: (first, last, gcd name).</summary>
    private List<(int First, int Last, string Gcd)> ClippingRuns()
    {
        var runs = new List<(int, int, string)>();
        for (var i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].Type == StepType.Pull || IsGcd(_steps[i])) continue;
            var start = i;
            while (i + 1 < _steps.Count && _steps[i + 1].Type != StepType.Pull && !IsGcd(_steps[i + 1])) i++;
            var prev = start - 1;
            if (prev >= 0 && IsGcd(_steps[prev]) && !IsPrepull(prev) && i - start + 1 >= 3)
                runs.Add((start, i, _steps[prev].Name));
        }
        return runs;
    }

    public IReadOnlySet<int> FlaggedSteps()
    {
        var set = new HashSet<int>();
        foreach (var (first, last, _) in ClippingRuns())
            for (var i = first; i <= last; i++) set.Add(i);
        return set;
    }

    public IReadOnlyList<BuilderWarning> Warnings()
    {
        var list = new List<BuilderWarning>();
        if (Kind == RotationKind.Opener && PullIndex < 0)
            list.Add(new BuilderWarning(null, "No Pull marker. Openers need one to line up the countdown."));
        foreach (var (first, last, gcd) in ClippingRuns())
            list.Add(new BuilderWarning(first, $"{last - first + 1} weaves after {gcd} may clip your GCD"));
        return list;
    }

    public Rotation ToRotation(Func<string> newId)
    {
        var steps = new List<Step>();
        for (var i = 0; i < _steps.Count; i++)
        {
            var s = _steps[i];
            if (s.Type == StepType.Pull && Kind == RotationKind.Loop) continue;
            if (s.At != null && !IsPrepull(i)) s = s with { At = null };
            steps.Add(s);
        }
        return new Rotation(Id ?? newId(), Job, 100, Kind, Name, steps, _source, _patch);
    }
}
