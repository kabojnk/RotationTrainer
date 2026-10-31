namespace RotationTrainer.Core;

public enum PadDirection { Left, Up, Right, Down }

/// <summary>
/// A cross hotbar button: hold <see cref="Trigger"/> ("LT" or "RT") and press a direction on the D-pad or on the
/// face buttons (Up is Y/triangle, Right B/circle, Down A/cross, Left X/square). Written as a keybind label like
/// "LT D-Left" or "RT A-Up" so it travels with the keyboard labels, and drawn as a glyph on the trail.
/// </summary>
public readonly record struct PadButton(string Trigger, bool Face, PadDirection Direction)
{
    public const int SlotsPerSet = 16;

    /// <summary>
    /// The button of cross hotbar slot <paramref name="slot"/> (0-15), in the game's order: LT D-pad, LT face
    /// buttons, RT D-pad, RT face buttons, each Left, Up, Right, Down (the /crosshotbar slots LDL…RAD).
    /// </summary>
    public static PadButton? ForSlot(int slot) => slot is < 0 or >= SlotsPerSet
        ? null
        : new PadButton(slot < 8 ? "LT" : "RT", slot / 4 % 2 == 1, (PadDirection)(slot % 4));

    public override string ToString() => $"{Trigger} {(Face ? "A" : "D")}-{Direction}";

    public static bool TryParse(string? label, out PadButton button)
    {
        button = default;
        var parts = label?.Split(' ');
        if (parts is not { Length: 2 } || parts[0] is not ("LT" or "RT")) return false;
        var kind = parts[1].Split('-');
        if (kind.Length != 2 || kind[0] is not ("D" or "A") || !Enum.TryParse<PadDirection>(kind[1], out var direction)
            || !Enum.IsDefined(direction))
            return false;
        button = new PadButton(parts[0], kind[0] == "A", direction);
        return true;
    }
}
