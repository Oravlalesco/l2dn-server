using FluentAssertions;
using L2Dn.GameServer.Data;

namespace L2Dn.GameServer.Model.Tests;

public class SchemeBufferTests
{
    [Theory]
    [InlineData("editschemes;Buffs;pvp;1", "editschemes", 4)]
    [InlineData("editschemes;Buffs;;1", "editschemes", 4)]
    [InlineData("skillselect;Buffs;pvp;1035;1", "skillselect", 5)]
    [InlineData("createscheme pvp", "createscheme", 2)]
    public void TryParse_splits_tokens_preserving_empty(string command, string expectedCommand, int tokenCount)
    {
        SchemeBufferBypass.TryParse(command, out SchemeBufferBypass bypass).Should().BeTrue();
        bypass.Command.Should().Be(expectedCommand);
        bypass.Tokens.Count.Should().Be(tokenCount);
    }

    [Fact]
    public void TryParse_editschemes_empty_scheme_name_token()
    {
        SchemeBufferBypass.TryParse("editschemes;Buffs;;1", out SchemeBufferBypass bypass).Should().BeTrue();
        bypass.TokenAt(2).Should().BeEmpty();
        bypass.TokenAt(3).Should().Be("1");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("pvp", true)]
    [InlineData("my pvp", true)]
    [InlineData("a;b", false)]
    public void IsValidFormat_rejects_invalid_names(string name, bool expected)
    {
        SchemeBufferSchemeNames.IsValidFormat(name, out _).Should().Be(expected);
    }

    [Fact]
    public void Normalize_returns_null_for_whitespace()
    {
        SchemeBufferSchemeNames.Normalize("   ").Should().BeNull();
        SchemeBufferSchemeNames.Normalize("pvp").Should().Be("pvp");
    }
}
