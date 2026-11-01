using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using RotationTrainer.Core;
using RotationTrainer.Game;
using RotationTrainer.Ui.Glass;
using RotationTrainer.Ui.Trail;

namespace RotationTrainer.Ui.Levelling;

/// <summary>Action icons for the levelling windows: the keybind in the corner, or "Lv N" greyed out when locked.</summary>
public sealed class CardChips(ITextureProvider textures, PracticeSession session)
{
    private const float GlyphSize = 10f;

    private static float Scale => ImGuiHelpers.GlobalScale;

    private static uint U32(Vector4 color) => ImGui.GetColorU32(color);

    /// <summary>The hotbar keybind, or null when the action isn't on a hotbar.</summary>
    public string? Keybind(Ability ability)
    {
        var label = session.KeybindLabel(ability.Type, ability.Id);
        return label == "?" ? null : label;
    }

    /// <summary>An icon at the cursor with its keybind (or "Lv N") on the bottom-right corner, and a name tooltip.</summary>
    public void Icon(Ability ability, float size, int level)
    {
        var s = Scale;
        var locked = ability.Level > level;
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(size * s);
        ImGui.Dummy(max - min);
        if (ImGui.IsItemHovered())
            GlassWidgets.Tooltip(locked ? $"{ability.Name} (level {ability.Level})" : ability.Name);

        var dl = ImGui.GetWindowDrawList();
        var rounding = 5 * s;
        var wrap = textures.GetFromGameIcon(new GameIconLookup(ability.IconId)).GetWrapOrEmpty();
        dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One,
            U32(new Vector4(1, 1, 1, locked ? 0.3f : 1f)), rounding);
        dl.AddRect(min, max, U32(GlassTheme.Fg(0.3f)), rounding, ImDrawFlags.None, s);

        var label = locked ? $"Lv {ability.Level}" : Keybind(ability);
        if (label != null) Badge(dl, max, label, locked);
    }

    /// <summary>A chip with the icon, the action's name and its keybind, for the guide's sequences.</summary>
    public void Chip(Ability ability, int level)
    {
        var s = Scale;
        var locked = ability.Level > level;
        var key = locked ? $"Lv {ability.Level}" : Keybind(ability);
        var icon = 24f;
        var nameSize = ImGui.CalcTextSize(ability.Name);
        var keySize = key == null ? Vector2.Zero : ImGui.CalcTextSize(key);
        var width = (3 + icon + 6) * s + nameSize.X + (key == null ? 0 : 6 * s + keySize.X) + 8 * s;
        var height = (icon + 6) * s;
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        ImGui.Dummy(max - min);
        if (locked && ImGui.IsItemHovered()) GlassWidgets.Tooltip($"Unlocks at level {ability.Level}.");

        var dl = ImGui.GetWindowDrawList();
        var alpha = locked ? 0.4f : 1f;
        dl.AddRectFilled(min, max, U32(GlassTheme.Fg(0.06f)), 6 * s);
        var iconMin = min + new Vector2(3 * s);
        var wrap = textures.GetFromGameIcon(new GameIconLookup(ability.IconId)).GetWrapOrEmpty();
        dl.AddImageRounded(wrap.Handle, iconMin, iconMin + new Vector2(icon * s), Vector2.Zero, Vector2.One,
            U32(new Vector4(1, 1, 1, locked ? 0.3f : 1f)), 4 * s);
        var x = iconMin.X + (icon + 6) * s;
        var textY = min.Y + (height - nameSize.Y) / 2;
        dl.AddText(new Vector2(x, textY), U32(GlassTheme.Fg(alpha)), ability.Name);
        if (key == null) return;
        x += nameSize.X + 6 * s;
        if (!locked && PadButton.TryParse(key, out var pad))
        {
            var text = pad.Set is { } set ? $"S{set} {pad.Trigger}" : pad.Trigger;
            dl.AddText(new Vector2(x, textY), U32(GlassTheme.CurrentAccent), text);
            TrailWindow.DrawPadGlyph(dl, new Vector2(x + ImGui.CalcTextSize(text).X + (3 + GlyphSize / 2) * s,
                min.Y + height / 2), GlyphSize * s, pad, 1f);
        }
        else
        {
            dl.AddText(new Vector2(x, textY), U32(locked ? GlassTheme.Mute : GlassTheme.CurrentAccent), key);
        }
    }

    /// <summary>Lays out items left to right, wrapping when the next one would not fit.</summary>
    public static void Flow<T>(IReadOnlyList<T> items, Func<T, float> width, Action<T> draw, string? separator = null)
    {
        var s = Scale;
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = 4 * s;
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine(0, spacing);
                var sepWidth = separator == null ? 0 : ImGui.CalcTextSize(separator).X + spacing;
                if (ImGui.GetCursorScreenPos().X + sepWidth + width(items[i]) > right)
                    ImGui.NewLine();
                else if (separator != null)
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextColored(GlassTheme.Mute, separator);
                    ImGui.SameLine(0, spacing);
                }
            }
            draw(items[i]);
        }
    }

    /// <summary>How wide <see cref="Chip"/> draws this ability.</summary>
    public float ChipWidth(Ability ability, int level)
    {
        var s = Scale;
        var key = ability.Level > level ? $"Lv {ability.Level}" : Keybind(ability);
        var keyWidth = key == null ? 0 : PadButton.TryParse(key, out var pad)
            ? ImGui.CalcTextSize(pad.Set is { } set ? $"S{set} {pad.Trigger}" : pad.Trigger).X + (3 + GlyphSize) * s
            : ImGui.CalcTextSize(key).X;
        return (3 + 24 + 6) * s + ImGui.CalcTextSize(ability.Name).X + (key == null ? 0 : 6 * s + keyWidth) + 8 * s;
    }

    private void Badge(ImDrawListPtr dl, Vector2 corner, string label, bool locked)
    {
        var s = Scale;
        var font = ImGui.GetFontSize() * 0.8f;
        var pad = PadButton.TryParse(label, out var button) ? button : (PadButton?)null;
        var text = pad is { } p ? (p.Set is { } set ? $"S{set} {p.Trigger}" : p.Trigger) : label;
        var textSize = ImGui.CalcTextSize(text) * 0.8f;
        var glyph = pad != null ? GlyphSize * s : 0;
        var size = new Vector2(textSize.X + (pad != null ? 2 * s + glyph : 0) + 6 * s, Math.Max(textSize.Y, glyph) + 2 * s);
        var min = corner - size + new Vector2(3 * s, 4 * s);
        dl.AddRectFilled(min, min + size, U32(GlassTheme.Palette.Chip), 3 * s);
        dl.AddText(ImGui.GetFont(), font, min + new Vector2(3 * s, (size.Y - textSize.Y) / 2),
            U32(locked ? GlassTheme.Mute : GlassTheme.CurrentAccent), text);
        if (pad is { } padButton)
            TrailWindow.DrawPadGlyph(dl, new Vector2(min.X + 3 * s + textSize.X + 2 * s + glyph / 2, min.Y + size.Y / 2),
                glyph, padButton, 1f);
    }
}
