using FluentAssertions;
using L2Dn.GameServer.Model.DailyMissions;
using L2Dn.Model.Enums;

namespace L2Dn.GameServer.Model.Tests;

public class DailyMissionCycleTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Theory]
    [InlineData("2026-08-03T06:29:59Z", "2026-08-02T06:30:00Z")]
    [InlineData("2026-08-03T06:30:00Z", "2026-08-03T06:30:00Z")]
    [InlineData("2026-08-03T23:59:59Z", "2026-08-03T06:30:00Z")]
    public void Daily_cycle_changes_exactly_at_0630(string nowText, string expectedText)
    {
        DateTimeOffset now = DateTimeOffset.Parse(nowText);
        DateTime expected = DateTime.Parse(expectedText).ToUniversalTime();

        DailyMissionCycle.getCycleStartUtc(MissionResetType.DAY, now, Utc).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-08-03T06:29:59Z", "2026-07-27T06:30:00Z")]
    [InlineData("2026-08-03T06:30:00Z", "2026-08-03T06:30:00Z")]
    [InlineData("2026-08-09T23:59:59Z", "2026-08-03T06:30:00Z")]
    public void Weekly_cycle_starts_on_monday_at_0630(string nowText, string expectedText)
    {
        DateTimeOffset now = DateTimeOffset.Parse(nowText);
        DateTime expected = DateTime.Parse(expectedText).ToUniversalTime();

        DailyMissionCycle.getCycleStartUtc(MissionResetType.WEEK, now, Utc).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-08-01T06:29:59Z", "2026-07-01T06:30:00Z")]
    [InlineData("2026-08-01T06:30:00Z", "2026-08-01T06:30:00Z")]
    [InlineData("2026-08-31T23:59:59Z", "2026-08-01T06:30:00Z")]
    public void Monthly_cycle_starts_on_the_first_at_0630(string nowText, string expectedText)
    {
        DateTimeOffset now = DateTimeOffset.Parse(nowText);
        DateTime expected = DateTime.Parse(expectedText).ToUniversalTime();

        DailyMissionCycle.getCycleStartUtc(MissionResetType.MONTH, now, Utc).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-08-08T06:29:59Z", false)]
    [InlineData("2026-08-08T06:30:00Z", true)]
    [InlineData("2026-08-09T12:00:00Z", true)]
    [InlineData("2026-08-10T06:29:59Z", true)]
    [InlineData("2026-08-10T06:30:00Z", false)]
    public void Weekend_window_runs_from_saturday_to_monday_at_0630(string nowText, bool expected)
    {
        DailyMissionCycle.isWeekendWindow(DateTimeOffset.Parse(nowText), Utc).Should().Be(expected);
    }

    [Fact]
    public void Next_reset_is_calculated_from_the_current_cycle()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-08-03T06:29:59Z");

        DailyMissionCycle.getNextResetUtc(MissionResetType.DAY, now, Utc)
            .Should().Be(DateTime.Parse("2026-08-03T06:30:00Z").ToUniversalTime());
        DailyMissionCycle.getNextResetUtc(MissionResetType.WEEK, now, Utc)
            .Should().Be(DateTime.Parse("2026-08-03T06:30:00Z").ToUniversalTime());
        DailyMissionCycle.getNextResetUtc(MissionResetType.MONTH, now, Utc)
            .Should().Be(DateTime.Parse("2026-08-01T06:30:00Z").ToUniversalTime().AddMonths(1));
    }
}
