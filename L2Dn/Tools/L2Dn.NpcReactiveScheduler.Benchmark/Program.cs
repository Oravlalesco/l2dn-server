using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

int npcCount = GetArgument("--npc-count", 5_000);
int eventCount = GetArgument("--events", 100_000);
int workerCount = GetArgument("--workers", 0);
string pipelineName = GetStringArgument("--pipeline", "Legacy");
if (!Enum.TryParse(pipelineName, true, out BenchmarkPipeline pipeline))
{
    throw new ArgumentException("--pipeline must be Legacy, Shadow, or Intent.");
}
if (npcCount <= 0 || eventCount <= 0 || workerCount < 0)
{
    throw new ArgumentOutOfRangeException(nameof(npcCount), "NPC and event counts must be positive; workers may be zero for automatic sizing.");
}

NpcTemplate template = CreateNpcTemplate();
Attackable[] actors = Enumerable.Range(0, npcCount).Select(_ => CreateActor(template)).ToArray();
List<ScenarioReport> reports = [];

reports.Add(await RunScenarioAsync("R1_one_event_per_npc", actors, npcCount, workerCount, pipeline, false,
    (coordinator, executor) =>
    {
        foreach (Attackable actor in actors)
        {
            executor.RecordWake(coordinator.Wake(actor, NpcWakeReason.PlayerBecameRelevant));
        }
        return npcCount;
    }));

reports.Add(await RunScenarioAsync("R2_ten_events_per_npc", actors, npcCount * 10, workerCount, pipeline, true,
    (coordinator, executor) =>
    {
        foreach (Attackable actor in actors)
        {
            for (int eventIndex = 0; eventIndex < 10; eventIndex++)
            {
                executor.RecordWake(coordinator.Wake(actor, eventIndex % 2 == 0
                    ? NpcWakeReason.Attacked
                    : NpcWakeReason.ThreatChanged));
            }
        }
        return npcCount * 10;
    }));

Attackable[] stormActors = actors.Take(Math.Min(1_000, actors.Length)).ToArray();
reports.Add(await RunScenarioAsync("R3_event_storm", stormActors, stormActors.Length * 100, workerCount, pipeline, true,
    (coordinator, executor) =>
    {
        foreach (Attackable actor in stormActors)
        {
            for (int eventIndex = 0; eventIndex < 100; eventIndex++)
            {
                executor.RecordWake(coordinator.Wake(actor, NpcWakeReason.Attacked));
            }
        }
        return stormActors.Length * 100;
    }));

reports.Add(await RunScenarioAsync("R4_mixed_priority", actors, eventCount, workerCount, pipeline, true,
    (coordinator, executor) =>
    {
        Random random = new(0x25A1);
        for (int index = 0; index < eventCount; index++)
        {
            int roll = random.Next(100);
            NpcWakeReason reason = roll < 5
                ? NpcWakeReason.Attacked
                : roll < 20
                    ? NpcWakeReason.ThreatChanged
                    : NpcWakeReason.PeriodicDue;
            executor.RecordWake(coordinator.Wake(actors[random.Next(actors.Length)], reason));
        }
        return eventCount;
    }));

Attackable[] hotspotActors = actors.Take(Math.Min(500, actors.Length)).ToArray();
reports.Add(await RunScenarioAsync("R5_hotspot_concurrent", hotspotActors, eventCount, workerCount, pipeline, true,
    (coordinator, executor) =>
    {
        Parallel.For(0, eventCount, index =>
        {
            Attackable actor = hotspotActors[index % hotspotActors.Length];
            executor.RecordWake(coordinator.Wake(actor,
                index % 5 == 0 ? NpcWakeReason.Attacked : NpcWakeReason.ThreatChanged));
        });
        return eventCount;
    }));

BenchmarkReport report = new(
    DateTimeOffset.UtcNow,
    npcCount,
    eventCount,
    workerCount == 0 ? Math.Clamp(Environment.ProcessorCount, 1, 16) : workerCount,
    pipeline,
    reports,
    GC.CollectionCount(0),
    GC.CollectionCount(1),
    GC.CollectionCount(2));
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
}));

return;

int GetArgument(string name, int fallback)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int parsed)
        ? parsed
        : fallback;
}

string GetStringArgument(string name, string fallback)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}

