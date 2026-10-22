using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v0.2.2", "0.2.0")]
    [InlineData("ExiledToolkit-0.3.0", "0.2.9")]
    [InlineData("1.10", "1.9")]
    [InlineData("1.0.0", "1.0.0-beta")]
    [InlineData("1.0.0-beta.10", "1.0.0-beta.2")]
    [InlineData("1.0.0-beta", "1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha")]
    [InlineData("2.0.0-rc.1", "1.9.9")]
    public void The_first_is_newer(string newer, string older)
    {
        Assert.True(Version(newer).CompareTo(Version(older)) > 0);
        Assert.True(Version(older).CompareTo(Version(newer)) < 0);
    }

    [Theory]
    [InlineData("1.2", "1.2.0")]
    [InlineData("v1.2.0", "1.2.0.0")]
    [InlineData("1.0+build5", "1.0")]
    public void These_are_the_same_version(string one, string other)
    {
        Assert.Equal(0, Version(one).CompareTo(Version(other)));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1..2")]
    [InlineData("1.0-")]
    [InlineData("1.0-beta..1")]
    [InlineData("99999999999")]
    public void A_tag_without_a_usable_version_is_unreadable(string text)
    {
        Assert.Null(ReleaseVersion.Parse(text));
    }

    private static ReleaseVersion Version(string text) => ReleaseVersion.Parse(text) ?? throw new InvalidOperationException(text + " did not parse");
}

public class LatestReleaseFinderTests
{
    private static Func<Uri, Task<HttpAnswer>> GitHub(params (string Url, HttpAnswer Answer)[] answers)
    {
        var byUrl = answers.ToDictionary(pair => pair.Url, pair => pair.Answer);

        return uri => Task.FromResult(byUrl.TryGetValue(uri.AbsoluteUri, out var answer) ? answer : new HttpAnswer.Answered(500, null, ""));
    }

    private static HttpAnswer Redirect(string location) => new HttpAnswer.Answered(302, location, "");

    [Fact]
    public void The_latest_release_is_where_its_page_redirects_to()
    {
        var head = GitHub(("https://github.com/Ann/Mod/releases/latest", Redirect("https://github.com/Ann/Mod/releases/tag/v1.2.0")));

        var found = Assert.IsType<LatestRelease.Found>(LatestReleaseFinder.Find("Ann/Mod", head).Result);

        Assert.Equal("v1.2.0", found.Tag);
        Assert.Equal("https://github.com/Ann/Mod/releases/tag/v1.2.0", found.PageUrl);
        Assert.Equal(("Ann", "Mod"), (found.Owner, found.Repository));
    }

    [Fact]
    public void A_renamed_repository_is_followed_to_its_new_name()
    {
        var head = GitHub(
            ("https://github.com/Ann/Old/releases/latest", new HttpAnswer.Answered(301, "https://github.com/Ann/New/releases/latest", "")),
            ("https://github.com/Ann/New/releases/latest", Redirect("/Ann/New/releases/tag/2.0")));

        var found = Assert.IsType<LatestRelease.Found>(LatestReleaseFinder.Find("Ann/Old", head).Result);

        Assert.Equal("New", found.Repository);
        Assert.Equal("https://github.com/Ann/New/releases/tag/2.0", found.PageUrl);
    }

    [Fact]
    public void A_repository_without_releases_redirects_to_the_release_list()
    {
        var head = GitHub(("https://github.com/Ann/Mod/releases/latest", Redirect("https://github.com/Ann/Mod/releases")));

        Assert.IsType<LatestRelease.NoReleases>(LatestReleaseFinder.Find("Ann/Mod", head).Result);
    }

    [Theory]
    [InlineData("https://evil.example/Ann/Mod/releases/tag/v1")]
    [InlineData("http://github.com/Ann/Mod/releases/tag/v1")]
    [InlineData("https://github.com.evil.example/Ann/Mod/releases/tag/v1")]
    [InlineData("https://user@github.com/Ann/Mod/releases/tag/v1")]
    [InlineData("https://github.com:8443/Ann/Mod/releases/tag/v1")]
    [InlineData("https://github.com/login")]
    [InlineData("https://github.com/Ann/Mod/releases/tag/")]
    [InlineData("")]
    public void A_redirect_anywhere_but_a_github_release_page_is_not_trusted(string location)
    {
        var head = GitHub(("https://github.com/Ann/Mod/releases/latest", Redirect(location)));

        Assert.IsType<LatestRelease.Failed>(LatestReleaseFinder.Find("Ann/Mod", head).Result);
    }

    [Fact]
    public void A_missing_repository_or_no_connection_fails_with_the_reason()
    {
        var missing = GitHub(("https://github.com/Ann/Gone/releases/latest", new HttpAnswer.Answered(404, null, "")));

        Assert.Contains("Ann/Gone", Assert.IsType<LatestRelease.Failed>(LatestReleaseFinder.Find("Ann/Gone", missing).Result).Reason);
        Assert.Equal("offline", Assert.IsType<LatestRelease.Failed>(LatestReleaseFinder.Find("Ann/Mod", _ => Task.FromResult<HttpAnswer>(new HttpAnswer.Unreachable("offline"))).Result).Reason);
    }

