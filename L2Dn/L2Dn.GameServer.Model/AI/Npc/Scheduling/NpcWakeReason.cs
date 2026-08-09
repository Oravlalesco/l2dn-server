namespace L2Dn.GameServer.AI.Scheduling;

[Flags]
public enum NpcWakeReason
{
    None = 0,
    PeriodicDue = 1 << 0,
    PlayerBecameRelevant = 1 << 1,
    Attacked = 1 << 2,
    ThreatChanged = 1 << 3,
    TargetLost = 1 << 4,
    TargetDied = 1 << 5,
    AllyAttacked = 1 << 6,
    CombatStarted = 1 << 7,
    CombatEnded = 1 << 8,
    RegionActivated = 1 << 9,
    Respawned = 1 << 10,
    ActionReady = 1 << 11
}
