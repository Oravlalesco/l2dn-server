using System.Collections.Immutable;
using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

public class NpcStrategyDecisionComparerTests
{
    private static readonly NpcKey Actor = new(1, 2);
    private static readonly EntityKey TargetA = new(10, 0, EntityKind.Player);
    private static readonly EntityKey TargetB = new(11, 0, EntityKind.Player);

    [Fact]
    public void Comparison_has_deterministic_precedence()
    {
        Compare(Attack(TargetA, 1), Skill(100, TargetA, 1)).Kind
            .Should().Be(NpcStrategyComparisonKind.DifferentAction);
        Compare(Skill(100, TargetA, 1), Skill(101, TargetB, 1)).Kind
            .Should().Be(NpcStrategyComparisonKind.DifferentTarget);
        Compare(Skill(100, TargetA, 1), Skill(101, TargetA, 1)).Kind
            .Should().Be(NpcStrategyComparisonKind.DifferentSkill);
        Compare(Approach(TargetA, 40, 1), Approach(TargetA, 80, 1)).Kind
            .Should().Be(NpcStrategyComparisonKind.DifferentMovement);
    }

    [Fact]
    public void Comparison_distinguishes_exact_and_semantic_matches()
    {
        NpcIntent exact = Attack(TargetA, 1);
        Compare(exact, exact).Kind.Should().Be(NpcStrategyComparisonKind.ExactMatch);
        Compare(Attack(TargetA, 1), Attack(TargetA, 2)).Kind
            .Should().Be(NpcStrategyComparisonKind.SemanticMatch);
    }

    [Fact]
    public void Comparison_marks_multi_intent_decisions_as_not_comparable()
    {
        NpcBrainDecision baseline = Decision(Attack(TargetA, 1), Attack(TargetA, 1));

        NpcStrategyDecisionComparer.Compare(baseline, Decision(Attack(TargetA, 1))).Kind
            .Should().Be(NpcStrategyComparisonKind.NotComparable);
    }

    private static StrategyComparisonResult Compare(NpcIntent baseline, NpcIntent strategy) =>
        NpcStrategyDecisionComparer.Compare(Decision(baseline), Decision(strategy));

    private static NpcBrainDecision Decision(params NpcIntent[] intents) =>
        new(Actor, 1, NpcBrainLayer.Tactical, intents.ToImmutableArray());

    private static BasicAttackIntent Attack(EntityKey target, long sequence) =>
        new(Envelope(NpcIntentType.BasicAttack, sequence), target);

    private static CastSkillIntent Skill(int skillId, EntityKey target, long sequence) =>
        new(Envelope(NpcIntentType.CastSkill, sequence), skillId, 1, target);

    private static ApproachTargetIntent Approach(EntityKey target, int range, long sequence) =>
        new(Envelope(NpcIntentType.ApproachTarget, sequence), target, range);

    private static NpcIntentEnvelope Envelope(NpcIntentType type, long sequence) =>
        new(NpcIntent.CurrentSchemaVersion, Actor, 1, sequence, type);
}
