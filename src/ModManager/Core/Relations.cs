using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>How a setting tells the Mod Manager when a change takes effect, without referencing it.</summary>
public static class ApplyModes
{
    /// <summary>A plain string in a setting's ConfigDescription tags: the change takes effect at once.</summary>
    public const string LiveTag = "ModManager.Live";

    /// <summary>A plain string in a setting's ConfigDescription tags: the change takes effect at the next game start.</summary>
    public const string RestartTag = "ModManager.Restart";

    /// <summary>A tag on the setting wins; a setting the Toolkit's LiveSettings watches is live; otherwise the mod decides.</summary>
    public static ApplyMode ForSetting(IEnumerable<object?>? tags, bool isWatched)
    {
        var strings = (tags ?? Array.Empty<object?>()).OfType<string>().ToList();

        if (strings.Contains(RestartTag)) return ApplyMode.Restart;

        if (strings.Contains(LiveTag) || isWatched) return ApplyMode.Live;

        return ApplyMode.Unknown;
    }
}

public enum RelationKind
{
    /// <summary>A hard BepInDependency: BepInEx will not start the mod without it.</summary>
    Needs,

    /// <summary>A soft BepInDependency: used when present, fine without.</summary>
    WorksWith,

    /// <summary>A BepInIncompatibility: BepInEx will not start the mod while the other one is there.</summary>
    DoesNotWorkWith,
}

/// <summary>A relation as the plugin declares it, by the other plugin's GUID, with the version range a hard dependency asks for.</summary>
public sealed record DeclaredRelation(RelationKind Kind, string Guid, string? Range = null);

public enum RelationStatus
{
    /// <summary>Not installed, or not in a folder this screen knows.</summary>
    Missing,

    /// <summary>Will be started at the next game start.</summary>
    On,

    /// <summary>Installed but turned off for the next start.</summary>
    Off,

    /// <summary>Installed, but its version is outside the range the mod asks for, so BepInEx will not start the mod.</summary>
    WrongVersion,
}

/// <summary>A declared relation with the other mod's name and whether it will be there at the next start.</summary>
public sealed record Relation(RelationKind Kind, string Guid, string Name, RelationStatus Status, bool CanTurnOn, string? Range = null);

/// <summary>What still stands between a mod and starting: needed mods to turn on, ones not installed, and ones at the wrong version.</summary>
public sealed record NeededMods(IReadOnlyList<string> TurnOn, IReadOnlyList<string> Missing, IReadOnlyList<string> WrongVersion);

public static class RelationResolver
{
    /// <summary>Kept in declared order; a GUID declared twice under one kind shows once.</summary>
    /// <param name="isInRange">Whether a version is inside a version range, read the way BepInEx reads it.</param>
    public static IReadOnlyList<Relation> Resolve(IEnumerable<DeclaredRelation> declared, IReadOnlyList<ModEntry> mods, Func<string, string, bool> isInRange) =>
        declared
            .GroupBy(relation => (relation.Kind, relation.Guid))
            .Select(group => group.First())
            .Select(relation => Resolve(relation, mods.FirstOrDefault(mod => mod.Guid == relation.Guid), isInRange))
            .ToList();

    /// <summary>The other mods that need or can use this one, by name, each with whether it runs at the next start.</summary>
    public static IReadOnlyList<Relation> Dependents(string guid, IReadOnlyDictionary<string, IReadOnlyList<DeclaredRelation>> declaredBy, IReadOnlyList<ModEntry> mods) =>
        mods
            .Where(mod => mod.Guid != guid && declaredBy.ContainsKey(mod.Guid))
            .SelectMany(mod => declaredBy[mod.Guid]
                .Where(relation => relation.Guid == guid && relation.Kind is RelationKind.Needs or RelationKind.WorksWith)
                .Select(relation => relation.Kind)
                .Distinct()
                .Select(kind => new Relation(kind, mod.Guid, mod.DisplayName, mod.IsOnNextStart ? RelationStatus.On : RelationStatus.Off, false)))
            .OrderBy(relation => relation.Kind)
            .ThenBy(relation => relation.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The needed mods that are off and can be turned on here, and the names of the ones not installed or at the wrong version.</summary>
    public static NeededMods Needed(IReadOnlyList<Relation> relations)
    {
        var needs = relations.Where(relation => relation.Kind == RelationKind.Needs).ToList();

        return new NeededMods(
            needs.Where(relation => relation.Status == RelationStatus.Off && relation.CanTurnOn).Select(relation => relation.Guid).ToList(),
            needs.Where(relation => relation.Status == RelationStatus.Missing).Select(relation => relation.Name).ToList(),
            needs.Where(relation => relation.Status == RelationStatus.WrongVersion).Select(relation => relation.Name + " " + relation.Range).ToList());
    }

    private static Relation Resolve(DeclaredRelation relation, ModEntry? other, Func<string, string, bool> isInRange)
    {
        if (other == null) return new Relation(relation.Kind, relation.Guid, relation.Guid, RelationStatus.Missing, false, relation.Range);

        if (relation.Range != null && !isInRange(other.Version, relation.Range)) return new Relation(relation.Kind, relation.Guid, other.DisplayName, RelationStatus.WrongVersion, false, relation.Range);

        var status = other.IsOnNextStart ? RelationStatus.On : RelationStatus.Off;

        return new Relation(relation.Kind, relation.Guid, other.DisplayName, status, other.Switch is ModSwitch.Allowed, relation.Range);
    }
}

/// <summary>What the page says after "Turn on what it needs".</summary>
public static class NeedsNotes
{
    public static BilingualText TurnedOn(IReadOnlyList<string> turnedOn, IReadOnlyList<string> missing, IReadOnlyList<string> failed, IReadOnlyList<string> wrongVersion)
    {
        var english = new List<string>();
        var chinese = new List<string>();

        if (turnedOn.Count > 0)
        {
            english.Add("On from the next start: " + string.Join(", ", turnedOn) + ".");
            chinese.Add("下次启动时开启：" + string.Join("、", turnedOn) + "。");
        }

        if (failed.Count > 0)
        {
            english.Add("Could not turn on: " + string.Join(", ", failed) + ".");
            chinese.Add("无法开启：" + string.Join("、", failed) + "。");
        }

        if (missing.Count > 0)
        {
            english.Add("Not installed, so install these yourself: " + string.Join(", ", missing) + ".");
            chinese.Add("未安装，请自行安装：" + string.Join("、", missing) + "。");
        }

        if (wrongVersion.Count > 0)
        {
            english.Add("Wrong version, so install one that fits: " + string.Join(", ", wrongVersion) + ".");
            chinese.Add("版本不符，请安装符合要求的版本：" + string.Join("、", wrongVersion) + "。");
        }

        if (english.Count == 0) return new BilingualText("Everything it needs is already on.", "它需要的模组都已开启。");

        return new BilingualText(string.Join(" ", english), string.Join("", chinese));
    }
}
