using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModManager.Core;

/// <summary>Where installing one mod's update stands this session. The new files are used from the next game start.</summary>
public abstract record InstallState
{
    private InstallState()
    {
    }

    public sealed record None : InstallState;

    public sealed record Installing : InstallState;

    public sealed record Installed(string Tag) : InstallState;

    /// <summary>The update is downloaded and checked, and can do things the installed version cannot; the player decides.</summary>
    public sealed record NeedsApproval(string Tag, IReadOnlyList<Power> NewPowers) : InstallState
    {
        public bool Equals(NeedsApproval? other) => other != null && Tag == other.Tag && NewPowers.SequenceEqual(other.NewPowers);

        public override int GetHashCode() => Tag.GetHashCode();
    }

    /// <summary>Something went wrong that trying again could fix, such as GitHub not answering.</summary>
    public sealed record Failed(string Reason) : InstallState;

    /// <summary>The update itself breaks a safety rule, so trying again gives the same answer and it is not offered.</summary>
    public sealed record Refused(string Reason) : InstallState;
}

/// <summary>A file attached to a GitHub release.</summary>
public sealed record ReleaseAsset(string Name, Uri Url, long Size);

/// <summary>Picks the zip to install from a release's API answer.</summary>
public static class ReleaseAssets
{
    public const long MaxDownloadBytes = 50L * 1024 * 1024;

    /// <summary>The only .zip in the release, else the one whose name holds the DLL's name ("BetterMap" for BetterMap.dll).</summary>
    public static (ReleaseAsset? Asset, string? Problem) Choose(string json, string dllFileName)
    {
        List<ReleaseAsset> zips;

        try
        {
            zips = ZipsIn(json);
        }
        catch (JsonException ex)
        {
            return (null, "GitHub sent a release that could not be read: " + ex.Message);
        }

        var baseName = Path.GetFileNameWithoutExtension(dllFileName);
        var chosen = zips.Count == 1 ? zips[0] : zips.Where(zip => zip.Name.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0).ToList() is { Count: 1 } named ? named[0] : null;

        if (chosen == null) return (null, zips.Count == 0 ? "the release has no .zip file to install" : "the release has several .zip files and none is named after " + baseName);

        if (chosen.Size > MaxDownloadBytes) return (null, chosen.Name + " is larger than " + MaxDownloadBytes / 1024 / 1024 + " MB");

        return DownloadAddress.IsAllowed(chosen.Url) ? (chosen, null) : (null, "the release's file is not on GitHub: " + chosen.Url);
    }

    private static List<ReleaseAsset> ZipsIn(string json)
    {
        using var document = JsonDocument.Parse(json);
        var zips = new List<ReleaseAsset>();

        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return zips;

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object) continue;

            var name = asset.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
            var size = asset.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var bytes) ? bytes : 0;

            if (name == null || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;

            zips.Add(new ReleaseAsset(name, uri, size));
        }

        return zips;
    }
}

/// <summary>Downloads are only taken from GitHub, over https, including every redirect on the way.</summary>
public static class DownloadAddress
{
    public const int MaxHops = 5;

    public static bool IsAllowed(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return false;

        var host = uri.Host.ToLowerInvariant();

        return host == "github.com" || host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);
    }

    /// <summary>The next address of a redirect, or null when it leaves GitHub or cannot be read.</summary>
    public static Uri? Next(Uri request, string? location) =>
        !string.IsNullOrWhiteSpace(location) && Uri.TryCreate(request, location, out var next) && IsAllowed(next) ? next : null;
}

/// <summary>A file in a downloaded zip: its path inside the zip and its unpacked size.</summary>
public sealed record ZipItem(string FullName, long Length);

/// <summary>One file to write: the zip entry it comes from and the full path it goes to.</summary>
public sealed record PlacedFile(string EntryName, string Target);

/// <summary>
/// Decides where each file of an update's zip goes. The zip must hold the mod's DLL; the folder holding it in the zip
/// maps onto the folder holding it on disk, and nothing may land outside that folder.
/// </summary>
public static class ZipPlan
{
    public const int MaxEntries = 100;
    public const long MaxUnpackedBytes = 200L * 1024 * 1024;

