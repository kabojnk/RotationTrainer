namespace RotationTrainer.Core;

public enum TrailStyle { Rail, Timeline, Focus }

public enum TrailSize { Big, Small }

public enum TrailRow { Gcd, Weave }

public sealed record TrailMetrics(float Gcd, float Weave, float PixelsPerSecond)
{
    public static TrailMetrics For(TrailSize size) => size == TrailSize.Big
        ? new TrailMetrics(56f, 40f, 72f)
        : new TrailMetrics(38f, 28f, 48f);
}

public readonly record struct TrailInput(int Index, bool IsGcd, double Expected);

public sealed record TrailItem(int Index, float CenterX, TrailRow Row, float Size, float Scale, bool IsCurrent);

/// <summary>Unscaled trail geometry; the caller multiplies by the UI scale. upcoming[0] is the current step.</summary>
public static class TrailLayout
{
    private const float WeaveGap = 8f;
    private const float WeaveToGcdGap = 4f;
    private const float RailGcdGap = 32f;
    private const float FocusGcdGap = 22f;
    private const float FocusCurrentScale = 1.3f;

    public static IReadOnlyList<TrailItem> Layout(TrailStyle style, TrailSize size,
        IReadOnlyList<TrailInput> upcoming, double referenceTime, float markerX)
    {
        var items = new List<TrailItem>(upcoming.Count);
        if (upcoming.Count == 0) return items;

        var m = TrailMetrics.For(size);
        float gcdGap = style == TrailStyle.Focus ? FocusGcdGap : RailGcdGap;
        float x = 0f;
        bool prevWeave = false;

        for (int i = 0; i < upcoming.Count; i++)
        {
            var u = upcoming[i];
            float s = u.IsGcd ? m.Gcd : m.Weave;
            float center;
            if (style == TrailStyle.Timeline)
            {
                center = markerX + (float)((u.Expected - referenceTime) * m.PixelsPerSecond);
            }
            else if (u.IsGcd)
            {
                if (prevWeave) x += WeaveToGcdGap;
                center = x + s / 2f;
                x += s + gcdGap;
                prevWeave = false;
            }
            else
            {
                center = x + s / 2f;
                x += s + WeaveGap;
                prevWeave = true;
            }
            bool current = i == 0;
            float scale = style == TrailStyle.Focus && current ? FocusCurrentScale : 1f;
            items.Add(new TrailItem(u.Index, center, u.IsGcd ? TrailRow.Gcd : TrailRow.Weave, s, scale, current));
        }

        if (style != TrailStyle.Timeline)
        {
            float shift = markerX - items[0].CenterX;
            for (int i = 0; i < items.Count; i++) items[i] = items[i] with { CenterX = items[i].CenterX + shift };
        }
        return items;
    }
}
