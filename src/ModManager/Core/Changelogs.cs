using System;

namespace ModManager.Core;

/// <summary>A mod's changelog for its CHANGELOG tab, fetched the first time the player opens that tab.</summary>
public abstract record ChangelogState
{
    private ChangelogState()
    {
    }

    /// <summary>The mod names no changelog the screen can read as text, so it has no tab.</summary>
    public sealed record None : ChangelogState;

    public sealed record NotAsked : ChangelogState;

    public sealed record Loading : ChangelogState;

    public sealed record Ready(string Text) : ChangelogState;

    public sealed record Failed(string Reason) : ChangelogState;
}

/// <summary>
/// Turns a mod's changelog link into an address that serves the file as plain text, and reads the answer. A GitHub
/// file page (github.com/owner/repo/blob/branch/path) is fetched from raw.githubusercontent.com, which needs no account
/// and does not count against GitHub's API limit. Any other https link to a .md or .txt file is fetched as it is.
/// </summary>
public static class Changelogs
{
    /// <summary>More than any real changelog; a longer answer is cut, so a wrong link cannot fill the screen.</summary>
    public const int MaxLength = 200_000;

    public static Uri? TextUri(string? link)
    {
        if (link == null || !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0) return null;

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return GitHubRaw(uri);

        return IsTextFile(uri.AbsolutePath) ? uri : null;
    }

    public static ChangelogState From(HttpAnswer answer) => answer switch
    {
        HttpAnswer.Unreachable unreachable => new ChangelogState.Failed(unreachable.Reason),
        HttpAnswer.Answered { Status: 200 } answered => new ChangelogState.Ready(Cut(answered.Body.Replace("\r\n", "\n"))),
        HttpAnswer.Answered { Status: 404 } => new ChangelogState.Failed("the changelog is not at that address"),
        HttpAnswer.Answered answered => new ChangelogState.Failed("the server answered " + answered.Status),
        _ => throw new ArgumentOutOfRangeException(nameof(answer), answer, null),
    };

    private static Uri? GitHubRaw(Uri uri)
    {
        var parts = uri.AbsolutePath.Trim('/').Split('/');

        if (parts.Length < 5 || parts[2] != "blob" || !ModInfoReader.IsGitHubName(parts[0]) || !ModInfoReader.IsGitHubName(parts[1]) || !IsTextFile(uri.AbsolutePath)) return null;

        return new Uri("https://raw.githubusercontent.com/" + parts[0] + "/" + parts[1] + "/" + string.Join("/", parts, 3, parts.Length - 3));
    }

    private static bool IsTextFile(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    private static string Cut(string answer) => answer.Length <= MaxLength ? answer : answer.Substring(0, MaxLength) + "\n...";
}
