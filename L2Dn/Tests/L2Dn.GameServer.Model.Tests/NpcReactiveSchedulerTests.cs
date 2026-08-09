using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.Model.Tests;

public class NpcReactiveSchedulerTests
{
    private static int _nextTemplateId = 9_300_000;

    [Theory]
    [InlineData("Disabled", NpcReactiveSchedulerMode.Disabled)]
    [InlineData("OBSERVE", NpcReactiveSchedulerMode.Observe)]
    [InlineData("shadow", NpcReactiveSchedulerMode.Shadow)]
    [InlineData("Enabled", NpcReactiveSchedulerMode.Enabled)]
    [InlineData("invalid", NpcReactiveSchedulerMode.Disabled)]
    public void Reactive_mode_parses_operational_configuration(string configured,
        NpcReactiveSchedulerMode expected)
    {
        NpcReactiveSchedulerOptions options = NpcReactiveSchedulerOptions.FromEnvironment(name => name switch
        {
            "NPC_REACTIVE_SCHEDULER_MODE" => configured,
            "NPC_REACTIVE_WORKER_COUNT" => "3",
            "NPC_REACTIVE_QUEUE_CAPACITY" => "99",
            "NPC_CRITICAL_MIN_THINK_INTERVAL_MS" => "11",
            "NPC_COMBAT_MIN_THINK_INTERVAL_MS" => "22",
            "NPC_NORMAL_MIN_THINK_INTERVAL_MS" => "33",
            "NPC_WAKE_ON_PLAYER_RELEVANT" => "false",
            _ => null
        });

        options.Mode.Should().Be(expected);
        options.WorkerCount.Should().Be(3);
        options.QueueCapacity.Should().Be(99);
        options.CriticalMinimumInterval.Should().Be(TimeSpan.FromMilliseconds(11));
        options.CombatMinimumInterval.Should().Be(TimeSpan.FromMilliseconds(22));
        options.NormalMinimumInterval.Should().Be(TimeSpan.FromMilliseconds(33));
        options.WakeOnPlayerRelevant.Should().BeFalse();
    }

    [Fact]
    public async Task Concurrent_wakeups_are_single_flight_and_coalesce_to_one_followup()
    {
        Attackable actor = CreateSpawnedAttackable();
        BlockingExecutor executor = new();
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 4);

