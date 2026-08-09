using System.Collections.Immutable;
using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class NpcBrainCoordinator: INpcBrain
{
    private readonly NpcBrainStateStore _states;
    private readonly NpcIntelligenceProfileResolver _profiles;
    private readonly ReflexBrain _reflex;
    private readonly TacticalBrain _tactical;

    public NpcBrainCoordinator()
        : this(new NpcBrainStateStore(), NpcIntelligenceProfileResolver.Instance,
            new ReflexBrain(), new TacticalBrain())
    {
    }

    internal NpcBrainCoordinator(NpcBrainStateStore states, NpcIntelligenceProfileResolver profiles,
        ReflexBrain reflex, TacticalBrain tactical)
    {
        _states = states;
        _profiles = profiles;
        _reflex = reflex;
        _tactical = tactical;
    }

    public NpcBrainDecision Decide(NpcPerceptionSnapshot perception, NpcBrainContext context)
    {
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(context);
        NpcKey actor = perception.Envelope.Npc;
        if (perception.Envelope.SchemaVersion != NpcPerceptionSnapshot.CurrentSchemaVersion ||
            perception.Envelope.StateRevision <= 0)
        {
            return new NpcBrainDecision(actor, 0, NpcBrainLayer.None, []);
        }

        bool accepted = _states.TryUse(actor, state => DecideWithState(perception, context, state),
            out NpcBrainDecision? decision);
        return accepted && decision != null
            ? decision
            : new NpcBrainDecision(actor, 0, NpcBrainLayer.None, []);
    }

    public void Remove(NpcKey npc) => _states.Remove(npc);

    private NpcBrainDecision DecideWithState(NpcPerceptionSnapshot perception, NpcBrainContext context,
        NpcBrainState state)
    {
        long sequence = ++state.DecisionSequence;
        NpcIntelligenceProfile profile = context.Profile ?? _profiles.Resolve(perception.State.Identity);
        NpcIntent? intent = _reflex.Decide(perception, context, profile, sequence);
        NpcBrainLayer layer = intent == null ? NpcBrainLayer.None : NpcBrainLayer.Reflex;
        if (intent == null)
        {
            intent = _tactical.Decide(perception, profile, sequence);
            layer = intent == null ? NpcBrainLayer.None : NpcBrainLayer.Tactical;
        }

        state.LastTarget = perception.State.Combat.CurrentTarget;
        state.LastDecision = intent?.Envelope.IntentType;
        state.LastDecisionWorldTick = perception.Envelope.WorldTick;
        state.ReturnHomeRequested = intent is ReturnHomeIntent;
        state.FleeMode = intent is FleeIntent;

        ImmutableArray<NpcIntent> intents = intent == null ? [] : [intent];
        return new NpcBrainDecision(perception.Envelope.Npc, sequence, layer, intents);
    }
}
