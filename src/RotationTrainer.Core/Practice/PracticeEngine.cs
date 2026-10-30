namespace RotationTrainer.Core;

public enum AdvanceMode { Manual, Auto }

public enum EngineEventKind { Popped, WrongPress, Paused, Resumed, Finished }

public sealed record EngineEvent(EngineEventKind Kind, int Index);

public sealed record StepResult(int Index, uint AbilityId, double Expected, double Actual, int WrongPresses);

public sealed class PracticeEngine
{
    private readonly PracticeSequence _sequence;
    private readonly Scheduler _scheduler;
    private readonly AdvanceMode _mode;
    private readonly Func<StepType, uint, bool> _isRelevant;
    private readonly List<double> _expected = new();
    private readonly List<StepResult> _runLog = new();
    private int _wrongPresses;

    public PracticeEngine(PracticeSequence sequence, Scheduler scheduler, AdvanceMode mode,
        Func<StepType, uint, bool> isRelevant)
    {
        _sequence = sequence;
        _scheduler = scheduler;
        _mode = mode;
        _isRelevant = isRelevant;
        IsFinished = _sequence[0] == null;
    }

    public int CurrentIndex { get; private set; }
    public double? Clock { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsFinished { get; private set; }
    public IReadOnlyList<StepResult> RunLog => _runLog;
    public event Action<EngineEvent>? Event;

    public double ExpectedTime(int index)
    {
        if (_sequence[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
        while (_expected.Count <= index)
            _expected.Add(_scheduler.Next(_sequence[_expected.Count]!));
        return _expected[index];
    }

    public void OnActionUsed(StepType type, uint id)
    {
        if (IsFinished) return;
        var step = _sequence[CurrentIndex];
        if (step == null) return;

        if (type == StepType.Item) id = ItemIds.Normalize(id);
        var stepId = step.Ability.Type == StepType.Item ? ItemIds.Normalize(step.Ability.Id) : step.Ability.Id;
        if (step.Ability.Type != type || stepId != id)
        {
            if (_isRelevant(type, id))
            {
                _wrongPresses++;
                Raise(EngineEventKind.WrongPress, CurrentIndex);
            }
            return;
        }

        var expected = ExpectedTime(CurrentIndex);
        Clock ??= expected;
        var index = CurrentIndex;
        _runLog.Add(new StepResult(index, step.Ability.Id, expected, Clock.Value, _wrongPresses));
        _wrongPresses = 0;
        CurrentIndex++;
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
            var expected = ExpectedTime(CurrentIndex);
            if (target >= expected)
            {
                Clock = Math.Max(Clock.Value, expected);
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
        _runLog.Clear();
        _expected.Clear();
        _scheduler.Reset();
    }

    private void Raise(EngineEventKind kind, int index) => Event?.Invoke(new EngineEvent(kind, index));
}
