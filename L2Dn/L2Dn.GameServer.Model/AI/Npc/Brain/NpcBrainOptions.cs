namespace L2Dn.GameServer.AI.Runtime;

internal sealed record NpcBrainOptions(NpcBrainMode Mode)
{
    public static NpcBrainOptions FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        string normalized = read("NPC_BRAIN_MODE")?.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal) ?? string.Empty;
        return new NpcBrainOptions(Enum.TryParse(normalized, true, out NpcBrainMode mode)
            ? mode
            : NpcBrainMode.Legacy);
    }
}
