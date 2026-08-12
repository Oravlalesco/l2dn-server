using FluentAssertions;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Actor.Templates;

namespace L2Dn.GameServer.Model.Tests;

/// <summary>
/// Control AI events must not abort skill application when caster context is missing.
/// Live: Orc 20130 Stun (4072) showed the icon while the player could still move because
/// <c>notifyEvent(EVT_ACTION_BLOCKED)</c> threw inside <c>callSkill</c>.
/// These tests cover the Model notify contract only; BlockActions flags live in Scripts.
/// </summary>
public class ControlEffectAiNotifyTests
{
    private static int _nextTemplateId = 9_300_000;

    [Fact]
    public void StartParalyze_without_caster_does_not_throw()
    {
        Attackable actor = CreateActor();

        Action act = () => actor.startParalyze();

        act.Should().NotThrow();
    }

    [Fact]
    public void StartParalyze_with_caster_does_not_throw()
    {
        Attackable actor = CreateActor();
        Attackable caster = CreateActor();

        Action act = () => actor.startParalyze(caster);

        act.Should().NotThrow();
    }

    [Fact]
    public void ActionBlocked_notify_without_argument_does_not_throw()
    {
        Attackable actor = CreateActor();

        Action act = () => actor.getAI().notifyEvent(CtrlEvent.EVT_ACTION_BLOCKED);

        act.Should().NotThrow();
    }

    [Fact]
    public void Rooted_muted_and_confused_notify_without_caster_does_not_throw_or_self_aggro()
    {
        Attackable actor = CreateActor();
        Attackable caster = CreateActor();

        Action missing = () =>
        {
            actor.getAI().notifyEvent(CtrlEvent.EVT_ROOTED);
            actor.getAI().notifyEvent(CtrlEvent.EVT_MUTED);
            actor.getAI().notifyEvent(CtrlEvent.EVT_CONFUSED);
        };

        missing.Should().NotThrow();
        actor.getAggroList().IsEmpty.Should().BeTrue(
            "a missing caster must not fall back to the actor and add self-threat");

        Action withCaster = () =>
        {
            actor.getAI().notifyEvent(CtrlEvent.EVT_ROOTED, caster);
            actor.getAI().notifyEvent(CtrlEvent.EVT_MUTED, caster);
            actor.getAI().notifyEvent(CtrlEvent.EVT_CONFUSED, caster);
        };

        withCaster.Should().NotThrow();
    }

    private static Attackable CreateActor()
    {
        StatSet set = new();
        set.set("id", Interlocked.Increment(ref _nextTemplateId));
        set.set("type", "Monster");
        set.set("name", "Control Effect Test NPC");
        set.set("baseHpMax", 100d);
        set.set("baseMpMax", 100d);
        set.set("aggroRange", 500);
        Monster actor = new(new NpcTemplate(set));
        actor.beginRespawnLifecycle();
        actor.onRespawn();
        actor.completeRespawnLifecycle();
        actor.setSpawned(true);
        _ = actor.getAI();
        return actor;
    }
}
