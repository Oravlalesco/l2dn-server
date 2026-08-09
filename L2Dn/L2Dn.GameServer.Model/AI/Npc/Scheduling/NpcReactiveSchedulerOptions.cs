using System.Globalization;

namespace L2Dn.GameServer.AI.Scheduling;

internal sealed record NpcReactiveSchedulerOptions(
    NpcReactiveSchedulerMode Mode,
    int WorkerCount,
    int QueueCapacity,
    TimeSpan CriticalMinimumInterval,
    TimeSpan CombatMinimumInterval,
    TimeSpan NormalMinimumInterval,
    bool ReactionTelemetryEnabled,
    bool WakeOnAttacked,
    bool WakeOnTargetLost,
    bool WakeOnPlayerRelevant,
    bool WakeOnThreatChanged)
{
    public int EffectiveWorkerCount => WorkerCount == 0
        ? Math.Clamp(Environment.ProcessorCount, 1, 16)
        : WorkerCount;

    public static NpcReactiveSchedulerOptions FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        return new NpcReactiveSchedulerOptions(
            ParseMode(read("NPC_REACTIVE_SCHEDULER_MODE")),
            ParseInt(read("NPC_REACTIVE_WORKER_COUNT"), 0, 0, 256),
            ParseInt(read("NPC_REACTIVE_QUEUE_CAPACITY"), 20_000, 1, 1_000_000),
            TimeSpan.FromMilliseconds(ParseInt(read("NPC_CRITICAL_MIN_THINK_INTERVAL_MS"), 50, 0, 60_000)),
            TimeSpan.FromMilliseconds(ParseInt(read("NPC_COMBAT_MIN_THINK_INTERVAL_MS"), 100, 0, 60_000)),
            TimeSpan.FromMilliseconds(ParseInt(read("NPC_NORMAL_MIN_THINK_INTERVAL_MS"), 500, 0, 60_000)),
            ParseBoolean(read("NPC_REACTION_TELEMETRY_ENABLED"), true),
            ParseBoolean(read("NPC_WAKE_ON_ATTACKED"), true),
            ParseBoolean(read("NPC_WAKE_ON_TARGET_LOST"), true),
            ParseBoolean(read("NPC_WAKE_ON_PLAYER_RELEVANT"), true),
            ParseBoolean(read("NPC_WAKE_ON_THREAT_CHANGED"), true));
    }

    public bool IsReasonEnabled(NpcWakeReason reason) => reason switch
    {
        NpcWakeReason.Attacked => WakeOnAttacked,
        NpcWakeReason.TargetLost or NpcWakeReason.TargetDied => WakeOnTargetLost,
        NpcWakeReason.PlayerBecameRelevant => WakeOnPlayerRelevant,
        NpcWakeReason.ThreatChanged or NpcWakeReason.AllyAttacked or NpcWakeReason.CombatStarted =>
            WakeOnThreatChanged,
        _ => true
    };

    public TimeSpan GetMinimumInterval(NpcThinkPriority priority) => priority switch
    {
        NpcThinkPriority.Critical => CriticalMinimumInterval,
        NpcThinkPriority.Combat => CombatMinimumInterval,
        _ => NormalMinimumInterval
    };

    private static NpcReactiveSchedulerMode ParseMode(string? value)
    {
        string normalized = value?.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        return Enum.TryParse(normalized, true, out NpcReactiveSchedulerMode mode)
            ? mode
            : NpcReactiveSchedulerMode.Disabled;
    }

    private static int ParseInt(string? value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static bool ParseBoolean(string? value, bool fallback) =>
        bool.TryParse(value, out bool parsed) ? parsed : fallback;
}

