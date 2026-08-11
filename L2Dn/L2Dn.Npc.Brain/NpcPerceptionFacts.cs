using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

internal static class NpcPerceptionFacts
{
    public static bool IsActorOperational(NpcPerceptionSnapshot perception)
    {
        NpcPhysicalFlags physical = perception.State.Physical.Flags;
        NpcCombatFlags combat = perception.State.Combat.Flags;
        return physical.HasFlag(NpcPhysicalFlags.Alive) && !physical.HasFlag(NpcPhysicalFlags.AlikeDead) &&
            physical.HasFlag(NpcPhysicalFlags.Spawned) && !combat.HasFlag(NpcCombatFlags.CoreAiDisabled) &&
            !combat.HasFlag(NpcCombatFlags.AllSkillsDisabled);
    }

    public static bool TryGetValidTarget(NpcPerceptionSnapshot perception, EntityKey key,
        out VisibleEntity visible, out double distance)
    {
        foreach (VisibleEntity candidate in perception.State.VisibleEntities)
        {
            if (candidate.Entity != key)
            {
                continue;
            }

            visible = candidate;
            distance = candidate.Distance2D;
            return IsValidVisibleTarget(candidate) &&
                (candidate.Relations.HasFlag(EntityRelationFlags.AutoAttackable) ||
                    HasPositiveVisibleThreat(perception, key));
        }

        foreach (ThreatEntry threat in perception.State.Threats)
        {
            if (threat.Target == key && threat.Hate > 0 && threat.Visible && threat.ValidTarget)
            {
                visible = default;
                distance = threat.Distance2D;
                return true;
            }
        }

        visible = default;
        distance = double.MaxValue;
        return false;
    }

    public static EntityKey? SelectTarget(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile)
    {
        EntityKey? threat = SelectHighestVisibleThreat(perception);
        if (threat.HasValue)
        {
            return threat;
        }

        if (!profile.AcquireVisibleHostiles)
        {
            return null;
        }

        NpcCapabilities capabilities = perception.State.Identity.Capabilities;
        int aggroRange = perception.State.Combat.AggroRange;
        NpcPosition actorPosition = perception.State.Physical.Position;
        return perception.State.VisibleEntities
            .Where(candidate => Distance3D(actorPosition, candidate.Position) <= aggroRange &&
                IsVisibleHostileCandidate(candidate, capabilities))
            .OrderBy(static candidate => candidate.Distance2D)
            .ThenBy(static candidate => candidate.ObservationOrdinal)
            .Select(static candidate => (EntityKey?)candidate.Entity)
            .FirstOrDefault();
    }

    public static EntityKey? SelectHighestVisibleThreat(NpcPerceptionSnapshot perception)
    {
        EntityKey? target = null;
        long highestHate = 0;
        foreach (ThreatEntry entry in perception.State.Threats)
        {
            // Strictly greater preserves the first observed entry on ties, matching
            // legacy getMostHated() while remaining deterministic for replay.
            if (entry.Visible && entry.ValidTarget && entry.Hate > highestHate)
            {
                target = entry.Target;
                highestHate = entry.Hate;
            }
        }
        return target;
    }

    public static bool TryGetVisibleEntity(NpcPerceptionSnapshot perception, EntityKey key,
        out VisibleEntity visible)
    {
        foreach (VisibleEntity candidate in perception.State.VisibleEntities)
        {
            if (candidate.Entity == key)
            {
                visible = candidate;
                return true;
            }
        }

        visible = default;
        return false;
    }

    public static double HpPercent(NpcPhysicalState physical) =>
        physical.MaximumHp <= 0 ? 0 : physical.CurrentHp / physical.MaximumHp * 100;

    public static double Distance2D(NpcPosition left, NpcPosition right) =>
        double.Hypot((double)left.X - right.X, (double)left.Y - right.Y);

    public static double Distance3D(NpcPosition left, NpcPosition right)
    {
        double xy = Distance2D(left, right);
        return double.Hypot(xy, (double)left.Z - right.Z);
    }

    private static bool IsValidVisibleTarget(VisibleEntity candidate)
    {
        EntityStateFlags state = candidate.State;
        return state.HasFlag(EntityStateFlags.Alive) && !state.HasFlag(EntityStateFlags.AlikeDead) &&
            state.HasFlag(EntityStateFlags.Spawned) && !state.HasFlag(EntityStateFlags.Invulnerable) &&
            !state.HasFlag(EntityStateFlags.RecentFakeDeath) &&
            candidate.Relations.HasFlag(EntityRelationFlags.SameInstance);
    }

    private static bool IsVisibleHostileCandidate(VisibleEntity candidate, NpcCapabilities capabilities)
    {
        if (!IsValidVisibleTarget(candidate) ||
            !candidate.Relations.HasFlag(EntityRelationFlags.Playable) ||
            !candidate.Relations.HasFlag(EntityRelationFlags.AutoAttackable))
        {
            return false;
        }

        bool protectedPeaceZone = candidate.State.HasFlag(EntityStateFlags.PeaceZone) &&
            candidate.State.HasFlag(EntityStateFlags.NoPvpZone);
        if (protectedPeaceZone && !capabilities.HasFlag(NpcCapabilities.CanAcquireInPeaceZone))
        {
            return false;
        }

        return !candidate.State.HasFlag(EntityStateFlags.SilentMoving) ||
            capabilities.HasFlag(NpcCapabilities.CanSeeSilentMovement);
    }

    private static bool HasPositiveVisibleThreat(NpcPerceptionSnapshot perception, EntityKey key) =>
        perception.State.Threats.Any(threat => threat.Target == key && threat.Hate > 0 && threat.Visible &&
            threat.ValidTarget);
}
