using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed record NpcPerceptionCapture(
    NpcKey Npc,
    NpcLifecycleStamp Lifecycle,
    long WorldTick,
    long CaptureMonotonicMilliseconds,
    NpcPerceptionState State)
{
    public bool TryCreateSnapshot(Attackable actor, long stateRevision, out NpcPerceptionSnapshot? snapshot)
    {
        if (stateRevision <= 0 || !actor.tryGetStableLifecycleStamp(out NpcLifecycleStamp current) ||
            current != Lifecycle)
        {
            snapshot = null;
            return false;
        }

        snapshot = new NpcPerceptionSnapshot(
            new NpcPerceptionEnvelope(NpcPerceptionSnapshot.CurrentSchemaVersion, Npc, stateRevision,
                WorldTick, CaptureMonotonicMilliseconds),
            State);
        return true;
    }
}
