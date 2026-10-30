using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using RotationTrainer.Core;
using GameActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace RotationTrainer.Game;

/// <summary>
/// Reports actions and items the local player successfully used, by hooking
/// <see cref="ActionEffectHandler.Receive"/> (the action-effect packet, sent only for uses that went off).
/// <see cref="ActionUsed"/> is raised on the thread the game calls the hook on; listeners that touch
/// plugin state should queue the event and handle it on <see cref="IFramework.Update"/>.
/// </summary>
public sealed unsafe class ActionListener : IDisposable
{
    private readonly IPlayerState _player;
    private readonly IPluginLog _log;
    private readonly Hook<ActionEffectHandler.Delegates.Receive> _hook;

    public event Action<StepType, uint>? ActionUsed;

    public ActionListener(IGameInteropProvider interop, IPlayerState player, IPluginLog log)
    {
        _player = player;
        _log = log;
        _hook = interop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
            ActionEffectHandler.Addresses.Receive.Value, Detour);
        _hook.Enable();
    }

    private void Detour(uint casterEntityId, Character* casterPtr, Vector3* targetPos,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds)
    {
        _hook.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);
        try
        {
            if (header == null || casterEntityId == 0 || casterEntityId != _player.EntityId) return;
            StepType? type = (GameActionType)header->ActionType switch
            {
                GameActionType.Action => StepType.Action,
                GameActionType.Item => StepType.Item,
                _ => null,
            };
            if (type != null) ActionUsed?.Invoke(type.Value, header->ActionId);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling an action effect.");
        }
    }

    public void Dispose() => _hook.Dispose();
}
