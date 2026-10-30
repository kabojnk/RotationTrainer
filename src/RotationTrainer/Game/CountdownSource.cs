using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace RotationTrainer.Game;

/// <summary>
/// The in-game <c>/countdown</c>, read from memory once per frame (no hook, no signature).
/// Source: FFXIVClientStructs' <c>AgentCountDownSettingDialog</c>
/// (https://github.com/aers/FFXIVClientStructs, FFXIV/Client/UI/Agent/AgentCountDownSettingDialog.cs):
/// <c>Active</c> is set while a countdown runs (whoever started it) and <c>TimeRemaining</c> holds its seconds left.
/// Call <see cref="Update"/> on the framework thread only.
/// </summary>
public sealed unsafe class CountdownSource
{
    /// <summary>Seconds left on the running countdown, or null when none is running.</summary>
    public double? SecondsRemaining { get; private set; }

    public void Update()
    {
        var agent = AgentCountDownSettingDialog.Instance();
        SecondsRemaining = agent != null && agent->Active && agent->TimeRemaining > 0 ? agent->TimeRemaining : null;
    }
}
