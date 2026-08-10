using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using L2Dn.NpcContracts;

int npcCount = GetArgument("--npc-count", 5_000);
int visiblePerNpc = GetArgument("--visible-per-npc", 30);
if (npcCount <= 0 || visiblePerNpc < 0)
{
    throw new ArgumentOutOfRangeException(nameof(npcCount), "Counts must be positive (visible count may be zero).");
}

CreateSnapshot(0, 1, Math.Min(visiblePerNpc, 2), false);
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

(List<NpcPerceptionSnapshot> full, PhaseResult fullPhase) = Measure("full_snapshot", npcCount, () =>
{
    List<NpcPerceptionSnapshot> snapshots = new(npcCount);
    for (int index = 0; index < npcCount; index++)
    {
        snapshots.Add(CreateSnapshot(index, 1, visiblePerNpc, false));
    }
    return snapshots;
});

(List<NpcPerceptionSnapshot> changed, PhaseResult changedPhase) = Measure("changed_snapshot", npcCount, () =>
{
    List<NpcPerceptionSnapshot> snapshots = new(npcCount);
    for (int index = 0; index < npcCount; index++)
    {
        snapshots.Add(CreateSnapshot(index, 2, visiblePerNpc, true));
    }
    return snapshots;
});

long[] diffTicks = new long[npcCount];
long deltaAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
long deltaStartedAt = Stopwatch.GetTimestamp();
List<NpcPerceptionDelta> deltas = new(npcCount);
for (int index = 0; index < npcCount; index++)
{
    long itemStartedAt = Stopwatch.GetTimestamp();
    NpcPerceptionDelta delta = NpcPerceptionDiff.Create(full[index], changed[index]);
    NpcPerceptionApplyResult applied = NpcPerceptionDeltaApplier.Apply(full[index], delta);
    if (!applied.Applied || !NpcPerceptionExactComparer.Instance.Equals(applied.Snapshot, changed[index]))
    {
        throw new InvalidOperationException($"Delta verification failed for NPC index {index}.");
    }
    deltas.Add(delta);
    diffTicks[index] = Stopwatch.GetTimestamp() - itemStartedAt;
}
PhaseResult deltaPhase = CreatePhase("diff_apply", npcCount, deltaStartedAt, deltaAllocatedBefore, diffTicks);

long jsonBytes = 0;
long codecAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
long codecStartedAt = Stopwatch.GetTimestamp();
const int batchSize = 1_000;
int batchCount = 0;
for (int offset = 0; offset < npcCount; offset += batchSize)
{
    int count = Math.Min(batchSize, npcCount - offset);
    NpcPerceptionRegionBatch batch = new(2, new RegionKey(0, 0, 0), 1, ++batchCount, [],
        deltas.GetRange(offset, count).ToImmutableArray());
    jsonBytes += NpcPerceptionJsonCodec.Serialize(batch).LongLength;
}
PhaseResult codecPhase = CreatePhase("json_codec", batchCount, codecStartedAt, codecAllocatedBefore, null);

BenchmarkReport report = new(
    npcCount,
    visiblePerNpc,
    DateTimeOffset.UtcNow,
    [fullPhase, changedPhase, deltaPhase, codecPhase],
    jsonBytes,
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

static (T Value, PhaseResult Result) Measure<T>(string name, int operations, Func<T> action)
{
    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    long startedAt = Stopwatch.GetTimestamp();
    T value = action();
    return (value, CreatePhase(name, operations, startedAt, allocatedBefore, null));
}

static PhaseResult CreatePhase(string name, int operations, long startedAt, long allocatedBefore, long[]? itemTicks)
{
    TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);
    long allocated = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    double? p50 = itemTicks == null ? null : PercentileMilliseconds(itemTicks, 0.50);
    double? p95 = itemTicks == null ? null : PercentileMilliseconds(itemTicks, 0.95);
    double? p99 = itemTicks == null ? null : PercentileMilliseconds(itemTicks, 0.99);
    return new PhaseResult(name, operations, elapsed.TotalMilliseconds,
        operations == 0 ? 0 : elapsed.TotalMilliseconds / operations, allocated,
        operations == 0 ? 0 : allocated / operations, p50, p95, p99);
}

static double PercentileMilliseconds(long[] ticks, double percentile)
{
    long[] ordered = (long[])ticks.Clone();
    Array.Sort(ordered);
    int index = Math.Clamp((int)Math.Ceiling(ordered.Length * percentile) - 1, 0, ordered.Length - 1);
    return ordered[index] * 1_000d / Stopwatch.Frequency;
}

static NpcPerceptionSnapshot CreateSnapshot(int index, long revision, int visibleCount, bool changed)
{
    NpcKey npc = new(index + 1, 1);
    ImmutableArray<VisibleEntity>.Builder visible = ImmutableArray.CreateBuilder<VisibleEntity>(visibleCount);
    for (int ordinal = 0; ordinal < visibleCount; ordinal++)
    {
        int x = ordinal * 20 + (changed && ordinal == 0 ? 3 : 0);
        visible.Add(new VisibleEntity(ordinal,
            new EntityKey(1_000_000 + index * Math.Max(visibleCount, 1) + ordinal, 0, EntityKind.Player),
            new NpcPosition(x, ordinal * 10, 0, 0), 85, 8, 16, double.Hypot(x, ordinal * 10),
            EntityStateFlags.Alive | EntityStateFlags.Spawned,
            EntityRelationFlags.Player | EntityRelationFlags.SameInstance |
            EntityRelationFlags.AutoAttackable));
    }

    NpcPerceptionState state = new(
        new NpcIdentity(20_000 + index, NpcKind.Monster, LegacyNpcAiType.Fighter, 85, 300, [],
            NpcCapabilities.CanMove | NpcCapabilities.CanAttack),
        new NpcPhysicalState(new NpcPosition(index, index, 0, 0), changed ? 90 : 100, 100, 50, 50, 8, 16,
            NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned),
        new NpcCombatFacts(visibleCount == 0 ? null : visible[0].Entity, 40, 500,
            visibleCount == 0 ? NpcCombatFlags.None : NpcCombatFlags.InCombat),
        new NpcEnvironment(new RegionKey(0, index % 32, index / 32 % 32), null, true, true, false, false, true,
            300, 1500),
        visible.MoveToImmutable(), [], [], []);
    return new NpcPerceptionSnapshot(new NpcPerceptionEnvelope(1, npc, revision, revision, revision), state);
}

internal sealed record PhaseResult(
    string Name,
    int Operations,
    double TotalMilliseconds,
    double MeanMilliseconds,
    long AllocatedBytes,
    long AllocatedBytesPerOperation,
    double? P50Milliseconds,
    double? P95Milliseconds,
    double? P99Milliseconds);

internal sealed record BenchmarkReport(
    int NpcCount,
    int VisibleEntitiesPerNpc,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyCollection<PhaseResult> Phases,
    long JsonPayloadBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);
