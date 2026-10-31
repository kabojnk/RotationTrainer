using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace RotationTrainer.Game;

/// <summary>
/// Resets enmity on the striking dummies fighting the player, as the dummy's own "Reset Enmity" menu option does,
/// so a restarted run starts out of combat. Only striking dummies on the player's enmity list are touched.
/// Call on the framework thread only.
/// </summary>
public sealed unsafe class DummyEnmity(IObjectTable objects, IPluginLog log)
{
    /// <summary>BNpcName row of "Striking Dummy".</summary>
    private const uint StrikingDummyNameId = 541;
    /// <summary>The game command behind the dummy's "Reset Enmity" option.</summary>
    private const int ResetEnmityCommand = 319;
    /// <summary>A new countdown and a rebuild can ask within moments of each other; one reset is enough.</summary>
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2);

    private DateTime _last = DateTime.MinValue;

    /// <summary>Resets every striking dummy with enmity on the player. Returns how many were reset.</summary>
    public int ResetAll()
    {
        var now = DateTime.UtcNow;
        if (now - _last < MinInterval) return 0;
        var state = UIState.Instance();
        if (state == null) return 0;

        var reset = 0;
        var haters = state->Hater;
        for (var i = 0; i < haters.HaterCount && i < haters.Haters.Length; i++)
        {
            var entityId = haters.Haters[i].EntityId;
            if (objects.SearchByEntityId(entityId) is not IBattleNpc { NameId: StrikingDummyNameId }) continue;
            GameMain.ExecuteCommand(ResetEnmityCommand, (int)entityId);
            reset++;
        }
        if (reset > 0)
        {
            _last = now;
            log.Debug("Reset enmity on {Count} striking dummies.", reset);
        }
        return reset;
    }
}
