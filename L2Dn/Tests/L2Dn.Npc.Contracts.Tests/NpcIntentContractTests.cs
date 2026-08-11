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
            new AcquireTargetIntent(Envelope(NpcIntentType.AcquireTarget), Target,
                NpcTargetAcquisitionMode.PreserveMovement),
            new ClearTargetIntent(Envelope(NpcIntentType.ClearTarget), Target),
            new BasicAttackIntent(Envelope(NpcIntentType.BasicAttack), Target),
            new ApproachTargetIntent(Envelope(NpcIntentType.ApproachTarget), Target, 40,
                NpcApproachConstraint.TowardSpawnOnly),
            new ReturnHomeIntent(Envelope(NpcIntentType.ReturnHome), NpcReturnHomeMode.PreserveThreat),
            new ReturnHomeIntent(Envelope(NpcIntentType.ReturnHome), NpcReturnHomeMode.TeleportReset),
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
    public void Semantic_comparison_includes_movement_and_return_policy()
    {
        ApproachTargetIntent unrestricted = new(
            Envelope(NpcIntentType.ApproachTarget), Target, 40);
        ApproachTargetIntent homeward = new(
            Envelope(NpcIntentType.ApproachTarget), Target, 40, NpcApproachConstraint.TowardSpawnOnly);
        ReturnHomeIntent reset = new(Envelope(NpcIntentType.ReturnHome));
        ReturnHomeIntent preserve = new(Envelope(NpcIntentType.ReturnHome), NpcReturnHomeMode.PreserveThreat);
        AcquireTargetIntent engage = new(Envelope(NpcIntentType.AcquireTarget), Target);
        AcquireTargetIntent preserveMovement = new(Envelope(NpcIntentType.AcquireTarget), Target,
            NpcTargetAcquisitionMode.PreserveMovement);

        NpcIntentSemanticComparer.Instance.Equals(unrestricted, homeward).Should().BeFalse();
        NpcIntentSemanticComparer.Instance.Equals(reset, preserve).Should().BeFalse();
        NpcIntentSemanticComparer.Instance.Equals(engage, preserveMovement).Should().BeFalse();
    }

    [Fact]
    public void Older_json_without_new_policy_fields_uses_safe_defaults()
    {
        const string approachJson =
            """{"$intent":"approachTarget","envelope":{"schemaVersion":1,"actor":{"objectId":8172,"generation":4},"basedOnStateRevision":812,"decisionSequence":3,"intentType":4},"target":{"objectId":9182,"generation":0,"kind":1},"preferredRange":40}""";
        const string returnJson =
            """{"$intent":"returnHome","envelope":{"schemaVersion":1,"actor":{"objectId":8172,"generation":4},"basedOnStateRevision":812,"decisionSequence":3,"intentType":5}}""";
        const string acquireJson =
            """{"$intent":"acquireTarget","envelope":{"schemaVersion":1,"actor":{"objectId":8172,"generation":4},"basedOnStateRevision":812,"decisionSequence":3,"intentType":1},"target":{"objectId":9182,"generation":0,"kind":1}}""";

        ((ApproachTargetIntent)NpcIntentJsonCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(approachJson))!)
            .Constraint.Should().Be(NpcApproachConstraint.None);
        ((ReturnHomeIntent)NpcIntentJsonCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(returnJson))!)
            .Mode.Should().Be(NpcReturnHomeMode.ResetCombat);
        ((AcquireTargetIntent)NpcIntentJsonCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(acquireJson))!)
            .Mode.Should().Be(NpcTargetAcquisitionMode.Engage);
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
