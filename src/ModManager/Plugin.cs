using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit.Adapters;
using ModManager.Adapters;
using ModManager.Core;
using UnityEngine;

namespace ModManager;

[BepInPlugin(Guid, "Mod Manager", Version)]
[BepInDependency(ExiledToolkit.Plugin.Guid, ToolkitVersion)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.dev.survivallog.modmanager";
    public const string Version = "0.8.4";

    /// <summary>The Toolkit version whose internals this build was made against.</summary>
    private const string ToolkitVersion = ">=0.4.0";

    public override void Load()
    {
        var openKey = Config.Bind("Menu", "OpenKey", KeyCode.F10,
            "The key that opens and closes the Mods screen. The Mods button in the Esc menu and on the title screen opens it too. Default: F10.\n" +
            "打开和关闭「模组」界面的按键。Esc 菜单和标题画面中的「模组」按钮也能打开它。默认 F10。");
        var checkForUpdates = Config.Bind("Updates", "CheckForUpdates", true,
            "Each time the game starts, ask GitHub whether mods that name a GitHub repository have a newer release. Only GitHub is contacted, and no account or key is needed. Default: on.\n" +
            "每次启动游戏时，向 GitHub 查询标明了 GitHub 仓库的模组是否有新版本。只会连接 GitHub，不需要账号或密钥。默认开启。");
        var showToast = Config.Bind("Updates", "ShowToast", true,
            "After the update check at the start, show one message saying how many mods have updates. Click it to see them. Default: on.\n" +
            "启动时检查更新后，显示一条消息，说明有多少个模组有更新。点击即可查看。默认开启。");

        try
        {
            Start(openKey, checkForUpdates, showToast);
        }
        catch (Exception ex) when (ex is MissingMethodException or TypeLoadException or MissingFieldException)
        {
            Log.LogError("Mod Manager needs Exiled's Toolkit 0.4.0 or later, so the Mods screen is off: " + ex.Message);
        }
    }

    /// <summary>Kept apart from Load so a Toolkit that is too old fails here, where Load can catch it.</summary>
    private void Start(ConfigEntry<KeyCode> openKey, ConfigEntry<bool> checkForUpdates, ConfigEntry<bool> showToast)
    {
        var log = new ToolkitLog(Log, Path.Combine(Paths.BepInExRootPath, "mod-manager.log"));
        var startLine = $"===== Mod Manager {Version} started";
        var watch = new LogWatch(Paths.BepInExRootPath, startLine, log.Write);

        // Listening before the start line is logged: everything above it in LogOutput.log is read from the file instead.
        BepInEx.Logging.Logger.Listeners.Add(watch);
        log.Write($"{startLine}, open key {openKey.Value}");
        Ticker.Log = log.Write;
        var folders = new PluginFolders(Paths.PluginPath, new[] { Path.Combine(Paths.BepInExRootPath, "disabled-plugins"), Path.Combine(Paths.BepInExRootPath, "plugins-disabled") });

        var userAgent = "SurvivalLog-ModManager/" + Version;
        var updates = new UpdateBook();
        var checks = new UpdateChecks(updates, new SavedGameVersions(Path.Combine(Paths.BepInExRootPath, "mod-manager", "game-versions"), log.Write), userAgent, log.Write);
        var shelf = new BackupShelf(Path.Combine(Paths.BepInExRootPath, "mod-manager", "backups"));
        var pins = new RepositoryPins(Path.Combine(Paths.BepInExRootPath, "mod-manager", "repositories"), log.Write);
        var installer = new UpdateInstaller(updates, shelf, pins, userAgent, log.Write);
        var catalog = new PluginCatalog(folders, updates, shelf.Versions, watch.Book, Guid, ReadGameVersion(log.Write), log.Write);
        var session = new ModsSession(new ModsPage(log.Write), catalog, updates, checks, installer, watch, () => openKey.Value, () => checkForUpdates.Value, () => showToast.Value, log.Write);

        Ticker.Session = session;
        checkForUpdates.SettingChanged += (_, _) => session.CheckForUpdates(checkForUpdates.Value);
        AddComponent<Ticker>();
    }

    /// <summary>The game writes its version to Version.txt beside its data; without it, mods are not checked against a version.</summary>
    private static string? ReadGameVersion(Action<string> log)
    {
        var path = Path.Combine(Application.dataPath, "Version.txt");

        try
        {
            var version = GameVersion.FromVersionFile(File.ReadAllText(path));

            log("game version " + (version ?? "unknown: " + path + " holds no version"));

            return version;
        }
        catch (Exception ex)
        {
            log("could not read the game version from " + path + ", so mods are not checked against it: " + ex.Message);

            return null;
        }
    }
}
