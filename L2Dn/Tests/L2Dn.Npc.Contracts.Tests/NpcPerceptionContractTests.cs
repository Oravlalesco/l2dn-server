using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using L2Dn.NpcContracts;

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
            new NpcEnvironment(default, null, true, true, false, false, true, 300, 1500),
            default, default, default, default);

        identity.ClanIds.IsDefault.Should().BeFalse();
        state.VisibleEntities.IsDefault.Should().BeFalse();
        state.Threats.IsDefault.Should().BeFalse();
        state.Affordances.IsDefault.Should().BeFalse();
        state.SpatialObservations.IsDefault.Should().BeFalse();
        state.Skills.IsDefault.Should().BeFalse();
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

    [Fact]
    public void Diff_and_apply_reconstructs_added_updated_removed_visibility_in_exact_order()
    {
        NpcKey key = new(7, 2);
        NpcPerceptionSnapshot before = new(new NpcPerceptionEnvelope(1, key, 10, 100, 1_000), CreateState());
        VisibleEntity updatedMonster = before.State.VisibleEntities[1] with
        {
            ObservationOrdinal = 0,
            Position = new NpcPosition(31, 21, 30, 100),
            Distance2D = 22
        };
        VisibleEntity addedPlayer = new(1, new EntityKey(4, 0, EntityKind.Player),
            new NpcPosition(40, 20, 30, 0), 22, 5, 10, 30, EntityStateFlags.Alive,
            EntityRelationFlags.Player);
        NpcPerceptionState afterState = new(
            before.State.Identity,
            before.State.Physical with { CurrentHp = 60 },
            before.State.Combat with { CurrentTarget = addedPlayer.Entity },
            before.State.Environment,
            [updatedMonster, addedPlayer],
            [new ThreatEntry(addedPlayer.Entity, 20, 3, 30, true, true)],
            [new NpcAffordanceObservation(addedPlayer.Entity, NpcAffordanceFlags.CanTarget)],
            [new SpatialObservation(addedPlayer.Entity, SpatialObservationFlags.HasLineOfSight)]);
        NpcPerceptionSnapshot after = new(new NpcPerceptionEnvelope(1, key, 11, 101, 1_100), afterState);

        NpcPerceptionDelta delta = NpcPerceptionDiff.Create(before, after);
        NpcPerceptionApplyResult result = NpcPerceptionDeltaApplier.Apply(before, delta);

        delta.Changes.Should().HaveFlag(NpcPerceptionChangeMask.VisibleEntities);
        delta.VisibleEntities!.Added.Should().ContainSingle().Which.Should().Be(addedPlayer);
        delta.VisibleEntities.Updated.Should().ContainSingle().Which.Should().Be(updatedMonster);
        delta.VisibleEntities.Removed.Should().ContainSingle().Which.Should().Be(before.State.VisibleEntities[0].Entity);
        result.Status.Should().Be(NpcPerceptionApplyStatus.Applied);
        NpcPerceptionExactComparer.Instance.Equals(result.Snapshot, after).Should().BeTrue();
    }

    [Fact]
    public void Delta_apply_reports_protocol_mismatches_without_exceptions()
    {
        NpcPerceptionSnapshot before = new(new NpcPerceptionEnvelope(1, new NpcKey(7, 2), 10, 100, 1_000),
            CreateState());
        NpcPerceptionSnapshot after = new(new NpcPerceptionEnvelope(1, new NpcKey(7, 2), 11, 101, 1_100),
            new NpcPerceptionState(before.State.Identity, before.State.Physical with { CurrentHp = 50 },
                before.State.Combat, before.State.Environment, before.State.VisibleEntities,
                before.State.Threats, before.State.Affordances, before.State.SpatialObservations));
        NpcPerceptionDelta delta = NpcPerceptionDiff.Create(before, after);

        NpcPerceptionDeltaApplier.Apply(before with
            {
                Envelope = before.Envelope with { StateRevision = 9 }
            }, delta).Status.Should().Be(NpcPerceptionApplyStatus.RevisionGap);
        NpcPerceptionDeltaApplier.Apply(before with
            {
                Envelope = before.Envelope with { Npc = new NpcKey(8, 2) }
            }, delta).Status.Should().Be(NpcPerceptionApplyStatus.NpcMismatch);
        NpcPerceptionDeltaApplier.Apply(before with
            {
                Envelope = before.Envelope with { Npc = new NpcKey(7, 3) }
            }, delta).Status.Should().Be(NpcPerceptionApplyStatus.GenerationMismatch);
        NpcPerceptionDeltaApplier.Apply(before with
            {
                Envelope = before.Envelope with { SchemaVersion = 2 }
            }, delta).Status.Should().Be(NpcPerceptionApplyStatus.SchemaMismatch);
        NpcPerceptionDelta unknownMask = new(delta.Envelope, (NpcPerceptionChangeMask)(1 << 20),
            null, null, null, null, null, [], [], []);
        NpcPerceptionDeltaApplier.Apply(before, unknownMask).Status.Should().Be(NpcPerceptionApplyStatus.InvalidDelta);
    }

    [Fact]
    public void Diff_apply_property_holds_for_generated_states()
    {
        NpcKey key = new(77, 4);
        for (int seed = 0; seed < 100; seed++)
        {
            NpcPerceptionState firstState = CreateGeneratedState(seed);
            NpcPerceptionState secondState = CreateGeneratedState(seed + 10_000);
            NpcPerceptionSnapshot first = new(new NpcPerceptionEnvelope(1, key, seed * 2 + 1, seed, seed),
                firstState);
            NpcPerceptionSnapshot second = new(new NpcPerceptionEnvelope(1, key, seed * 2 + 2, seed + 1, seed + 1),
                secondState);

            NpcPerceptionDelta delta = NpcPerceptionDiff.Create(first, second);
            NpcPerceptionApplyResult applied = NpcPerceptionDeltaApplier.Apply(first, delta);

            applied.Status.Should().Be(NpcPerceptionApplyStatus.Applied);
            NpcPerceptionExactComparer.Instance.Equals(applied.Snapshot, second).Should().BeTrue();
        }
    }

    [Fact]
    public void Snapshot_and_delta_json_round_trip_preserve_exact_values()
    {
        NpcKey key = new(7, 2);
        NpcPerceptionSnapshot before = new(new NpcPerceptionEnvelope(1, key, 10, 100, 1_000), CreateState());
        NpcPerceptionSnapshot after = new(new NpcPerceptionEnvelope(1, key, 11, 101, 1_100),
            CreateGeneratedState(123));
        NpcPerceptionDelta delta = NpcPerceptionDiff.Create(before, after);

        NpcPerceptionSnapshot snapshotRoundTrip = JsonSerializer.Deserialize<NpcPerceptionSnapshot>(
            JsonSerializer.Serialize(after))!;
        NpcPerceptionDelta deltaRoundTrip = JsonSerializer.Deserialize<NpcPerceptionDelta>(
            JsonSerializer.Serialize(delta))!;
        NpcPerceptionApplyResult applied = NpcPerceptionDeltaApplier.Apply(before, deltaRoundTrip);

        NpcPerceptionExactComparer.Instance.Equals(snapshotRoundTrip, after).Should().BeTrue();
        applied.Status.Should().Be(NpcPerceptionApplyStatus.Applied);
        NpcPerceptionExactComparer.Instance.Equals(applied.Snapshot, after).Should().BeTrue();
    }

    [Fact]
    public void Region_batch_normalizes_arrays_and_is_explicitly_partial()
    {
        NpcPerceptionRegionBatch batch = new(10, new RegionKey(0, 1, 2), 3, 4, default, default);

        batch.SourcePoolId.Should().Be(3);
        batch.BatchSequence.Should().Be(4);
        batch.FullSnapshots.IsDefault.Should().BeFalse();
        batch.Deltas.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Replay_codec_round_trip_preserves_batch_and_format_version()
    {
        NpcPerceptionSnapshot snapshot = new(
            new NpcPerceptionEnvelope(1, new NpcKey(7, 2), 10, 100, 1_000), CreateState());
        NpcPerceptionRegionBatch batch = new(100, snapshot.State.Environment.Region, 3, 4, [snapshot], []);

        byte[] payload = NpcPerceptionJsonCodec.Serialize(batch);
        NpcPerceptionReplayRecord replay = NpcPerceptionJsonCodec.Deserialize(payload)!;

        replay.FormatVersion.Should().Be(NpcPerceptionReplayRecord.CurrentFormatVersion);
        replay.Batch.SourcePoolId.Should().Be(3);
        replay.Batch.BatchSequence.Should().Be(4);
        replay.Batch.FullSnapshots.Should().ContainSingle();
        NpcPerceptionExactComparer.Instance.Equals(replay.Batch.FullSnapshots[0], snapshot).Should().BeTrue();
    }

    private static NpcPerceptionState CreateState()
    {
        NpcIdentity identity = new(100, NpcKind.Monster, LegacyNpcAiType.Fighter, 20, 300,
            [12, 18], NpcCapabilities.CanMove | NpcCapabilities.CanAttack);
        NpcPhysicalState physical = new(new NpcPosition(10, 20, 30, 40), 80, 100, 20, 30, 8, 16,
            NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned);
        NpcCombatFacts combat = new(null, 40, 500, NpcCombatFlags.None);
        NpcEnvironment environment = new(new RegionKey(0, 10, 11), null, true, true, false, false, true, 300, 1500);
        ImmutableArray<VisibleEntity> visible =
        [
            new VisibleEntity(0, new EntityKey(2, 0, EntityKind.Player), new NpcPosition(15, 20, 30, 0),
                20, 5, 10, 5, EntityStateFlags.Alive, EntityRelationFlags.Player),
            new VisibleEntity(1, new EntityKey(3, 1, EntityKind.Monster), new NpcPosition(30, 20, 30, 0),
                20, 5, 10, 20, EntityStateFlags.Alive, EntityRelationFlags.Monster)
        ];

        return new NpcPerceptionState(identity, physical, combat, environment, visible, [], [], []);
    }

    private static NpcPerceptionState CreateGeneratedState(int seed)
    {
        Random random = new(seed);
        int count = random.Next(1, 8);
        ImmutableArray<VisibleEntity>.Builder visible = ImmutableArray.CreateBuilder<VisibleEntity>(count);
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            int objectId = seed * 100 + ordinal + 10;
            visible.Add(new VisibleEntity(ordinal, new EntityKey(objectId, 0, EntityKind.Player),
                new NpcPosition(random.Next(-10_000, 10_000), random.Next(-10_000, 10_000), random.Next(-500, 500),
                    random.Next(0, 65_535)), random.Next(1, 120), 5, 10, random.NextDouble() * 2_000,
                EntityStateFlags.Alive | EntityStateFlags.Spawned, EntityRelationFlags.Player));
        }

        NpcIdentity identity = new(100 + seed, NpcKind.Monster, LegacyNpcAiType.Fighter, random.Next(1, 120),
            300, [seed, seed + 1], NpcCapabilities.CanMove | NpcCapabilities.CanAttack);
        NpcPhysicalState physical = new(new NpcPosition(seed + 1, seed + 2, seed + 3, seed + 4),
            random.NextDouble() * 100, 100, random.NextDouble() * 50, 50, 8, 16,
            NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned);
        NpcCombatFacts combat = new(visible[0].Entity, 40, 500, NpcCombatFlags.InCombat);
        NpcEnvironment environment = new(new RegionKey(0, seed % 10, seed % 7), null, true, true, false, false,
            true, 300, 1500);
        return new NpcPerceptionState(identity, physical, combat, environment, visible.MoveToImmutable(), [], [], []);
    }
}
