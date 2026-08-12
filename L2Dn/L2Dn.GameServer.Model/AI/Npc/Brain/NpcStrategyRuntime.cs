namespace L2Dn.GameServer.AI.Runtime;

public static class NpcStrategyRuntime
{
    private static int _mode = (int)NpcStrategyMode.Disabled;

    public static NpcStrategyMode Mode => (NpcStrategyMode)Volatile.Read(ref _mode);

    internal static void Configure(NpcStrategyMode mode) => Volatile.Write(ref _mode, (int)mode);
}
