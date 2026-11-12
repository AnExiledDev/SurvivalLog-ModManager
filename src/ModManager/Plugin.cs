using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit;
using ExiledToolkit.Kit;
using ExiledToolkit.Web;
using ModManager.Adapters;
using ModManager.Core;
using UnityEngine;

namespace ModManager;

[BepInPlugin(Guid, "Mod Manager", Version)]
[BepInDependency(ExiledToolkit.Plugin.Guid, ToolkitVersion)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.dev.survivallog.modmanager";
    public const string Version = "0.20.3";

    /// <summary>The Toolkit version whose internals this build was made against.</summary>
    private const string ToolkitVersion = ">=0.19.0";

    internal const string EscButtonId = "modmanager.mods";
    internal const string TitleButtonId = "modmanager.title";

    /// <summary>First among the mods' buttons: the Toolkit's README lists the order each mod takes.</summary>
    private const int MenuOrder = 100;

    public override void Load()
    {
        ModUiSize.Bind(Config, "ModManager");

        var openKey = Config.Bind("Menu", "OpenKey", Enum.Parse<KeyCode>(Hotkey.DefaultKey),
            "The key that opens and closes the Mods screen. Unbound by default: the Mods button in the Esc menu and on the title screen opens it. Set a key here to open it from the keyboard too. The key does nothing while you type in a text box.\n" +
            "打开和关闭「模组」界面的按键。默认不绑定：点击 Esc 菜单和标题画面中的「模组」按钮即可打开。在此设置按键后也可以用键盘打开。在输入框里打字时该按键不起作用。");
        var checkForUpdates = Config.Bind("Updates", "CheckForUpdates", true,
            "Each time the game starts, look for mod updates and for what is known about your mods. GitHub is asked about mods that name a GitHub repository, the Survival Log Wiki (sldb.core-directive.com) for its list of mods and known problems, and Nexus Mods, at most once a day, for the newest version of each of the game's mods there. No account or key is needed, and none of your files, settings or saves are sent. Default: on.\n" +
            "每次启动游戏时，检查模组更新以及已知的模组信息。会向 GitHub 查询标明了 GitHub 仓库的模组，向 Survival Log 维基（sldb.core-directive.com）获取模组列表和已知问题，并且每天最多一次向 Nexus Mods 查询本游戏各模组的最新版本。不需要账号或密钥，也不会发送你的任何文件、设置或存档。默认开启。");
        var wikiDownloads = Config.Bind("Updates", "WikiDownloads", false,
            "Download mods and their updates from the Survival Log Wiki's copies (sldb.core-directive.com) first, instead of only from the author's GitHub releases. Each copy must match the wiki's list. When this is off, the Mod Manager downloads only from GitHub, and a mod that only the wiki has can't be installed from BROWSE. The wiki's list of mods and known problems is still read while CheckForUpdates is on. Default: off.\n" +
            "优先从 Survival Log 维基（sldb.core-directive.com）保存的副本下载模组和更新，而不是只从作者的 GitHub 发布下载。每个副本都必须与维基的列表一致。关闭时，模组管理器只从 GitHub 下载，只有维基提供下载的模组无法在「浏览」中安装。只要「CheckForUpdates」开启，仍会读取维基的模组列表和已知问题。默认关闭。");
        var showToast = Config.Bind("Updates", "ShowToast", true,
            "After the update check at the start, show one message saying how many mods have updates. Click it to see them. Default: on.\n" +
            "启动时检查更新后，显示一条消息，说明有多少个模组有更新。点击即可查看。默认开启。");

        try
        {
            Start(openKey, checkForUpdates, wikiDownloads, showToast);
        }
        catch (Exception ex) when (ex is MissingMethodException or TypeLoadException or MissingFieldException)
        {
            Log.LogError("Mod Manager needs Exiled's Toolkit 0.19.0 or later, so the Mods screen is off: " + ex.Message);
        }
    }

    /// <summary>Kept apart from Load so a Toolkit that is too old fails here, where Load can catch it.</summary>
    private void Start(ConfigEntry<KeyCode> openKey, ConfigEntry<bool> checkForUpdates, ConfigEntry<bool> wikiDownloads, ConfigEntry<bool> showToast)
    {
        var log = new ModLog(Log, "mod-manager.log");
        var startLine = $"===== Mod Manager {Version} started";
        var watch = new LogWatch(Paths.BepInExRootPath, startLine, log.Write);

        // Listening before the start line is logged: everything above it in LogOutput.log is read from the file instead.
        BepInEx.Logging.Logger.Listeners.Add(watch);
        log.Write($"{startLine}, open key {openKey.Value}");
        Ticker.Log = log.Write;
        var folders = new PluginFolders(Paths.PluginPath, new[] { Path.Combine(Paths.BepInExRootPath, "disabled-plugins"), Path.Combine(Paths.BepInExRootPath, "plugins-disabled") });

        var userAgent = "SurvivalLog-ModManager/" + Version;
        var updates = new UpdateBook();
        var checks = new UpdateChecks(updates, new SavedGameVersions(Path.Combine(Paths.BepInExRootPath, "mod-manager", "game-versions"), log.Write), new SavedFile(Path.Combine(Paths.BepInExRootPath, "mod-manager", "index.json"), "index of the wiki's mods", log.Write), userAgent, log.Write);
        var nexus = new NexusLookups(updates, new SavedFile(Path.Combine(Paths.BepInExRootPath, "mod-manager", "nexus.json"), "answers from Nexus Mods", log.Write), Version, log.Write);
        var shelf = new BackupShelf(Path.Combine(Paths.BepInExRootPath, "mod-manager", "backups"));
        var pins = new RepositoryPins(Path.Combine(Paths.BepInExRootPath, "mod-manager", "repositories"), log.Write);
        var installer = new UpdateInstaller(updates, shelf, pins, userAgent, () => wikiDownloads.Value, log.Write);
        var items = new GameItemTable(log.Write);
        var fits = new GameFitChecks(Path.Combine(Paths.BepInExRootPath, "interop"), Path.Combine(Paths.BepInExRootPath, "core"), new SavedFile(Path.Combine(Paths.BepInExRootPath, "mod-manager", "game-code-checks.json"), "checks of the mods against the game's code", log.Write), log.Write);
        var trash = new TrashCan(Path.Combine(Paths.BepInExRootPath, "mod-manager", "trash"), new[] { folders.Plugins }.Concat(folders.OffFolders).Append(Paths.ConfigPath).ToList(), log.Write);
        var scanner = new InstallScanner(Paths.BepInExRootPath, Paths.PluginPath, Paths.PatcherPluginPath, Paths.ConfigPath, log.Write);
        var catalog = new PluginCatalog(folders, updates, shelf.Versions, watch.Book, Guid, ReadGameVersion(log.Write), items, fits.FitOf, trash, Paths.ConfigPath, scanner, log.Write);
        var page = new WebPage(typeof(Plugin).Assembly, "ModManager.page.js", PageScript.Api, PageCommand.Prefix + "noscript", log.Write);
        var session = new ModsSession(page, catalog, items, updates, checks, nexus, fits, installer, watch, new SavedFile(Path.Combine(Paths.BepInExRootPath, "mod-manager", "profiles.txt"), "profiles", log.Write), () => openKey.Value, () => checkForUpdates.Value, () => wikiDownloads.Value, () => showToast.Value, log.Write);

        // Walked on a worker from now, so the item pickers usually find the icons ready.
        WebIcons.StartScan();
        scanner.StartWarmUp();
        Ticker.Session = session;
        AddMenuButtons(session);
        checkForUpdates.SettingChanged += (_, _) => session.CheckForUpdates(checkForUpdates.Value);

        // Each download reads the setting when it starts, so a change only needs the open screen redrawn.
        LiveSettings.Watch(wikiDownloads, _ => session.Refresh());
        AddComponent<Ticker>();
    }

    /// <summary>The Mods button in the Esc menu, right after Settings, and on the title screen, right before Exit. Exiled's Toolkit places them, so they fit with every other mod's.</summary>
    private static void AddMenuButtons(ModsSession session)
    {
        GameMenu.Add(new GameMenuButton(EscButtonId, "Mods", GameMenuPlace.PauseTop, session.Open) { ChineseLabel = "模组", Order = MenuOrder });
        GameMenu.Add(new GameMenuButton(TitleButtonId, "Mods", GameMenuPlace.Title, session.Open) { ChineseLabel = "模组", Order = MenuOrder });
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
