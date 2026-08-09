using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class BrainNpcThinkExecutor: INpcThinkExecutor, INpcThinkLifecycle
{
    private readonly LegacyNpcThinkExecutor _legacy;
    private readonly INpcBrain _brain;
    private readonly NpcIntentGateway _gateway;

    public BrainNpcThinkExecutor(LegacyNpcThinkExecutor legacy, INpcBrain brain, NpcIntentGateway gateway)
    {
        _legacy = legacy;
        _brain = brain;
        _gateway = gateway;
    }

    public ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Attackable? actor = LegacyNpcThinkExecutor.Resolve(npc);
        if (actor == null)
        {
            return ValueTask.CompletedTask;
        }

        // Scripted/specialized AI remains on its authoritative legacy path until it has an explicit profile.
        if (actor.getAI() is not AttackableAI ai || ai.GetType() != typeof(AttackableAI))
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

            NpcBrainDecision decision = NpcAiTelemetry.ObserveBrainDecision(() => _brain.Decide(
                perception.Snapshot,
                new NpcBrainContext(NpcBrainStimulusMapper.Map(context.Reasons))));
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
}
