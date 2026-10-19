# Changelog

## 0.6.0

- INSTALL ALL UPDATES at the top of the "Updates" filter installs every available update at once.
- ROLL BACK TO (version): after an update, put the files it replaced back for the next start.
- A mod's page shows the release notes of every release since your version, not only the latest.
- A mod's page has a CONFLICTS tab listing the other mods that change the same game code, which parts, and what each mod does there. Each is marked LIKELY TO CLASH (both rewrite the game code), MAY CLASH (one can skip or rewrite it) or PROBABLY HARMLESS, riskiest first.
- A mod that links its changelog gets a CHANGELOG tab next to CONFLICTS, which shows the changelog on the mod's page.
- A mod's page shows the version range of each mod it needs, and marks an installed version outside that range as wrong version.
- A mod's page lists the mods that need it (NEEDED BY) and the ones that can use it (USED BY (OPTIONAL)). A mod that needs it turns red while it is off.
- A mod with too many sections for one row of tabs lists them down the left side of its settings instead.
- Changing the view redraws only what changed, so the screen no longer flashes and replays its fade-in on every click.
- Fixed: a mod with an optional dependency lost its name, description, links and relations on the Mods screen, because its dependency could not be read.

## 0.5.0

- Item pickers: settings that hold game items, lists of items, items with amounts, or item categories can ask for a picker with names and icons in the game's language, instead of a box for id numbers.
- Mods can hide a setting until another setting has a given value, and fold rarely used settings into an "Advanced" group.
- Mods can show a banner across the top of their page and a CHANGELOG link.
- Mods can list fixed hotkeys they use. They show on the mod's page and count in the hotkey clash check.
- COPY MOD LIST at the bottom of the screen copies every mod with its version and state, for bug reports.

## 0.4.0

- Install updates: INSTALL UPDATE downloads the release's zip from GitHub and puts the new files in place for the next start. The old files are kept in `BepInEx\mod-manager\backups`, and a failed install puts them back.
- One message at the start says how many mods have updates; click it to see them. Turn it off with `Updates.ShowToast`.
- Settings show whether a change needs a restart: NEEDS RESTART, MAY NEED RESTART, or no mark for settings the mod applies at once. Mods built on Exiled's Toolkit 0.4.0 can apply settings live.
- A mod's page lists the mods it needs, works with and does not work with. TURN ON WHAT IT NEEDS turns on the ones it needs for the next start.
- Mods can give a name and description in English and Chinese, an icon, a category, a link for reporting problems, and the game version they were made for. A mod made for another game version is marked VERSION.
- Shared libraries sit in a folded "Libraries" group at the bottom of the list.
- Mods can opt out of update checks, and a mod's GitHub repository is found from its `RepositoryUrl` when it does not name one.
- The title-screen button reads "Mods", to match the other buttons there.
- Needs Exiled's Toolkit 0.4.0 or later.

## 0.3.0

- Update checks: at each game start, mods that name a GitHub repository are checked for a newer release. Mods with an update are marked in the list and get an "Updates" filter, and their page shows the release notes and a GET UPDATE button that opens the release page. No account or key is needed. Turn it off with `Updates.CheckForUpdates`.

## 0.2.0

- Turn mods on or off from a switch on each mod's page. The DLL moves to `BepInEx\disabled-plugins` (or back) and the change applies at the next start. Turned-off mods are listed under "Off". Turning a mod off names the mods that need it.
- Mods can show a description, their author, and Nexus Mods, GitHub and website links. Mod authors set these with assembly metadata; see "For mod authors" in the README.

## 0.1.0

First version. An in-game Mods screen (F10, or the MODS button in the Esc menu and on the title screen) that lists every installed mod, shows whether it loaded, and changes its settings. Needs Exiled's Toolkit 0.3.0 or later.
