using FluentAssertions;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Model.Olympiads;

namespace L2Dn.GameServer.Model.Tests;

public class OlympiadDateTimeTests
{
    [Fact]
    public void ToUtc_local_converts_to_utc()
    {
        DateTime local = new(2026, 5, 17, 22, 27, 40, DateTimeKind.Local);
        DateTime utc = GameServerDbContext.ToUtc(local);
        utc.Kind.Should().Be(DateTimeKind.Utc);
        utc.Should().Be(local.ToUniversalTime());
        Olympiad.ToUtc(local).Should().Be(utc);
    }

    [Fact]
    public void ToUtc_utc_is_unchanged()
    {
        DateTime value = new(2026, 5, 17, 20, 27, 40, DateTimeKind.Utc);
        GameServerDbContext.ToUtc(value).Should().Be(value);
    }

    [Fact]
    public void ToUtc_unspecified_is_treated_as_utc()
    {
        DateTime value = new(2026, 5, 17, 20, 27, 40, DateTimeKind.Unspecified);
        DateTime utc = GameServerDbContext.ToUtc(value);
        utc.Kind.Should().Be(DateTimeKind.Utc);
        utc.Should().Be(value);
    }
}
