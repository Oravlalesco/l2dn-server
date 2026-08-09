namespace L2Dn.GameServer.AI.Scheduling;

public enum NpcThinkPriority
{
    Normal = 0,
    Combat = 1,
    Critical = 2
}

internal static class NpcWakePriorities
{
    public static NpcThinkPriority For(NpcWakeReason reasons)
    {
        if ((reasons & (NpcWakeReason.Attacked | NpcWakeReason.TargetLost | NpcWakeReason.TargetDied |
                       NpcWakeReason.PlayerBecameRelevant)) != 0)
        {
            return NpcThinkPriority.Critical;
        }

        if ((reasons & (NpcWakeReason.ThreatChanged | NpcWakeReason.AllyAttacked |
                       NpcWakeReason.CombatStarted)) != 0)
        {
            return NpcThinkPriority.Combat;
        }

        return NpcThinkPriority.Normal;
    }
}

