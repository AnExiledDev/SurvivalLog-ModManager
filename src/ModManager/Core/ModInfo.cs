using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

public enum LinkKind
{
    Nexus,
    GitHub,
    Website,
    Issues,
    Changelog,
}

/// <summary>The fixed set a mod can pick its category from, so the list can be searched and read in both languages.</summary>
public enum ModCategory
{
    Gameplay,
    Balance,
    QualityOfLife,
    Interface,
    Visuals,
    Audio,
    Cheats,
    Tools,
}

/// <summary>Whether a changed setting takes effect at once or only at the next game start.</summary>
public enum ApplyMode
{
    Unknown,
    Live,
    Restart,
}

public sealed record ModLink(LinkKind Kind, string Url);

/// <summary>A key the mod listens for that is not a setting, so the player can see it and the page can warn of clashes.</summary>
/// <param name="Keys">BepInEx's form: the main key, then any held keys ("F11 + LeftControl").</param>
public sealed record FixedHotkey(string Keys, Description Label);

/// <summary>What a mod tells the Mods screen about itself. Every part is optional.</summary>
public sealed record ModInfo
{
    public static readonly ModInfo None = new();

    public string? Author { get; init; }

    public Description? Description { get; init; }

    public IReadOnlyList<ModLink> Links { get; init; } = Array.Empty<ModLink>();

    /// <summary>The name to show instead of the plugin's own, in each language.</summary>
    public Description? Name { get; init; }

    /// <summary>The name of a PNG or JPEG embedded in the mod's DLL.</summary>
    public string? Icon { get; init; }

    /// <summary>The name of a wide PNG or JPEG embedded in the mod's DLL, shown across the top of its page.</summary>
    public string? Banner { get; init; }

    public IReadOnlyList<FixedHotkey> Hotkeys { get; init; } = Array.Empty<FixedHotkey>();

    public ModCategory? Category { get; init; }

    /// <summary>A mod that only serves other mods; the list keeps it out of the way.</summary>
    public bool IsLibrary { get; init; }

    /// <summary>The game versions the mod was made for. Empty when it does not say.</summary>
    public IReadOnlyList<string> GameVersions { get; init; } = Array.Empty<string>();

    public bool ChecksForUpdates { get; init; } = true;

    /// <summary>How the mod's settings apply when a setting says nothing for itself.</summary>
    public ApplyMode SettingsApply { get; init; } = ApplyMode.Unknown;

    public ModLink? Link(LinkKind kind) => Links.FirstOrDefault(link => link.Kind == kind);

    /// <summary>"owner/repository" when the mod names one on GitHub.</summary>
    public string? GitHubRepository => Link(LinkKind.GitHub)?.Url is { } url ? url.Substring(ModInfoReader.GitHubBase.Length) : null;

    /// <summary>The repository whose releases are checked for updates, unless the mod opted out.</summary>
    public string? UpdateRepository => ChecksForUpdates ? GitHubRepository : null;
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
    public const string IssuesKey = Prefix + "Issues";
    public const string NameKey = Prefix + "Name";
    public const string IconKey = Prefix + "Icon";
    public const string BannerKey = Prefix + "Banner";
    public const string ChangelogKey = Prefix + "Changelog";
    public const string HotkeysKey = Prefix + "Hotkeys";
    public const string CategoryKey = Prefix + "Category";
    public const string LibraryKey = Prefix + "Library";
    public const string GameVersionKey = Prefix + "GameVersion";
    public const string CheckForUpdatesKey = Prefix + "CheckForUpdates";
    public const string SettingsKey = Prefix + "Settings";

    /// <summary>Written by the .NET SDK from a project's RepositoryUrl; used for GitHub when the mod does not say.</summary>
    public const string RepositoryUrlKey = "RepositoryUrl";

    public const string GitHubBase = "https://github.com/";

    private const int MaxAuthorLength = 80;
    private const int MaxNameLength = 160;
    private const int MaxDescriptionLength = 1000;
    private const int MaxIconLength = 200;
    private const int MaxGameVersions = 10;
    private const int MaxHotkeys = 30;
    private const int MaxHotkeysLength = 3000;
    private const int MaxHotkeyLabelLength = 160;

    private const string NexusGame = "https://www.nexusmods.com/survivallog/mods/";

    private static readonly string[] Known = { DescriptionKey, AuthorKey, NexusIdKey, GitHubKey, WebsiteKey, IssuesKey, ChangelogKey, NameKey, IconKey, BannerKey, HotkeysKey, CategoryKey, LibraryKey, GameVersionKey, CheckForUpdatesKey, SettingsKey };

