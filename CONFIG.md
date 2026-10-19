# Mod Manager settings

Settings live in `BepInEx\config\com.dev.survivallog.modmanager.cfg`, created the first time the game starts with the mod. Change them on the Mods screen, or edit the file while the game is closed. Delete the file to get the defaults back.

## Menu

| Setting | Default | What it does |
| --- | --- | --- |
| `OpenKey` | `F10` | The key that opens and closes the Mods screen. The Mods button in the Esc menu and on the title screen opens it too. Set it to `None` to use only the buttons. |

While the Mods screen is open, the game's own hotkeys are paused, and Esc closes the screen. While you type in a box on the screen, keys go to the box instead.

## Updates

| Setting | Default | What it does |
| --- | --- | --- |
| `CheckForUpdates` | `true` | Each time the game starts, ask GitHub whether mods that name a GitHub repository have a newer release. Only GitHub is contacted, and no account or key is needed. Turning it off stops the checks at once. |
| `ShowToast` | `true` | After the update check at the start, show one message saying how many mods have updates. Click it to see them. |
