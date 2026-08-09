using System.Collections.Concurrent;
using System.Threading.Channels;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Scheduling;

internal sealed class NpcThinkCoordinator: IAsyncDisposable
{
    private readonly NpcReactiveSchedulerOptions _options;
    private readonly INpcThinkExecutor _executor;
    private readonly INpcGenerationValidator _generationValidator;
    private readonly INpcThinkClock _clock;
    private readonly ConcurrentDictionary<int, NpcThinkRuntimeState> _states = new();
    private readonly ConcurrentDictionary<NpcKey, NpcThinkRuntimeState> _criticalOverflow = new();
    private readonly ConcurrentDictionary<NpcKey, NpcThinkRuntimeState> _combatOverflow = new();
    private readonly Channel<NpcThinkQueueEntry> _critical;
    private readonly Channel<NpcThinkQueueEntry> _combat;
    private readonly Channel<NpcThinkQueueEntry> _normal;
    private readonly SemaphoreSlim _available = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly NpcThinkDelayQueue? _delays;
    private readonly Task[] _workers;
    private long _criticalDepth;
    private long _combatDepth;
    private long _normalDepth;
    private long _dequeueSequence;
    private int _disposed;

    public static NpcThinkCoordinator Instance { get; } = new(
        NpcReactiveSchedulerOptions.FromEnvironment(), LegacyNpcThinkExecutor.Instance,
        LegacyNpcThinkExecutor.Instance);

    internal NpcThinkCoordinator(NpcReactiveSchedulerOptions options, INpcThinkExecutor executor,
        INpcGenerationValidator generationValidator, INpcThinkClock? clock = null)
    {
        _options = options;
        _executor = options.Mode == NpcReactiveSchedulerMode.Shadow ? NoOpNpcThinkExecutor.Instance : executor;
        _generationValidator = generationValidator;
        _clock = clock ?? StopwatchNpcThinkClock.Instance;

        int criticalCapacity = Math.Max(1, options.QueueCapacity / 4);
        int combatCapacity = Math.Max(1, options.QueueCapacity / 4);
        int normalCapacity = Math.Max(1, options.QueueCapacity - criticalCapacity - combatCapacity);
        _critical = CreateQueue(criticalCapacity);
        _combat = CreateQueue(combatCapacity);
        _normal = CreateQueue(normalCapacity);
        NpcAiTelemetry.SetReactiveSchedulerMode(options.Mode);

        if (options.Mode is NpcReactiveSchedulerMode.Shadow or NpcReactiveSchedulerMode.Enabled)
        {
            _delays = new NpcThinkDelayQueue(_clock, OnDelayDue);
            _workers = Enumerable.Range(0, options.EffectiveWorkerCount)
                .Select(_ => Task.Run(WorkerAsync))
                .ToArray();
        }
        else
        {
            _workers = [];
        }
    }

    public NpcReactiveSchedulerMode Mode => _options.Mode;

    internal (long Critical, long Combat, long Normal, int States, int CriticalOverflow, int CombatOverflow)
        GetRuntimeSnapshot() =>
        (Volatile.Read(ref _criticalDepth), Volatile.Read(ref _combatDepth),
            Volatile.Read(ref _normalDepth), _states.Count, _criticalOverflow.Count, _combatOverflow.Count);

    public NpcWakeDisposition Wake(Attackable actor, NpcWakeReason reason, long eventTimestamp = 0,
        int sourcePoolId = 0)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (_options.Mode == NpcReactiveSchedulerMode.Disabled || !_options.IsReasonEnabled(reason))
        {
            return NpcWakeDisposition.Ignored;
        }

        long now = _clock.GetTimestamp();
        eventTimestamp = eventTimestamp == 0 ? now : eventTimestamp;
        NpcKey key = new(actor.ObjectId, actor.getSpawnGeneration());
        NpcThinkPriority priority = NpcWakePriorities.For(reason);
        NpcThinkRuntimeState state = GetOrReplaceState(key);

        if (_options.Mode == NpcReactiveSchedulerMode.Observe)
        {
            lock (state.SyncRoot)
            {
                state.ObservedReasons |= reason;
                state.EarliestObservedEventTimestamp = Earlier(
                    state.EarliestObservedEventTimestamp, eventTimestamp);
            }
            NpcAiTelemetry.RecordWakeup(reason, priority, _options.Mode, NpcWakeDisposition.Observed);
            return NpcWakeDisposition.Observed;
        }

