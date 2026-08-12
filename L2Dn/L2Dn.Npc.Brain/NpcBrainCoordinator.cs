using System.Collections.Immutable;
using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class NpcBrainCoordinator: INpcBrain
{
    private readonly NpcBrainStateStore _states;
    private readonly NpcIntelligenceProfileResolver _profiles;
    private readonly StrategyBrain _strategy;
    private readonly ReflexBrain _reflex;
    private readonly TacticalBrain _tactical;

    public NpcBrainCoordinator()
        : this(new NpcBrainStateStore(), NpcIntelligenceProfileResolver.Instance,
            new StrategyBrain(), new ReflexBrain(), new TacticalBrain())
    {
    }

    internal NpcBrainCoordinator(NpcBrainStateStore states, NpcIntelligenceProfileResolver profiles,
        StrategyBrain strategy, ReflexBrain reflex, TacticalBrain tactical)
    {
        _states = states;
        _profiles = profiles;
        _strategy = strategy;
        _reflex = reflex;
        _tactical = tactical;
    }

    /// <summary>
    /// Executes the exact Phase 3 Reflex/Tactical pipeline. StrategyBrain is not invoked.
    /// </summary>
    public NpcBrainDecision Decide(NpcPerceptionSnapshot perception, NpcBrainContext context) =>
        DecideCore(perception, context, null, null).Decision;

    public NpcBrainDecision DecideWithStrategy(NpcPerceptionSnapshot perception, NpcBrainContext context,
        NpcStrategyProfile strategyProfile)
    {
        ArgumentNullException.ThrowIfNull(strategyProfile);
        return DecideCore(perception, context, strategyProfile, null).Decision;
    }

    internal NpcStrategyBrainEvaluation DecideWithStrategyDiagnostics(NpcPerceptionSnapshot perception,
        NpcBrainContext context, NpcStrategyProfile strategyProfile)
    {
        ArgumentNullException.ThrowIfNull(strategyProfile);
        return DecideCore(perception, context, strategyProfile, new NpcStrategyDiagnosticsCollector());
    }

    public void Remove(NpcKey npc) => _states.Remove(npc);

    private NpcStrategyBrainEvaluation DecideCore(NpcPerceptionSnapshot perception, NpcBrainContext context,
        NpcStrategyProfile? strategyProfile, NpcStrategyDiagnosticsCollector? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(context);
        NpcKey actor = perception.Envelope.Npc;
        if (perception.Envelope.SchemaVersion != NpcPerceptionSnapshot.CurrentSchemaVersion ||
            perception.Envelope.StateRevision <= 0)
        {
            return new NpcStrategyBrainEvaluation(
                new NpcBrainDecision(actor, 0, NpcBrainLayer.None, []),
                NpcStrategyDecisionDiagnostics.Empty);
        }

        bool accepted = _states.TryUse(actor,
            state => DecideWithState(perception, context, strategyProfile, diagnostics, state),
            out NpcStrategyBrainEvaluation? evaluation);
        return accepted && evaluation != null
            ? evaluation
            : new NpcStrategyBrainEvaluation(
                new NpcBrainDecision(actor, 0, NpcBrainLayer.None, []),
                NpcStrategyDecisionDiagnostics.Empty);
    }

    private NpcStrategyBrainEvaluation DecideWithState(NpcPerceptionSnapshot perception,
        NpcBrainContext context, NpcStrategyProfile? strategyProfile,
        NpcStrategyDiagnosticsCollector? diagnostics, NpcBrainState state)
    {
        long sequence = ++state.DecisionSequence;
        NpcIntelligenceProfile profile = context.Profile ?? _profiles.Resolve(perception.State.Identity);
        NpcStrategyDecision? strategy = strategyProfile == null
            ? null
            : _strategy.Decide(perception, profile, strategyProfile);

        NpcIntent? intent = strategy.HasValue
            ? _reflex.Decide(perception, context, profile, strategy.Value, state, sequence)
            : _reflex.Decide(perception, context, profile, state, sequence);
        NpcBrainLayer layer = intent == null ? NpcBrainLayer.None : NpcBrainLayer.Reflex;
        if (intent == null)
        {
            if (strategy.HasValue)
            {
                intent = diagnostics == null
                    ? _tactical.Decide(perception, profile, strategy.Value, state, sequence)
                    : _tactical.Decide(perception, profile, strategy.Value, state, sequence, diagnostics);
            }
            else
            {
                intent = _tactical.Decide(perception, profile, state, sequence);
            }
            layer = intent == null ? NpcBrainLayer.None : NpcBrainLayer.Tactical;
        }

        state.LastTarget = perception.State.Combat.CurrentTarget;
        state.LastDecision = intent?.Envelope.IntentType;
        state.LastDecisionWorldTick = perception.Envelope.WorldTick;
        state.FleeMode = intent is FleeIntent;
        state.LastStrategy = strategy?.Archetype ?? NpcStrategyArchetype.Balanced;

        ImmutableArray<NpcIntent> intents = intent == null ? [] : [intent];
        NpcStrategyDecisionSummary? summary = strategy.HasValue
            ? new NpcStrategyDecisionSummary(strategy.Value.Archetype,
                MapStrategyAction(intent, perception), DetermineReason(intent, layer, perception),
                strategy.Value.AppliedModifiers)
            : null;
        NpcBrainDecision decision = new(perception.Envelope.Npc, sequence, layer, intents, summary);
        NpcStrategyDecisionDiagnostics detail = diagnostics != null && strategy.HasValue
            ? diagnostics.Build(profile, strategy.Value)
            : NpcStrategyDecisionDiagnostics.Empty;
        return new NpcStrategyBrainEvaluation(decision, detail);
    }

    private static NpcStrategyAction MapStrategyAction(NpcIntent? intent,
        NpcPerceptionSnapshot perception) => intent switch
    {
        BasicAttackIntent => NpcStrategyAction.BasicAttack,
        ApproachTargetIntent => NpcStrategyAction.Approach,
        FleeIntent => NpcStrategyAction.Flee,
        CastSkillIntent cast when perception.State.Skills.Any(skill =>
            skill.SkillId == cast.SkillId && skill.Level == cast.SkillLevel &&
            skill.Category == NpcSkillCategory.Heal) => NpcStrategyAction.Heal,
        CastSkillIntent => NpcStrategyAction.OffensiveSkill,
        _ => NpcStrategyAction.None
    };

    private static NpcStrategyDecisionReason DetermineReason(NpcIntent? intent, NpcBrainLayer layer,
        NpcPerceptionSnapshot perception)
    {
        if (layer == NpcBrainLayer.Reflex)
        {
            return NpcStrategyDecisionReason.ReflexAuthority;
        }
        if (layer == NpcBrainLayer.Tactical)
        {
            return NpcStrategyDecisionReason.HighestEligibleScore;
        }
        if (!NpcPerceptionFacts.IsActorOperational(perception))
        {
            return NpcStrategyDecisionReason.ActorUnavailable;
        }
        if (!perception.State.Combat.CurrentTarget.HasValue)
        {
            return NpcStrategyDecisionReason.TargetUnavailable;
        }
        return intent == null
            ? NpcStrategyDecisionReason.NoEligibleAction
            : NpcStrategyDecisionReason.None;
    }
}

internal sealed record NpcStrategyBrainEvaluation(
    NpcBrainDecision Decision,
    NpcStrategyDecisionDiagnostics Diagnostics);
