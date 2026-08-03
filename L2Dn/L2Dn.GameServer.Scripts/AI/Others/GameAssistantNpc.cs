using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Npcs;
using L2Dn.GameServer.Model.GameAssistant;

namespace L2Dn.GameServer.Scripts.AI.Others;

public sealed class GameAssistantNpc: AbstractScript
{
    public const int NpcId = 32478;

    public GameAssistantNpc()
    {
        SubscribeToEvent<OnNpcFirstTalk>(OnFirstTalk, SubscriptionType.NpcTemplate, NpcId);
    }

    private static void OnFirstTalk(OnNpcFirstTalk ev)
    {
        GameAssistantUi.ShowMain(ev.getActiveChar(), ev.getNpc());
    }
}
