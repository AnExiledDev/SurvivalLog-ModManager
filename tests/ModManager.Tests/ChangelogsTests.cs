using System;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A mod's CHANGELOG tab shows the changelog its link names, fetched as plain text without an account, and says why
/// when it cannot.
/// </summary>
public class ChangelogsTests
{
    [Fact]
    public void A_GitHub_file_page_is_fetched_as_raw_text()
    {
        Assert.Equal("https://raw.githubusercontent.com/Ann/Mod/main/docs/CHANGELOG.md", Changelogs.TextUri("https://github.com/Ann/Mod/blob/main/docs/CHANGELOG.md")?.AbsoluteUri);
    }

    [Fact]
    public void Another_https_text_file_is_fetched_as_it_is()
    {
        Assert.Equal("https://example.com/mod/changes.txt", Changelogs.TextUri("https://example.com/mod/changes.txt")?.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a link")]
    [InlineData("http://example.com/CHANGELOG.md")]
    [InlineData("https://user:pass@example.com/CHANGELOG.md")]
    [InlineData("https://example.com/changes")]
    [InlineData("https://github.com/Ann/Mod/releases")]
    [InlineData("https://github.com/Ann/Mod/tree/main/CHANGELOG.md")]
    [InlineData("https://github.com/Ann/Mod/blob/main/Plugin.cs")]
    [InlineData("https://www.nexusmods.com/survivallog/mods/1?tab=logs")]
    public void A_link_the_tab_cannot_show_as_text_gives_no_tab(string? link)
    {
        Assert.Null(Changelogs.TextUri(link));
    }

    [Fact]
    public void A_found_changelog_is_its_text_with_plain_line_ends()
    {
        Assert.Equal("## 1.0.0\n- First.", Assert.IsType<ChangelogState.Ready>(Changelogs.From(new HttpAnswer.Answered(200, null, "## 1.0.0\r\n- First."))).Text);
    }

    [Fact]
    public void A_very_long_answer_is_cut_short()
    {
        var text = Assert.IsType<ChangelogState.Ready>(Changelogs.From(new HttpAnswer.Answered(200, null, new string('a', Changelogs.MaxLength + 10)))).Text;

        Assert.Equal(Changelogs.MaxLength + 4, text.Length);
        Assert.EndsWith("\n...", text);
    }

    [Fact]
    public void A_missing_changelog_an_error_and_no_answer_each_say_why()
    {
        Assert.Equal("the changelog is not at that address", Assert.IsType<ChangelogState.Failed>(Changelogs.From(new HttpAnswer.Answered(404, null, ""))).Reason);
        Assert.Equal("the server answered 500", Assert.IsType<ChangelogState.Failed>(Changelogs.From(new HttpAnswer.Answered(500, null, "oops"))).Reason);
        Assert.Equal("the server answered 301", Assert.IsType<ChangelogState.Failed>(Changelogs.From(new HttpAnswer.Answered(301, "https://elsewhere", ""))).Reason);
        Assert.Equal("offline", Assert.IsType<ChangelogState.Failed>(Changelogs.From(new HttpAnswer.Unreachable("offline"))).Reason);
    }

    [Fact]
    public void The_book_keeps_a_changelog_for_the_session_even_when_update_checks_restart()
    {
        var book = new UpdateBook();
        var changelog = new Uri("https://raw.githubusercontent.com/Ann/Mod/main/CHANGELOG.md");

        Assert.IsType<ChangelogState.None>(book.ChangelogOf(null));
        Assert.IsType<ChangelogState.NotAsked>(book.ChangelogOf(changelog));

        book.Record(changelog, new ChangelogState.Ready("text"));
        book.Clear();

        Assert.Equal(new ChangelogState.Ready("text"), book.ChangelogOf(changelog));
    }
}