    private static readonly string[] SameNameExtensions = { ".dll", ".pdb", ".xml" };

    /// <summary>Files Windows runs when opened. A mod never needs one, so an update that brings one is refused whole.</summary>
    private static readonly string[] ProgramExtensions = { ".exe", ".com", ".scr", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".wsf", ".hta", ".msi", ".lnk", ".reg" };

    /// <param name="modFolder">The folder of the mod's DLL now.</param>
    /// <param name="isSharedFolder">The mod's DLL sits straight in the plugins folder, which other mods share, so only files named after the DLL are written.</param>
    public static (IReadOnlyList<PlacedFile>? Files, string? Problem) Place(IReadOnlyList<ZipItem> items, string dllFileName, string modFolder, bool isSharedFolder)
    {
        if (items.Count > MaxEntries) return (null, "the zip has more than " + MaxEntries + " files");

        if (items.Sum(item => Math.Max(0, item.Length)) > MaxUnpackedBytes) return (null, "the zip unpacks to more than " + MaxUnpackedBytes / 1024 / 1024 + " MB");

        var files = items.Select(item => (Item: item, Path: item.FullName.Replace('\\', '/'))).Where(file => !file.Path.EndsWith("/", StringComparison.Ordinal)).ToList();
        var dll = files
            .Where(file => string.Equals(LastPart(file.Path), dllFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Path.Count(c => c == '/'))
            .Select(file => file.Path)
            .FirstOrDefault();

        if (dll == null) return (null, "the zip does not hold " + dllFileName);

        var root = dll.Substring(0, dll.Length - LastPart(dll).Length);
        var folder = Path.GetFullPath(modFolder);
        var baseName = Path.GetFileNameWithoutExtension(dllFileName);
        var placed = new List<PlacedFile>();

        foreach (var (item, path) in files.Where(file => file.Path.StartsWith(root, StringComparison.Ordinal)))
        {
            var relative = path.Substring(root.Length);

            if (isSharedFolder && !IsNamedAfter(relative, baseName)) continue;

            var target = Path.GetFullPath(Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar)));

            if (!IsBelow(folder, target)) return (null, "the zip has a file that would land outside the mod's folder: " + item.FullName);

            if (HasBadName(relative)) return (null, "the zip has a file name Windows would change or read differently: " + item.FullName);

            if (IsProgram(relative)) return (null, "the zip holds a program file, which is never installed: " + item.FullName);

            placed.Add(new PlacedFile(item.FullName, target));
        }

        return (placed, null);
    }

    /// <summary>
    /// Windows drops a trailing dot or space from a name (so "run.bat " is written as run.bat) and reads ':' as a hidden
    /// stream of a file, so a check on the name in the zip would not be a check on the file written.
    /// </summary>
    private static bool HasBadName(string relative) =>
        relative.Split('/').Any(part => part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal) || part.IndexOfAny(BadNameCharacters) >= 0);

    private static readonly char[] BadNameCharacters = { ':', '*', '?', '"', '<', '>', '|' };

    private static string LastPart(string path) => path.Substring(path.LastIndexOf('/') + 1);

    private static bool IsProgram(string relative) => ProgramExtensions.Contains(Path.GetExtension(relative).ToLowerInvariant());

    private static bool IsNamedAfter(string relative, string baseName) =>
        !relative.Contains('/') && string.Equals(Path.GetFileNameWithoutExtension(relative), baseName, StringComparison.OrdinalIgnoreCase) && SameNameExtensions.Contains(Path.GetExtension(relative).ToLowerInvariant());

    internal static bool IsBelow(string folder, string target)
    {
        var relative = Path.GetRelativePath(folder, target);

        return relative != "." && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}

/// <summary>What the startup toast says when updates were found.</summary>
public static class UpdateToast
{
    public static BilingualText Text(int count) => count == 1
        ? new BilingualText("1 mod has an update. Click to see it.", "1 个模组有更新。点击查看。")
        : new BilingualText(count + " mods have updates. Click to see them.", count + " 个模组有更新。点击查看。");
}
