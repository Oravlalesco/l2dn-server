using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class TacticalBrain
{
    private readonly TacticalActionEvaluator _evaluator;

    public TacticalBrain(TacticalActionEvaluator? evaluator = null) =>
        _evaluator = evaluator ?? new TacticalActionEvaluator();

    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        long decisionSequence)
    {
        if (!profile.TacticalEnabled || !NpcPerceptionFacts.IsActorOperational(perception) ||
            perception.State.Combat.CurrentTarget is not { } target ||
            !NpcPerceptionFacts.TryGetValidTarget(perception, target, out _, out double distance))
        {
            return null;
        }

        NpcTacticalScore score = _evaluator.Evaluate(perception, profile, distance);
        NpcPerceptionEnvelope snapshot = perception.Envelope;
        return score.Action switch
        {
            NpcTacticalAction.BasicAttack => new BasicAttackIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.BasicAttack), target),
            NpcTacticalAction.Approach => new ApproachTargetIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.ApproachTarget), target,
                profile.PreferredRange > 0 ? profile.PreferredRange : perception.State.Combat.PhysicalAttackRange),
            NpcTacticalAction.Flee => new FleeIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.Flee), target),
            _ => null
        };
    }

    private static NpcIntentEnvelope Envelope(NpcPerceptionEnvelope snapshot, long sequence, NpcIntentType type) =>
        new(NpcIntent.CurrentSchemaVersion, snapshot.Npc, snapshot.StateRevision, sequence, type);
}
