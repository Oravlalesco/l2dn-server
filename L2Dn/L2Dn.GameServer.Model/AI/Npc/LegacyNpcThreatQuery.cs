using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class LegacyNpcThreatQuery: INpcThreatQuery
{
    public static LegacyNpcThreatQuery Instance { get; } = new();

    private LegacyNpcThreatQuery()
    {
    }

    public long GetHating(Attackable npc, Creature target) => npc.getHating(target);

    public Creature? GetMostHated(Attackable npc) => npc.getMostHated();

    public IEnumerable<AggroInfo> GetAggroEntries(Attackable npc) => npc.getAggroList().Values;
}
