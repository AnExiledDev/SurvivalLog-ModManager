using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>One DLL below the plugins folder, as the health check found it.</summary>
/// <param name="Path">Below the BepInEx folder, with "/" between folders, as the page shows it.</param>
/// <param name="Sha256">Null when the file could not be read.</param>
/// <param name="Guids">The plugins it declares; none for a library, or a DLL that could not be read.</param>
public sealed record FoundDll(string Path, string? Sha256, IReadOnlyList<string> Guids);

/// <summary>The install as it is on disk when the screen opens.</summary>
/// <param name="BepInExVersion">The running BepInEx's version, build suffix and all; empty when it could not be read.</param>
/// <param name="Folders">Every folder below plugins, below the BepInEx folder, with "/" between folders.</param>
/// <param name="SettingsFiles">The names of the .cfg files directly in BepInEx/config.</param>
/// <param name="SettingsInUse">The names of the .cfg files a running mod has open.</param>
/// <param name="OtherGuids">Plugins that are not in the list but still have a claim on a settings file: patchers, and mods in the trash.</param>
public sealed record InstallScan(
    string BepInExVersion,
    IReadOnlyList<FoundDll> Dlls,
    IReadOnlyList<string> Folders,
    IReadOnlyList<string> SettingsFiles,
    IReadOnlyList<string> SettingsInUse,
    IReadOnlyList<string> OtherGuids);

/// <summary>One thing wrong with how BepInEx and the mods are installed.</summary>
public abstract record HealthFinding
{
    private HealthFinding()
    {
    }

    /// <summary>A BepInEx build other than the one the mods are made and tested on.</summary>
    public sealed record OtherBepInEx(string Found) : HealthFinding;

    /// <summary>A mod that needs a newer Exiled's Toolkit than the one installed, so BepInEx will not start it.</summary>
    public sealed record OldToolkit(string Mod, string Range, string Installed) : HealthFinding;

    /// <summary>Two or more DLLs declare the same plugin, and BepInEx starts only one.</summary>
    public sealed record SameGuid(string Guid, IReadOnlyList<string> Files) : HealthFinding;

    /// <summary>One DLL in two places: the same file name, or the same bytes.</summary>
    public sealed record SameDll(IReadOnlyList<string> Files) : HealthFinding;

    /// <summary>A BepInEx or plugins folder inside plugins: a mod unpacked one folder too deep.</summary>
    public sealed record NestedFolder(string Folder) : HealthFinding;

    /// <summary>.cfg files named after no plugin this screen knows of: most likely left by removed mods.</summary>
    public sealed record UnusedSettings(IReadOnlyList<string> Files) : HealthFinding;
}

public static class HealthCheck
{
    /// <summary>The BepInEx build every mod here is made and tested on, for IL2CPP games.</summary>
    public const string TestedBepInEx = "6.0.0-be.788";

    private static readonly string[] MisplacedFolders = { "BepInEx", "plugins" };

    /// <summary>Everything found, the ones that keep mods from starting first.</summary>
    public static IReadOnlyList<HealthFinding> Find(InstallScan scan, IReadOnlyList<ModEntry> mods, string toolkitGuid)
    {
        var found = new List<HealthFinding>();

        if (BepInExFinding(scan.BepInExVersion) is { } bepInEx) found.Add(bepInEx);

        found.AddRange(OldToolkits(mods, toolkitGuid));

        var sameDll = SameDlls(scan.Dlls);

        found.AddRange(SameGuids(scan.Dlls, sameDll));
        found.AddRange(sameDll);
        found.AddRange(Nested(scan.Folders));

        var unused = UnusedSettings(scan, mods);

        if (unused.Count > 0) found.Add(new HealthFinding.UnusedSettings(unused));

        return found;
    }

    /// <summary>Null for the tested build, or for a version that could not be read; the build suffix after "+" does not count.</summary>
    private static HealthFinding? BepInExFinding(string version)
    {
        var plain = version.Split('+')[0].Trim();

        return plain.Length == 0 || plain == TestedBepInEx ? null : new HealthFinding.OtherBepInEx(plain);
    }

