using System.Text.Json.Serialization;

namespace L2Dn.NpcContracts;

public enum NpcIntentType
{
    AcquireTarget = 1,
    ClearTarget = 2,
    BasicAttack = 3,
    ApproachTarget = 4,
    ReturnHome = 5,
    Flee = 6,
    CastSkill = 7,
    StopCombat = 8
}

public sealed record NpcIntentEnvelope(
    int SchemaVersion,
    NpcKey Actor,
    long BasedOnStateRevision,
    long DecisionSequence,
    NpcIntentType IntentType);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$intent")]
[JsonDerivedType(typeof(AcquireTargetIntent), "acquireTarget")]
[JsonDerivedType(typeof(ClearTargetIntent), "clearTarget")]
[JsonDerivedType(typeof(BasicAttackIntent), "basicAttack")]
[JsonDerivedType(typeof(ApproachTargetIntent), "approachTarget")]
[JsonDerivedType(typeof(ReturnHomeIntent), "returnHome")]
[JsonDerivedType(typeof(FleeIntent), "flee")]
[JsonDerivedType(typeof(CastSkillIntent), "castSkill")]
[JsonDerivedType(typeof(StopCombatIntent), "stopCombat")]
public abstract record NpcIntent
{
    public const int CurrentSchemaVersion = 1;

    protected NpcIntent(NpcIntentEnvelope envelope, NpcIntentType expectedType)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.IntentType != expectedType)
        {
            throw new ArgumentException($"Intent envelope type {envelope.IntentType} does not match {expectedType}.",
                nameof(envelope));
        }

        Envelope = envelope;
    }

    public NpcIntentEnvelope Envelope { get; }
}

public sealed record AcquireTargetIntent: NpcIntent
{
    [JsonConstructor]
    public AcquireTargetIntent(NpcIntentEnvelope envelope, EntityKey target)
        : base(envelope, NpcIntentType.AcquireTarget) => Target = target;

    public EntityKey Target { get; }
}

public sealed record ClearTargetIntent: NpcIntent
{
    [JsonConstructor]
    public ClearTargetIntent(NpcIntentEnvelope envelope, EntityKey? expectedTarget)
        : base(envelope, NpcIntentType.ClearTarget) => ExpectedTarget = expectedTarget;

    public EntityKey? ExpectedTarget { get; }
}

public sealed record BasicAttackIntent: NpcIntent
{
    [JsonConstructor]
    public BasicAttackIntent(NpcIntentEnvelope envelope, EntityKey target)
        : base(envelope, NpcIntentType.BasicAttack) => Target = target;

    public EntityKey Target { get; }
}

public sealed record ApproachTargetIntent: NpcIntent
{
    [JsonConstructor]
    public ApproachTargetIntent(NpcIntentEnvelope envelope, EntityKey target, int preferredRange)
        : base(envelope, NpcIntentType.ApproachTarget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(preferredRange);
        Target = target;
        PreferredRange = preferredRange;
    }

    public EntityKey Target { get; }
    public int PreferredRange { get; }
}

public sealed record ReturnHomeIntent: NpcIntent
{
    [JsonConstructor]
    public ReturnHomeIntent(NpcIntentEnvelope envelope)
        : base(envelope, NpcIntentType.ReturnHome)
    {
    }
}

public sealed record FleeIntent: NpcIntent
{
    [JsonConstructor]
    public FleeIntent(NpcIntentEnvelope envelope, EntityKey? threat)
        : base(envelope, NpcIntentType.Flee) => Threat = threat;

    public EntityKey? Threat { get; }
}

public sealed record CastSkillIntent: NpcIntent
{
    [JsonConstructor]
    public CastSkillIntent(NpcIntentEnvelope envelope, int skillId, int skillLevel, EntityKey? target)
        : base(envelope, NpcIntentType.CastSkill)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(skillId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(skillLevel);
        SkillId = skillId;
        SkillLevel = skillLevel;
        Target = target;
    }

    public int SkillId { get; }
    public int SkillLevel { get; }
    public EntityKey? Target { get; }
}

public sealed record StopCombatIntent: NpcIntent
{
    [JsonConstructor]
    public StopCombatIntent(NpcIntentEnvelope envelope)
        : base(envelope, NpcIntentType.StopCombat)
    {
    }
}

public enum NpcIntentExecutionStatus
{
    Executed = 1,
    Rejected = 2,
    Failed = 3
}

public enum NpcIntentRejectionReason
{
    None = 0,
    InvalidSchema = 1,
    InvalidIntent = 2,
    ActorNotFound = 3,
    GenerationMismatch = 4,
    ActorNotSpawned = 5,
    ActorDead = 6,
    ActorUnsupported = 7,
    InstanceMismatch = 8,
    TargetNotFound = 9,
    TargetDead = 10,
    TargetInvalid = 11,
    TargetChanged = 12,
    OutOfRange = 13,
    Blocked = 14,
    SkillNotFound = 15,
    SkillUnavailable = 16,
    Cooldown = 17,
    InsufficientMana = 18,
    Policy = 19,
    UnsupportedIntent = 20,
    ExecutionFailed = 21
}

public readonly record struct NpcIntentExecutionResult(
    NpcIntentExecutionStatus Status,
    NpcIntentRejectionReason RejectionReason)
{
    public bool IsExecuted => Status == NpcIntentExecutionStatus.Executed;

    public static NpcIntentExecutionResult Executed() =>
        new(NpcIntentExecutionStatus.Executed, NpcIntentRejectionReason.None);

    public static NpcIntentExecutionResult Rejected(NpcIntentRejectionReason reason) =>
        new(NpcIntentExecutionStatus.Rejected, reason);

    public static NpcIntentExecutionResult Failed() =>
        new(NpcIntentExecutionStatus.Failed, NpcIntentRejectionReason.ExecutionFailed);
}

public enum NpcIntentComparisonKind
{
    ExactMatch = 1,
    SemanticMatch = 2,
    Different = 3,
    NotComparable = 4
}
