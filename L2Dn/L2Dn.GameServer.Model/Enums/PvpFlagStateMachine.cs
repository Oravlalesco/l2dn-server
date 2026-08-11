namespace L2Dn.GameServer.Enums;

internal static class PvpFlagStateMachine
{
    internal static readonly TimeSpan FlashingWindow = TimeSpan.FromSeconds(20);

    internal static PvpFlagStatus Evaluate(DateTime now, DateTime expiresAt)
    {
        if (now >= expiresAt)
        {
            return PvpFlagStatus.None;
        }

        return now >= expiresAt - FlashingWindow
            ? PvpFlagStatus.Flashing
            : PvpFlagStatus.Enabled;
    }
}
