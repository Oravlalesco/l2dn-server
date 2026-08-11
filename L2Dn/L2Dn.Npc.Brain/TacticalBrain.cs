using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class TacticalBrain
{
    private readonly TacticalActionEvaluator _evaluator;

    public TacticalBrain(TacticalActionEvaluator? evaluator = null) =>
        _evaluator = evaluator ?? new TacticalActionEvaluator();

    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcStrategyDecision strategy, NpcBrainState state, long decisionSequence)
    {
        if (!profile.TacticalEnabled || !NpcPerceptionFacts.IsActorOperational(perception) ||
            perception.State.Combat.CurrentTarget is not { } target ||
            !NpcPerceptionFacts.TryGetValidTarget(perception, target, out VisibleEntity visibleTarget,
                out double distance))
        {
            return null;
        }

        double targetCollisionRadius = visibleTarget.Entity == target ? visibleTarget.CollisionRadius : 0;
        NpcTacticalScore score = _evaluator.Evaluate(perception, profile, strategy, distance,
            targetCollisionRadius);
        NpcPerceptionEnvelope snapshot = perception.Envelope;
        bool defensiveReturn = state.ReturnState == NpcReturnEngagementState.DefensiveReturn;
        if (defensiveReturn && score.Action == NpcTacticalAction.Flee)
        {
            if (state.ReturnMovementIssued)
            {
                return null;
            }

            state.ReturnMovementIssued = true;
            return new ReturnHomeIntent(Envelope(snapshot, decisionSequence, NpcIntentType.ReturnHome),
                NpcReturnHomeMode.PreserveThreat);
        }

        if (defensiveReturn && score.Action == NpcTacticalAction.Approach)
        {
            if (perception.State.Environment.SpawnPosition is not { } spawn ||
                !NpcPerceptionFacts.TryGetVisibleEntity(perception, target, out VisibleEntity visible) ||
                NpcPerceptionFacts.Distance2D(visible.Position, spawn) >
                NpcPerceptionFacts.Distance2D(perception.State.Physical.Position, spawn))
            {
                if (state.ReturnMovementIssued)
                {
                    return null;
                }

                state.ReturnMovementIssued = true;
                return new ReturnHomeIntent(Envelope(snapshot, decisionSequence, NpcIntentType.ReturnHome),
                    NpcReturnHomeMode.PreserveThreat);
            }

            state.ReturnMovementIssued = false;
            return new ApproachTargetIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.ApproachTarget), target,
                score.DesiredRange > 0 ? score.DesiredRange : perception.State.Combat.PhysicalAttackRange,
                NpcApproachConstraint.TowardSpawnOnly);
        }

        if (defensiveReturn)
        {
            state.ReturnMovementIssued = false;
        }

        return score.Action switch
        {
            NpcTacticalAction.BasicAttack => new BasicAttackIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.BasicAttack), target),
            NpcTacticalAction.Approach => new ApproachTargetIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.ApproachTarget), target,
                score.DesiredRange > 0 ? score.DesiredRange : perception.State.Combat.PhysicalAttackRange),
            NpcTacticalAction.Flee => new FleeIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.Flee), target),
            NpcTacticalAction.CastSkill when score.Skill is { } skill => new CastSkillIntent(
                Envelope(snapshot, decisionSequence, NpcIntentType.CastSkill), skill.SkillId, skill.Level,
                skill.Category is NpcSkillCategory.Heal or NpcSkillCategory.Buff
                    ? new EntityKey(snapshot.Npc.ObjectId, snapshot.Npc.Generation, EntityKind.Npc)
                    : target),
            _ => null
        };
    }

    private static NpcIntentEnvelope Envelope(NpcPerceptionEnvelope snapshot, long sequence, NpcIntentType type) =>
        new(NpcIntent.CurrentSchemaVersion, snapshot.Npc, snapshot.StateRevision, sequence, type);
}
