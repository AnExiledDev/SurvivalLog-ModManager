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
    public const string Version = "0.2.0";

    /// <summary>The Toolkit version whose internals this build was made against.</summary>
    private const string ToolkitVersion = ">=0.3.0";

    public override void Load()
    {
        var openKey = Config.Bind("Menu", "OpenKey", KeyCode.F10,
            "The key that opens and closes the Mods screen. The MODS button in the Esc menu and on the title screen opens it too. Default: F10.\n" +
            "打开和关闭「模组」界面的按键。Esc 菜单和标题画面中的「MODS」按钮也能打开它。默认 F10。");

        try
        {
            Start(openKey);
        }
        catch (Exception ex) when (ex is MissingMethodException or TypeLoadException or MissingFieldException)
        {
            Log.LogError("Mod Manager needs Exiled's Toolkit 0.3.0 or later, so the Mods screen is off: " + ex.Message);
        }
    }

    /// <summary>Kept apart from Load so a Toolkit that is too old fails here, where Load can catch it.</summary>
    private void Start(ConfigEntry<KeyCode> openKey)
    {
        var log = new ToolkitLog(Log, Path.Combine(Paths.BepInExRootPath, "mod-manager.log"));

        log.Write($"===== Mod Manager {Version} started, open key {openKey.Value}");
        Ticker.Log = log.Write;
        var folders = new PluginFolders(Paths.PluginPath, new[] { Path.Combine(Paths.BepInExRootPath, "disabled-plugins"), Path.Combine(Paths.BepInExRootPath, "plugins-disabled") });

        Ticker.Session = new ModsSession(new ModsPage(log.Write), new PluginCatalog(folders, Guid, log.Write), () => openKey.Value, log.Write);
        AddComponent<Ticker>();
    }
}
