using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class BrainNpcThinkExecutor: INpcThinkExecutor, INpcThinkLifecycle
{
    private readonly LegacyNpcThinkExecutor _legacy;
    private readonly NpcBrainCoordinator _brain;
    private readonly NpcIntentGateway _gateway;
    private readonly NpcReturnDefensePolicy _returnDefense;
    private readonly NpcStrategyOptions _strategy;

    public BrainNpcThinkExecutor(LegacyNpcThinkExecutor legacy, NpcBrainCoordinator brain,
        NpcIntentGateway gateway, NpcReturnDefensePolicy returnDefense, NpcStrategyOptions strategy)
    {
        _legacy = legacy;
        _brain = brain;
        _gateway = gateway;
        _returnDefense = returnDefense;
        _strategy = strategy;
    }

    public ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Attackable? actor = LegacyNpcThinkExecutor.Resolve(npc);
        if (actor == null)
        {
            return ValueTask.CompletedTask;
        }

        // Sharing the base AttackableAI is not sufficient: guards, raids, minions,
        // and scripted actor subclasses still own legacy behavior not represented
        // by the Phase 3 Brain contract.
        if (!NpcBrainEligibility.TryGetIntentAi(actor, out AttackableAI ai))
        {
            return _legacy.ExecuteAsync(npc, context, cancellationToken);
        }

        NpcPerceptionCycle? perception = null;
        _legacy.ExecuteMeasured(actor, ai =>
        {
            perception = _legacy.Capture(actor, true);
            if (perception == null)
            {
                // Lifecycle changed during capture. Do not act on torn state; the next wake retries.
                return;
            }

            NpcBrainContext brainContext = new(NpcBrainStimulusMapper.Map(context.Reasons),
                ReturnDefense: _returnDefense,
                ReflexPolicy: ResolveReflexPolicy(perception.Snapshot));
            NpcBrainDecision decision = Decide(actor, perception.Snapshot, brainContext);
            foreach (NpcIntent intent in decision.Intents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _gateway.Execute(intent);
            }
        });
        _legacy.Publish(context, perception);
        return ValueTask.CompletedTask;
    }

    public void Remove(NpcKey npc) => _brain.Remove(npc);

    private NpcReflexPolicy ResolveReflexPolicy(NpcPerceptionSnapshot perception)
    {
        NpcIdentity identity = perception.State.Identity;
        NpcIntelligenceProfile intelligence =
            NpcIntelligenceProfileResolver.Instance.Resolve(identity);
        double fleeHp = _strategy.EffectiveMode != NpcStrategyMode.Disabled &&
            _strategy.Registry.TryResolve(identity.TemplateId, out NpcStrategyProfile stratProfile, out _)
                ? stratProfile.FleeHpPercentOverride ?? intelligence.FleeHpPercent
                : intelligence.FleeHpPercent;
        return new NpcReflexPolicy(fleeHp, intelligence.FleeAllowed, intelligence.LeashDistance);
    }

    private NpcBrainDecision Decide(Attackable actor, NpcPerceptionSnapshot perception,
        NpcBrainContext context)
    {
        if (_strategy.EffectiveMode == NpcStrategyMode.Disabled ||
            !_strategy.Registry.TryResolve(actor.getId(), out NpcStrategyProfile profile,
                out bool runtimeFallback))
        {
            return NpcAiTelemetry.ObserveBrainDecision(() => _brain.Decide(perception, context));
        }
        if (runtimeFallback)
        {
            NpcAiTelemetry.RecordStrategyFallback("runtime_profile");
        }

        if (_strategy.EffectiveMode == NpcStrategyMode.Shadow)
        {
            NpcStrategyShadowEvaluation shadow = _brain.DecideShadow(perception, context, profile);
            NpcAiTelemetry.RecordBrainDecision(shadow.BaselineDecision, shadow.BaselineDecisionDuration);
            if (shadow.StrategyDecision?.StrategyDecision is { } strategyDecision)
            {
                NpcAiTelemetry.RecordStrategyEvaluation(strategyDecision,
                    shadow.StrategyDecisionDuration, NpcStrategyMode.Shadow);
                StrategyComparisonResult comparison = NpcStrategyDecisionComparer.Compare(
                    shadow.BaselineDecision, shadow.StrategyDecision);
                NpcAiTelemetry.RecordStrategyShadowComparison(profile.Archetype, comparison);
            }
            else
            {
                NpcAiTelemetry.RecordStrategyEvaluationFailure(profile.Archetype,
                    shadow.StrategyDecisionDuration, NpcStrategyMode.Shadow);
            }
            return shadow.BaselineDecision;
        }

        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        NpcBrainDecision enabled = _brain.DecideWithStrategy(perception, context, profile);
        TimeSpan duration = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt);
        NpcAiTelemetry.RecordBrainDecision(enabled, duration);
        if (enabled.StrategyDecision is { } strategyDecisionEnabled)
        {
            NpcAiTelemetry.RecordStrategyEvaluation(strategyDecisionEnabled,
                duration, NpcStrategyMode.Enabled);
        }
        return enabled;
    }
}
