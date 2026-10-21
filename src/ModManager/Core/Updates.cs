using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModManager.Core;

/// <summary>
/// A version read from a release tag or a plugin's version: one to four whole numbers, then an optional prerelease
/// label after "-". Anything before the first digit ("v", "ModName-") and build data after "+" are passed over.
/// </summary>
public sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
    private const int MaxParts = 4;
    private const int MaxDigits = 9;

    private readonly int[] _numbers;
    private readonly string[] _prerelease;

    private ReleaseVersion(int[] numbers, string[] prerelease)
    {
        _numbers = numbers;
        _prerelease = prerelease;
    }

    public static ReleaseVersion? Parse(string text)
    {
        var start = text.IndexOfAny("0123456789".ToCharArray());

        if (start < 0) return null;

        var rest = text.Substring(start).Trim();
        var plus = rest.IndexOf('+');

        if (plus >= 0) rest = rest.Substring(0, plus);

        var dash = rest.IndexOf('-');
        var core = dash >= 0 ? rest.Substring(0, dash) : rest;
        var label = dash >= 0 ? rest.Substring(dash + 1) : "";
        var parts = core.Split('.');

        if (parts.Length > MaxParts || !parts.All(IsNumber)) return null;

        if (dash >= 0 && (label.Length == 0 || label.Split('.').Any(part => part.Length == 0))) return null;

        return new ReleaseVersion(parts.Select(int.Parse).ToArray(), label.Length == 0 ? Array.Empty<string>() : label.Split('.'));
    }

    /// <summary>Missing numbers count as 0 (1.2 equals 1.2.0); a release is newer than any prerelease of the same numbers.</summary>
    public int CompareTo(ReleaseVersion? other)
    {
        if (other == null) return 1;

        for (var i = 0; i < Math.Max(_numbers.Length, other._numbers.Length); i++)
        {
            var order = NumberAt(i).CompareTo(other.NumberAt(i));

            if (order != 0) return order;
        }

        if (_prerelease.Length == 0 || other._prerelease.Length == 0) return other._prerelease.Length.CompareTo(_prerelease.Length);

        return ComparePrerelease(_prerelease, other._prerelease);
    }

    private int NumberAt(int index) => index < _numbers.Length ? _numbers[index] : 0;

    private static bool IsNumber(string part) => part.Length is > 0 and <= MaxDigits && part.All(c => c is >= '0' and <= '9');

    /// <summary>Label parts compare one by one as in semantic versioning: numbers by value and below words, words by ordinal.</summary>
    private static int ComparePrerelease(string[] mine, string[] theirs)
    {
        for (var i = 0; i < Math.Min(mine.Length, theirs.Length); i++)
        {
            var isMineNumber = IsNumber(mine[i]);
            var isTheirsNumber = IsNumber(theirs[i]);
            var order = (isMineNumber, isTheirsNumber) switch
            {
                (true, true) => int.Parse(mine[i]).CompareTo(int.Parse(theirs[i])),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(mine[i], theirs[i]),
            };

            if (order != 0) return Math.Sign(order);
        }

        return mine.Length.CompareTo(theirs.Length);
    }
}

/// <summary>What GitHub said is the latest release of one repository.</summary>
public abstract record LatestRelease
{
    private LatestRelease()
    {
    }

    public sealed record Checking : LatestRelease;

    /// <summary>The newest release that is neither a draft nor a prerelease, and the address of its page.</summary>
    public sealed record Found(string Owner, string Repository, string Tag, string PageUrl) : LatestRelease;

    public sealed record NoReleases : LatestRelease;

    public sealed record Failed(string Reason) : LatestRelease;
}

/// <summary>The notes of every release since the installed one, fetched only when the player looks at a mod that has an update.</summary>
public abstract record NotesState
{
    private NotesState()
    {
    }

    public sealed record NotAsked : NotesState;

    public sealed record Loading : NotesState;

    /// <summary>The releases newer than the installed version that have notes, newest first.</summary>
    public sealed record Ready(IReadOnlyList<ReleaseNote> Releases) : NotesState
    {
        public bool Equals(Ready? other) => other != null && Releases.SequenceEqual(other.Releases);

        public override int GetHashCode() => Releases.Count;
    }

    /// <summary>None of the new releases has notes.</summary>
    public sealed record Empty : NotesState;

