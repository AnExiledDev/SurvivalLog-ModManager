using System.Collections.Generic;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ModInfoReaderTests
{
    private static ModInfoRead Read(params (string Key, string? Value)[] pairs) =>
        ModInfoReader.Read(pairs.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)));

    [Fact]
    public void Every_known_key_becomes_info_with_full_addresses()
    {
        var read = Read(
            ("ModManager.Author", " Ann "),
            ("ModManager.Description", "Does a thing.\n\u505a\u4e00\u4ef6\u4e8b\u3002"),
            ("ModManager.NexusId", "0047"),
            ("ModManager.GitHub", "AnExiledDev/SurvivalLog-Thing"),
            ("ModManager.Website", "https://example.com/mods?id=1"));

        Assert.Empty(read.Ignored);
        Assert.Equal("Ann", read.Info.Author);
        Assert.Equal(new Description("Does a thing.", "\u505a\u4e00\u4ef6\u4e8b\u3002"), read.Info.Description);
        Assert.Equal("https://www.nexusmods.com/survivallog/mods/47", read.Info.Link(LinkKind.Nexus)?.Url);
        Assert.Equal("https://github.com/AnExiledDev/SurvivalLog-Thing", read.Info.Link(LinkKind.GitHub)?.Url);
        Assert.Equal("https://example.com/mods?id=1", read.Info.Link(LinkKind.Website)?.Url);
    }

    [Fact]
    public void A_mod_that_sets_nothing_has_no_info_and_nothing_to_report()
    {
        var read = Read(("RepositoryUrl", "https://x.example"), ("SomeoneElse.Key", "v"));

        Assert.Empty(read.Ignored);
        Assert.Null(read.Info.Author);
        Assert.Null(read.Info.Description);
        Assert.Empty(read.Info.Links);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/x")]
    [InlineData("https://user:pass@example.com/")]
    [InlineData("example.com")]
    [InlineData("C:\\Games\\run.bat")]
    public void A_website_that_is_not_a_plain_web_address_is_refused(string value)
    {
        var read = Read(("ModManager.Website", value));

        Assert.Null(read.Info.Link(LinkKind.Website));
        Assert.Contains(read.Ignored, message => message.StartsWith("ModManager.Website must be"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-4")]
    [InlineData("1234567890")]
    [InlineData("4/../../evil")]
    public void A_nexus_id_must_be_a_real_mod_number(string value)
    {
        var read = Read(("ModManager.NexusId", value));

        Assert.Null(read.Info.Link(LinkKind.Nexus));
        Assert.Single(read.Ignored);
    }

    [Theory]
    [InlineData("just-a-name")]
    [InlineData("a/b/c")]
    [InlineData("../evil")]
    [InlineData("owner/re po")]
    [InlineData("owner/repo?x=1")]
    public void A_github_value_must_be_owner_slash_repository(string value)
    {
        var read = Read(("ModManager.GitHub", value));

        Assert.Null(read.Info.Link(LinkKind.GitHub));
        Assert.Single(read.Ignored);
    }

    [Fact]
    public void Text_past_its_cap_is_dropped_and_reported()
    {
        var read = Read(("ModManager.Author", new string('a', 81)), ("ModManager.Description", new string('d', 1001)));

        Assert.Null(read.Info.Author);
        Assert.Null(read.Info.Description);
        Assert.Equal(2, read.Ignored.Count);
        Assert.NotNull(Read(("ModManager.Author", new string('a', 80))).Info.Author);
    }

    [Fact]
    public void The_first_value_of_a_key_counts()
    {
        var read = Read(("ModManager.Author", "First"), ("ModManager.Author", "Second"));

        Assert.Equal("First", read.Info.Author);
    }

    [Fact]
    public void A_misspelt_key_is_reported_so_the_author_can_find_it()
    {
        var read = Read(("ModManager.Nexus", "47"));

        Assert.Empty(read.Info.Links);
        Assert.Equal(new[] { "ModManager.Nexus is not a key the Mod Manager knows" }, read.Ignored);
    }
}
