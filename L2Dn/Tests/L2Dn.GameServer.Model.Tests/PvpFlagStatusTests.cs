using FluentAssertions;
using L2Dn.GameServer.Cache;
using L2Dn.GameServer.Enums;

namespace L2Dn.GameServer.Model.Tests;

public class PvpFlagStatusTests
{
    [Theory]
    [InlineData(PvpFlagStatus.None, false, true)]
    [InlineData(PvpFlagStatus.Enabled, true, false)]
    [InlineData(PvpFlagStatus.Flashing, true, false)]
    public void Flagged_and_unflagged_classifications_are_complementary(
        PvpFlagStatus status, bool isFlagged, bool isUnflagged)
    {
        status.IsFlagged().Should().Be(isFlagged);
        status.IsUnflagged().Should().Be(isUnflagged);
        status.IsFlagged().Should().Be(!status.IsUnflagged());
    }

    [Fact]
    public void Active_flag_is_enabled_before_flashing_window()
    {
        DateTime expiresAt = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        PvpFlagStateMachine.Evaluate(expiresAt - TimeSpan.FromSeconds(21), expiresAt)
            .Should().Be(PvpFlagStatus.Enabled);
    }

    [Fact]
    public void Active_flag_flashes_during_final_twenty_seconds()
    {
        DateTime expiresAt = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        PvpFlagStateMachine.Evaluate(expiresAt - TimeSpan.FromSeconds(20), expiresAt)
            .Should().Be(PvpFlagStatus.Flashing);
    }

    [Fact]
    public void Flag_expires_at_deadline()
    {
        DateTime expiresAt = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

        PvpFlagStateMachine.Evaluate(expiresAt, expiresAt).Should().Be(PvpFlagStatus.None);
    }

    [Fact]
    public void Relation_cache_detects_pvp_flag_only_changes()
    {
        RelationCache cache = new(0, false, 0, PvpFlagStatus.None);

        cache.Matches(0, false, 0, PvpFlagStatus.None).Should().BeTrue();
        cache.Matches(0, false, 0, PvpFlagStatus.Enabled).Should().BeFalse();
        cache.Matches(0, false, 0, PvpFlagStatus.Flashing).Should().BeFalse();
    }

    [Fact]
    public void Relation_cache_detects_reputation_only_changes()
    {
        RelationCache cache = new(0, false, 0, PvpFlagStatus.None);

        cache.Matches(0, false, -1, PvpFlagStatus.None).Should().BeFalse();
    }
}
