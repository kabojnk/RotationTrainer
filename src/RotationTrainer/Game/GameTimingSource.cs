using FFXIVClientStructs.FFXIV.Client.Game;
using RotationTrainer.Core;
using GameActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace RotationTrainer.Game;

/// <summary>
/// Cast and recast times as the game computes them for the current character (speed, haste traits).
/// Items use their base timings. Call on the framework thread only.
/// </summary>
public sealed unsafe class GameTimingSource : ITimingSource
{
    public double CastSeconds(Ability a) =>
        a.Type == StepType.Item ? a.BaseCastMs / 1000.0 : Seconds(ActionManager.GetAdjustedCastTime(GameActionType.Action, a.Id, false, null), a.BaseCastMs);

    public double RecastSeconds(Ability a) =>
        a.Type == StepType.Item ? a.BaseRecastMs / 1000.0 : Seconds(ActionManager.GetAdjustedRecastTime(GameActionType.Action, a.Id, true), a.BaseRecastMs);

    // A negative result means the game could not compute it (e.g. while loading); fall back to the base time.
    private static double Seconds(int ms, int baseMs) => (ms >= 0 ? ms : baseMs) / 1000.0;
}
