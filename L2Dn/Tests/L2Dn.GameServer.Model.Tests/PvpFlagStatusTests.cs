using FluentAssertions;
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
}
