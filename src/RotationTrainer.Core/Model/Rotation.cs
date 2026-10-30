namespace RotationTrainer.Core;

public enum RotationKind { Opener, Loop }

public enum StepType { Action, Item, Pull }

public sealed record Step(StepType Type, uint Id, string Name, double? At = null);

public sealed record Rotation(string Id, string Job, int Level, RotationKind Kind, string Name,
    IReadOnlyList<Step> Steps, string? Source = null, string? Patch = null);
