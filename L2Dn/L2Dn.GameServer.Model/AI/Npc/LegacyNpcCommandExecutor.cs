using L2Dn.GameServer.Configuration;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.Geometry;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class LegacyNpcCommandExecutor: ILegacyNpcCommandExecutor
{
    public static LegacyNpcCommandExecutor Instance { get; } = new();

    private LegacyNpcCommandExecutor()
    {
    }

    public void SetIntention(AbstractAI ai, CtrlIntention intention, object? argument = null)
    {
        LegacyNpcCommandObserver.RecordSetIntention(ai, intention, argument);
        Execute("set_intention", () => ai.setIntention(intention, argument));
    }

    public void MoveTo(AbstractAI ai, Location3D destination)
    {
        LegacyNpcCommandObserver.RecordMove(ai);
        Execute("move_to", () => ai.moveTo(destination));
    }

    public void StartFollow(AbstractAI ai, Creature target, int range = -1, bool wakeWhenInRange = false) =>
        Execute("start_follow", () => ai.startFollow(target, range, wakeWhenInRange));

    public void StopFollow(AbstractAI ai) => Execute("stop_follow", ai.stopFollow);

    public void StopMovement(AbstractAI ai) => Execute("stop_movement", () => ai.clientStopMoving(null));

    public void SetTarget(Creature actor, WorldObject? target)
    {
        LegacyNpcCommandObserver.RecordTarget(actor, target);
        Execute("set_target", () => actor.setTarget(target));
    }

    public void SetRunning(Creature actor) => Execute("set_running", actor.setRunning);

    public void SetWalking(Creature actor) => Execute("set_walking", actor.setWalking);

    public void ReturnHome(Attackable npc)
    {
        LegacyNpcCommandObserver.RecordReturnHome(npc);
        Execute("return_home", npc.returnHome);
    }

    public void RestoreFullHealth(Creature actor) => Execute("restore_full_health", () =>
    {
        actor.setCurrentHp(actor.getMaxHp());
        actor.setCurrentMp(actor.getMaxMp());
    });

    public void Teleport(Creature actor, Location destination, bool randomOffset) =>
        Execute("teleport", () => actor.teleToLocation(destination, randomOffset));

    public void AbortAttack(Creature actor)
    {
        LegacyNpcCommandObserver.RecordStopCombat(actor);
        Execute("abort_attack", actor.abortAttack);
    }

    public void Cast(Creature actor, Skill skill, Item? item = null, bool forceUse = false, bool dontMove = false)
    {
        LegacyNpcCommandObserver.RecordCast(actor, skill);
        Execute("cast", () => actor.doCast(skill, item, forceUse, dontMove));
    }

    public void AutoAttack(Creature actor, Creature target)
    {
        LegacyNpcCommandObserver.RecordAutoAttack(actor, target);
        Execute("auto_attack", () => actor.doAutoAttack(target));
    }

    public void AddThreat(Attackable npc, Creature target, long damage, long hate) =>
        Execute("add_threat", () => npc.addDamageHate(target, damage, hate));

    public void StopHating(Attackable npc, Creature? target) =>
        Execute("stop_hating", () => npc.stopHating(target));

    public void ClearCombatMemory(Attackable npc)
    {
        LegacyNpcCommandObserver.RecordStopCombat(npc);
        Execute("clear_combat_memory", () =>
    {
        npc.clearAggroList();
        npc.getAttackByList().clear();
    });
    }

    public void PickUpDroppedItem(Attackable npc, Item item) => Execute("pick_up", () =>
    {
        item.pickupMe(npc);
        if (Config.General.SAVE_DROPPED_ITEM)
        {
            ItemsOnGroundManager.getInstance().removeObject(item);
        }

        if (item.getTemplate().hasExImmediateEffect())
        {
            foreach (ItemSkillHolder skillHolder in item.getTemplate().getAllSkills())
            {
                SkillCaster.triggerCast(npc, null, skillHolder.getSkill(), null, false);
            }

            npc.broadcastInfo(); // Preserve the legacy fake-player pickup refresh.
        }
    });

    private static void Execute(string command, Action action) => NpcAiTelemetry.ObserveCommand(command, action);
}
