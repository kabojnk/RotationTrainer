namespace RotationTrainer.Core;

public enum KeybindMode { Auto, Keyboard, Controller, Off }

public enum InputMode { Keyboard, Controller }

/// <summary>
/// One hotbar slot. <paramref name="Type"/> says whether the ids are actions or items (item ids may carry the
/// HQ +1,000,000 offset). <paramref name="CommandId"/> is 0 when the slot's command is not an action or item
/// (e.g. a macro showing an action's icon): such a slot is matched only by its apparent action.
/// </summary>
public sealed record HotbarObservation(int Bar, int Slot, bool IsCross, StepType Type, uint CommandId, uint ApparentActionId,
    string? Hint);

public sealed class KeybindIndex
{
    public const string Unknown = "?";

    private readonly record struct SlotKey(int Bar, int Slot, bool IsCross);

    private readonly record struct Learned(SlotKey Slot, uint CommandId);

    private readonly record struct AbilityKey(StepType Type, uint Id);

    private IReadOnlyList<HotbarObservation> _snapshot = Array.Empty<HotbarObservation>();
    private readonly Dictionary<AbilityKey, List<Learned>> _learned = new();

    public void Update(IReadOnlyList<HotbarObservation> snapshot)
    {
        _snapshot = snapshot.ToArray();
        foreach (var o in _snapshot)
        {
            if (o.ApparentActionId == 0 || o.CommandId == 0) continue;
            var key = Key(o.Type, o.ApparentActionId);
            if (!_learned.TryGetValue(key, out var list))
                _learned[key] = list = new List<Learned>();
            var entry = new Learned(new SlotKey(o.Bar, o.Slot, o.IsCross), Id(o.Type, o.CommandId));
            if (!list.Contains(entry)) list.Add(entry);
        }
    }

    /// <summary>The hint of the lowest slot holding this action or item, or "?" when there is none.</summary>
    public string Label(StepType type, uint id, InputMode mode)
    {
        id = Id(type, id);
        if (id == 0) return Unknown;
        var cross = mode == InputMode.Controller;
        var slots = _snapshot.Where(o => o.IsCross == cross && o.Type == type).ToList();

        var hit = Lowest(slots.Where(o => Id(type, o.ApparentActionId) == id));

        if (hit == null && _learned.TryGetValue(new AbilityKey(type, id), out var list))
            hit = Lowest(slots.Where(o => o.CommandId != 0
                && list.Contains(new Learned(new SlotKey(o.Bar, o.Slot, o.IsCross), Id(type, o.CommandId)))));

        hit ??= Lowest(slots.Where(o => o.CommandId != 0 && Id(type, o.CommandId) == id));

        return hit == null || string.IsNullOrWhiteSpace(hit.Hint) ? Unknown : hit.Hint;
    }

    public void Clear()
    {
        _snapshot = Array.Empty<HotbarObservation>();
        _learned.Clear();
    }

    private static uint Id(StepType type, uint id) => type == StepType.Item ? ItemIds.Normalize(id) : id;

    private static AbilityKey Key(StepType type, uint id) => new(type, Id(type, id));

    private static HotbarObservation? Lowest(IEnumerable<HotbarObservation> source) =>
        source.OrderBy(o => o.Bar).ThenBy(o => o.Slot).FirstOrDefault();
}
