namespace L2Dn.GameServer.AI.Runtime;

internal sealed record NpcAiDependencies(
    INpcWorldQuery World,
    INpcGeoQuery Geo,
    INpcThreatQuery Threat,
    ILegacyNpcCommandExecutor Commands)
{
    public static NpcAiDependencies Legacy { get; } = new(
        LegacyNpcWorldQuery.Instance,
        LegacyNpcGeoQuery.Instance,
        LegacyNpcThreatQuery.Instance,
        LegacyNpcCommandExecutor.Instance);
}
