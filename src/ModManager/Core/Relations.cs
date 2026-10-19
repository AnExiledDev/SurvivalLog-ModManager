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

/// <summary>A relation as the plugin declares it, by the other plugin's GUID.</summary>
public sealed record DeclaredRelation(RelationKind Kind, string Guid);

public enum RelationStatus
{
    /// <summary>Not installed, or not in a folder this screen knows.</summary>
    Missing,

    /// <summary>Will be started at the next game start.</summary>
    On,

    /// <summary>Installed but turned off for the next start.</summary>
    Off,
}

/// <summary>A declared relation with the other mod's name and whether it will be there at the next start.</summary>
public sealed record Relation(RelationKind Kind, string Guid, string Name, RelationStatus Status, bool CanTurnOn);

public static class RelationResolver
{
    /// <summary>Kept in declared order; a GUID declared twice under one kind shows once.</summary>
    public static IReadOnlyList<Relation> Resolve(IEnumerable<DeclaredRelation> declared, IReadOnlyList<ModEntry> mods) =>
        declared
            .GroupBy(relation => (relation.Kind, relation.Guid))
            .Select(group => group.First())
            .Select(relation => Resolve(relation, mods.FirstOrDefault(mod => mod.Guid == relation.Guid)))
            .ToList();

    /// <summary>The needed mods that are off and can be turned on here, and the names of the ones that are not installed.</summary>
    public static (IReadOnlyList<string> TurnOn, IReadOnlyList<string> Missing) Needed(IReadOnlyList<Relation> relations)
    {
        var needs = relations.Where(relation => relation.Kind == RelationKind.Needs).ToList();

        return (
            needs.Where(relation => relation.Status == RelationStatus.Off && relation.CanTurnOn).Select(relation => relation.Guid).ToList(),
            needs.Where(relation => relation.Status == RelationStatus.Missing).Select(relation => relation.Name).ToList());
    }

    private static Relation Resolve(DeclaredRelation relation, ModEntry? other)
    {
        if (other == null) return new Relation(relation.Kind, relation.Guid, relation.Guid, RelationStatus.Missing, false);

        var status = other.IsOnNextStart ? RelationStatus.On : RelationStatus.Off;

        return new Relation(relation.Kind, relation.Guid, other.DisplayName, status, other.Switch is ModSwitch.Allowed);
    }
}

/// <summary>What the page says after "Turn on what it needs".</summary>
public static class NeedsNotes
{
    public static BilingualText TurnedOn(IReadOnlyList<string> turnedOn, IReadOnlyList<string> missing, IReadOnlyList<string> failed)
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

        if (english.Count == 0) return new BilingualText("Everything it needs is already on.", "它需要的模组都已开启。");

        return new BilingualText(string.Join(" ", english), string.Join("", chinese));
    }
}
