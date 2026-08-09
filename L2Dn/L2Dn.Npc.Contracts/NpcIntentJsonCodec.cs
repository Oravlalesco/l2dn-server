using System.Text.Json;

namespace L2Dn.NpcContracts;

public static class NpcIntentJsonCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static byte[] Serialize(NpcIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return JsonSerializer.SerializeToUtf8Bytes(intent, Options);
    }

    public static NpcIntent? Deserialize(ReadOnlySpan<byte> payload) =>
        JsonSerializer.Deserialize<NpcIntent>(payload, Options);
}
