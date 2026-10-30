namespace RotationTrainer.Core;

/// <summary>Values are persisted in the config: CompactRail was called Focus.</summary>
public enum TrailStyle { Rail = 0, Timeline = 1, CompactRail = 2 }

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
    private const float CompactGcdGap = 22f;
    private const float CompactCurrentScale = 1.3f;

    public static IReadOnlyList<TrailItem> Layout(TrailStyle style, TrailSize size,
        IReadOnlyList<TrailInput> upcoming, double referenceTime, float markerX)
    {
        if (upcoming.Count == 0) return new List<TrailItem>();
        if (style == TrailStyle.Timeline)
        {
            var m = TrailMetrics.For(size);
            return upcoming.Select((u, i) => new TrailItem(u.Index,
                markerX + (float)((u.Expected - referenceTime) * m.PixelsPerSecond),
                u.IsGcd ? TrailRow.Gcd : TrailRow.Weave, u.IsGcd ? m.Gcd : m.Weave, 1f, i == 0)).ToList();
        }

        var items = Place(style, size, upcoming, 0);
        float shift = markerX - items[0].CenterX;
        return items.Select(i => i with { CenterX = i.CenterX + shift }).ToList();
    }

    /// <summary>
    /// Auto on the rail styles: rail spacing, scrolled by the clock like an editing track. Each step crosses the
    /// marker at its expected time and the track moves at an even pace from one step to the next.
    /// <paramref name="steps"/> may start before the current step (already popped steps the clock has not
    /// reached yet); only items from <paramref name="currentIndex"/> on are returned.
    /// </summary>
    public static IReadOnlyList<TrailItem> Flow(TrailStyle style, TrailSize size, IReadOnlyList<TrailInput> steps,
        int currentIndex, double clock, float markerX)
    {
        if (steps.Count == 0) return new List<TrailItem>();
        var current = Math.Max(0, steps.ToList().FindIndex(s => s.Index == currentIndex));
        var items = Place(style, size, steps, current);

        var j = -1;
        while (j + 1 < steps.Count && steps[j + 1].Expected <= clock) j++;
        float scroll;
        if (j < 0) scroll = items[0].CenterX;
        else if (j == steps.Count - 1) scroll = items[j].CenterX;
        else
        {
            var span = steps[j + 1].Expected - steps[j].Expected;
            var t = span > 1e-6 ? (float)((clock - steps[j].Expected) / span) : 1f;
            scroll = items[j].CenterX + t * (items[j + 1].CenterX - items[j].CenterX);
        }

        return items.Skip(current).Select(i => i with { CenterX = markerX + i.CenterX - scroll }).ToList();
    }

    /// <summary>Rail spacing from x = 0; the step at <paramref name="current"/> is the current one.</summary>
    private static List<TrailItem> Place(TrailStyle style, TrailSize size, IReadOnlyList<TrailInput> upcoming, int current)
    {
        var m = TrailMetrics.For(size);
        float gcdGap = style == TrailStyle.CompactRail ? CompactGcdGap : RailGcdGap;
        var items = new List<TrailItem>(upcoming.Count);
        float x = 0f;
        bool prevWeave = false;

        for (int i = 0; i < upcoming.Count; i++)
        {
            var u = upcoming[i];
            float s = u.IsGcd ? m.Gcd : m.Weave;
            float center;
            if (u.IsGcd)
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
            bool isCurrent = i == current;
            float scale = style == TrailStyle.CompactRail && isCurrent ? CompactCurrentScale : 1f;
            items.Add(new TrailItem(u.Index, center, u.IsGcd ? TrailRow.Gcd : TrailRow.Weave, s, scale, isCurrent));
        }
        return items;
    }
}