    /// <summary>GitHub's limit for requests without an account was reached; it resets within the hour.</summary>
    public sealed record Busy : NotesState;

    public sealed record Failed(string Reason) : NotesState;
}

/// <summary>Whether one mod has an update, comparing its installed version with its repository's latest release.</summary>
public abstract record UpdateState
{
    private UpdateState()
    {
    }

    /// <summary>The mod names no GitHub repository, or checking is turned off.</summary>
    public sealed record NotChecked : UpdateState;

    public sealed record Checking : UpdateState;

    public sealed record UpToDate(string Tag) : UpdateState;

    public sealed record Available(string Tag, NotesState Notes) : UpdateState;

    public sealed record NoReleases : UpdateState;

    public sealed record Failed(string Reason) : UpdateState;

    /// <summary>The latest tag or the installed version has no version number this can compare.</summary>
    public sealed record Unreadable(string Tag) : UpdateState;
}

/// <summary>An HTTP reply as the update checks need it, or why there was none.</summary>
public abstract record HttpAnswer
{
    private HttpAnswer()
    {
    }

    public sealed record Answered(int Status, string? Location, string Body) : HttpAnswer;

    public sealed record Unreachable(string Reason) : HttpAnswer;
}

/// <summary>
/// Finds a repository's latest release from where github.com/owner/repo/releases/latest redirects to. That page needs
/// no account or key and does not count against GitHub's API limit of 60 requests an hour, which the release notes use.
/// </summary>
public static class LatestReleaseFinder
{
    /// <summary>A renamed or moved repository redirects to its new name first.</summary>
    private const int MaxHops = 3;

    public static Uri LatestUri(string repository) => new("https://github.com/" + repository + "/releases/latest");

    public static async Task<LatestRelease> Find(string repository, Func<Uri, Task<HttpAnswer>> head)
    {
        var uri = LatestUri(repository);

        for (var hop = 0; hop <= MaxHops; hop++)
        {
            switch (await head(uri).ConfigureAwait(false))
            {
                case HttpAnswer.Unreachable unreachable:
                    return new LatestRelease.Failed(unreachable.Reason);
                case HttpAnswer.Answered { Status: 404 }:
                    return new LatestRelease.Failed("GitHub has no repository " + repository);
                case HttpAnswer.Answered { Status: 301 or 302 or 303 or 307 or 308 } moved:
                    switch (Read(uri, moved.Location))
                    {
                        case Step.Release release:
                            return release.Latest;
                        case Step.NoReleases:
                            return new LatestRelease.NoReleases();
                        case Step.Again again:
                            uri = again.Uri;
                            continue;
                        default:
                            return new LatestRelease.Failed("GitHub sent an unexpected address: " + moved.Location);
                    }
                case HttpAnswer.Answered answered:
                    return new LatestRelease.Failed("GitHub answered " + answered.Status);
                default:
                    throw new InvalidOperationException("unknown HTTP answer");
            }
        }

        return new LatestRelease.Failed("GitHub redirected too many times");
    }

    private abstract record Step
    {
        public sealed record Release(LatestRelease.Found Latest) : Step;

        public sealed record NoReleases : Step;

        public sealed record Again(Uri Uri) : Step;

        public sealed record Unexpected : Step;
    }

    /// <summary>Only an https address on github.com is followed or opened later, whatever the redirect says.</summary>
    private static Step Read(Uri request, string? location)
    {
        if (string.IsNullOrWhiteSpace(location) || !Uri.TryCreate(request, location, out var uri)) return new Step.Unexpected();

        if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return new Step.Unexpected();

        var parts = uri.AbsolutePath.Trim('/').Split('/');

        if (parts.Length < 3 || parts[2] != "releases" || !ModInfoReader.IsGitHubName(parts[0]) || !ModInfoReader.IsGitHubName(parts[1])) return new Step.Unexpected();

        if (parts.Length == 3) return new Step.NoReleases();

        if (parts.Length == 4 && parts[3] == "latest") return new Step.Again(new Uri(uri.GetLeftPart(UriPartial.Path)));

        if (parts.Length < 5 || parts[3] != "tag") return new Step.Unexpected();

        var tag = Uri.UnescapeDataString(string.Join("/", parts.Skip(4)));

        return tag.Trim().Length is > 0 and <= 200 ? new Step.Release(new LatestRelease.Found(parts[0], parts[1], tag, uri.GetLeftPart(UriPartial.Path))) : new Step.Unexpected();
    }
}

