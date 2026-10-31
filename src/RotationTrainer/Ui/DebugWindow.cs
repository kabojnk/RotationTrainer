using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using RotationTrainer.Core;
using RotationTrainer.Game;

namespace RotationTrainer.Ui;

/// <summary>Plain diagnostics for the game inputs and the practice engine (<c>/rtrain debug</c>).</summary>
public sealed class DebugWindow : Window, IDisposable
{
    private sealed record Use(StepType Type, uint Id);

    private readonly PracticeSession _session;
    private readonly ActionListener _listener;
    private readonly CountdownSource _countdown;
    private Use? _lastUse;

    public DebugWindow(PracticeSession session, ActionListener listener, CountdownSource countdown) : base("Rotation Trainer Debug")
    {
        _session = session;
        _listener = listener;
        _countdown = countdown;
        Size = new Vector2(360, 260);
        SizeCondition = ImGuiCond.FirstUseEver;
        _listener.ActionUsed += OnActionUsed;
    }

    // May run on the game's thread; a reference assignment is atomic.
    private void OnActionUsed(StepType type, uint id) => _lastUse = new Use(type, id);

    public override void Draw()
    {
        var last = _lastUse;
        ImGui.TextUnformatted($"Job: {_session.Job ?? "-"}   Input: {_session.InputMode}");
        ImGui.TextUnformatted($"Last action: {(last == null ? "-" : $"{last.Type} {last.Id}")}");
        ImGui.TextWrapped($"Cross hotbar: {_session.CrossHotbarDiagnostics}");
        ImGui.TextUnformatted($"Countdown: {(_countdown.SecondsRemaining is { } s ? $"{s:0.00} s" : "-")}");
        ImGui.Separator();

        var engine = _session.Engine;
        if (engine == null)
        {
            ImGui.TextUnformatted("No engine (no job, or no opener/loop selected).");
        }
        else
        {
            var step = _session.Sequence[engine.CurrentIndex];
            ImGui.TextUnformatted($"Index: {engine.CurrentIndex}");
            ImGui.TextUnformatted($"Clock: {(engine.Clock is { } c ? $"{c:0.00} s" : "-")}");
            ImGui.TextUnformatted($"Paused: {engine.IsPaused}   Finished: {engine.IsFinished}");
            ImGui.TextUnformatted($"Step: {step?.Ability.Name ?? "-"}");
            ImGui.TextUnformatted(
                $"Keybind: {(step == null ? "-" : _session.KeybindLabel(step.Ability.Type, step.Ability.Id))}");
        }

        ImGui.Separator();
        var gcd = _session.ReferenceGcd;
        ImGui.TextUnformatted(gcd == null
            ? "GCD: -"
            : $"GCD: {_session.Timing.RecastSeconds(gcd):0.00} s ({gcd.Name}{(_session.Timing is GameTimingSource ? "" : ", custom speed")})");
    }

    public void Dispose() => _listener.ActionUsed -= OnActionUsed;
}