    /// <summary>The first value of each key counts. Keys without the prefix belong to someone else and are passed over.</summary>
    /// <param name="keyNames">Unity's KeyCode names, which a fixed hotkey must use.</param>
    public static ModInfoRead Read(IEnumerable<KeyValuePair<string, string?>> metadata, IReadOnlyList<string> keyNames)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var ignored = new List<string>();

        foreach (var (key, value) in metadata)
        {
            var isOurs = key.StartsWith(Prefix, StringComparison.Ordinal) || key == RepositoryUrlKey;

            if (!isOurs || values.ContainsKey(key)) continue;

            values[key] = (value ?? "").Trim();
        }

        var info = new ModInfo
        {
            Author = Text(values, AuthorKey, MaxAuthorLength, ignored),
            Description = Text(values, DescriptionKey, MaxDescriptionLength, ignored) is { } description ? Wording.SplitDescription(description) : null,
            Links = Links(values, ignored),
            Name = Text(values, NameKey, MaxNameLength, ignored) is { } name ? Wording.SplitDescription(name) : null,
            Icon = Text(values, IconKey, MaxIconLength, ignored),
            Banner = Text(values, BannerKey, MaxIconLength, ignored),
            Hotkeys = Text(values, HotkeysKey, MaxHotkeysLength, ignored) is { } hotkeys ? HotkeysOf(hotkeys, keyNames, ignored) : Array.Empty<FixedHotkey>(),
            Category = Parsed(values, CategoryKey, CategoryOf, "one of " + string.Join(", ", Enum.GetNames(typeof(ModCategory))), ignored),
            IsLibrary = Parsed(values, LibraryKey, BoolOf, "true or false", ignored) ?? false,
            GameVersions = GameVersionsOf(values, ignored),
            ChecksForUpdates = Parsed(values, CheckForUpdatesKey, BoolOf, "true or false", ignored) ?? true,
            SettingsApply = Parsed(values, SettingsKey, ApplyOf, "Live or Restart", ignored) ?? ApplyMode.Unknown,
        };

        foreach (var key in values.Keys.Where(key => key != RepositoryUrlKey && !Known.Contains(key))) ignored.Add(key + " is not a key the Mod Manager knows");

