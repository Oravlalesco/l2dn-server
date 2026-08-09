using FluentAssertions;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Contracts.Tests;

public class NpcIntentContractTests
{
    private static readonly NpcKey Actor = new(8172, 4);
    private static readonly EntityKey Target = new(9182, 0, EntityKind.Player);

    [Fact]
    public void All_intent_contracts_are_immutable()
    {
        Type[] intentTypes =
        [
            typeof(AcquireTargetIntent), typeof(ClearTargetIntent), typeof(BasicAttackIntent),
            typeof(ApproachTargetIntent), typeof(ReturnHomeIntent), typeof(FleeIntent),
            typeof(CastSkillIntent), typeof(StopCombatIntent)
        ];

        foreach (Type intentType in intentTypes)
        {
            intentType.GetProperties().Should().OnlyContain(static property => property.SetMethod == null,
                $"{intentType.Name} must not expose mutable properties");
        }
    }

    [Fact]
    public void Envelope_type_must_match_concrete_intent()
    {
        NpcIntentEnvelope invalid = Envelope(NpcIntentType.BasicAttack);

        Action create = () => new AcquireTargetIntent(invalid, Target);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Polymorphic_json_round_trip_preserves_all_intent_types()
    {
        NpcIntent[] intents =
        [
            new AcquireTargetIntent(Envelope(NpcIntentType.AcquireTarget), Target),
            new ClearTargetIntent(Envelope(NpcIntentType.ClearTarget), Target),
            new BasicAttackIntent(Envelope(NpcIntentType.BasicAttack), Target),
            new ApproachTargetIntent(Envelope(NpcIntentType.ApproachTarget), Target, 40),
            new ReturnHomeIntent(Envelope(NpcIntentType.ReturnHome)),
            new FleeIntent(Envelope(NpcIntentType.Flee), Target),
            new CastSkillIntent(Envelope(NpcIntentType.CastSkill), 107, 2, Target),
            new StopCombatIntent(Envelope(NpcIntentType.StopCombat))
        ];

        foreach (NpcIntent intent in intents)
        {
            NpcIntent? roundTrip = NpcIntentJsonCodec.Deserialize(NpcIntentJsonCodec.Serialize(intent));

            roundTrip.Should().Be(intent);
            roundTrip.Should().BeOfType(intent.GetType());
        }
    }

    [Fact]
    public void Semantic_comparison_ignores_revision_sequence_and_approach_distance()
    {
        ApproachTargetIntent first = new(
            new NpcIntentEnvelope(1, Actor, 10, 11, NpcIntentType.ApproachTarget), Target, 40);
        ApproachTargetIntent second = new(
            new NpcIntentEnvelope(1, Actor, 99, 100, NpcIntentType.ApproachTarget), Target, 80);

        NpcIntentSemanticComparer.Instance.Equals(first, second).Should().BeTrue();
        first.Should().NotBe(second);
    }

    [Fact]
    public void Execution_results_are_typed_and_do_not_require_exceptions()
    {
        NpcIntentExecutionResult executed = NpcIntentExecutionResult.Executed();
        NpcIntentExecutionResult rejected =
            NpcIntentExecutionResult.Rejected(NpcIntentRejectionReason.GenerationMismatch);

        executed.IsExecuted.Should().BeTrue();
        executed.RejectionReason.Should().Be(NpcIntentRejectionReason.None);
        rejected.Status.Should().Be(NpcIntentExecutionStatus.Rejected);
        rejected.RejectionReason.Should().Be(NpcIntentRejectionReason.GenerationMismatch);
    }

    private static NpcIntentEnvelope Envelope(NpcIntentType type) =>
        new(NpcIntent.CurrentSchemaVersion, Actor, 812, 3, type);
}
