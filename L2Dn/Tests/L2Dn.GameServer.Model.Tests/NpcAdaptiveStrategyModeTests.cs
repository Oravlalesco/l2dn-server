using FluentAssertions;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.NpcBrain;

namespace L2Dn.GameServer.Model.Tests;

/// <summary>
/// NPC-4B5-01 — NpcAdaptiveStrategyMode parsing tests.
/// Verifies that NPC_STRATEGY_ADAPTIVE_MODE is parsed correctly via NpcStrategyOptions.FromEnvironment.
/// Lives here (L2Dn.GameServer.Model.Tests) because NpcStrategyOptions is internal to
/// L2Dn.GameServer.Model and InternalsVisibleTo only covers this project.
/// </summary>
public class NpcAdaptiveStrategyModeTests
{
    [Fact]
    public void AdaptiveMode_default_is_Disabled_when_env_var_not_set()
    {
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled, _ => null);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
    }

    [Fact]
    public void AdaptiveMode_Disabled_parses_correctly()
    {
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "Disabled" : null);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
    }

    [Fact]
    public void AdaptiveMode_Shadow_falls_back_to_Disabled_with_warning()
    {
        List<string> warnings = [];
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "Shadow" : null,
            warnings.Add);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
        warnings.Should().ContainSingle().Which.Should().Contain("Shadow");
    }

    [Fact]
    public void AdaptiveMode_Enabled_falls_back_to_Disabled_with_warning()
    {
        List<string> warnings = [];
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "Enabled" : null,
            warnings.Add);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
        warnings.Should().ContainSingle().Which.Should().Contain("Enabled");
    }

    [Fact]
    public void AdaptiveMode_unknown_value_falls_back_to_Disabled_with_warning()
    {
        List<string> warnings = [];
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "turbo_mode" : null,
            warnings.Add);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
        warnings.Should().ContainSingle().Which.Should().Contain("turbo_mode");
    }

    [Theory]
    [InlineData("DISABLED")]
    [InlineData("disabled")]
    [InlineData("Disabled")]
    [InlineData("DIS-ABLED")]
    public void AdaptiveMode_Disabled_parsing_is_case_and_separator_insensitive(string value)
    {
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? value : null);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
    }

    [Fact]
    public void AdaptiveMode_does_not_affect_NpcStrategyMode_parsing()
    {
        // Setting the adaptive env var must not interfere with the existing strategy mode.
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled, name => name switch
            {
                "NPC_STRATEGY_MODE" => "shadow",
                "NPC_STRATEGY_ADAPTIVE_MODE" => "Shadow", // falls back to Disabled
                _ => null
            });

        options.EffectiveMode.Should().Be(NpcStrategyMode.Shadow);
        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
    }
}
