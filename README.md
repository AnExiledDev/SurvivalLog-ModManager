# Mod Manager

An in-game Mods screen for Survival Log. It lists every BepInEx mod you have installed, shows whether each one loaded, and lets you change their settings without leaving the game or opening a .cfg file.

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

The Mod Manager adds nothing to your save and does not edit game files. It only writes the .cfg files of the mods whose settings you change.

## Known issues and limits

- It shows settings that mods register with BepInEx. A mod that keeps its settings somewhere else has nothing to show here.
- Many mods read a setting only when the game starts. The change is saved at once, but such a mod uses it after the next game start.
- It cannot turn mods on or off yet.

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

Check `BepInEx\LogOutput.log` for `===== Mod Manager 0.1.0 started`. If it says the Mods screen is off because it needs Exiled's Toolkit 0.3.0 or later, update the Toolkit. Every setting the screen changes is logged as `set <mod>/<section>/<key> from <old> to <new>`. The same lines are in `BepInEx\mod-manager.log`, which survives restarts.
