using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModManager.Core;

/// <summary>One release of a mod that the wiki keeps a copy of.</summary>
/// <param name="Games">The game versions the release was made for.</param>
/// <param name="File">The zip's name in the wiki's downloads folder.</param>
/// <param name="Sha256">The zip's SHA-256, lower-case hex. A download that does not match it is thrown away.</param>
/// <param name="DllSha256">The SHA-256 of the mod's DLL in the zip, lower-case hex; null when the index gives none.</param>
public sealed record IndexRelease(string Version, IReadOnlyList<string> Games, string File, long Size, string Sha256, string? DllSha256);

/// <param name="GitHub">The repository its releases come from, as "owner/name".</param>
public sealed record IndexMod(string Guid, string Name, string? Slug, int? NexusId, string? GitHub, IReadOnlyList<IndexRelease> Releases);

/// <summary>
/// A mod the wiki keeps no copy of, whose author publishes it on GitHub the way the Mod Manager installs from: a release
/// zip holding its DLL. BROWSE installs it from there.
/// </summary>
/// <param name="GitHub">The repository, as "owner/name".</param>
/// <param name="Dll">The mod's DLL file name.</param>
public sealed record IndexListed(string Guid, string Name, int? NexusId, string GitHub, string Dll);

/// <summary>Two mods known not to work together.</summary>
public sealed record IndexConflict(string A, string B, BilingualText Why);

/// <summary>Versions of a mod known to be broken.</summary>
public sealed record IndexBroken(string Guid, IReadOnlyList<string> Versions, BilingualText Why);

/// <summary>A known conflict as one mod sees it: the other mod and why they do not work together.</summary>
public sealed record KnownConflict(string OtherGuid, BilingualText Why);

/// <summary>
/// The wiki's list of the mods it keeps copies of: every release with its file and hashes, plus what is known about
/// mods in general (pairs that do not work together, broken builds, and the Nexus page of mods that are not ours).
/// </summary>
public sealed class ModIndex
{
    public static readonly ModIndex Empty = new("", Array.Empty<IndexMod>(), Array.Empty<IndexConflict>(), Array.Empty<IndexBroken>(), new Dictionary<string, int>());

    private readonly Dictionary<string, IndexMod> _byGuid;

    public ModIndex(string generated, IReadOnlyList<IndexMod> mods, IReadOnlyList<IndexConflict> conflicts, IReadOnlyList<IndexBroken> broken, IReadOnlyDictionary<string, int> nexusIds)
    {
        Generated = generated;
        Mods = mods;
        Conflicts = conflicts;
        Broken = broken;
        NexusIds = nexusIds;
        _byGuid = new Dictionary<string, IndexMod>(StringComparer.Ordinal);

        foreach (var mod in mods) _byGuid.TryAdd(mod.Guid, mod);
    }

    public string Generated { get; }

    public IReadOnlyList<IndexMod> Mods { get; }

    public IReadOnlyList<IndexConflict> Conflicts { get; }

    public IReadOnlyList<IndexBroken> Broken { get; }

    /// <summary>The Nexus page number of mods that are not on the wiki, by GUID.</summary>
    public IReadOnlyDictionary<string, int> NexusIds { get; }

    /// <summary>Mods of other authors that install from their own GitHub releases.</summary>
    public IReadOnlyList<IndexListed> Listed { get; init; } = Array.Empty<IndexListed>();

    public IndexListed? ListedOf(string guid) => Listed.FirstOrDefault(listed => listed.Guid == guid);

    public IndexMod? ModOf(string guid) => _byGuid.TryGetValue(guid, out var mod) ? mod : null;

    /// <summary>
    /// Which release the installed DLL is: the release whose DLL has the same SHA-256 first, else the one with the
    /// plugin's version. Null when the wiki does not know the mod or that version.
    /// </summary>
    public IndexRelease? Identify(string guid, string version, string? dllSha256)
    {
        if (ModOf(guid) is not { } mod) return null;

        if (dllSha256 != null && mod.Releases.FirstOrDefault(release => release.DllSha256 != null && string.Equals(release.DllSha256, dllSha256, StringComparison.OrdinalIgnoreCase)) is { } same) return same;

        return ReleaseOf(mod, version);
    }

