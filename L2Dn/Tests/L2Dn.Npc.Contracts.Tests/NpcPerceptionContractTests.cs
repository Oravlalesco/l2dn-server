using System.Collections.Immutable;
using FluentAssertions;
using L2Dn.Npc.Contracts;

namespace L2Dn.Npc.Contracts.Tests;

public class NpcPerceptionContractTests
{
    [Fact]
    public void Contracts_assembly_has_no_L2Dn_dependencies()
    {
        string[] references = typeof(NpcPerceptionSnapshot).Assembly.GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .Where(static name => name.StartsWith("L2Dn.", StringComparison.Ordinal))
            .ToArray();

        references.Should().BeEmpty();
    }

    [Fact]
    public void Default_immutable_arrays_are_normalized_to_empty()
    {
        NpcIdentity identity = new(1, NpcKind.Monster, LegacyNpcAiType.Fighter, 20, 300,
            default, NpcCapabilities.CanMove);
        NpcPerceptionState state = new(identity,
            new NpcPhysicalState(default, 1, 1, 1, 1, 5, 10, NpcPhysicalFlags.Alive),
            new NpcCombatFacts(null, 40, 500, NpcCombatFlags.None),
            new NpcEnvironment(default, null, true, true, false, false, true),
            default, default, default, default);

        identity.ClanIds.IsDefault.Should().BeFalse();
        state.VisibleEntities.IsDefault.Should().BeFalse();
        state.Threats.IsDefault.Should().BeFalse();
        state.Affordances.IsDefault.Should().BeFalse();
        state.SpatialObservations.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Semantic_comparer_ignores_publication_metadata()
    {
        NpcPerceptionState state = CreateState();
        NpcPerceptionSnapshot first = new(new NpcPerceptionEnvelope(1, new NpcKey(7, 2), 10, 100, 1_000), state);
        NpcPerceptionSnapshot second = new(new NpcPerceptionEnvelope(1, new NpcKey(7, 2), 11, 101, 1_100), state);

        NpcPerceptionStateComparer.Instance.Equals(first.State, second.State).Should().BeTrue();
        NpcPerceptionExactComparer.Instance.Equals(first, second).Should().BeFalse();
    }

    [Fact]
    public void Semantic_comparer_compares_collection_values_and_order()
    {
        NpcPerceptionState first = CreateState();
        NpcPerceptionState sameValues = CreateState();
        NpcPerceptionState reversed = new(first.Identity, first.Physical, first.Combat, first.Environment,
            first.VisibleEntities.Reverse().ToImmutableArray(), first.Threats, first.Affordances,
            first.SpatialObservations);

        NpcPerceptionStateComparer.Instance.Equals(first, sameValues).Should().BeTrue();
        NpcPerceptionStateComparer.Instance.Equals(first, reversed).Should().BeFalse();
    }

    private static NpcPerceptionState CreateState()
    {
        NpcIdentity identity = new(100, NpcKind.Monster, LegacyNpcAiType.Fighter, 20, 300,
            [12, 18], NpcCapabilities.CanMove | NpcCapabilities.CanAttack);
        NpcPhysicalState physical = new(new NpcPosition(10, 20, 30, 40), 80, 100, 20, 30, 8, 16,
            NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned);
        NpcCombatFacts combat = new(null, 40, 500, NpcCombatFlags.None);
        NpcEnvironment environment = new(new RegionKey(0, 10, 11), null, true, true, false, false, true);
        ImmutableArray<VisibleEntity> visible =
        [
            new VisibleEntity(0, new EntityKey(2, 0, EntityKind.Player), new NpcPosition(15, 20, 30, 0),
                20, 5, 10, 5, EntityStateFlags.Alive, EntityRelationFlags.Player),
            new VisibleEntity(1, new EntityKey(3, 1, EntityKind.Monster), new NpcPosition(30, 20, 30, 0),
                20, 5, 10, 20, EntityStateFlags.Alive, EntityRelationFlags.Monster)
        ];

        return new NpcPerceptionState(identity, physical, combat, environment, visible, [], [], []);
    }
}
