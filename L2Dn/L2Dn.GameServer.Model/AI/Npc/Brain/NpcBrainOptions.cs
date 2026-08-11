using System.Globalization;
using L2Dn.GameServer.TaskManagers;
using L2Dn.NpcBrain;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed record NpcBrainOptions(
    NpcBrainMode Mode,
    NpcReturnDefensePolicy ReturnDefense)
{
    public static NpcBrainOptions FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        string normalized = read("NPC_BRAIN_MODE")?.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        NpcBrainMode mode = Enum.TryParse(normalized, true, out NpcBrainMode parsedMode)
            ? parsedMode
            : NpcBrainMode.Legacy;
        bool defenseEnabled = ParseBoolean(read("NPC_RETURN_DEFENSE_ENABLED"), true);
        int timeoutMs = ParseInt(read("NPC_RETURN_DEFENSE_TIMEOUT_MS"), 120_000, 0, 600_000);
        int graceMs = ParseInt(read("NPC_LEASH_GRACE_MS"), 20_000, 0, 120_000);
        int maxExcursions = ParseInt(read("NPC_LEASH_MAX_EXCURSIONS"), 3, 0, 10);
        int hardExtension = ParseInt(read("NPC_LEASH_HARD_EXTENSION"), 500, 0, 5_000);
        return new NpcBrainOptions(mode, new NpcReturnDefensePolicy(defenseEnabled,
            ToWorldTicks(timeoutMs), ToWorldTicks(graceMs), maxExcursions, hardExtension));
    }

    private static int ToWorldTicks(int milliseconds) => milliseconds == 0
        ? 0
        : Math.Max(1, (milliseconds + GameTimeTaskManager.MILLIS_IN_TICK - 1) /
            GameTimeTaskManager.MILLIS_IN_TICK);

    private static int ParseInt(string? value, int fallback, int minimum, int maximum) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static bool ParseBoolean(string? value, bool fallback) =>
        bool.TryParse(value, out bool parsed) ? parsed : fallback;
}
