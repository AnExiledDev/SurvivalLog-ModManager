# Changelog

## 0.3.0

- Update checks: at each game start, mods that name a GitHub repository are checked for a newer release. Mods with an update are marked in the list and get an "Updates" filter, and their page shows the release notes and a GET UPDATE button that opens the release page. No account or key is needed. Turn it off with `Updates.CheckForUpdates`.

## 0.2.0

- Turn mods on or off from a switch on each mod's page. The DLL moves to `BepInEx\disabled-plugins` (or back) and the change applies at the next start. Turned-off mods are listed under "Off". Turning a mod off names the mods that need it.
- Mods can show a description, their author, and Nexus Mods, GitHub and website links. Mod authors set these with assembly metadata; see "For mod authors" in the README.

## 0.1.0

First version. An in-game Mods screen (F10, or the MODS button in the Esc menu and on the title screen) that lists every installed mod, shows whether it loaded, and changes its settings. Needs Exiled's Toolkit 0.3.0 or later.