        coordinator.Wake(actor, NpcWakeReason.PeriodicDue);
        await executor.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Parallel.For(0, 1_000, _ => coordinator.Wake(actor, NpcWakeReason.Attacked));
        executor.ReleaseFirst.TrySetResult();

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.ExecutionCount.Should().Be(2);
        executor.MaximumConcurrent.Should().Be(1);
    }

    [Fact]
    public async Task Event_arriving_during_think_is_not_lost_or_run_concurrently()
    {
        Attackable actor = CreateSpawnedAttackable();
        BlockingExecutor executor = new();
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 2);

        coordinator.Wake(actor, NpcWakeReason.PeriodicDue);
        await executor.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Wake(actor, NpcWakeReason.TargetLost);
        executor.ReleaseFirst.TrySetResult();

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Contexts.Should().HaveCount(2);
        executor.Contexts[1].Reasons.Should().HaveFlag(NpcWakeReason.TargetLost);
        executor.MaximumConcurrent.Should().Be(1);
    }

    [Fact]
    public async Task Critical_work_bypasses_a_normal_backlog()
    {
        Attackable blocker = CreateSpawnedAttackable();
        Attackable critical = CreateSpawnedAttackable();
        Attackable[] normal = Enumerable.Range(0, 20).Select(_ => CreateSpawnedAttackable()).ToArray();
        OrderedBlockingExecutor executor = new(blocker.ObjectId);
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 1);

        coordinator.Wake(blocker, NpcWakeReason.PeriodicDue);
        await executor.BlockerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        foreach (Attackable actor in normal)
        {
            coordinator.Wake(actor, NpcWakeReason.PeriodicDue);
        }
        coordinator.Wake(critical, NpcWakeReason.Attacked);
        executor.ReleaseBlocker.TrySetResult();

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Order.Take(2).Should().Equal(blocker.ObjectId, critical.ObjectId);
    }

    [Fact]
    public async Task Weighted_dequeue_prevents_normal_starvation()
    {
        Attackable blocker = CreateSpawnedAttackable();
        Attackable normal = CreateSpawnedAttackable();
        Attackable[] critical = Enumerable.Range(0, 30).Select(_ => CreateSpawnedAttackable()).ToArray();
        OrderedBlockingExecutor executor = new(blocker.ObjectId);
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 1);

        coordinator.Wake(blocker, NpcWakeReason.Attacked);
        await executor.BlockerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        foreach (Attackable actor in critical)
        {
            coordinator.Wake(actor, NpcWakeReason.Attacked);
        }
        coordinator.Wake(normal, NpcWakeReason.PeriodicDue);
        executor.ReleaseBlocker.TrySetResult();

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Order.IndexOf(normal.ObjectId).Should().BeLessThan(executor.Order.Count - 1);
    }

    [Fact]
    public async Task Stale_generation_is_discarded_before_execution()
    {
        Attackable actor = CreateSpawnedAttackable();
        RecordingExecutor executor = new();
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, validator: new NeverCurrentValidator());

        coordinator.Wake(actor, NpcWakeReason.Attacked);

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Contexts.Should().BeEmpty();
    }

    [Fact]
    public async Task Respawn_invalidates_an_old_generation_while_it_waits_in_queue()
    {
        Attackable blocker = CreateSpawnedAttackable();
        Attackable stale = CreateSpawnedAttackable();
        OrderedBlockingExecutor executor = new(blocker.ObjectId);
        ActorGenerationValidator validator = new(blocker, stale);
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 1,
            validator: validator);

        coordinator.Wake(blocker, NpcWakeReason.Attacked);
        await executor.BlockerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Wake(stale, NpcWakeReason.Attacked);
        stale.beginRespawnLifecycle();
        stale.onRespawn();
        stale.completeRespawnLifecycle();
        executor.ReleaseBlocker.TrySetResult();

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Order.Should().NotContain(stale.ObjectId);
    }

    [Fact]
    public async Task Executor_failure_is_isolated_and_worker_processes_next_npc()
    {
        Attackable failing = CreateSpawnedAttackable();
        Attackable succeeding = CreateSpawnedAttackable();
        FailOneExecutor executor = new(failing.ObjectId);
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 1);

        coordinator.Wake(failing, NpcWakeReason.Attacked);
        coordinator.Wake(succeeding, NpcWakeReason.Attacked);

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Successful.Should().Contain(succeeding.ObjectId);
    }

    [Fact]
    public async Task Event_storm_has_bounded_runtime_state_and_queue_entries()
    {
        Attackable hot = CreateSpawnedAttackable();
        BlockingExecutor executor = new();
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 1, queueCapacity: 8);

        coordinator.Wake(hot, NpcWakeReason.Attacked);
        await executor.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Parallel.For(0, 100_000, index => coordinator.Wake(hot,
            index % 2 == 0 ? NpcWakeReason.Attacked : NpcWakeReason.ThreatChanged));

        var snapshot = coordinator.GetRuntimeSnapshot();
        snapshot.States.Should().Be(1);
        (snapshot.Critical + snapshot.Combat + snapshot.Normal).Should().BeLessThanOrEqualTo(1);
        executor.ReleaseFirst.TrySetResult();
        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.ExecutionCount.Should().Be(2);
        executor.MaximumConcurrent.Should().Be(1);
    }

    [Fact]
    public async Task Remove_invalidates_queued_work()
    {
        Attackable blocker = CreateSpawnedAttackable();
        Attackable removed = CreateSpawnedAttackable();
        OrderedBlockingExecutor executor = new(blocker.ObjectId);
        await using NpcThinkCoordinator coordinator = CreateCoordinator(executor, workerCount: 1);

        coordinator.Wake(blocker, NpcWakeReason.Attacked);
        await executor.BlockerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Wake(removed, NpcWakeReason.Attacked);
        coordinator.Remove(removed.ObjectId);
        executor.ReleaseBlocker.TrySetResult();

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Order.Should().NotContain(removed.ObjectId);
    }

    [Fact]
    public async Task Disabled_mode_is_inert_and_preserves_the_direct_legacy_rollback_path()
    {
        Attackable actor = CreateSpawnedAttackable();
        RecordingExecutor executor = new();
        NpcReactiveSchedulerOptions options = CreateOptions(NpcReactiveSchedulerMode.Disabled);
        await using NpcThinkCoordinator coordinator = new(options, executor, AlwaysCurrentValidator.Instance);

        coordinator.Wake(actor, NpcWakeReason.Attacked).Should().Be(NpcWakeDisposition.Ignored);
        coordinator.GetRuntimeSnapshot().States.Should().Be(0);
        executor.Contexts.Should().BeEmpty();
    }

    [Fact]
    public async Task Observe_mode_records_wakes_without_executing_think()
    {
        Attackable actor = CreateSpawnedAttackable();
        RecordingExecutor executor = new();
        NpcReactiveSchedulerOptions options = CreateOptions(NpcReactiveSchedulerMode.Observe);
        await using NpcThinkCoordinator coordinator = new(options, executor, AlwaysCurrentValidator.Instance);

        coordinator.Wake(actor, NpcWakeReason.PlayerBecameRelevant).Should().Be(NpcWakeDisposition.Observed);
        coordinator.ObservePeriodic(actor);

        executor.Contexts.Should().BeEmpty();
    }

    [Fact]
    public async Task Shadow_mode_exercises_scheduler_without_calling_gameplay_executor()
    {
        Attackable actor = CreateSpawnedAttackable();
        RecordingExecutor executor = new();
        NpcReactiveSchedulerOptions options = CreateOptions(NpcReactiveSchedulerMode.Shadow);
        await using NpcThinkCoordinator coordinator = new(options, executor, AlwaysCurrentValidator.Instance);

        coordinator.Wake(actor, NpcWakeReason.Attacked);

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        executor.Contexts.Should().BeEmpty();
    }

    [Fact]
    public async Task Critical_interval_overrides_the_longer_normal_interval()
    {
        Attackable actor = CreateSpawnedAttackable();
        RecordingExecutor executor = new();
        NpcReactiveSchedulerOptions options = CreateOptions(NpcReactiveSchedulerMode.Enabled) with
        {
            CriticalMinimumInterval = TimeSpan.FromMilliseconds(10),
            NormalMinimumInterval = TimeSpan.FromSeconds(2)
        };
        await using NpcThinkCoordinator coordinator = new(options, executor, AlwaysCurrentValidator.Instance);

        coordinator.Wake(actor, NpcWakeReason.PeriodicDue);
        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        long startedAt = Stopwatch.GetTimestamp();
        coordinator.Wake(actor, NpcWakeReason.Attacked);

        (await coordinator.WaitForIdleAsync(TimeSpan.FromSeconds(1))).Should().BeTrue();
        Stopwatch.GetElapsedTime(startedAt).Should().BeLessThan(TimeSpan.FromSeconds(1));
        executor.Contexts.Should().HaveCount(2);
    }

    private static NpcThinkCoordinator CreateCoordinator(INpcThinkExecutor executor, int workerCount = 2,
        int queueCapacity = 128, INpcGenerationValidator? validator = null)
    {
        NpcReactiveSchedulerOptions options = new(NpcReactiveSchedulerMode.Enabled, workerCount, queueCapacity,
            TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, true, true, true, true, true);
        return new NpcThinkCoordinator(options, executor, validator ?? AlwaysCurrentValidator.Instance);
    }

    private static NpcReactiveSchedulerOptions CreateOptions(NpcReactiveSchedulerMode mode) =>
        new(mode, 1, 128, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
            true, true, true, true, true);

    private static Attackable CreateSpawnedAttackable()
    {
        Attackable actor = new(CreateNpcTemplate());
        actor.beginRespawnLifecycle();
        actor.onRespawn();
        actor.completeRespawnLifecycle();
        actor.setSpawned(true);
        return actor;
    }

    private static NpcTemplate CreateNpcTemplate()
    {
        StatSet set = new();
        set.set("id", Interlocked.Increment(ref _nextTemplateId));
        set.set("type", "Monster");
        set.set("name", "Reactive test NPC");
        set.set("baseHpMax", 100d);
        set.set("baseMpMax", 100d);
        return new NpcTemplate(set);
    }

    private sealed class AlwaysCurrentValidator: INpcGenerationValidator
    {
        public static AlwaysCurrentValidator Instance { get; } = new();
        public bool IsCurrent(NpcKey npc) => true;
    }

    private sealed class NeverCurrentValidator: INpcGenerationValidator
    {
        public bool IsCurrent(NpcKey npc) => false;
    }

    private sealed class ActorGenerationValidator(params Attackable[] actors): INpcGenerationValidator
    {
        private readonly IReadOnlyDictionary<int, Attackable> _actors =
            actors.ToDictionary(static actor => actor.ObjectId);

        public bool IsCurrent(NpcKey npc) => _actors.TryGetValue(npc.ObjectId, out Attackable? actor) &&
            actor.getSpawnGeneration() == npc.Generation;
    }

    private sealed class RecordingExecutor: INpcThinkExecutor
    {
        public ConcurrentQueue<NpcWakeContext> Contexts { get; } = new();
        public ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
        {
            Contexts.Enqueue(context);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingExecutor: INpcThinkExecutor
    {
        private int _active;
        private int _maximumConcurrent;
        private int _executionCount;
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<NpcWakeContext> Contexts { get; } = [];
        public int MaximumConcurrent => Volatile.Read(ref _maximumConcurrent);
        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public async ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
        {
            int active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            int execution = Interlocked.Increment(ref _executionCount);
            lock (Contexts)
            {
                Contexts.Add(context);
            }
            try
            {
                if (execution == 1)
                {
                    FirstStarted.TrySetResult();
                    await ReleaseFirst.Task.WaitAsync(cancellationToken);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void UpdateMaximum(int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref _maximumConcurrent)) &&
                   Interlocked.CompareExchange(ref _maximumConcurrent, value, current) != current)
            {
            }
        }
    }

    private sealed class OrderedBlockingExecutor(int blockerObjectId): INpcThinkExecutor
    {
        public TaskCompletionSource BlockerStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseBlocker { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Order { get; } = [];

        public async ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
        {
            lock (Order)
            {
                Order.Add(npc.ObjectId);
            }
            if (npc.ObjectId == blockerObjectId)
            {
                BlockerStarted.TrySetResult();
                await ReleaseBlocker.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class FailOneExecutor(int failingObjectId): INpcThinkExecutor
    {
        public ConcurrentBag<int> Successful { get; } = [];
        public ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken)
        {
            if (npc.ObjectId == failingObjectId)
            {
                throw new InvalidOperationException("Expected scheduler isolation test failure.");
            }
            Successful.Add(npc.ObjectId);
            return ValueTask.CompletedTask;
        }
    }
}
