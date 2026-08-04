using L2Dn.Model.Enums;

namespace L2Dn.GameServer.Model.DailyMissions;

public static class DailyMissionCycle
{
    public const int ResetHour = 6;
    public const int ResetMinute = 30;

    public static DateTime getCycleStartUtc(MissionResetType resetType, DateTimeOffset now)
    {
        return getCycleStartUtc(resetType, now, TimeZoneInfo.Local);
    }

    public static DateTime getCycleStartUtc(MissionResetType resetType, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        DateTime localNow = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
        DateTime localStart = getLocalCycleStart(resetType, localNow);
        return toUtc(localStart, timeZone);
    }

    public static DateTime getNextResetUtc(MissionResetType resetType, DateTimeOffset now)
    {
        return getNextResetUtc(resetType, now, TimeZoneInfo.Local);
    }

    public static DateTime getNextResetUtc(MissionResetType resetType, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        DateTime localNow = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
        DateTime localStart = getLocalCycleStart(resetType, localNow);
        DateTime nextLocalReset = resetType switch
        {
            MissionResetType.DAY => localStart.AddDays(1),
            MissionResetType.WEEK => localStart.AddDays(7),
            MissionResetType.MONTH => localStart.AddMonths(1),
            MissionResetType.WEEKEND => localStart.AddDays(7),
            _ => throw new ArgumentOutOfRangeException(nameof(resetType), resetType, null),
        };

        return toUtc(nextLocalReset, timeZone);
    }

    public static int getRemainingSeconds(MissionResetType resetType, DateTimeOffset now)
    {
        DateTime nextReset = getNextResetUtc(resetType, now);
        double seconds = (nextReset - now.UtcDateTime).TotalSeconds;
        return (int)Math.Clamp(Math.Ceiling(seconds), 0, int.MaxValue);
    }

    public static bool isWeekendWindow(DateTimeOffset now)
    {
        return isWeekendWindow(now, TimeZoneInfo.Local);
    }

    public static bool isWeekendWindow(DateTimeOffset now, TimeZoneInfo timeZone)
    {
        DateTime localNow = TimeZoneInfo.ConvertTime(now, timeZone).DateTime;
        DateTime start = getLocalCycleStart(MissionResetType.WEEKEND, localNow);
        return localNow >= start && localNow < start.AddDays(2);
    }

    private static DateTime getLocalCycleStart(MissionResetType resetType, DateTime localNow)
    {
        DateTime resetToday = new(localNow.Year, localNow.Month, localNow.Day, ResetHour, ResetMinute, 0,
            DateTimeKind.Unspecified);

        return resetType switch
        {
            MissionResetType.DAY => localNow < resetToday ? resetToday.AddDays(-1) : resetToday,
            MissionResetType.WEEK => getWeekStart(localNow, DayOfWeek.Monday),
            MissionResetType.MONTH => getMonthStart(localNow),
            MissionResetType.WEEKEND => getWeekStart(localNow, DayOfWeek.Saturday),
            _ => throw new ArgumentOutOfRangeException(nameof(resetType), resetType, null),
        };
    }

    private static DateTime getWeekStart(DateTime localNow, DayOfWeek firstDay)
    {
        int elapsedDays = ((int)localNow.DayOfWeek - (int)firstDay + 7) % 7;
        DateTime start = new DateTime(localNow.Year, localNow.Month, localNow.Day, ResetHour, ResetMinute, 0,
            DateTimeKind.Unspecified).AddDays(-elapsedDays);
        return localNow < start ? start.AddDays(-7) : start;
    }

    private static DateTime getMonthStart(DateTime localNow)
    {
        DateTime start = new(localNow.Year, localNow.Month, 1, ResetHour, ResetMinute, 0,
            DateTimeKind.Unspecified);
        return localNow < start ? start.AddMonths(-1) : start;
    }

    private static DateTime toUtc(DateTime localTime, TimeZoneInfo timeZone)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), timeZone);
    }
}
