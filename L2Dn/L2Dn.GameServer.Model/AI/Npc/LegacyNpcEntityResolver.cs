using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class LegacyNpcEntityResolver: ILegacyNpcEntityResolver
{
    public static LegacyNpcEntityResolver Instance { get; } = new();

    private LegacyNpcEntityResolver()
    {
    }

    public WorldObject? Resolve(Attackable observer, EntityKey key)
    {
        NpcAiTelemetry.RecordLegacyResolve();
        WorldObject? resolved = World.getInstance().findObject(key.ObjectId);
        if (resolved == null || resolved.getInstanceId() != observer.getInstanceId() ||
            GetEntityKind(resolved) != key.Kind)
        {
            return null;
        }

        if (key.Generation > 0 && (resolved is not Npc npc || npc.getSpawnGeneration() != key.Generation))
        {
            return null;
        }

        return resolved;
    }

    private static EntityKind GetEntityKind(WorldObject entity)
    {
        if (entity is Player) return EntityKind.Player;
        if (entity is Summon) return EntityKind.Summon;
        if (entity is Guard) return EntityKind.Guard;
        if (entity is Monster) return EntityKind.Monster;
        if (entity is Npc) return EntityKind.Npc;
        if (entity is Door) return EntityKind.Door;
        if (entity is Creature) return EntityKind.OtherCreature;
        return EntityKind.Unknown;
    }
}