        NpcWakeDisposition disposition;
        lock (state.SyncRoot)
        {
            if (state.Removed || state.Npc != key)
            {
                disposition = NpcWakeDisposition.StaleGeneration;
            }
            else
            {
                state.PendingReasons |= reason;
                state.PendingPriority = Max(state.PendingPriority, priority);
                state.EarliestEventTimestamp = Earlier(state.EarliestEventTimestamp, eventTimestamp);
                state.EarliestWakeTimestamp = Earlier(state.EarliestWakeTimestamp, now);
                if (sourcePoolId != 0)
                {
                    state.SourcePoolId = sourcePoolId;
                }

                if (state.IsRunning)
                {
                    NpcAiTelemetry.RecordSingleFlightCollision();
                    disposition = NpcWakeDisposition.Coalesced;
                }
                else if (state.IsQueued)
                {
                    disposition = priority > state.QueuedPriority
                        ? Promote(state, priority)
                        : NpcWakeDisposition.Coalesced;
                }
                else
                {
                    disposition = QueueNew(state, priority);
                }
            }
        }

        NpcAiTelemetry.RecordWakeup(reason, priority, _options.Mode, disposition);
        return disposition;
    }

    public void ObservePeriodic(Attackable actor)
    {
        if (_options.Mode != NpcReactiveSchedulerMode.Observe ||
            !_states.TryGetValue(actor.ObjectId, out NpcThinkRuntimeState? state))
        {
            return;
        }

        NpcWakeReason reasons;
        long eventTimestamp;
        lock (state.SyncRoot)
        {
            if (state.Npc.Generation != actor.getSpawnGeneration())
            {
                state.Removed = true;
                _states.TryRemove(actor.ObjectId, out _);
                return;
            }

            reasons = state.ObservedReasons;
            eventTimestamp = state.EarliestObservedEventTimestamp;
            state.ObservedReasons = NpcWakeReason.None;
            state.EarliestObservedEventTimestamp = 0;
        }

        if (reasons != NpcWakeReason.None && eventTimestamp != 0)
        {
            NpcAiTelemetry.RecordLegacyEventToPeriodicDelay(reasons,
                _clock.GetElapsedTime(eventTimestamp, _clock.GetTimestamp()));
        }
    }

    public void Remove(int objectId)
    {
        if (!_states.TryRemove(objectId, out NpcThinkRuntimeState? state))
        {
            return;
        }

        lock (state.SyncRoot)
        {
            state.Removed = true;
            state.IsQueued = false;
            state.PendingReasons = NpcWakeReason.None;
            state.QueueVersion++;
        }
        _criticalOverflow.TryRemove(state.Npc, out _);
        _combatOverflow.TryRemove(state.Npc, out _);
    }

    internal async Task<bool> WaitForIdleAsync(TimeSpan timeout)
    {
        long deadline = _clock.Add(_clock.GetTimestamp(), timeout);
        while (_clock.GetTimestamp() < deadline)
        {
            bool busy = _states.Values.Any(static state =>
            {
                lock (state.SyncRoot)
                {
                    return state.IsRunning || state.IsQueued;
                }
            });
            if (!busy && Volatile.Read(ref _criticalDepth) == 0 && Volatile.Read(ref _combatDepth) == 0 &&
                Volatile.Read(ref _normalDepth) == 0)
            {
                return true;
            }
            await Task.Delay(5).ConfigureAwait(false);
        }
        return false;
    }

    private static Channel<NpcThinkQueueEntry> CreateQueue(int capacity) =>
        Channel.CreateBounded<NpcThinkQueueEntry>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });

    private NpcThinkRuntimeState GetOrReplaceState(NpcKey key)
    {
        while (true)
        {
            NpcThinkRuntimeState state = _states.GetOrAdd(key.ObjectId, _ => new NpcThinkRuntimeState(key));
            if (state.Npc == key)
            {
                return state;
            }

            NpcThinkRuntimeState replacement = new(key);
            if (_states.TryUpdate(key.ObjectId, replacement, state))
            {
                lock (state.SyncRoot)
                {
                    state.Removed = true;
                    state.IsQueued = false;
                    state.QueueVersion++;
                }
                _criticalOverflow.TryRemove(state.Npc, out _);
                _combatOverflow.TryRemove(state.Npc, out _);
                return replacement;
            }
        }
    }

    private NpcWakeDisposition QueueNew(NpcThinkRuntimeState state, NpcThinkPriority priority)
    {
        state.IsQueued = true;
        state.QueueVersion++;
        state.QueuedPriority = priority;
        return TryQueueActive(state, priority, state.QueueVersion);
    }

    private NpcWakeDisposition Promote(NpcThinkRuntimeState state, NpcThinkPriority priority)
    {
        state.QueueVersion++;
        state.QueuedPriority = priority;
        return TryQueueActive(state, priority, state.QueueVersion) switch
        {
            NpcWakeDisposition.Queued => NpcWakeDisposition.Coalesced,
            NpcWakeDisposition.Deferred => NpcWakeDisposition.Deferred,
            _ => NpcWakeDisposition.Coalesced
        };
    }

    private NpcWakeDisposition TryQueueActive(NpcThinkRuntimeState state, NpcThinkPriority priority, long version)
    {
        Channel<NpcThinkQueueEntry> queue = GetQueue(priority);
        if (queue.Writer.TryWrite(new NpcThinkQueueEntry(state, version)))
        {
            state.QueueLocation = NpcThinkQueueLocation.Active;
            IncrementDepth(priority);
            _available.Release();
            return NpcWakeDisposition.Queued;
        }

        if (priority == NpcThinkPriority.Normal)
        {
            state.IsQueued = false;
            state.QueueLocation = NpcThinkQueueLocation.None;
            return NpcWakeDisposition.DroppedNormal;
        }

        state.QueueLocation = NpcThinkQueueLocation.Overflow;
        GetOverflow(priority)[state.Npc] = state;
        return NpcWakeDisposition.Deferred;
    }

    private async Task WorkerAsync()
    {
        CancellationToken cancellationToken = _shutdown.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _available.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (!TryDequeueFair(out NpcThinkQueueEntry entry))
                {
                    continue;
                }

                DrainOverflow();
                await ProcessAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private bool TryDequeueFair(out NpcThinkQueueEntry entry)
    {
        long slot = (Interlocked.Increment(ref _dequeueSequence) & long.MaxValue) % 10;
        if (slot == 9)
        {
            return TryRead(NpcThinkPriority.Normal, out entry) ||
                   TryRead(NpcThinkPriority.Critical, out entry) ||
                   TryRead(NpcThinkPriority.Combat, out entry);
        }
        if (slot >= 6)
        {
            return TryRead(NpcThinkPriority.Combat, out entry) ||
                   TryRead(NpcThinkPriority.Critical, out entry) ||
                   TryRead(NpcThinkPriority.Normal, out entry);
        }
        return TryRead(NpcThinkPriority.Critical, out entry) ||
               TryRead(NpcThinkPriority.Combat, out entry) ||
               TryRead(NpcThinkPriority.Normal, out entry);
    }

    private bool TryRead(NpcThinkPriority priority, out NpcThinkQueueEntry entry)
    {
        if (!GetQueue(priority).Reader.TryRead(out entry))
        {
            return false;
        }
        DecrementDepth(priority);
        return true;
    }

    private async ValueTask ProcessAsync(NpcThinkQueueEntry entry, CancellationToken cancellationToken)
    {
        NpcThinkRuntimeState state = entry.State;
        NpcWakeContext context;
        lock (state.SyncRoot)
        {
            if (state.Removed || !state.IsQueued || state.QueueVersion != entry.Version ||
                state.QueueLocation != NpcThinkQueueLocation.Active)
            {
                return;
            }

            long now = _clock.GetTimestamp();
            NpcThinkPriority priority = state.PendingPriority;
            long due = state.LastThinkStarted == 0
                ? now
                : _clock.Add(state.LastThinkStarted, _options.GetMinimumInterval(priority));
            if (due > now)
            {
                state.QueueLocation = NpcThinkQueueLocation.Delayed;
                _delays!.TrySchedule(new NpcDelayedThinkEntry(state, entry.Version, due));
                return;
            }

            state.IsQueued = false;
            state.QueueLocation = NpcThinkQueueLocation.None;
            state.IsRunning = true;
            state.LastThinkStarted = now;
            context = new NpcWakeContext(state.Npc, state.PendingReasons, priority,
                state.EarliestEventTimestamp == 0 ? now : state.EarliestEventTimestamp,
                state.EarliestWakeTimestamp == 0 ? now : state.EarliestWakeTimestamp,
                now, state.SourcePoolId);
            state.PendingReasons = NpcWakeReason.None;
            state.PendingPriority = NpcThinkPriority.Normal;
            state.EarliestEventTimestamp = 0;
            state.EarliestWakeTimestamp = 0;
            state.SourcePoolId = 0;
        }

        NpcAiTelemetry.RecordQueueDelay(context,
            _clock.GetElapsedTime(context.WakeTimestamp, context.ThinkStartedTimestamp));
        NpcAiTelemetry.ReactiveWorkerStarted();
        try
        {
            if (!_generationValidator.IsCurrent(context.Npc))
            {
                NpcAiTelemetry.RecordWakeup(context.Reasons, context.Priority, _options.Mode,
                    NpcWakeDisposition.StaleGeneration);
                return;
            }

            using IDisposable reaction = NpcReactionTracker.Enter(context, _clock,
                _options.ReactionTelemetryEnabled && _options.Mode == NpcReactiveSchedulerMode.Enabled);
            await _executor.ExecuteAsync(context.Npc, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            NpcAiTelemetry.RecordSchedulerExecutionFailure();
        }
        finally
        {
            NpcAiTelemetry.ReactiveWorkerCompleted();
            Complete(state);
        }
    }

    private void Complete(NpcThinkRuntimeState state)
    {
        NpcWakeDisposition? disposition = null;
        NpcWakeReason reasons = NpcWakeReason.None;
        NpcThinkPriority priority = NpcThinkPriority.Normal;
        lock (state.SyncRoot)
        {
            state.IsRunning = false;
            state.LastThinkCompleted = _clock.GetTimestamp();
            if (!state.Removed && state.PendingReasons != NpcWakeReason.None)
            {
                reasons = state.PendingReasons;
                priority = state.PendingPriority;
                disposition = QueueNew(state, priority);
                NpcAiTelemetry.RecordPendingFollowup();
            }
        }

        if (disposition.HasValue)
        {
            NpcAiTelemetry.RecordWakeup(reasons, priority, _options.Mode, disposition.Value);
        }
    }

    private void OnDelayDue(NpcDelayedThinkEntry entry)
    {
        NpcThinkRuntimeState state = entry.State;
        lock (state.SyncRoot)
        {
            if (state.Removed || !state.IsQueued || state.QueueVersion != entry.Version ||
                state.QueueLocation != NpcThinkQueueLocation.Delayed)
            {
                return;
            }
            TryQueueActive(state, state.QueuedPriority, entry.Version);
        }
    }

    private void DrainOverflow()
    {
        DrainOverflow(_criticalOverflow, NpcThinkPriority.Critical);
        DrainOverflow(_combatOverflow, NpcThinkPriority.Combat);
    }

    private void DrainOverflow(ConcurrentDictionary<NpcKey, NpcThinkRuntimeState> overflow,
        NpcThinkPriority priority)
    {
        foreach ((NpcKey key, NpcThinkRuntimeState state) in overflow)
        {
            lock (state.SyncRoot)
            {
                if (state.Removed || !state.IsQueued || state.QueueLocation != NpcThinkQueueLocation.Overflow ||
                    state.QueuedPriority != priority)
                {
                    overflow.TryRemove(key, out _);
                    continue;
                }

                if (GetQueue(priority).Writer.TryWrite(new NpcThinkQueueEntry(state, state.QueueVersion)))
                {
                    state.QueueLocation = NpcThinkQueueLocation.Active;
                    overflow.TryRemove(key, out _);
                    IncrementDepth(priority);
                    _available.Release();
                }
                break;
            }
        }
    }

    private Channel<NpcThinkQueueEntry> GetQueue(NpcThinkPriority priority) => priority switch
    {
        NpcThinkPriority.Critical => _critical,
        NpcThinkPriority.Combat => _combat,
        _ => _normal
    };

    private ConcurrentDictionary<NpcKey, NpcThinkRuntimeState> GetOverflow(NpcThinkPriority priority) =>
        priority == NpcThinkPriority.Critical ? _criticalOverflow : _combatOverflow;

    private void IncrementDepth(NpcThinkPriority priority)
    {
        long value = priority switch
        {
            NpcThinkPriority.Critical => Interlocked.Increment(ref _criticalDepth),
            NpcThinkPriority.Combat => Interlocked.Increment(ref _combatDepth),
            _ => Interlocked.Increment(ref _normalDepth)
        };
        NpcAiTelemetry.SetQueueDepth(priority, value);
    }

    private void DecrementDepth(NpcThinkPriority priority)
    {
        long value = priority switch
        {
            NpcThinkPriority.Critical => Interlocked.Decrement(ref _criticalDepth),
            NpcThinkPriority.Combat => Interlocked.Decrement(ref _combatDepth),
            _ => Interlocked.Decrement(ref _normalDepth)
        };
        NpcAiTelemetry.SetQueueDepth(priority, value);
    }

    private static NpcThinkPriority Max(NpcThinkPriority left, NpcThinkPriority right) =>
        left >= right ? left : right;

    private static long Earlier(long current, long candidate) =>
        current == 0 || candidate < current ? candidate : current;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _critical.Writer.TryComplete();
        _combat.Writer.TryComplete();
        _normal.Writer.TryComplete();
        _shutdown.Cancel();
        if (_delays != null)
        {
            await _delays.DisposeAsync().ConfigureAwait(false);
        }
        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _available.Dispose();
        _shutdown.Dispose();
    }

    private sealed class NoOpNpcThinkExecutor: INpcThinkExecutor
    {
        public static NoOpNpcThinkExecutor Instance { get; } = new();
        public ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
