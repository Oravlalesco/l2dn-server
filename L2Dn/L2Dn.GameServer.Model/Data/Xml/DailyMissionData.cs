using System.Collections.Frozen;
using System.Collections.Immutable;
using L2Dn.GameServer.Dto;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.StaticData;
using L2Dn.GameServer.Model.Spawns;
using L2Dn.GameServer.Model.DailyMissions;
using L2Dn.GameServer.Utilities;
using L2Dn.Model;
using L2Dn.Model.Xml;
using NLog;

namespace L2Dn.GameServer.Data.Xml;

/**
 * @author Sdw, Mobius
 */
public sealed class DailyMissionData: DataReaderBase
{
	private static readonly Logger _logger = LogManager.GetLogger(nameof(DailyMissionData));

	private FrozenDictionary<int, DailyMissionDataHolder> _dailyMissionRewards =
		FrozenDictionary<int, DailyMissionDataHolder>.Empty;

	private DailyMissionData()
	{
		load();
	}

	public void load()
	{
		XmlDailyMissionData document = LoadXmlDocument<XmlDailyMissionData>(DataFileLocation.Data, "DailyMission.xml");
		validateDefinitions(document);

		Dictionary<int, DailyMissionDataHolder> dailyMissionRewards = [];
		List<DailyMissionDataHolder> createdHolders = [];
		bool missionSeasonStarted = MissionLevel.getInstance().getCurrentSeason() <= 0; // TODO Must be handled somewhere else
		try
		{
			foreach (XmlDailyMission xmlDailyMission in document.DailyMissions)
			{
				ImmutableArray<CharacterClass> characterClasses =
					xmlDailyMission.ClassIds.Select(x => (CharacterClass)x).ToImmutableArray();

				ImmutableArray<ItemHolder> rewardItems = xmlDailyMission.Rewards
					.Where(x => missionSeasonStarted || x.Id != AbstractDailyMissionHandler.MISSION_LEVEL_POINTS)
					.Select(x => new ItemHolder(x.Id, x.Count)).ToImmutableArray();

				Func<DailyMissionDataHolder, AbstractDailyMissionHandler>? handlerFactory =
					DailyMissionHandler.getInstance().getHandler(xmlDailyMission.Handler!.Name);
				StatSet handlerParams = new();
				foreach (XmlDailyMissionHandlerParam parameter in xmlDailyMission.Handler.Parameters)
				{
					handlerParams.set(parameter.Name, parameter.Value);
				}

				DailyMissionDataHolder holder = new(xmlDailyMission.Id, xmlDailyMission.RequiredCompletion,
					xmlDailyMission.DailyReset, xmlDailyMission.IsOneTime, xmlDailyMission.IsMainClassOnly,
					xmlDailyMission.IsDualClassOnly, xmlDailyMission.IsDisplayedWhenNotAvailable,
					xmlDailyMission.Duration, characterClasses, rewardItems, handlerFactory, handlerParams);
				createdHolders.Add(holder);
				dailyMissionRewards.Add(holder.getId(), holder);
			}
		}
		catch
		{
			createdHolders.ForEach(holder => holder.Dispose());
			throw;
		}

		FrozenDictionary<int, DailyMissionDataHolder> previous = _dailyMissionRewards;
		_dailyMissionRewards = dailyMissionRewards.ToFrozenDictionary();
		foreach (DailyMissionDataHolder holder in previous.Values)
		{
			holder.Dispose();
		}

		_logger.Info(GetType().Name + ": Loaded " + _dailyMissionRewards.Count + " one day rewards.");
		IEnumerable<string> areaCoverage = SpawnData.getInstance().getNpcSpawns(spawn =>
			NpcData.getInstance().getTemplate(spawn.getId())?.isAttackable() ?? false)
			.SelectMany(spawn => spawn.getDailyMissionAreas().Select(area => (Area: area, NpcId: spawn.getId())))
			.GroupBy(entry => entry.Area, StringComparer.OrdinalIgnoreCase)
			.OrderBy(group => group.Key)
			.Select(group => $"{group.Key}={group.Select(entry => entry.NpcId).Distinct().Count()} NPCs");
		_logger.Info("Daily mission spawn-area coverage: " + string.Join(", ", areaCoverage));
	}

