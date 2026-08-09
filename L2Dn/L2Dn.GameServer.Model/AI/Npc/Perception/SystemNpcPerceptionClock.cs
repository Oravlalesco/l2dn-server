namespace L2Dn.GameServer.AI.Runtime;

internal sealed class SystemNpcPerceptionClock: INpcPerceptionClock
{
    public static SystemNpcPerceptionClock Instance { get; } = new();

    private SystemNpcPerceptionClock()
    {
    }

    public long GetMonotonicMilliseconds() => Environment.TickCount64;
}
