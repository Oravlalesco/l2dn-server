using System.Collections.Concurrent;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal enum NpcPerceptionPublicationKind
{
    None = 0,
    Full = 1
}

internal sealed record NpcPerceptionCycle(
    NpcPerceptionSnapshot Snapshot,
    NpcPerceptionPublicationKind Publication,
    bool SemanticStateChanged);

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

    public NpcPerceptionCycle? Capture(Attackable actor)
    {
        if (_options.Mode == NpcPerceptionMode.Disabled)
        {
            return null;
        }

        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
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

                bool changed = published.State == null ||
                    !NpcPerceptionStateComparer.Instance.Equals(published.State, capture.State);
                bool fullDue = published.State == null || IsPeriodicFullDue(capture, published);
                NpcPerceptionPublicationKind publication = changed || fullDue
                    ? NpcPerceptionPublicationKind.Full
                    : NpcPerceptionPublicationKind.None;
                long candidateRevision = published.Revision +
                    (publication == NpcPerceptionPublicationKind.Full ? 1 : 0);
                if (!capture.TryCreateSnapshot(actor, candidateRevision, out NpcPerceptionSnapshot? snapshot) ||
                    snapshot == null)
                {
                    NpcAiTelemetry.RecordPerceptionCaptureFailure("lifecycle_commit", _options.Mode);
                    return null;
                }

                // Revision and published state are committed only after lifecycle validation succeeds.
                if (publication == NpcPerceptionPublicationKind.Full)
                {
                    published.Revision = candidateRevision;
                    published.State = capture.State;
                    published.LastFullMonotonicMilliseconds = capture.CaptureMonotonicMilliseconds;
                }

                if (_options.Mode == NpcPerceptionMode.ShadowValidate)
                {
                    NpcPerceptionValidator.Validate(actor, snapshot);
                }

                NpcAiTelemetry.RecordPerceptionCapture(snapshot, publication, changed, _options.Mode,
                    startedAt, allocatedBefore);
                return new NpcPerceptionCycle(snapshot, publication, changed);
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
            : (uint)HashCode.Combine(capture.Npc.ObjectId, capture.Npc.Generation) % jitterWindow;
        return capture.CaptureMonotonicMilliseconds - published.LastFullMonotonicMilliseconds >= interval + jitter;
    }

    private sealed class PublishedState
    {
        public NpcKey Npc { get; private set; }
        public long Revision { get; set; }
        public NpcPerceptionState? State { get; set; }
        public long LastFullMonotonicMilliseconds { get; set; }

        public void Reset(NpcKey npc)
        {
            Npc = npc;
            Revision = 0;
            State = null;
            LastFullMonotonicMilliseconds = 0;
        }
    }
}
