using System.Collections.Immutable;
using System.Globalization;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Attackables;
using L2Dn.GameServer.Model.DailyMissions;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * A monster mission rule. All instances are evaluated by one global kill subscriber so raids and regular
 * monsters follow the same path and one kill can be persisted as a single character batch.
 */
public class MonsterDailyMissionHandler: AbstractDailyMissionHandler
{
    private static readonly DailyMissionMonsterKillEvaluator _evaluator = new();

    private readonly int _minLevel;
    private readonly int _maxLevel;
    private readonly int _minMonsterLevelOffset;
    private readonly HashSet<int> _ids;
    private readonly HashSet<int> _excludedIds;
    private readonly HashSet<string> _areas;
    private readonly TimeSpan? _startTime;
    private readonly TimeSpan? _endTime;
    private bool _registered;

    internal DailyMissionMonsterTargetMode TargetMode { get; }
    internal IReadOnlySet<int> Ids => _ids;
    internal IReadOnlySet<string> Areas => _areas;

    public MonsterDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
    {
        _minLevel = holder.getParams().getInt("minLevel", 0);
        _maxLevel = holder.getParams().getInt("maxLevel", int.MaxValue);
        _minMonsterLevelOffset = holder.getParams().getInt("minMonsterLevelOffset", -4);
        _ids = ParseIntSet(holder.getParams().getString("ids", ""));
        _excludedIds = ParseIntSet(holder.getParams().getString("excludedIds", ""));
        _areas = ParseStringSet(holder.getParams().getString("areas", ""));

        string mode = holder.getParams().getString("targetMode", _ids.Count == 0 ? "ANY" : "NPC_IDS");
        if (!Enum.TryParse(mode, true, out DailyMissionMonsterTargetMode targetMode))
        {
            throw new InvalidOperationException($"Invalid targetMode '{mode}' for daily mission {holder.getId()}.");
        }

        TargetMode = targetMode;
        string startHour = holder.getParams().getString("startHour", "");
        string endHour = holder.getParams().getString("endHour", "");
        if (!string.IsNullOrWhiteSpace(startHour) || !string.IsNullOrWhiteSpace(endHour))
        {
            if (!TimeSpan.TryParseExact(startHour, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan start) ||
                !TimeSpan.TryParseExact(endHour, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan end))
            {
                throw new InvalidOperationException(
                    $"Invalid time interval '{startHour}-{endHour}' for daily mission {holder.getId()}.");
            }

            _startTime = start;
            _endTime = end;
        }
    }

    public override void init()
    {
        _evaluator.Register(this);
        _registered = true;
    }

    public static string diagnoseKill(Player player, Attackable monster)
    {
        return _evaluator.Diagnose(player, monster);
    }

    internal bool IsEligible(Player player, Attackable monster, IReadOnlySet<string> originAreas,
        DateTimeOffset occurredAt)
    {
        if (_excludedIds.Contains(monster.getId()) || player.getLevel() < _minLevel || player.getLevel() > _maxLevel ||
            !CheckTimeInterval(occurredAt))
        {
            return false;
        }

        bool targetMatch = DailyMissionMonsterRule.MatchesTarget(TargetMode, monster.getId(), _ids, originAreas,
            _areas);

        // The relative monster-level rule is deliberately restricted to generic hunting missions.
        return targetMatch && (TargetMode != DailyMissionMonsterTargetMode.ANY ||
            DailyMissionMonsterRule.MatchesGenericMonsterLevel(player.getLevel(), monster.getLevel(),
                _minMonsterLevelOffset));
    }

    private bool CheckTimeInterval(DateTimeOffset occurredAt)
    {
        if (_startTime == null || _endTime == null)
        {
            return true;
        }

        TimeSpan time = occurredAt.ToLocalTime().TimeOfDay;
        return _startTime <= _endTime
            ? time >= _startTime && time < _endTime
            : time >= _startTime || time < _endTime;
    }

