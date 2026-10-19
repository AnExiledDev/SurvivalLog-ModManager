# Mod Manager

An in-game Mods screen for Survival Log. It lists every BepInEx mod you have installed, shows whether each one loaded, turns mods on or off, installs their updates, and lets you change their settings without leaving the game or opening a .cfg file.

Open it with F10, or with the Mods button in the Esc menu or on the title screen.

## Features

- **Every mod in one list**: each installed BepInEx plugin, with its icon, version, DLL and how many settings it has. Mods that BepInEx found but did not start are listed apart, and any load errors BepInEx reported are shown under them. Shared libraries that other mods need sit in their own "Libraries" group at the bottom, folded away until you open it.
- **Change settings in-game**: switches for on/off settings, sliders for numbers with a range, number boxes, drop-down lists, text boxes and key capture for hotkeys (click the key, then press the new one; Esc cancels). Each change is saved to the mod's .cfg file the moment you make it.
- **Knows which changes need a restart**: a mod can say whether its settings take effect at once. Changed settings that only apply at the next start are marked NEEDS RESTART, settings the mod applies at once get no mark, and settings of mods that do not say are marked MAY NEED RESTART.
- **Checked before saving**: a number outside its range is moved to the nearest end and the screen says so. A value the mod cannot take is refused with the reason, and the file is left as it was.
- **Search every setting**: type in the search box to find a setting across all mods by its name, section or description, or a mod by its name in either language.
- **Shows what you changed**: settings that differ from the mod's default are marked, with a button to put each one back, or all of a mod's settings at once.
- **Hotkey clashes**: a hotkey that another mod also uses is shown in red, with the name of the other mod.
- **English and Chinese**: the screen follows the game's language, and the language switch at the top changes it. Mod names and descriptions written in both languages show the matching one.
- **Open the .cfg folder**: a button opens Explorer with the mod's .cfg file selected.
- **Turn mods on or off**: the switch at the top of a mod's page moves its DLL out of `BepInEx\plugins` into `BepInEx\disabled-plugins` (or back). The change applies at the next game start, and the screen marks mods waiting for a restart. Turned-off mods stay in the list under "Off", and their settings files are kept. Turning a mod off also lists the mods that need it, since they will not start either. Mods in `BepInEx\plugins-disabled` are listed too and can be turned back on.
- **What a mod needs**: a mod's page lists the mods it needs, the ones it works with when present, and the ones it does not work with, each marked on, off or not installed. When something it needs is off, a TURN ON WHAT IT NEEDS button turns those on for the next start, along with whatever they need in turn, and names any that are not installed.
- **Game version check**: a mod can name the game version it was made for. When the game is a different version, the mod gets a VERSION mark in the list and a warning on its page.
- **Mod info and links**: a mod can give its name, a short description, its author, a category, and links to its Nexus Mods page, GitHub repository, website and a page for reporting problems. The link buttons open them in your browser.
- **Update checks**: each time the game starts, the Mod Manager asks GitHub whether mods that name a GitHub repository have a newer release. Mods with an update get an UPDATE mark in the list and an "Updates" filter, and one message says how many mods have updates (click it to see them). A mod's page shows the new version, the release notes, an INSTALL UPDATE button and a GET UPDATE button that opens the release page in your browser.
- **Install updates**: INSTALL UPDATE downloads the release's zip from GitHub and puts the new files in place for the next game start. The running game keeps the version it loaded. The old files are kept in `BepInEx\mod-manager\backups`, and a failed install puts them back.

The Mod Manager adds nothing to your save and does not edit game files. It writes the .cfg files of the mods whose settings you change, moves the DLLs of mods you turn on or off, and replaces the files of mods you update. Its only network use is GitHub: the update check, which asks for the latest release of each mod that names a repository, and the download when you click INSTALL UPDATE.

## Known issues and limits

- It shows settings that mods register with BepInEx. A mod that keeps its settings somewhere else has nothing to show here.
- Many mods read a setting only when the game starts. The change is saved at once, but such a mod uses it after the next game start. Mods can fix this with live settings (see "For mod authors").
- The Mod Manager and Exiled's Toolkit cannot be turned off here, since the screen needs them. Delete their DLLs to remove them.
- A mod whose DLL is outside `BepInEx\plugins` cannot be turned on, off or updated here.
- Update checks only cover mods that name a GitHub repository. INSTALL UPDATE needs the release to carry a .zip that holds the mod's DLL; otherwise use GET UPDATE and install it yourself.
- An installed update is used from the next game start. Turning the mod off in the same session is refused until then.
- GitHub allows 60 requests an hour without an account. Past that, release notes and installs say to try again within the hour; the update itself is still shown.

