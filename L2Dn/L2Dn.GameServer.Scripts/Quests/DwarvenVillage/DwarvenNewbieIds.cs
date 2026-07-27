namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Shared IDs for the dwarven newbie story chain (NewQuestData 10071–10079).<br>
 * Values must stay in sync with DataPack XML (NewQuestData, TeleportListData, NPC/item stats).
 */
internal static class DwarvenNewbieIds
{
	// --- Quest IDs (NewQuestData.xml) ---

	/** Ore from the Strip Mine — talk to Foreman Laferon. */
	public const int QuestOreFromTheStripMine = 10071;

	/** New Life's Lessons — kill Gremlins. */
	public const int QuestNewLifesLessons = 10072;

	/** Strength of Spirit — kill Gremlins; rewards level 5. */
	public const int QuestStrengthOfSpirit = 10073;

	/** Learning to Auto-hunt — kill Longtail Keltirs (no Herb Roots item; EP30-safe). */
	public const int QuestLearningToAutoHunt = 10074;

	/** Be Prepared — hunt Frozen Valley. */
	public const int QuestBePrepared = 10075;

	/** Useful Preparations — hunt Frozen Valley; turn in at Mion. */
	public const int QuestUsefulPreparations = 10076;

	/** Time to Think About Weapons — hunt Western Mining Zone. */
	public const int QuestTimeToThinkAboutWeapons = 10077;

	/** Watch Out — hunt Western Mining Zone. */
	public const int QuestWatchOut = 10078;

	/** Developing Your Abilities — last chain quest (reward level 20). Does not chain to 10080. */
	public const int QuestDevelopingYourAbilities = 10079;

	// --- NPCs (stats/npcs + Schuttgart.xml spawns) ---

	/** Foreman Laferon — Strip Mine / Newbie Guide (also used by Q00206Tutorial). */
	public const int NpcForemanLaferon = 30528;

	/** Grocer Mion — Dwarven Village (end NPC of Useful Preparations). */
	public const int NpcGrocerMion = 30519;

	/** Head Priest of the Earth Gerald — Dwarven Village. */
	public const int NpcGerald = 30650;

	// --- Monsters ---

	/** Tutorial / early-zone Gremlin (Gremlins.xml, same as Q00206Tutorial). */
	public const int MonsterGremlin = 18342;

	/** Frozen Valley outskirts (DwarvenStarting.xml). */
	public const int MonsterLongtailKeltir = 20533;
	public const int MonsterElderLongtailKeltir = 20539;
	public const int MonsterBlackWolf = 20317;
	public const int MonsterGoblinSnooper = 20327;
	public const int MonsterUtukuOrc = 20446;

	/** Western Mining Zone (DwarvenStarting.xml near teleport 145). */
	public const int MonsterGoblinBrigand = 20322;
	public const int MonsterUtukuOrcArcher = 20447;
	public const int MonsterGarumWerewolf = 20307;
	public const int MonsterBladeBat = 20480;
	public const int MonsterGoblinBrigandLieutenant = 20324;
	public const int MonsterMagicalWeaver = 20153;

	/** Kill list for Frozen Valley hunting quests (10074–10076). */
	public static readonly int[] FrozenValleyMonsters =
	[
		MonsterLongtailKeltir,
		MonsterElderLongtailKeltir,
		MonsterBlackWolf,
		MonsterGoblinSnooper,
		MonsterUtukuOrc
	];

	/** Kill list for Western Mining Zone quests (10077–10079). */
	public static readonly int[] WesternMiningZoneMonsters =
	[
		MonsterGoblinBrigand,
		MonsterUtukuOrcArcher,
		MonsterGarumWerewolf,
		MonsterBladeBat,
		MonsterGoblinBrigandLieutenant,
		MonsterMagicalWeaver
	];

	/** Kill targets for Learning to Auto-hunt (10074). */
	public static readonly int[] HerbRootMonsters =
	[
		MonsterLongtailKeltir,
		MonsterElderLongtailKeltir
	];

	// --- Items (stats/items) ---

	/**
	 * Herb Roots — Essence NewQuest item. Missing on many EP30 clients (Unknown + crash).
	 * Dwarven 10074 no longer grants this; constant kept for cleanup of leftovers.
	 */
	public const int ItemHerbRoots = 98464;

	// --- EP30-unsafe sealed jewelry from Useful Preparations (10076) → classic IDs ---

	public const int ItemSealedEarringOfStrength = 98474;
	public const int ItemSealedEarringOfWisdom = 98475;
	public const int ItemSealedRingOfWisdom = 98476;
	public const int ItemSealedBlueCoralRing = 98477;
	public const int ItemSealedBlueDiamondNecklace = 98478;

	public const int ItemClassicEarringOfStrength = 114;
	public const int ItemClassicEarringOfWisdom = 115;
	public const int ItemClassicRingOfWisdom = 877;
	public const int ItemClassicBlueCoralRing = 878;
	public const int ItemClassicBlueDiamondNecklace = 909;

	/** Sealed → classic pairs for inventory cleanup on EP30 clients. */
	public static readonly (int SealedId, int ClassicId)[] Ep30UnsafeJewelryRemap =
	[
		(ItemSealedEarringOfStrength, ItemClassicEarringOfStrength),
		(ItemSealedEarringOfWisdom, ItemClassicEarringOfWisdom),
		(ItemSealedRingOfWisdom, ItemClassicRingOfWisdom),
		(ItemSealedBlueCoralRing, ItemClassicBlueCoralRing),
		(ItemSealedBlueDiamondNecklace, ItemClassicBlueDiamondNecklace),
	];

	// --- TeleportListData.xml IDs used by NewQuestData locations for this chain ---

	/** Frozen Valley hunting ground (NewQuestData startLocationId for 10074–10076). */
	public const int TeleportFrozenValley = 144;

	/** Western Mining Zone (NewQuestData startLocationId for 10077–10079). */
	public const int TeleportWesternMiningZone = 145;

	/** Foreman Laferon / Strip Mine (NewQuestData start/endLocationId for 10071–10073). */
	public const int TeleportForemanLaferon = 492;

	/** Grocer Mion (NewQuestData endLocationId for 10076). */
	public const int TeleportGrocerMion = 493;

	/** Head Priest Gerald (NewQuestData endLocationId for 10079). */
	public const int TeleportGerald = 494;

	// --- Tutorial coexistence (Q00206Tutorial memoState) ---

	/** Quest id / script name for the dwarven newbie tutorial. */
	public const int TutorialQuestId = 206;
	public const string TutorialQuestName = "Q00206Tutorial";

	/**
	 * Memo states 0–4 are active tutorial steps on Laferon.
	 * From this value onward the tutorial yields first-talk to story quests
	 * and 10071 may be accepted (server-side gate; specificStart alone is not enough).
	 */
	public const int TutorialFinishedMemoState = 5;
}
