using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class LegacyNpcThreatQuery: INpcThreatQuery
{
    public static LegacyNpcThreatQuery Instance { get; } = new();

    private LegacyNpcThreatQuery()
    {
    }

    public long GetHating(Attackable npc, Creature target) =>
        NpcAiTelemetry.ObserveThreatQuery("get_hating", () => npc.getHating(target));

    public Creature? GetMostHated(Attackable npc) =>
        NpcAiTelemetry.ObserveThreatQuery("get_most_hated", npc.getMostHated);

    public IEnumerable<AggroInfo> GetAggroEntries(Attackable npc) =>
        NpcAiTelemetry.ObserveThreatQuery("get_aggro_entries", () => npc.getAggroList().Values);
}
