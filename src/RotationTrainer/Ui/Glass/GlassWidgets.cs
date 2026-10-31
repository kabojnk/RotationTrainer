using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RotationTrainer.Ui.Glass;

/// <summary>Drawing helpers for the glass look. They use <see cref="GlassTheme.CurrentAccent"/> for highlights.</summary>
public static class GlassWidgets
{
    private const int ShadowRings = 12;

    private static float Scale => ImGuiHelpers.GlobalScale;

    private static uint U32(Vector4 color) => ImGui.GetColorU32(color);

    /// <summary>
    /// Draws the translucent window background (no blur) behind the current window's contents: a diagonal
    /// gradient, a 1 px border, a top highlight and a soft shadow outside the edges. Call first in Draw.
    /// <paramref name="opacity"/> is the fill's alpha (1 = opaque) and multiplies the other alphas.
    /// </summary>
    public static void DrawGlassBackground(float opacity)
    {
        var o = Math.Clamp(opacity, 0f, 1f);
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var rounding = GlassTheme.WindowRounding * Scale;

        // Shadow: rings outside the window only, so it never darkens the translucent glass.
        var reach = 24 * Scale;
        dl.PushClipRect(min - new Vector2(reach * 2), max + new Vector2(reach * 2), false);
        for (var i = 1; i <= ShadowRings; i++)
        {
            var t = i / (float)ShadowRings;
            var grow = reach * t;
            var offset = new Vector2(0, grow * 0.5f);
            var alpha = 0.5f * o * (1 - t) * (1 - t) / 3f;
            dl.AddRect(min - new Vector2(grow) + offset, max + new Vector2(grow) + offset, U32(new Vector4(0, 0, 0, alpha)),
                rounding + grow, ImDrawFlags.None, reach / ShadowRings + 1);
        }
        dl.PopClipRect();

        // The theme's gradient at about 160°.
        var start = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, U32(new Vector4(1, 1, 1, o)), rounding);
        var end = dl.VtxBuffer.Size;
        var height = max.Y - min.Y;
        ImGuiP.ShadeVertsLinearColorGradientKeepAlpha(dl, start, end, min, new Vector2(min.X + height * 0.36f, max.Y),
            U32(GlassTheme.Palette.GlassTop), U32(GlassTheme.Palette.GlassBottom));

