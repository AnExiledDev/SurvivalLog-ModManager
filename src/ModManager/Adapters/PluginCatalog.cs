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
using ExiledToolkit;
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
    private readonly Func<string, IReadOnlyList<string>> _backupVersions;
    private readonly ProblemBook _errors;
    private readonly string _selfGuid;
    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();
    private readonly Dictionary<string, IReadOnlyList<PluginFile>> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where each DLL moved this session, by where it was when the game started.</summary>
    private readonly Dictionary<string, string> _moves = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<(string Path, PluginFile Plugin)>? _offAtStart;
    private IReadOnlyDictionary<string, string>? _icons;
    private IReadOnlyDictionary<string, string>? _banners;

    /// <param name="backupVersions">The names of a mod's backup folders, by GUID.</param>
    public PluginCatalog(PluginFolders folders, UpdateBook updates, Func<string, IReadOnlyList<string>> backupVersions, ProblemBook errors, string selfGuid, string? gameVersion, Action<string> log)
    {
        _folders = folders;
        _updates = updates;
        _backupVersions = backupVersions;
        _errors = errors;
        _selfGuid = selfGuid;
        GameVersion = gameVersion;
        _log = log;
    }

    public string? GameVersion { get; }

    /// <summary>Each mod with its relations resolved against all the others, so the list says whether what it needs will be there.</summary>
    public IReadOnlyList<ModEntry> Mods() => Listing().Mods;

    /// <summary>The mods, and the errors this session that belong to none of them.</summary>
    public (IReadOnlyList<ModEntry> Mods, IReadOnlyList<ModProblem> OtherErrors, int ErrorsNotKept) Listing()
    {
        var nodes = Nodes();
        var mods = Plugins().Select(plugin => Entry(plugin, nodes)).Concat(OffAtStart().Select(OffEntry)).ToList();
        var overlaps = PatchOverlaps.Find(SharedPatches());
        var declaredBy = mods.Select(mod => mod.Guid).Distinct().ToDictionary(guid => guid, DeclaredOf);
        var errors = ProblemOwners.Assign(_errors.All(), Owners(mods), overlaps);

        var listed = ModList.SortByName(mods.Select(mod => mod with
        {
            Relations = RelationResolver.Resolve(declaredBy[mod.Guid], mods, IsInRange),
            Dependents = RelationResolver.Dependents(mod.Guid, declaredBy, mods),
            PatchOverlaps = overlaps.TryGetValue(mod.Guid, out var shared) ? shared : Array.Empty<PatchOverlap>(),
            Errors = errors.ByMod.TryGetValue(mod.Guid, out var own) ? own : Array.Empty<ModProblem>(),
        }));

        return (listed, errors.Unclaimed, _errors.Dropped);
    }

    /// <summary>Each plugin BepInEx found, loaded or not, by the names its log source and its code go by.</summary>
    private static IReadOnlyList<ProblemOwner> Owners(IReadOnlyList<ModEntry> mods)
    {
        var isLibrary = mods.GroupBy(mod => mod.Guid).ToDictionary(group => group.Key, group => group.First().Info.IsLibrary);

        return Plugins().Select(plugin => new ProblemOwner(
            plugin.Metadata.GUID,
            plugin.Metadata.Name,
            new[] { RootOf(plugin.TypeName), Path.GetFileNameWithoutExtension(plugin.Location ?? "") }.Where(root => root.Length > 0).Distinct().ToList(),
            isLibrary.TryGetValue(plugin.Metadata.GUID, out var library) && library)).ToList();
    }

    private static string RootOf(string? typeName)
    {
        var name = typeName ?? "";
        var dot = name.IndexOf('.');

        return dot < 0 ? "" : name.Substring(0, dot);
    }

    /// <summary>Read fresh each time: a mod may patch late, or unpatch when it reloads.</summary>
    private IReadOnlyList<PatchedMethod> SharedPatches()
    {
        var loaded = Plugins().Where(plugin => plugin.Instance != null).ToList();
        var pluginOf = new Dictionary<Assembly, string>();

        foreach (var plugin in loaded)
        {
            pluginOf.TryAdd(plugin.Instance.GetType().Assembly, plugin.Metadata.GUID);
        }

        return HarmonyPatches.Shared(pluginOf, loaded.Select(plugin => plugin.Metadata.GUID).ToHashSet(), message => ReportOnce("patches", message));
    }

    /// <summary>The icons of every mod that embeds a usable one, read once: DLLs do not change their pictures while the game runs.</summary>
    public IReadOnlyDictionary<string, string> Icons() => _icons ??= Pictures("icon", info => info.Icon, IconImage.MaxBytes);

    public IReadOnlyDictionary<string, string> Banners() => _banners ??= Pictures("banner", info => info.Banner, IconImage.MaxBannerBytes);

    private Dictionary<string, string> Pictures(string what, Func<ModInfo, string?> nameOf, int maxBytes)
    {
        var pictures = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var mod in Mods())
        {
            if (nameOf(mod.Info) is not { } name || StartPathOf(mod.Guid) is not { } path) continue;

            try
            {
                var bytes = PluginFileReader.Resource(CurrentPath(path), name, maxBytes);
                var (uri, problem) = bytes == null ? (null, "is not embedded in " + Path.GetFileName(path)) : IconImage.DataUri(bytes, maxBytes);

                if (uri != null) pictures[mod.Guid] = uri;
                else ReportOnce(mod.Guid + "/" + what, "the " + what + " " + name + " of " + mod.Guid + " is not shown: it " + problem);
            }
            catch (Exception ex)
            {
                ReportOnce(mod.Guid + "/" + what, "could not read the " + what + " of " + mod.Guid + ": " + ex.Message);
            }
        }

        return pictures;
    }

    /// <summary>Where a mod's update may be put: a loaded mod under the plugins folder whose DLL was not moved this session.</summary>
    public (string? DllPath, string? Problem) InstallPathOf(string guid)
    {
        var location = Find(guid)?.Location;

        if (location == null || !_folders.IsOn(location)) return (null, "only a mod that is running from the plugins folder can be updated here");

        if (_moves.ContainsKey(location)) return (null, "it was turned off this session; turn it on and restart the game first");

        return (location, null);
    }

    public bool IsStraightInPlugins(string dllPath) =>
        string.Equals(Path.GetFullPath(Path.GetDirectoryName(dllPath) ?? ""), Path.GetFullPath(_folders.Plugins).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> Problems() => IL2CPPChainloader.Instance.DependencyErrors.ToList();

    public ModEntry? Mod(string guid) => Mods().FirstOrDefault(mod => mod.Guid == guid);

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
        var info = InfoOf(guid, location);
        ModState state = plugin.Instance is BasePlugin loaded ? new ModState.Loaded(ModList.Arrange(guid, Found(guid, loaded.Config), _startValues, info.SettingsApply)) : new ModState.NotLoaded();
        ModSwitch modSwitch = location != null && _folders.IsOn(location) && nodes.FirstOrDefault(node => node.Guid == guid) is { } node
            ? SwitchPlanner.ForLoaded(node, nodes, _selfGuid, _folders.IsOn(CurrentPath(location)))
            : new ModSwitch.Unavailable();

        var version = plugin.Metadata.Version.ToString();

        return Finish(new ModEntry(guid, plugin.Metadata.Name, version, Path.GetFileName(location ?? ""), state, info, modSwitch, _updates.StateOf(info.UpdateRepository, version)));
    }

    private ModEntry OffEntry((string Path, PluginFile Plugin) off)
    {
        var plugin = off.Plugin;
        var modSwitch = new ModSwitch.Allowed(_folders.IsOn(CurrentPath(off.Path)), Array.Empty<string>());

        var info = Info(plugin);

        return Finish(new ModEntry(plugin.Guid, plugin.Name, plugin.Version, Path.GetFileName(off.Path), new ModState.Off(), info, modSwitch, _updates.StateOf(info.UpdateRepository, plugin.Version)));
    }

    private ModEntry Finish(ModEntry mod)
    {
        var listed = _updates.GameVersionsListed(mod.Info.UpdateRepository, mod.Version);
        var install = _updates.InstallOf(mod.Guid);
        var rollback = mod.State is ModState.Loaded ? _updates.RollbackOf(mod.Guid, _backupVersions(mod.Guid), mod.Version) : new RollbackState.None();

        return mod with
        {
            Switch = SwitchPlanner.HoldWhileFilesChange(mod.Switch, install, rollback),
            Install = install,
            Rollback = rollback,
            Changelog = _updates.ChangelogOf(Changelogs.TextUri(mod.Info.Link(LinkKind.Changelog)?.Url)),
            ListedGameVersions = listed,
            IsUntestedOnGame = !Core.GameVersion.IsKnownToWork(mod.Info.GameVersions, listed, GameVersion),
        };
    }

    /// <summary>Reads the range the way BepInEx does; a range or version it cannot read is not held against the mod.</summary>
    private static bool IsInRange(string version, string range)
    {
        try
        {
            return new SemanticVersioning.Range(range, true).IsSatisfied(version, true);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private IReadOnlyList<DeclaredRelation> DeclaredOf(string guid) =>
        StartPathOf(guid) is { } path ? FilesOf(path).FirstOrDefault(plugin => plugin.Guid == guid)?.Relations ?? Array.Empty<DeclaredRelation>() : Array.Empty<DeclaredRelation>();

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
        var read = ModInfoReader.Read(plugin.Metadata, KeyNames);

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

    /// <summary>
    /// One setting that can't be read is skipped (and logged once) rather than hiding the whole mod. A tag or rule the
    /// Mod Manager cannot use is logged once too, so the mod's author can find out why.
    /// </summary>
    private IEnumerable<FoundSetting> Found(string guid, ConfigFile config)
    {
        var found = new List<FoundSetting>();

        foreach (var entry in config.Values)
        {
            try
            {
                var (setting, ignored) = Read(entry);

                found.Add(setting);

                foreach (var problem in ignored) ReportOnce(guid + "/" + entry.Definition + "/" + problem, "the setting " + entry.Definition + " of " + guid + ": " + problem);
            }
            catch (Exception ex)
            {
                ReportOnce(guid + "/" + entry.Definition, "could not read the setting " + entry.Definition + " of " + guid + ", so it is not shown: " + ex.Message);
            }
        }

        foreach (var problem in ShowRules.Problems(found)) ReportOnce(guid + "/show/" + problem, "in " + guid + ", " + problem);

        return found;
    }

    private static (FoundSetting Setting, IReadOnlyList<string> Ignored) Read(ConfigEntryBase entry)
    {
        var description = entry.Description;
        var tags = SettingTags.Read(description?.Tags);
        var ignored = tags.Ignored.ToList();
        var kind = SettingKinds.Classify(entry.SettingType, AcceptableOf(description?.AcceptableValues, entry.SettingType), KeyNames);

        if (tags.Control is { } control)
        {
            var (withControl, problem) = SettingKinds.WithControl(entry.SettingType, kind, control);

            kind = withControl;

            if (problem != null) ignored.Add(problem);
        }

        var defaultValue = TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType);
        var apply = ApplyModes.ForSetting(description?.Tags, LiveSettings.IsWatched(entry));
        var hints = HintsOf(description?.Tags) with { IsAdvanced = tags.IsAdvanced || IsAdvancedByAttributes(description?.Tags), ShowWhen = tags.ShowWhen, ChoiceZh = tags.ChoiceZh };

        return (new FoundSetting(entry.Definition.Section, entry.Definition.Key, description?.Description ?? "", kind, defaultValue, entry.GetSerializedValue(), hints, apply), ignored);
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
        var attributes = AttributesOf(tags);

        if (attributes == null) return Hints.None;

        return new Hints(
            IsHidden: Member(attributes, "Browsable") is false,
            DisplayName: Member(attributes, "DispName") as string,
            Order: Member(attributes, "Order") is int order ? order : 0,
            IsReadOnly: Member(attributes, "ReadOnly") is true);
    }

    private static bool IsAdvancedByAttributes(object[]? tags) => AttributesOf(tags) is { } attributes && Member(attributes, "IsAdvanced") is true;

    private static object? AttributesOf(object[]? tags) => tags?.FirstOrDefault(tag => tag != null && tag.GetType().Name.Contains("ConfigurationManagerAttributes"));

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
