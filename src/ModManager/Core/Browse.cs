using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ModManager.Core;

/// <summary>How a mod in BROWSE can be had.</summary>
public enum BrowseSupport
{
    Installed,

    /// <summary>The Mod Manager can install it: the wiki keeps a copy, or the wiki lists its author's GitHub releases.</summary>
    Installable,

    /// <summary>Its author has not set it up for the Mod Manager, so it is downloaded from Nexus by hand.</summary>
    Manual,
}

/// <summary>Whether BROWSE has its list.</summary>
public enum BrowseFeed
{
    /// <summary>Update checks are off, so neither Nexus nor the wiki is asked.</summary>
    Off,

    /// <summary>Nexus or the wiki is being asked now.</summary>
    Asking,

    Ready,
}

/// <summary>One mod in BROWSE, from the Nexus list and the wiki's index together.</summary>
/// <param name="Key">The mod's name on the page: its GUID when known, else its Nexus page number.</param>
/// <param name="Guid">Null for a mod known only from Nexus.</param>
public sealed record BrowseMod(string Key, string Name, string Summary, string Author, string Version, string UpdatedAt, int Endorsements, int Downloads, string? Thumbnail, int? NexusId, string? Guid, BrowseSupport Support)
{
    public InstallState Install { get; init; } = new InstallState.None();

    /// <summary>Mods this one needs that are not installed and cannot be installed from here, by name.</summary>
    public IReadOnlyList<BrowseNeed> Needs { get; init; } = Array.Empty<BrowseNeed>();

    /// <summary>Installed by BROWSE this session, so UNDO can take it out again before the game ever loads it.</summary>
    public bool IsFresh { get; init; }
}

/// <param name="NexusId">The Nexus page of the needed mod, when one is known.</param>
public sealed record BrowseNeed(string Name, int? NexusId);

/// <summary>Where a fresh install comes from.</summary>
/// <param name="WikiCopy">The wiki's hash-checked copy of the newest release; null for a mod installed from its own GitHub releases.</param>
/// <param name="GitHub">The repository, as "owner/name", of a mod the wiki lists but keeps no copy of.</param>
public sealed record FreshTarget(string Guid, string Name, string Dll, IndexRelease? WikiCopy, string? GitHub);

/// <summary>A mod BROWSE installed this session: the files it wrote, and the mods its DLL needs.</summary>
public sealed record FreshInstall(IReadOnlyList<string> Files, IReadOnlyList<string> Needs);

public static class Browse
{
    private static readonly Regex WikiZip = new("^([A-Za-z0-9_]+)-\\d+\\.\\d+\\.\\d+\\.zip$", RegexOptions.CultureInvariant);

    /// <summary>What the mod installs from, or null when the Mod Manager cannot install it.</summary>
    public static FreshTarget? TargetOf(ModIndex index, string guid)
    {
        if (index.ModOf(guid) is { } mod)
        {
            return ModIndex.Newest(mod) is { } newest && WikiZip.Match(newest.File) is { Success: true } match
                ? new FreshTarget(mod.Guid, mod.Name, match.Groups[1].Value + ".dll", newest, null)
                : null;
        }

        return index.ListedOf(guid) is { } listed ? new FreshTarget(listed.Guid, listed.Name, listed.Dll, null, listed.GitHub) : null;
    }

    /// <summary>
    /// Every mod on Nexus for the game, then the installable ones Nexus does not list, each once. A Nexus mod is
    /// matched to the index by its page number, so the wiki's mods and the listed ones get their GUID.
    /// </summary>
    /// <param name="installedGuids">Every mod in the plugins folder, on or off.</param>
    /// <param name="installedNexusIds">The Nexus pages the installed mods were matched to.</param>
    public static IReadOnlyList<BrowseMod> List(NexusCache nexus, ModIndex index, IReadOnlySet<string> installedGuids, IReadOnlySet<int> installedNexusIds, Func<string, InstallState> installOf, Func<string, FreshInstall?> freshOf)
    {
        var guidByNexus = new Dictionary<int, string>();

        foreach (var mod in index.Mods.Where(mod => mod.NexusId != null)) guidByNexus.TryAdd(mod.NexusId!.Value, mod.Guid);

        foreach (var listed in index.Listed.Where(listed => listed.NexusId != null)) guidByNexus.TryAdd(listed.NexusId!.Value, listed.Guid);

        var list = new List<BrowseMod>();
        var seenGuids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mod in nexus.Mods.GroupBy(mod => mod.ModId).Select(group => group.First()))
        {
            var guid = guidByNexus.TryGetValue(mod.ModId, out var known) ? known : null;

            if (guid != null) seenGuids.Add(guid);

            var support = guid != null && installedGuids.Contains(guid) || installedNexusIds.Contains(mod.ModId) ? BrowseSupport.Installed
                : guid != null && TargetOf(index, guid) != null ? BrowseSupport.Installable
                : BrowseSupport.Manual;

            list.Add(With(new BrowseMod(guid ?? "nexus:" + mod.ModId, mod.Name, mod.Summary, mod.Uploader, mod.Version, mod.UpdatedAt, mod.Endorsements, mod.Downloads, mod.Thumbnail, mod.ModId, guid, support), index, installedGuids, installOf, freshOf));
        }