static async Task<ScenarioReport> RunScenarioAsync(string name, Attackable[] actors, int expectedEvents,
    int workerCount, BenchmarkPipeline pipeline, bool gateExecution,
    Func<NpcThinkCoordinator, BenchmarkExecutor, int> submit)
{
    BenchmarkExecutor executor = new(gateExecution, pipeline, actors);
    NpcReactiveSchedulerOptions options = new(NpcReactiveSchedulerMode.Enabled, workerCount, 20_000,
        TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, false, true, true, true, true);
    await using NpcThinkCoordinator coordinator = new(options, executor, AlwaysCurrentValidator.Instance);

    executor.WarmUp();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long allocatedBefore = GC.GetTotalAllocatedBytes(true);
    long startedAt = Stopwatch.GetTimestamp();
    int submitted = submit(coordinator, executor);
    if (submitted != expectedEvents)
    {
        throw new InvalidOperationException($"Scenario {name} submitted {submitted} events; expected {expectedEvents}.");
    }

    var queueAtPeak = coordinator.GetRuntimeSnapshot();
    executor.Release();
    bool idle = await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(60));
    TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
    if (!idle)
    {
        throw new TimeoutException($"Scenario {name} did not drain in 60 seconds.");
    }

    long allocated = Math.Max(0, GC.GetTotalAllocatedBytes(true) - allocatedBefore);
    double[] reaction = executor.Contexts.Select(static context =>
        Stopwatch.GetElapsedTime(context.EventTimestamp, context.ThinkStartedTimestamp).TotalMilliseconds).ToArray();
    double[] queueDelay = executor.Contexts.Select(static context =>
        Stopwatch.GetElapsedTime(context.WakeTimestamp, context.ThinkStartedTimestamp).TotalMilliseconds).ToArray();
    double[] criticalReaction = ReactionFor(executor.Contexts, NpcThinkPriority.Critical);
    double[] combatReaction = ReactionFor(executor.Contexts, NpcThinkPriority.Combat);
    double[] normalReaction = ReactionFor(executor.Contexts, NpcThinkPriority.Normal);
    int executions = executor.Contexts.Count;
    double[] pipelineDuration = executor.PipelineDurations.ToArray();
    return new ScenarioReport(
        name,
        actors.Length,
        submitted,
        executions,
        elapsed.TotalMilliseconds,
        Percentile(reaction, 0.50),
        Percentile(reaction, 0.95),
        Percentile(reaction, 0.99),
        Percentile(queueDelay, 0.50),
        Percentile(queueDelay, 0.95),
        Percentile(queueDelay, 0.99),
        Percentile(criticalReaction, 0.95),
        Percentile(criticalReaction, 0.99),
        Percentile(combatReaction, 0.95),
        Percentile(combatReaction, 0.99),
        Percentile(normalReaction, 0.95),
        Percentile(normalReaction, 0.99),
        submitted == 0 ? 0 : 1d - (double)executions / submitted,
        executor.MaximumConcurrentPerNpc,
        queueAtPeak.Critical,
        queueAtPeak.Combat,
        queueAtPeak.Normal,
        queueAtPeak.CriticalOverflow + queueAtPeak.CombatOverflow,
        allocated,
        submitted == 0 ? 0 : allocated / submitted,
        Percentile(pipelineDuration, 0.50),
        Percentile(pipelineDuration, 0.95),
        Percentile(pipelineDuration, 0.99),
        executor.DroppedWakeups);
}

static double[] ReactionFor(IEnumerable<NpcWakeContext> contexts, NpcThinkPriority priority) =>
    contexts.Where(context => context.Priority == priority).Select(static context =>
        Stopwatch.GetElapsedTime(context.EventTimestamp, context.ThinkStartedTimestamp).TotalMilliseconds).ToArray();

static double Percentile(double[] values, double percentile)
{
    if (values.Length == 0)
    {
        return 0;
    }
    Array.Sort(values);
    int index = Math.Clamp((int)Math.Ceiling(values.Length * percentile) - 1, 0, values.Length - 1);
    return values[index];
}

static Attackable CreateActor(NpcTemplate template)
{
    Attackable actor = new(template);
    actor.beginRespawnLifecycle();
    actor.onRespawn();
    actor.completeRespawnLifecycle();
    actor.setSpawned(true);
    return actor;
}

static NpcTemplate CreateNpcTemplate()
{
    StatSet set = new();
    set.set("id", 9_400_001);
    set.set("type", "Monster");
    set.set("name", "Reactive benchmark NPC");
    set.set("baseHpMax", 100d);
    set.set("baseMpMax", 100d);
    return new NpcTemplate(set);
}

internal sealed class AlwaysCurrentValidator: INpcGenerationValidator
{
    public static AlwaysCurrentValidator Instance { get; } = new();
    public bool IsCurrent(NpcKey npc) => true;
}

internal enum BenchmarkPipeline
{
    Legacy,
    Shadow,
    Intent
}

internal sealed class BenchmarkExecutor: INpcThinkExecutor
{
    private readonly bool _gated;
    private readonly BenchmarkPipeline _pipeline;
    private readonly Dictionary<int, NpcPerceptionSnapshot> _perceptions;
    private readonly NpcBrainCoordinator _brain = new();
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<int, int> _activeByNpc = new();
    private int _maximumConcurrentPerNpc;
    private long _droppedWakeups;
    public ConcurrentQueue<NpcWakeContext> Contexts { get; } = new();
    public ConcurrentQueue<double> PipelineDurations { get; } = new();
    public int MaximumConcurrentPerNpc => Volatile.Read(ref _maximumConcurrentPerNpc);
    public long DroppedWakeups => Volatile.Read(ref _droppedWakeups);

