using L2Dn.GameServer.Model.Actor;

namespace L2Dn.GameServer.AI.Scheduling;

internal static class NpcReactivity
{
    public static NpcReactiveSchedulerMode Mode => NpcThinkCoordinator.Instance.Mode;

    public static NpcWakeDisposition Wake(Attackable actor, NpcWakeReason reason,
        long eventTimestamp = 0, int sourcePoolId = 0) =>
        !actor.hasAI()
            ? NpcWakeDisposition.Ignored
            : NpcThinkCoordinator.Instance.Wake(actor, reason, eventTimestamp, sourcePoolId);

    public static void Remove(Attackable actor) => NpcThinkCoordinator.Instance.Remove(actor.ObjectId);
}
