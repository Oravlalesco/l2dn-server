using System.Diagnostics;
using System.Diagnostics.Metrics;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.TaskManagers;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

public static class NpcAiTelemetry
{
    public const string MeterName = "L2Dn.GameServer.NpcAI";
    public const string ActivitySourceName = "L2Dn.GameServer.NpcAI";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly ActivitySource Activities = new(ActivitySourceName, "1.0.0");

    private static readonly Counter<long> ThinkCalls =
        Meter.CreateCounter<long>("l2dn.npc.think.calls", "{call}", "NPC AI think callbacks executed.");

    private static readonly Counter<long> ThinkErrors =
        Meter.CreateCounter<long>("l2dn.npc.think.errors", "{error}", "NPC AI think callbacks that failed.");

    private static readonly Counter<double> ThinkBusyTime =
        Meter.CreateCounter<double>("l2dn.npc.think.busy_time", "s", "Wall-clock time spent executing NPC think callbacks.");

    private static readonly Histogram<double> ThinkDuration =
        Meter.CreateHistogram<double>("l2dn.npc.think.duration", "s", "NPC AI think callback duration.");

    private static readonly Histogram<long> ThinkAllocations =
        Meter.CreateHistogram<long>("l2dn.npc.think.allocations", "By", "Managed bytes allocated by an NPC think callback on its worker thread.");

    private static readonly Counter<long> WorldQueryCalls =
        Meter.CreateCounter<long>("l2dn.npc.world_query.calls", "{call}", "World visibility queries made by legacy NPC AI.");

    private static readonly Histogram<double> WorldQueryDuration =
        Meter.CreateHistogram<double>("l2dn.npc.world_query.duration", "s", "Legacy NPC world visibility query duration.");

    private static readonly Counter<long> GeoQueryCalls =
        Meter.CreateCounter<long>("l2dn.npc.geo_query.calls", "{call}", "Geodata queries made by legacy NPC AI.");

    private static readonly Histogram<double> GeoQueryDuration =
        Meter.CreateHistogram<double>("l2dn.npc.geo_query.duration", "s", "Legacy NPC geodata query duration.");

    private static readonly Counter<long> PathfindingCalls =
        Meter.CreateCounter<long>("l2dn.pathfinding.calls", "{call}", "GameServer pathfinding calls.");

    private static readonly Histogram<double> PathfindingDuration =
        Meter.CreateHistogram<double>("l2dn.pathfinding.duration", "s", "GameServer pathfinding duration.");

    private static readonly Counter<long> CommandCalls =
        Meter.CreateCounter<long>("l2dn.npc.command.calls", "{call}", "Commands executed by the legacy NPC command adapter.");

    private static readonly Counter<long> GuardPursuitResets =
        Meter.CreateCounter<long>("l2dn.npc.guard.pursuit_reset", "{reset}", "Guard pursuits ended after the legacy attack timeout.");

    private static readonly Counter<long> PerceptionCaptureCalls =
        Meter.CreateCounter<long>("l2dn.npc.perception.capture.count", "{capture}", "NPC perception captures completed.");

    private static readonly Histogram<double> PerceptionCaptureDuration =
        Meter.CreateHistogram<double>("l2dn.npc.perception.capture.duration", "s", "NPC perception capture duration.");

    private static readonly Histogram<long> PerceptionCaptureAllocations =
        Meter.CreateHistogram<long>("l2dn.npc.perception.alloc.bytes", "By", "Managed bytes allocated while capturing NPC perception.");

    private static readonly Histogram<long> PerceptionEstimatedBytes =
        Meter.CreateHistogram<long>("l2dn.npc.perception.estimated_payload_bytes", "By", "Estimated logical NPC perception payload size.");

    private static readonly Histogram<long> PerceptionVisibleEntities =
        Meter.CreateHistogram<long>("l2dn.npc.perception.visible_entities", "{entity}", "Visible entities copied into NPC perception.");

    private static readonly Histogram<long> PerceptionThreatEntries =
        Meter.CreateHistogram<long>("l2dn.npc.perception.threat_entries", "{entry}", "Threat entries copied into NPC perception.");

    private static readonly Counter<long> PerceptionFullSnapshots =
        Meter.CreateCounter<long>("l2dn.npc.perception.full.count", "{snapshot}", "Full NPC perception snapshots published.");

    private static readonly Counter<long> PerceptionDeltas =
        Meter.CreateCounter<long>("l2dn.npc.perception.delta.count", "{delta}", "NPC perception deltas published.");

    private static readonly Histogram<long> PerceptionDeltaBytes =
        Meter.CreateHistogram<long>("l2dn.npc.perception.delta.bytes", "By", "Estimated logical NPC perception delta size.");

    private static readonly Histogram<double> PerceptionCompressionRatio =
        Meter.CreateHistogram<double>("l2dn.npc.perception.compression_ratio", "1", "Estimated delta-to-full logical payload ratio.");

    private static readonly Counter<long> PerceptionRevisionGaps =
        Meter.CreateCounter<long>("l2dn.npc.perception.revision_gap", "{gap}", "NPC perception delta revision gaps detected.");

    private static readonly Counter<long> PerceptionCaptureFailures =
        Meter.CreateCounter<long>("l2dn.npc.perception.capture.failures", "{failure}", "NPC perception captures rejected or failed.");

    private static readonly Counter<long> PerceptionValidationMismatches =
        Meter.CreateCounter<long>("l2dn.npc.perception.validation.mismatch", "{mismatch}", "NPC perception validation mismatches.");

