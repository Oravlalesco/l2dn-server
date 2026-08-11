using System.Collections.Concurrent;
using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

internal enum NpcReturnEngagementState
{
    None = 0,
    LeashGrace = 1,
    ReturningHome = 2,
    DefensiveReturn = 3
}

internal sealed class NpcBrainState
{
    public NpcBrainState(NpcKey npc) => Npc = npc;

    public object SyncRoot { get; } = new();
    public NpcKey Npc { get; }
    public long DecisionSequence { get; set; }
    public EntityKey? LastTarget { get; set; }
    public NpcIntentType? LastDecision { get; set; }
    public long LastDecisionWorldTick { get; set; }
    public NpcReturnEngagementState ReturnState { get; set; }
    public bool FleeMode { get; set; }
    public EntityKey? ReturnDefenseTarget { get; set; }
    public long ReturnDefenseExpiresAtWorldTick { get; set; }
    public long LeashGraceExpiresAtWorldTick { get; set; }
    public int LeashExcursionCount { get; set; }
    public bool ReturnMovementIssued { get; set; }
}

internal readonly record struct NpcBrainStateSnapshot(
    NpcKey Npc,
    long DecisionSequence,
    EntityKey? LastTarget,
    NpcIntentType? LastDecision,
    long LastDecisionWorldTick,
    NpcReturnEngagementState ReturnState,
    bool FleeMode,
    EntityKey? ReturnDefenseTarget,
    long ReturnDefenseExpiresAtWorldTick,
    long LeashGraceExpiresAtWorldTick,
    int LeashExcursionCount,
    bool ReturnMovementIssued);

internal sealed class NpcBrainStateStore
{
    private readonly ConcurrentDictionary<int, NpcBrainState> _states = new();

    public bool TryUse<T>(NpcKey key, Func<NpcBrainState, T> action, out T? result)
    {
        ArgumentNullException.ThrowIfNull(action);
        while (true)
        {
            if (_states.TryGetValue(key.ObjectId, out NpcBrainState? existing))
            {
                if (existing.Npc.Generation > key.Generation)
                {
                    result = default;
                    return false;
                }

                if (existing.Npc.Generation < key.Generation)
                {
                    NpcBrainState replacement = new(key);
                    if (!_states.TryUpdate(key.ObjectId, replacement, existing))
                    {
                        continue;
                    }
                    existing = replacement;
                }

                lock (existing.SyncRoot)
                {
                    result = action(existing);
                    return true;
                }
            }

            if (_states.TryAdd(key.ObjectId, new NpcBrainState(key)))
            {
                continue;
            }
        }
    }

    public void Remove(NpcKey key)
    {
        if (_states.TryGetValue(key.ObjectId, out NpcBrainState? state) && state.Npc == key)
        {
            _states.TryRemove(new KeyValuePair<int, NpcBrainState>(key.ObjectId, state));
        }
    }

    public NpcBrainStateSnapshot? GetSnapshot(int objectId)
    {
        if (!_states.TryGetValue(objectId, out NpcBrainState? state))
        {
            return null;
        }

        lock (state.SyncRoot)
        {
            return new NpcBrainStateSnapshot(state.Npc, state.DecisionSequence, state.LastTarget,
                state.LastDecision, state.LastDecisionWorldTick, state.ReturnState, state.FleeMode,
                state.ReturnDefenseTarget, state.ReturnDefenseExpiresAtWorldTick,
                state.LeashGraceExpiresAtWorldTick, state.LeashExcursionCount, state.ReturnMovementIssued);
        }
    }
}
