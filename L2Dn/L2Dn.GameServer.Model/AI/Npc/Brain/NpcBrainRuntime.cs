namespace L2Dn.GameServer.AI.Runtime;

public static class NpcBrainRuntime
{
    private static int _mode = (int)NpcBrainOptions.FromEnvironment().Mode;

    public static NpcBrainMode Mode => (NpcBrainMode)Volatile.Read(ref _mode);

    internal static void Configure(NpcBrainMode mode) => Volatile.Write(ref _mode, (int)mode);
}
