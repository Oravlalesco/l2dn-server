using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;

namespace L2Dn.GameServer.AI.Runtime;

/// <summary>
/// Defines the deliberately narrow Phase 3 Intent vertical. Eligibility is based
/// on both the physical actor and AI implementation: sharing AttackableAI does
/// not imply that Guard, raid, minion, or scripted policies have been migrated.
/// </summary>
internal static class NpcBrainEligibility
{
    public static bool TryGetIntentAi(Attackable actor, out AttackableAI ai)
    {
        if (actor.GetType() == typeof(Monster) && actor.getAI() is AttackableAI candidate &&
            candidate.GetType() == typeof(AttackableAI))
        {
            ai = candidate;
            return true;
        }

        ai = null!;
        return false;
    }

    public static bool IsIntentEligible(Attackable actor, AttackableAI ai) =>
        actor.GetType() == typeof(Monster) && ai.GetType() == typeof(AttackableAI);
}
