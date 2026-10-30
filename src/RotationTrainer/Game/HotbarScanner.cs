using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using RotationTrainer.Core;
using SlotType = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.HotbarSlotType;

namespace RotationTrainer.Game;

/// <summary>
/// Reads the 10 standard hotbars (12 slots) and 8 cross hotbars (16 slots) of the current job.
/// Only slots that look like an action or item (directly, or through a macro's icon) are reported.
/// Item ids are normalized (HQ +1,000,000 removed). Call on the framework thread only.
/// </summary>
public sealed unsafe class HotbarScanner
{
    private const int StandardBars = 10;
    private const int StandardSlots = 12;
    private const int CrossBars = 8;
    private const int CrossSlots = 16;

    public IReadOnlyList<HotbarObservation> Scan()
    {
        var result = new List<HotbarObservation>();
        var module = RaptureHotbarModule.Instance();
        if (module == null) return result;

        var bars = module->Hotbars;
        for (var bar = 0; bar < StandardBars; bar++)
            Read(ref bars[bar], bar, StandardSlots, false, result);
        for (var bar = 0; bar < CrossBars; bar++)
            Read(ref bars[StandardBars + bar], bar, CrossSlots, true, result);
        return result;
    }

    private static void Read(ref RaptureHotbarModule.Hotbar bar, int barIndex, int slotCount, bool isCross,
        List<HotbarObservation> into)
    {
        for (var i = 0; i < slotCount; i++)
        {
            ref var slot = ref bar.Slots[i];
            if (slot.IsEmpty) continue;
            var apparent = slot.ApparentSlotType switch
            {
                SlotType.Action => slot.ApparentActionId,
                SlotType.Item => ItemIds.Normalize(slot.ApparentActionId),
                _ => 0u,
            };
            var command = slot.CommandType == SlotType.Item ? ItemIds.Normalize(slot.CommandId) : slot.CommandId;
            if (apparent == 0 && slot.CommandType is not (SlotType.Action or SlotType.Item)) continue;
            into.Add(new HotbarObservation(barIndex, i, isCross, command, apparent, Hint(slot.PopUpKeybindHintString)));
        }
    }

    /// <summary>The pop-up hint reads like " [Ctrl-3]"; the label is just "Ctrl-3".</summary>
    private static string? Hint(string raw)
    {
        var text = raw.Trim().TrimStart('[').TrimEnd(']').Trim();
        return text.Length == 0 ? null : text;
    }
}
