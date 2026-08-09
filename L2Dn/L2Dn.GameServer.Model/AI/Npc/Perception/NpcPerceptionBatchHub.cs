using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal static class NpcPerceptionBatchHub
{
    public static event Action<NpcPerceptionRegionBatch>? Published;

    public static void Publish(NpcPerceptionRegionBatch batch)
    {
        NpcAiTelemetry.RecordPerceptionBatch(batch);
        NpcPerceptionReplayRecorder.Record(batch);
        Delegate[] subscribers = Published?.GetInvocationList() ?? [];
        foreach (Delegate subscriber in subscribers)
        {
            try
            {
                ((Action<NpcPerceptionRegionBatch>)subscriber)(batch);
            }
            catch
            {
                // Perception consumers are non-authoritative and may never interrupt the scheduler.
                NpcAiTelemetry.RecordPerceptionBatchConsumerFailure();
            }
        }
    }
}
