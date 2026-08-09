using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class ShadowNpcThinkExecutor: INpcThinkExecutor, INpcThinkLifecycle
{
    private readonly LegacyNpcThinkExecutor _legacy;
    private readonly INpcBrain _brain;

    public ShadowNpcThinkExecutor(LegacyNpcThinkExecutor legacy, INpcBrain brain)
    {
        _legacy = legacy;
        _brain = brain;
    }

    public ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Attackable? actor = LegacyNpcThinkExecutor.Resolve(npc);
        if (actor == null)
        {
            return ValueTask.CompletedTask;
        }
        if (actor.getAI() is not AttackableAI attackableAi || attackableAi.GetType() != typeof(AttackableAI))
        {
            return _legacy.ExecuteAsync(npc, context, cancellationToken);
        }

        NpcPerceptionCycle? perception = null;
        _legacy.ExecuteMeasured(actor, ai =>
        {
            perception = _legacy.Capture(actor, true);
            if (perception == null)
            {
                ai.onEvtThink();
                return;
            }

            NpcBrainDecision decision = NpcAiTelemetry.ObserveBrainDecision(() => _brain.Decide(
                perception.Snapshot,
                new NpcBrainContext(NpcBrainStimulusMapper.Map(context.Reasons))));
            using LegacyNpcCommandObserver.Scope observed = LegacyNpcCommandObserver.Begin(perception.Snapshot);
            if (NpcPerceptionCoordinator.Instance.Mode == NpcPerceptionMode.SnapshotRead)
            {
                attackableAi.onEvtThink(perception.Snapshot);
            }
            else
            {
                ai.onEvtThink();
            }
            NpcAiTelemetry.RecordShadowComparison(Compare(decision.Intents, observed.Intents));
        });
        _legacy.Publish(context, perception);
        return ValueTask.CompletedTask;
    }

    public void Remove(NpcKey npc) => _brain.Remove(npc);

    internal static NpcIntentComparisonKind Compare(IReadOnlyCollection<NpcIntent> brain,
        IReadOnlyCollection<NpcIntent> legacy)
    {
        if (brain.Count == 0 && legacy.Count == 0)
        {
            return NpcIntentComparisonKind.ExactMatch;
        }
        if (brain.Count == 0 || legacy.Count == 0)
        {
            return NpcIntentComparisonKind.NotComparable;
        }

        foreach (NpcIntent brainIntent in brain)
        {
            foreach (NpcIntent legacyIntent in legacy)
            {
                if (brainIntent == legacyIntent)
                {
                    return NpcIntentComparisonKind.ExactMatch;
                }
                if (NpcIntentSemanticComparer.Instance.Equals(brainIntent, legacyIntent))
                {
                    return NpcIntentComparisonKind.SemanticMatch;
                }
            }
        }
        return NpcIntentComparisonKind.Different;
    }
}
