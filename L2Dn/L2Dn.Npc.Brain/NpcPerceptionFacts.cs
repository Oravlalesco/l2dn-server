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
            return IsValidVisibleTarget(candidate);
        }

        foreach (ThreatEntry threat in perception.State.Threats)
        {
            if (threat.Target == key && threat.ValidTarget)
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
        ThreatEntry? threat = perception.State.Threats
            .Where(static entry => entry.ValidTarget && entry.Hate > 0)
            .OrderByDescending(static entry => entry.Hate)
            .ThenBy(static entry => entry.Distance2D)
            .Cast<ThreatEntry?>()
            .FirstOrDefault();
        if (threat.HasValue)
        {
            return threat.Value.Target;
        }

        if (!profile.AcquireVisibleHostiles)
        {
            return null;
        }

        NpcCapabilities capabilities = perception.State.Identity.Capabilities;
        int aggroRange = perception.State.Combat.AggroRange;
        return perception.State.VisibleEntities
            .Where(candidate => candidate.Distance2D <= aggroRange && IsVisibleHostileCandidate(candidate,
                capabilities))
            .OrderBy(static candidate => candidate.Distance2D)
            .ThenBy(static candidate => candidate.ObservationOrdinal)
            .Select(static candidate => (EntityKey?)candidate.Entity)
            .FirstOrDefault();
    }

    public static double HpPercent(NpcPhysicalState physical) =>
        physical.MaximumHp <= 0 ? 0 : physical.CurrentHp / physical.MaximumHp * 100;

    public static double Distance2D(NpcPosition left, NpcPosition right) =>
        double.Hypot((double)left.X - right.X, (double)left.Y - right.Y);

    private static bool IsValidVisibleTarget(VisibleEntity candidate)
    {
        EntityStateFlags state = candidate.State;
        return state.HasFlag(EntityStateFlags.Alive) && !state.HasFlag(EntityStateFlags.AlikeDead) &&
            state.HasFlag(EntityStateFlags.Spawned) && !state.HasFlag(EntityStateFlags.Invulnerable) &&
            !state.HasFlag(EntityStateFlags.PeaceZone) &&
            candidate.Relations.HasFlag(EntityRelationFlags.SameInstance);
    }

    private static bool IsVisibleHostileCandidate(VisibleEntity candidate, NpcCapabilities capabilities)
    {
        if (!IsValidVisibleTarget(candidate) ||
            !candidate.Relations.HasFlag(EntityRelationFlags.Playable))
        {
            return false;
        }

        return !candidate.State.HasFlag(EntityStateFlags.SilentMoving) ||
            capabilities.HasFlag(NpcCapabilities.CanSeeSilentMovement);
    }
}
