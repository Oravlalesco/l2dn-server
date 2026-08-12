using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class ReflexBrain
{
    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcBrainContext context,
        NpcIntelligenceProfile profile, NpcBrainState state, long decisionSequence) =>
        DecideCore(perception, context, profile, profile.FleeHpPercent, state, decisionSequence);

    internal NpcIntent? Decide(NpcPerceptionSnapshot perception, NpcBrainContext context,
        NpcIntelligenceProfile profile, NpcStrategyDecision strategy,
        NpcBrainState state, long decisionSequence) =>
        DecideCore(perception, context, profile, strategy.EffectiveFleeHpPercent, state, decisionSequence);

    private static NpcIntent? DecideCore(NpcPerceptionSnapshot perception, NpcBrainContext context,
        NpcIntelligenceProfile profile, double fleeHpPercent,
        NpcBrainState state, long decisionSequence)
    {
        if (!profile.ReflexEnabled || !NpcPerceptionFacts.IsActorOperational(perception))
        {
            ResetReturnState(state);
            return null;
        }

        NpcPerceptionEnvelope snapshot = perception.Envelope;
        EntityKey? currentTarget = perception.State.Combat.CurrentTarget;
        EntityKey? highestThreat = NpcPerceptionFacts.SelectHighestVisibleThreat(perception);
        NpcEnvironment environment = perception.State.Environment;
        if (environment.SpawnPosition is { } spawn)
        {
            double distanceFromSpawn = NpcPerceptionFacts.Distance2D(perception.State.Physical.Position, spawn);
            int returnHomeDistance = environment.ReturnHomeDistance;
            int combatLeashDistance = environment.CombatLeashDistance;
            bool atHome = returnHomeDistance > 0 && distanceFromSpawn <= returnHomeDistance;
            bool outsideCombatLeash = combatLeashDistance > 0 && distanceFromSpawn > combatLeashDistance;
            bool attacked = (context.Stimuli & NpcBrainStimulus.Attacked) != 0;
            bool threatChanged = (context.Stimuli & NpcBrainStimulus.ThreatChanged) != 0;
            NpcReturnDefensePolicy defensePolicy = context.ReturnDefense ?? NpcReturnDefensePolicy.Default;
            bool outsideHardLeash = outsideCombatLeash && defensePolicy.HardLeashExtension >= 0 &&
                distanceFromSpawn > (long)combatLeashDistance + defensePolicy.HardLeashExtension;

            if (environment.ReturningToSpawn && state.ReturnState == NpcReturnEngagementState.None)
            {
                state.ReturnState = NpcReturnEngagementState.ReturningHome;
            }

            if (state.ReturnState == NpcReturnEngagementState.LeashGrace)
            {
                if (!outsideCombatLeash)
                {
                    ResumeCombat(state);
                }
                else if (outsideHardLeash || GraceExpired(state, snapshot.WorldTick))
                {
                    if (!defensePolicy.Enabled || !highestThreat.HasValue)
                    {
                        SetReturningHome(state);
                        return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.ResetCombat);
                    }

                    EnterDefensiveReturn(state, highestThreat.Value, snapshot.WorldTick,
                        defensePolicy.TimeoutWorldTicks);
                    state.ReturnMovementIssued = true;
                    return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.PreserveThreat);
                }
            }

            if (state.ReturnState == NpcReturnEngagementState.DefensiveReturn)
            {
                if (atHome)
                {
                    ResetReturnState(state);
                    return new StopCombatIntent(Envelope(snapshot, decisionSequence, NpcIntentType.StopCombat));
                }

                bool threatOutsideCombatLeash = combatLeashDistance > 0 && highestThreat.HasValue &&
                    !IsEntityInsideLeash(perception, highestThreat.Value, spawn, combatLeashDistance);
                if (!outsideCombatLeash && !threatOutsideCombatLeash)
                {
                    // Ordinary pursuit resumes only after both actor and threat are
                    // back inside the authoritative territory.
                    ResumeCombat(state);
                }
                else
                {
                    if ((attacked || threatChanged) && defensePolicy.TimeoutWorldTicks > 0)
                    {
                        state.ReturnDefenseExpiresAtWorldTick = AddSaturating(snapshot.WorldTick,
                            defensePolicy.TimeoutWorldTicks);
                    }

                    if (!defensePolicy.Enabled || DefenseExpired(state, snapshot.WorldTick) ||
                        !highestThreat.HasValue)
                    {
                        SetReturningHome(state);
                        return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.ResetCombat);
                    }

                    if (state.ReturnDefenseTarget != highestThreat)
                    {
                        state.ReturnDefenseTarget = highestThreat;
                        state.ReturnMovementIssued = false;
                    }

                    if (!currentTarget.HasValue || currentTarget != highestThreat ||
                        !NpcPerceptionFacts.TryGetValidTarget(perception, currentTarget.Value, out _, out _))
                    {
                        return new AcquireTargetIntent(
                            Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                            highestThreat.Value, NpcTargetAcquisitionMode.PreserveMovement);
                    }

                    // Tactical decides whether to attack in place, approach homeward,
                    // or keep returning without discarding the current hate table.
                    return null;
                }
            }

            if (outsideCombatLeash && currentTarget.HasValue)
            {
                if (!highestThreat.HasValue)
                {
                    SetReturningHome(state);
                    return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.ResetCombat);
                }

                if (!defensePolicy.Enabled)
                {
                    SetReturningHome(state);
                    return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.ResetCombat);
                }

                EnterLeashGrace(state, snapshot.WorldTick, defensePolicy.LeashGraceWorldTicks);
                if (ShouldTeleportHome(state, defensePolicy) || outsideHardLeash)
                {
                    if (ShouldTeleportHome(state, defensePolicy))
                    {
                        ResetReturnState(state);
                        return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.TeleportReset);
                    }

                    if (defensePolicy.Enabled)
                    {
                        EnterDefensiveReturn(state, highestThreat.Value, snapshot.WorldTick,
                            defensePolicy.TimeoutWorldTicks);
                        state.ReturnMovementIssued = true;
                        return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.PreserveThreat);
                    }

                    SetReturningHome(state);
                    return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.ResetCombat);
                }

                // Crossing the soft leash opens one fixed grace window. Damage does
                // not renew it, so a ranged attacker cannot keep the chase alive forever.
            }

            if (environment.ReturningToSpawn && returnHomeDistance > 0 &&
                distanceFromSpawn > returnHomeDistance)
            {
                state.ReturnState = NpcReturnEngagementState.ReturningHome;
                if (attacked && highestThreat.HasValue)
                {
                    if (outsideCombatLeash &&
                        (!defensePolicy.Enabled || defensePolicy.TimeoutWorldTicks <= 0))
                    {
                        SetReturningHome(state);
                        return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.ResetCombat);
                    }

                    if (outsideCombatLeash)
                    {
                        EnterDefensiveReturn(state, highestThreat.Value, snapshot.WorldTick,
                            defensePolicy.TimeoutWorldTicks);
                    }
                    else
                    {
                        ResumeCombat(state);
                    }

                    return new AcquireTargetIntent(
                        Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                        highestThreat.Value, outsideCombatLeash
                            ? NpcTargetAcquisitionMode.PreserveMovement
                            : NpcTargetAcquisitionMode.Engage);
                }

                // Visibility and faction events alone never interrupt a physical return.
                return null;
            }

            bool outsideHomeRange = !currentTarget.HasValue && returnHomeDistance > 0 &&
                distanceFromSpawn > returnHomeDistance;
            if (environment.CanReturnToSpawn && outsideHomeRange)
            {
                SetReturningHome(state);
                return ReturnHome(snapshot, decisionSequence, NpcReturnHomeMode.ResetCombat);
            }

            if (atHome && !currentTarget.HasValue &&
                (state.ReturnState != NpcReturnEngagementState.None || state.LeashExcursionCount > 0))
            {
                ResetReturnState(state);
            }
        }
        else
        {
            ResetReturnState(state);
        }

        if (currentTarget.HasValue)
        {
            if (!NpcPerceptionFacts.TryGetValidTarget(perception, currentTarget.Value, out _, out _))
            {
                if (highestThreat.HasValue && highestThreat.Value != currentTarget.Value)
                {
                    return new AcquireTargetIntent(
                        Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                        highestThreat.Value);
                }
                return new ClearTargetIntent(Envelope(snapshot, decisionSequence, NpcIntentType.ClearTarget),
                    currentTarget);
            }

            if (highestThreat.HasValue && highestThreat.Value != currentTarget.Value)
            {
                return new AcquireTargetIntent(
                    Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                    highestThreat.Value);
            }

            if (profile.FleeAllowed &&
                NpcPerceptionFacts.HpPercent(perception.State.Physical) <= fleeHpPercent)
            {
                return new FleeIntent(Envelope(snapshot, decisionSequence, NpcIntentType.Flee), currentTarget);
            }

            return null;
        }

        EntityKey? selected = NpcPerceptionFacts.SelectTarget(perception, profile);
        if (selected.HasValue)
        {
            return new AcquireTargetIntent(Envelope(snapshot, decisionSequence, NpcIntentType.AcquireTarget),
                selected.Value);
        }

        return null;
    }

    private static bool DefenseExpired(NpcBrainState state, long worldTick) =>
        !state.ReturnDefenseTarget.HasValue || worldTick >= state.ReturnDefenseExpiresAtWorldTick;

    private static bool GraceExpired(NpcBrainState state, long worldTick) =>
        state.LeashGraceExpiresAtWorldTick <= 0 || worldTick >= state.LeashGraceExpiresAtWorldTick;

    private static bool ShouldTeleportHome(NpcBrainState state, NpcReturnDefensePolicy policy) =>
        policy.MaxLeashExcursions > 0 && state.LeashExcursionCount >= policy.MaxLeashExcursions;

    private static bool IsEntityInsideLeash(NpcPerceptionSnapshot perception, EntityKey entity,
        NpcPosition spawn, int combatLeashDistance) =>
        NpcPerceptionFacts.TryGetVisibleEntity(perception, entity, out VisibleEntity visible) &&
        NpcPerceptionFacts.Distance2D(visible.Position, spawn) <= combatLeashDistance;

    private static void EnterDefensiveReturn(NpcBrainState state, EntityKey target, long worldTick,
        int timeoutWorldTicks)
    {
        state.ReturnState = NpcReturnEngagementState.DefensiveReturn;
        state.ReturnDefenseTarget = target;
        state.ReturnDefenseExpiresAtWorldTick = timeoutWorldTicks > 0
            ? AddSaturating(worldTick, timeoutWorldTicks)
            : worldTick;
        state.LeashGraceExpiresAtWorldTick = 0;
        state.ReturnMovementIssued = false;
    }

    private static void EnterLeashGrace(NpcBrainState state, long worldTick, int graceWorldTicks)
    {
        if (state.ReturnState == NpcReturnEngagementState.LeashGrace)
        {
            return;
        }

        state.ReturnState = NpcReturnEngagementState.LeashGrace;
        state.LeashExcursionCount = state.LeashExcursionCount == int.MaxValue
            ? int.MaxValue
            : state.LeashExcursionCount + 1;
        state.LeashGraceExpiresAtWorldTick = graceWorldTicks > 0
            ? AddSaturating(worldTick, graceWorldTicks)
            : worldTick;
        state.ReturnDefenseTarget = null;
        state.ReturnDefenseExpiresAtWorldTick = 0;
        state.ReturnMovementIssued = false;
    }

    private static void SetReturningHome(NpcBrainState state)
    {
        state.ReturnState = NpcReturnEngagementState.ReturningHome;
        state.ReturnDefenseTarget = null;
        state.ReturnDefenseExpiresAtWorldTick = 0;
        state.LeashGraceExpiresAtWorldTick = 0;
        state.ReturnMovementIssued = false;
    }

    private static void ResumeCombat(NpcBrainState state)
    {
        state.ReturnState = NpcReturnEngagementState.None;
        state.ReturnDefenseTarget = null;
        state.ReturnDefenseExpiresAtWorldTick = 0;
        state.LeashGraceExpiresAtWorldTick = 0;
        state.ReturnMovementIssued = false;
    }

    private static void ResetReturnState(NpcBrainState state)
    {
        ResumeCombat(state);
        state.LeashExcursionCount = 0;
    }

    private static ReturnHomeIntent ReturnHome(NpcPerceptionEnvelope snapshot, long sequence,
        NpcReturnHomeMode mode) =>
        new(Envelope(snapshot, sequence, NpcIntentType.ReturnHome), mode);

    private static long AddSaturating(long value, int increment) =>
        increment > 0 && value > long.MaxValue - increment ? long.MaxValue : value + increment;

    private static NpcIntentEnvelope Envelope(NpcPerceptionEnvelope snapshot, long sequence, NpcIntentType type) =>
        new(NpcIntent.CurrentSchemaVersion, snapshot.Npc, snapshot.StateRevision, sequence, type);
}
