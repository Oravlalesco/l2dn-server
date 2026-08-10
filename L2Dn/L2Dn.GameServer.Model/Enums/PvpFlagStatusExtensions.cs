namespace L2Dn.GameServer.Enums;

internal static class PvpFlagStatusExtensions
{
    internal static bool IsFlagged(this PvpFlagStatus status) => status != PvpFlagStatus.None;

    internal static bool IsUnflagged(this PvpFlagStatus status) => status == PvpFlagStatus.None;
}
