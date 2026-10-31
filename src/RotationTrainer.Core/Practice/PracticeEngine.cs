namespace RotationTrainer.Core;

public enum AdvanceMode { Manual, Auto }

/// <summary><see cref="EngineEventKind.OutOfOrder"/> follows the Popped of a neighbouring step used early.</summary>
public enum EngineEventKind { Popped, WrongPress, Paused, Resumed, Finished, OutOfOrder }

public sealed record EngineEvent(EngineEventKind Kind, int Index);

/// <param name="Skipped">The player skipped the step instead of using it.</param>
/// <param name="OutOfOrder">Used before the step it follows (see <see cref="PracticeEngine"/>).</param>
public sealed record StepResult(int Index, uint AbilityId, double Expected, double Actual, int WrongPresses,
    bool Skipped = false, bool OutOfOrder = false);

/// <summary>
/// Follows the player through a <see cref="PracticeSequence"/>. With neighbours allowed, a step may be used early
/// when it is a neighbour of the current one: another weave between the same two GCDs, or, on a GCD, the next GCD
/// (weaves between them aside), never across the pull. It pops where it is and is passed over once reached.
/// </summary>
public sealed class PracticeEngine
{
    /// <summary>How far ahead neighbours are looked for (a loop of weaves alone never ends).</summary>
    private const int NeighbourReach = 16;

    private readonly PracticeSequence _sequence;
    private readonly Scheduler _scheduler;
    private readonly AdvanceMode _mode;
    private readonly Func<StepType, uint, bool> _isRelevant;
    private readonly Func<uint, uint, bool> _sameAction;
    private readonly Func<uint, uint, bool> _sameItem;
    private readonly double _latency;
    private readonly bool _allowNeighbours;
    private readonly HashSet<int> _doneAhead = new();
    /// <summary>Per step: when it should start, and how long its cast takes.</summary>
    private readonly List<(double Expected, double Cast)> _timing = new();
    private readonly List<StepResult> _runLog = new();
    private int _wrongPresses;

    /// <param name="sameAction">Whether a used action id counts as a step's action id (button variants);
    /// plain equality when null.</param>
    /// <param name="latencySeconds">How late a hard cast's effect may arrive after its cast ends before Auto pauses.</param>
    /// <param name="sameItem">Whether a used item id counts as a step's item id (both HQ-normalised); plain
    /// equality when null. Lets any potion count for a potion step.</param>
    /// <param name="allowNeighbours">Whether a neighbouring step may be used before the current one.</param>
    public PracticeEngine(PracticeSequence sequence, Scheduler scheduler, AdvanceMode mode,
        Func<StepType, uint, bool> isRelevant, Func<uint, uint, bool>? sameAction = null, double latencySeconds = 0,
        Func<uint, uint, bool>? sameItem = null, bool allowNeighbours = false)
    {
        _allowNeighbours = allowNeighbours;
        _sequence = sequence;
        _scheduler = scheduler;
        _mode = mode;
        _isRelevant = isRelevant;
        _sameAction = sameAction ?? ((a, b) => a == b);
        _sameItem = sameItem ?? ((a, b) => a == b);
        _latency = latencySeconds;
        IsFinished = _sequence[0] == null;
    }

