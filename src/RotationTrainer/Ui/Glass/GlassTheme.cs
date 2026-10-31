using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RotationTrainer.Ui.Glass;

/// <summary>
/// One look for the glass: the glass gradient, the foreground that text and thin lines are drawn in, the text
/// shades, and the fills that sit on the glass. <paramref name="Tint"/> scales how far the trail's glass leans towards
/// the accent.
/// </summary>
public sealed record GlassPalette(Vector4 GlassTop, Vector4 GlassBottom, Vector4 Foreground, Vector4 Ink, Vector4 Dim,
    Vector4 Mute, Vector4 OnAccent, Vector4 Warning, Vector4 Danger, Vector4 Chip, Vector4 Popup, float Tint);

/// <summary>Colours and style for the glass windows. Pixel sizes here are unscaled; multiply by GlobalScale.</summary>
public static class GlassTheme
{
    public const float WindowRounding = 16f, CardRounding = 12f, FrameRounding = 8f;

    public static GlassPalette PaletteFor(Theme theme) => theme switch
    {
        Theme.Graphite => new GlassPalette(Rgb(0x3A3A3F), Rgb(0x1B1B1E), Vector4.One, Rgb(0xF2F2F3), Rgb(0xB6B6BB),
            Rgb(0x86868C), Rgb(0x121214), Rgb(0xFFB86B), Rgb(0xFF7A85), new Vector4(0.05f, 0.05f, 0.06f, 0.75f),
            new Vector4(0.13f, 0.13f, 0.15f, 0.97f), 0.45f),
        Theme.Sakura => new GlassPalette(Rgb(0xFFD9EF), Rgb(0xF7B4DC), Rgb(0x5C2149), Rgb(0x4E1A3E), Rgb(0x7E4569),
            Rgb(0xA6718F), Rgb(0x4E1A3E), Rgb(0xC2590F), Rgb(0xD62E55), new Vector4(1, 1, 1, 0.82f),
            new Vector4(1f, 0.89f, 0.95f, 0.98f), 0.5f),
        _ => new GlassPalette(Rgb(0x282E40), Rgb(0x12141E), Vector4.One, Rgb(0xF4F6FB), Rgb(0xAAB2C5), Rgb(0x7C8499),
            Rgb(0x0D1220), Rgb(0xFFB86B), Rgb(0xFF7A85), new Vector4(10 / 255f, 12 / 255f, 20 / 255f, 0.7f),
            new Vector4(24 / 255f, 27 / 255f, 38 / 255f, 0.97f), 1f),
    };

    /// <summary>The theme every window draws with this frame; set from the configuration before drawing.</summary>
    public static GlassPalette Palette { get; private set; } = PaletteFor(Theme.Midnight);

    public static void Use(Theme theme) => Palette = PaletteFor(theme);

    public static Vector4 Ink => Palette.Ink;
    public static Vector4 Dim => Palette.Dim;
    public static Vector4 Mute => Palette.Mute;
    /// <summary>Text on an accent fill.</summary>
    public static Vector4 OnAccent => Palette.OnAccent;
    public static Vector4 Warning => Palette.Warning;
    public static Vector4 Danger => Palette.Danger;

    /// <summary>The accent of the innermost <see cref="PushWindow"/>; Sky outside one.</summary>
    public static Vector4 CurrentAccent { get; private set; } = AccentColor(Accent.Sky);

    public static Vector4 AccentColor(Accent a) => a switch
    {
        Accent.Violet => Rgb(0xC9A6FF),
        Accent.Mint => Rgb(0x7CF0B0),
        Accent.Gold => Rgb(0xFFD27A),
        Accent.Sakura => Rgb(0xFF9CC2),
        _ => Rgb(0x8FB4FF),
    };

    /// <summary>The theme's foreground (white on dark glass, plum on Sakura) at <paramref name="alpha"/>: for text,
    /// thin lines and the fills that lighten or darken the glass.</summary>
    public static Vector4 Fg(float alpha) => Palette.Foreground with { W = alpha };