        var unlisted = index.Mods.Select(mod => (mod.Guid, mod.Name, mod.NexusId, Version: ModIndex.Newest(mod)?.Version ?? ""))
            .Concat(index.Listed.Select(listed => (listed.Guid, listed.Name, listed.NexusId, Version: "")))
            .Where(mod => seenGuids.Add(mod.Guid) && TargetOf(index, mod.Guid) != null);

        foreach (var mod in unlisted)
        {
            var support = installedGuids.Contains(mod.Guid) ? BrowseSupport.Installed : BrowseSupport.Installable;

            list.Add(With(new BrowseMod(mod.Guid, mod.Name, "", "", mod.Version, "", 0, 0, null, mod.NexusId, mod.Guid, support), index, installedGuids, installOf, freshOf));
        }

        return list;
    }

    private static BrowseMod With(BrowseMod mod, ModIndex index, IReadOnlySet<string> installedGuids, Func<string, InstallState> installOf, Func<string, FreshInstall?> freshOf)
    {
        // An installed mod's own updates show on its card in the Mods list, not here.
        if (mod.Guid == null || mod.Support == BrowseSupport.Installed) return mod;

        var fresh = freshOf(mod.Guid);
        IReadOnlyList<BrowseNeed> needs = fresh == null ? Array.Empty<BrowseNeed>() : MissingNeeds(fresh.Needs, installedGuids, index, installOf).Where(need => TargetOf(index, need) == null).Select(need => NeedOf(need, index)).ToList();

        return mod with { Install = installOf(mod.Guid), IsFresh = fresh != null, Needs = needs };
    }

    /// <summary>The needed mods that are neither installed nor being installed or installed this session.</summary>
    public static IReadOnlyList<string> MissingNeeds(IEnumerable<string> needs, IReadOnlySet<string> installedGuids, ModIndex index, Func<string, InstallState> installOf) =>
        needs.Distinct(StringComparer.Ordinal).Where(guid => !installedGuids.Contains(guid) && installOf(guid) is not (InstallState.Installing or InstallState.Installed or InstallState.NeedsApproval)).ToList();

    private static BrowseNeed NeedOf(string guid, ModIndex index) =>
        new(index.ModOf(guid)?.Name ?? index.ListedOf(guid)?.Name ?? guid, index.NexusIdOf(guid));

    /// <summary>
    /// The checks on a mod's DLL before it is first installed. It must be the mod the index names. The wiki's own copy
    /// is checked against the index's hash and goes in at once; a mod from its author's GitHub is shown to the player
    /// with everything its code can do first, unless it can do none of those things.
    /// </summary>
    public static UpdateVerdict Judge(string guid, DllScan scan, bool isWikiCopy)
    {
        switch (scan)
        {
            case DllScan.Unreadable unreadable:
                return new UpdateVerdict.Rejected("its DLL could not be read (" + unreadable.Reason + ")");
            case DllScan.Read read when !read.PluginGuids.Contains(guid, StringComparer.Ordinal):
                return new UpdateVerdict.Rejected(read.PluginGuids.Count == 0 ? "its DLL holds no BepInEx plugin" : "its DLL is a different mod (" + string.Join(", ", read.PluginGuids) + ")");
        }

        var powers = scan.Powers.Distinct().OrderBy(power => power).ToList();

        return isWikiCopy || powers.Count == 0 ? new UpdateVerdict.Clear() : new UpdateVerdict.NeedsApproval(powers);
    }
}

/// <summary>The request a player sends a mod's author, asking for Mod Manager support.</summary>
public static class AskAuthor
{
    public const string KitUrl = "https://github.com/AnExiledDev/SurvivalLog-ModKit";
    public const string ModManagerUrl = "https://www.nexusmods.com/survivallog/mods/73";

    /// <summary>English, then Chinese, in one message, as the author may read either.</summary>
    public static string Message(string modName) =>
        "Hi! Could you add Mod Manager support to " + modName + "? Then players can install it and get its updates from the in-game Mods screen, without a manual download. " +
        "It takes a few lines in the project file and a release zip on GitHub; the steps are here: " + KitUrl + " (Mod Manager: " + ModManagerUrl + "). Thank you for the mod!\n\n" +
        "你好！能否为「" + modName + "」加入 Mod Manager 支持？这样玩家就能在游戏内的模组界面里安装和更新它，不用手动下载。" +
        "只需要在项目文件里加几行，并在 GitHub 上发布一个 zip；步骤见：" + KitUrl + "（Mod Manager：" + ModManagerUrl + "）。感谢制作这个模组！";
}
