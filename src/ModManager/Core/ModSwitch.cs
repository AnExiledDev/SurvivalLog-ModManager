using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModManager.Core;

/// <summary>A loaded plugin as the on/off switch sees it: its DLL, and the plugins it cannot start without.</summary>
public sealed record PluginNode(string Guid, string Name, string File, IReadOnlyList<string> HardDependencies);

/// <summary>What the switch on a mod's page can do. A change always takes effect at the next game start.</summary>
public abstract record ModSwitch
{
    private ModSwitch()
    {
    }

    /// <param name="IsOnNextStart">Whether BepInEx will start the mod next time, which differs from now once the player flips it.</param>
    /// <param name="AlsoStops">Mods that cannot run without this one, or share its DLL, so they stop with it.</param>
    public sealed record Allowed(bool IsOnNextStart, IReadOnlyList<string> AlsoStops) : ModSwitch;

    /// <summary>Turning it off would stop the Mod Manager too, and with it the way to turn it back on.</summary>
    public sealed record Locked(bool IsModManager, IReadOnlyList<string> NeededBy) : ModSwitch;

    /// <summary>
    /// An update or rollback is replacing its files this session, so moving its DLL now could leave the old and new
    /// copies in different folders. The switch waits for the next game start.
    /// </summary>
    public sealed record Held(bool IsOnNextStart) : ModSwitch;

    /// <summary>Its DLL is not in the plugins folder or a folder for turned-off plugins, so there is nothing to move.</summary>
    public sealed record Unavailable : ModSwitch;
}

public static class SwitchPlanner
{
    public static ModSwitch ForLoaded(PluginNode target, IReadOnlyList<PluginNode> plugins, string selfGuid, bool isOnNextStart)
    {
        var stopsWith = StopsWith(target, plugins);
        var names = stopsWith.Select(plugin => plugin.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        if (target.Guid == selfGuid) return new ModSwitch.Locked(true, names);

        if (stopsWith.Any(plugin => plugin.Guid == selfGuid)) return new ModSwitch.Locked(false, names);

        return new ModSwitch.Allowed(isOnNextStart, names);
    }

    /// <summary>A switch that could move the DLL is held while an install runs or waits for the next start, or after a rollback.</summary>
    public static ModSwitch HoldWhileFilesChange(ModSwitch modSwitch, InstallState install, RollbackState rollback) =>
        modSwitch is ModSwitch.Allowed allowed && (install is InstallState.Installing or InstallState.Installed || rollback is RollbackState.Done)
            ? new ModSwitch.Held(allowed.IsOnNextStart)
            : modSwitch;

    /// <summary>Every other plugin that will not start without the target: those that need it (directly or through another), and those in the same DLL.</summary>
    public static IReadOnlyList<PluginNode> StopsWith(PluginNode target, IReadOnlyList<PluginNode> plugins)
    {
        var stopped = new List<PluginNode> { target };
        var isGrowing = true;

        while (isGrowing)
        {
            var next = plugins.Where(plugin => !stopped.Any(s => s.Guid == plugin.Guid) && stopped.Any(s => Needs(plugin, s))).ToList();

            stopped.AddRange(next);
            isGrowing = next.Count > 0;
        }

        return stopped.Skip(1).ToList();
    }

    private static bool Needs(PluginNode plugin, PluginNode stopped) =>
        plugin.HardDependencies.Contains(stopped.Guid) || string.Equals(plugin.File, stopped.File, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Where plugin DLLs live. BepInEx starts every DLL under the plugins folder, so turning a mod off moves its DLL to
/// the first folder for turned-off plugins, at the same place below it, and turning it on moves it back.
/// </summary>
public sealed record PluginFolders(string Plugins, IReadOnlyList<string> OffFolders)
{
    public bool IsOn(string path) => Below(Plugins, path) != null;

    public string? OffPath(string path) => Below(Plugins, path) is { } relative ? Path.Combine(OffFolders[0], relative) : null;

    public string? OnPath(string path) =>
        OffFolders.Select(folder => Below(folder, path)).FirstOrDefault(relative => relative != null) is { } relative ? Path.Combine(Plugins, relative) : null;

    private static string? Below(string folder, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(folder), Path.GetFullPath(path));

        var isOutside = relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative);

        return relative == "." || isOutside ? null : relative;
    }
}