        dl.AddRect(min, max, U32(GlassTheme.Gloss(0.18f * o)), rounding, ImDrawFlags.None, Scale);
        dl.AddLine(new Vector2(min.X + rounding, min.Y + Scale), new Vector2(max.X - rounding, min.Y + Scale),
            U32(GlassTheme.Gloss(0.28f * o)), Scale);
    }

    /// <summary>
    /// A tooltip in the theme's popup colours, whatever colours are pushed around it (the trail draws outside any
    /// glass window, and Sakura's dark text would otherwise sit on ImGui's dark default).
    /// </summary>
    public static void Tooltip(string text)
    {
        var p = GlassTheme.Palette;
        using var colors = ImRaii.PushColor(ImGuiCol.PopupBg, p.Popup)
            .Push(ImGuiCol.Text, p.Ink)
            .Push(ImGuiCol.Border, GlassTheme.Fg(0.18f));
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupRounding, 6 * Scale)
            .Push(ImGuiStyleVar.PopupBorderSize, Scale)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(8, 5) * Scale);
        ImGui.SetTooltip(text);
    }

    /// <summary>A full-width sidebar entry with a Font Awesome icon. Returns true when clicked.</summary>
    public static bool SidebarTab(string label, FontAwesomeIcon icon, bool selected)
    {
        var height = ImGui.GetFrameHeight() + 8 * Scale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, height);
        var pos = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(label, size);
        var hovered = ImGui.IsItemHovered();

        var dl = ImGui.GetWindowDrawList();
        var rounding = 10 * Scale;
        var accent = GlassTheme.CurrentAccent;
        if (selected)
        {
            dl.AddRectFilled(pos, pos + size, U32(GlassTheme.WithAlpha(accent, 0.16f)), rounding);
            dl.AddRect(pos, pos + size, U32(GlassTheme.WithAlpha(accent, 0.35f)), rounding, ImDrawFlags.None, Scale);
        }
        else if (hovered)
        {
            dl.AddRectFilled(pos, pos + size, U32(GlassTheme.Fg(0.06f)), rounding);
        }

        var textColor = U32(selected ? Vector4.One : hovered ? GlassTheme.Ink : GlassTheme.Dim);
        var iconSlot = 18 * Scale;
        var left = pos.X + 10 * Scale;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconText = icon.ToIconString();
            var iconSize = ImGui.CalcTextSize(iconText);
            dl.AddText(new Vector2(left + (iconSlot - iconSize.X) / 2, pos.Y + (height - iconSize.Y) / 2), textColor, iconText);
        }
        var labelSize = ImGui.CalcTextSize(label);
        dl.AddText(new Vector2(left + iconSlot + 10 * Scale, pos.Y + (height - labelSize.Y) / 2), textColor, label);
        return clicked;
    }

    /// <summary>A 1.4× title, a dim subtitle and a short accent bar.</summary>
    public static void SectionHeader(string title, string subtitle)
    {
        var dl = ImGui.GetWindowDrawList();
        var fontSize = ImGui.GetFontSize() * 1.4f;
        var pos = ImGui.GetCursorScreenPos();
        dl.AddText(ImGui.GetFont(), fontSize, pos, U32(GlassTheme.Ink), title);
        ImGui.Dummy(new Vector2(ImGui.CalcTextSize(title).X * 1.4f, fontSize));

        using (ImRaii.PushColor(ImGuiCol.Text, GlassTheme.Dim))
            ImGui.TextWrapped(subtitle);

        var barPos = ImGui.GetCursorScreenPos();
        var barSize = new Vector2(40, 3) * Scale;
        dl.AddRectFilled(barPos, barPos + barSize, U32(GlassTheme.CurrentAccent), 2 * Scale);
        ImGui.Dummy(barSize);
        ImGui.Spacing();
    }

    /// <summary>
    /// A rounded translucent panel that grows to fit its contents (measured each frame, applied the next).
    /// Dispose to end it. <paramref name="width"/> 0 fills the available width.
    /// </summary>
    public static IDisposable Card(string id, float width = 0)
    {
        var key = ImGui.GetID(id);
        var padding = new Vector2(14, 12) * Scale;
        var height = CardHeights.TryGetValue(key, out var h) ? h : ImGui.GetFrameHeightWithSpacing() * 2 + padding.Y * 2;
        using (ImRaii.PushColor(ImGuiCol.ChildBg, GlassTheme.Fg(0.05f)).Push(ImGuiCol.Border, GlassTheme.Fg(0.08f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, Scale).Push(ImGuiStyleVar.WindowPadding, padding))
        {
            ImGui.BeginChild(id, new Vector2(width, height), true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        }
        return new CardScope(key, padding.Y);
    }

    /// <summary>
    /// A row of options with the selected one filled in the accent colour. Returns true when the value changed.
    /// </summary>
    public static bool Segmented<T>(string id, ref T value, params (T Value, string Label)[] options)
    {
        using var idScope = ImRaii.PushId(id);
        var dl = ImGui.GetWindowDrawList();
        var inset = 3 * Scale;
        var pad = 12 * Scale;
        var height = ImGui.GetFrameHeight();
        var widths = options.Select(o => ImGui.CalcTextSize(o.Label).X + pad * 2).ToArray();
        var total = new Vector2(widths.Sum() + inset * 2, height + inset * 2);

        ImGui.BeginGroup();
        var start = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(start, start + total, U32(GlassTheme.Fg(0.06f)), GlassTheme.FrameRounding * Scale);

        var changed = false;
        var x = start.X + inset;
        for (var i = 0; i < options.Length; i++)
        {
            var (optionValue, label) = options[i];
            var pos = new Vector2(x, start.Y + inset);
            var size = new Vector2(widths[i], height);
            ImGui.SetCursorScreenPos(pos);
            if (ImGui.InvisibleButton($"##{i}", size) && !EqualityComparer<T>.Default.Equals(value, optionValue))
            {
                value = optionValue;
                changed = true;
            }
            var hovered = ImGui.IsItemHovered();
            var selected = EqualityComparer<T>.Default.Equals(value, optionValue);
            if (selected)
                dl.AddRectFilled(pos, pos + size, U32(GlassTheme.CurrentAccent), 7 * Scale);
            else if (hovered)
                dl.AddRectFilled(pos, pos + size, U32(GlassTheme.Fg(0.06f)), 7 * Scale);

            var textSize = ImGui.CalcTextSize(label);
            var color = selected ? GlassTheme.OnAccent : hovered ? GlassTheme.Ink : GlassTheme.Dim;
            dl.AddText(pos + (size - textSize) / 2, U32(color), label);
            x += widths[i];
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(total);
        ImGui.EndGroup();
        return changed;
    }

    /// <summary>A switch with a label and an optional dim help line below it. Returns true when toggled.</summary>
    public static bool Toggle(string id, string label, string? help, ref bool value)
    {
        using var idScope = ImRaii.PushId(id);
        var dl = ImGui.GetWindowDrawList();
        var track = new Vector2(36, 20) * Scale;
        var gap = 12 * Scale;
        var pos = ImGui.GetCursorScreenPos();
        var width = Math.Max(ImGui.GetContentRegionAvail().X, track.X + gap + 1);
        var textWidth = width - track.X - gap;
        var lineHeight = ImGui.GetTextLineHeight();
        var helpSize = help == null ? Vector2.Zero : ImGui.CalcTextSize(help, false, textWidth);
        var textHeight = lineHeight + (help == null ? 0 : 2 * Scale + helpSize.Y);
        var height = Math.Max(track.Y, textHeight);

        var changed = ImGui.InvisibleButton("##toggle", new Vector2(width, height));
        if (changed) value = !value;
        var hovered = ImGui.IsItemHovered();

        var trackPos = pos + new Vector2(0, Math.Max(0, (lineHeight - track.Y) / 2));
        var fill = value ? GlassTheme.CurrentAccent : GlassTheme.Fg(hovered ? 0.24f : 0.18f);
        dl.AddRectFilled(trackPos, trackPos + track, U32(fill), track.Y / 2);
        var knobRadius = track.Y / 2 - 2 * Scale;
        var knobX = value ? trackPos.X + track.X - track.Y / 2 : trackPos.X + track.Y / 2;
        dl.AddCircleFilled(new Vector2(knobX, trackPos.Y + track.Y / 2), knobRadius, U32(Vector4.One));

        var textX = pos.X + track.X + gap;
        var labelY = pos.Y + Math.Max(0, (track.Y - lineHeight) / 2);
        dl.AddText(new Vector2(textX, labelY), U32(GlassTheme.Ink), label);
        if (help != null)
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(textX, labelY + lineHeight + 2 * Scale),
                U32(GlassTheme.Mute), help, textWidth);
        return changed;
    }

    /// <summary>A small pill: tinted fill, coloured text. Stays on the current line for SameLine use.</summary>
    public static void Chip(string text, Vector4 color)
    {
        var pad = new Vector2(8, 2) * Scale;
        var textSize = ImGui.CalcTextSize(text);
        var size = textSize + pad * 2;
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + size, U32(GlassTheme.WithAlpha(color, 0.18f)), size.Y / 2);
        dl.AddText(pos + pad, U32(color), text);
        ImGui.Dummy(size);
    }

    /// <summary>A button filled in the accent colour, for the main action of a group.</summary>
    public static bool PrimaryButton(string label)
    {
        var accent = GlassTheme.CurrentAccent;
        using var colors = ImRaii.PushColor(ImGuiCol.Button, accent)
            .Push(ImGuiCol.ButtonHovered, Vector4.Lerp(accent, Vector4.One, 0.15f))
            .Push(ImGuiCol.ButtonActive, Vector4.Lerp(accent, Vector4.One, 0.3f))
            .Push(ImGuiCol.Text, GlassTheme.OnAccent);
        return ImGui.Button(label);
    }

    private static readonly Dictionary<uint, float> CardHeights = new();

    private sealed class CardScope(uint key, float paddingY) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CardHeights[key] = ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y + paddingY;
            ImGui.EndChild();
        }
    }
}
