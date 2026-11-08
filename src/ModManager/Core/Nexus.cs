using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModManager.Core;

/// <summary>One of the game's mods on Nexus Mods, as the public list shows it.</summary>
public sealed record NexusMod(int ModId, string Name, string Version, string UpdatedAt, string Uploader);

/// <summary>One file of a mod on Nexus Mods: its version, its place on the page (MAIN, OPTIONAL, OLD_VERSION, ARCHIVED) and its manifest.</summary>
public sealed record NexusFile(int FileId, string Version, string Category, string Uri);

/// <summary>A mod's files, and the mod's last update when they were read, so they are read again only after it changes.</summary>
public sealed record NexusFiles(string UpdatedAt, IReadOnlyList<NexusFile> Files);

/// <summary>What Nexus Mods has for an installed mod: its newest version there, and whether that is newer than the installed one.</summary>
public sealed record NexusNewest(int ModId, string Name, string Version, bool IsNewer);

/// <summary>
/// What Nexus Mods said, kept between starts: the game's mods, the files of the mods that are not the wiki's, and the
/// DLL hashes in each file's manifest. A manifest never changes, so it is read once.
/// </summary>
public sealed class NexusCache
{
    public const int Schema = 1;

    private const int MaxMods = 2000;
    private const int MaxFilesPerMod = 200;
    private const int MaxManifests = 10000;
    private const int MaxHashesPerManifest = 100;

    private Dictionary<string, List<(int ModId, NexusFile File)>>? _byHash;

    public NexusCache(DateTimeOffset? checkedAt, IReadOnlyList<NexusMod> mods, IReadOnlyDictionary<int, NexusFiles> files, IReadOnlyDictionary<string, IReadOnlyList<string>> manifests)
    {
        CheckedAt = checkedAt;
        Mods = mods;
        Files = files;
        Manifests = manifests;
    }

    public static readonly NexusCache Empty = new(null, Array.Empty<NexusMod>(), new Dictionary<int, NexusFiles>(), new Dictionary<string, IReadOnlyList<string>>());

    /// <summary>When the list of mods was last read; null when it never was.</summary>
    public DateTimeOffset? CheckedAt { get; }

    public IReadOnlyList<NexusMod> Mods { get; }

    public IReadOnlyDictionary<int, NexusFiles> Files { get; }

    /// <summary>The SHA-256 of each DLL in a file, by the file's manifest address. Empty when the file holds no DLL.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Manifests { get; }

    public NexusMod? ModById(int modId) => Mods.FirstOrDefault(mod => mod.ModId == modId);

    /// <summary>Every file, of any mod, whose manifest lists a DLL with this hash.</summary>
    public IReadOnlyList<(int ModId, NexusFile File)> FilesWithDll(string sha256)
    {
        _byHash ??= ByHash();

        return _byHash.TryGetValue(sha256.ToLowerInvariant(), out var hits) ? hits : (IReadOnlyList<(int, NexusFile)>)Array.Empty<(int, NexusFile)>();
    }

