# Mod Manager

An in-game Mods screen for Survival Log. It lists every BepInEx mod you have installed, shows whether each one loaded, turns mods on or off, and lets you change their settings without leaving the game or opening a .cfg file.

Open it with F10, or with the MODS button in the Esc menu or on the title screen.

## Features

- **Every mod in one list**: each installed BepInEx plugin, with its version, its DLL and how many settings it has. Mods that BepInEx found but did not start are listed apart, and any load errors BepInEx reported are shown under them.
- **Change settings in-game**: switches for on/off settings, sliders for numbers with a range, number boxes, drop-down lists, text boxes and key capture for hotkeys (click the key, then press the new one; Esc cancels). Each change is saved to the mod's .cfg file the moment you make it.
- **Checked before saving**: a number outside its range is moved to the nearest end and the screen says so. A value the mod cannot take is refused with the reason, and the file is left as it was.
- **Search every setting**: type in the search box to find a setting across all mods by its name, section or description.
- **Shows what you changed**: settings that differ from the mod's default are marked, with a button to put each one back, or all of a mod's settings at once. Settings changed since the game started are marked "MAY NEED RESTART", since some mods only read their settings at start.
- **Hotkey clashes**: a hotkey that another mod also uses is shown in red, with the name of the other mod.
- **English and Chinese**: the screen follows the game's language, and the language switch at the top changes it. Descriptions written in both languages show the matching half.
- **Open the .cfg folder**: a button opens Explorer with the mod's .cfg file selected.
- **Turn mods on or off**: the switch at the top of a mod's page moves its DLL out of `BepInEx\plugins` into `BepInEx\disabled-plugins` (or back). The change applies at the next game start, and the screen marks mods waiting for a restart. Turned-off mods stay in the list under "Off", and their settings files are kept. Turning a mod off also lists the mods that need it, since they will not start either. Mods in `BepInEx\plugins-disabled` are listed too and can be turned back on.
- **Mod info and links**: a mod can give a short description, its author, and links to its Nexus Mods page, GitHub repository or website. The link buttons open them in your browser.

The Mod Manager adds nothing to your save and does not edit game files. It only writes the .cfg files of the mods whose settings you change, and moves the DLLs of mods you turn on or off.

## Known issues and limits

- It shows settings that mods register with BepInEx. A mod that keeps its settings somewhere else has nothing to show here.
- Many mods read a setting only when the game starts. The change is saved at once, but such a mod uses it after the next game start.
- Turning a mod on does not turn on the mods it needs. Turn those on as well.
- The Mod Manager and Exiled's Toolkit cannot be turned off here, since the screen needs them. Delete their DLLs to remove them.
- A mod whose DLL is outside `BepInEx\plugins` cannot be turned on or off here.

## Requirements

- Survival Log 1.1.18902. Built and tested on this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.3.0 or later.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Install Exiled's Toolkit 0.3.0 or later.
3. Download `ModManager-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-ModManager/releases/latest) or from Nexus Mods.
4. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ModManager.dll`

## Uninstall

Delete `ModManager.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.modmanager.cfg` and `BepInEx\mod-manager.log`. Settings you changed with it stay in the other mods' .cfg files.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.modmanager.cfg`, created the first time the game starts with the mod. You can change them on the Mods screen itself. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Tested on Survival Log 1.1.18902 with BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for `===== Mod Manager 0.2.0 started`. If it says the Mods screen is off because it needs Exiled's Toolkit 0.3.0 or later, update the Toolkit. Every setting the screen changes is logged as `set <mod>/<section>/<key> from <old> to <new>`, and every DLL it moves as `turned off <mod> from the next start: moved <from> to <to>`. The same lines are in `BepInEx\mod-manager.log`, which survives restarts.

## For mod authors

Your mod can tell the Mods screen about itself with plain .NET assembly metadata. It needs no reference to the Mod Manager, so your mod runs the same without it, and the screen reads it even while your mod is turned off. Add any of these to your .csproj:

```xml
<ItemGroup>
  <AssemblyMetadata Include="ModManager.Description" Value="What the mod does.&#10;模组的作用。" />
  <AssemblyMetadata Include="ModManager.Author" Value="Your name" />
  <AssemblyMetadata Include="ModManager.NexusId" Value="47" />
  <AssemblyMetadata Include="ModManager.GitHub" Value="owner/repository" />
  <AssemblyMetadata Include="ModManager.Website" Value="https://example.com/my-mod" />
</ItemGroup>
```

Or in code: `[assembly: AssemblyMetadata("ModManager.NexusId", "47")]`.

| Key | Value |
| --- | --- |
| `ModManager.Description` | Up to 1000 characters. English, then a new line (`&#10;` in a .csproj), then Chinese; the screen shows the half that matches its language. |
| `ModManager.Author` | Up to 80 characters. |
| `ModManager.NexusId` | The number at the end of your mod's Nexus Mods address, `nexusmods.com/survivallog/mods/<number>`. |
| `ModManager.GitHub` | `owner/repository`. |
| `ModManager.Website` | A full `http` or `https` address. |

Values that do not fit are left out, and `BepInEx\mod-manager.log` says which and why.
