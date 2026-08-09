using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.InstanceZones;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.Geometry;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.Model.Tests;

public class NpcAiRuntimeTests
{
    private static int _nextObjectId = 1_910_000_000;
    private static int _nextTemplateId = 9_100_000;

    [Fact]
    public void Legacy_world_query_matches_world_visibility()
    {
        TestWorldObject source = new(GetNextObjectId());
        TestWorldObject target = new(GetNextObjectId());
        WorldRegion region = source.getWorldRegion();
        region.AddVisibleObject(source);
        region.AddVisibleObject(target);

        try
        {
            LegacyNpcWorldQuery.Instance.GetVisibleObjects<TestWorldObject>(source)
                .Should().ContainSingle().Which.Should().BeSameAs(target);
            LegacyNpcWorldQuery.Instance.GetVisibleObjectsInRange<TestWorldObject>(source, 100)
                .Should().ContainSingle().Which.Should().BeSameAs(target);

            List<TestWorldObject> visited = [];
            LegacyNpcWorldQuery.Instance.ForEachVisibleObjectInRange<TestWorldObject>(source, 100, visited.Add);
            visited.Should().ContainSingle().Which.Should().BeSameAs(target);
        }
        finally
        {
            region.RemoveVisibleObject(source);
            region.RemoveVisibleObject(target);
        }
    }

    [Fact]
    public void Legacy_geo_query_forwards_calls_to_the_wrapped_engine()
    {
        Location3D source = new(100, 100, 0);
        Location3D target = new(150, 150, 0);
        Location3D validTarget = new(140, 140, 0);
        (Location3D Source, Location3D Target, Instance? Instance)? movementCall = null;
        (Location3D Source, Location3D Target, Instance? Instance)? validLocationCall = null;
        (WorldObject Source, WorldObject Target)? visibilityCall = null;
        TestWorldObject sourceObject = new(GetNextObjectId());
        TestWorldObject targetObject = new(GetNextObjectId());
        LegacyNpcGeoQuery query = new(
            (actualSource, actualTarget) =>
            {
                visibilityCall = (actualSource, actualTarget);
                return true;
            },
            (actualSource, actualTarget, instance) =>
            {
                movementCall = (actualSource, actualTarget, instance);
                return false;
            },
            (actualSource, actualTarget, instance) =>
            {
                validLocationCall = (actualSource, actualTarget, instance);
                return validTarget;
            });

        query.CanSeeTarget(sourceObject, targetObject).Should().BeTrue();
        query.CanMoveToTarget(source, target, null).Should().BeFalse();
        query.GetValidLocation(source, target, null).Should().Be(validTarget);

        (WorldObject Source, WorldObject Target) actualVisibilityCall = visibilityCall ??
            throw new InvalidOperationException("The visibility query was not forwarded.");
        actualVisibilityCall.Source.Should().BeSameAs(sourceObject);
        actualVisibilityCall.Target.Should().BeSameAs(targetObject);
        movementCall.Should().Be((source, target, (Instance?)null));
        validLocationCall.Should().Be((source, target, (Instance?)null));
    }

    [Fact]
    public void Legacy_threat_query_exposes_current_aggro_entries()
    {
        Attackable actor = CreateAttackable();
        Attackable attacker = CreateAttackable();
        AggroInfo entry = new(attacker);
        entry.addHate(17);
        actor.getAggroList().put(attacker, entry);

        LegacyNpcThreatQuery.Instance.GetAggroEntries(actor).Should().ContainSingle().Which.Should().BeSameAs(entry);
    }

    [Fact]
    public void Attackable_ai_uses_injected_command_executor_for_target_changes()
    {
        Attackable actor = CreateAttackable();
        TestWorldObject target = new(GetNextObjectId());
        RecordingCommandExecutor commands = new();
        NpcAiDependencies dependencies = new(new EmptyWorldQuery(), new EmptyGeoQuery(),
            new EmptyThreatQuery(), commands, new DeterministicRandomSource(), new NullEntityResolver());
        AttackableAI ai = new(actor, dependencies);

        ai.setTarget(target);

        commands.TargetActor.Should().BeSameAs(actor);
        commands.Target.Should().BeSameAs(target);
    }