        return new ModInfoRead(info, ignored);
    }

    /// <summary>"Live" or "Restart", in any case; anything else says nothing.</summary>
    public static ApplyMode? ApplyOf(string value) => value.ToLowerInvariant() switch
    {
        "live" => ApplyMode.Live,
        "restart" => ApplyMode.Restart,
        _ => null,
    };

    /// <summary>Case, spaces, "-" and "_" do not matter: "quality of life" is QualityOfLife.</summary>
    public static ModCategory? CategoryOf(string value)
    {
        var plain = new string(value.Where(c => c is not (' ' or '-' or '_')).ToArray());

        foreach (ModCategory category in Enum.GetValues(typeof(ModCategory)))
        {
            if (string.Equals(category.ToString(), plain, StringComparison.OrdinalIgnoreCase)) return category;
        }

        return null;
    }

    /// <summary>
    /// "F11 + LeftControl=Open the console|打开控制台;F12=Take a picture": keys, "=", then the label in English, or
    /// English and Chinese split by "|". A bad entry is reported and the rest are kept.
    /// </summary>
    private static IReadOnlyList<FixedHotkey> HotkeysOf(string value, IReadOnlyList<string> keyNames, List<string> ignored)
    {
        var hotkeys = new List<FixedHotkey>();

        foreach (var entry in value.Split(';').Select(part => part.Trim()).Where(part => part.Length > 0))
        {
            var equals = entry.IndexOf('=');
            var keys = equals < 0 ? null : KeyChords.Normalize(entry.Substring(0, equals), keyNames);
            var label = equals < 0 ? "" : entry.Substring(equals + 1).Trim();

            if (keys == null || label.Length == 0 || label.Length > MaxHotkeyLabelLength)
            {
                ignored.Add(HotkeysKey + " entry \"" + entry + "\" must be keys=label, such as F11 + LeftControl=Open the console, with a label of up to " + MaxHotkeyLabelLength + " characters");
            }
            else if (hotkeys.Count == MaxHotkeys)
            {
                ignored.Add(HotkeysKey + " lists more than " + MaxHotkeys + " hotkeys; the rest are not shown");

                break;
            }
            else
            {
                hotkeys.Add(new FixedHotkey(keys, Wording.SplitDescription(label.Replace('|', '\n'))));
            }
        }

        return hotkeys;
    }

    private static bool? BoolOf(string value) => value.ToLowerInvariant() switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    private static T? Parsed<T>(Dictionary<string, string> values, string key, Func<string, T?> parse, string expected, List<string> ignored) where T : struct
    {
        if (!values.TryGetValue(key, out var value) || value.Length == 0) return null;

        var parsed = parse(value);

        if (parsed == null) ignored.Add(key + " must be " + expected + ", not \"" + value + "\"");

        return parsed;
    }

    /// <summary>Comma separated, each one to four whole numbers: "1.1.18902", or "1.1" for every 1.1 build.</summary>
    private static IReadOnlyList<string> GameVersionsOf(Dictionary<string, string> values, List<string> ignored)
    {
        if (!values.TryGetValue(GameVersionKey, out var value) || value.Length == 0) return Array.Empty<string>();

        var versions = value.Split(',').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();

        if (versions.Count is > 0 and <= MaxGameVersions && versions.All(GameVersion.IsVersion)) return versions;

        ignored.Add(GameVersionKey + " must be up to " + MaxGameVersions + " game versions separated by commas, such as 1.1.18902, not \"" + value + "\"");

        return Array.Empty<string>();
    }

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

        // The SDK's value is a fallback and not ours to complain about, so one that is not a GitHub repository is passed over quietly.
        if (links.All(link => link.Kind != LinkKind.GitHub) && values.TryGetValue(RepositoryUrlKey, out var repository) && GitHubUrlFromAddress(repository) is { } fromSdk) links.Add(new ModLink(LinkKind.GitHub, fromSdk));

        AddLink(links, values, WebsiteKey, LinkKind.Website, WebsiteUrl, "a full http or https address", ignored);
        AddLink(links, values, IssuesKey, LinkKind.Issues, WebsiteUrl, "a full http or https address", ignored);
        AddLink(links, values, ChangelogKey, LinkKind.Changelog, WebsiteUrl, "a full http or https address", ignored);

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

        return parts.Length == 2 && parts.All(IsGitHubName) ? GitHubBase + parts[0] + "/" + parts[1] : null;
    }

    /// <summary>"https://github.com/owner/repository", with or without ".git" or a trailing "/".</summary>
    private static string? GitHubUrlFromAddress(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0) return null;

        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && !uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase)) return null;

        var parts = uri.AbsolutePath.Trim('/').Split('/');

        if (parts.Length != 2) return null;

        var repository = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1].Substring(0, parts[1].Length - 4) : parts[1];

        return GitHubUrl(parts[0] + "/" + repository);
    }

    public static bool IsGitHubName(string part) =>
        part.Length is > 0 and <= 100 && part != "." && part != ".." && part.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.');

    /// <summary>Only web addresses: the link is opened by the player's browser, so a file or program path is never accepted.</summary>
    private static string? WebsiteUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;

        return uri.UserInfo.Length == 0 && uri.Host.Length > 0 ? uri.AbsoluteUri : null;
    }
}

/// <summary>The running game's version, read from the Version.txt the game ships, and whether a mod was made for it.</summary>
public static class GameVersion
{
    public static bool IsVersion(string text)
    {
        var parts = text.Split('.');

        return parts.Length is >= 1 and <= 4 && parts.All(part => part.Length is > 0 and <= 9 && part.All(c => c is >= '0' and <= '9'));
    }

    /// <summary>The file holds the version on its first line ("1.1") and the build on its second ("18902").</summary>
    public static string? FromVersionFile(string text)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(line => line.Trim()).ToArray();

        if (lines.Length == 0 || !IsVersion(lines[0])) return null;

        return lines.Length > 1 && lines[1].Length > 0 && IsVersion(lines[1]) && !lines[1].Contains('.') ? lines[0] + "." + lines[1] : lines[0];
    }

    /// <summary>True when the mod names no version, or the running one is a version it names or a build under one ("1.1" covers "1.1.18902").</summary>
    public static bool IsMadeFor(IReadOnlyList<string> madeFor, string? running)
    {
        if (madeFor.Count == 0 || running == null) return true;

        return madeFor.Any(version => running == version || running.StartsWith(version + ".", StringComparison.Ordinal));
    }

    /// <summary>
    /// True when the running game is one the mod was made for, or one its author has since listed it as working on.
    /// A mod that names no version anywhere is never held against the game.
    /// </summary>
    public static bool IsKnownToWork(IReadOnlyList<string> madeFor, IReadOnlyList<string> listedAsWorking, string? running) =>
        IsMadeFor(madeFor.Concat(listedAsWorking).ToList(), running);
}
