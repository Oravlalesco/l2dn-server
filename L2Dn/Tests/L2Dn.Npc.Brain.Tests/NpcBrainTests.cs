using System.Collections.Immutable;
using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

public class NpcBrainTests
{
    private static readonly NpcKey Actor = new(8172, 4);
    private static readonly EntityKey Player = new(9182, 0, EntityKind.Player);

    [Fact]
    public void Brain_assembly_references_only_contracts_from_L2Dn()
    {
        string[] references = typeof(NpcBrainCoordinator).Assembly.GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .Where(static name => name.StartsWith("L2Dn.", StringComparison.Ordinal))
            .ToArray();

        references.Should().Equal("L2Dn.Npc.Contracts");
    }

    [Fact]
    public void Dead_actor_produces_no_intent()
    {
        NpcPerceptionSnapshot perception = CreatePerception(physicalFlags: NpcPhysicalFlags.Spawned);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Aggressive_actor_acquires_nearest_visible_hostile()
    {
        VisibleEntity far = Hostile(new EntityKey(9991, 0, EntityKind.Player), 300, 0);
        VisibleEntity near = Hostile(Player, 90, 1);
        NpcPerceptionSnapshot perception = CreatePerception(visible: [far, near]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Layer.Should().Be(NpcBrainLayer.Reflex);
        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Passive_actor_does_not_acquire_visible_player_without_threat()
    {
        NpcPerceptionSnapshot perception = CreatePerception(visible: [Hostile(Player, 90)],
            capabilities: NpcCapabilities.CanMove | NpcCapabilities.CanAttack);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Threat_is_acquired_even_by_passive_actor()
    {
        NpcPerceptionSnapshot perception = CreatePerception(threats:
            [new ThreatEntry(Player, 10, 2, 100, true, true)],
            capabilities: NpcCapabilities.CanMove | NpcCapabilities.CanAttack);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.Attacked));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Dead_current_target_is_cleared()
    {
        VisibleEntity dead = Hostile(Player, 30) with { State = EntityStateFlags.Spawned | EntityStateFlags.AlikeDead };
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [dead]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.TargetDied));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ClearTargetIntent>()
            .Which.ExpectedTarget.Should().Be(Player);
    }

    [Fact]
    public void Valid_distant_target_is_approached()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [Hostile(Player, 300)]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Layer.Should().Be(NpcBrainLayer.Tactical);
        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ApproachTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Valid_target_in_range_is_attacked()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [Hostile(Player, 30)]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Actor_outside_leash_returns_home_without_target()
    {
        NpcPerceptionSnapshot perception = CreatePerception(position: new NpcPosition(500, 0, 0, 0),
            spawn: new NpcPosition(0, 0, 0, 0));

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ReturnHomeIntent>();
    }

    [Fact]
    public void Low_health_actor_flees_when_profile_allows_it()
    {
        NpcIntelligenceProfile profile = new(NpcIntelligenceArchetype.BasicMeleeMob,
            true, true, true, true, 20, 0, 200);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [Hostile(Player, 30)], hp: 10);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            new NpcBrainContext(NpcBrainStimulus.Attacked, profile));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
    }

    [Fact]
    public void New_generation_replaces_state_and_stale_generation_cannot_replace_it_back()
    {
        NpcBrainCoordinator brain = new();
        NpcPerceptionSnapshot generation4 = CreatePerception();
        NpcPerceptionSnapshot generation5 = CreatePerception(actor: Actor with { Generation = 5 });

        brain.Decide(generation4, Context()).DecisionSequence.Should().Be(1);
        brain.Decide(generation4, Context()).DecisionSequence.Should().Be(2);
        brain.Decide(generation5, Context()).DecisionSequence.Should().Be(1);
        brain.Decide(generation4, Context()).DecisionSequence.Should().Be(0);
    }

    [Fact]
    public void Replay_of_same_perception_sequence_is_semantically_deterministic()
    {
        NpcPerceptionSnapshot[] replay =
        [
            CreatePerception(visible: [Hostile(Player, 80)]),
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], revision: 2),
            CreatePerception(target: Player,
                visible: [Hostile(Player, 30) with { State = EntityStateFlags.Spawned | EntityStateFlags.AlikeDead }],
                revision: 3)
        ];
        NpcBrainCoordinator first = new();
        NpcBrainCoordinator second = new();

        NpcIntent[] firstRun = replay.SelectMany(frame => first.Decide(frame, Context()).Intents).ToArray();
        NpcIntent[] secondRun = replay.SelectMany(frame => second.Decide(frame, Context()).Intents).ToArray();

        firstRun.Should().HaveCount(3);
        firstRun.Zip(secondRun).Should().OnlyContain(pair =>
            NpcIntentSemanticComparer.Instance.Equals(pair.First, pair.Second));
    }

    private static NpcBrainContext Context(NpcBrainStimulus stimuli = NpcBrainStimulus.PeriodicDue) => new(stimuli);

    private static VisibleEntity Hostile(EntityKey key, double distance, int ordinal = 0) =>
        new(ordinal, key, new NpcPosition((int)distance, 0, 0, 0), 20, 5, 10, distance,
            EntityStateFlags.Alive | EntityStateFlags.Spawned,
            EntityRelationFlags.Player | EntityRelationFlags.Playable | EntityRelationFlags.SameInstance);

    private static NpcPerceptionSnapshot CreatePerception(
        NpcKey? actor = null,
        EntityKey? target = null,
        ImmutableArray<VisibleEntity> visible = default,
        ImmutableArray<ThreatEntry> threats = default,
        NpcCapabilities capabilities = NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.Aggressive,
        NpcPhysicalFlags physicalFlags = NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned,
        NpcPosition? position = null,
        NpcPosition? spawn = null,
        double hp = 100,
        long revision = 1)
    {
        NpcKey npc = actor ?? Actor;
        NpcIdentity identity = new(100, NpcKind.Monster, LegacyNpcAiType.Fighter, 20, 300, [], capabilities);
        NpcPhysicalState physical = new(position ?? new NpcPosition(0, 0, 0, 0), hp, 100, 50, 50, 8, 16,
            physicalFlags);
        NpcCombatFacts combat = new(target, 40, 500, NpcCombatFlags.None);
        NpcEnvironment environment = new(new RegionKey(0, 1, 1), spawn, true, true, false, false, true);
        NpcPerceptionState state = new(identity, physical, combat, environment, visible, threats, [], []);
        return new NpcPerceptionSnapshot(new NpcPerceptionEnvelope(1, npc, revision, revision, revision), state);
    }
}
