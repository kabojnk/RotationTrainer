using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RotationTrainer.Ui.Glass;

/// <summary>Colors and style for the glass windows. Pixel sizes here are unscaled; multiply by GlobalScale.</summary>
public static class GlassTheme
{
    public const float WindowRounding = 16f, CardRounding = 12f, FrameRounding = 8f;

    public static readonly Vector4 Ink = Rgb(0xF4F6FB);
    public static readonly Vector4 Dim = Rgb(0xAAB2C5);
    public static readonly Vector4 Mute = Rgb(0x7C8499);
    /// <summary>Text on an accent fill.</summary>
    public static readonly Vector4 OnAccent = Rgb(0x0D1220);
    public static readonly Vector4 Warning = Rgb(0xFFB86B);
    public static readonly Vector4 Danger = Rgb(0xFF7A85);

    /// <summary>The accent of the innermost <see cref="PushWindow"/>; Sky outside one.</summary>
    public static Vector4 CurrentAccent { get; private set; } = AccentColor(Accent.Sky);

    public static Vector4 AccentColor(Accent a) => a switch
    {
        Accent.Violet => Rgb(0xC9A6FF),
        Accent.Mint => Rgb(0x7CF0B0),
        Accent.Gold => Rgb(0xFFD27A),
        _ => Rgb(0x8FB4FF),
    };

    public static Vector4 White(float alpha) => new(1, 1, 1, alpha);

    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };

    /// <summary>
    /// Pushes the style vars and colors for a glass window (call before Begin, dispose after End). The window
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
            .Push(ImGuiCol.Border, White(0.14f))
            .Push(ImGuiCol.BorderShadow, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, new Vector4(24 / 255f, 27 / 255f, 38 / 255f, 0.97f))
            .Push(ImGuiCol.ModalWindowDimBg, new Vector4(0, 0, 0, 0.45f))
            .Push(ImGuiCol.Text, Ink)
            .Push(ImGuiCol.TextDisabled, Mute)
            .Push(ImGuiCol.FrameBg, White(0.08f * o + 0.02f))
            .Push(ImGuiCol.FrameBgHovered, White(0.12f * o + 0.03f))
            .Push(ImGuiCol.FrameBgActive, White(0.16f * o + 0.04f))
            .Push(ImGuiCol.Button, White(0.08f * o + 0.02f))
            .Push(ImGuiCol.ButtonHovered, White(0.14f * o + 0.04f))
            .Push(ImGuiCol.ButtonActive, White(0.20f * o + 0.05f))
            .Push(ImGuiCol.Header, WithAlpha(acc, 0.22f * o + 0.05f))
            .Push(ImGuiCol.HeaderHovered, WithAlpha(acc, 0.30f * o + 0.06f))
            .Push(ImGuiCol.HeaderActive, WithAlpha(acc, 0.38f * o + 0.08f))
            .Push(ImGuiCol.CheckMark, acc)
            .Push(ImGuiCol.SliderGrab, acc)
            .Push(ImGuiCol.SliderGrabActive, Vector4.Lerp(acc, Vector4.One, 0.3f))
            .Push(ImGuiCol.Separator, White(0.14f))
            .Push(ImGuiCol.TableHeaderBg, Vector4.Zero)
            .Push(ImGuiCol.TableBorderLight, White(0.08f))
            .Push(ImGuiCol.TableBorderStrong, White(0.12f))
            .Push(ImGuiCol.TableRowBg, Vector4.Zero)
            .Push(ImGuiCol.TableRowBgAlt, White(0.02f))
            .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
            .Push(ImGuiCol.ScrollbarGrab, White(0.15f))
            .Push(ImGuiCol.ScrollbarGrabHovered, White(0.22f))
            .Push(ImGuiCol.ScrollbarGrabActive, White(0.30f))
            .Push(ImGuiCol.ResizeGrip, White(0.10f))
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
