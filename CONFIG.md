# Mod Manager settings

Settings live in `BepInEx\config\com.dev.survivallog.modmanager.cfg`, created the first time the game starts with the mod. Change them on the Mods screen, or edit the file while the game is closed. Delete the file to get the defaults back.

## Menu

| Setting | Default | What it does |
| --- | --- | --- |
| `OpenKey` | `None` | The key that opens and closes the Mods screen. Unbound by default: the Mods button in the Esc menu and on the title screen opens it. Set a key here to open it from the keyboard too. The key does nothing while you type in a text box. |

While the Mods screen is open, the game's hotkeys are paused, and Esc closes the screen. While you type in a box on the screen, keys go to the box instead.

## Updates

| Setting | Default | What it does |
| --- | --- | --- |
| `CheckForUpdates` | `true` | Each time the game starts, look for mod updates and for what is known about your mods. GitHub is asked about mods that name a GitHub repository, the Survival Log Wiki (`sldb.core-directive.com`) for its list of mods and known problems, and Nexus Mods, at most once a day, for the newest version of each of the game's mods there. No account or key is needed, and none of your files, settings or saves are sent. Turning it off stops the checks at once. |
| `WikiDownloads` | `false` | Download mods and their updates from the Survival Log Wiki's copies (`sldb.core-directive.com`) first, instead of only from the author's GitHub releases. Each copy must match the wiki's list. When this is off, the Mod Manager downloads only from GitHub, and a mod that only the wiki has can't be installed from BROWSE. Either way, a download that is the exact file the wiki lists installs without asking first. The wiki's list of mods and known problems is still read while `CheckForUpdates` is on. |
| `ShowToast` | `true` | After the update check at the start, show one message saying how many mods have updates. Click it to see them. |

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | How big Mod Manager's own windows, panels and messages are drawn, in percent, from 75 to 200. At `0` they follow the UI size set in Exiled's Toolkit (its `UiScale`). |