    /// <summary>Pure white at <paramref name="alpha"/>, for glossy highlights that stay white on every theme.</summary>
    public static Vector4 Gloss(float alpha) => new(1, 1, 1, alpha);

    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };

    /// <summary>
    /// Pushes the style vars and colours for a glass window (call before Begin, dispose after End). The window
    /// background itself is transparent: draw it with <see cref="GlassWidgets.DrawGlassBackground"/>.
    /// <paramref name="opacity"/> multiplies the alpha of the frame, button and header fills.
    /// </summary>
    public static IDisposable PushWindow(float opacity, Vector4? accent = null)
    {
        var acc = accent ?? CurrentAccent;
        var o = Math.Clamp(opacity, 0f, 1f);
        var scale = ImGuiHelpers.GlobalScale;

        var colors = ImRaii.PushColor(ImGuiCol.WindowBg, Vector4.Zero)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.Border, Fg(0.14f))
            .Push(ImGuiCol.BorderShadow, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, Palette.Popup)
            .Push(ImGuiCol.ModalWindowDimBg, new Vector4(0, 0, 0, 0.45f))
            .Push(ImGuiCol.Text, Ink)
            .Push(ImGuiCol.TextDisabled, Mute)
            .Push(ImGuiCol.FrameBg, Fg(0.08f * o + 0.02f))
            .Push(ImGuiCol.FrameBgHovered, Fg(0.12f * o + 0.03f))
            .Push(ImGuiCol.FrameBgActive, Fg(0.16f * o + 0.04f))
            .Push(ImGuiCol.Button, Fg(0.08f * o + 0.02f))
            .Push(ImGuiCol.ButtonHovered, Fg(0.14f * o + 0.04f))
            .Push(ImGuiCol.ButtonActive, Fg(0.20f * o + 0.05f))
            .Push(ImGuiCol.Header, WithAlpha(acc, 0.22f * o + 0.05f))
            .Push(ImGuiCol.HeaderHovered, WithAlpha(acc, 0.30f * o + 0.06f))
            .Push(ImGuiCol.HeaderActive, WithAlpha(acc, 0.38f * o + 0.08f))
            .Push(ImGuiCol.CheckMark, acc)
            .Push(ImGuiCol.SliderGrab, acc)
            .Push(ImGuiCol.SliderGrabActive, Vector4.Lerp(acc, Vector4.One, 0.3f))
            .Push(ImGuiCol.Separator, Fg(0.14f))
            .Push(ImGuiCol.TableHeaderBg, Vector4.Zero)
            .Push(ImGuiCol.TableBorderLight, Fg(0.08f))
            .Push(ImGuiCol.TableBorderStrong, Fg(0.12f))
            .Push(ImGuiCol.TableRowBg, Vector4.Zero)
            .Push(ImGuiCol.TableRowBgAlt, Fg(0.02f))
            .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
            .Push(ImGuiCol.ScrollbarGrab, Fg(0.15f))
            .Push(ImGuiCol.ScrollbarGrabHovered, Fg(0.22f))
            .Push(ImGuiCol.ScrollbarGrabActive, Fg(0.30f))
            .Push(ImGuiCol.ResizeGrip, Fg(0.10f))
            .Push(ImGuiCol.ResizeGripHovered, WithAlpha(acc, 0.5f))
            .Push(ImGuiCol.ResizeGripActive, acc);

        var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, WindowRounding * scale)
            .Push(ImGuiStyleVar.WindowBorderSize, 0f)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(14, 12) * scale)
            .Push(ImGuiStyleVar.ChildRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.ChildBorderSize, 0f)
            .Push(ImGuiStyleVar.PopupRounding, CardRounding * scale)
            .Push(ImGuiStyleVar.FrameRounding, FrameRounding * scale)
            .Push(ImGuiStyleVar.GrabRounding, FrameRounding * scale)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(10, 5) * scale)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(10, 8) * scale)
            .Push(ImGuiStyleVar.ScrollbarRounding, FrameRounding * scale);

        var previous = CurrentAccent;
        CurrentAccent = acc;
        return new Scope(colors, styles, previous);
    }

    private static Vector4 Rgb(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    private sealed class Scope(IDisposable colors, IDisposable styles, Vector4 previousAccent) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            styles.Dispose();
            colors.Dispose();
            CurrentAccent = previousAccent;
        }
    }
}
