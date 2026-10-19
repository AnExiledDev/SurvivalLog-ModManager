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
    private readonly PluginFolders _folders;
    private readonly UpdateBook _updates;
    private readonly string _selfGuid;
    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();
    private readonly Dictionary<string, IReadOnlyList<PluginFile>> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where each DLL moved this session, by where it was when the game started.</summary>
    private readonly Dictionary<string, string> _moves = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<(string Path, PluginFile Plugin)>? _offAtStart;

    public PluginCatalog(PluginFolders folders, UpdateBook updates, string selfGuid, Action<string> log)
    {
        _folders = folders;
        _updates = updates;
        _selfGuid = selfGuid;
        _log = log;
    }

    public IReadOnlyList<ModEntry> Mods()
    {
        var nodes = Nodes();

        return ModList.SortByName(Plugins().Select(plugin => Entry(plugin, nodes)).Concat(OffAtStart().Select(OffEntry)));
    }

    public IReadOnlyList<string> Problems() => IL2CPPChainloader.Instance.DependencyErrors.ToList();

    public ModEntry? Mod(string guid)
    {
        if (Find(guid) is { } plugin) return Entry(plugin, Nodes());

        return OffAtStart().Where(off => off.Plugin.Guid == guid).Select(OffEntry).FirstOrDefault();
    }

    public ModLink? Link(string guid, LinkKind kind) => Mod(guid)?.Info.Link(kind);

    /// <summary>Moves the mod's DLL into or out of the plugins folder; BepInEx reads the folder at the next start.</summary>
    public ModNote Switch(string guid, bool isOn)
    {
        var mod = Mod(guid);
        var startPath = StartPathOf(guid);

        if (mod?.Switch is not ModSwitch.Allowed allowed || startPath == null) return new ModNote(guid, true, SwitchNotes.CannotSwitch());

        var current = CurrentPath(startPath);
        var target = isOn == _folders.IsOn(current) ? null : isOn ? _folders.OnPath(current) : _folders.OffPath(current);

        if (target != null)
        {
            if (File.Exists(target)) return new ModNote(guid, true, SwitchNotes.TargetTaken(target));

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
                File.Move(current, target);
                _moves[startPath] = target;
                _log((isOn ? "turned on " : "turned off ") + guid + " from the next start: moved " + current + " to " + target);
            }
            catch (Exception ex)
            {
                _log("could not move " + current + " to " + target + ": " + ex.Message);

                return new ModNote(guid, true, SwitchNotes.MoveFailed(ex.Message));
            }
        }

        return new ModNote(guid, false, SwitchNotes.Moved(mod.State is not ModState.Off, isOn, allowed.AlsoStops));
    }

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

    private static IReadOnlyList<PluginNode> Nodes() =>
        Plugins().Select(plugin => new PluginNode(
            plugin.Metadata.GUID,
            plugin.Metadata.Name,
            plugin.Location ?? "",
            plugin.Dependencies.Where(dependency => (dependency.Flags & BepInDependency.DependencyFlags.HardDependency) != 0).Select(dependency => dependency.DependencyGUID).ToList()))
            .ToList();

    private ModEntry Entry(PluginInfo plugin, IReadOnlyList<PluginNode> nodes)
    {
        var guid = plugin.Metadata.GUID;
        var location = plugin.Location;
        ModState state = plugin.Instance is BasePlugin loaded ? new ModState.Loaded(ModList.Arrange(guid, Found(guid, loaded.Config), _startValues)) : new ModState.NotLoaded();
        ModSwitch modSwitch = location != null && _folders.IsOn(location) && nodes.FirstOrDefault(node => node.Guid == guid) is { } node
            ? SwitchPlanner.ForLoaded(node, nodes, _selfGuid, _folders.IsOn(CurrentPath(location)))
            : new ModSwitch.Unavailable();

        var version = plugin.Metadata.Version.ToString();
        var info = InfoOf(guid, location);

        return new ModEntry(guid, plugin.Metadata.Name, version, Path.GetFileName(location ?? ""), state, info, modSwitch, _updates.StateOf(info.GitHubRepository, version));
    }

    private ModEntry OffEntry((string Path, PluginFile Plugin) off)
    {
        var plugin = off.Plugin;
        var modSwitch = new ModSwitch.Allowed(_folders.IsOn(CurrentPath(off.Path)), Array.Empty<string>());

        var info = Info(plugin);

        return new ModEntry(plugin.Guid, plugin.Name, plugin.Version, Path.GetFileName(off.Path), new ModState.Off(), info, modSwitch, _updates.StateOf(info.GitHubRepository, plugin.Version));
    }

    /// <summary>Plugins found in the folders for turned-off plugins, read once: the list stays as it was at the start, while their files may move.</summary>
    private IReadOnlyList<(string Path, PluginFile Plugin)> OffAtStart()
    {
        if (_offAtStart != null) return _offAtStart;

        var found = new List<(string Path, PluginFile Plugin)>();

        foreach (var folder in _folders.OffFolders.Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.dll", SearchOption.AllDirectories))
            {
                found.AddRange(FilesOf(path).Select(plugin => (path, plugin)));
            }
        }

        var loaded = Plugins().Select(plugin => plugin.Metadata.GUID).ToHashSet();

        _offAtStart = found.Where(off => !loaded.Contains(off.Plugin.Guid)).GroupBy(off => off.Plugin.Guid).Select(group => group.First()).ToList();

        return _offAtStart;
    }

    private string? StartPathOf(string guid) => Find(guid)?.Location ?? OffAtStart().Where(off => off.Plugin.Guid == guid).Select(off => off.Path).FirstOrDefault();

    private string CurrentPath(string startPath) => _moves.TryGetValue(startPath, out var moved) ? moved : startPath;

    private ModInfo InfoOf(string guid, string? location) =>
        location != null && FilesOf(location).FirstOrDefault(plugin => plugin.Guid == guid) is { } plugin ? Info(plugin) : ModInfo.None;

    private ModInfo Info(PluginFile plugin)
    {
        var read = ModInfoReader.Read(plugin.Metadata);

        foreach (var ignored in read.Ignored) ReportOnce(plugin.Guid + "/info/" + ignored, "ignored part of the Mods screen info of " + plugin.Guid + ": " + ignored);

        return read.Info;
    }

    /// <summary>Read once per DLL, by where it was at the start; a DLL that can't be read just has no info.</summary>
    private IReadOnlyList<PluginFile> FilesOf(string path)
    {
        if (_files.TryGetValue(path, out var known)) return known;

        try
        {
            known = PluginFileReader.Read(CurrentPath(path));
        }
        catch (Exception ex)
        {
            ReportOnce(path, "could not read " + path + ": " + ex.Message);
            known = Array.Empty<PluginFile>();
        }

        _files[path] = known;

        return known;
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
