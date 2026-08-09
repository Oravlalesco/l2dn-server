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

        NpcEnvironment environment = perception.State.Environment;
        if (environment.CanReturnToSpawn && !environment.ReturningToSpawn &&
            environment.SpawnPosition is { } spawn &&
            NpcPerceptionFacts.Distance2D(perception.State.Physical.Position, spawn) > profile.LeashDistance)
        {
            return new ReturnHomeIntent(Envelope(snapshot, decisionSequence, NpcIntentType.ReturnHome));
        }

        return null;
    }

    private static NpcIntentEnvelope Envelope(NpcPerceptionEnvelope snapshot, long sequence, NpcIntentType type) =>
        new(NpcIntent.CurrentSchemaVersion, snapshot.Npc, snapshot.StateRevision, sequence, type);
}
