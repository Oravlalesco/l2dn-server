namespace L2Dn.NpcContracts;

public enum EntityKind
{
    Unknown = 0,
    Player = 1,
    Summon = 2,
    Npc = 3,
    Monster = 4,
    Guard = 5,
    Door = 6,
    OtherCreature = 7
}

public enum NpcKind
{
    Unknown = 0,
    Attackable = 1,
    Monster = 2,
    Guard = 3,
    RaidBoss = 4,
    RaidMinion = 5,
    Friendly = 6,
    Controllable = 7
}

public enum LegacyNpcAiType
{
    Unknown = 0,
    Fighter = 1,
    Archer = 2,
    Balanced = 3,
    Mage = 4,
    Healer = 5,
    Corpse = 6
}

[Flags]
public enum NpcCapabilities
{
    None = 0,
    CanMove = 1 << 0,
    CanAttack = 1 << 1,
    CanCast = 1 << 2,
    Aggressive = 1 << 3,
    Guard = 1 << 4,
    RaidBoss = 1 << 5,
    RaidMinion = 1 << 6,
    Flying = 1 << 7,
    FakePlayer = 1 << 8,
    CanSeeSilentMovement = 1 << 9
}

[Flags]
public enum NpcPhysicalFlags
{
    None = 0,
    Alive = 1 << 0,
    AlikeDead = 1 << 1,
    Running = 1 << 2,
    Moving = 1 << 3,
    MovementDisabled = 1 << 4,
    Spawned = 1 << 5
}

[Flags]
public enum NpcCombatFlags
{
    None = 0,
    InCombat = 1 << 0,
    Casting = 1 << 1,
    Attacking = 1 << 2,
    Confused = 1 << 3,
    CoreAiDisabled = 1 << 4,
    AllSkillsDisabled = 1 << 5
}

[Flags]
public enum EntityStateFlags
{
    None = 0,
    Alive = 1 << 0,
    AlikeDead = 1 << 1,
    Spawned = 1 << 2,
    Invulnerable = 1 << 3,
    Moving = 1 << 4,
    Casting = 1 << 5,
    SilentMoving = 1 << 6,
    RecentFakeDeath = 1 << 7,
    PeaceZone = 1 << 8,
    NoPvpZone = 1 << 9
}

[Flags]
public enum EntityRelationFlags
{
    None = 0,
    Playable = 1 << 0,
    Player = 1 << 1,
    Summon = 1 << 2,
    Npc = 1 << 3,
    Attackable = 1 << 4,
    Monster = 1 << 5,
    Guard = 1 << 6,
    FakePlayer = 1 << 7,
    SameClan = 1 << 8,
    CurrentTarget = 1 << 9,
    PrimaryThreat = 1 << 10,
    SameInstance = 1 << 11
}

[Flags]
public enum NpcAffordanceFlags
{
    None = 0,
    CanTarget = 1 << 0,
    CanPhysicallyAttack = 1 << 1,
    CanAssist = 1 << 2,
    CanInteract = 1 << 3,
    AutoAttackable = 1 << 4
}

[Flags]
public enum SpatialObservationFlags
{
    None = 0,
    HasLineOfSight = 1 << 0,
    CanReachDirectly = 1 << 1
}

public enum NpcSkillCategory
{
    Unknown = 0,
    Offensive = 1,
    Heal = 2,
    Buff = 3,
    Debuff = 4,
    Control = 5,
    Resurrection = 6,
    Suicide = 7
}

[Flags]
public enum NpcSkillObservationFlags
{
    None = 0,
    Ready = 1 << 0,
    Cooldown = 1 << 1,
    InsufficientMana = 1 << 2,
    Magic = 1 << 3,
    Bad = 1 << 4
}