    public BenchmarkExecutor(bool gated, BenchmarkPipeline pipeline, IEnumerable<Attackable> actors)
    {
        _gated = gated;
        _pipeline = pipeline;
        _perceptions = actors.ToDictionary(static actor => actor.ObjectId, CreatePerception);
    }

    public void WarmUp()
    {
        KeyValuePair<int, NpcPerceptionSnapshot> first = _perceptions.First();
        ExecutePipeline(first.Value.Envelope.Npc, default);
    }

    public void RecordWake(NpcWakeDisposition disposition)
    {
        if (disposition is NpcWakeDisposition.DroppedNormal or NpcWakeDisposition.StaleGeneration)
        {
            Interlocked.Increment(ref _droppedWakeups);
        }
    }

    public async ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
    {
        int active = _activeByNpc.AddOrUpdate(npc.ObjectId, 1, static (_, current) => current + 1);
        UpdateMaximum(active);
        Contexts.Enqueue(context);
        try
        {
            if (_gated)
            {
                await _gate.Task.WaitAsync(cancellationToken);
            }
            long startedAt = Stopwatch.GetTimestamp();
            ExecutePipeline(npc, context);
            PipelineDurations.Enqueue(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        finally
        {
            _activeByNpc.AddOrUpdate(npc.ObjectId, 0, static (_, current) => current - 1);
        }
    }

    private void ExecutePipeline(NpcKey npc, NpcWakeContext context)
    {
        if (_pipeline == BenchmarkPipeline.Legacy)
        {
            return;
        }

        NpcBrainDecision decision = _brain.Decide(_perceptions[npc.ObjectId],
            new NpcBrainContext((NpcBrainStimulus)(int)context.Reasons));
        if (_pipeline == BenchmarkPipeline.Shadow)
        {
            _ = decision.Intents.FirstOrDefault(); // semantic comparison input, with no world mutation.
            return;
        }

        foreach (NpcIntent intent in decision.Intents)
        {
            if (intent.Envelope.Actor != npc || intent.Envelope.BasedOnStateRevision <= 0 ||
                intent.Envelope.SchemaVersion != NpcIntent.CurrentSchemaVersion)
            {
                throw new InvalidOperationException("Synthetic intent validation failed.");
            }
        }
    }

    private static NpcPerceptionSnapshot CreatePerception(Attackable actor)
    {
        NpcKey npc = new(actor.ObjectId, actor.getSpawnGeneration());
        EntityKey player = new(actor.ObjectId ^ int.MinValue, 0, EntityKind.Player);
        VisibleEntity visible = new(0, player, new NpcPosition(30, 0, 0, 0), 20, 5, 10, 30,
            EntityStateFlags.Alive | EntityStateFlags.Spawned,
            EntityRelationFlags.Player | EntityRelationFlags.Playable | EntityRelationFlags.SameInstance);
        NpcPerceptionState state = new(
            new NpcIdentity(actor.getId(), NpcKind.Monster, LegacyNpcAiType.Fighter, 20, 0, [],
                NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.Aggressive),
            new NpcPhysicalState(default, 100, 100, 100, 100, 5, 10,
                NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned),
            new NpcCombatFacts(null, 40, 500, NpcCombatFlags.None),
            new NpcEnvironment(default, default, true, true, false, false, true, 300, 1500),
            [visible], [], [], []);
        return new NpcPerceptionSnapshot(new NpcPerceptionEnvelope(1, npc, 1, 1, 1), state);
    }

    public void Release() => _gate.TrySetResult();

    private void UpdateMaximum(int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref _maximumConcurrentPerNpc)) &&
               Interlocked.CompareExchange(ref _maximumConcurrentPerNpc, value, current) != current)
        {
        }
    }
}

internal sealed record ScenarioReport(
    string Scenario,
    int NpcCount,
    int Events,
    int ThinkExecutions,
    double TotalMilliseconds,
    double ReactionP50Milliseconds,
    double ReactionP95Milliseconds,
    double ReactionP99Milliseconds,
    double QueueDelayP50Milliseconds,
    double QueueDelayP95Milliseconds,
    double QueueDelayP99Milliseconds,
    double CriticalReactionP95Milliseconds,
    double CriticalReactionP99Milliseconds,
    double CombatReactionP95Milliseconds,
    double CombatReactionP99Milliseconds,
    double NormalReactionP95Milliseconds,
    double NormalReactionP99Milliseconds,
    double CoalescingRatio,
    int MaximumConcurrentThinkPerNpc,
    long PeakCriticalQueueDepth,
    long PeakCombatQueueDepth,
    long PeakNormalQueueDepth,
    int PeakOverflowStates,
    long AllocatedBytes,
    long AllocatedBytesPerEvent,
    double PipelineP50Milliseconds,
    double PipelineP95Milliseconds,
    double PipelineP99Milliseconds,
    long DroppedWakeups);

internal sealed record BenchmarkReport(
    DateTimeOffset CapturedAtUtc,
    int ConfiguredNpcCount,
    int ConfiguredMixedEventCount,
    int EffectiveWorkerCount,
    BenchmarkPipeline Pipeline,
    IReadOnlyCollection<ScenarioReport> Scenarios,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);