    private Dictionary<string, List<(int ModId, NexusFile File)>> ByHash()
    {
        var byHash = new Dictionary<string, List<(int ModId, NexusFile File)>>(StringComparer.Ordinal);

        foreach (var (modId, files) in Files)
        {
            foreach (var file in files.Files)
            {
                if (!Manifests.TryGetValue(file.Uri, out var hashes)) continue;

                foreach (var hash in hashes)
                {
                    if (!byHash.TryGetValue(hash, out var list)) byHash[hash] = list = new List<(int, NexusFile)>();

                    list.Add((modId, file));
                }
            }
        }

        return byHash;
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", Schema);
            json.WriteString("checked", CheckedAt?.ToString("O") ?? "");
            json.WriteStartArray("mods");

            foreach (var mod in Mods)
            {
                json.WriteStartObject();
                json.WriteNumber("id", mod.ModId);
                json.WriteString("name", mod.Name);
                json.WriteString("version", mod.Version);
                json.WriteString("updated", mod.UpdatedAt);
                json.WriteString("uploader", mod.Uploader);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartObject("files");

            foreach (var (modId, files) in Files)
            {
                json.WriteStartObject(modId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                json.WriteString("updated", files.UpdatedAt);
                json.WriteStartArray("files");

                foreach (var file in files.Files)
                {
                    json.WriteStartObject();
                    json.WriteNumber("id", file.FileId);
                    json.WriteString("version", file.Version);
                    json.WriteString("category", file.Category);
                    json.WriteString("uri", file.Uri);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteStartObject("manifests");

            foreach (var (uri, hashes) in Manifests)
            {
                json.WriteStartArray(uri);

                foreach (var hash in hashes) json.WriteStringValue(hash);

                json.WriteEndArray();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The saved copy, or null when it is of another schema or cannot be read; it is then read from Nexus again.</summary>
    public static NexusCache? FromJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var number) || number != Schema) return null;

            DateTimeOffset? checkedAt = DateTimeOffset.TryParse(Json.StringOf(root, "checked"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var at) ? at : null;

            var mods = Json.ArrayOf(root, "mods").Take(MaxMods)
                .Select(item => Json.IntOf(item, "id") is { } id ? new NexusMod(id, Json.StringOf(item, "name") ?? "", Json.StringOf(item, "version") ?? "", Json.StringOf(item, "updated") ?? "", Json.StringOf(item, "uploader") ?? "") : null)
                .OfType<NexusMod>().ToList();

            var files = new Dictionary<int, NexusFiles>();

            if (root.TryGetProperty("files", out var fileMap) && fileMap.ValueKind == JsonValueKind.Object)
            {
                foreach (var pair in fileMap.EnumerateObject().Take(MaxMods))
                {
                    if (!int.TryParse(pair.Name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var modId)) continue;

                    var list = Json.ArrayOf(pair.Value, "files").Take(MaxFilesPerMod).Select(NexusApi.FileOf).OfType<NexusFile>().ToList();

                    files[modId] = new NexusFiles(Json.StringOf(pair.Value, "updated") ?? "", list);
                }
            }

            var manifests = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            if (root.TryGetProperty("manifests", out var manifestMap) && manifestMap.ValueKind == JsonValueKind.Object)
            {
                foreach (var pair in manifestMap.EnumerateObject().Take(MaxManifests))
                {
                    if (pair.Value.ValueKind != JsonValueKind.Array || !NexusApi.IsManifestUri(pair.Name)) continue;

                    manifests[pair.Name] = pair.Value.EnumerateArray().Where(hash => hash.ValueKind == JsonValueKind.String).Select(hash => hash.GetString() ?? "").Where(NexusApi.IsSha256).Take(MaxHashesPerManifest).ToList();
                }
            }

            return new NexusCache(checkedAt, mods, files, manifests);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The keyless public API of Nexus Mods: the game's mods, their files and the files' manifests, which is all the Mod
/// Manager reads there. Every request names the app and its version, as Nexus asks.
/// </summary>
public static class NexusApi
{
    public const string GameDomain = "survivallog";
    public const int GameId = 9894;

    /// <summary>The Nexus account the wiki's own mods are uploaded by: their files are known from the wiki's index.</summary>
    public const string OwnUploader = "AnExiledDev";

    public const string AppName = "SurvivalLog-ModManager";

    /// <summary>The list is asked for in pages this long; Nexus allows up to 100.</summary>
    public const int PageSize = 80;

    public const int MaxPages = 25;

    /// <summary>Mods whose files are asked for in one request.</summary>
    public const int FilesPerRequest = 10;

    /// <summary>Manifests read in one day at most; the rest wait for the next day's check.</summary>
    public const int ManifestsPerDay = 60;

    public static readonly TimeSpan Every = TimeSpan.FromDays(1);

    public static readonly Uri GraphQl = new("https://api.nexusmods.com/v2/graphql");

    private const string ManifestRoot = "https://mod-file-manifests.nexusmods.com/";

    private static readonly Regex ManifestPath = new("^[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f-]{8,64}$", RegexOptions.CultureInvariant);
    private static readonly Regex Sha256Hex = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    /// <summary>Once a day at most; a clock that went back counts as due, so a wrong date cannot stop the checks for good.</summary>
    public static bool IsDue(DateTimeOffset? checkedAt, DateTimeOffset now) => checkedAt is not { } at || now - at >= Every || at > now;

    public static string PageUrl(int modId) => "https://www.nexusmods.com/" + GameDomain + "/mods/" + modId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsManifestUri(string uri) => ManifestPath.IsMatch(uri);

    public static bool IsSha256(string text) => Sha256Hex.IsMatch(text);

    public static Uri ManifestUri(string uri) => new(ManifestRoot + uri);

    public static string ModsRequest(int offset) => Request(
        "{ mods(filter: { gameDomainName: { value: \"" + GameDomain + "\", op: EQUALS } }, count: " + PageSize + ", offset: " + offset + ") { totalCount nodes { modId name version updatedAt uploader { name } } } }");

    /// <summary>One request for several mods' files, each under its own name.</summary>
    public static string FilesRequest(IEnumerable<int> modIds) => Request(
        "{ " + string.Join(" ", modIds.Select(id => "m" + id + ": modFiles(modId: " + id + ", gameId: " + GameId + ") { fileId version category uri }")) + " }");

    private static string Request(string query)
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("query", query);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>One page of the list, and how many mods the game has there in all.</summary>
    public static (IReadOnlyList<NexusMod>? Mods, int Total, string? Problem) ReadMods(string body)
    {
        var (data, problem) = DataOf(body);

        if (data is not { } root) return (null, 0, problem);

        if (!root.TryGetProperty("mods", out var mods) || mods.ValueKind != JsonValueKind.Object) return (null, 0, "Nexus Mods sent no list of mods");

        var total = Json.IntOf(mods, "totalCount") ?? 0;
        var list = Json.ArrayOf(mods, "nodes")
            .Select(node => Json.IntOf(node, "modId") is { } id and > 0
                ? new NexusMod(id, Cut(Json.StringOf(node, "name") ?? ""), Cut(Json.StringOf(node, "version") ?? ""), Cut(Json.StringOf(node, "updatedAt") ?? ""), node.TryGetProperty("uploader", out var uploader) && uploader.ValueKind == JsonValueKind.Object ? Cut(Json.StringOf(uploader, "name") ?? "") : "")
                : null)
            .OfType<NexusMod>().ToList();

        return (list, total, null);
    }

    /// <summary>The files of each mod asked for. A mod Nexus sent nothing for is left out, so it is asked for again.</summary>
    public static (IReadOnlyDictionary<int, IReadOnlyList<NexusFile>>? Files, string? Problem) ReadFiles(string body, IEnumerable<int> modIds)
    {
        var (data, problem) = DataOf(body);

        if (data is not { } root) return (null, problem);

        var files = new Dictionary<int, IReadOnlyList<NexusFile>>();

        foreach (var id in modIds)
        {
            if (root.TryGetProperty("m" + id, out var list) && list.ValueKind == JsonValueKind.Array) files[id] = list.EnumerateArray().Take(200).Select(FileOf).OfType<NexusFile>().ToList();
        }

        return (files, null);
    }

    internal static NexusFile? FileOf(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && Json.IntOf(item, item.TryGetProperty("fileId", out _) ? "fileId" : "id") is { } id && Json.StringOf(item, "uri") is { } uri && IsManifestUri(uri)
            ? new NexusFile(id, Cut(Json.StringOf(item, "version") ?? ""), Cut(Json.StringOf(item, "category") ?? ""), uri)
            : null;

    /// <summary>The SHA-256 of every DLL the file holds, in lower case; none when it holds no DLL.</summary>
    public static (IReadOnlyList<string>? Hashes, string? Problem) ReadManifest(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return (null, "the manifest is not a JSON object");

            var hashes = Json.ArrayOf(root, "files")
                .Where(file => Json.StringOf(file, "file_path") is { } path && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(file => file.TryGetProperty("file_hashes", out var found) && found.ValueKind == JsonValueKind.Object ? Json.StringOf(found, "SHA256")?.ToLowerInvariant() : null)
                .OfType<string>().Where(IsSha256).Distinct().Take(100).ToList();

            return (hashes, null);
        }
        catch (JsonException ex)
        {
            return (null, "the manifest could not be read: " + ex.Message);
        }
    }

    private static (JsonElement? Data, string? Problem) DataOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return (null, "Nexus Mods sent an answer that is not a JSON object");

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                return (null, "Nexus Mods answered with an error: " + Cut(errors[0].ValueKind == JsonValueKind.Object ? Json.StringOf(errors[0], "message") ?? "" : ""));
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return (null, "Nexus Mods sent no data");

            return (data.Clone(), null);
        }
        catch (JsonException ex)
        {
            return (null, "Nexus Mods sent an answer that could not be read: " + ex.Message);
        }
    }

    private static string Cut(string text) => text.Length <= 200 ? text : text.Substring(0, 200);
}

/// <summary>Which mod on Nexus Mods an installed DLL is, and whether Nexus has a newer version of it.</summary>
public static class NexusMatch
{
    private static readonly string[] Current = { "MAIN", "OPTIONAL" };

    /// <summary>
    /// The DLL's hash in a file's manifest first: that names the mod, and the file says whether it is the newest.
    /// Else the Nexus page the wiki lists for the mod's GUID, by the plugin's version. A hash found in several mods
    /// (a shared library) names none of them unless one is the listed page.
    /// </summary>
    public static NexusNewest? Find(NexusCache cache, int? listedId, string installedVersion, string? dllSha256)
    {
        if (dllSha256 != null)
        {
            var hits = cache.FilesWithDll(dllSha256);
            var ids = hits.Select(hit => hit.ModId).Distinct().ToList();
            int? id = listedId is { } listed && ids.Contains(listed) ? listed : ids.Count == 1 ? ids[0] : null;

            if (id is { } found && cache.ModById(found) is { } mod)
            {
                var newestMain = cache.Files.TryGetValue(found, out var all) ? all.Files.Where(file => file.Category == "MAIN").Select(file => (int?)file.FileId).Max() : null;

                // A current file is up to date when its version is the mod's, or when it is the newest main file: authors
                // do not always give a file the version they give the mod.
                var isCurrent = hits.Where(hit => hit.ModId == found).Select(hit => hit.File)
                    .Any(file => Current.Contains(file.Category) && (Compare(mod.Version, file.Version) <= 0 || file.FileId == newestMain));

                return new NexusNewest(found, mod.Name, mod.Version, !isCurrent);
            }
        }

        if (listedId is { } page && cache.ModById(page) is { } listedMod) return new NexusNewest(page, listedMod.Name, listedMod.Version, Compare(listedMod.Version, installedVersion) > 0);

        return null;
    }

    /// <summary>Versions that cannot be read count as the same, so nothing is called newer on a guess.</summary>
    private static int Compare(string a, string b) => ReleaseVersion.Parse(a) is { } left && ReleaseVersion.Parse(b) is { } right ? left.CompareTo(right) : 0;
}

/// <summary>What the daily check asks Nexus Mods for, and how the answers join what was saved.</summary>
public static class NexusPlan
{
    private static readonly string[] CategoryOrder = { "MAIN", "OPTIONAL", "OLD_VERSION", "ARCHIVED" };

    /// <summary>
    /// The mods whose files may name an installed DLL: none when every installed mod is on the wiki; only the pages the
    /// wiki lists when it lists one for every other installed mod; else every mod that is not the wiki's.
    /// </summary>
    /// <param name="ours">The Nexus pages of the wiki's own mods, whose files the wiki's index already knows.</param>
    /// <param name="foreign">The Nexus page the wiki lists for each installed mod that is not on the wiki, or null.</param>
    public static IReadOnlyList<int> Candidates(IReadOnlyList<NexusMod> mods, IReadOnlySet<int> ours, IReadOnlyList<int?> foreign)
    {
        if (foreign.Count == 0) return Array.Empty<int>();

        var isAnyUnlisted = foreign.Any(id => id == null);
        var listed = foreign.OfType<int>().ToHashSet();

        return mods
            .Where(mod => !ours.Contains(mod.ModId) && !string.Equals(mod.Uploader, NexusApi.OwnUploader, StringComparison.OrdinalIgnoreCase))
            .Where(mod => isAnyUnlisted || listed.Contains(mod.ModId))
            .Select(mod => mod.ModId).Distinct().ToList();
    }

    /// <summary>Candidates whose files were never read, or were read before the mod last changed.</summary>
    public static IReadOnlyList<int> NeedFiles(IReadOnlyList<int> candidates, IReadOnlyList<NexusMod> mods, NexusCache saved)
    {
        var updated = mods.GroupBy(mod => mod.ModId).ToDictionary(group => group.Key, group => group.First().UpdatedAt);

        return candidates.Where(id => !saved.Files.TryGetValue(id, out var files) || !updated.TryGetValue(id, out var at) || files.UpdatedAt != at).ToList();
    }

    /// <summary>The candidates' manifests not read yet, current files first and newest first, at most this many.</summary>
    public static IReadOnlyList<string> NeedManifests(IReadOnlyList<int> candidates, IReadOnlyDictionary<int, NexusFiles> files, IReadOnlyDictionary<string, IReadOnlyList<string>> read, int max) =>
        candidates
            .SelectMany(id => files.TryGetValue(id, out var list) ? list.Files : Array.Empty<NexusFile>())
            .Where(file => !read.ContainsKey(file.Uri))
            .OrderBy(file => Rank(file.Category))
            .ThenByDescending(file => file.FileId)
            .Select(file => file.Uri)
            .Distinct(StringComparer.Ordinal)
            .Take(max)
            .ToList();

    /// <summary>
    /// The new list of mods with the files and manifests read now laid over the saved ones. A mod no longer listed loses
    /// its files, and a manifest no kept file points to is dropped.
    /// </summary>
    public static IReadOnlyDictionary<int, NexusFiles> MergeFiles(IReadOnlyList<NexusMod> mods, NexusCache saved, IReadOnlyDictionary<int, NexusFiles> fresh)
    {
        var listed = mods.Select(mod => mod.ModId).ToHashSet();
        var files = saved.Files.Where(pair => listed.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

        foreach (var (id, list) in fresh) files[id] = list;

        return files;
    }

    public static NexusCache Merge(DateTimeOffset checkedAt, IReadOnlyList<NexusMod> mods, NexusCache saved, IReadOnlyDictionary<int, NexusFiles> fresh, IReadOnlyDictionary<string, IReadOnlyList<string>> manifests)
    {
        var files = MergeFiles(mods, saved, fresh);
        var pointedTo = files.Values.SelectMany(list => list.Files).Select(file => file.Uri).ToHashSet(StringComparer.Ordinal);
        var kept = saved.Manifests.Where(pair => pointedTo.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        foreach (var (uri, hashes) in manifests) kept[uri] = hashes;

        return new NexusCache(checkedAt, mods, files, kept);
    }

    private static int Rank(string category)
    {
        var index = Array.IndexOf(CategoryOrder, category);

        return index < 0 ? CategoryOrder.Length : index;
    }
}

/// <summary>Small readers shared by the Nexus answers and the saved copy.</summary>
internal static class Json
{
    public static IEnumerable<JsonElement> ArrayOf(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().ToList() : Enumerable.Empty<JsonElement>();

    public static string? StringOf(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text.Trim() : null;

    /// <summary>A number, or a string holding one, as Nexus sends some numbers.</summary>
    public static int? IntOf(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var value)) return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
