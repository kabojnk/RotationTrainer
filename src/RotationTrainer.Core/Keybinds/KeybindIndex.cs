namespace RotationTrainer.Core;

public enum KeybindMode { Auto, Keyboard, Controller, Off }

public enum InputMode { Keyboard, Controller }

public sealed record HotbarObservation(int Bar, int Slot, bool IsCross, uint CommandId, uint ApparentActionId, string? Hint);

public sealed class KeybindIndex
{
    public const string Unknown = "?";

    private readonly record struct SlotKey(int Bar, int Slot, bool IsCross);

    private readonly record struct Learned(SlotKey Slot, uint CommandId);

    private IReadOnlyList<HotbarObservation> _snapshot = Array.Empty<HotbarObservation>();
    private readonly Dictionary<uint, List<Learned>> _learned = new();

    public void Update(IReadOnlyList<HotbarObservation> snapshot)
    {
        _snapshot = snapshot.ToArray();
        foreach (var o in _snapshot)
        {
            if (o.ApparentActionId == 0) continue;
            if (!_learned.TryGetValue(o.ApparentActionId, out var list))
                _learned[o.ApparentActionId] = list = new List<Learned>();
            var entry = new Learned(new SlotKey(o.Bar, o.Slot, o.IsCross), o.CommandId);
            if (!list.Contains(entry)) list.Add(entry);
        }
    }

    public string Label(uint actionId, InputMode mode)
    {
        if (actionId == 0) return Unknown;
        var cross = mode == InputMode.Controller;
        var slots = _snapshot.Where(o => o.IsCross == cross).ToList();

        var hit = Lowest(slots.Where(o => o.ApparentActionId == actionId));

        if (hit == null && _learned.TryGetValue(actionId, out var list))
            hit = Lowest(slots.Where(o => list.Contains(new Learned(new SlotKey(o.Bar, o.Slot, o.IsCross), o.CommandId))));

        hit ??= Lowest(slots.Where(o => o.CommandId == actionId));

        return hit == null || string.IsNullOrWhiteSpace(hit.Hint) ? Unknown : hit.Hint;
    }

    public void Clear()
    {
        _snapshot = Array.Empty<HotbarObservation>();
        _learned.Clear();
    }

    private static HotbarObservation? Lowest(IEnumerable<HotbarObservation> source) =>
        source.OrderBy(o => o.Bar).ThenBy(o => o.Slot).FirstOrDefault();
}
