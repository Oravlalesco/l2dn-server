using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class TacticalBrain
{
    private readonly TacticalActionEvaluator _evaluator;

    public TacticalBrain(TacticalActionEvaluator? evaluator = null) =>
        _evaluator = evaluator ?? new TacticalActionEvaluator();

    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcBrainState state, long decisionSequence) =>
        DecideCore(perception, profile, null, state, decisionSequence, null);

    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcStrategyDecision strategy, NpcBrainState state, long decisionSequence) =>
        DecideCore(perception, profile, strategy, state, decisionSequence, null);

    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcStrategyDecision strategy, NpcBrainState state, long decisionSequence,
        NpcStrategyDiagnosticsCollector diagnostics) =>
        DecideCore(perception, profile, strategy, state, decisionSequence, diagnostics);

    private NpcIntent? DecideCore(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcStrategyDecision? strategy, NpcBrainState state, long decisionSequence,
        NpcStrategyDiagnosticsCollector? diagnostics)
    {
        if (!profile.TacticalEnabled || !NpcPerceptionFacts.IsActorOperational(perception) ||
            perception.State.Combat.Flags.HasFlag(NpcCombatFlags.Casting) ||
            perception.State.Combat.CurrentTarget is not { } target ||
            !NpcPerceptionFacts.TryGetValidTarget(perception, target, out VisibleEntity visibleTarget,
                out double distance))
        {
            return null;
        }

        double targetCollisionRadius = visibleTarget.Entity == target ? visibleTarget.CollisionRadius : 0;
        double physicalReach = Math.Max(1, perception.State.Combat.PhysicalAttackRange) +
            perception.State.Physical.CollisionRadius + targetCollisionRadius;
        bool repeatedMeleeCast = state.LastDecision == NpcIntentType.CastSkill && distance <= physicalReach;
        // Legacy FIGHTER templates use ranged skills opportunistically, then close for their physical attack.
        // MAGE/HEALER templates keep ranged spell preference. Actor movement is observed state, not new memory.
        bool continuingPhysicalApproach = state.LastDecision == NpcIntentType.ApproachTarget &&
            perception.State.Physical.Flags.HasFlag(NpcPhysicalFlags.Moving);
        bool fighterPhysicalPreference = perception.State.Identity.LegacyAiType == LegacyNpcAiType.Fighter &&
            (distance <= physicalReach || continuingPhysicalApproach ||
                state.LastDecision == NpcIntentType.CastSkill);
        NpcStrategyCandidateEligibility? offensiveSkillSuppression = repeatedMeleeCast
            ? NpcStrategyCandidateEligibility.RepeatedActionSuppressed
            : fighterPhysicalPreference
                ? NpcStrategyCandidateEligibility.TacticalPreferenceSuppressed
                : null;
        NpcTacticalScore score = strategy.HasValue
            ? _evaluator.Evaluate(perception, profile, strategy.Value, distance,
                targetCollisionRadius, diagnostics, offensiveSkillSuppression)
            : _evaluator.Evaluate(perception, profile, distance, targetCollisionRadius,
                offensiveSkillSuppression);
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