    [Fact]
    public void Npc_lifecycle_generation_is_stable_only_outside_respawn_transition()
    {
        Attackable actor = CreateAttackable();

        actor.tryGetStableLifecycleStamp(out NpcLifecycleStamp initial).Should().BeTrue();
        initial.Should().Be(new NpcLifecycleStamp(0, 0));

        actor.beginRespawnLifecycle();
        actor.getSpawnGeneration().Should().Be(1);
        actor.tryGetStableLifecycleStamp(out _).Should().BeFalse();

        actor.completeRespawnLifecycle();
        actor.tryGetStableLifecycleStamp(out NpcLifecycleStamp firstSpawn).Should().BeTrue();
        firstSpawn.Should().Be(new NpcLifecycleStamp(1, 2));

        actor.beginRespawnLifecycle();
        actor.completeRespawnLifecycle();
        actor.getSpawnGeneration().Should().Be(2);
    }

    [Fact]
    public void Perception_builder_copies_state_and_remains_observationally_pure()
    {
        Attackable actor = CreateSpawnedAttackable();
        Attackable attacker = CreateSpawnedAttackable();
        actor.setXYZ(100, 100, 20);
        attacker.setXYZ(130, 140, 25);
        actor.setTarget(attacker);
        actor.setCurrentHp(75, false);
        double capturedHp = actor.getCurrentHp();

        AggroInfo aggro = new(attacker);
        aggro.addHate(17);
        aggro.addDamage(9);
        actor.getAggroList().put(attacker, aggro);
        List<WorldObject> visible = [attacker];
        TestWorldQuery world = new(visible, 42);
        TestGeoQuery geo = new();
        NpcPerceptionBuilder builder = new(world, geo, new FixedThreatQuery([aggro]), new FixedClock(1234));

        builder.TryCapture(actor, out NpcPerceptionCapture? capture).Should().BeTrue();
        capture.Should().NotBeNull();
        capture!.TryCreateSnapshot(actor, 1, out NpcPerceptionSnapshot? snapshot).Should().BeTrue();

        snapshot!.Envelope.Npc.Generation.Should().Be(1);
        snapshot.Envelope.StateRevision.Should().Be(1);
        snapshot.Envelope.WorldTick.Should().Be(42);
        snapshot.Envelope.CaptureMonotonicMilliseconds.Should().Be(1234);
        snapshot.State.Physical.CurrentHp.Should().Be(capturedHp);
        snapshot.State.Physical.Position.Should().Be(new NpcPosition(100, 100, 20, actor.getHeading()));
        snapshot.State.Combat.CurrentTarget.Should().Be(new EntityKey(attacker.ObjectId, 1, EntityKind.Npc));
        snapshot.State.VisibleEntities.Should().ContainSingle().Which.ObservationOrdinal.Should().Be(0);
        snapshot.State.Threats.Should().ContainSingle().Which.Should().Match<ThreatEntry>(entry =>
            entry.Hate == 17 && entry.Damage == 9 && entry.Visible);
        snapshot.State.SpatialObservations.Should().ContainSingle();
        geo.VisibilityCalls.Should().Be(1);
        geo.MovementCalls.Should().Be(1);

        actor.getTarget().Should().BeSameAs(attacker);
        actor.getAggroList().Should().ContainSingle();
        aggro.getHate().Should().Be(17);

        actor.setXYZ(999, 999, 999);
        actor.setTarget(null);
        aggro.addHate(100);
        visible.Clear();

        snapshot.State.Physical.CurrentHp.Should().Be(capturedHp);
        snapshot.State.Physical.Position.Should().Be(new NpcPosition(100, 100, 20, snapshot.State.Physical.Position.Heading));
        snapshot.State.Combat.CurrentTarget.Should().NotBeNull();
        snapshot.State.Threats.Single().Hate.Should().Be(17);
        snapshot.State.VisibleEntities.Should().ContainSingle();
    }

    [Fact]
    public void Perception_builder_retries_once_when_generation_changes_during_capture()
    {
        Attackable actor = CreateSpawnedAttackable();
        int worldTickReads = 0;
        TestWorldQuery world = new([], 7, () =>
        {
            if (Interlocked.Increment(ref worldTickReads) == 1)
            {
                actor.beginRespawnLifecycle();
                actor.completeRespawnLifecycle();
            }
        });
        NpcPerceptionBuilder builder = new(world, new TestGeoQuery(), new FixedThreatQuery([]),
            new FixedClock(1));

        builder.TryCapture(actor, out NpcPerceptionCapture? capture).Should().BeTrue();

        worldTickReads.Should().Be(2);
        capture!.Npc.Generation.Should().Be(2);
    }

