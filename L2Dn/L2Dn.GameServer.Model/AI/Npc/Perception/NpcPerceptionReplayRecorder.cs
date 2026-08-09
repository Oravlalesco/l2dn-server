using System.Threading.Channels;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class NpcPerceptionReplayRecorder
{
    private const int DefaultCapacity = 1_000;
    private static readonly Lazy<NpcPerceptionReplayRecorder?> Current = new(CreateFromEnvironment);
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    private readonly Channel<byte[]> _channel;
    private readonly string _filePath;

    private NpcPerceptionReplayRecorder(string directory, int capacity)
    {
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory,
            $"npc-perception-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.jsonl");
        _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            // TryWrite remains non-blocking and reports false when full, allowing drop telemetry.
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        _ = Task.Run(ProcessAsync);
    }

    public static void Record(NpcPerceptionRegionBatch batch)
    {
        NpcPerceptionReplayRecorder? recorder = Current.Value;
        if (recorder == null)
        {
            return;
        }

        byte[] payload = NpcPerceptionJsonCodec.Serialize(batch);
        if (!recorder._channel.Writer.TryWrite(payload))
        {
            NpcAiTelemetry.RecordPerceptionReplayDrop();
        }
    }

    private async Task ProcessAsync()
    {
        try
        {
            await using FileStream stream = new(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await foreach (byte[] payload in _channel.Reader.ReadAllAsync())
            {
                await stream.WriteAsync(payload);
                await stream.WriteAsync(NewLine);
            }
        }
        catch
        {
            NpcAiTelemetry.RecordPerceptionReplayFailure();
        }
    }

    private static NpcPerceptionReplayRecorder? CreateFromEnvironment()
    {
        string? configuredDirectory = Environment.GetEnvironmentVariable("NPC_PERCEPTION_REPLAY_DIRECTORY");
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return null;
        }

        int capacity = int.TryParse(Environment.GetEnvironmentVariable("NPC_PERCEPTION_REPLAY_CAPACITY"),
            out int parsed) && parsed > 0 ? parsed : DefaultCapacity;
        try
        {
            return new NpcPerceptionReplayRecorder(Path.GetFullPath(configuredDirectory), capacity);
        }
        catch
        {
            NpcAiTelemetry.RecordPerceptionReplayFailure();
            return null;
        }
    }
}