    private static readonly Counter<long> LegacyEntityResolves =
        Meter.CreateCounter<long>("l2dn.npc.perception.legacy_resolve.count", "{resolve}", "Snapshot entity keys resolved back to legacy world objects.");

    private static readonly Histogram<double> PoolIterationDuration =
        Meter.CreateHistogram<double>("l2dn.npc.scheduler.pool_iteration.duration", "s", "Legacy NPC scheduler pool iteration duration.");

    private static readonly Counter<long> PoolIterationOverruns =
        Meter.CreateCounter<long>("l2dn.npc.scheduler.pool_iteration.overrun", "{overrun}", "Legacy NPC scheduler pool iterations exceeding their one-second cadence.");

    private static readonly Counter<long> PerceptionBatches =
        Meter.CreateCounter<long>("l2dn.npc.perception.batch.count", "{batch}", "Partial region perception batches published in process.");

    private static readonly Counter<long> PerceptionBatchConsumerFailures =
        Meter.CreateCounter<long>("l2dn.npc.perception.batch.consumer_failures", "{failure}", "Failures isolated from in-process perception batch consumers.");

    private static readonly Counter<long> PerceptionReplayDrops =
        Meter.CreateCounter<long>("l2dn.npc.perception.replay.dropped", "{batch}", "Replay batches dropped to keep the scheduler non-blocking.");

    private static readonly Counter<long> PerceptionReplayFailures =
        Meter.CreateCounter<long>("l2dn.npc.perception.replay.failures", "{failure}", "Replay recorder initialization or write failures.");

    private static readonly Counter<long> Wakeups =
        Meter.CreateCounter<long>("l2dn.npc.wakeup.total", "{wakeup}", "NPC wake-up requests received.");

    private static readonly Counter<long> WakeupsCoalesced =
        Meter.CreateCounter<long>("l2dn.npc.wakeup.coalesced", "{wakeup}", "NPC wake-ups merged into pending work.");

    private static readonly Counter<long> WakeupsDropped =
        Meter.CreateCounter<long>("l2dn.npc.wakeup.dropped", "{wakeup}", "NPC wake-ups dropped or invalidated under explicit policy.");

    private static readonly Histogram<double> SchedulerQueueDelay =
        Meter.CreateHistogram<double>("l2dn.npc.scheduler.queue_delay", "s", "Time between NPC wake request and think start.");

    private static readonly Histogram<double> LegacyEventToPeriodicDelay =
        Meter.CreateHistogram<double>("l2dn.npc.reaction.legacy_periodic_delay", "s", "Observed event-to-next-periodic-think delay in legacy mode.");

    private static readonly Histogram<double> ReactionLatency =
        Meter.CreateHistogram<double>("l2dn.npc.reaction.latency", "s", "Time between a relevant event and first command execution.");

    private static readonly Counter<long> SingleFlightCollisions =
        Meter.CreateCounter<long>("l2dn.npc.scheduler.singleflight.collision", "{collision}", "Wake-ups received while an NPC think was running.");

    private static readonly Counter<long> PendingFollowups =
        Meter.CreateCounter<long>("l2dn.npc.think.pending_followup", "{followup}", "Follow-up thinks scheduled after coalescing events during execution.");

    private static readonly Counter<long> SchedulerExecutionFailures =
        Meter.CreateCounter<long>("l2dn.npc.scheduler.execution.failure", "{failure}", "NPC think executor failures isolated by scheduler workers.");

    private static readonly Counter<long> BrainDecisions =
        Meter.CreateCounter<long>("l2dn.npc.brain.decision.count", "{decision}", "Local NPC Brain decisions completed.");

    private static readonly Histogram<double> BrainDecisionDuration =
        Meter.CreateHistogram<double>("l2dn.npc.brain.decision.duration", "s", "Local NPC Brain decision duration.");

    private static readonly Histogram<double> BrainReflexDuration =
        Meter.CreateHistogram<double>("l2dn.npc.brain.reflex.duration", "s", "Reflex Brain decision duration.");

    private static readonly Histogram<double> BrainTacticalDuration =
        Meter.CreateHistogram<double>("l2dn.npc.brain.tactical.duration", "s", "Tactical Brain decision duration.");

    private static readonly Counter<long> IntentsCreated =
        Meter.CreateCounter<long>("l2dn.npc.intent.created", "{intent}", "NPC intents created by a Brain.");

    private static readonly Counter<long> IntentsExecuted =
        Meter.CreateCounter<long>("l2dn.npc.intent.executed", "{intent}", "NPC intents authorized and executed.");

    private static readonly Counter<long> IntentsRejected =
        Meter.CreateCounter<long>("l2dn.npc.intent.rejected", "{intent}", "NPC intents rejected by the authoritative gateway.");

    private static readonly Histogram<double> IntentValidationDuration =
        Meter.CreateHistogram<double>("l2dn.npc.intent.validation.duration", "s", "NPC intent validation and execution duration.");

    private static readonly Counter<long> BrainShadowMatches =
        Meter.CreateCounter<long>("l2dn.npc.brain.shadow.match", "{comparison}", "Shadow Brain decisions matching legacy commands.");

    private static readonly Counter<long> BrainShadowDiffs =
        Meter.CreateCounter<long>("l2dn.npc.brain.shadow.diff", "{comparison}", "Shadow Brain decisions differing from legacy commands.");

    private static readonly Counter<long> StrategyEvaluations =
        Meter.CreateCounter<long>("l2dn.npc.strategy.evaluation.total", "{evaluation}", "Strategy evaluations completed.");

