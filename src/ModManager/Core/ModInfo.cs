using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

public enum LinkKind
{
    Nexus,
    GitHub,
    Website,
}

public sealed record ModLink(LinkKind Kind, string Url);

/// <summary>What a mod tells the Mods screen about itself. Every part is optional.</summary>
public sealed record ModInfo(string? Author, Description? Description, IReadOnlyList<ModLink> Links)
{
    public static readonly ModInfo None = new(null, null, Array.Empty<ModLink>());

    public ModLink? Link(LinkKind kind) => Links.FirstOrDefault(link => link.Kind == kind);
}

/// <summary>The info read from a mod, plus the values it set that could not be used, so the author can find out why.</summary>
public sealed record ModInfoRead(ModInfo Info, IReadOnlyList<string> Ignored);

/// <summary>
/// Reads a mod's info from the assembly metadata it declares, for example
/// <c>[assembly: AssemblyMetadata("ModManager.NexusId", "47")]</c>. Plain .NET attributes need no reference to the
/// Mod Manager, so a mod that sets them runs the same with or without it, and they can be read from a DLL that is
/// turned off and was never loaded.
/// </summary>
public static class ModInfoReader
{
    public const string Prefix = "ModManager.";
    public const string DescriptionKey = Prefix + "Description";
    public const string AuthorKey = Prefix + "Author";
    public const string NexusIdKey = Prefix + "NexusId";
    public const string GitHubKey = Prefix + "GitHub";
    public const string WebsiteKey = Prefix + "Website";

    private const int MaxAuthorLength = 80;
    private const int MaxDescriptionLength = 1000;
    private const string NexusGame = "https://www.nexusmods.com/survivallog/mods/";

    /// <summary>The first value of each key counts. Keys without the prefix belong to someone else and are passed over.</summary>
    public static ModInfoRead Read(IEnumerable<KeyValuePair<string, string?>> metadata)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var ignored = new List<string>();

        foreach (var (key, value) in metadata)
        {
            if (!key.StartsWith(Prefix, StringComparison.Ordinal) || values.ContainsKey(key)) continue;

            values[key] = (value ?? "").Trim();
        }

        var info = new ModInfo(
            Text(values, AuthorKey, MaxAuthorLength, ignored),
            Text(values, DescriptionKey, MaxDescriptionLength, ignored) is { } description ? Wording.SplitDescription(description) : null,
            Links(values, ignored));

        foreach (var key in values.Keys.Where(key => !IsKnown(key))) ignored.Add(key + " is not a key the Mod Manager knows");

        return new ModInfoRead(info, ignored);
    }

    private static bool IsKnown(string key) => key is DescriptionKey or AuthorKey or NexusIdKey or GitHubKey or WebsiteKey;

    private static string? Text(Dictionary<string, string> values, string key, int maxLength, List<string> ignored)
    {
        if (!values.TryGetValue(key, out var value) || value.Length == 0) return null;

        if (value.Length <= maxLength) return value;

        ignored.Add(key + " is longer than " + maxLength + " characters");

        return null;
    }

    private static IReadOnlyList<ModLink> Links(Dictionary<string, string> values, List<string> ignored)
    {
        var links = new List<ModLink>();

        AddLink(links, values, NexusIdKey, LinkKind.Nexus, NexusUrl, "a whole number, the id in the mod's Nexus Mods address", ignored);
        AddLink(links, values, GitHubKey, LinkKind.GitHub, GitHubUrl, "owner/repository", ignored);
        AddLink(links, values, WebsiteKey, LinkKind.Website, WebsiteUrl, "a full http or https address", ignored);

        return links;
    }

    private static void AddLink(List<ModLink> links, Dictionary<string, string> values, string key, LinkKind kind, Func<string, string?> urlOf, string expected, List<string> ignored)
    {
        if (!values.TryGetValue(key, out var value) || value.Length == 0) return;

        if (urlOf(value) is { } url) links.Add(new ModLink(kind, url));
        else ignored.Add(key + " must be " + expected + ", not \"" + value + "\"");
    }

    private static string? NexusUrl(string value) =>
        value.Length is > 0 and <= 9 && value.All(c => c is >= '0' and <= '9') && value.Any(c => c != '0') ? NexusGame + value.TrimStart('0') : null;

    private static string? GitHubUrl(string value)
    {
        var parts = value.Split('/');

        return parts.Length == 2 && parts.All(IsGitHubName) ? "https://github.com/" + parts[0] + "/" + parts[1] : null;
    }

    private static bool IsGitHubName(string part) =>
        part.Length is > 0 and <= 100 && part != "." && part != ".." && part.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.');

    /// <summary>Only web addresses: the link is opened by the player's browser, so a file or program path is never accepted.</summary>
    private static string? WebsiteUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;

        return uri.UserInfo.Length == 0 && uri.Host.Length > 0 ? uri.AbsoluteUri : null;
    }
}
