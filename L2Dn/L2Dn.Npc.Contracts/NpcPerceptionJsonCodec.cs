using System.Text.Json;

namespace L2Dn.NpcContracts;

public sealed record NpcPerceptionReplayRecord(int FormatVersion, NpcPerceptionRegionBatch Batch)
{
    public const int CurrentFormatVersion = 1;
}

public static class NpcPerceptionJsonCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static byte[] Serialize(NpcPerceptionRegionBatch batch) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new NpcPerceptionReplayRecord(NpcPerceptionReplayRecord.CurrentFormatVersion, batch), Options);

    public static NpcPerceptionReplayRecord? Deserialize(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<NpcPerceptionReplayRecord>(payload, Options);
}