## Requirements

- Survival Log 1.1.18902. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.4.0 or later.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Install Exiled's Toolkit 0.4.0 or later.
3. Download `ModManager-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-ModManager/releases/latest) or from Nexus Mods.
4. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ModManager.dll`

## Uninstall

Delete `ModManager.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.modmanager.cfg`, `BepInEx\mod-manager.log` and the `BepInEx\mod-manager` folder (old files kept by updates). Settings you changed with it stay in the other mods' .cfg files.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.modmanager.cfg`, created the first time the game starts with the mod. You can change them on the Mods screen itself. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Tested on Survival Log 1.1.18902 with BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Mod Manager 0.4.0 started`. If it says the Mods screen is off because it needs Exiled's Toolkit 0.4.0 or later, update the Toolkit.

- Every setting the screen changes is logged as `set <mod>/<section>/<key> from <old> to <new>`, and every DLL it moves as `turned off <mod> from the next start: moved <from> to <to>`.
- Update checks log `checking GitHub for updates of <n> repositories`, then `latest release of <owner/repository>: <tag>` for each one, or the reason it could not be checked.
- Installs log `installing <mod> <tag> over <version>`, then where the old files went, or why it failed.
- A mod's info values that do not fit are logged with the key and the reason.

The same lines are in `BepInEx\mod-manager.log`, which survives restarts.

## For mod authors

Everything below is optional, and your mod runs the same without the Mod Manager. The Mod Manager reads it from your DLL's assembly metadata and BepInEx attributes, so it needs no reference to the Mod Manager, and it is read even while your mod is turned off.

### Mod info

Add any of these to your .csproj:

```xml
<ItemGroup>
  <AssemblyMetadata Include="ModManager.Name" Value="Better Map&#10;更好的地图" />
  <AssemblyMetadata Include="ModManager.Description" Value="What the mod does.&#10;模组的作用。" />
  <AssemblyMetadata Include="ModManager.Author" Value="Your name" />
  <AssemblyMetadata Include="ModManager.Category" Value="Interface" />
  <AssemblyMetadata Include="ModManager.Icon" Value="BetterMap.icon.png" />
  <AssemblyMetadata Include="ModManager.GameVersion" Value="1.1.18902" />
  <AssemblyMetadata Include="ModManager.Settings" Value="Live" />
  <AssemblyMetadata Include="ModManager.NexusId" Value="47" />
  <AssemblyMetadata Include="ModManager.GitHub" Value="owner/repository" />
  <AssemblyMetadata Include="ModManager.Website" Value="https://example.com/my-mod" />
  <AssemblyMetadata Include="ModManager.Issues" Value="https://github.com/owner/repository/issues" />
</ItemGroup>
```

Or in code: `[assembly: AssemblyMetadata("ModManager.NexusId", "47")]`. Only the first value of each key counts.

| Key | Value |
| --- | --- |
| `ModManager.Name` | Up to 160 characters. The name shown on the screen instead of your BepInPlugin name. English, then a new line (`&#10;` in a .csproj), then Chinese. Search finds both, and the plugin name too. |
| `ModManager.Description` | Up to 1000 characters. English, then a new line, then Chinese; the screen shows the half that matches its language. |
| `ModManager.Author` | Up to 80 characters. |
| `ModManager.Category` | One of `Gameplay`, `Balance`, `QualityOfLife`, `Interface`, `Visuals`, `Audio`, `Cheats`, `Tools`. Case, spaces, `-` and `_` do not matter. The screen shows it in the player's language. |
| `ModManager.Icon` | The name of an embedded resource in your DLL: a PNG or JPEG, up to 256 KB, square (64x64 or larger looks best). See "Icon" below. |
| `ModManager.Library` | `true` for a mod that only exists for other mods to use. It goes in the folded "Libraries" group. Default `false`. |
| `ModManager.GameVersion` | The game version your mod was made for, such as `1.1.18902`, or `1.1` for every 1.1 build. Up to 10, separated by commas. Leave it out and the mod is never flagged. |
| `ModManager.Settings` | `Live` when your settings take effect at once, `Restart` when they take effect at the next start. Covers every setting the tags below do not. Leave it out and changed settings say MAY NEED RESTART. |
| `ModManager.NexusId` | The number at the end of your mod's Nexus Mods address, `nexusmods.com/survivallog/mods/<number>`. |
| `ModManager.GitHub` | `owner/repository`. Shows a GitHub link and turns on update checks and installs (see "Updates"). |
| `ModManager.CheckForUpdates` | `false` keeps the GitHub link but stops update checks for your mod. Default `true`. |
| `ModManager.Website` | A full `http` or `https` address. |
| `ModManager.Issues` | A full `http` or `https` address where players report problems. Shown as REPORT A PROBLEM. |

