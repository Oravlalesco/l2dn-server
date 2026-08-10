using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class ReflexBrain
{
    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcBrainContext context,
        NpcIntelligenceProfile profile, long decisionSequence)
    {
        if (!profile.ReflexEnabled || !NpcPerceptionFacts.IsActorOperational(perception))
        {
            return null;
        }

        NpcPerceptionEnvelope snapshot = perception.Envelope;
        EntityKey? currentTarget = perception.State.Combat.CurrentTarget;
        EntityKey? highestThreat = NpcPerceptionFacts.SelectHighestVisibleThreat(perception);
        NpcEnvironment environment = perception.State.Environment;
        if (environment.SpawnPosition is { } spawn)
        {
            double distanceFromSpawn = NpcPerceptionFacts.Distance2D(perception.State.Physical.Position, spawn);
            int returnHomeDistance = environment.ReturnHomeDistance > 0
                ? environment.ReturnHomeDistance
                : profile.LeashDistance;
            int combatLeashDistance = environment.CombatLeashDistance;

            bool outsideCombatLeash = currentTarget.HasValue && combatLeashDistance > 0 &&
                distanceFromSpawn > combatLeashDistance;
            bool outsideHomeRange = !currentTarget.HasValue && returnHomeDistance > 0 &&
                distanceFromSpawn > returnHomeDistance;
            if (environment.CanReturnToSpawn && !environment.ReturningToSpawn &&
                (outsideCombatLeash || outsideHomeRange))
            {
                return new ReturnHomeIntent(Envelope(snapshot, decisionSequence, NpcIntentType.ReturnHome));
            }

            // Match legacy MOVE_TO behavior: a mere spectator cannot interrupt the
            // return, but a new attack/aggression event with authoritative hate can.
            if (environment.ReturningToSpawn && returnHomeDistance > 0 &&
                distanceFromSpawn > returnHomeDistance)
            {
                NpcBrainStimulus interruptingStimuli = NpcBrainStimulus.Attacked |
                    NpcBrainStimulus.ThreatChanged | NpcBrainStimulus.AllyAttacked;
                if ((context.Stimuli & interruptingStimuli) != 0 && highestThreat.HasValue)
                {
                    return new AcquireTargetIntent(
                        Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                        highestThreat.Value);
                }
                return null;
            }
        }

        if (currentTarget.HasValue)
        {
            if (!NpcPerceptionFacts.TryGetValidTarget(perception, currentTarget.Value, out _, out _))
            {
                if (highestThreat.HasValue && highestThreat.Value != currentTarget.Value)
                {
                    return new AcquireTargetIntent(
                        Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                        highestThreat.Value);
                }
                return new ClearTargetIntent(Envelope(snapshot, decisionSequence, NpcIntentType.ClearTarget),
                    currentTarget);
            }

            // Legacy thinkAttack() continuously follows getMostHated(). The current
            // target is therefore not sticky when another visible attacker has won hate.
            if (highestThreat.HasValue && highestThreat.Value != currentTarget.Value)
            {
                return new AcquireTargetIntent(
                    Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                    highestThreat.Value);
            }

            if (profile.FleeAllowed &&
                NpcPerceptionFacts.HpPercent(perception.State.Physical) <= profile.FleeHpPercent)
            {
                return new FleeIntent(Envelope(snapshot, decisionSequence, NpcIntentType.Flee), currentTarget);
            }

            return null;
        }

        EntityKey? selected = NpcPerceptionFacts.SelectTarget(perception, profile);
        if (selected.HasValue)
        {
            return new AcquireTargetIntent(Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                selected.Value);
        }

        return null;
    }

    private static NpcIntentEnvelope Envelope(NpcPerceptionEnvelope snapshot, long sequence, NpcIntentType type) =>
        new(NpcIntent.CurrentSchemaVersion, snapshot.Npc, snapshot.StateRevision, sequence, type);
}
