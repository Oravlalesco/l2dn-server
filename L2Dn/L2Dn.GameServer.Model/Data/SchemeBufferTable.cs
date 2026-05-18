using System.Xml.Linq;
using L2Dn.Extensions;
using L2Dn.GameServer.Configuration;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.StaticData;
using L2Dn.GameServer.Utilities;
using L2Dn.Utilities;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace L2Dn.GameServer.Data;

/**
 * This class loads available skills and stores players' buff schemes into _schemesTable.
 */
public sealed class SchemeBufferTable: DataReaderBase
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(SchemeBufferTable));

	private readonly Map<int, Map<string, List<int>>> _schemesTable = new();
	private readonly Map<int, BuffSkillHolder> _availableBuffs = new();

    private SchemeBufferTable()
	{
		try
		{
			XDocument document = LoadXmlDocument(DataFileLocation.Data, "SchemeBufferSkills.xml");
			document.Elements("list").Elements("category").ForEach(node =>
			{
				string categoryType = node.GetAttributeValueAsString("type");
				foreach (var buff in node.Elements("buff"))
				{
					int buffId = buff.GetAttributeValueAsInt32("id");
					int buffLevel = buff.GetAttributeValueAsInt32("level");
					int price = buff.GetAttributeValueAsInt32("price");
					string desc = buff.GetAttributeValueAsString("desc");

					_availableBuffs.put(buffId, new BuffSkillHolder(buffId, buffLevel, price, categoryType, desc));
				}
			});
		}
		catch (Exception e)
		{
			LOGGER.Warn("SchemeBufferTable: Failed to load buff info : " + e);
		}

		int count = 0;
		int skipped = 0;
		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			List<DbBufferScheme> schemes = ctx.BufferSchemes.ToList();
			foreach (DbBufferScheme scheme in schemes)
			{
				if (!SchemeBufferSchemeNames.IsValidFormat(scheme.Name, out _))
				{
					LOGGER.Warn(
						"SchemeBufferTable: Skipping invalid scheme from database objectId={0}, name='{1}'.",
						scheme.ObjectId, scheme.Name);
					ctx.BufferSchemes.Where(r => r.ObjectId == scheme.ObjectId && r.Name == scheme.Name)
						.ExecuteDelete();
					skipped++;
					continue;
				}

				string schemeName = SchemeBufferSchemeNames.Normalize(scheme.Name)!;
				string[] skills = scheme.Skills.Split(",");
				List<int> schemeList = [];
				foreach (string skill in skills)
				{
					if (string.IsNullOrEmpty(skill))
						break;

					int skillId = int.Parse(skill);
					if (_availableBuffs.ContainsKey(skillId))
						schemeList.Add(skillId);
				}

				putScheme(scheme.ObjectId, schemeName, schemeList);
				count++;
			}

			if (skipped > 0)
				ctx.SaveChanges();
		}
		catch (Exception e)
		{
			LOGGER.Warn("SchemeBufferTable: Failed to load buff schemes: " + e);
		}

		LOGGER.Info("SchemeBufferTable: Loaded " + count + " players schemes and " + _availableBuffs.Count +
		            " available buffs.");
	}

	private const int MaxSkillsColumnLength = 500;

	public void saveSchemes()
	{
		try
		{
			foreach (KeyValuePair<int, Map<string, List<int>>> player in _schemesTable)
			{
				foreach (KeyValuePair<string, List<int>> scheme in player.Value)
				{
					saveSchemeRow(player.Key, scheme.Key, scheme.Value);
				}
			}
		}
		catch (Exception e)
		{
			LOGGER.Warn("SchemeBufferTable: Error while saving schemes: " + e);
		}
	}

	public void persistScheme(int objectId, string schemeName)
	{
		if (!tryResolveSchemeKey(objectId, schemeName, null, out string resolved))
			return;

		saveSchemeRow(objectId, resolved, getScheme(objectId, resolved));
	}

	public void saveSchemeRow(int objectId, string schemeName, IReadOnlyList<int> skillIds)
	{
		try
		{
			string skills = string.Join(",", skillIds);
			if (skills.Length > MaxSkillsColumnLength)
			{
				LOGGER.Warn(
					"SchemeBufferTable: Scheme skills exceed column limit for objectId={0}, scheme={1} (length={2}, max={3}).",
					objectId, schemeName, skills.Length, MaxSkillsColumnLength);
				return;
			}

			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			DbBufferScheme? existing = ctx.BufferSchemes.Find(objectId, schemeName);
			if (existing != null)
			{
				existing.Skills = skills;
			}
			else
			{
				ctx.BufferSchemes.Add(new DbBufferScheme
				{
					ObjectId = objectId,
					Name = schemeName,
					Skills = skills
				});
			}

			ctx.SaveChanges();
		}
		catch (Exception e)
		{
			LOGGER.Warn("SchemeBufferTable: Error while saving scheme objectId={0}, name={1}: {2}",
				objectId, schemeName, e);
		}
	}

	public void deleteSchemeRow(int objectId, string schemeName)
	{
		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			ctx.BufferSchemes.Where(r => r.ObjectId == objectId && r.Name == schemeName).ExecuteDelete();
		}
		catch (Exception e)
		{
			LOGGER.Warn("SchemeBufferTable: Error while deleting scheme objectId={0}, name={1}: {2}",
				objectId, schemeName, e);
		}
	}

	public bool tryAddScheme(int playerId, string schemeName, out string? error)
	{
		error = null;
		if (!SchemeBufferSchemeNames.IsValidFormat(schemeName, out string? formatError))
		{
			error = formatError switch
            {
                "empty" => "You must enter a scheme name.",
                "length" => "Scheme's name must contain up to 14 chars.",
                "forbidden" => "Scheme name contains invalid characters.",
                _ => "Please use plain alphanumeric characters."
            };
			return false;
		}

		string normalized = SchemeBufferSchemeNames.Normalize(schemeName)!;
		Map<string, List<int>> schemes = _schemesTable.GetOrAdd(playerId,
			static _ => new Map<string, List<int>>(StringComparer.InvariantCultureIgnoreCase));

		if (schemes.ContainsKey(normalized))
		{
			error = "The scheme name already exists.";
			return false;
		}

		if (schemes.Count >= Config.SchemeBuffer.BUFFER_MAX_SCHEMES)
		{
			error = "Maximum schemes amount is already reached.";
			return false;
		}

		putScheme(playerId, normalized, []);
		saveSchemeRow(playerId, normalized, []);
		return true;
	}

	public bool tryRemoveScheme(int playerId, string schemeName, out string? error)
	{
		error = null;
		if (!tryResolveSchemeKey(playerId, schemeName, PlayerBufferSchemeContext.GetActiveScheme(playerId),
			    out string resolved))
		{
			error = "This scheme name is invalid.";
			return false;
		}

		Map<string, List<int>>? schemes = getPlayerSchemes(playerId);
		if (schemes == null || schemes.remove(resolved) == null)
		{
			error = "This scheme name is invalid.";
			return false;
		}

		deleteSchemeRow(playerId, resolved);
		string? active = PlayerBufferSchemeContext.GetActiveScheme(playerId);
		if (active != null && active.Equals(resolved, StringComparison.InvariantCultureIgnoreCase))
			PlayerBufferSchemeContext.ClearActiveScheme(playerId);

		return true;
	}

	public bool hasScheme(int playerId, string schemeName)
	{
		return tryResolveSchemeKey(playerId, schemeName, PlayerBufferSchemeContext.GetActiveScheme(playerId),
			out _);
	}

	public bool tryResolveSchemeKey(int playerId, string parsedName, string? activeFallback, out string resolvedKey)
	{
		resolvedKey = string.Empty;
		Map<string, List<int>>? schemes = getPlayerSchemes(playerId);
		if (schemes == null || schemes.Count == 0)
			return false;

		string? candidate = SchemeBufferSchemeNames.Normalize(parsedName);
		if (candidate == null)
			candidate = SchemeBufferSchemeNames.Normalize(activeFallback);

		if (candidate == null || candidate.equalsIgnoreCase("none"))
			return false;

		if (schemes.TryGetValue(candidate, out _))
		{
			resolvedKey = getCanonicalKey(schemes, candidate);
			return true;
		}

		return false;
	}

	private static string getCanonicalKey(Map<string, List<int>> schemes, string candidate)
	{
		foreach (KeyValuePair<string, List<int>> entry in schemes)
		{
			if (entry.Key.Equals(candidate, StringComparison.InvariantCultureIgnoreCase))
				return entry.Key;
		}

		return candidate;
	}

	private void putScheme(int playerId, string schemeName, List<int> list)
	{
		Map<string, List<int>> schemes = _schemesTable.GetOrAdd(playerId,
			static _ => new Map<string, List<int>>(StringComparer.InvariantCultureIgnoreCase));

		if (schemes.ContainsKey(schemeName))
		{
			schemes.put(schemeName, list);
			return;
		}

		if (schemes.Count >= Config.SchemeBuffer.BUFFER_MAX_SCHEMES)
			return;

		schemes.put(schemeName, list);
	}

	public Map<string, List<int>>? getPlayerSchemes(int playerId)
	{
		return _schemesTable.get(playerId);
	}

	public List<int> getScheme(int playerId, string schemeName)
	{
		if (!tryResolveSchemeKey(playerId, schemeName, PlayerBufferSchemeContext.GetActiveScheme(playerId),
			    out string resolved))
			return [];

		if (!_schemesTable.TryGetValue(playerId, out Map<string, List<int>>? schemes))
			return [];

		if (!schemes.TryGetValue(resolved, out List<int>? scheme))
			return [];

		return scheme;
	}

	public bool getSchemeContainsSkill(int playerId, string schemeName, int skillId)
	{
		List<int> skills = getScheme(playerId, schemeName);
		if (skills.Count == 0)
			return false;

		foreach (int id in skills)
		{
			if (id == skillId)
				return true;
		}

		return false;
	}

	public List<int> getSkillsIdsByType(string groupType)
	{
		List<int> skills = new();
		foreach (BuffSkillHolder skill in _availableBuffs.Values)
		{
			if (skill.getType().equalsIgnoreCase(groupType))
				skills.Add(skill.getId());
		}

		return skills;
	}

	public List<string> getSkillTypes()
	{
		List<string> skillTypes = new();
		foreach (BuffSkillHolder skill in _availableBuffs.Values)
		{
			if (!skillTypes.Contains(skill.getType()))
				skillTypes.Add(skill.getType());
		}

		return skillTypes;
	}

	public BuffSkillHolder? getAvailableBuff(int skillId)
	{
		return _availableBuffs.get(skillId);
	}

	public static SchemeBufferTable getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		public static readonly SchemeBufferTable INSTANCE = new();
	}
}