Values that do not fit are left out, and `BepInEx\mod-manager.log` says which and why.

If you leave out `ModManager.GitHub` and your .csproj sets `RepositoryUrl` to a GitHub repository (`https://github.com/owner/repository`, with or without `.git`), that repository is used. The .NET SDK writes `RepositoryUrl` into the DLL for you.

### Icon

Embed the picture with a fixed resource name, then name it in `ModManager.Icon`:

```xml
<ItemGroup>
  <EmbeddedResource Include="icon.png" LogicalName="BetterMap.icon.png" />
  <AssemblyMetadata Include="ModManager.Icon" Value="BetterMap.icon.png" />
</ItemGroup>
```

The type is read from the file's bytes, so only a real PNG or JPEG is shown. Mods without an icon get their initials.

### Settings that apply live (hot reload)

By default the screen cannot know whether your mod reads a setting only at start, so a changed setting says MAY NEED RESTART. Three ways to tell it, from most to least work saved for players:

1. **Apply settings live with Exiled's Toolkit.** `LiveSettings.Watch` calls your code at once and again after every change, on the game's main thread. Each watched setting is shown as live, with no restart mark. See "Live settings" in the [Toolkit's README](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit#live-settings).

   ```csharp
   using ExiledToolkit;

   var speed = Config.Bind("General", "Speed", 1.5f, "...");
   LiveSettings.Watch(speed, value => Mover.Speed = value);
   ```

2. **Tag a setting.** Put the plain string `"ModManager.Live"` or `"ModManager.Restart"` in the setting's `ConfigDescription` tags. Use this when you apply a setting yourself (for example you read it every frame) or when it really needs a restart. No reference is needed.

   ```csharp
   Config.Bind("General", "Speed", 1.5f, new ConfigDescription("...", null, "ModManager.Live"));
   Config.Bind("General", "UseNewMap", true, new ConfigDescription("...", null, "ModManager.Restart"));
   ```

3. **Say it for the whole mod** with `ModManager.Settings` (`Live` or `Restart`).

When they disagree, a `ModManager.Restart` tag wins, then a `ModManager.Live` tag or a Toolkit watch, then `ModManager.Settings`.

### Dependencies and incompatibilities

The screen reads your plugin's BepInEx attributes, so declare them as you would anyway:

```csharp
[BepInDependency("com.dev.survivallog.exiledtoolkit")]                                         // needs
[BepInDependency("com.other.mod", BepInDependency.DependencyFlags.SoftDependency)]             // works with
[BepInIncompatibility("com.old.mod")]                                                          // does not work with
```

Hard dependencies are listed as NEEDS, and TURN ON WHAT IT NEEDS turns them on. Soft ones are listed as WORKS WITH (OPTIONAL). Incompatibilities are listed as DOES NOT WORK WITH and turn red while that mod is on.

### Updates

With `ModManager.GitHub` set (or a GitHub `RepositoryUrl`), the screen checks the repository's latest GitHub release at each game start:

- The release tag is the version, compared with your BepInPlugin version: `v1.2.0` or `1.2.0` against `1.1.0`. A tag that is not a version is shown as unreadable and never counts as an update.
- The release notes are the release's body text.
- For INSTALL UPDATE, attach a `.zip` to the release that holds your DLL. With one zip it is used whatever its name; with several, the one whose name holds your DLL's name (`BetterMap` for `BetterMap.dll`). Up to 50 MB.
- The folder holding your DLL in the zip maps onto the folder holding your DLL on disk. A zip laid out like the install (`BepInEx/plugins/BetterMap.dll`) works, and so does one with the DLL at its root. Files beside your DLL in the zip (other DLLs, a `lang` folder) are installed beside it.
- When your DLL sits straight in `BepInEx\plugins`, a folder every mod shares, only your DLL and its `.pdb` and `.xml` are written. Put your mod in its own folder (`BepInEx\plugins\BetterMap\`) if it ships more files.
- Downloads only come from GitHub over https. A zip with a file that would land outside your mod's folder is refused.