    /// <summary>
    /// A Toolkit that is missing or off cannot be seen here: Mod Manager needs it to start, and its switch is locked.
    /// So what is left to find is a Toolkit older than a mod asks for.
    /// </summary>
    private static IEnumerable<HealthFinding> OldToolkits(IReadOnlyList<ModEntry> mods, string toolkitGuid)
    {
        var installed = mods.FirstOrDefault(mod => mod.Guid == toolkitGuid)?.Version;

        if (installed == null) return Array.Empty<HealthFinding>();

        return mods
            .Where(mod => mod.Guid != toolkitGuid && mod.State is not ModState.Off)
            .SelectMany(mod => mod.Relations
                .Where(relation => relation.Kind == RelationKind.Needs && relation.Guid == toolkitGuid && relation.Status == RelationStatus.WrongVersion)
                .Select(relation => (HealthFinding)new HealthFinding.OldToolkit(mod.DisplayName, relation.Range ?? "", installed)))
            .ToList();
    }

    /// <summary>Groups of DLLs with the same file name or the same bytes; a file in two groups joins them into one.</summary>
    private static List<HealthFinding.SameDll> SameDlls(IReadOnlyList<FoundDll> dlls)
    {
        var groups = new List<HashSet<string>>();

        void Join(IEnumerable<string> paths)
        {
            var group = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);

            if (group.Count < 2) return;

            foreach (var other in groups.Where(other => other.Overlaps(group)).ToList())
            {
                group.UnionWith(other);
                groups.Remove(other);
            }

            groups.Add(group);
        }

        foreach (var byName in dlls.GroupBy(dll => FileName(dll.Path), StringComparer.OrdinalIgnoreCase)) Join(byName.Select(dll => dll.Path));

        foreach (var byBytes in dlls.Where(dll => dll.Sha256 != null).GroupBy(dll => dll.Sha256)) Join(byBytes.Select(dll => dll.Path));

        return groups.Select(group => new HealthFinding.SameDll(Sorted(group))).OrderBy(group => group.Files[0], StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>A GUID in more than one DLL, unless those DLLs are already one of the copies found as the same DLL.</summary>
    private static IEnumerable<HealthFinding> SameGuids(IReadOnlyList<FoundDll> dlls, IReadOnlyList<HealthFinding.SameDll> sameDll) =>
        dlls
            .SelectMany(dll => dll.Guids.Distinct().Select(guid => (Guid: guid, dll.Path)))
            .GroupBy(pair => pair.Guid, StringComparer.Ordinal)
            .Select(group => (group.Key, Files: Sorted(group.Select(pair => pair.Path))))
            .Where(group => group.Files.Count > 1 && !sameDll.Any(copies => group.Files.All(file => copies.Files.Contains(file, StringComparer.OrdinalIgnoreCase))))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (HealthFinding)new HealthFinding.SameGuid(group.Key, group.Files));

    /// <summary>The outermost BepInEx or plugins folder in each branch below plugins; the ones inside it go with it.</summary>
    private static IEnumerable<HealthFinding> Nested(IReadOnlyList<string> folders)
    {
        var shown = new List<string>();

        foreach (var folder in Sorted(folders))
        {
            if (!MisplacedFolders.Contains(FileName(folder), StringComparer.OrdinalIgnoreCase)) continue;
            if (shown.Any(outer => folder.StartsWith(outer + "/", StringComparison.OrdinalIgnoreCase))) continue;

            shown.Add(folder);
        }

        return shown.Select(folder => (HealthFinding)new HealthFinding.NestedFolder(folder));
    }

    /// <summary>
    /// A .cfg is in use when it is open, or named after a known plugin (a mod may add more files that start with its
    /// GUID). BepInEx.cfg is BepInEx's own.
    /// </summary>
    private static IReadOnlyList<string> UnusedSettings(InstallScan scan, IReadOnlyList<ModEntry> mods)
    {
        var known = mods.Select(mod => mod.Guid).Concat(scan.Dlls.SelectMany(dll => dll.Guids)).Concat(scan.OtherGuids).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var open = scan.SettingsInUse.ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool IsKnown(string file)
        {
            var name = file.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) ? file.Substring(0, file.Length - 4) : file;

            return known.Any(guid => name.Equals(guid, StringComparison.OrdinalIgnoreCase) || name.StartsWith(guid + ".", StringComparison.OrdinalIgnoreCase));
        }

        return Sorted(scan.SettingsFiles.Where(file => !file.Equals("BepInEx.cfg", StringComparison.OrdinalIgnoreCase) && !open.Contains(file) && !IsKnown(file)));
    }

    private static string FileName(string path) => path.Substring(path.LastIndexOf('/') + 1);

    private static IReadOnlyList<string> Sorted(IEnumerable<string> paths) => paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
}