    [Fact]
    public void Perception_coordinator_advances_revision_only_for_publications_and_resets_on_generation()
    {
        Attackable actor = CreateSpawnedAttackable();
        TestClock clock = new(1_000);
        NpcPerceptionBuilder builder = new(new TestWorldQuery([], 11), new TestGeoQuery(),
            new FixedThreatQuery([]), clock);
        NpcPerceptionCoordinator coordinator = new(builder,
            new NpcPerceptionOptions(NpcPerceptionMode.CaptureOnly, TimeSpan.Zero, TimeSpan.Zero));

        NpcPerceptionCycle first = coordinator.Capture(actor)!;
        NpcPerceptionCycle unchanged = coordinator.Capture(actor)!;
        actor.setXYZ(12, 34, 56);
        NpcPerceptionCycle changed = coordinator.Capture(actor)!;

        first.Publication.Should().Be(NpcPerceptionPublicationKind.Full);
        first.Snapshot.Envelope.StateRevision.Should().Be(1);
        unchanged.Publication.Should().Be(NpcPerceptionPublicationKind.None);
        unchanged.Snapshot.Envelope.StateRevision.Should().Be(1);
        changed.Publication.Should().Be(NpcPerceptionPublicationKind.Full);
        changed.Snapshot.Envelope.StateRevision.Should().Be(2);

        actor.beginRespawnLifecycle();
        actor.onRespawn();
        actor.completeRespawnLifecycle();
        NpcPerceptionCycle respawned = coordinator.Capture(actor)!;

        respawned.Snapshot.Envelope.Npc.Generation.Should().Be(2);
        respawned.Snapshot.Envelope.StateRevision.Should().Be(1);
    }

    [Fact]
    public void Perception_coordinator_periodic_full_is_deterministic_and_revisioned()
    {
        Attackable actor = CreateSpawnedAttackable();
        TestClock clock = new(1_000);
        NpcPerceptionCoordinator coordinator = new(
            new NpcPerceptionBuilder(new TestWorldQuery([], 1), new TestGeoQuery(), new FixedThreatQuery([]), clock),
            new NpcPerceptionOptions(NpcPerceptionMode.CaptureOnly, TimeSpan.FromSeconds(60), TimeSpan.Zero));

        NpcPerceptionCycle first = coordinator.Capture(actor)!;
        clock.Value = 60_999;
        NpcPerceptionCycle notDue = coordinator.Capture(actor)!;
        clock.Value = 61_000;
        NpcPerceptionCycle due = coordinator.Capture(actor)!;

        first.Snapshot.Envelope.StateRevision.Should().Be(1);
        notDue.Publication.Should().Be(NpcPerceptionPublicationKind.None);
        notDue.Snapshot.Envelope.StateRevision.Should().Be(1);
        due.Publication.Should().Be(NpcPerceptionPublicationKind.Full);
        due.Snapshot.Envelope.StateRevision.Should().Be(2);
    }

    [Fact]
    public void Snapshot_read_resolves_current_target_only_at_the_legacy_edge()
    {
        Attackable actor = CreateSpawnedAttackable();
        Attackable target = CreateSpawnedAttackable();
        actor.setTarget(target);
        NpcPerceptionBuilder builder = new(new TestWorldQuery([target], 1), new TestGeoQuery(),
            new FixedThreatQuery([]), new TestClock(1));
        builder.TryCapture(actor, out NpcPerceptionCapture? capture).Should().BeTrue();
        capture!.TryCreateSnapshot(actor, 1, out NpcPerceptionSnapshot? snapshot).Should().BeTrue();
        FixedEntityResolver resolver = new(target);
        TargetReadingAttackableAI ai = new(actor, new NpcAiDependencies(new EmptyWorldQuery(), new EmptyGeoQuery(),
            new EmptyThreatQuery(), new RecordingCommandExecutor(), new DeterministicRandomSource(), resolver));

        ai.onEvtThink(snapshot!);

        ai.ObservedTarget.Should().BeSameAs(target);
        resolver.ResolveCalls.Should().Be(1);
    }

    [Theory]
    [InlineData("Disabled", (int)NpcPerceptionMode.Disabled)]
    [InlineData("CAPTURE_ONLY", (int)NpcPerceptionMode.CaptureOnly)]
    [InlineData("shadow-validate", (int)NpcPerceptionMode.ShadowValidate)]
    [InlineData("SnapshotRead", (int)NpcPerceptionMode.SnapshotRead)]
    [InlineData("invalid", (int)NpcPerceptionMode.Disabled)]
    public void Perception_mode_parses_operational_configuration(string configured, int expected)
    {
        NpcPerceptionOptions options = NpcPerceptionOptions.FromEnvironment(name => name switch
        {
            "NPC_PERCEPTION_MODE" => configured,
            _ => null
        });

        options.Mode.Should().Be((NpcPerceptionMode)expected);
    }