    [Fact]
    public void Redirects_that_go_round_in_circles_stop()
    {
        var asked = 0;

        Task<HttpAnswer> Loop(Uri uri)
        {
            asked++;

            return Task.FromResult<HttpAnswer>(Redirect(uri.AbsoluteUri));
        }

        Assert.IsType<LatestRelease.Failed>(LatestReleaseFinder.Find("Ann/Mod", Loop).Result);
        Assert.True(asked < 10);
    }
}

public class ReleaseNotesTests
{
    private static readonly LatestRelease.Found Release = new("Ann", "Mod", "v1.0 beta", "https://github.com/Ann/Mod/releases/tag/v1.0%20beta");

    private static string ReleaseJson(string tag, string? body, bool isDraft = false, bool isPrerelease = false) =>
        "{\"tag_name\":\"" + tag + "\",\"draft\":" + (isDraft ? "true" : "false") + ",\"prerelease\":" + (isPrerelease ? "true" : "false") + ",\"body\":" + (body == null ? "null" : "\"" + body + "\"") + "}";

    private static NotesState Notes(string installed, string latest, params string[] releases) =>
        ReleaseNotes.From(new HttpAnswer.Answered(200, null, "[" + string.Join(",", releases) + "]"), installed, latest);

    [Fact]
    public void The_notes_are_every_release_since_the_installed_one_newest_first()
    {
        var notes = Notes("1.0.0", "v1.3.0", ReleaseJson("v1.3.0", "Faster saves."), ReleaseJson("v1.0.0", "First."), ReleaseJson("v1.2.0", "Fixed a crash.\\r\\nNew map."), ReleaseJson("v0.9.0", "Old."));

        Assert.Equal(new[] { new ReleaseNote("v1.3.0", "Faster saves."), new ReleaseNote("v1.2.0", "Fixed a crash.\nNew map.") }, Assert.IsType<NotesState.Ready>(notes).Releases);
    }

    [Fact]
    public void Drafts_prereleases_releases_after_the_latest_and_empty_notes_are_left_out()
    {
        var notes = Notes("1.0.0", "v1.3.0", ReleaseJson("v1.4.0", "Not out yet."), ReleaseJson("v1.3.5", "Draft.", isDraft: true), ReleaseJson("v1.3.0", "Out."), ReleaseJson("v1.2.0", "Beta.", isPrerelease: true), ReleaseJson("v1.1.0", "  "), ReleaseJson("v1.0.5", null), ReleaseJson("nightly", "No version."));

        Assert.Equal(new[] { new ReleaseNote("v1.3.0", "Out.") }, Assert.IsType<NotesState.Ready>(notes).Releases);
    }

    [Fact]
    public void Long_notes_are_cut_short_and_a_long_history_keeps_the_newest_releases()
    {
        var releases = Enumerable.Range(1, ReleaseNotes.MaxReleases + 5).Select(minor => ReleaseJson("v1." + minor + ".0", new string('a', ReleaseNotes.MaxLength + 500))).ToArray();
        var notes = Assert.IsType<NotesState.Ready>(Notes("1.0.0", "v1.99.0", releases)).Releases;

        Assert.Equal(ReleaseNotes.MaxReleases, notes.Count);
        Assert.Equal("v1." + (ReleaseNotes.MaxReleases + 5) + ".0", notes[0].Tag);
        Assert.True(notes[0].Text.Length <= ReleaseNotes.MaxLength + 4);
        Assert.EndsWith("...", notes[0].Text);
    }

    [Fact]
    public void Releases_without_notes_say_so()
    {
        Assert.IsType<NotesState.Empty>(Notes("1.0.0", "v1.1.0", ReleaseJson("v1.1.0", " ")));
        Assert.IsType<NotesState.Empty>(Notes("1.0.0", "v1.1.0"));
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    public void GitHubs_limit_for_requests_without_an_account_reads_as_busy(int status)
    {
        Assert.IsType<NotesState.Busy>(ReleaseNotes.From(new HttpAnswer.Answered(status, null, ""), "1.0", "v1.1"));
    }

    [Fact]
    public void Broken_answers_fail_instead_of_throwing()
    {
        Assert.IsType<NotesState.Failed>(ReleaseNotes.From(new HttpAnswer.Answered(200, null, "<html>"), "1.0", "v1.1"));
        Assert.IsType<NotesState.Failed>(ReleaseNotes.From(new HttpAnswer.Answered(200, null, "{\"body\":\"one release\"}"), "1.0", "v1.1"));
        Assert.IsType<NotesState.Failed>(ReleaseNotes.From(new HttpAnswer.Answered(500, null, ""), "1.0", "v1.1"));
        Assert.IsType<NotesState.Failed>(ReleaseNotes.From(new HttpAnswer.Unreachable("offline"), "1.0", "v1.1"));
    }

    [Fact]
    public void The_notes_come_from_the_list_of_recent_releases()
    {
        Assert.Equal("https://api.github.com/repos/Ann/Mod/releases?per_page=30", ReleaseNotes.ListUri(Release).AbsoluteUri);
    }
}

public class UpdateBookTests
{
    private static readonly LatestRelease.Found Latest = new("Ann", "Mod", "v1.2.0", "https://github.com/Ann/Mod/releases/tag/v1.2.0");

