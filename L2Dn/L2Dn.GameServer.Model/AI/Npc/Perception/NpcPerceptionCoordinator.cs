using System.Collections.Concurrent;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal enum NpcPerceptionPublicationKind
{
    None = 0,
    Full = 1,
    Delta = 2
}

internal sealed record NpcPerceptionCycle(
    NpcPerceptionSnapshot Snapshot,
    NpcPerceptionPublicationKind Publication,
    bool SemanticStateChanged,
    NpcPerceptionDelta? Delta);

internal sealed class NpcPerceptionCoordinator
{
    private readonly NpcPerceptionBuilder _builder;
    private readonly NpcPerceptionOptions _options;
    private readonly ConcurrentDictionary<int, PublishedState> _published = new();

    public static NpcPerceptionCoordinator Instance { get; } = new(
        new NpcPerceptionBuilder(LegacyNpcWorldQuery.Instance, LegacyNpcGeoQuery.Instance,
            LegacyNpcThreatQuery.Instance),
        NpcPerceptionOptions.FromEnvironment());

    internal NpcPerceptionCoordinator(NpcPerceptionBuilder builder, NpcPerceptionOptions options)
    {
        _builder = builder;
        _options = options;
        NpcAiTelemetry.SetPerceptionMode(options.Mode);
    }

    public NpcPerceptionMode Mode => _options.Mode;

    public void Remove(int objectId) => _published.TryRemove(objectId, out _);

    public bool TryGetPublishedRevision(NpcKey npc, out long revision)
    {
        if (_published.TryGetValue(npc.ObjectId, out PublishedState? published))
        {
            lock (published)
            {
                if (published.Npc == npc && published.Revision > 0)
                {
                    revision = published.Revision;
                    return true;
                }
            }
        }

        revision = 0;
        return false;
    }

    public NpcPerceptionCycle? Capture(Attackable actor, bool requiredForBrain = false)
    {
        if (_options.Mode == NpcPerceptionMode.Disabled && !requiredForBrain)
        {
            return null;
        }

        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        using System.Diagnostics.Activity? activity = NpcAiTelemetry.StartPerceptionActivity(_options.Mode);
        try
        {
            if (!_builder.TryCapture(actor, out NpcPerceptionCapture? capture) || capture == null)
            {
                NpcAiTelemetry.RecordPerceptionCaptureFailure("unstable_lifecycle", _options.Mode);
                return null;
            }

            PublishedState published = _published.GetOrAdd(actor.ObjectId, static _ => new PublishedState());
            lock (published)
            {
                bool generationChanged = published.Npc != capture.Npc;
                if (generationChanged)
                {
                    published.Reset(capture.Npc);
                }

                bool changed = published.Snapshot == null ||
                    !NpcPerceptionStateComparer.Instance.Equals(published.Snapshot.State, capture.State);
                bool fullDue = published.Snapshot == null || IsPeriodicFullDue(capture, published);
                NpcPerceptionPublicationKind publication = fullDue
                    ? NpcPerceptionPublicationKind.Full
                    : changed
                        ? NpcPerceptionPublicationKind.Delta
                        : NpcPerceptionPublicationKind.None;
                long candidateRevision = published.Revision +
                    (publication == NpcPerceptionPublicationKind.None ? 0 : 1);
                if (!capture.TryCreateSnapshot(actor, candidateRevision, out NpcPerceptionSnapshot? snapshot) ||
                    snapshot == null)
                {
                    NpcAiTelemetry.RecordPerceptionCaptureFailure("lifecycle_commit", _options.Mode);
                    return null;
                }

                NpcPerceptionDelta? delta = publication == NpcPerceptionPublicationKind.Delta
                    ? NpcPerceptionDiff.Create(published.Snapshot!, snapshot)
                    : null;

                // Revision and published state are committed only after lifecycle validation and diff succeed.
                if (publication != NpcPerceptionPublicationKind.None)
                {
                    published.Revision = candidateRevision;
                    published.Snapshot = snapshot;
                }

                if (publication == NpcPerceptionPublicationKind.Full)
                {
                    published.LastFullMonotonicMilliseconds = capture.CaptureMonotonicMilliseconds;
                }

                if (_options.Mode == NpcPerceptionMode.ShadowValidate)
                {
                    NpcPerceptionValidator.Validate(actor, snapshot);
                }

                NpcAiTelemetry.RecordPerceptionCapture(snapshot, delta, publication, changed, _options.Mode,
                    startedAt, allocatedBefore);
                return new NpcPerceptionCycle(snapshot, publication, changed, delta);
            }
        }
        catch
        {
            NpcAiTelemetry.RecordPerceptionCaptureFailure("error", _options.Mode);
            return null;
        }
    }

    private bool IsPeriodicFullDue(NpcPerceptionCapture capture, PublishedState published)
    {
        if (_options.FullSnapshotInterval <= TimeSpan.Zero)
        {
            return false;
        }

        long interval = (long)_options.FullSnapshotInterval.TotalMilliseconds;
        long jitterWindow = (long)_options.FullSnapshotJitter.TotalMilliseconds;
        long jitter = jitterWindow <= 0
            ? 0
            : (uint)HashCode.Combine(capture.Npc.ObjectId, capture.Npc.Generation) % jitterWindow - jitterWindow / 2;
        long dueAfter = Math.Max(1, interval + jitter);
        return capture.CaptureMonotonicMilliseconds - published.LastFullMonotonicMilliseconds >= dueAfter;
    }

    private sealed class PublishedState
    {
        public NpcKey Npc { get; private set; }
        public long Revision { get; set; }
        public NpcPerceptionSnapshot? Snapshot { get; set; }
        public long LastFullMonotonicMilliseconds { get; set; }

        public void Reset(NpcKey npc)
        {
            Npc = npc;
            Revision = 0;
            Snapshot = null;
            LastFullMonotonicMilliseconds = 0;
        }
    }
}
