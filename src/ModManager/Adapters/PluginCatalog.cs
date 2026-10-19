using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ModManager.Core;
using UnityEngine;

namespace ModManager.Adapters;

/// <summary>
/// Every plugin BepInEx loaded, read fresh from the chainloader and each plugin's own ConfigFile whenever asked, so
/// a setting a mod binds late, or a value changed in the .cfg, shows the next time the screen opens.
/// </summary>
internal sealed class PluginCatalog
{
    private static readonly string[] KeyNames = Enum.GetNames(typeof(KeyCode));

    private readonly StartValues _startValues = new();
    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();

    public PluginCatalog(Action<string> log)
    {
        _log = log;
    }

    public IReadOnlyList<ModEntry> Mods() => ModList.SortByName(Plugins().Select(Entry));

    public IReadOnlyList<string> Problems() => IL2CPPChainloader.Instance.DependencyErrors.ToList();

    public ModEntry? Mod(string guid) => Find(guid) is { } plugin ? Entry(plugin) : null;

    public ConfigFile? ConfigOf(string guid) => Find(guid)?.Instance is BasePlugin plugin ? plugin.Config : null;

    public ConfigEntryBase? EntryOf(SettingId id)
    {
        var config = ConfigOf(id.Guid);

        if (config == null) return null;

        IDictionary<ConfigDefinition, ConfigEntryBase> entries = config;

        return entries.TryGetValue(new ConfigDefinition(id.Section, id.Key), out var entry) ? entry : null;
    }

    public Setting? Setting(SettingId id) =>
        Mod(id.Guid)?.State is ModState.Loaded loaded ? loaded.Settings.FirstOrDefault(setting => setting.Id == id) : null;

    private static IEnumerable<PluginInfo> Plugins() => IL2CPPChainloader.Instance.Plugins.Values;

    private static PluginInfo? Find(string guid) => IL2CPPChainloader.Instance.Plugins.TryGetValue(guid, out var plugin) ? plugin : null;

    private ModEntry Entry(PluginInfo plugin)
    {
        var guid = plugin.Metadata.GUID;
        ModState state = plugin.Instance is BasePlugin loaded ? new ModState.Loaded(ModList.Arrange(guid, Found(guid, loaded.Config), _startValues)) : new ModState.NotLoaded();

        return new ModEntry(guid, plugin.Metadata.Name, plugin.Metadata.Version.ToString(), Path.GetFileName(plugin.Location ?? ""), state);
    }

    /// <summary>One setting that can't be read is skipped (and logged once) rather than hiding the whole mod.</summary>
    private IEnumerable<FoundSetting> Found(string guid, ConfigFile config)
    {
        var found = new List<FoundSetting>();

        foreach (var entry in config.Values)
        {
            try
            {
                found.Add(Read(entry));
            }
            catch (Exception ex)
            {
                ReportOnce(guid + "/" + entry.Definition, "could not read the setting " + entry.Definition + " of " + guid + ", so it is not shown: " + ex.Message);
            }
        }

        return found;
    }

    private static FoundSetting Read(ConfigEntryBase entry)
    {
        var description = entry.Description;
        var kind = SettingKinds.Classify(entry.SettingType, AcceptableOf(description?.AcceptableValues, entry.SettingType), KeyNames);
        var defaultValue = TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType);

        return new FoundSetting(entry.Definition.Section, entry.Definition.Key, description?.Description ?? "", kind, defaultValue, entry.GetSerializedValue(), HintsOf(description?.Tags));
    }

    /// <summary>AcceptableValueRange and AcceptableValueList are generic, so their values are read by name.</summary>
    private static Acceptable AcceptableOf(AcceptableValueBase? acceptable, Type settingType)
    {
        if (acceptable == null) return new Acceptable.Any();

        var type = acceptable.GetType();

        if (type.GetProperty("MinValue")?.GetValue(acceptable) is { } min && type.GetProperty("MaxValue")?.GetValue(acceptable) is { } max)
        {
            return new Acceptable.Range(Convert.ToDouble(min, CultureInfo.InvariantCulture), Convert.ToDouble(max, CultureInfo.InvariantCulture));
        }

        if (type.GetProperty("AcceptableValues")?.GetValue(acceptable) is IEnumerable values)
        {
            return new Acceptable.Values(values.Cast<object>().Select(value => TomlTypeConverter.ConvertToString(value, settingType)).ToList());
        }

        return new Acceptable.Any();
    }

    /// <summary>Mods copy ConfigurationManagerAttributes into their own assembly, so it is found by its members, not its type.</summary>
    private static Hints HintsOf(object[]? tags)
    {
        var attributes = tags?.FirstOrDefault(tag => tag != null && tag.GetType().Name.Contains("ConfigurationManagerAttributes"));

        if (attributes == null) return Hints.None;

        return new Hints(
            IsHidden: Member(attributes, "Browsable") is false,
            DisplayName: Member(attributes, "DispName") as string,
            Order: Member(attributes, "Order") is int order ? order : 0,
            IsReadOnly: Member(attributes, "ReadOnly") is true);
    }

    private static object? Member(object owner, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = owner.GetType();

        return type.GetField(name, flags)?.GetValue(owner) ?? type.GetProperty(name, flags)?.GetValue(owner);
    }

    private void ReportOnce(string key, string message)
    {
        if (_reportedFailures.Add(key)) _log(message);
    }
}