    private static UpdateBook Book(LatestRelease latest)
    {
        var book = new UpdateBook();

        book.Record("Ann/Mod", latest);

        return book;
    }

    [Fact]
    public void An_older_installed_version_has_an_update_and_its_page()
    {
        var book = Book(Latest);

        Assert.Equal("v1.2.0", Assert.IsType<UpdateState.Available>(book.StateOf("Ann/Mod", "1.1.0")).Tag);
        Assert.Equal(Latest.PageUrl, book.UpdateFor("Ann/Mod", "1.1.0")?.PageUrl);
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.3.0")]
    public void The_same_or_a_newer_installed_version_is_up_to_date_and_offers_nothing(string installed)
    {
        var book = Book(Latest);

        Assert.IsType<UpdateState.UpToDate>(book.StateOf("Ann/Mod", installed));
        Assert.Null(book.UpdateFor("Ann/Mod", installed));
    }

    [Fact]
    public void Repository_names_match_ignoring_case_as_on_GitHub()
    {
        Assert.IsType<UpdateState.Available>(Book(Latest).StateOf("ann/mod", "1.0"));
    }

    [Fact]
    public void A_mod_without_a_repository_or_never_checked_is_not_checked()
    {
        var book = Book(Latest);

        Assert.IsType<UpdateState.NotChecked>(book.StateOf(null, "1.0"));
        Assert.IsType<UpdateState.NotChecked>(book.StateOf("Ann/Other", "1.0"));
    }

    [Fact]
    public void A_tag_that_is_not_a_version_cannot_be_compared()
    {
        var book = Book(Latest with { Tag = "nightly" });

        Assert.Equal("nightly", Assert.IsType<UpdateState.Unreadable>(book.StateOf("Ann/Mod", "1.0")).Tag);
        Assert.Null(book.UpdateFor("Ann/Mod", "1.0"));
    }

    [Fact]
    public void Checking_no_releases_and_failures_carry_through()
    {
        Assert.IsType<UpdateState.Checking>(Book(new LatestRelease.Checking()).StateOf("Ann/Mod", "1.0"));
        Assert.IsType<UpdateState.NoReleases>(Book(new LatestRelease.NoReleases()).StateOf("Ann/Mod", "1.0"));
        Assert.Equal("offline", Assert.IsType<UpdateState.Failed>(Book(new LatestRelease.Failed("offline")).StateOf("Ann/Mod", "1.0")).Reason);
    }

    [Fact]
    public void Notes_belong_to_one_release_and_installed_version_and_clearing_forgets_everything()
    {
        var book = Book(Latest);
        var ready = new NotesState.Ready(new[] { new ReleaseNote("v1.2.0", "Fixed things.") });

        Assert.IsType<NotesState.NotAsked>(Assert.IsType<UpdateState.Available>(book.StateOf("Ann/Mod", "1.0")).Notes);

        book.Record(Latest, "1.0", ready);

        Assert.Equal(ready, Assert.IsType<UpdateState.Available>(book.StateOf("Ann/Mod", "1.0")).Notes);
        Assert.IsType<NotesState.NotAsked>(Assert.IsType<UpdateState.Available>(book.StateOf("Ann/Mod", "1.1")).Notes);
        Assert.IsType<NotesState.NotAsked>(book.Notes(Latest with { Tag = "v1.3.0" }, "1.0"));

        book.Clear();

        Assert.IsType<UpdateState.NotChecked>(book.StateOf("Ann/Mod", "1.0"));
        Assert.IsType<NotesState.NotAsked>(book.Notes(Latest, "1.0"));
    }

    [Fact]
    public void The_book_is_settled_only_once_every_started_check_has_an_answer()
    {
        var book = new UpdateBook();

        Assert.False(book.IsSettled);

        book.Record("Ann/Mod", new LatestRelease.Checking());
        book.Record("Ann/Other", Latest);

        Assert.False(book.IsSettled);

        book.Record("Ann/Mod", new LatestRelease.Failed("offline"));

        Assert.True(book.IsSettled);
    }

    [Fact]
    public void Installs_are_remembered_per_mod_and_survive_a_new_round_of_checks()
    {
        var book = Book(Latest);

        book.Record("com.ann.mod", new InstallState.Installed("v1.2.0"));
        book.Clear();

        Assert.Equal(new InstallState.Installed("v1.2.0"), book.InstallOf("com.ann.mod"));
        Assert.IsType<InstallState.None>(book.InstallOf("com.other"));
    }

    [Fact]
    public void The_repository_comes_from_the_mods_GitHub_info()
    {
        var info = ModInfoReader.Read(new[] { new KeyValuePair<string, string?>(ModInfoReader.GitHubKey, "Ann/Mod") }, System.Array.Empty<string>()).Info;

        Assert.Equal("Ann/Mod", info.GitHubRepository);
        Assert.Null(ModInfo.None.GitHubRepository);
    }
}
