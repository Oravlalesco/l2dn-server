using System.Runtime.CompilerServices;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.Geometry;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Scheduling;

/// <summary>
/// Detects the edge where a moving player enters an NPC's own aggro radius.
/// World visibility remains the broad-phase; this tracker only performs the
/// per-NPC range check and emits once until the player leaves that radius.
/// </summary>
internal sealed class NpcSpatialRelevanceTracker
{
    private readonly ConditionalWeakTable<Player, NpcSpatialRelevanceState> _players = new();

    public static NpcSpatialRelevanceTracker Instance { get; } = new();

    private NpcSpatialRelevanceTracker()
    {
    }

    public void ObserveMovement(Player player)
    {
        if (NpcReactivity.Mode == NpcReactiveSchedulerMode.Disabled || !player.isSpawned())
        {
            return;
        }

        NpcSpatialRelevanceState state = _players.GetValue(player, static _ => new NpcSpatialRelevanceState());
        state.ObserveMovement(player);
    }
}

internal sealed class NpcSpatialRelevanceState
{
    private readonly object _syncRoot = new();
    private readonly Action<Attackable> _observeNpc;
    private readonly List<Attackable> _enteredActors = [];
    private HashSet<NpcKey> _inside = [];
    private HashSet<NpcKey> _current = [];
    private Player? _player;

    public NpcSpatialRelevanceState()
    {
        // Reuse the delegate and collections: this runs on every moving-player update.
        _observeNpc = ObserveNpc;
    }

    public void ObserveMovement(Player player)
    {
        Attackable[] entered;
        lock (_syncRoot)
        {
            _current.Clear();
            _enteredActors.Clear();
            _player = player;
            try
            {
                World.getInstance().forEachVisibleObject(player, _observeNpc);
            }
            finally
            {
                _player = null;
            }

            SwapCurrentState();
            entered = _enteredActors.Count == 0 ? [] : _enteredActors.ToArray();
            _enteredActors.Clear();
        }

        foreach (Attackable actor in entered)
        {
            NpcReactivity.Wake(actor, NpcWakeReason.PlayerBecameRelevant);
        }
    }

    public NpcKey[] Update(IEnumerable<NpcKey> current)
    {
        lock (_syncRoot)
        {
            _current.Clear();
            foreach (NpcKey key in current)
            {
                _current.Add(key);
            }

            NpcKey[] entered = _current.Where(key => !_inside.Contains(key)).ToArray();
            SwapCurrentState();
            return entered;
        }
    }

    private void ObserveNpc(Attackable npc)
    {
        Player player = _player!;
        if (!npc.isSpawned() || npc.isDead() || npc.getInstanceId() != player.getInstanceId())
        {
            return;
        }

        bool hasAuthoritativeHate = npc.getHating(player) > 0;
        if (!hasAuthoritativeHate && !npc.isAggressive() && npc is not Guard)
        {
            return;
        }

        if (!hasAuthoritativeHate && !player.isAutoAttackable(npc))
        {
            return;
        }

        // Preserve the legacy acquisition boundary: guards use a fixed 500-unit
        // radius and World range queries include the Z axis.
        int aggroRange = npc is Guard ? 500 : npc.getAggroRange();
        if (aggroRange <= 0 || !npc.IsInsideRadius3D(player, aggroRange))
        {
            return;
        }

        NpcKey key = new(npc.ObjectId, npc.getSpawnGeneration());
        if (_current.Add(key) && !_inside.Contains(key))
        {
            _enteredActors.Add(npc);
        }
    }

    private void SwapCurrentState()
    {
        (_inside, _current) = (_current, _inside);
    }
}
