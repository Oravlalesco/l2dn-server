using System.Globalization;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed record NpcPerceptionOptions(
    NpcPerceptionMode Mode,
    TimeSpan FullSnapshotInterval,
    TimeSpan FullSnapshotJitter)
{
    public static NpcPerceptionOptions FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        NpcPerceptionMode mode = ParseMode(read("NPC_PERCEPTION_MODE"));
        int intervalSeconds = ParseNonNegativeInt(read("NPC_PERCEPTION_FULL_INTERVAL_SECONDS"), 60);
        int jitterSeconds = ParseNonNegativeInt(read("NPC_PERCEPTION_FULL_JITTER_SECONDS"), 10);
        return new NpcPerceptionOptions(mode, TimeSpan.FromSeconds(intervalSeconds),
            TimeSpan.FromSeconds(jitterSeconds));
    }

    private static NpcPerceptionMode ParseMode(string? value)
    {
        string normalized = value?.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        return Enum.TryParse(normalized, true, out NpcPerceptionMode mode) ? mode : NpcPerceptionMode.Disabled;
    }

    private static int ParseNonNegativeInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0
            ? parsed
            : fallback;
}
