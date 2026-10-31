namespace RotationTrainer.Core;

public enum KeybindMode { Auto, Keyboard, Controller, Off }

public enum InputMode { Keyboard, Controller }

/// <summary>
/// One hotbar slot. <paramref name="Reachable"/> is false for a slot the player can't press right now (a cross
/// hotbar set that isn't active): its hint would name a button that fires something else. <paramref name="Type"/> says whether the ids are actions or items (item ids may carry the
/// HQ +1,000,000 offset). <paramref name="CommandId"/> is 0 when the slot's command is not an action or item
/// (e.g. a macro showing an action's icon): such a slot is matched only by its apparent action.
/// </summary>
public sealed record HotbarObservation(int Bar, int Slot, bool IsCross, StepType Type, uint CommandId, uint ApparentActionId,
    string? Hint, bool Reachable = true);

/// <summary>
/// A slot seen showing <paramref name="AbilityId"/> while its command was <paramref name="CommandId"/> (e.g. Pom Muse on
/// a Living Muse slot). Saved so the slot is known the next time, before it shows that ability again.
/// </summary>
public sealed record LearnedKeybind(StepType Type, uint AbilityId, int Bar, int Slot, bool IsCross, uint CommandId);

public sealed class KeybindIndex
{
    public const string Unknown = "?";

    private readonly record struct SlotKey(int Bar, int Slot, bool IsCross);

    private readonly record struct Learned(SlotKey Slot, uint CommandId);

    private readonly record struct AbilityKey(StepType Type, uint Id);

    private IReadOnlyList<HotbarObservation> _snapshot = Array.Empty<HotbarObservation>();
    private readonly Dictionary<AbilityKey, List<Learned>> _learned = new();

    /// <summary>Takes a new hotbar snapshot. Returns true when it taught a slot something new (see <see cref="Export"/>).</summary>
    public bool Update(IReadOnlyList<HotbarObservation> snapshot)
    {
        _snapshot = snapshot.ToArray();
        var learnedNew = false;
        foreach (var o in _snapshot)
        {
            // A slot showing its own command is found by the command rule; only replacements are worth learning.
            if (o.ApparentActionId == 0 || o.CommandId == 0 || Id(o.Type, o.ApparentActionId) == Id(o.Type, o.CommandId))
                continue;
            learnedNew |= Learn(o.Type, o.ApparentActionId, new Learned(new SlotKey(o.Bar, o.Slot, o.IsCross), Id(o.Type, o.CommandId)));
        }
        return learnedNew;
    }

    /// <summary>Everything learned so far, to save.</summary>
    public IReadOnlyList<LearnedKeybind> Export() =>
        _learned.SelectMany(kv => kv.Value.Select(l =>
            new LearnedKeybind(kv.Key.Type, kv.Key.Id, l.Slot.Bar, l.Slot.Slot, l.Slot.IsCross, l.CommandId))).ToList();

    /// <summary>Adds slots learned in an earlier session.</summary>
    public void Import(IEnumerable<LearnedKeybind> learned)
    {
        foreach (var l in learned)
            Learn(l.Type, l.AbilityId, new Learned(new SlotKey(l.Bar, l.Slot, l.IsCross), Id(l.Type, l.CommandId)));
    }

    private bool Learn(StepType type, uint abilityId, Learned entry)
    {
        var key = Key(type, abilityId);
        if (!_learned.TryGetValue(key, out var list))
            _learned[key] = list = new List<Learned>();
        if (list.Contains(entry)) return false;
        list.Add(entry);
        return true;
    }

    /// <summary>
    /// The hint of the lowest reachable slot holding this action or item, or "?" when there is none. In order: a slot
    /// showing it; a slot seen showing it before; a slot of one of its <paramref name="hosts"/> (the buttons it
    /// replaces, e.g. Living Muse for Pom Muse), by command or by what the slot shows; a slot whose command is it.
    /// Within a rule a slot with a hint beats one without; an empty hint never falls through to a later rule.
    /// </summary>
    public string Label(StepType type, uint id, InputMode mode, IReadOnlyCollection<uint>? hosts = null)
    {
        id = Id(type, id);
        if (id == 0) return Unknown;
        var cross = mode == InputMode.Controller;
        var slots = _snapshot.Where(o => o.Reachable && o.IsCross == cross && o.Type == type).ToList();

        var hit = Best(slots.Where(o => Id(type, o.ApparentActionId) == id));

        if (hit == null && _learned.TryGetValue(new AbilityKey(type, id), out var list))
            hit = Best(slots.Where(o => o.CommandId != 0
                && list.Contains(new Learned(new SlotKey(o.Bar, o.Slot, o.IsCross), Id(type, o.CommandId)))));

        if (hit == null && hosts is { Count: > 0 })
            hit = Best(slots.Where(o => hosts.Contains(o.CommandId) || hosts.Contains(o.ApparentActionId)));

        hit ??= Best(slots.Where(o => o.CommandId != 0 && Id(type, o.CommandId) == id));

        return hit == null || string.IsNullOrWhiteSpace(hit.Hint) ? Unknown : hit.Hint;
    }

    public void Clear()
    {
        _snapshot = Array.Empty<HotbarObservation>();
        _learned.Clear();
    }

    private static uint Id(StepType type, uint id) => type == StepType.Item ? ItemIds.Normalize(id) : id;

    private static AbilityKey Key(StepType type, uint id) => new(type, Id(type, id));

    private static HotbarObservation? Best(IEnumerable<HotbarObservation> source) =>
        source.OrderBy(o => string.IsNullOrWhiteSpace(o.Hint)).ThenBy(o => o.Bar).ThenBy(o => o.Slot).FirstOrDefault();
}