/// <summary>One release's tag and its notes as plain text.</summary>
public sealed record ReleaseNote(string Tag, string Text);

/// <summary>
/// Release notes come from GitHub's API, which allows 60 requests an hour without an account. One request lists the
/// recent releases, so a player several versions behind sees what every release since theirs changed.
/// </summary>
public static class ReleaseNotes
{
    public const int MaxLength = 3000;
    public const int MaxReleases = 10;

    /// <summary>The address of one release, which lists its files.</summary>
    public static Uri ApiUri(LatestRelease.Found release) =>
        new("https://api.github.com/repos/" + release.Owner + "/" + release.Repository + "/releases/tags/" + Uri.EscapeDataString(release.Tag));

    /// <summary>The address of the repository's recent releases.</summary>
    public static Uri ListUri(LatestRelease.Found release) =>
        new("https://api.github.com/repos/" + release.Owner + "/" + release.Repository + "/releases?per_page=30");

    /// <param name="installedVersion">Releases up to this version are left out.</param>
    /// <param name="latestTag">Releases after the latest one are left out.</param>
    public static NotesState From(HttpAnswer answer, string installedVersion, string latestTag) => answer switch
    {
        HttpAnswer.Unreachable unreachable => new NotesState.Failed(unreachable.Reason),
        HttpAnswer.Answered { Status: 200 } ok => Read(ok.Body, installedVersion, latestTag),
        HttpAnswer.Answered { Status: 403 or 429 } => new NotesState.Busy(),
        HttpAnswer.Answered answered => new NotesState.Failed("GitHub answered " + answered.Status),
        _ => throw new InvalidOperationException("unknown HTTP answer"),
    };

    /// <summary>Each release's own text, shown as plain text and cut short when long; the release page has all of it.</summary>
    private static NotesState Read(string json, string installedVersion, string latestTag)
    {
        var installed = ReleaseVersion.Parse(installedVersion);
        var latest = ReleaseVersion.Parse(latestTag);
        List<(ReleaseVersion Version, ReleaseNote Note)> releases;

        if (installed == null || latest == null) return new NotesState.Empty();

        try
        {
            releases = Published(json);
        }
        catch (JsonException ex)
        {
            return new NotesState.Failed("GitHub sent notes that could not be read: " + ex.Message);
        }

        var notes = releases
            .Where(release => release.Version.CompareTo(installed) > 0 && release.Version.CompareTo(latest) <= 0)
            .OrderByDescending(release => release.Version)
            .Select(release => release.Note)
            .Take(MaxReleases)
            .ToList();

        return notes.Count == 0 ? new NotesState.Empty() : new NotesState.Ready(notes);
    }

    /// <summary>Releases with a readable version and some notes. Drafts and prereleases are passed over.</summary>
    private static List<(ReleaseVersion Version, ReleaseNote Note)> Published(string json)
    {
        using var document = JsonDocument.Parse(json);
        var found = new List<(ReleaseVersion, ReleaseNote)>();

        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("the answer is not a list of releases");

        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object || IsTrue(release, "draft") || IsTrue(release, "prerelease")) continue;

            var tag = StringOf(release, "tag_name");
            var text = (StringOf(release, "body") ?? "").Replace("\r", "").Trim();

            if (tag == null || ReleaseVersion.Parse(tag) is not { } version || text.Length == 0) continue;

