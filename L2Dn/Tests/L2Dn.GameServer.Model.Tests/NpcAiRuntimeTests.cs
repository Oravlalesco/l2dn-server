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
            new EmptyThreatQuery(), commands);
        AttackableAI ai = new(actor, dependencies);

        ai.setTarget(target);

        commands.TargetActor.Should().BeSameAs(actor);
        commands.Target.Should().BeSameAs(target);
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

    private sealed class EmptyGeoQuery: INpcGeoQuery
    {
        public bool CanSeeTarget(WorldObject source, WorldObject target) => true;
        public bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance) => true;
        public Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance) => target;
    }

    private sealed class EmptyThreatQuery: INpcThreatQuery
    {
        public long GetHating(Attackable npc, Creature target) => 0;
        public Creature? GetMostHated(Attackable npc) => null;
        public IEnumerable<AggroInfo> GetAggroEntries(Attackable npc) => [];
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
}
