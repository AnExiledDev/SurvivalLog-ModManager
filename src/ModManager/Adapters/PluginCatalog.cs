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
    private readonly ModAnswers _answers;
    private readonly GameItemTable _items;
    private readonly Func<string?, GameFit?> _fitOf;
    private readonly TrashCan _trash;
    private readonly string _configFolder;
    private readonly Dictionary<string, bool> _prepInConfig = new(StringComparer.OrdinalIgnoreCase);
    private readonly InstallScanner _scanner;

    /// <summary>The trash item of each DLL uninstalled this session, by where the DLL was when the game started.</summary>
    private readonly Dictionary<string, string> _trashed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The ConfigFiles whose .cfg went to the trash with their mod, which then stop saving so the file is not made again.</summary>
    private readonly Dictionary<string, bool> _savingStopped = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<PluginFile>> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _hashes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where each DLL moved this session, by where it was when the game started.</summary>
    private readonly Dictionary<string, string> _moves = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<(string Path, PluginFile Plugin)>? _offAtStart;
    private IReadOnlyDictionary<string, string>? _icons;
    private IReadOnlyDictionary<string, string>? _banners;

    /// <param name="backupVersions">The names of a mod's backup folders, by GUID.</param>
    /// <param name="items">The game's items and recipes, which recipe lists are checked against.</param>
    /// <param name="fitOf">The check of a DLL against the game's code, by the DLL's SHA-256; null until it is checked.</param>
    /// <param name="configFolder">BepInEx/config, where a mod that is not running keeps its .cfg.</param>
    /// <param name="scanner">Looks at the BepInEx folder for the health check.</param>
    public PluginCatalog(PluginFolders folders, UpdateBook updates, Func<string, IReadOnlyList<string>> backupVersions, ProblemBook errors, string selfGuid, string? gameVersion, GameItemTable items, Func<string?, GameFit?> fitOf, TrashCan trash, string configFolder, InstallScanner scanner, Action<string> log)
    {
        _scanner = scanner;
        _trash = trash;
        _configFolder = configFolder;
        _fitOf = fitOf;
        _items = items;
        _folders = folders;
        _updates = updates;
        _backupVersions = backupVersions;
        _errors = errors;
        _selfGuid = selfGuid;
        GameVersion = gameVersion;
        _log = log;
        _answers = new ModAnswers(log);
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
        var installed = mods.Select(mod => mod.Guid).ToHashSet(StringComparer.Ordinal);

        var listed = ModList.SortByName(mods.Select(mod => mod with
        {
            Relations = RelationResolver.Resolve(declaredBy[mod.Guid], mods, IsInRange),
            Dependents = RelationResolver.Dependents(mod.Guid, declaredBy, mods),
            PatchOverlaps = overlaps.TryGetValue(mod.Guid, out var shared) ? shared : Array.Empty<PatchOverlap>(),
            Errors = errors.ByMod.TryGetValue(mod.Guid, out var own) ? own : Array.Empty<ModProblem>(),
            Known = KnownFactsOf.For(mod, _updates.Index, _updates.Nexus, installed, HashOf(mod.Guid)),
            Fit = _fitOf(HashOf(mod.Guid)),
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

        if (_trashed.ContainsKey(location)) return (null, "it was uninstalled this session; restore it and restart the game first");

        if (_moves.ContainsKey(location)) return (null, "it was turned off this session; turn it on and restart the game first");

        return (location, null);
    }

    public bool IsStraightInPlugins(string dllPath) =>
        string.Equals(Path.GetFullPath(Path.GetDirectoryName(dllPath) ?? ""), Path.GetFullPath(_folders.Plugins).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> Problems() => IL2CPPChainloader.Instance.DependencyErrors.ToList();

    /// <summary>Where BROWSE installs a mod: straight in the plugins folder.</summary>
    public string PluginsFolder => _folders.Plugins;

    /// <summary>True when a turned-off DLL of this name waits at the top of an off folder, so a fresh copy would meet it when turned on.</summary>
    public bool IsOffThere(string dllName) => _folders.OffFolders.Any(folder => File.Exists(Path.Combine(folder, dllName)));

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

    /// <summary>
    /// Moves the mod's DLL to the trash, and its .cfg too when asked. The game keeps running the copy it loaded, so the
    /// mod stays in the list, marked uninstalled, until the game quits.
    /// </summary>
    public ModNote Uninstall(string guid, bool withSettings)
    {
        var mod = Mod(guid);
        var startPath = StartPathOf(guid);

        if (mod?.Switch is not ModSwitch.Allowed allowed || startPath == null) return new ModNote(guid, true, TrashNotes.CannotUninstall());

        var current = CurrentPath(startPath);
        var config = ConfigOf(guid);
        var settingsPath = config?.ConfigFilePath ?? Path.Combine(_configFolder, guid + ".cfg");
        var hasSettingsFile = File.Exists(settingsPath);
        var isMovingSettings = withSettings && hasSettingsFile;
        TrashItem item;

        try
        {
            item = _trash.Put(guid, mod.DisplayName, mod.Version, current, isMovingSettings ? settingsPath : null);
        }
        catch (Exception ex)
        {
            _log("could not move " + guid + " to the trash: " + ex.Message);

            return new ModNote(guid, true, TrashNotes.UninstallFailed(ex.Message));
        }

        _trashed[startPath] = item.Id;
        _moves[startPath] = _trash.StoredPath(item, item.Dll);

        if (isMovingSettings && config != null)
        {
            _savingStopped[guid] = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
        }

        _log("uninstalled " + guid + " from the next start: moved " + current + (isMovingSettings ? " and " + settingsPath : "") + " to the trash as " + item.Id);

        return new ModNote(guid, false, TrashNotes.Uninstalled(hasSettingsFile, isMovingSettings, allowed.AlsoStops));
    }

    /// <summary>
    /// Puts an uninstalled mod's files back where they were. One uninstalled this session is in the list again at once;
    /// one from an earlier session shows from the next start, when BepInEx finds it.
    /// </summary>
    public ModNote Restore(string trashId)
    {
        var item = _trash.Items().FirstOrDefault(each => each.Id == trashId);

        if (item == null) return new ModNote("", true, TrashNotes.Gone());

        try
        {
            _trash.Restore(item);
        }
        catch (TrashTaken taken)
        {
            return new ModNote(item.Guid, true, TrashNotes.RestoreTaken(taken.Path));
        }
        catch (Exception ex)
        {
            _log("could not restore " + item.Id + " from the trash: " + ex.Message);

            return new ModNote(item.Guid, true, TrashNotes.RestoreFailed(ex.Message));
        }

        var startPath = _trashed.Where(pair => pair.Value == item.Id).Select(pair => pair.Key).FirstOrDefault();
        var isThisSession = startPath != null;

        if (startPath != null)
        {
            _trashed.Remove(startPath);

            if (string.Equals(Path.GetFullPath(startPath), Path.GetFullPath(item.Dll.From), StringComparison.OrdinalIgnoreCase)) _moves.Remove(startPath);
            else _moves[startPath] = item.Dll.From;
        }

        if (_savingStopped.Remove(item.Guid, out var wasSaving) && ConfigOf(item.Guid) is { } config) config.SaveOnConfigSet = wasSaving;

        _log("restored " + item.Id + " from the trash to " + string.Join(" and ", item.Files.Select(file => file.From)));

        return new ModNote(item.Guid, false, TrashNotes.Restored(item.Name, _folders.IsOn(item.Dll.From), isThisSession));
    }

    /// <summary>Deletes for good every mod in the trash but the ones uninstalled this session, which the game is still running.</summary>
    public ModNote EmptyTrash()
    {
        var (deleted, kept, failed) = _trash.Empty();

        return new ModNote("", failed.Count > 0, TrashNotes.Emptied(deleted, kept, failed));
    }

    public IReadOnlyList<TrashEntry> Trash() => _trash.Items().Select(item => TrashEntry.Of(item, _trash.IsThisSession(item.Id))).ToList();

    /// <summary>What is wrong with how BepInEx and the mods are installed, looked at again each time the screen asks.</summary>
    public IReadOnlyList<HealthFinding> Health(IReadOnlyList<ModEntry> mods)
    {
        try
        {
            var inUse = Plugins().Select(plugin => (plugin.Instance as BasePlugin)?.Config?.ConfigFilePath).OfType<string>().ToList();
            var scan = _scanner.Scan(Paths.BepInExVersion?.ToString() ?? "", inUse, _trash.Items().Select(item => item.Guid));

            return HealthCheck.Find(scan, mods, ExiledToolkit.Plugin.Guid);
        }
        catch (Exception ex)
        {
            ReportOnce("health", "could not run the health check: " + ex.Message);

            return Array.Empty<HealthFinding>();
        }
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

    /// <summary>The assembly of a loaded plugin, or null when it is not loaded.</summary>
    public Assembly? AssemblyOf(string guid) => Find(guid)?.Instance?.GetType().Assembly;

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
        ModState state = plugin.Instance is BasePlugin loaded ? new ModState.Loaded(ModList.Arrange(guid, Found(guid, loaded.Config, loaded.GetType().Assembly), _startValues, info.SettingsApply)) : new ModState.NotLoaded();
        ModSwitch modSwitch = location != null && _trashed.TryGetValue(location, out var trashId) ? new ModSwitch.Uninstalled(trashId)
            : location != null && _folders.IsOn(location) && nodes.FirstOrDefault(node => node.Guid == guid) is { } node
            ? SwitchPlanner.ForLoaded(node, nodes, _selfGuid, _folders.IsOn(CurrentPath(location)))
            : new ModSwitch.Unavailable();

        var version = plugin.Metadata.Version.ToString();
        var offersPrep = state is ModState.Loaded running ? UninstallPrep.IsIn(running.Settings) : PrepInConfig(guid);

        return Finish(new ModEntry(guid, plugin.Metadata.Name, version, Path.GetFileName(location ?? ""), state, info, modSwitch, _updates.StateOf(info.UpdateRepository, version)) { OffersUninstallPrep = offersPrep });
    }

    private ModEntry OffEntry((string Path, PluginFile Plugin) off)
    {
        var plugin = off.Plugin;
        ModSwitch modSwitch = _trashed.TryGetValue(off.Path, out var trashId) ? new ModSwitch.Uninstalled(trashId) : new ModSwitch.Allowed(_folders.IsOn(CurrentPath(off.Path)), Array.Empty<string>());

        var info = Info(plugin);

        return Finish(new ModEntry(plugin.Guid, plugin.Name, plugin.Version, Path.GetFileName(off.Path), new ModState.Off(), info, modSwitch, _updates.StateOf(info.UpdateRepository, plugin.Version)) { OffersUninstallPrep = PrepInConfig(plugin.Guid) });
    }

    private ModEntry Finish(ModEntry mod)
    {
        var listed = _updates.GameVersionsListed(mod.Info.UpdateRepository, mod.Guid, mod.Version, HashOf(mod.Guid));
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

    /// <summary>For a mod that is not running: whether its .cfg has the UninstallPrep setting. Read once, as the file only changes while the mod runs.</summary>
    private bool PrepInConfig(string guid)
    {
        if (_prepInConfig.TryGetValue(guid, out var known)) return known;

        var path = Path.Combine(_configFolder, guid + ".cfg");

        try
        {
            known = File.Exists(path) && UninstallPrep.IsInConfig(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            ReportOnce(path + "/prep", "could not read " + path + ": " + ex.Message);
            known = false;
        }

        _prepInConfig[guid] = known;

        return known;
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

    /// <summary>Each installed mod's DLL, on or off, where it is now, with its SHA-256; one DLL with two plugins is listed once.</summary>
    public IReadOnlyList<(string Path, string Sha256)> Dlls() =>
        Plugins().Select(plugin => plugin.Metadata.GUID).Concat(OffAtStart().Select(off => off.Plugin.Guid)).Distinct()
            .Select(guid => (Start: StartPathOf(guid), Sha256: HashOf(guid)))
            .Where(dll => dll.Start != null && dll.Sha256 != null)
            .Select(dll => (CurrentPath(dll.Start!), dll.Sha256!))
            .GroupBy(dll => dll.Item2).Select(group => group.First()).ToList();

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
    /// The SHA-256 of the mod's DLL as it was at the start, which the wiki's index knows each release by. Read once per
    /// DLL; one that can't be read has no hash, and the mod is then known by its version.
    /// </summary>
    internal string? HashOf(string guid)
    {
        if (StartPathOf(guid) is not { } path) return null;
        if (_hashes.TryGetValue(path, out var known)) return known;

        try
        {
            using var file = File.OpenRead(CurrentPath(path));
            known = Sha256Text.Of(file);
        }
        catch (Exception ex)
        {
            ReportOnce(path + "/hash", "could not read " + path + " to know which release it is: " + ex.Message);
            known = null;
        }

        _hashes[path] = known;

        return known;
    }

    /// <summary>
    /// One setting that can't be read is skipped (and logged once) rather than hiding the whole mod. A tag or rule the
    /// Mod Manager cannot use is logged once too, so the mod's author can find out why.
    /// </summary>
    private IEnumerable<FoundSetting> Found(string guid, ConfigFile config, Assembly assembly)
    {
        var found = new List<FoundSetting>();

        foreach (var entry in config.Values)
        {
            try
            {
                var (setting, ignored) = Read(entry);

                if (setting.Kind is SettingKind.ItemList or SettingKind.ItemCounts or SettingKind.EntryList) setting = setting with { Answer = _answers.For(guid, assembly, setting.Section, setting.Key, setting.Kind is SettingKind.EntryList) };

                found.Add(setting);

                foreach (var problem in ignored) ReportOnce(guid + "/" + entry.Definition + "/" + problem, "the setting " + entry.Definition + " of " + guid + ": " + problem);
            }
            catch (Exception ex)
            {
                ReportOnce(guid + "/" + entry.Definition, "could not read the setting " + entry.Definition + " of " + guid + ", so it is not shown: " + ex.Message);
            }
        }

        foreach (var problem in ShowRules.Problems(found).Concat(ModList.LinkProblems(found))) ReportOnce(guid + "/show/" + problem, "in " + guid + ", " + problem);

        return RecipeClash.IsNeeded(found) ? RecipeClash.Check(found, _items.Recipes(), _items.NameOf) : found;
    }

    private static (FoundSetting Setting, IReadOnlyList<string> Ignored) Read(ConfigEntryBase entry)
    {
        var description = entry.Description;
        var tags = SettingTags.Read(description?.Tags);
        var ignored = tags.Ignored.ToList();
        var kind = SettingKinds.Classify(entry.SettingType, AcceptableOf(description?.AcceptableValues, entry.SettingType), KeyNames);

        if (tags.Control is { } control)
        {
            var (withControl, problem) = SettingKinds.WithControl(entry.SettingType, kind, control, tags.Rules, tags.Entry);

            kind = withControl;

            if (problem != null) ignored.Add(problem);
        }

        var defaultValue = TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType);
        var apply = ApplyModes.ForSetting(description?.Tags, LiveSettings.IsWatched(entry));
        var hints = HintsOf(description?.Tags) with { IsAdvanced = tags.IsAdvanced || IsAdvancedByAttributes(description?.Tags), ShowWhen = tags.ShowWhen, ChoiceZh = tags.ChoiceZh, ChoicePreview = tags.ChoicePreview, Makes = tags.Makes, ReturnsOf = tags.ReturnsOf };

        var world = tags.IsWorldRule ? WorldAccess.StateOf(entry) : null;

        return (new FoundSetting(entry.Definition.Section, entry.Definition.Key, description?.Description ?? "", kind, defaultValue, entry.GetSerializedValue(), hints, apply) { World = world }, ignored);
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