            found.Add((version, new ReleaseNote(tag, text.Length <= MaxLength ? text : text.Substring(0, MaxLength).TrimEnd() + "\n...")));
        }

        return found;
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsTrue(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

/// <summary>
/// The latest release of each repository and the notes asked for this session, and what that means for each mod.
/// Main thread only: the background checks hand their results over to be recorded here.
/// </summary>
public sealed class UpdateBook
{
    private readonly Dictionary<string, LatestRelease> _latest = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NotesState> _notes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InstallState> _installs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RollbackState> _rollbacks = new(StringComparer.Ordinal);
    private readonly Dictionary<Uri, ChangelogState> _changelogs = new();
    private readonly Dictionary<string, GameVersionList> _gameVersions = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string repository, LatestRelease latest) => _latest[repository] = latest;

    /// <summary>Game version lists are kept for the session whether or not update checks run again.</summary>
    public void Record(string repository, GameVersionList list) => _gameVersions[repository] = list;

    /// <summary>The game versions the repository lists this version of its mod as working on; none when it lists nothing.</summary>
    public IReadOnlyList<string> GameVersionsListed(string? repository, string modVersion) =>
        repository != null && _gameVersions.TryGetValue(repository, out var list) ? list.For(modVersion) : Array.Empty<string>();

    /// <summary>Changelogs are kept for the session whether or not update checks run again.</summary>
    public void Record(Uri changelog, ChangelogState state) => _changelogs[changelog] = state;

    /// <param name="installedVersion">Notes list the releases since this version, so two installs of one repository can differ.</param>
    public void Record(LatestRelease.Found release, string installedVersion, NotesState notes) => _notes[NotesKey(release, installedVersion)] = notes;

    public void Record(string guid, InstallState install) => _installs[guid] = install;

    /// <summary>Only a rollback that was tried is recorded; one that can be done is worked out from the backups.</summary>
    public void Record(string guid, RollbackState rollback) => _rollbacks[guid] = rollback;

    /// <summary>Installs are kept: the files on disk stay changed whether or not checks run again.</summary>
    public void Clear()
    {
        _latest.Clear();
        _notes.Clear();
    }

    /// <summary>True once every check started has an answer; false while none were started.</summary>
    public bool IsSettled => _latest.Count > 0 && _latest.Values.All(latest => latest is not LatestRelease.Checking);

    /// <param name="changelog">Where the mod's changelog is as text, or null when it names none the screen can read.</param>
    public ChangelogState ChangelogOf(Uri? changelog)
    {
        if (changelog == null) return new ChangelogState.None();

        return _changelogs.TryGetValue(changelog, out var state) ? state : new ChangelogState.NotAsked();
    }

    public InstallState InstallOf(string guid) => _installs.TryGetValue(guid, out var install) ? install : new InstallState.None();

    /// <param name="backupVersions">The names of the mod's backup folders.</param>
    public RollbackState RollbackOf(string guid, IReadOnlyList<string> backupVersions, string runningVersion)
    {
        if (_rollbacks.TryGetValue(guid, out var tried)) return tried;

        return Rollback.Target(backupVersions, runningVersion, InstallOf(guid)) is { } version ? new RollbackState.Available(version) : new RollbackState.None();
    }

    /// <summary>After a rollback the mod's files are left alone until the next start, so nothing can install over them.</summary>
    public bool HasRolledBack(string guid) => _rollbacks.TryGetValue(guid, out var tried) && tried is RollbackState.Done;

    public LatestRelease? Latest(string? repository) => repository != null && _latest.TryGetValue(repository, out var latest) ? latest : null;

    public NotesState Notes(LatestRelease.Found release, string installedVersion) => _notes.TryGetValue(NotesKey(release, installedVersion), out var notes) ? notes : new NotesState.NotAsked();

    public UpdateState StateOf(string? repository, string installedVersion)
    {
        switch (Latest(repository))
        {
            case null:
                return new UpdateState.NotChecked();
            case LatestRelease.Checking:
                return new UpdateState.Checking();
            case LatestRelease.NoReleases:
                return new UpdateState.NoReleases();
            case LatestRelease.Failed failed:
                return new UpdateState.Failed(failed.Reason);
            case LatestRelease.Found found:
                var latest = ReleaseVersion.Parse(found.Tag);
                var installed = ReleaseVersion.Parse(installedVersion);

                if (latest == null || installed == null) return new UpdateState.Unreadable(found.Tag);

                return latest.CompareTo(installed) > 0 ? new UpdateState.Available(found.Tag, Notes(found, installedVersion)) : new UpdateState.UpToDate(found.Tag);
            default:
                throw new InvalidOperationException("unknown latest release");
        }
    }

    /// <summary>The page of the release a mod can update to, or null when it has no update.</summary>
    public LatestRelease.Found? UpdateFor(string? repository, string installedVersion) =>
        StateOf(repository, installedVersion) is UpdateState.Available && Latest(repository) is LatestRelease.Found found ? found : null;

    private static string NotesKey(LatestRelease.Found release, string installedVersion) => release.Owner.ToLowerInvariant() + "/" + release.Repository.ToLowerInvariant() + "|" + release.Tag + "|" + installedVersion;
}