    public int CurrentIndex { get; private set; }
    public double? Clock { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsFinished { get; private set; }
    public IReadOnlyList<StepResult> RunLog => _runLog;

    /// <summary>Whether a step after the current one was already used out of order.</summary>
    public bool IsDone(int index) => _doneAhead.Contains(index);
    public event Action<EngineEvent>? Event;

    /// <summary>When the step should be started (pressed).</summary>
    public double ExpectedTime(int index) => Timing(index).Expected;

    /// <summary>
    /// When the step's use is reported: its effect arrives when the cast ends, so a hard cast is due at
    /// expected + cast + latency allowance. An instant is due at its expected time.
    /// </summary>
    public double DueTime(int index)
    {
        var (expected, cast) = Timing(index);
        return cast > 0 ? expected + cast + _latency : expected;
    }

    private (double Expected, double Cast) Timing(int index)
    {
        if (_sequence[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
        while (_timing.Count <= index)
        {
            var step = _sequence[_timing.Count]!;
            _timing.Add((_scheduler.Next(step), _scheduler.CastSeconds(step.Ability)));
        }
        return _timing[index];
    }

    public void OnActionUsed(StepType type, uint id)
    {
        if (IsFinished) return;
        var step = _sequence[CurrentIndex];
        if (step == null) return;

        if (!IsStep(CurrentIndex, type, id))
        {
            if (_allowNeighbours && Neighbours().FirstOrDefault(i => IsStep(i, type, id), -1) is var early and >= 0)
            {
                UseEarly(early);
                return;
            }
            if (_isRelevant(type, id))
            {
                _wrongPresses++;
                Raise(EngineEventKind.WrongPress, CurrentIndex);
            }
            return;
        }
        Advance(step, skipped: false);
    }

    /// <summary>Moves past the current step as if it were used, e.g. for an ability that is not on a hotbar.</summary>
    public void Skip()
    {
        if (IsFinished || _sequence[CurrentIndex] is not { } step) return;
        Advance(step, skipped: true);
    }

    /// <summary>Steps that may be used before the current one (see the class summary), not yet done.</summary>
    private IEnumerable<int> Neighbours()
    {
        var current = _sequence[CurrentIndex]!;
        for (var i = CurrentIndex + 1; i <= CurrentIndex + NeighbourReach && _sequence[i] is { } step; i++)
        {
            if (step.IsPrepull != current.IsPrepull) yield break;
            if (current.Ability.IsGcd)
            {
                if (!step.Ability.IsGcd) continue;
                if (!_doneAhead.Contains(i)) yield return i;
                yield break;
            }
            if (step.Ability.IsGcd) yield break;
            if (!_doneAhead.Contains(i)) yield return i;
        }
    }

    private void UseEarly(int index)
    {
        var (expected, cast) = Timing(index);
        Clock ??= expected + cast;
        _runLog.Add(new StepResult(index, _sequence[index]!.Ability.Id, expected, Clock.Value, _wrongPresses,
            OutOfOrder: true));
        _wrongPresses = 0;
        _doneAhead.Add(index);
        Raise(EngineEventKind.Popped, index);
        Raise(EngineEventKind.OutOfOrder, index);
    }

    private void Advance(PlannedStep step, bool skipped)
    {
        var (expected, cast) = Timing(CurrentIndex);
        // The first use starts the clock at the moment it was reported: when its cast ended.
        // A use never moves a running clock.
        Clock ??= expected + cast;
        var index = CurrentIndex;
        _runLog.Add(new StepResult(index, step.Ability.Id, expected, Clock.Value, _wrongPresses, skipped));
        _wrongPresses = 0;
        CurrentIndex++;
        while (_doneAhead.Remove(CurrentIndex)) CurrentIndex++;
        Raise(EngineEventKind.Popped, index);
        if (IsPaused)
        {
            IsPaused = false;
            Raise(EngineEventKind.Resumed, index);
        }
        if (_sequence[CurrentIndex] == null)
        {
            IsFinished = true;
            Raise(EngineEventKind.Finished, index);
        }
    }

    /// <summary>Whether using this action or item would count as step <paramref name="index"/> (button variants included).</summary>
    public bool IsStep(int index, StepType type, uint id)
    {
        var step = _sequence[index];
        if (step == null || step.Ability.Type != type) return false;
        if (type == StepType.Item) return _sameItem(ItemIds.Normalize(id), ItemIds.Normalize(step.Ability.Id));
        return _sameAction(id, step.Ability.Id);
    }

    public void OnCountdown(double secondsRemaining)
    {
        ClearRun();
        Clock = -secondsRemaining;
    }

    public void OnCountdownCancelled() => Reset();

    public void Tick(double deltaSeconds)
    {
        if (Clock == null || IsFinished || IsPaused) return;
        var target = Clock.Value + deltaSeconds;
        if (_mode == AdvanceMode.Auto)
        {
            var due = DueTime(CurrentIndex);
            if (target >= due)
            {
                Clock = Math.Max(Clock.Value, due);
                IsPaused = true;
                Raise(EngineEventKind.Paused, CurrentIndex);
                return;
            }
        }
        Clock = target;
    }

    public void Reset()
    {
        ClearRun();
        Clock = null;
    }

    private void ClearRun()
    {
        CurrentIndex = 0;
        IsPaused = false;
        IsFinished = _sequence[0] == null;
        _wrongPresses = 0;
        _doneAhead.Clear();
        _runLog.Clear();
        _timing.Clear();
        _scheduler.Reset();
    }

    private void Raise(EngineEventKind kind, int index) => Event?.Invoke(new EngineEvent(kind, index));
}