    [Fact]
    public void Custom_meter_emits_think_query_geo_and_command_measurements()
    {
        List<RecordedMeasurement> measurements = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == NpcAiTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            measurements.Add(new RecordedMeasurement(instrument.Name, measurement, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            measurements.Add(new RecordedMeasurement(instrument.Name, measurement, tags.ToArray())));
        listener.Start();

        Attackable actor = CreateAttackable();
        CreatureAI ai = new(actor);
        long startedAt = Stopwatch.GetTimestamp();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread();

        NpcAiTelemetry.RecordThink(ai, CtrlIntention.AI_INTENTION_ACTIVE, startedAt, allocatedBytes);
        NpcAiTelemetry.RecordThinkError(ai, CtrlIntention.AI_INTENTION_ACTIVE);
        NpcAiTelemetry.ObserveWorldQuery("test_world", typeof(TestWorldObject), static () => 1);
        NpcAiTelemetry.ObserveGeoQuery("test_geo", static () => true,
            static result => result ? "allowed" : "blocked");
        NpcAiTelemetry.ObserveCommand("test_command", static () => { });

        measurements.Should().Contain(item => item.Name == "l2dn.npc.think.calls");
        measurements.Should().Contain(item => item.Name == "l2dn.npc.think.duration");
        measurements.Should().Contain(item => item.Name == "l2dn.npc.think.allocations");
        measurements.Should().Contain(item => item.Name == "l2dn.npc.think.errors");
        measurements.Should().Contain(item => item.Name == "l2dn.npc.world_query.calls" &&
            item.Tags.Any(tag => tag.Key == "operation" && Equals(tag.Value, "test_world")));
        measurements.Should().Contain(item => item.Name == "l2dn.npc.geo_query.calls" &&
            item.Tags.Any(tag => tag.Key == "outcome" && Equals(tag.Value, "allowed")));
        measurements.Should().Contain(item => item.Name == "l2dn.npc.command.calls" &&
            item.Tags.Any(tag => tag.Key == "outcome" && Equals(tag.Value, "success")));
        measurements.SelectMany(item => item.Tags).Should().NotContain(tag =>
            tag.Key == "npc_id" || tag.Key == "object_id" || tag.Key == "template_id" || tag.Key == "npc_name");
    }

    private static Attackable CreateAttackable()
    {
        StatSet set = new();
        set.set("id", Interlocked.Increment(ref _nextTemplateId));
        set.set("type", "Monster");
        set.set("name", "Test NPC");
        set.set("baseHpMax", 100d);
        set.set("baseMpMax", 100d);
        return new Attackable(new NpcTemplate(set));
    }

    private static Attackable CreateSpawnedAttackable()
    {
        Attackable actor = CreateAttackable();
        actor.beginRespawnLifecycle();
        actor.onRespawn();
        actor.completeRespawnLifecycle();
        actor.setSpawned(true);
        return actor;
    }

    private static int GetNextObjectId() => Interlocked.Increment(ref _nextObjectId);

    private sealed record RecordedMeasurement(
        string Name,
        double Value,
        IReadOnlyCollection<KeyValuePair<string, object?>> Tags);

    private sealed class TestWorldObject(int objectId): WorldObject(objectId)
    {
        public override int getId() => 0;
        public override bool isAutoAttackable(Creature attacker) => false;
        public override void sendInfo(Player player)
        {
        }
    }

    private sealed class EmptyWorldQuery: INpcWorldQuery
    {
        public List<T> GetVisibleObjects<T>(WorldObject source) where T: WorldObject => [];
        public List<T> GetVisibleObjectsInRange<T>(WorldObject source, int range) where T: WorldObject => [];
        public void ForEachVisibleObjectInRange<T>(WorldObject source, int range, Action<T> action) where T: WorldObject
        {
        }
        public int GetWorldTick() => 0;
    }

    private sealed class TestWorldQuery(List<WorldObject> visible, int worldTick, Action? onWorldTick = null):
        INpcWorldQuery
    {
        public List<T> GetVisibleObjects<T>(WorldObject source) where T: WorldObject =>
            visible.OfType<T>().ToList();
        public List<T> GetVisibleObjectsInRange<T>(WorldObject source, int range) where T: WorldObject =>
            visible.OfType<T>().ToList();
        public void ForEachVisibleObjectInRange<T>(WorldObject source, int range, Action<T> action)
            where T: WorldObject => visible.OfType<T>().ToList().ForEach(action);
        public int GetWorldTick()
        {
            onWorldTick?.Invoke();
            return worldTick;
        }
    }

    private sealed class EmptyGeoQuery: INpcGeoQuery
    {
        public bool CanSeeTarget(WorldObject source, WorldObject target) => true;
        public bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance) => true;
        public Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance) => target;
    }

    private sealed class TestGeoQuery: INpcGeoQuery
    {
        public int VisibilityCalls { get; private set; }
        public int MovementCalls { get; private set; }
        public bool CanSeeTarget(WorldObject source, WorldObject target)
        {
            VisibilityCalls++;
            return true;
        }
        public bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance)
        {
            MovementCalls++;
            return true;
        }
        public Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance) => target;
    }

    private sealed class EmptyThreatQuery: INpcThreatQuery
    {
        public long GetHating(Attackable npc, Creature target) => 0;
        public Creature? GetMostHated(Attackable npc) => null;
        public IEnumerable<AggroInfo> GetAggroEntries(Attackable npc) => [];
    }

    private sealed class FixedThreatQuery(IReadOnlyCollection<AggroInfo> entries): INpcThreatQuery
    {
        public long GetHating(Attackable npc, Creature target) =>
            entries.FirstOrDefault(entry => ReferenceEquals(entry.getAttacker(), target))?.getHate() ?? 0;
        public Creature? GetMostHated(Attackable npc) => entries.MaxBy(static entry => entry.getHate())?.getAttacker();
        public IEnumerable<AggroInfo> GetAggroEntries(Attackable npc) => entries;
    }

    private sealed class FixedClock(long value): INpcPerceptionClock
    {
        public long GetMonotonicMilliseconds() => value;
    }

    private sealed class TestClock(long value): INpcPerceptionClock
    {
        public long Value { get; set; } = value;
        public long GetMonotonicMilliseconds() => Value;
    }

    private sealed class NullEntityResolver: ILegacyNpcEntityResolver
    {
        public WorldObject? Resolve(Attackable observer, EntityKey key) => null;
    }

    private sealed class FixedEntityResolver(WorldObject target): ILegacyNpcEntityResolver
    {
        public int ResolveCalls { get; private set; }
        public WorldObject? Resolve(Attackable observer, EntityKey key)
        {
            ResolveCalls++;
            return target.ObjectId == key.ObjectId ? target : null;
        }
    }

    private sealed class TargetReadingAttackableAI(Attackable actor, NpcAiDependencies dependencies):
        AttackableAI(actor, dependencies)
    {
        public WorldObject? ObservedTarget { get; private set; }
        public override void onEvtThink() => ObservedTarget = getTarget();
    }

    private sealed class RecordingCommandExecutor: ILegacyNpcCommandExecutor
    {
        public Creature? TargetActor { get; private set; }
        public WorldObject? Target { get; private set; }

        public void SetTarget(Creature actor, WorldObject? target)
        {
            TargetActor = actor;
            Target = target;
        }

        public void SetIntention(AbstractAI ai, CtrlIntention intention, object? argument = null) => throw Unexpected();
        public void MoveTo(AbstractAI ai, Location3D destination) => throw Unexpected();
        public void StartFollow(AbstractAI ai, Creature target) => throw Unexpected();
        public void SetRunning(Creature actor) => throw Unexpected();
        public void SetWalking(Creature actor) => throw Unexpected();
        public void ReturnHome(Attackable npc) => throw Unexpected();
        public void RestoreFullHealth(Creature actor) => throw Unexpected();
        public void Teleport(Creature actor, Location destination, bool randomOffset) => throw Unexpected();
        public void AbortAttack(Creature actor) => throw Unexpected();
        public void Cast(Creature actor, Skill skill, Item? item = null, bool forceUse = false, bool dontMove = false) => throw Unexpected();
        public void AutoAttack(Creature actor, Creature target) => throw Unexpected();
        public void AddThreat(Attackable npc, Creature target, long damage, long hate) => throw Unexpected();
        public void StopHating(Attackable npc, Creature? target) => throw Unexpected();
        public void ClearCombatMemory(Attackable npc) => throw Unexpected();
        public void PickUpDroppedItem(Attackable npc, Item item) => throw Unexpected();

        private static Exception Unexpected() => new InvalidOperationException("Unexpected command in test.");
    }

    private sealed class DeterministicRandomSource: INpcRandomSource
    {
        public int Next(int maxExclusive) => 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public bool NextBoolean() => false;
        public T Pick<T>(IReadOnlyList<T> values) => values[0];
        public T? PickOrDefault<T>(IReadOnlyList<T> values) => values.Count == 0 ? default : values[0];
    }
}
