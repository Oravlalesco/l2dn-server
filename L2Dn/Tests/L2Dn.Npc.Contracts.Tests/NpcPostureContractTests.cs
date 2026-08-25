using FluentAssertions;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Contracts.Tests;

/// <summary>
/// NPC-4B5-04 (4B.5.3) -- Contract tests for posture types.
/// Verifies cardinalidad, value-type invariants, and immutability
/// without depending on any Brain or GameServer logic.
/// </summary>
public class NpcPostureContractTests
{
    // -----------------------------------------------------------------------
    // 4B5-A7 partial: NpcStrategicPosture has exactly 5 values.
    // Cardinality is bounded for telemetry tag safety (no unbounded enum growth).
    // -----------------------------------------------------------------------
    [Fact]
    public void Posture_HasExactlyFiveValues()
    {
        Enum.GetValues<NpcStrategicPosture>().Length.Should().Be(5,
            because: "exactly 5 postures are defined: Neutral, Pressure, ControlRange, Recover, Disengage");
    }

    // -----------------------------------------------------------------------
    // 4B5-A7 partial: NpcPostureTransitionReason has at least 12 values.
    // Criterion 4B5-A7 mandates >=12 transition reasons.
    // -----------------------------------------------------------------------
    [Fact]
    public void TransitionReason_HasAtLeastTwelveValues()
    {
        Enum.GetValues<NpcPostureTransitionReason>().Length.Should().BeGreaterThanOrEqualTo(12,
            because: "4B5-A7 requires at least 12 distinct transition reasons");
    }

    // -----------------------------------------------------------------------
    // NpcStrategyDirective is a value type (zero heap allocation per Think tick).
    // readonly record struct in C# has no public setters by construction,
    // so we also verify the absence of mutable public properties.
    // -----------------------------------------------------------------------
    [Fact]
    public void StrategyDirective_IsValueType_And_Immutable()
    {
        Type type = typeof(NpcStrategyDirective);
        type.IsValueType.Should().BeTrue(
            because: "NpcStrategyDirective must be a struct to avoid heap allocation per Think tick");

        type.GetProperties().Should().OnlyContain(
            static p => p.SetMethod == null || p.SetMethod.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(static m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"),
            because: "NpcStrategyDirective must not expose regular (non-init) setters; init-only is acceptable for record structs");
    }

    // -----------------------------------------------------------------------
    // NpcPostureUtilities is a value type (diagnostics/replay snapshot).
    // -----------------------------------------------------------------------
    [Fact]
    public void PostureUtilities_IsValueType()
    {
        typeof(NpcPostureUtilities).IsValueType.Should().BeTrue(
            because: "NpcPostureUtilities is a diagnostic snapshot and must not allocate on the heap");
    }
}
