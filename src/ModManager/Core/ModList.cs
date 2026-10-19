using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>Names one setting across the page and the game: the plugin's GUID, then the setting's section and key.</summary>
public sealed record SettingId(string Guid, string Section, string Key)
{
    public override string ToString() => Guid + "/" + Section + "/" + Key;
}

/// <summary>
/// What a mod can say about how its settings are shown, read from the attribute object that ConfigurationManager
/// popularised (in the setting's tags). A hidden setting is left out; the rest are display only.
/// </summary>
public sealed record Hints(bool IsHidden, string? DisplayName, int Order, bool IsReadOnly)
{
    public static readonly Hints None = new(false, null, 0, false);
}

/// <summary>A setting as read from a plugin's config, values written as in the .cfg file.</summary>
/// <param name="Apply">What the setting says for itself about when a change takes effect; Unknown leaves it to the mod.</param>
public sealed record FoundSetting(string Section, string Key, string Description, SettingKind Kind, string Default, string Current, Hints Hints, ApplyMode Apply = ApplyMode.Unknown);

/// <summary>A setting ready to show. <see cref="AtStart"/> is its value when this game session first saw it.</summary>
public sealed record Setting(SettingId Id, string Label, Description Description, SettingKind Kind, string Default, string Current, string AtStart, bool IsReadOnly)
{
    public ApplyMode Apply { get; init; } = ApplyMode.Unknown;

    public bool IsChanged => Current != Default;

    public bool IsChangedThisSession => Current != AtStart;
}

public abstract record ModState
{
    private ModState()
    {
    }

    public sealed record Loaded(IReadOnlyList<Setting> Settings) : ModState;

    /// <summary>BepInEx found the plugin but never started it (a load error); its settings were never read.</summary>
    public sealed record NotLoaded : ModState;

    /// <summary>Its DLL was in a folder for turned-off plugins when the game started, so BepInEx never saw it.</summary>
    public sealed record Off : ModState;
}

public sealed record ModEntry(string Guid, string Name, string Version, string FileName, ModState State, ModInfo Info, ModSwitch Switch, UpdateState Update)
{
    /// <summary>The mods it needs, works with, or does not work with, and whether each will be there at the next start.</summary>
    public IReadOnlyList<Relation> Relations { get; init; } = Array.Empty<Relation>();

    public InstallState Install { get; init; } = new InstallState.None();

    /// <summary>The mod names the game versions it was made for, and the running game is none of them.</summary>
    public bool IsForOtherGame { get; init; }

    /// <summary>The name the mod asks to be shown by, else the one its plugin declares.</summary>
    public string DisplayName => Info.Name?.English ?? Name;

    /// <summary>Will BepInEx start it at the next game start, as far as this screen can tell.</summary>
    public bool IsOnNextStart => Switch switch
    {
        ModSwitch.Allowed allowed => allowed.IsOnNextStart,
        ModSwitch.Locked => true,
        _ => State is not ModState.Off,
    };
}

/// <summary>Everything the Mods screen shows when it opens.</summary>
public sealed record ModsView(IReadOnlyList<ModEntry> Mods, IReadOnlyList<string> Problems, bool IsChinese, string OpenKey)
{
    /// <summary>The running game's version, or null when it could not be read.</summary>
    public string? GameVersion { get; init; }
}

/// <summary>
/// Remembers each setting's value from the first time this game session read it, so the page can say what changed
/// since the game started (a change that may need a restart to take effect).
/// </summary>
public sealed class StartValues
{
    private readonly Dictionary<string, string> _values = new();

    public string Remember(SettingId id, string current)
    {
        var key = id.ToString();

        if (_values.TryGetValue(key, out var atStart)) return atStart;

        _values[key] = current;

        return current;
    }
}

public static class ModList
{
    /// <summary>Hidden settings dropped; sections kept in the order the mod made them, settings sorted by their Order hint (higher first), ties in the mod's order.</summary>
    /// <param name="modApply">How the mod says its settings apply, for settings that say nothing for themselves.</param>
    public static IReadOnlyList<Setting> Arrange(string guid, IEnumerable<FoundSetting> found, StartValues startValues, ApplyMode modApply = ApplyMode.Unknown)
    {
        var visible = found.Where(setting => !setting.Hints.IsHidden).ToList();
        var sections = visible.Select(setting => setting.Section).Distinct().ToList();

        return visible
            .Select((setting, index) => (setting, index))
            .OrderBy(pair => sections.IndexOf(pair.setting.Section))
            .ThenByDescending(pair => pair.setting.Hints.Order)
            .ThenBy(pair => pair.index)
            .Select(pair => Show(guid, pair.setting, startValues, modApply))
            .ToList();
    }

    public static IReadOnlyList<ModEntry> SortByName(IEnumerable<ModEntry> mods) =>
        mods.OrderBy(mod => mod.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(mod => mod.Guid, StringComparer.Ordinal).ToList();

    private static Setting Show(string guid, FoundSetting found, StartValues startValues, ApplyMode modApply)
    {
        var id = new SettingId(guid, found.Section, found.Key);
        var label = found.Hints.DisplayName is { Length: > 0 } name && name.Trim().Length > 0 ? name : Wording.LabelFromKey(found.Key);

        return new Setting(id, label, Wording.SplitDescription(found.Description), found.Kind, found.Default, found.Current, startValues.Remember(id, found.Current), found.Hints.IsReadOnly)
        {
            Apply = found.Apply != ApplyMode.Unknown ? found.Apply : modApply,
        };
    }
}
