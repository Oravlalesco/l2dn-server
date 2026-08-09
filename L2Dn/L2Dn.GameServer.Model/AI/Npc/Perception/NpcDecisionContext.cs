using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class NpcDecisionContext
{
    public NpcDecisionContext(NpcPerceptionSnapshot perception, INpcGeoQuery geo)
    {
        Perception = perception;
        Geo = geo;
    }

    public NpcPerceptionSnapshot Perception { get; }

    // Transitional dependency for dynamic positions that cannot be precomputed in perception.
    public INpcGeoQuery Geo { get; }
}
