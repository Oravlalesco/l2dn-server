using System.Threading.Channels;

namespace L2Dn.GameServer.AI.Scheduling;

internal sealed class NpcThinkDelayQueue: IAsyncDisposable
{
    private readonly Channel<NpcDelayedThinkEntry> _incoming;
    private readonly INpcThinkClock _clock;
    private readonly Action<NpcDelayedThinkEntry> _onDue;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _pump;

    public NpcThinkDelayQueue(INpcThinkClock clock, Action<NpcDelayedThinkEntry> onDue)
    {
        _clock = clock;
        _onDue = onDue;
        // There is at most one current delayed token per registered NPC. Promotions leave at most two stale
        // tokens, so this queue is bounded by coordinator state rather than by the number of wake-up events.
        _incoming = Channel.CreateUnbounded<NpcDelayedThinkEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _pump = Task.Run(PumpAsync);
    }

    public bool TrySchedule(NpcDelayedThinkEntry entry) => _incoming.Writer.TryWrite(entry);

    private async Task PumpAsync()
    {
        PriorityQueue<NpcDelayedThinkEntry, long> waiting = new();
        CancellationToken cancellationToken = _shutdown.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_incoming.Reader.TryRead(out NpcDelayedThinkEntry incoming))
                {
                    waiting.Enqueue(incoming, incoming.DueTimestamp);
                }

                if (!waiting.TryPeek(out _, out long dueTimestamp))
                {
                    if (!await _incoming.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return;
                    }
                    continue;
                }

                long now = _clock.GetTimestamp();
                TimeSpan remaining = dueTimestamp <= now
                    ? TimeSpan.Zero
                    : _clock.GetElapsedTime(now, dueTimestamp);
                if (remaining > TimeSpan.Zero)
                {
                    Task delay = Task.Delay(remaining, cancellationToken);
                    Task<bool> newEntry = _incoming.Reader.WaitToReadAsync(cancellationToken).AsTask();
                    await Task.WhenAny(delay, newEntry).ConfigureAwait(false);
                    continue;
                }

                NpcDelayedThinkEntry due = waiting.Dequeue();
                _onDue(due);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _incoming.Writer.TryComplete();
        _shutdown.Cancel();
        try
        {
            await _pump.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        _shutdown.Dispose();
    }
}