    private static readonly Histogram<double> StrategyEvaluationDuration =
        Meter.CreateHistogram<double>("l2dn.npc.strategy.evaluation.duration", "s", "Strategy decision pipeline duration.");

    private static readonly Counter<long> StrategyProfilesResolved =
        Meter.CreateCounter<long>("l2dn.npc.strategy.profile.resolved", "{profile}", "Strategy profiles resolved for evaluation.");

    private static readonly Counter<long> StrategyModifiersApplied =
        Meter.CreateCounter<long>("l2dn.npc.strategy.modifier.applied", "{modifier}", "Strategy modifiers applied to baseline preferences.");

    private static readonly Counter<long> StrategyActionsSelected =
        Meter.CreateCounter<long>("l2dn.npc.strategy.action.selected", "{action}", "Actions selected by Strategy evaluation.");

    private static readonly Counter<long> StrategyShadowComparisons =
        Meter.CreateCounter<long>("l2dn.npc.strategy.shadow.comparison", "{comparison}", "Causal Shadow comparisons by semantic outcome.");

    private static readonly Counter<long> StrategyShadowChangedDecisions =
        Meter.CreateCounter<long>("l2dn.npc.strategy.shadow.changed_decision", "{decision}", "Shadow evaluations whose semantic decision changed.");

    private static readonly Counter<long> StrategyFallbacks =
        Meter.CreateCounter<long>("l2dn.npc.strategy.fallback", "{fallback}", "Safe Strategy fallbacks by bounded reason.");

    private static readonly object SnapshotLock = new();
    private static StateSnapshot _snapshot = StateSnapshot.Empty;
    private static long _snapshotTimestamp;
    private static int _perceptionMode;
    private static int _reactiveSchedulerMode;
    private static int _brainMode;
    private static int _strategyMode;
    private static long _criticalQueueDepth;
    private static long _combatQueueDepth;
    private static long _normalQueueDepth;
    private static long _activeReactiveWorkers;

    // Meter keeps observable instruments weakly referenced. Retain them for the
    // lifetime of the process so infrequently observed mode/queue series remain
    // available to the OTLP collection cycle.
    private static readonly Instrument[] ObservableInstruments;

    static NpcAiTelemetry()
    {
        ObservableInstruments =
        [
            Meter.CreateObservableGauge("l2dn.npc.loaded", () => GetStateSnapshot().Loaded, "{npc}", "NPCs registered in the world."),
            Meter.CreateObservableGauge("l2dn.npc.thinking", () => GetStateSnapshot().Thinking, "{npc}", "Attackable NPCs registered in the legacy think scheduler."),
            Meter.CreateObservableGauge("l2dn.npc.combat", () => GetStateSnapshot().Combat, "{npc}", "Attackable NPCs currently in combat."),
            Meter.CreateObservableGauge("l2dn.npc.visible", () => GetStateSnapshot().Visible, "{npc}", "NPCs whose world region has active neighbours."),
            Meter.CreateObservableGauge("l2dn.npc.sleeping", () => GetStateSnapshot().Sleeping, "{npc}", "NPCs whose world region has no active neighbours."),
            Meter.CreateObservableGauge("l2dn.players.online", () => GetStateSnapshot().PlayersOnline, "{player}", "Players currently registered in the world."),
            Meter.CreateObservableGauge("l2dn.npc.intention", () => GetStateSnapshot().Intentions, "{npc}", "Thinking NPCs grouped by current legacy intention."),
            Meter.CreateObservableGauge("l2dn.npc.region.loaded", () => GetStateSnapshot().Regions, "{npc}", "Loaded NPCs grouped by instance and world region."),
            Meter.CreateObservableGauge("l2dn.npc.perception.mode", () => new Measurement<long>(1,
                new KeyValuePair<string, object?>("mode", ((NpcPerceptionMode)Volatile.Read(ref _perceptionMode)).ToString())),
                "{mode}", "Configured NPC perception operating mode."),
            Meter.CreateObservableGauge("l2dn.npc.scheduler.reactive.mode", () => new Measurement<long>(1,
                new KeyValuePair<string, object?>("mode", ((NpcReactiveSchedulerMode)Volatile.Read(ref _reactiveSchedulerMode)).ToString())),
                "{mode}", "Configured NPC reactive scheduler operating mode."),
            Meter.CreateObservableGauge("l2dn.npc.brain.mode", () => new Measurement<long>(1,
                new KeyValuePair<string, object?>("mode", ((NpcBrainMode)Volatile.Read(ref _brainMode)).ToString())),
                "{mode}", "Configured NPC Brain operating mode."),
            Meter.CreateObservableGauge("l2dn.npc.strategy.mode", () => new Measurement<long>(1,
                new KeyValuePair<string, object?>("mode", ToStrategyModeTag(
                    (NpcStrategyMode)Volatile.Read(ref _strategyMode)))),
                "{mode}", "Effective NPC Strategy operating mode."),
            Meter.CreateObservableGauge("l2dn.npc.scheduler.queue.depth", ObserveQueueDepth,
                "{wakeup}", "Current NPC reactive queue depth by priority."),
            Meter.CreateObservableGauge("l2dn.npc.scheduler.active_workers", () => Volatile.Read(ref _activeReactiveWorkers),
                "{worker}", "NPC reactive workers currently executing a think.")
        ];
    }

    internal static bool ThinkMeasurementsEnabled =>
        ThinkCalls.Enabled || ThinkBusyTime.Enabled || ThinkDuration.Enabled || ThinkAllocations.Enabled;

