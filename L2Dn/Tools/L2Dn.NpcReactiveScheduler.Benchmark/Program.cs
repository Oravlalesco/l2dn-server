using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.NpcContracts;

int npcCount = GetArgument("--npc-count", 5_000);
int eventCount = GetArgument("--events", 100_000);
int workerCount = GetArgument("--workers", 0);
if (npcCount <= 0 || eventCount <= 0 || workerCount < 0)
{
    throw new ArgumentOutOfRangeException(nameof(npcCount), "NPC and event counts must be positive; workers may be zero for automatic sizing.");
}

NpcTemplate template = CreateNpcTemplate();
Attackable[] actors = Enumerable.Range(0, npcCount).Select(_ => CreateActor(template)).ToArray();
List<ScenarioReport> reports = [];

reports.Add(await RunScenarioAsync("R1_one_event_per_npc", actors, npcCount, workerCount, false,
    (coordinator, executor) =>
    {
        foreach (Attackable actor in actors)
        {
            coordinator.Wake(actor, NpcWakeReason.PlayerBecameRelevant);
        }
        return npcCount;
    }));

reports.Add(await RunScenarioAsync("R2_ten_events_per_npc", actors, npcCount * 10, workerCount, true,
    (coordinator, executor) =>
    {
        foreach (Attackable actor in actors)
        {
            for (int eventIndex = 0; eventIndex < 10; eventIndex++)
            {
                coordinator.Wake(actor, eventIndex % 2 == 0
                    ? NpcWakeReason.Attacked
                    : NpcWakeReason.ThreatChanged);
            }
        }
        return npcCount * 10;
    }));

Attackable[] stormActors = actors.Take(Math.Min(1_000, actors.Length)).ToArray();
reports.Add(await RunScenarioAsync("R3_event_storm", stormActors, stormActors.Length * 100, workerCount, true,
    (coordinator, executor) =>
    {
        foreach (Attackable actor in stormActors)
        {
            for (int eventIndex = 0; eventIndex < 100; eventIndex++)
            {
                coordinator.Wake(actor, NpcWakeReason.Attacked);
            }
        }
        return stormActors.Length * 100;
    }));

reports.Add(await RunScenarioAsync("R4_mixed_priority", actors, eventCount, workerCount, true,
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
            coordinator.Wake(actors[random.Next(actors.Length)], reason);
        }
        return eventCount;
    }));

Attackable[] hotspotActors = actors.Take(Math.Min(500, actors.Length)).ToArray();
reports.Add(await RunScenarioAsync("R5_hotspot_concurrent", hotspotActors, eventCount, workerCount, true,
    (coordinator, executor) =>
    {
        Parallel.For(0, eventCount, index =>
        {
            Attackable actor = hotspotActors[index % hotspotActors.Length];
            coordinator.Wake(actor, index % 5 == 0 ? NpcWakeReason.Attacked : NpcWakeReason.ThreatChanged);
        });
        return eventCount;
    }));

BenchmarkReport report = new(
    DateTimeOffset.UtcNow,
    npcCount,
    eventCount,
    workerCount == 0 ? Math.Clamp(Environment.ProcessorCount, 1, 16) : workerCount,
    reports,
    GC.CollectionCount(0),
    GC.CollectionCount(1),
    GC.CollectionCount(2));
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

return;

int GetArgument(string name, int fallback)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int parsed)
        ? parsed
        : fallback;
}

static async Task<ScenarioReport> RunScenarioAsync(string name, Attackable[] actors, int expectedEvents,
    int workerCount, bool gateExecution, Func<NpcThinkCoordinator, BenchmarkExecutor, int> submit)
{
    BenchmarkExecutor executor = new(gateExecution);
    NpcReactiveSchedulerOptions options = new(NpcReactiveSchedulerMode.Enabled, workerCount, 20_000,
        TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, false, true, true, true, true);
    await using NpcThinkCoordinator coordinator = new(options, executor, AlwaysCurrentValidator.Instance);

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
        submitted == 0 ? 0 : allocated / submitted);
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

internal sealed class BenchmarkExecutor(bool gated): INpcThinkExecutor
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<int, int> _activeByNpc = new();
    private int _maximumConcurrentPerNpc;
    public ConcurrentQueue<NpcWakeContext> Contexts { get; } = new();
    public int MaximumConcurrentPerNpc => Volatile.Read(ref _maximumConcurrentPerNpc);

    public async ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
    {
        int active = _activeByNpc.AddOrUpdate(npc.ObjectId, 1, static (_, current) => current + 1);
        UpdateMaximum(active);
        Contexts.Enqueue(context);
        try
        {
            if (gated)
            {
                await _gate.Task.WaitAsync(cancellationToken);
            }
        }
        finally
        {
            _activeByNpc.AddOrUpdate(npc.ObjectId, 0, static (_, current) => current - 1);
        }
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
    long AllocatedBytesPerEvent);

internal sealed record BenchmarkReport(
    DateTimeOffset CapturedAtUtc,
    int ConfiguredNpcCount,
    int ConfiguredMixedEventCount,
    int EffectiveWorkerCount,
    IReadOnlyCollection<ScenarioReport> Scenarios,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);
