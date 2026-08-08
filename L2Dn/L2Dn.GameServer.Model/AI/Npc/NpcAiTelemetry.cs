using System.Diagnostics;
using System.Diagnostics.Metrics;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.TaskManagers;

namespace L2Dn.GameServer.AI.Runtime;

public static class NpcAiTelemetry
{
    public const string MeterName = "L2Dn.GameServer.NpcAI";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

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

    private static readonly object SnapshotLock = new();
    private static StateSnapshot _snapshot = StateSnapshot.Empty;
    private static long _snapshotTimestamp;

    static NpcAiTelemetry()
    {
        Meter.CreateObservableGauge("l2dn.npc.loaded", () => GetStateSnapshot().Loaded, "{npc}", "NPCs registered in the world.");
        Meter.CreateObservableGauge("l2dn.npc.thinking", () => GetStateSnapshot().Thinking, "{npc}", "Attackable NPCs registered in the legacy think scheduler.");
        Meter.CreateObservableGauge("l2dn.npc.combat", () => GetStateSnapshot().Combat, "{npc}", "Attackable NPCs currently in combat.");
        Meter.CreateObservableGauge("l2dn.npc.visible", () => GetStateSnapshot().Visible, "{npc}", "NPCs whose world region has active neighbours.");
        Meter.CreateObservableGauge("l2dn.npc.sleeping", () => GetStateSnapshot().Sleeping, "{npc}", "NPCs whose world region has no active neighbours.");
        Meter.CreateObservableGauge("l2dn.players.online", () => GetStateSnapshot().PlayersOnline, "{player}", "Players currently registered in the world.");
        Meter.CreateObservableGauge("l2dn.npc.intention", () => GetStateSnapshot().Intentions, "{npc}", "Thinking NPCs grouped by current legacy intention.");
        Meter.CreateObservableGauge("l2dn.npc.region.loaded", () => GetStateSnapshot().Regions, "{npc}", "Loaded NPCs grouped by instance and world region.");
    }

    internal static bool ThinkMeasurementsEnabled =>
        ThinkCalls.Enabled || ThinkBusyTime.Enabled || ThinkDuration.Enabled || ThinkAllocations.Enabled;

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

    internal static T ObserveWorldQuery<T>(string operation, Type entityType, Func<T> query)
    {
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
        }
        finally
        {
            CommandCalls.Add(1, new KeyValuePair<string, object?>("command", command),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    private static TagList CreateAiTags(CreatureAI ai, CtrlIntention intention)
    {
        TagList tags = default;
        tags.Add("ai_type", ai.GetType().Name);
        tags.Add("intention", intention.ToString());
        return tags;
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