    /// <summary>The newest release of the mod whose repository this is, the way GitHub would have named it.</summary>
    public LatestRelease.Found? LatestOf(string repository)
    {
        var mod = Mods.FirstOrDefault(candidate => candidate.GitHub != null && string.Equals(candidate.GitHub, repository, StringComparison.OrdinalIgnoreCase));

        if (mod?.GitHub == null || Newest(mod) is not { } newest) return null;

        var parts = mod.GitHub.Split('/');
        var tag = "v" + newest.Version;

        // GitHub could not be reached, so the release's page on the wiki stands in for GitHub's.
        var page = mod.Slug != null ? MirrorAddress.ModPage(mod.Slug) : "https://github.com/" + mod.GitHub + "/releases/tag/" + tag;

        return new LatestRelease.Found(parts[0], parts[1], tag, page);
    }

    /// <summary>
    /// The wiki's copy of an update: only for the same mod, from the same repository, at the same version, and only
    /// while downloads from the wiki are on.
    /// </summary>
    public IndexRelease? MirrorFor(string guid, string? repository, string tag, bool wikiDownloads)
    {
        if (!wikiDownloads || ModOf(guid) is not { GitHub: { } github } mod || repository == null || !string.Equals(github, repository, StringComparison.OrdinalIgnoreCase)) return null;

        return ReleaseOf(mod, tag);
    }

    /// <summary>Why this version of the mod is known to be broken, or null when it is not.</summary>
    public BilingualText? BrokenWhy(string guid, string version)
    {
        if (ReleaseVersion.Parse(version) is not { } wanted) return null;

        return Broken.FirstOrDefault(broken => broken.Guid == guid && broken.Versions.Any(listed => ReleaseVersion.Parse(listed) is { } parsed && parsed.CompareTo(wanted) == 0))?.Why;
    }

    /// <summary>Every mod known not to work with this one, each once.</summary>
    public IReadOnlyList<KnownConflict> ConflictsOf(string guid) =>
        Conflicts
            .Where(conflict => conflict.A == guid || conflict.B == guid)
            .Select(conflict => new KnownConflict(conflict.A == guid ? conflict.B : conflict.A, conflict.Why))
            .Where(conflict => conflict.OtherGuid != guid)
            .GroupBy(conflict => conflict.OtherGuid)
            .Select(group => group.First())
            .ToList();

    /// <summary>The mod's Nexus page number: the wiki's own mods carry it, and others are listed by GUID.</summary>
    public int? NexusIdOf(string guid) => ModOf(guid)?.NexusId ?? (NexusIds.TryGetValue(guid, out var id) ? id : ListedOf(guid)?.NexusId);

    /// <summary>The mod's newest release, or null when the wiki keeps none.</summary>
    public static IndexRelease? Newest(IndexMod mod) =>
        mod.Releases
            .Select(release => (Release: release, Version: ReleaseVersion.Parse(release.Version)))
            .Where(pair => pair.Version != null)
            .OrderByDescending(pair => pair.Version)
            .Select(pair => pair.Release)
            .FirstOrDefault();

    private static IndexRelease? ReleaseOf(IndexMod mod, string version) =>
        ReleaseVersion.Parse(version) is { } wanted ? mod.Releases.FirstOrDefault(release => ReleaseVersion.Parse(release.Version) is { } parsed && parsed.CompareTo(wanted) == 0) : null;
}

/// <summary>The index as read, and the parts of it that were passed over.</summary>
public sealed record ModIndexRead(ModIndex Index, IReadOnlyList<string> Ignored);

/// <summary>
/// Reads index.json. The file comes from the wiki, so every field is checked: a release whose file name, size or hash
/// is not as expected is passed over rather than trusted, and the rest of the index is still used.
/// </summary>
public static class ModIndexReader
{
    public const int Schema = 1;

    private const int MaxMods = 2000;
    private const int MaxReleasesPerMod = 200;
    private const int MaxListed = 2000;
    private const int MaxText = 2000;

    private static readonly Regex Sha256Hex = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    public static (ModIndexRead? Read, string? Problem) Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return (null, "the index is not a JSON object");

