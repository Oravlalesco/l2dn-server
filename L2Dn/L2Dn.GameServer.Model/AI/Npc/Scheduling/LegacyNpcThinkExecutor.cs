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

        NpcPerceptionCycle? perception = ExecuteDirect(actor);
        if (perception is { Publication: not NpcPerceptionPublicationKind.None })
        {
            long sequence = Interlocked.Increment(ref _batchSequence);
            foreach (NpcPerceptionRegionBatch batch in NpcPerceptionBatchBuilder.Build(
                         context.SourcePoolId, sequence, [perception]))
            {
                NpcPerceptionBatchHub.Publish(batch);
            }
        }

        return ValueTask.CompletedTask;
    }

    internal NpcPerceptionCycle? ExecuteDirect(Attackable actor)
    {
        if (!actor.hasAI() || actor.getAI() is not CreatureAI ai)
        {
            return null;
        }

        CtrlIntention intention = ai.getIntention();
        bool measure = NpcAiTelemetry.ThinkMeasurementsEnabled;
        long startedAt = measure ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        long allocatedBytesBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
        using System.Diagnostics.Activity? thinkActivity = NpcAiTelemetry.StartThinkActivity(ai, intention);
        try
        {
            NpcPerceptionCoordinator perceptionCoordinator = NpcPerceptionCoordinator.Instance;
            NpcPerceptionCycle? perception = perceptionCoordinator.Capture(actor);
            using (NpcAiTelemetry.StartDecisionActivity(ai))
            {
                if (perceptionCoordinator.Mode == NpcPerceptionMode.SnapshotRead && perception != null &&
                    ai is AttackableAI attackableAi)
                {
                    attackableAi.onEvtThink(perception.Snapshot);
                }
                else
                {
                    ai.onEvtThink();
                }
            }
            return perception;
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

    private static Attackable? Resolve(NpcKey npc)
    {
        if (World.getInstance().findObject(npc.ObjectId) is not Attackable actor ||
            actor.getSpawnGeneration() != npc.Generation || !actor.isSpawned() || !actor.hasAI())
        {
            return null;
        }
        return actor;
    }
}
