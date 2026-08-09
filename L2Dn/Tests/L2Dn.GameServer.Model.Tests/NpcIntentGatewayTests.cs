using FluentAssertions;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.InstanceZones;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.Geometry;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.Model.Tests;

public class NpcIntentGatewayTests
{
    private static int _nextTemplateId = 9_200_000;

    [Fact]
    public void Generation_mismatch_is_rejected_before_execution()
    {
        Attackable actor = CreateActor();
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor);
        BasicAttackIntent intent = AttackIntent(actor, new EntityKey(999, 0, EntityKind.Player),
            actor.getSpawnGeneration() - 1);

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.RejectionReason.Should().Be(NpcIntentRejectionReason.GenerationMismatch);
        commands.AutoAttackTarget.Should().BeNull();
    }

    [Fact]
    public void Stale_revision_is_live_revalidated_and_can_execute()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, target, currentRevision: 99);

        NpcIntentExecutionResult result = gateway.Execute(AttackIntent(actor, Key(target), revision: 1));

        result.IsExecuted.Should().BeTrue();
        commands.AutoAttackTarget.Should().BeSameAs(target);
    }

    [Fact]
    public void Target_dying_between_decision_and_gateway_is_rejected_without_exception()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        BasicAttackIntent intent = AttackIntent(actor, Key(target));
        target.setDead(true);
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor, target);

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.RejectionReason.Should().Be(NpcIntentRejectionReason.TargetDead);
    }

    [Fact]
    public void Target_disappearing_between_decision_and_gateway_is_rejected()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        BasicAttackIntent intent = AttackIntent(actor, Key(target));
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.TargetNotFound);
    }

    [Fact]
    public void Actor_becoming_disabled_before_attack_is_rejected()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        actor.disableAllSkills();
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor, target);

        gateway.Execute(AttackIntent(actor, Key(target))).RejectionReason
            .Should().Be(NpcIntentRejectionReason.Policy);
    }

    [Fact]
    public void Movement_blocked_after_decision_is_rejected_by_authoritative_geo()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        NpcIntentEnvelope envelope = Envelope(actor, NpcIntentType.ApproachTarget);
        ApproachTargetIntent intent = new(envelope, Key(target), 40);
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), false, actor, target);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.Blocked);
    }

    [Fact]
    public void Skill_entering_cooldown_after_decision_is_rejected()
    {
        Attackable actor = CreateActor();
        Skill skill = CreateSkill(7001, 1);
        actor.addSkill(skill);
        actor.addTimeStamp(skill, TimeSpan.FromSeconds(30));
        CastSkillIntent intent = new(Envelope(actor, NpcIntentType.CastSkill), skill.getId(), skill.getLevel(), null);
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.Cooldown);
    }

    [Fact]
    public void Brain_mode_parses_independently_from_reactive_scheduler_mode()
    {
        NpcBrainOptions.FromEnvironment(name => name == "NPC_BRAIN_MODE" ? "shadow" : null).Mode
            .Should().Be(NpcBrainMode.Shadow);
        NpcBrainOptions.FromEnvironment(name => name == "NPC_BRAIN_MODE" ? "INTENT" : null).Mode
            .Should().Be(NpcBrainMode.Intent);
        NpcBrainOptions.FromEnvironment(_ => "invalid").Mode.Should().Be(NpcBrainMode.Legacy);
    }

    [Fact]
    public void Shadow_comparison_is_semantic_across_revision_metadata()
    {
        Attackable actor = CreateActor();
        EntityKey target = new(8127, 0, EntityKind.Player);
        AcquireTargetIntent brain = new(Envelope(actor, NpcIntentType.AcquireTarget, revision: 1), target);
        AcquireTargetIntent legacy = new(Envelope(actor, NpcIntentType.AcquireTarget, revision: 9), target);

        ShadowNpcThinkExecutor.Compare([brain], [legacy]).Should().Be(NpcIntentComparisonKind.SemanticMatch);
    }

    private static NpcIntentGateway CreateGateway(RecordingCommands commands, params Attackable[] actors) =>
        CreateGateway(commands, true, actors);

    private static NpcIntentGateway CreateGateway(RecordingCommands commands, Attackable actor,
        Attackable target, long currentRevision) =>
        new(id => new[] { actor, target }.FirstOrDefault(item => item.ObjectId == id), _ => currentRevision,
            new TestGeo(true), commands);

    private static NpcIntentGateway CreateGateway(RecordingCommands commands, bool geoAllowed,
        params Attackable[] actors) =>
        new(id => actors.FirstOrDefault(item => item.ObjectId == id), _ => 1,
            new TestGeo(geoAllowed), commands);

    private static BasicAttackIntent AttackIntent(Attackable actor, EntityKey target, int? generation = null,
        long revision = 1) =>
        new(new NpcIntentEnvelope(1, new NpcKey(actor.ObjectId, generation ?? actor.getSpawnGeneration()),
            revision, 1, NpcIntentType.BasicAttack), target);

    private static NpcIntentEnvelope Envelope(Attackable actor, NpcIntentType type, long revision = 1) =>
        new(1, new NpcKey(actor.ObjectId, actor.getSpawnGeneration()), revision, 1, type);

    private static EntityKey Key(Attackable actor) =>
        new(actor.ObjectId, actor.getSpawnGeneration(), EntityKind.Npc);

    private static Attackable CreateActor()
    {
        Attackable actor = new(CreateNpcTemplate());
        actor.beginRespawnLifecycle();
        actor.onRespawn();
        actor.completeRespawnLifecycle();
        actor.setSpawned(true);
        _ = actor.getAI();
        return actor;
    }

    private static NpcTemplate CreateNpcTemplate()
    {
        StatSet set = new();
        set.set("id", Interlocked.Increment(ref _nextTemplateId));
        set.set("type", "Monster");
        set.set("name", "Intent Test NPC");
        set.set("baseHpMax", 100d);
        set.set("baseMpMax", 100d);
        return new NpcTemplate(set);
    }

    private static Skill CreateSkill(int id, int level)
    {
        StatSet set = new();
        set.set(".id", id);
        set.set(".level", level);
        set.set(".name", "Intent Test Skill");
        set.set("operateType", SkillOperateType.A1);
        return new Skill(set);
    }

    private sealed class TestGeo(bool allowed): INpcGeoQuery
    {
        public bool CanSeeTarget(WorldObject source, WorldObject target) => allowed;
        public bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance) => allowed;
        public Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance) =>
            allowed ? target : source;
    }

    private sealed class RecordingCommands: ILegacyNpcCommandExecutor
    {
        public Creature? AutoAttackTarget { get; private set; }
        public void AutoAttack(Creature actor, Creature target) => AutoAttackTarget = target;
        public void SetIntention(AbstractAI ai, CtrlIntention intention, object? argument = null) { }
        public void MoveTo(AbstractAI ai, Location3D destination) { }
        public void StartFollow(AbstractAI ai, Creature target) { }
        public void SetTarget(Creature actor, WorldObject? target) => actor.setTarget(target);
        public void SetRunning(Creature actor) => actor.setRunning();
        public void SetWalking(Creature actor) => actor.setWalking();
        public void ReturnHome(Attackable npc) { }
        public void RestoreFullHealth(Creature actor) { }
        public void Teleport(Creature actor, Location destination, bool randomOffset) { }
        public void AbortAttack(Creature actor) { }
        public void Cast(Creature actor, Skill skill, Item? item = null, bool forceUse = false, bool dontMove = false) { }
        public void AddThreat(Attackable npc, Creature target, long damage, long hate) =>
            npc.addDamageHate(target, damage, hate);
        public void StopHating(Attackable npc, Creature? target) => npc.stopHating(target);
        public void ClearCombatMemory(Attackable npc) => npc.clearAggroList();
        public void PickUpDroppedItem(Attackable npc, Item item) { }
    }
}