    private static HashSet<int> ParseIntSet(string value)
    {
        HashSet<int> result = [];
        foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result.Add(int.Parse(part, CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static HashSet<string> ParseStringSet(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public override void Dispose()
    {
        if (_registered)
        {
            _evaluator.Unregister(this);
            _registered = false;
        }

        base.Dispose();
    }

    private sealed class DailyMissionMonsterKillEvaluator
    {
        private readonly object _syncRoot = new();
        private readonly HashSet<MonsterDailyMissionHandler> _rules = [];
        private KillIndex _index = KillIndex.Empty;

        public DailyMissionMonsterKillEvaluator()
        {
            GlobalEvents.Global.Subscribe<OnAttackableKill>(this, OnKill);
        }

        public void Register(MonsterDailyMissionHandler rule)
        {
            lock (_syncRoot)
            {
                _rules.Add(rule);
                RebuildIndexLocked();
            }
        }

        public void Unregister(MonsterDailyMissionHandler rule)
        {
            lock (_syncRoot)
            {
                _rules.Remove(rule);
                RebuildIndexLocked();
            }
        }

		public string Diagnose(Player player, Attackable monster)
		{
			IReadOnlySet<string> areas = GetOriginAreas(monster);
			MonsterDailyMissionHandler[] rules;
			lock (_syncRoot)
			{
				rules = _rules.OrderBy(rule => rule.getHolder().getId()).ToArray();
			}

			DateTimeOffset now = DateTimeOffset.UtcNow;
			string matched = string.Join(',', rules.Where(rule => rule.IsEligible(player, monster, areas, now))
				.Select(rule => rule.getHolder().getId()));
			return $"npcId={monster.getId()} type={monster.getTemplate().getType()} level={monster.getLevel()} " +
				$"originAreas=[{string.Join(',', areas)}] playerLevel={player.getLevel()} matchedMissions=[{matched}]";
		}

        private void RebuildIndexLocked()
        {
            Dictionary<int, List<MonsterDailyMissionHandler>> byNpcId = [];
            Dictionary<string, List<MonsterDailyMissionHandler>> byArea = new(StringComparer.OrdinalIgnoreCase);
            ImmutableArray<MonsterDailyMissionHandler>.Builder generic = ImmutableArray.CreateBuilder<MonsterDailyMissionHandler>();

            foreach (MonsterDailyMissionHandler rule in _rules)
            {
                if (rule.TargetMode == DailyMissionMonsterTargetMode.ANY)
                {
                    generic.Add(rule);
                }

                if (rule.TargetMode is DailyMissionMonsterTargetMode.NPC_IDS or
                    DailyMissionMonsterTargetMode.NPC_IDS_OR_SPAWN_AREAS)
                {
                    foreach (int id in rule.Ids)
                    {
						if (!byNpcId.TryGetValue(id, out List<MonsterDailyMissionHandler>? rules))
						{
							byNpcId[id] = rules = [];
						}

						rules.Add(rule);
                    }
                }

                if (rule.TargetMode is DailyMissionMonsterTargetMode.SPAWN_AREAS or
                    DailyMissionMonsterTargetMode.NPC_IDS_OR_SPAWN_AREAS)
                {
                    foreach (string area in rule.Areas)
                    {
						if (!byArea.TryGetValue(area, out List<MonsterDailyMissionHandler>? rules))
						{
							byArea[area] = rules = [];
						}

						rules.Add(rule);
                    }
                }
            }

            _index = new KillIndex(
                byNpcId.ToDictionary(x => x.Key, x => x.Value.ToImmutableArray()),
                byArea.ToDictionary(x => x.Key, x => x.Value.ToImmutableArray(), StringComparer.OrdinalIgnoreCase),
                generic.ToImmutable());
        }

        private void OnKill(OnAttackableKill @event)
        {
            Player? player = @event.getAttacker();
            if (player == null)
            {
                return;
            }

            Attackable monster = @event.getTarget();
            IReadOnlySet<string> originAreas = GetOriginAreas(monster);
            KillIndex index = _index;
            HashSet<MonsterDailyMissionHandler> candidates = [..index.Generic];
            if (index.ByNpcId.TryGetValue(monster.getId(), out ImmutableArray<MonsterDailyMissionHandler> idRules))
            {
                candidates.UnionWith(idRules);
            }

            foreach (string area in originAreas)
            {
                if (index.ByArea.TryGetValue(area, out ImmutableArray<MonsterDailyMissionHandler> areaRules))
                {
                    candidates.UnionWith(areaRules);
                }
            }

            foreach (MonsterDailyMissionHandler rule in candidates)
            {
                if (rule.IsEligible(player, monster, originAreas, @event.getOccurredAt()))
                {
                    player.getDailyMissions().addProgress(rule.getHolder(), 1, @event.getOccurredAt());
                }
            }
        }

        private static IReadOnlySet<string> GetOriginAreas(Attackable monster)
        {
            Attackable? origin = monster;
            while (origin != null)
            {
                Spawn? spawn = origin.getSpawn();
                if (spawn != null)
                {
                    return spawn.getDailyMissionAreas();
                }

                origin = origin is Monster minion ? minion.getLeader() : null;
            }

            return ImmutableHashSet<string>.Empty;
        }

        private sealed record KillIndex(
            IReadOnlyDictionary<int, ImmutableArray<MonsterDailyMissionHandler>> ByNpcId,
            IReadOnlyDictionary<string, ImmutableArray<MonsterDailyMissionHandler>> ByArea,
            ImmutableArray<MonsterDailyMissionHandler> Generic)
        {
            public static readonly KillIndex Empty = new(
                new Dictionary<int, ImmutableArray<MonsterDailyMissionHandler>>(),
                new Dictionary<string, ImmutableArray<MonsterDailyMissionHandler>>(StringComparer.OrdinalIgnoreCase),
                []);
        }
    }
}
