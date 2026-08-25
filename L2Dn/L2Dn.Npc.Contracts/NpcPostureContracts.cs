namespace L2Dn.NpcContracts;

/// <summary>
/// Strategic posture of an NPC -- the top-level intent of the adaptive strategy layer.
/// Neutral means the NPC is outside combat and never competes in utility scoring.
/// </summary>
public enum NpcStrategicPosture
{
    Neutral = 0,        // outside combat; excluded from utility competition (4B5-A17)
    Pressure = 1,       // aggressive stance: maximize damage output
    ControlRange = 2,   // maintain optimal distance for ranged/skill usage
    Recover = 3,        // prioritize self-healing and damage reduction
    Disengage = 4       // escape or de-aggro (governed by FleeAllowed policy)
}

/// <summary>
/// Records the specific cause of every posture transition.
/// At least 12 values are required by criterion 4B5-A7.
/// </summary>
public enum NpcPostureTransitionReason
{
    Initial = 0,            // first posture assignment on combat entry
    CombatStarted = 1,      // transition triggered by entering combat
    CombatEnded = 2,        // transition triggered by leaving combat
    LowHealth = 3,          // HP dropped below configured threshold
    HealthRecovered = 4,    // HP recovered above threshold
    TargetInsideRange = 5,  // target entered preferred engagement range
    TargetOutsideRange = 6, // target moved beyond preferred engagement range
    HealAvailable = 7,      // a heal skill became ready
    NoHealAvailable = 8,    // no heal skill is ready
    FleeCondition = 9,      // flee condition met (governed by NpcReflexPolicy)
    SkillReady = 10,        // an offensive/utility skill became ready
    Timeout = 11            // posture commitment expired (PostureStabilityGate)
}

/// <summary>
/// Immutable output of StrategicDirectiveBuilder: communicates the selected posture and
/// tactical biases to TacticalActionEvaluator. Biases are in the range +-1000.
/// This is not an NpcIntent; it does not cross the Brain <-> Gateway boundary.
/// </summary>
public readonly record struct NpcStrategyDirective(
    NpcStrategicPosture Posture,
    int AttackBias,
    int ApproachBias,
    int OffensiveSkillBias,
    int HealBias,
    int FleeBias,
    int RetreatBias,
    int PreferredRange,
    NpcPostureTransitionReason Reason);

/// <summary>
/// Point-in-time snapshot of per-posture utility scores for replay and telemetry.
/// Neutral is excluded because it never participates in utility competition (4B5-A17).
/// Not persisted in NpcBrainState; produced on every Think tick for diagnostics only.
/// </summary>
public readonly record struct NpcPostureUtilities(
    int Pressure,
    int ControlRange,
    int Recover,
    int Disengage);
