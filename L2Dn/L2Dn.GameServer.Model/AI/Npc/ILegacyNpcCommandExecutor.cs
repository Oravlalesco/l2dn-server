using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.Geometry;

namespace L2Dn.GameServer.AI.Runtime;

internal interface ILegacyNpcCommandExecutor
{
    void SetIntention(AbstractAI ai, CtrlIntention intention, object? argument = null);
    void MoveTo(AbstractAI ai, Location3D destination);
    void StartFollow(AbstractAI ai, Creature target, int range = -1);
    void StopFollow(AbstractAI ai);
    void SetTarget(Creature actor, WorldObject? target);
    void SetRunning(Creature actor);
    void SetWalking(Creature actor);
    void ReturnHome(Attackable npc);
    void RestoreFullHealth(Creature actor);
    void Teleport(Creature actor, Location destination, bool randomOffset);
    void AbortAttack(Creature actor);
    void Cast(Creature actor, Skill skill, Item? item = null, bool forceUse = false, bool dontMove = false);
    void AutoAttack(Creature actor, Creature target);
    void AddThreat(Attackable npc, Creature target, long damage, long hate);
    void StopHating(Attackable npc, Creature? target);
    void ClearCombatMemory(Attackable npc);
    void PickUpDroppedItem(Attackable npc, Item item);
}