    internal static Activity? StartThinkActivity(CreatureAI ai, CtrlIntention intention) =>
        Activities.StartActivity("npc.think", ActivityKind.Internal, default(ActivityContext),
            [new KeyValuePair<string, object?>("ai_type", ai.GetType().Name),
                new KeyValuePair<string, object?>("intention", intention.ToString())]);

    internal static Activity? StartPerceptionActivity(NpcPerceptionMode mode) =>
        Activities.StartActivity("npc.perception.capture", ActivityKind.Internal, default(ActivityContext),
            [new KeyValuePair<string, object?>("mode", mode.ToString())]);

    internal static Activity? StartDecisionActivity(CreatureAI ai) =>
        Activities.StartActivity("npc.decision", ActivityKind.Internal, default(ActivityContext),
            [new KeyValuePair<string, object?>("ai_type", ai.GetType().Name)]);

    internal static void RecordThink(CreatureAI ai, CtrlIntention intention, long startedAt, long allocatedBytesBefore)
    {
        double elapsedSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
        long allocatedBytes = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore);
        TagList tags = CreateAiTags(ai, intention);

        ThinkCalls.Add(1, tags);
        ThinkBusyTime.Add(elapsedSeconds, tags);
        ThinkDuration.Record(elapsedSeconds, tags);
        ThinkAllocations.Record(allocatedBytes, tags);
    }

    internal static void RecordThinkError(CreatureAI ai, CtrlIntention intention)
    {
        TagList tags = CreateAiTags(ai, intention);
        ThinkErrors.Add(1, tags);
    }

    internal static void RecordGuardPursuitReset(string outcome) =>
        GuardPursuitResets.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    internal static void SetPerceptionMode(NpcPerceptionMode mode) =>
        Volatile.Write(ref _perceptionMode, (int)mode);

    internal static void SetReactiveSchedulerMode(NpcReactiveSchedulerMode mode) =>
        Volatile.Write(ref _reactiveSchedulerMode, (int)mode);

    internal static void SetBrainMode(NpcBrainMode mode) => Volatile.Write(ref _brainMode, (int)mode);

    internal static void SetStrategyMode(NpcStrategyMode mode) =>
        Volatile.Write(ref _strategyMode, (int)mode);

    internal static NpcBrainDecision ObserveBrainDecision(Func<NpcBrainDecision> decide)
    {
        using Activity? activity = Activities.StartActivity("npc.brain.decide", ActivityKind.Internal);
        long startedAt = Stopwatch.GetTimestamp();
        NpcBrainDecision decision = decide();
        RecordBrainDecision(decision, Stopwatch.GetElapsedTime(startedAt));
        activity?.SetTag("brain.layer", decision.Layer.ToString());
        activity?.SetTag("intent.count", decision.Intents.Length);
        return decision;
    }

    internal static void RecordBrainDecision(NpcBrainDecision decision, TimeSpan duration)
    {
        double elapsed = duration.TotalSeconds;
        TagList tags = default;
        tags.Add("layer", decision.Layer.ToString());
        tags.Add("strategy", decision.Strategy.HasValue
            ? ToStrategyProfileTag(decision.Strategy.Value)
            : "disabled");
        tags.Add("outcome", decision.Intents.IsEmpty ? "no_intent" : "intent");
        BrainDecisions.Add(1, tags);
        BrainDecisionDuration.Record(elapsed, tags);
        if (decision.Layer == NpcBrainLayer.Reflex)
        {
            BrainReflexDuration.Record(elapsed, tags);
        }
        else if (decision.Layer == NpcBrainLayer.Tactical)
        {
            BrainTacticalDuration.Record(elapsed, tags);
        }
        foreach (NpcIntent intent in decision.Intents)
        {
            TagList intentTags = default;
            intentTags.Add("intent_type", intent.Envelope.IntentType.ToString());
            intentTags.Add("intent_policy", GetIntentPolicyTag(intent));
            IntentsCreated.Add(1, intentTags);
        }
    }

    internal static NpcIntentExecutionResult ObserveIntentExecution(NpcIntent intent,
        Func<NpcIntentExecutionResult> execute)
    {
        using Activity? activity = Activities.StartActivity("npc.intent.validate", ActivityKind.Internal);
        activity?.SetTag("intent.type", intent.Envelope.IntentType.ToString());
        activity?.SetTag("intent.policy", GetIntentPolicyTag(intent));
        long startedAt = Stopwatch.GetTimestamp();
        NpcIntentExecutionResult result;
        try
        {
            result = execute();
        }
        catch
        {
            result = NpcIntentExecutionResult.Failed();
        }

        TagList tags = default;
        tags.Add("intent_type", intent.Envelope.IntentType.ToString());
        tags.Add("intent_policy", GetIntentPolicyTag(intent));
        tags.Add("status", result.Status.ToString());
        tags.Add("reason", ToRejectionTag(result.RejectionReason));
        IntentValidationDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        if (result.IsExecuted)
        {
            IntentsExecuted.Add(1, tags);
        }
        else
        {
            IntentsRejected.Add(1, tags);
        }
        activity?.SetTag("intent.status", result.Status.ToString());
        activity?.SetTag("intent.rejection_reason", ToRejectionTag(result.RejectionReason));
        return result;
    }

    private static string GetIntentPolicyTag(NpcIntent intent) => intent switch
    {
        AcquireTargetIntent acquire => acquire.Mode.ToString(),
        ApproachTargetIntent approach => approach.Constraint.ToString(),
        ReturnHomeIntent returnHome => returnHome.Mode.ToString(),
        _ => "default"
    };

    internal static void RecordShadowComparison(NpcIntentComparisonKind comparison)
    {
        KeyValuePair<string, object?> tag = new("comparison", comparison.ToString());
        if (comparison is NpcIntentComparisonKind.ExactMatch or NpcIntentComparisonKind.SemanticMatch)
        {
            BrainShadowMatches.Add(1, tag);
        }
        else
        {
            BrainShadowDiffs.Add(1, tag);
        }
    }

    internal static void RecordStrategyEvaluation(NpcStrategyDecisionSummary decision,
        TimeSpan duration, NpcStrategyMode mode, bool succeeded = true)
    {
        string profile = ToStrategyProfileTag(decision.Profile);
        TagList tags = default;
        tags.Add("profile", profile);
        tags.Add("mode", ToStrategyModeTag(mode));
        tags.Add("outcome", succeeded ? "success" : "failure");
        StrategyEvaluations.Add(1, tags);
        StrategyEvaluationDuration.Record(duration.TotalSeconds, tags);
        StrategyProfilesResolved.Add(1, new KeyValuePair<string, object?>("profile", profile));

        string? action = ToStrategyActionTag(decision.SelectedAction);
        if (action != null)
        {
            StrategyActionsSelected.Add(1,
                new KeyValuePair<string, object?>("profile", profile),
                new KeyValuePair<string, object?>("action", action));
        }
        RecordStrategyModifiers(profile, decision.AppliedModifiers);
    }

    internal static void RecordStrategyEvaluationFailure(NpcStrategyArchetype profile,
        TimeSpan duration, NpcStrategyMode mode)
    {
        NpcStrategyDecisionSummary failed = new(profile, NpcStrategyAction.None,
            NpcStrategyDecisionReason.None, NpcStrategyModifierFlags.None);
        RecordStrategyEvaluation(failed, duration, mode, false);
    }

    internal static void RecordStrategyShadowComparison(NpcStrategyArchetype profile,
        StrategyComparisonResult comparison)
    {
        string profileTag = ToStrategyProfileTag(profile);
        string comparisonTag = ToStrategyComparisonTag(comparison.Kind);
        StrategyShadowComparisons.Add(1,
            new KeyValuePair<string, object?>("profile", profileTag),
            new KeyValuePair<string, object?>("comparison", comparisonTag));
        if (comparison.ChangedDecision)
        {
            StrategyShadowChangedDecisions.Add(1,
                new KeyValuePair<string, object?>("profile", profileTag),
                new KeyValuePair<string, object?>("comparison", comparisonTag));
        }
    }

    internal static void RecordStrategyFallback(string reason) => StrategyFallbacks.Add(1,
        new KeyValuePair<string, object?>("reason", reason));

    private static void RecordStrategyModifiers(string profile, NpcStrategyModifierFlags modifiers)
    {
        for (int bit = 0; bit < 8; bit++)
        {
            NpcStrategyModifierFlags modifier = (NpcStrategyModifierFlags)(1 << bit);
            if ((modifiers & modifier) == 0)
            {
                continue;
            }
            StrategyModifiersApplied.Add(1,
                new KeyValuePair<string, object?>("profile", profile),
                new KeyValuePair<string, object?>("modifier", ToStrategyModifierTag(modifier)));
        }
    }

    private static string ToStrategyProfileTag(NpcStrategyArchetype profile) => profile switch
    {
        NpcStrategyArchetype.AggressivePressure => "aggressive_pressure",
        NpcStrategyArchetype.RangedControl => "ranged_control",
        NpcStrategyArchetype.Survival => "survival",
        _ => "balanced"
    };

    private static string? ToStrategyActionTag(NpcStrategyAction action) => action switch
    {
        NpcStrategyAction.BasicAttack => "attack",
        NpcStrategyAction.Approach => "approach",
        NpcStrategyAction.OffensiveSkill => "offensive_skill",
        NpcStrategyAction.Heal => "heal",
        NpcStrategyAction.Flee => "flee",
        _ => null
    };

    private static string ToStrategyComparisonTag(NpcStrategyComparisonKind comparison) => comparison switch
    {
        NpcStrategyComparisonKind.ExactMatch => "exact_match",
        NpcStrategyComparisonKind.SemanticMatch => "semantic_match",
        NpcStrategyComparisonKind.DifferentAction => "different_action",
        NpcStrategyComparisonKind.DifferentTarget => "different_target",
        NpcStrategyComparisonKind.DifferentSkill => "different_skill",
        NpcStrategyComparisonKind.DifferentMovement => "different_movement",
        _ => "not_comparable"
    };

    private static string ToStrategyModeTag(NpcStrategyMode mode) => mode switch
    {
        NpcStrategyMode.Shadow => "shadow",
        NpcStrategyMode.Enabled => "enabled",
        _ => "disabled"
    };

    private static string ToStrategyModifierTag(NpcStrategyModifierFlags modifier) => modifier switch
    {
        NpcStrategyModifierFlags.BasicAttackScore => "attack_score",
        NpcStrategyModifierFlags.ApproachScore => "approach_score",
        NpcStrategyModifierFlags.OffensiveSkillScore => "offensive_skill_score",
        NpcStrategyModifierFlags.HealScore => "heal_score",
        NpcStrategyModifierFlags.FleeScore => "flee_score",
        NpcStrategyModifierFlags.HealHpPercent => "heal_hp_percent",
        NpcStrategyModifierFlags.FleeHpPercent => "flee_hp_percent",
        NpcStrategyModifierFlags.PreferredRange => "preferred_range",
        _ => "none"
    };

    private static string ToRejectionTag(NpcIntentRejectionReason reason) => reason switch
    {
        NpcIntentRejectionReason.None => "none",
        NpcIntentRejectionReason.GenerationMismatch => "generation_mismatch",
        NpcIntentRejectionReason.ActorDead => "dead_actor",
        NpcIntentRejectionReason.TargetDead => "dead_target",
        NpcIntentRejectionReason.TargetInvalid => "invalid_target",
        NpcIntentRejectionReason.OutOfRange => "out_of_range",
        NpcIntentRejectionReason.Cooldown => "cooldown",
        NpcIntentRejectionReason.InsufficientMana => "insufficient_mana",
        _ => reason.ToString().ToLowerInvariant()
    };

    internal static void RecordWakeup(NpcWakeReason reasons, NpcThinkPriority priority,
        NpcReactiveSchedulerMode mode, NpcWakeDisposition disposition)
    {
        if (!Wakeups.Enabled && !WakeupsCoalesced.Enabled && !WakeupsDropped.Enabled)
        {
            return;
        }
        TagList tags = CreateWakeTags(reasons, priority, mode);
        tags.Add("disposition", disposition.ToString());
        Wakeups.Add(1, tags);
        if (disposition == NpcWakeDisposition.Coalesced)
        {
            WakeupsCoalesced.Add(1, tags);
        }
        else if (disposition is NpcWakeDisposition.DroppedNormal or NpcWakeDisposition.StaleGeneration)
        {
            WakeupsDropped.Add(1, tags);
        }
    }

    internal static void RecordQueueDelay(NpcWakeContext context, TimeSpan delay)
    {
        if (!SchedulerQueueDelay.Enabled)
        {
            return;
        }
        SchedulerQueueDelay.Record(delay.TotalSeconds,
            CreateWakeTags(context.Reasons, context.Priority,
                (NpcReactiveSchedulerMode)Volatile.Read(ref _reactiveSchedulerMode)));
    }

    internal static void RecordLegacyEventToPeriodicDelay(NpcWakeReason reasons, TimeSpan delay)
    {
        if (!LegacyEventToPeriodicDelay.Enabled)
        {
            return;
        }
        LegacyEventToPeriodicDelay.Record(delay.TotalSeconds,
            new KeyValuePair<string, object?>("reason", reasons.ToString()));
    }

    internal static void RecordReactionLatency(NpcWakeContext context, TimeSpan delay)
    {
        if (!ReactionLatency.Enabled)
        {
            return;
        }
        ReactionLatency.Record(delay.TotalSeconds,
            CreateWakeTags(context.Reasons, context.Priority,
                (NpcReactiveSchedulerMode)Volatile.Read(ref _reactiveSchedulerMode)));
    }

    internal static void RecordSingleFlightCollision() => SingleFlightCollisions.Add(1);

    internal static void RecordPendingFollowup() => PendingFollowups.Add(1);

    internal static void RecordSchedulerExecutionFailure() => SchedulerExecutionFailures.Add(1);

    internal static void SetQueueDepth(NpcThinkPriority priority, long value)
    {
        switch (priority)
        {
            case NpcThinkPriority.Critical:
                Volatile.Write(ref _criticalQueueDepth, value);
                break;
            case NpcThinkPriority.Combat:
                Volatile.Write(ref _combatQueueDepth, value);
                break;
            default:
                Volatile.Write(ref _normalQueueDepth, value);
                break;
        }
    }

    internal static void ReactiveWorkerStarted() => Interlocked.Increment(ref _activeReactiveWorkers);

    internal static void ReactiveWorkerCompleted() => Interlocked.Decrement(ref _activeReactiveWorkers);

    internal static void RecordPerceptionCapture(NpcPerceptionSnapshot snapshot, NpcPerceptionDelta? delta,
        NpcPerceptionPublicationKind publication, bool semanticStateChanged, NpcPerceptionMode mode,
        long startedAt, long allocatedBytesBefore)
    {
        TagList tags = default;
        tags.Add("mode", mode.ToString());
        tags.Add("publication", publication.ToString());
        tags.Add("semantic_change", semanticStateChanged);
        PerceptionCaptureCalls.Add(1, tags);
        PerceptionCaptureDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        PerceptionCaptureAllocations.Record(
            Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore), tags);
        PerceptionVisibleEntities.Record(snapshot.State.VisibleEntities.Length, tags);
        PerceptionThreatEntries.Record(snapshot.State.Threats.Length, tags);
        PerceptionEstimatedBytes.Record(EstimatePayloadBytes(snapshot), tags);
        if (publication == NpcPerceptionPublicationKind.Full)
        {
            PerceptionFullSnapshots.Add(1, tags);
        }
        else if (publication == NpcPerceptionPublicationKind.Delta && delta != null)
        {
            long fullBytes = EstimatePayloadBytes(snapshot);
            long deltaBytes = EstimateDeltaBytes(delta);
            PerceptionDeltas.Add(1, tags);
            PerceptionDeltaBytes.Record(deltaBytes, tags);
            PerceptionCompressionRatio.Record(fullBytes == 0 ? 1 : (double)deltaBytes / fullBytes, tags);
        }
    }

    internal static void RecordPerceptionCaptureFailure(string reason, NpcPerceptionMode mode) =>
        PerceptionCaptureFailures.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("mode", mode.ToString()));

    internal static void RecordPerceptionValidationMismatch(string field, string validationKind) =>
        PerceptionValidationMismatches.Add(1,
            new KeyValuePair<string, object?>("field", field),
            new KeyValuePair<string, object?>("validation_kind", validationKind));

    internal static void RecordLegacyResolve() => LegacyEntityResolves.Add(1);

    internal static void RecordPerceptionRevisionGap() => PerceptionRevisionGaps.Add(1);

    internal static void RecordPoolIteration(int npcCount, long startedAt)
    {
        double elapsedSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
        KeyValuePair<string, object?> countBand = new("npc_count_band", npcCount switch
        {
            0 => "empty",
            <= 250 => "1_250",
            <= 500 => "251_500",
            <= 750 => "501_750",
            _ => "751_1000"
        });
        PoolIterationDuration.Record(elapsedSeconds, countBand);
        if (elapsedSeconds > 1)
        {
            PoolIterationOverruns.Add(1, countBand);
        }
    }

    internal static void RecordPerceptionBatch(NpcPerceptionRegionBatch batch)
    {
        TagList tags = default;
        tags.Add("source_pool_id", batch.SourcePoolId);
        tags.Add("instance_id", batch.Region.InstanceId);
        tags.Add("region_x", batch.Region.RegionX);
        tags.Add("region_y", batch.Region.RegionY);
        tags.Add("full_count", batch.FullSnapshots.Length);
        tags.Add("delta_count", batch.Deltas.Length);
        PerceptionBatches.Add(1, tags);
    }

    internal static void RecordPerceptionBatchConsumerFailure() => PerceptionBatchConsumerFailures.Add(1);

    internal static void RecordPerceptionReplayDrop() => PerceptionReplayDrops.Add(1);

    internal static void RecordPerceptionReplayFailure() => PerceptionReplayFailures.Add(1);

    private static long EstimatePayloadBytes(NpcPerceptionSnapshot snapshot)
    {
        const int EnvelopeAndScalarEstimate = 256;
        const int VisibleEntityEstimate = 96;
        const int ThreatEntryEstimate = 56;
        const int AffordanceEstimate = 24;
        const int SpatialEstimate = 24;
        const int SkillEstimate = 32;
        const int ClanIdEstimate = sizeof(int);
        return EnvelopeAndScalarEstimate +
               snapshot.State.VisibleEntities.Length * VisibleEntityEstimate +
               snapshot.State.Threats.Length * ThreatEntryEstimate +
               snapshot.State.Affordances.Length * AffordanceEstimate +
               snapshot.State.SpatialObservations.Length * SpatialEstimate +
               snapshot.State.Skills.Length * SkillEstimate +
               snapshot.State.Identity.ClanIds.Length * ClanIdEstimate;
    }

    private static long EstimateDeltaBytes(NpcPerceptionDelta delta)
    {
        long bytes = 96;
        if (delta.Identity != null) bytes += 64 + delta.Identity.ClanIds.Length * sizeof(int);
        if (delta.Physical != null) bytes += 96;
        if (delta.Combat != null) bytes += 48;
        if (delta.Environment != null) bytes += 64;
        if (delta.VisibleEntities != null)
        {
            bytes += delta.VisibleEntities.Added.Length * 96L;
            bytes += delta.VisibleEntities.Updated.Length * 96L;
            bytes += delta.VisibleEntities.Removed.Length * 24L;
        }
        bytes += delta.Threats.Length * 56L;
        bytes += delta.Affordances.Length * 24L;
        bytes += delta.SpatialObservations.Length * 24L;
        bytes += delta.Skills.Length * 32L;
        return bytes;
    }

    internal static T ObserveWorldQuery<T>(string operation, Type entityType, Func<T> query)
    {
        using Activity? activity = Activities.StartActivity("npc.world.query", ActivityKind.Internal);
        activity?.SetTag("operation", operation);
        activity?.SetTag("entity_type", entityType.Name);
        if (!WorldQueryCalls.Enabled && !WorldQueryDuration.Enabled)
        {
            return query();
        }

        long startedAt = Stopwatch.GetTimestamp();
        try
        {
            return query();
        }
        finally
        {
            TagList tags = default;
            tags.Add("operation", operation);
            tags.Add("entity_type", entityType.Name);
            WorldQueryCalls.Add(1, tags);
            WorldQueryDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        }
    }

    internal static T ObserveGeoQuery<T>(string operation, Func<T> query, Func<T, string> outcomeSelector)
    {
        using Activity? activity = Activities.StartActivity("npc.geo.query", ActivityKind.Internal);
        activity?.SetTag("operation", operation);
        if (!GeoQueryCalls.Enabled && !GeoQueryDuration.Enabled)
        {
            return query();
        }

        long startedAt = Stopwatch.GetTimestamp();
        string outcome = "error";
        try
        {
            T result = query();
            outcome = outcomeSelector(result);
            activity?.SetTag("outcome", outcome);
            return result;
        }
        finally
        {
            TagList tags = default;
            tags.Add("operation", operation);
            tags.Add("outcome", outcome);
            GeoQueryCalls.Add(1, tags);
            GeoQueryDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        }
    }

    internal static T? ObservePathfinding<T>(string actorKind, Func<T?> query)
        where T: class
    {
        using Activity? activity = Activities.StartActivity("npc.pathfinding", ActivityKind.Internal);
        activity?.SetTag("actor_kind", actorKind);
        if (!PathfindingCalls.Enabled && !PathfindingDuration.Enabled)
        {
            return query();
        }

        long startedAt = Stopwatch.GetTimestamp();
        string outcome = "error";
        try
        {
            T? result = query();
            outcome = result is null ? "no_path" : "success";
            activity?.SetTag("outcome", outcome);
            return result;
        }
        finally
        {
            TagList tags = default;
            tags.Add("actor_kind", actorKind);
            tags.Add("outcome", outcome);
            PathfindingCalls.Add(1, tags);
            PathfindingDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        }
    }

    internal static void ObserveCommand(string command, Action action)
    {
        NpcReactionTracker.CommandStarted();
        using Activity? activity = Activities.StartActivity("npc.command.execute", ActivityKind.Internal);
        activity?.SetTag("command", command);
        if (!CommandCalls.Enabled)
        {
            action();
            return;
        }

        string outcome = "error";
        try
        {
            action();
            outcome = "success";
            activity?.SetTag("outcome", outcome);
        }
        finally
        {
            CommandCalls.Add(1, new KeyValuePair<string, object?>("command", command),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    internal static T ObserveThreatQuery<T>(string operation, Func<T> query)
    {
        using Activity? activity = Activities.StartActivity("npc.threat.query", ActivityKind.Internal);
        activity?.SetTag("operation", operation);
        return query();
    }

    private static TagList CreateAiTags(CreatureAI ai, CtrlIntention intention)
    {
        TagList tags = default;
        tags.Add("ai_type", ai.GetType().Name);
        tags.Add("intention", intention.ToString());
        return tags;
    }

    private static TagList CreateWakeTags(NpcWakeReason reasons, NpcThinkPriority priority,
        NpcReactiveSchedulerMode mode)
    {
        TagList tags = default;
        tags.Add("reason", reasons.ToString());
        tags.Add("priority", priority.ToString());
        tags.Add("mode", mode.ToString());
        return tags;
    }

    private static IEnumerable<Measurement<long>> ObserveQueueDepth()
    {
        yield return new Measurement<long>(Volatile.Read(ref _criticalQueueDepth),
            new KeyValuePair<string, object?>("priority", NpcThinkPriority.Critical.ToString()));
        yield return new Measurement<long>(Volatile.Read(ref _combatQueueDepth),
            new KeyValuePair<string, object?>("priority", NpcThinkPriority.Combat.ToString()));
        yield return new Measurement<long>(Volatile.Read(ref _normalQueueDepth),
            new KeyValuePair<string, object?>("priority", NpcThinkPriority.Normal.ToString()));
    }

    private static StateSnapshot GetStateSnapshot()
    {
        long now = Stopwatch.GetTimestamp();
        StateSnapshot snapshot = Volatile.Read(ref _snapshot);
        if (_snapshotTimestamp != 0 && Stopwatch.GetElapsedTime(_snapshotTimestamp, now) < TimeSpan.FromSeconds(1))
        {
            return snapshot;
        }

        lock (SnapshotLock)
        {
            now = Stopwatch.GetTimestamp();
            if (_snapshotTimestamp != 0 && Stopwatch.GetElapsedTime(_snapshotTimestamp, now) < TimeSpan.FromSeconds(1))
            {
                return _snapshot;
            }

            try
            {
                snapshot = BuildStateSnapshot();
                Volatile.Write(ref _snapshot, snapshot);
                Volatile.Write(ref _snapshotTimestamp, now);
            }
            catch
            {
                // Observable callbacks must never affect the GameServer.
            }

            return snapshot;
        }
    }

    private static StateSnapshot BuildStateSnapshot()
    {
        Npc[] loadedNpcs = World.getInstance().getVisibleObjects().OfType<Npc>().ToArray();
        Attackable[] thinkingNpcs = AttackableThinkTaskManager.getInstance().GetAttackablesSnapshot();

        long visible = loadedNpcs.LongCount(npc => npc.getWorldRegion().AreNeighborsActive);
        long sleeping = loadedNpcs.LongLength - visible;
        long combat = loadedNpcs.OfType<Attackable>().LongCount(npc => npc.isInCombat());

        Measurement<long>[] intentions = thinkingNpcs
            .Where(static npc => npc.hasAI())
            .GroupBy(static npc => npc.getAI().getIntention())
            .Select(static group => new Measurement<long>(group.LongCount(),
                new KeyValuePair<string, object?>("intention", group.Key.ToString())))
            .ToArray();

        Measurement<long>[] regions = loadedNpcs
            .GroupBy(static npc => (npc.getInstanceId(), npc.getWorldRegion().RegionX, npc.getWorldRegion().RegionY))
            .Select(static group => new Measurement<long>(group.LongCount(),
                new KeyValuePair<string, object?>("instance_id", group.Key.Item1),
                new KeyValuePair<string, object?>("region_x", group.Key.Item2),
                new KeyValuePair<string, object?>("region_y", group.Key.Item3)))
            .ToArray();

        return new StateSnapshot(loadedNpcs.LongLength, thinkingNpcs.LongLength, combat, visible, sleeping,
            World.getInstance().getPlayers().Count, intentions, regions);
    }

    private sealed record StateSnapshot(
        long Loaded,
        long Thinking,
        long Combat,
        long Visible,
        long Sleeping,
        long PlayersOnline,
        IReadOnlyCollection<Measurement<long>> Intentions,
        IReadOnlyCollection<Measurement<long>> Regions)
    {
        public static StateSnapshot Empty { get; } = new(0, 0, 0, 0, 0, 0, [], []);
    }
}
