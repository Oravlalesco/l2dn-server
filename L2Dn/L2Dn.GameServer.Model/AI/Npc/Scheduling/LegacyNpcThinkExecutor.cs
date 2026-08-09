using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Scheduling;

internal sealed class LegacyNpcThinkExecutor: INpcThinkExecutor, INpcGenerationValidator
{
    private long _batchSequence;

    public static LegacyNpcThinkExecutor Instance { get; } = new();

    private LegacyNpcThinkExecutor()
    {
    }

    public bool IsCurrent(NpcKey npc) => Resolve(npc) != null;

    public ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Attackable? actor = Resolve(npc);
        if (actor == null)
        {
            return ValueTask.CompletedTask;
        }

        NpcPerceptionCycle? perception = ExecuteCaptureAndLegacy(actor);
        Publish(context, perception);

        return ValueTask.CompletedTask;
    }

    internal NpcPerceptionCycle? Capture(Attackable actor, bool requiredForBrain = false) =>
        NpcPerceptionCoordinator.Instance.Capture(actor, requiredForBrain);

    internal NpcPerceptionCycle? ExecuteCaptureAndLegacy(Attackable actor, bool requiredForBrain = false)
    {
        NpcPerceptionCycle? perception = null;
        ExecuteMeasured(actor, ai =>
        {
            perception = Capture(actor, requiredForBrain);
            ExecuteDecision(ai, perception);
        });
        return perception;
    }

    internal void ExecuteDirect(Attackable actor, NpcPerceptionCycle? perception) =>
        ExecuteMeasured(actor, ai => ExecuteDecision(ai, perception));

    internal void ExecuteMeasured(Attackable actor, Action<CreatureAI> operation)
    {
        if (!actor.hasAI() || actor.getAI() is not CreatureAI ai)
        {
            return;
        }

        CtrlIntention intention = ai.getIntention();
        bool measure = NpcAiTelemetry.ThinkMeasurementsEnabled;
        long startedAt = measure ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        long allocatedBytesBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
        using System.Diagnostics.Activity? thinkActivity = NpcAiTelemetry.StartThinkActivity(ai, intention);
        try
        {
            operation(ai);
        }
        catch
        {
            NpcAiTelemetry.RecordThinkError(ai, intention);
            throw;
        }
        finally
        {
            if (measure)
            {
                NpcAiTelemetry.RecordThink(ai, intention, startedAt, allocatedBytesBefore);
            }
        }
    }

    private static void ExecuteDecision(CreatureAI ai, NpcPerceptionCycle? perception)
    {
        using (NpcAiTelemetry.StartDecisionActivity(ai))
        {
            if (NpcPerceptionCoordinator.Instance.Mode == NpcPerceptionMode.SnapshotRead && perception != null &&
                ai is AttackableAI attackableAi)
            {
                attackableAi.onEvtThink(perception.Snapshot);
            }
            else
            {
                ai.onEvtThink();
            }
        }
    }

    internal void Publish(NpcWakeContext context, NpcPerceptionCycle? perception)
    {
        if (perception is not { Publication: not NpcPerceptionPublicationKind.None })
        {
            return;
        }

        long sequence = Interlocked.Increment(ref _batchSequence);
        foreach (NpcPerceptionRegionBatch batch in NpcPerceptionBatchBuilder.Build(
                     context.SourcePoolId, sequence, [perception]))
        {
            NpcPerceptionBatchHub.Publish(batch);
        }
    }

    internal static Attackable? Resolve(NpcKey npc)
    {
        if (World.getInstance().findObject(npc.ObjectId) is not Attackable actor ||
            actor.getSpawnGeneration() != npc.Generation || !actor.isSpawned() || !actor.hasAI())
        {
            return null;
        }
        return actor;
    }
}
