using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Data;

public static class PlayerBufferSchemeContext
{
    private static readonly TimeSpan ActiveSchemeTimeout = TimeSpan.FromMinutes(30);
    private static readonly Map<int, ActiveSchemeEntry> ActiveSchemes = new();

    public static void SetActiveScheme(int playerId, string schemeName)
    {
        string? normalized = SchemeBufferSchemeNames.Normalize(schemeName);
        if (normalized == null)
            return;

        ActiveSchemes[playerId] = new ActiveSchemeEntry(normalized, DateTime.UtcNow);
    }

    public static string? GetActiveScheme(int playerId)
    {
        if (!ActiveSchemes.TryGetValue(playerId, out ActiveSchemeEntry? entry))
            return null;

        if (DateTime.UtcNow - entry.SetAt > ActiveSchemeTimeout)
        {
            ActiveSchemes.TryRemove(playerId, out _);
            return null;
        }

        return entry.SchemeName;
    }

    public static void ClearActiveScheme(int playerId)
    {
        ActiveSchemes.TryRemove(playerId, out _);
    }

    private sealed record ActiveSchemeEntry(string SchemeName, DateTime SetAt);
}
