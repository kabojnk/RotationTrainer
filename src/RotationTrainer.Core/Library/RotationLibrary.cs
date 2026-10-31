namespace RotationTrainer.Core;

public enum ConflictChoice { Replace, KeepBoth }

public sealed class RotationLibrary
{
    private readonly List<Rotation> _rotations;

    public RotationLibrary(IEnumerable<Rotation> rotations) => _rotations = rotations.ToList();

    public event Action? Changed;

    public IReadOnlyList<Rotation> All => _rotations;

    public IEnumerable<Rotation> ForJob(string job, RotationKind? kind = null) =>
        _rotations.Where(r => r.Job == job && (kind == null || r.Kind == kind));

    public Rotation? Get(string id) => _rotations.FirstOrDefault(r => r.Id == id);

    public IReadOnlyList<Rotation> Conflicts(IEnumerable<Rotation> incoming) =>
        incoming.Where(r => Get(r.Id) != null).ToList();

    public void Add(IEnumerable<Rotation> incoming, Func<Rotation, ConflictChoice> resolve, Func<string> newId)
    {
        foreach (var r in incoming)
        {
            var idx = _rotations.FindIndex(x => x.Id == r.Id);
            if (idx < 0)
                _rotations.Add(r);
            else if (resolve(r) == ConflictChoice.Replace)
                _rotations[idx] = r;
            else
                _rotations.Add(r with { Id = newId(), Name = r.Name + " (copy)" });
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Adds the built-in rotations not yet in <paramref name="offered"/>, then records them there, so one the
    /// player deleted is not added back and one new in a later version is. Returns how many were added.
    /// </summary>
    public int AddBuiltIns(IEnumerable<Rotation> builtIns, ISet<string> offered) =>
        AddMissing(builtIns.Where(r => offered.Add(r.Id)));

    /// <summary>Adds back any built-in rotation missing from the library; edited ones are left as they are.</summary>
    public int RestoreBuiltIns(IEnumerable<Rotation> builtIns) => AddMissing(builtIns);

    private int AddMissing(IEnumerable<Rotation> rotations)
    {
        var added = 0;
        foreach (var r in rotations.ToList())
        {
            if (Get(r.Id) != null) continue;
            _rotations.Add(r);
            added++;
        }
        if (added > 0) Changed?.Invoke();
        return added;
    }

    public void Save(Rotation rotation)
    {
        var idx = _rotations.FindIndex(x => x.Id == rotation.Id);
        if (idx < 0) _rotations.Add(rotation); else _rotations[idx] = rotation;
        Changed?.Invoke();
    }

    public bool Delete(string id)
    {
        var removed = _rotations.RemoveAll(r => r.Id == id) > 0;
        if (removed) Changed?.Invoke();
        return removed;
    }
}
