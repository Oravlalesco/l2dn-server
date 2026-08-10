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

            // Returning is an authoritative movement state. Do not reacquire a nearby
            // player until the NPC has completed the trip back into its home radius.
            if (environment.ReturningToSpawn && returnHomeDistance > 0 &&
                distanceFromSpawn > returnHomeDistance)
            {
                return null;
            }
        }

        if (currentTarget.HasValue)
        {
            if (!NpcPerceptionFacts.TryGetValidTarget(perception, currentTarget.Value, out _, out _))
            {
                return new ClearTargetIntent(Envelope(snapshot, decisionSequence, NpcIntentType.ClearTarget),
                    currentTarget);
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
