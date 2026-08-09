using System.Collections.Immutable;

namespace L2Dn.NpcContracts;

public static class NpcPerceptionDiff
{
    public static NpcPerceptionDelta Create(NpcPerceptionSnapshot from, NpcPerceptionSnapshot to)
    {
        if (from.Envelope.Npc != to.Envelope.Npc)
            throw new ArgumentException("Snapshots must describe the same NPC incarnation.", nameof(to));
        if (to.Envelope.StateRevision <= from.Envelope.StateRevision)
            throw new ArgumentException("Target revision must be greater than base revision.", nameof(to));

        NpcPerceptionChangeMask changes = NpcPerceptionChangeMask.None;
        NpcIdentity? identity = null;
        NpcPhysicalState? physical = null;
        NpcCombatFacts? combat = null;
        NpcEnvironment? environment = null;
        VisibleEntityDelta? visibleEntities = null;
        ImmutableArray<ThreatEntry> threats = [];
        ImmutableArray<NpcAffordanceObservation> affordances = [];
        ImmutableArray<SpatialObservation> spatial = [];
        ImmutableArray<NpcSkillObservation> skills = [];

        if (!IdentityEquals(from.State.Identity, to.State.Identity))
        {
            changes |= NpcPerceptionChangeMask.Identity;
            identity = to.State.Identity;
        }

        if (from.State.Physical != to.State.Physical)
        {
            changes |= NpcPerceptionChangeMask.Physical;
            physical = to.State.Physical;
        }

        if (from.State.Combat != to.State.Combat)
        {
            changes |= NpcPerceptionChangeMask.Combat;
            combat = to.State.Combat;
        }

        if (from.State.Environment != to.State.Environment)
        {
            changes |= NpcPerceptionChangeMask.Environment;
            environment = to.State.Environment;
        }

        if (!from.State.VisibleEntities.SequenceEqual(to.State.VisibleEntities))
        {
            changes |= NpcPerceptionChangeMask.VisibleEntities;
            visibleEntities = DiffVisibleEntities(from.State.VisibleEntities, to.State.VisibleEntities);
        }

        if (!from.State.Threats.SequenceEqual(to.State.Threats))
        {
            changes |= NpcPerceptionChangeMask.Threats;
            threats = to.State.Threats;
        }

        if (!from.State.Affordances.SequenceEqual(to.State.Affordances))
        {
            changes |= NpcPerceptionChangeMask.Affordances;
            affordances = to.State.Affordances;
        }

        if (!from.State.SpatialObservations.SequenceEqual(to.State.SpatialObservations))
        {
            changes |= NpcPerceptionChangeMask.SpatialObservations;
            spatial = to.State.SpatialObservations;
        }

        if (!from.State.Skills.SequenceEqual(to.State.Skills))
        {
            changes |= NpcPerceptionChangeMask.Skills;
            skills = to.State.Skills;
        }

        return new NpcPerceptionDelta(
            new NpcPerceptionDeltaEnvelope(NpcPerceptionSnapshot.CurrentSchemaVersion, to.Envelope.Npc,
                from.Envelope.StateRevision, to.Envelope.StateRevision, to.Envelope.WorldTick,
                to.Envelope.CaptureMonotonicMilliseconds),
            changes,
            identity,
            physical,
            combat,
            environment,
            visibleEntities,
            threats,
            affordances,
            spatial,
            skills);
    }

    private static VisibleEntityDelta DiffVisibleEntities(ImmutableArray<VisibleEntity> from,
        ImmutableArray<VisibleEntity> to)
    {
        Dictionary<EntityKey, VisibleEntity> oldByKey = from.ToDictionary(static entity => entity.Entity);
        Dictionary<EntityKey, VisibleEntity> newByKey = to.ToDictionary(static entity => entity.Entity);
        ImmutableArray<VisibleEntity>.Builder added = ImmutableArray.CreateBuilder<VisibleEntity>();
        ImmutableArray<VisibleEntity>.Builder updated = ImmutableArray.CreateBuilder<VisibleEntity>();
        ImmutableArray<EntityKey>.Builder removed = ImmutableArray.CreateBuilder<EntityKey>();

        foreach (VisibleEntity entity in to)
        {
            if (!oldByKey.TryGetValue(entity.Entity, out VisibleEntity old))
                added.Add(entity);
            else if (old != entity)
                updated.Add(entity);
        }

        foreach (VisibleEntity entity in from)
        {
            if (!newByKey.ContainsKey(entity.Entity))
                removed.Add(entity.Entity);
        }

        return new VisibleEntityDelta(added.ToImmutable(), updated.ToImmutable(), removed.ToImmutable());
    }

    private static bool IdentityEquals(NpcIdentity left, NpcIdentity right) =>
        left.TemplateId == right.TemplateId && left.Kind == right.Kind && left.LegacyAiType == right.LegacyAiType &&
        left.Level == right.Level && left.ClanHelpRange == right.ClanHelpRange &&
        left.Capabilities == right.Capabilities && left.ClanIds.SequenceEqual(right.ClanIds);
}