            if (!root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var number)) return (null, "the index does not say its schema");

            if (number != Schema) return (null, "the index is schema " + number + ", and this Mod Manager reads schema " + Schema);

            var ignored = new List<string>();
            var mods = Mods(root, ignored);
            var conflicts = Conflicts(root, ignored);
            var broken = Broken(root, ignored);
            var nexusIds = NexusIds(root, ignored);
            var listed = Listed(root, ignored, mods.Select(mod => mod.Guid).ToHashSet(StringComparer.Ordinal));

            return (new ModIndexRead(new ModIndex(StringOf(root, "generated") ?? "", mods, conflicts, broken, nexusIds) { Listed = listed }, ignored), null);
        }
        catch (JsonException ex)
        {
            return (null, "the index could not be read: " + ex.Message);
        }
    }

    private static List<IndexMod> Mods(JsonElement root, List<string> ignored)
    {
        var mods = new List<IndexMod>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in ArrayOf(root, "mods").Take(MaxMods))
        {
            var guid = item.ValueKind == JsonValueKind.Object ? StringOf(item, "guid") : null;

            if (guid == null || guid.Length > 200)
            {
                ignored.Add("a mod without a GUID");

                continue;
            }

            if (!seen.Add(guid))
            {
                ignored.Add("a second entry for " + guid);

                continue;
            }

            var slug = StringOf(item, "slug");
            var github = StringOf(item, "github");

            if (slug != null && !MirrorAddress.IsSlug(slug))
            {
                ignored.Add("the page name of " + guid + ": " + slug);
                slug = null;
            }

            if (github != null && !IsRepository(github))
            {
                ignored.Add("the repository of " + guid + ": " + github);
                github = null;
            }

            var releases = new List<IndexRelease>();

            foreach (var release in ArrayOf(item, "releases").Take(MaxReleasesPerMod))
            {
                var (read, problem) = Release(release);

                if (read != null) releases.Add(read);
                else ignored.Add("a release of " + guid + ": " + problem);
            }

            mods.Add(new IndexMod(guid, StringOf(item, "name") ?? guid, slug, PositiveInt(item, "nexus"), github, releases));
        }

        return mods;
    }

    private static (IndexRelease? Release, string? Problem) Release(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return (null, "it is not an object");

        var version = StringOf(item, "version");
        var file = StringOf(item, "file");
        var sha256 = StringOf(item, "sha256");
        var dllSha256 = StringOf(item, "dll_sha256");
        var size = item.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var bytes) ? bytes : 0;

        if (version == null || ReleaseVersion.Parse(version) == null) return (null, "its version cannot be read: " + version);

        if (file == null || !MirrorAddress.IsFileName(file)) return (null, version + " names no usable file: " + file);

        if (size <= 0 || size > ReleaseAssets.MaxDownloadBytes) return (null, version + " has a size of " + size);

        if (sha256 == null || !Sha256Hex.IsMatch(sha256)) return (null, version + " has no usable SHA-256");

        if (dllSha256 != null && !Sha256Hex.IsMatch(dllSha256)) dllSha256 = null;

        var games = ArrayOf(item, "games").Where(game => game.ValueKind == JsonValueKind.String).Select(game => game.GetString() ?? "").Where(game => game.Length is > 0 and <= 40).Take(50).ToList();

        return (new IndexRelease(version, games, file, size, sha256, dllSha256), null);
    }

    private static List<IndexConflict> Conflicts(JsonElement root, List<string> ignored)
    {
        var conflicts = new List<IndexConflict>();

        foreach (var item in ArrayOf(root, "conflicts").Take(MaxListed))
        {
            var a = item.ValueKind == JsonValueKind.Object ? StringOf(item, "a") : null;
            var b = item.ValueKind == JsonValueKind.Object ? StringOf(item, "b") : null;

            if (a == null || b == null || a == b || WhyOf(item) is not { } why)
            {
                ignored.Add("a conflict that does not name two mods and why");

                continue;
            }

            conflicts.Add(new IndexConflict(a, b, why));
        }

        return conflicts;
    }

    private static List<IndexBroken> Broken(JsonElement root, List<string> ignored)
    {
        var broken = new List<IndexBroken>();

        foreach (var item in ArrayOf(root, "broken").Take(MaxListed))
        {
            var guid = item.ValueKind == JsonValueKind.Object ? StringOf(item, "guid") : null;
            var versions = guid == null ? new List<string>() : ArrayOf(item, "versions").Where(version => version.ValueKind == JsonValueKind.String).Select(version => version.GetString() ?? "").Where(version => ReleaseVersion.Parse(version) != null).ToList();

            if (guid == null || versions.Count == 0 || WhyOf(item) is not { } why)
            {
                ignored.Add("a broken build that does not name a mod, its versions and why");

                continue;
            }

            broken.Add(new IndexBroken(guid, versions, why));
        }

        return broken;
    }

    private static Dictionary<string, int> NexusIds(JsonElement root, List<string> ignored)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);

        if (!root.TryGetProperty("nexus_ids", out var map) || map.ValueKind != JsonValueKind.Object) return ids;

        foreach (var pair in map.EnumerateObject().Take(MaxListed))
        {
            if (pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetInt32(out var id) && id > 0) ids[pair.Name] = id;
            else ignored.Add("the Nexus page of " + pair.Name);
        }

        return ids;
    }

    private static readonly Regex DllName = new("^[A-Za-z0-9_][A-Za-z0-9_.-]{0,100}\\.dll$", RegexOptions.CultureInvariant);

    /// <summary>Mods of other authors, each once; one the wiki keeps copies of is installed from those instead.</summary>
    private static List<IndexListed> Listed(JsonElement root, List<string> ignored, IReadOnlySet<string> mirrored)
    {
        var listed = new List<IndexListed>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in ArrayOf(root, "listed").Take(MaxListed))
        {
            var guid = item.ValueKind == JsonValueKind.Object ? StringOf(item, "guid") : null;
            var github = guid == null ? null : StringOf(item, "github");
            var dll = guid == null ? null : StringOf(item, "dll");

            if (guid == null || guid.Length > 200 || github == null || !IsRepository(github) || dll == null || !DllName.IsMatch(dll) || dll.Contains(".."))
            {
                ignored.Add("a listed mod that does not name its GUID, its GitHub repository and its DLL");

                continue;
            }

            if (mirrored.Contains(guid) || !seen.Add(guid))
            {
                ignored.Add("a second entry for " + guid);

                continue;
            }

            listed.Add(new IndexListed(guid, Cut(StringOf(item, "name") ?? guid), PositiveInt(item, "nexus"), github, dll));
        }

        return listed;
    }

    /// <summary>The English reason, and the Chinese one when there is one (else the English again).</summary>
    private static BilingualText? WhyOf(JsonElement item)
    {
        var english = StringOf(item, "why");

        if (english == null) return null;

        return new BilingualText(Cut(english), Cut(StringOf(item, "zh") ?? english));
    }

    private static string Cut(string text) => text.Length <= MaxText ? text : text.Substring(0, MaxText);

    private static bool IsRepository(string text)
    {
        var parts = text.Split('/');

        return parts.Length == 2 && ModInfoReader.IsGitHubName(parts[0]) && ModInfoReader.IsGitHubName(parts[1]);
    }

    private static IEnumerable<JsonElement> ArrayOf(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().ToList() : Enumerable.Empty<JsonElement>();

    private static string? StringOf(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text.Trim() : null;

    private static int? PositiveInt(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : null;
}

/// <summary>Where the wiki keeps its copies of the mods, which updates are downloaded from first.</summary>
public static class MirrorAddress
{
    public const string Host = "sldb.core-directive.com";

    private const string Root = "https://" + Host + "/";

    public static readonly Uri Index = new(Root + "downloads/index.json");

    private static readonly Regex FileName = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,150}\\.zip$", RegexOptions.CultureInvariant);
    private static readonly Regex Slug = new("^[a-z0-9][a-z0-9-]{0,80}$", RegexOptions.CultureInvariant);

    /// <summary>A plain zip name: no folder, so a file named in the index cannot point anywhere but the downloads folder.</summary>
    public static bool IsFileName(string name) => FileName.IsMatch(name) && !name.Contains("..");

    public static bool IsSlug(string slug) => Slug.IsMatch(slug);

    /// <param name="file">Checked with <see cref="IsFileName"/> when the index was read.</param>
    public static Uri FileUri(string file) => new(Root + "downloads/mods/" + file);

    public static string ModPage(string slug) => Root + "en/mods/" + slug + "/";

    /// <summary>The wiki's Chinese page for a Chinese game; any other address is left as it is.</summary>
    public static string ForLanguage(string url, bool isChinese) =>
        isChinese && url.StartsWith(Root + "en/", StringComparison.Ordinal) ? Root + "zh/" + url.Substring((Root + "en/").Length) : url;
}

/// <summary>SHA-256 as the index writes it.</summary>
public static class Sha256Text
{
    public static string Of(byte[] bytes)
    {
        using var sha = SHA256.Create();

        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    public static string Of(System.IO.Stream stream)
    {
        using var sha = SHA256.Create();

        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>Why a downloaded copy is not the file the index lists, or null when it is.</summary>
    public static string? Mismatch(byte[] download, IndexRelease release)
    {
        if (download.LongLength != release.Size) return "the wiki's copy is " + download.LongLength + " bytes, and the index says " + release.Size;

        return Of(download) == release.Sha256 ? null : "the wiki's copy does not match the SHA-256 in the index";
    }
}
