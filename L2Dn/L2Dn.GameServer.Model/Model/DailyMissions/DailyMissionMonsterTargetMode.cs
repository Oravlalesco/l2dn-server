namespace L2Dn.GameServer.Model.DailyMissions;

public enum DailyMissionMonsterTargetMode
{
    ANY,
    NPC_IDS,
    SPAWN_AREAS,
    NPC_IDS_OR_SPAWN_AREAS,
}

public static class DailyMissionMonsterRule
{
    public static bool MatchesTarget(DailyMissionMonsterTargetMode mode, int npcId, IReadOnlySet<int> ids,
        IReadOnlySet<string> originAreas, IReadOnlySet<string> areas)
    {
        bool idMatch = ids.Contains(npcId);
        bool areaMatch = areas.Overlaps(originAreas);
        return mode switch
        {
            DailyMissionMonsterTargetMode.ANY => true,
            DailyMissionMonsterTargetMode.NPC_IDS => idMatch,
            DailyMissionMonsterTargetMode.SPAWN_AREAS => areaMatch,
            DailyMissionMonsterTargetMode.NPC_IDS_OR_SPAWN_AREAS => idMatch || areaMatch,
            _ => false,
        };
    }

    public static bool MatchesGenericMonsterLevel(int playerLevel, int monsterLevel, int minimumOffset = -4)
    {
        return monsterLevel >= playerLevel + minimumOffset;
    }
}
