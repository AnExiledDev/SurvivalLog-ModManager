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
        Assert.Equal(ModInfo.None, read.Info with { Links = ModInfo.None.Links });
        Assert.Empty(read.Info.Links);
        Assert.True(read.Info.ChecksForUpdates);
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

    [Fact]
    public void The_newer_keys_become_info()
    {
        var read = Read(
            ("ModManager.Name", "Better Map\n\u66f4\u597d\u7684\u5730\u56fe"),
            ("ModManager.Icon", "BetterMap.icon.png"),
            ("ModManager.Category", "quality of life"),
            ("ModManager.Library", "TRUE"),
            ("ModManager.GameVersion", "1.1.18902, 1.2"),
            ("ModManager.Issues", "https://github.com/Ann/Map/issues"),
            ("ModManager.CheckForUpdates", "false"),
            ("ModManager.Settings", "live"));

        Assert.Empty(read.Ignored);
        Assert.Equal(new Description("Better Map", "\u66f4\u597d\u7684\u5730\u56fe"), read.Info.Name);
        Assert.Equal("BetterMap.icon.png", read.Info.Icon);
        Assert.Equal(ModCategory.QualityOfLife, read.Info.Category);
        Assert.True(read.Info.IsLibrary);
        Assert.Equal(new[] { "1.1.18902", "1.2" }, read.Info.GameVersions);
        Assert.Equal("https://github.com/Ann/Map/issues", read.Info.Link(LinkKind.Issues)?.Url);
        Assert.False(read.Info.ChecksForUpdates);
        Assert.Equal(ApplyMode.Live, read.Info.SettingsApply);
    }

    [Theory]
    [InlineData("ModManager.Category", "Weapons")]
    [InlineData("ModManager.Library", "yes")]
    [InlineData("ModManager.CheckForUpdates", "0")]
    [InlineData("ModManager.Settings", "sometimes")]
    [InlineData("ModManager.GameVersion", "latest")]
    [InlineData("ModManager.GameVersion", "1.1, beta")]
    [InlineData("ModManager.Issues", "javascript:alert(1)")]
    public void A_value_that_does_not_fit_its_key_is_reported_and_dropped(string key, string value)
    {
        var read = Read((key, value));

        Assert.Single(read.Ignored);
        Assert.StartsWith(key + " must be", read.Ignored[0]);
        Assert.Equal(ModInfo.None, read.Info with { Links = ModInfo.None.Links });
        Assert.Empty(read.Info.Links);
    }

    [Fact]
    public void A_mod_that_opts_out_of_update_checks_keeps_its_GitHub_link_but_is_not_checked()
    {
        var info = Read(("ModManager.GitHub", "Ann/Map"), ("ModManager.CheckForUpdates", "false")).Info;

        Assert.Equal("Ann/Map", info.GitHubRepository);
        Assert.Null(info.UpdateRepository);
    }

    [Theory]
    [InlineData("https://github.com/Ann/Map")]
    [InlineData("https://github.com/Ann/Map.git")]
    [InlineData("https://www.github.com/Ann/Map/")]
    public void The_SDKs_repository_url_stands_in_for_a_missing_GitHub_key(string url)
    {
        var read = Read(("RepositoryUrl", url));

        Assert.Empty(read.Ignored);
        Assert.Equal("Ann/Map", read.Info.GitHubRepository);
    }

    [Theory]
    [InlineData("https://gitlab.com/Ann/Map")]
    [InlineData("http://github.com/Ann/Map")]
    [InlineData("https://github.com/Ann")]
    [InlineData("https://github.com/Ann/Map/tree/main")]
    [InlineData("https://github.com.evil.example/Ann/Map")]
    public void A_repository_url_that_is_not_a_GitHub_repository_is_passed_over_quietly(string url)
    {
        var read = Read(("RepositoryUrl", url));

        Assert.Empty(read.Ignored);
        Assert.Null(read.Info.GitHubRepository);
    }

    [Fact]
    public void The_mods_own_GitHub_key_wins_over_the_repository_url()
    {
        var info = Read(("ModManager.GitHub", "Ann/Map"), ("RepositoryUrl", "https://github.com/Fork/Other")).Info;

        Assert.Equal("Ann/Map", info.GitHubRepository);
        Assert.Single(info.Links);
    }
}

public class GameVersionTests
{
    [Fact]
    public void The_version_file_gives_version_and_build()
    {
        Assert.Equal("1.1.18902", GameVersion.FromVersionFile("1.1\r\n18902\r\nstaging_1.1\r\nRelease\r\n"));
    }

    [Theory]
    [InlineData("1.1", "1.1")]
    [InlineData("1.1\nRelease", "1.1")]
    [InlineData("staging\n18902", null)]
    [InlineData("", null)]
    public void A_file_without_a_build_line_gives_what_it_can(string text, string? expected)
    {
        Assert.Equal(expected, GameVersion.FromVersionFile(text));
    }

    [Theory]
    [InlineData("1.1.18902", true)]
    [InlineData("1.1", true)]
    [InlineData("1.2", false)]
    [InlineData("1.1.1", false)]
    public void A_mod_is_made_for_its_version_or_any_build_under_it(string madeFor, bool expected)
    {
        Assert.Equal(expected, GameVersion.IsMadeFor(new[] { madeFor }, "1.1.18902"));
    }

    [Fact]
    public void A_build_number_that_only_starts_the_same_is_another_version()
    {
        Assert.False(GameVersion.IsMadeFor(new[] { "1.1.1890" }, "1.1.18902"));
    }

    [Fact]
    public void A_mod_that_names_no_version_or_an_unknown_game_is_never_flagged()
    {
        Assert.True(GameVersion.IsMadeFor(System.Array.Empty<string>(), "1.1.18902"));
        Assert.True(GameVersion.IsMadeFor(new[] { "0.9" }, null));
    }
}