	private static void validateDefinitions(XmlDailyMissionData document)
	{
		List<string> errors = [];
		HashSet<int> ids = [];
		foreach (XmlDailyMission mission in document.DailyMissions)
		{
			string prefix = $"Daily mission id={mission.Id}";
			if (mission.Id <= 0 || mission.Id > short.MaxValue)
			{
				errors.Add($"{prefix}: id must be between 1 and {short.MaxValue}.");
			}
			else if (!ids.Add(mission.Id))
			{
				errors.Add($"{prefix}: duplicated id.");
			}

			if (mission.RequiredCompletion <= 0)
			{
				errors.Add($"{prefix}: requiredCompletion must be greater than zero.");
			}

			if (mission.IsMainClassOnly && mission.IsDualClassOnly)
			{
				errors.Add($"{prefix}: main-class-only and dual-class-only cannot both be true.");
			}

			if (mission.Handler == null || string.IsNullOrWhiteSpace(mission.Handler.Name))
			{
				errors.Add($"{prefix}: a handler is required.");
				continue;
			}

			if (DailyMissionHandler.getInstance().getHandler(mission.Handler.Name) == null)
			{
				errors.Add($"{prefix}: unknown handler '{mission.Handler.Name}'.");
			}

			Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase);
			foreach (XmlDailyMissionHandlerParam parameter in mission.Handler.Parameters)
			{
				if (string.IsNullOrWhiteSpace(parameter.Name) || !parameters.TryAdd(parameter.Name, parameter.Value.Trim()))
				{
					errors.Add($"{prefix}: empty or duplicated handler parameter '{parameter.Name}'.");
				}
			}

			validateParameters(prefix, mission.Handler.Name, parameters, errors);

			if (mission.Rewards.Count == 0)
			{
				errors.Add($"{prefix}: at least one reward item is required.");
			}

			foreach (XmlDailyMissionReward reward in mission.Rewards)
			{
				if (reward.Count <= 0)
				{
					errors.Add($"{prefix}: item {reward.Id} has a non-positive count.");
				}

				if (reward.Id != AbstractDailyMissionHandler.MISSION_LEVEL_POINTS &&
					reward.Id != AbstractDailyMissionHandler.CLAN_EXP && ItemData.getInstance().getTemplate(reward.Id) == null)
				{
					errors.Add($"{prefix}: reward item {reward.Id} does not exist.");
				}
			}

			int[] duplicatedRewards = mission.Rewards.GroupBy(reward => reward.Id)
				.Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
			if (duplicatedRewards.Length != 0)
			{
				errors.Add($"{prefix}: duplicated reward items {string.Join(',', duplicatedRewards)}.");
			}
		}

