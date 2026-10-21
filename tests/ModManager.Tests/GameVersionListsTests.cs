using System;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A mod's author lists, in game-versions.txt in the mod's repository, the game versions each version of the mod is
/// known to work on, so a mod that still works after a game update stops being flagged without a new release.
/// </summary>
public class GameVersionListsTests
{
    private const string List = "# Camera\n0.4.0 = 1.1.18902, 1.1.19131\nv0.3.2=1.1.18902\n";

    [Fact]
    public void The_list_is_read_from_the_default_branch_without_an_account()
    {
        Assert.Equal("https://raw.githubusercontent.com/Ann/Mod/HEAD/game-versions.txt", GameVersionLists.Address("Ann/Mod").AbsoluteUri);
    }

    [Fact]
    public void Each_mod_version_gets_only_its_own_game_versions()
    {
        var list = GameVersionLists.Parse(List).List;

        Assert.Equal(new[] { "1.1.18902", "1.1.19131" }, list.For("0.4.0"));
        Assert.Equal(new[] { "1.1.18902" }, list.For("0.3.2"));
        Assert.Empty(list.For("0.4.1"));
    }

    [Fact]
    public void A_version_written_shorter_or_with_a_v_is_the_same_version()
    {
        var list = GameVersionLists.Parse("v0.4 = 1.1.19131").List;

        Assert.Equal(new[] { "1.1.19131" }, list.For("0.4.0"));
    }

    [Fact]
    public void A_bad_line_is_reported_and_the_good_ones_are_kept()
    {
        var read = GameVersionLists.Parse("0.4.0 = 1.1.19131\r\n0.3.0 1.1.18902\r\nlatest = 1.1\r\n0.2.0 = soon\r\n");

        Assert.Equal(new[] { "1.1.19131" }, read.List.For("0.4.0"));
        Assert.Empty(read.List.For("0.3.0"));
        Assert.Equal(3, read.Ignored.Count);
        Assert.Contains("line 2", read.Ignored[0]);
    }

    [Fact]
    public void A_missing_file_means_the_repository_lists_nothing()
    {
        Assert.IsType<GameVersionListAnswer.Missing>(GameVersionLists.From(new HttpAnswer.Answered(404, null, "404: Not Found")));
    }

    [Fact]
    public void A_failed_request_is_not_mistaken_for_an_empty_list()
    {
        Assert.IsType<GameVersionListAnswer.Failed>(GameVersionLists.From(new HttpAnswer.Answered(500, null, "")));
        Assert.IsType<GameVersionListAnswer.Failed>(GameVersionLists.From(new HttpAnswer.Unreachable("offline")));
    }

    [Fact]
    public void A_found_file_keeps_its_text_to_be_saved()
    {
        var found = Assert.IsType<GameVersionListAnswer.Found>(GameVersionLists.From(new HttpAnswer.Answered(200, null, List)));

        Assert.Equal(List, found.Text);
        Assert.Equal(new[] { "1.1.18902", "1.1.19131" }, found.Read.List.For("0.4.0"));
    }

    [Fact]
    public void A_game_version_the_author_listed_counts_as_known_to_work()
    {
        Assert.True(GameVersion.IsKnownToWork(new[] { "1.1.18902" }, new[] { "1.1.19131" }, "1.1.19131"));
    }

    [Fact]
    public void A_newer_game_listed_nowhere_is_not_known_to_work()
    {
        Assert.False(GameVersion.IsKnownToWork(new[] { "1.1.18902" }, Array.Empty<string>(), "1.1.19131"));
        Assert.False(GameVersion.IsKnownToWork(new[] { "1.1.18902" }, new[] { "1.1.18950" }, "1.1.19131"));
    }

    [Fact]
    public void A_list_alone_is_enough_for_a_mod_that_names_no_version_in_its_dll()
    {
        Assert.False(GameVersion.IsKnownToWork(Array.Empty<string>(), new[] { "1.1.18902" }, "1.1.19131"));
        Assert.True(GameVersion.IsKnownToWork(Array.Empty<string>(), Array.Empty<string>(), "1.1.19131"));
    }

    [Fact]
    public void The_book_answers_for_the_repository_and_mod_version_it_was_given()
    {
        var book = new UpdateBook();

        book.Record("Ann/Mod", GameVersionLists.Parse(List).List);

        Assert.Equal(new[] { "1.1.18902", "1.1.19131" }, book.GameVersionsListed("ann/mod", "0.4.0"));
        Assert.Empty(book.GameVersionsListed("Ann/Other", "0.4.0"));
        Assert.Empty(book.GameVersionsListed(null, "0.4.0"));
    }

    [Fact]
    public void The_lists_survive_update_checks_being_turned_off()
    {
        var book = new UpdateBook();

        book.Record("Ann/Mod", GameVersionLists.Parse(List).List);
        book.Clear();

        Assert.NotEmpty(book.GameVersionsListed("Ann/Mod", "0.4.0"));
    }
}
