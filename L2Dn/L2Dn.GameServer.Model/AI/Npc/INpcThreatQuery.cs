using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model;

namespace L2Dn.GameServer.AI.Runtime;

internal interface INpcThreatQuery
{
    long GetHating(Attackable npc, Creature target);

    Creature? GetMostHated(Attackable npc);

    IEnumerable<AggroInfo> GetAggroEntries(Attackable npc);
}