		if (errors.Count != 0)
		{
			throw new InvalidDataException("Invalid DailyMission.xml:" + Environment.NewLine +
				string.Join(Environment.NewLine, errors));
		}
	}

	private static void validateParameters(string prefix, string handlerName,
		Dictionary<string, string> parameters, List<string> errors)
	{
		foreach (string name in new[] { "minLevel", "maxLevel", "level" })
		{
			if (parameters.TryGetValue(name, out string? value) && !int.TryParse(value, out _))
			{
				errors.Add($"{prefix}: parameter '{name}' must be an integer.");
			}
		}

		if (parameters.TryGetValue("minLevel", out string? minText) && int.TryParse(minText, out int minLevel) &&
			parameters.TryGetValue("maxLevel", out string? maxText) && int.TryParse(maxText, out int maxLevel) &&
			minLevel > maxLevel)
		{
			errors.Add($"{prefix}: minLevel cannot be greater than maxLevel.");
		}

		if (handlerName.Equals("level", StringComparison.OrdinalIgnoreCase) &&
			(!parameters.TryGetValue("level", out string? levelText) || !int.TryParse(levelText, out int level) || level <= 0))
		{
			errors.Add($"{prefix}: level handler requires a positive 'level' parameter.");
		}

		string? idParameterName = handlerName.Equals("useitem", StringComparison.OrdinalIgnoreCase) ? "itemIds" :
			handlerName.Equals("monster", StringComparison.OrdinalIgnoreCase) ? "ids" : null;
		if (idParameterName != null && parameters.TryGetValue(idParameterName, out string? idText) &&
			!string.IsNullOrWhiteSpace(idText))
		{
			HashSet<int> parsedIds = [];
			foreach (string part in idText.Split(','))
			{
				if (!int.TryParse(part.Trim(), out int id) || id <= 0)
				{
					errors.Add($"{prefix}: invalid id '{part}' in parameter '{idParameterName}'.");
					continue;
				}

				if (!parsedIds.Add(id))
				{
					errors.Add($"{prefix}: duplicated id {id} in parameter '{idParameterName}'.");
				}

				if (idParameterName == "ids" && NpcData.getInstance().getTemplate(id) == null)
				{
					errors.Add($"{prefix}: monster id {id} does not exist.");
				}
				else if (idParameterName == "ids" &&
					!(NpcData.getInstance().getTemplate(id)?.isAttackable() ?? false))
				{
					errors.Add($"{prefix}: monster id {id} is not attackable and cannot emit a kill.");
				}
				else if (idParameterName == "itemIds" && ItemData.getInstance().getTemplate(id) == null)
				{
					errors.Add($"{prefix}: item id {id} does not exist.");
				}
			}
		}

		bool hasStart = parameters.TryGetValue("startHour", out string? startHour);
		bool hasEnd = parameters.TryGetValue("endHour", out string? endHour);
		if (hasStart != hasEnd || hasStart &&
			(!TimeSpan.TryParseExact(startHour, "hh\\:mm", null, out _) ||
			 !TimeSpan.TryParseExact(endHour, "hh\\:mm", null, out _)))
		{
			errors.Add($"{prefix}: startHour and endHour must both use HH:mm.");
		}

		if (handlerName.Equals("monster", StringComparison.OrdinalIgnoreCase))
		{
			validateMonsterParameters(prefix, parameters, errors);
		}
	}

	private static void validateMonsterParameters(string prefix, Dictionary<string, string> parameters,
		List<string> errors)
	{
		HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase)
		{
			"targetMode", "ids", "areas", "excludedIds", "minLevel", "maxLevel",
			"minMonsterLevelOffset", "startHour", "endHour",
		};
		foreach (string parameter in parameters.Keys.Where(parameter => !allowed.Contains(parameter)))
		{
			errors.Add($"{prefix}: unknown monster parameter '{parameter}'.");
		}

		string modeText = parameters.GetValueOrDefault("targetMode",
			parameters.TryGetValue("ids", out string? legacyIds) && !string.IsNullOrWhiteSpace(legacyIds)
				? "NPC_IDS"
				: "ANY");
		if (!Enum.TryParse(modeText, true, out DailyMissionMonsterTargetMode mode))
		{
			errors.Add($"{prefix}: targetMode must be ANY, NPC_IDS, SPAWN_AREAS or NPC_IDS_OR_SPAWN_AREAS.");
			return;
		}

		bool requiresIds = mode is DailyMissionMonsterTargetMode.NPC_IDS or
			DailyMissionMonsterTargetMode.NPC_IDS_OR_SPAWN_AREAS;
		bool requiresAreas = mode is DailyMissionMonsterTargetMode.SPAWN_AREAS or
			DailyMissionMonsterTargetMode.NPC_IDS_OR_SPAWN_AREAS;
		bool hasIds = parameters.TryGetValue("ids", out string? ids) && !string.IsNullOrWhiteSpace(ids);
		bool hasAreas = parameters.TryGetValue("areas", out string? areas) && !string.IsNullOrWhiteSpace(areas);
		if (requiresIds != hasIds)
		{
			errors.Add($"{prefix}: targetMode {mode} {(requiresIds ? "requires" : "does not accept")} 'ids'.");
		}

		if (requiresAreas != hasAreas)
		{
			errors.Add($"{prefix}: targetMode {mode} {(requiresAreas ? "requires" : "does not accept")} 'areas'.");
		}

		if (parameters.TryGetValue("minMonsterLevelOffset", out string? offsetText) &&
			(!int.TryParse(offsetText, out _) || mode != DailyMissionMonsterTargetMode.ANY))
		{
			errors.Add($"{prefix}: minMonsterLevelOffset is an integer accepted only by targetMode ANY.");
		}

		validateMonsterIds(prefix, "excludedIds", parameters.GetValueOrDefault("excludedIds", ""), errors);
		if (!hasAreas)
		{
			return;
		}

		Dictionary<string, HashSet<int>> coverage = new(StringComparer.OrdinalIgnoreCase);
		foreach (NpcSpawnTemplate spawn in SpawnData.getInstance().getNpcSpawns(_ => true))
		{
			if (!(NpcData.getInstance().getTemplate(spawn.getId())?.isAttackable() ?? false))
			{
				continue;
			}

			foreach (string area in spawn.getDailyMissionAreas())
			{
				if (!coverage.TryGetValue(area, out HashSet<int>? npcIds))
				{
					coverage[area] = npcIds = [];
				}

				npcIds.Add(spawn.getId());
			}
		}

		HashSet<string> parsedAreas = new(StringComparer.OrdinalIgnoreCase);
		foreach (string part in areas!.Split(','))
		{
			string area = part.Trim();
			if (string.IsNullOrEmpty(area) || area.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '_')))
			{
				errors.Add($"{prefix}: invalid daily mission area '{part}'. Use letters, digits and underscores.");
				continue;
			}

			if (!parsedAreas.Add(area))
			{
				errors.Add($"{prefix}: duplicated daily mission area '{area}'.");
			}
			else if (!coverage.TryGetValue(area, out HashSet<int>? npcIds) || npcIds.Count == 0)
			{
				errors.Add($"{prefix}: daily mission area '{area}' has no attackable spawn coverage.");
			}
		}
	}

	private static void validateMonsterIds(string prefix, string parameterName, string value, List<string> errors)
	{
		HashSet<int> ids = [];
		foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!int.TryParse(part, out int id) || id <= 0 || !ids.Add(id))
			{
				errors.Add($"{prefix}: invalid or duplicated id '{part}' in parameter '{parameterName}'.");
			}
		}
	}

	public ImmutableArray<DailyMissionDataHolder> getDailyMissionData()
	{
		return _dailyMissionRewards.Values;
	}

	public List<DailyMissionDataHolder> getDailyMissionData(Player player)
	{
		List<DailyMissionDataHolder> missionData = [];
		foreach (DailyMissionDataHolder mission in _dailyMissionRewards.Values)
		{
			if (mission.isDisplayable(player))
				missionData.Add(mission);
		}

		return missionData;
	}

	public DailyMissionDataHolder? getDailyMissionData(int id)
	{
		return _dailyMissionRewards.GetValueOrDefault(id);
	}

	public void refreshPlayer(Player player)
	{
		foreach (DailyMissionDataHolder holder in _dailyMissionRewards.Values)
		{
			holder.refresh(player);
		}
	}

	public bool isAvailable()
	{
		return _dailyMissionRewards.Count != 0;
	}

	/**
	 * Gets the single instance of DailyMissionData.
	 * @return single instance of DailyMissionData
	 */
	public static DailyMissionData getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		public static readonly DailyMissionData INSTANCE = new();
	}
}
