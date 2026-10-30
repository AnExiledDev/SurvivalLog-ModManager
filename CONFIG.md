# Mod Manager settings

Settings live in `BepInEx\config\com.dev.survivallog.modmanager.cfg`, created the first time the game starts with the mod. Change them on the Mods screen, or edit the file while the game is closed. Delete the file to get the defaults back.

## Menu

| Setting | Default | What it does |
| --- | --- | --- |
| `OpenKey` | `None` | The key that opens and closes the Mods screen. Unbound by default: the Mods button in the Esc menu and on the title screen opens it. Set a key here to open it from the keyboard too. The key does nothing while you type in a text box. |

While the Mods screen is open, the game's own hotkeys are paused, and Esc closes the screen. While you type in a box on the screen, keys go to the box instead.

## Updates

| Setting | Default | What it does |
| --- | --- | --- |
| `CheckForUpdates` | `true` | Each time the game starts, ask GitHub whether mods that name a GitHub repository have a newer release. Only GitHub is contacted, and no account or key is needed. Turning it off stops the checks at once. |
| `ShowToast` | `true` | After the update check at the start, show one message saying how many mods have updates. Click it to see them. |

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | How big Mod Manager's own windows, panels and messages are drawn, in percent, from 75 to 200. At `0` they follow the UI size set in Exiled's Toolkit (its `UiScale`). Needs Exiled's Toolkit 0.11.0 or newer. |
