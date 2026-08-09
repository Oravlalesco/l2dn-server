using System.Collections.Immutable;

namespace L2Dn.NpcContracts;

public static class NpcPerceptionDeltaApplier
{
    public static NpcPerceptionApplyResult Apply(NpcPerceptionSnapshot current, NpcPerceptionDelta delta)
    {
        if (current.Envelope.SchemaVersion != NpcPerceptionSnapshot.CurrentSchemaVersion ||
            delta.Envelope.SchemaVersion != NpcPerceptionSnapshot.CurrentSchemaVersion)
            return new NpcPerceptionApplyResult(NpcPerceptionApplyStatus.SchemaMismatch, null);
        if (current.Envelope.Npc.ObjectId != delta.Envelope.Npc.ObjectId)
            return new NpcPerceptionApplyResult(NpcPerceptionApplyStatus.NpcMismatch, null);
        if (current.Envelope.Npc.Generation != delta.Envelope.Npc.Generation)
            return new NpcPerceptionApplyResult(NpcPerceptionApplyStatus.GenerationMismatch, null);
        if (current.Envelope.StateRevision != delta.Envelope.BaseRevision)
            return new NpcPerceptionApplyResult(NpcPerceptionApplyStatus.RevisionGap, null);
        const NpcPerceptionChangeMask allChanges = NpcPerceptionChangeMask.Identity |
            NpcPerceptionChangeMask.Physical | NpcPerceptionChangeMask.Combat |
            NpcPerceptionChangeMask.Environment | NpcPerceptionChangeMask.VisibleEntities |
            NpcPerceptionChangeMask.Threats | NpcPerceptionChangeMask.Affordances |
            NpcPerceptionChangeMask.SpatialObservations;
        if (delta.Envelope.Revision <= delta.Envelope.BaseRevision || delta.Changes == NpcPerceptionChangeMask.None ||
            (delta.Changes & ~allChanges) != 0 ||
            !HasValidSections(delta))
            return new NpcPerceptionApplyResult(NpcPerceptionApplyStatus.InvalidDelta, null);

        NpcPerceptionState state = current.State;
        ImmutableArray<VisibleEntity> visible = state.VisibleEntities;
        if (delta.Changes.HasFlag(NpcPerceptionChangeMask.VisibleEntities))
        {
            if (!TryApplyVisibleEntities(visible, delta.VisibleEntities!, out visible))
                return new NpcPerceptionApplyResult(NpcPerceptionApplyStatus.InvalidDelta, null);
        }

        NpcPerceptionState appliedState = new(
            delta.Changes.HasFlag(NpcPerceptionChangeMask.Identity) ? delta.Identity! : state.Identity,
            delta.Changes.HasFlag(NpcPerceptionChangeMask.Physical) ? delta.Physical! : state.Physical,
            delta.Changes.HasFlag(NpcPerceptionChangeMask.Combat) ? delta.Combat! : state.Combat,
            delta.Changes.HasFlag(NpcPerceptionChangeMask.Environment) ? delta.Environment! : state.Environment,
            visible,
            delta.Changes.HasFlag(NpcPerceptionChangeMask.Threats) ? delta.Threats : state.Threats,
            delta.Changes.HasFlag(NpcPerceptionChangeMask.Affordances) ? delta.Affordances : state.Affordances,
            delta.Changes.HasFlag(NpcPerceptionChangeMask.SpatialObservations)
                ? delta.SpatialObservations
                : state.SpatialObservations);

        NpcPerceptionSnapshot snapshot = new(
            new NpcPerceptionEnvelope(delta.Envelope.SchemaVersion, delta.Envelope.Npc, delta.Envelope.Revision,
                delta.Envelope.WorldTick, delta.Envelope.CaptureMonotonicMilliseconds),
            appliedState);
        return new NpcPerceptionApplyResult(NpcPerceptionApplyStatus.Applied, snapshot);
    }

    private static bool HasValidSections(NpcPerceptionDelta delta) =>
        (!delta.Changes.HasFlag(NpcPerceptionChangeMask.Identity) || delta.Identity != null) &&
        (!delta.Changes.HasFlag(NpcPerceptionChangeMask.Physical) || delta.Physical != null) &&
        (!delta.Changes.HasFlag(NpcPerceptionChangeMask.Combat) || delta.Combat != null) &&
        (!delta.Changes.HasFlag(NpcPerceptionChangeMask.Environment) || delta.Environment != null) &&
        (!delta.Changes.HasFlag(NpcPerceptionChangeMask.VisibleEntities) || delta.VisibleEntities != null);

    private static bool TryApplyVisibleEntities(ImmutableArray<VisibleEntity> current, VisibleEntityDelta delta,
        out ImmutableArray<VisibleEntity> result)
    {
        Dictionary<EntityKey, VisibleEntity> entities;
        try
        {
            entities = current.ToDictionary(static entity => entity.Entity);
        }
        catch (ArgumentException)
        {
            result = default;
            return false;
        }

        foreach (EntityKey removed in delta.Removed)
        {
            if (!entities.Remove(removed))
            {
                result = default;
                return false;
            }
        }

        foreach (VisibleEntity updated in delta.Updated)
        {
            if (!entities.ContainsKey(updated.Entity))
            {
                result = default;
                return false;
            }
            entities[updated.Entity] = updated;
        }

        foreach (VisibleEntity added in delta.Added)
        {
            if (!entities.TryAdd(added.Entity, added))
            {
                result = default;
                return false;
            }
        }

        VisibleEntity[] ordered = entities.Values.OrderBy(static entity => entity.ObservationOrdinal).ToArray();
        if (ordered.Where((entity, index) => entity.ObservationOrdinal != index).Any())
        {
            result = default;
            return false;
        }

        result = [.. ordered];
        return true;
    }
}
