using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using RotationTrainer.Core;
using SlotType = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.HotbarSlotType;

namespace RotationTrainer.Game;

/// <summary>
/// Reads the 10 standard hotbars (12 slots) and 8 cross hotbars (16 slots) of the current job.
/// Only slots that show or run an action or item (directly, or a macro with an action's icon) are reported.
/// Item ids are normalised (HQ +1,000,000 removed); a command that is not the slot's type is reported as 0.
/// Only the cross hotbar set the cross hotbar is showing is reachable; other sets (and the expanded hold bars, whose
/// hints aren't known to match their buttons) are reported as unreachable. Call on the framework thread only.
/// </summary>
public sealed unsafe class HotbarScanner
{
    private const int StandardBars = 10;
    private const int StandardSlots = 12;
    private const int CrossBars = 8;
    private const int CrossSlots = 16;

    /// <summary>What the last scan saw of the cross hotbar, for the debug window.</summary>
    public string CrossDiagnostics { get; private set; } = "-";

    public IReadOnlyList<HotbarObservation> Scan()
    {
        var result = new List<HotbarObservation>();
        CrossDiagnostics = "hotbar module not loaded";
        var module = RaptureHotbarModule.Instance();
        if (module == null) return result;

        var bars = module->Hotbars;
        for (var bar = 0; bar < StandardBars; bar++)
            Read(ref bars[bar], bar, StandardSlots, false, true, result);
        var active = ActiveCrossBar();
        for (var bar = 0; bar < CrossBars; bar++)
            Read(ref bars[StandardBars + bar], bar, CrossSlots, true, bar == active, result);

        var sample = active < 0 ? null : FirstFilled(ref bars[StandardBars + active]);
        CrossDiagnostics = $"set {(active < 0 ? "none" : (active + 1).ToString())}, "
            + $"{result.Count(o => o.IsCross && o.Reachable)} slots"
            + (sample is { } i
                ? $"; slot {i + 1} ({PadButton.ForSlot(i)}) hint [{Convert.ToHexString(HintBytes(ref bars[StandardBars + active].Slots[i]))}]"
                : "");
        return result;
    }

    /// <summary>
    /// The cross hotbar set (0-7) the cross hotbar addon is showing, or -1 when it isn't loaded, shows the pet bar
    /// or reports something else.
    /// </summary>
    private static int ActiveCrossBar()
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null) return -1;
        var addon = (AddonActionCross*)manager->GetAddonByName("_ActionCross");
        if (addon == null || !addon->IsReady || addon->DisplayPetBar) return -1;
        var bar = addon->RaptureHotbarId - StandardBars;
        return bar is >= 0 and < CrossBars ? bar : -1;
    }

    private static void Read(ref RaptureHotbarModule.Hotbar bar, int barIndex, int slotCount, bool isCross,
        bool reachable, List<HotbarObservation> into)
    {
        for (var i = 0; i < slotCount; i++)
        {
            ref var slot = ref bar.Slots[i];
            if (slot.IsEmpty) continue;
            // Type comes from what the slot shows (a macro can show an action); slots showing anything else are skipped.
            StepType type;
            uint apparent;
            switch (slot.ApparentSlotType)
            {
                case SlotType.Action: type = StepType.Action; apparent = slot.ApparentActionId; break;
                case SlotType.Item: type = StepType.Item; apparent = ItemIds.Normalize(slot.ApparentActionId); break;
                default:
                    if (slot.CommandType is not (SlotType.Action or SlotType.Item)) continue;
                    type = slot.CommandType == SlotType.Item ? StepType.Item : StepType.Action;
                    apparent = 0;
                    break;
            }
            // CommandId only counts when the command is the same kind as the type (not a macro number, say).
            var commandType = type == StepType.Item ? SlotType.Item : SlotType.Action;
            var command = slot.CommandType != commandType ? 0u
                : type == StepType.Item ? ItemIds.Normalize(slot.CommandId) : slot.CommandId;
            var hint = isCross ? (PadHint(ref slot) ?? PadButton.ForSlot(i))?.ToString() : Hint(ref slot);
            into.Add(new HotbarObservation(barIndex, i, isCross, type, command, apparent, hint, reachable));
        }
    }

    private static int? FirstFilled(ref RaptureHotbarModule.Hotbar bar)
    {
        for (var i = 0; i < CrossSlots; i++)
            if (!bar.Slots[i].IsEmpty) return i;
        return null;
    }

    private static byte[] HintBytes(ref RaptureHotbarModule.HotbarSlot slot)
    {
        var raw = slot.PopUpKeybindHint;
        var end = raw.IndexOf((byte)0);
        return (end < 0 ? raw : raw[..end]).ToArray();
    }

    /// <summary>The pop-up hint reads like " [Ctrl-3]" on a keyboard bar; the label is just "Ctrl-3".</summary>
    private static string? Hint(ref RaptureHotbarModule.HotbarSlot slot)
    {
        var text = string.Concat(SeString.Parse(HintBytes(ref slot)).Payloads.OfType<TextPayload>().Select(t => t.Text))
            .Trim().TrimStart('[').TrimEnd(']').Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// A cross hotbar slot's button from the game glyphs in its pop-up hint (a trigger and a D-pad or face button),
    /// or null when the hint doesn't name one; the slot's position gives it then (<see cref="PadButton.ForSlot"/>).
    /// </summary>
    private static PadButton? PadHint(ref RaptureHotbarModule.HotbarSlot slot)
    {
        string? trigger = null;
        (bool Face, PadDirection Direction)? button = null;
        foreach (var icon in SeString.Parse(HintBytes(ref slot)).Payloads.OfType<IconPayload>().Select(p => p.Icon))
        {
            switch (icon)
            {
                case BitmapFontIcon.ControllerTriggerLeft: trigger = "LT"; break;
                case BitmapFontIcon.ControllerTriggerRight: trigger = "RT"; break;
                case BitmapFontIcon.ControllerDPadLeft: button = (false, PadDirection.Left); break;
                case BitmapFontIcon.ControllerDPadUp: button = (false, PadDirection.Up); break;
                case BitmapFontIcon.ControllerDPadRight: button = (false, PadDirection.Right); break;
                case BitmapFontIcon.ControllerDPadDown: button = (false, PadDirection.Down); break;
                case BitmapFontIcon.ControllerButton3: button = (true, PadDirection.Left); break;  // X / square
                case BitmapFontIcon.ControllerButton0: button = (true, PadDirection.Up); break;    // Y / triangle
                case BitmapFontIcon.ControllerButton1: button = (true, PadDirection.Right); break; // B / circle
                case BitmapFontIcon.ControllerButton2: button = (true, PadDirection.Down); break;  // A / cross
            }
        }
        return trigger != null && button is { } b ? new PadButton(trigger, b.Face, b.Direction) : null;
    }
}
