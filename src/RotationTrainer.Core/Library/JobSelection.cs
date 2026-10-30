namespace RotationTrainer.Core;

public sealed class JobSelection
{
    public string? OpenerId { get; set; }
    public string? LoopId { get; set; }

    public (Rotation? Opener, Rotation? Loop) Resolve(RotationLibrary library, string job) =>
        (Lookup(library, OpenerId, job, RotationKind.Opener), Lookup(library, LoopId, job, RotationKind.Loop));

    private static Rotation? Lookup(RotationLibrary library, string? id, string job, RotationKind kind)
    {
        if (id == null) return null;
        var r = library.Get(id);
        return r != null && r.Job == job && r.Kind == kind ? r : null;
    }
}
