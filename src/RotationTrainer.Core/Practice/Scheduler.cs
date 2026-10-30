namespace RotationTrainer.Core;

public sealed class Scheduler
{
    private readonly ITimingSource _timing;
    private readonly double _lock;
    private double _nextGcdAt;
    private double _nextActionAt;
    private bool _first;
    private bool _pulled;

    public Scheduler(ITimingSource timing, double animationLockSeconds = 0.65)
    {
        _timing = timing;
        _lock = animationLockSeconds;
        Reset();
    }

    public void Reset()
    {
        _nextGcdAt = double.NegativeInfinity;
        _nextActionAt = double.NegativeInfinity;
        _first = true;
        _pulled = false;
    }

    /// <summary>Expected clock time of this step; call in sequence order.</summary>
    public double Next(PlannedStep step)
    {
        if (!step.IsPrepull && !_pulled)
        {
            _pulled = true;
            _nextActionAt = Math.Max(_nextActionAt, 0);
        }

        var a = step.Ability;
        var ready = a.IsGcd ? Math.Max(_nextGcdAt, _nextActionAt) : _nextActionAt;
        double t;
        if (step.At.HasValue) t = step.At.Value;
        else if (_first && step.IsPrepull) t = -_lock;
        else t = ready;
        _first = false;

        if (a.IsGcd)
        {
            _nextGcdAt = t + _timing.RecastSeconds(a);
            _nextActionAt = t + Math.Max(_timing.CastSeconds(a), _lock);
        }
        else
        {
            _nextActionAt = t + _lock;
        }
        return t;
    }
}
