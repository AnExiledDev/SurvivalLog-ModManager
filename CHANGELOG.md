# Changelog

## 0.20.0

- New PROFILES page beside BROWSE. SAVE THIS SETUP keeps which mods are on and off and every setting you changed, so you can go back to it later.
- COPY CODE copies a profile as a short code to share. Paste a code, or a list from COPY MOD LIST, and SEE CHANGES lists everything it would change before anything does. USE THIS SETUP turns mods on and off for the next start, changes the settings at once, and installs the mods it needs from BROWSE where it can. Mods it needs that BROWSE can't install get OPEN ON NEXUS.
- The search box on BROWSE no longer clears itself as you type.
- Made for Survival Log 1.1.20719.

## 0.19.0

- New BROWSE page beside MODS: every mod for Survival Log on Nexus Mods, with search, filters and sorting.
- Mods whose authors set them up for the Mod Manager install with one INSTALL click, along with the mods they need that install the same way. UNDO in the bar at the bottom takes a new mod out again before the next start.
- Every other mod is marked as a manual download. ASK THE AUTHOR copies a short request for Mod Manager support, in English and Chinese, and opens the mod's posts on Nexus for you to paste it.
- Made for Survival Log 1.1.20719.

## 0.18.2

- With Save Backups 0.5.0 or newer installed, your saves are backed up before updates are installed: the run you are playing, or every save on the title screen. Installs close together ask for one backup.
- Made for Survival Log 1.1.20719.

## 0.18.1

- With mods from Nexus Mods installed, the few Nexus file lists read each day now go first to the Nexus mods named most like yours, so most of them show their Nexus version from the first day.
- Made for Survival Log 1.1.20719.

## 0.18.0

- Updates of the mods listed on the Survival Log Wiki now download from the wiki first, so they install even where GitHub is slow or blocked. A copy that is not exactly the released file is never used, and the update comes from GitHub instead.
- When GitHub can't be reached, mods listed on the wiki still show their newest version, INSTALL UPDATE still works, and GET UPDATE opens the mod's page on the wiki.
- Mods from Nexus Mods now show when Nexus has a newer version, with a NEXUS button that opens the mod's page there. The Mod Manager asks Nexus at most once a day and needs no account.
- A mod version that is known not to work gets a BROKEN mark with the reason, and an update to such a version is marked HELD and never installed.
- Mods known not to work together are listed at the top of the Conflicts tab.
- A mod that uses game code this game version no longer has gets an OUTDATED mark, and its page lists what is missing.
- New UNINSTALL button on a mod's page. It moves the mod to a trash folder, and its settings file too if you tick the box. Before it does, it tells you whether the mod can get your save ready to load without it, and which other mods stop with it.
- New TRASH page beside MODS. RESTORE puts an uninstalled mod back, and EMPTY TRASH deletes them for good.
- A bar at the bottom of the screen now lists every change that waits for the next start, with UNDO to take one back and QUIT GAME to quit and use them.
- New HEALTH page beside MODS that shows problems with how mods are installed: the same mod or DLL installed twice, a folder unpacked one level too deep, a BepInEx build other than the one these mods are made for, a mod that needs a newer Exiled's Toolkit, and settings files left from mods you removed.
- Made for Survival Log 1.1.20719.

## 0.17.1

- Fixed Pack Rat's Inventories and Items screens, which since 0.16.0 were squeezed into one row with most of the screen outside the window.
- Made for Survival Log 1.1.20719.

## 0.17.0

- A mod's own list can now say what you have of each entry, such as how many of an item you carry or keep at home. Those rows are marked in the list and in the ADD window, and the window gets a "You have" group at the top that shows only them. Vital Signs is the first to use it.
- Made for Survival Log 1.1.20719.

## 0.16.0

- A mod's own list (such as Vital Signs' food, beds and buffs) now shows as a list instead of cards: one row per entry with its picture, its name and what it does. Each row has buttons to move it up or down and to take it out.
- A choice, such as a bed's Sleep or Nap, is a row of buttons on the entry, so you pick it in one click.
- ADD opens a window that lists everything the mod offers as rows, filed under the mod's groups, with an Add button on each row. The window stays open, so you can add several in a row, and a click on ADDED takes one out again.
- A list the mod fills in when it is left blank no longer shows everything it stands for, only the mod's line about it.
- Enter in the search box of the item window now adds the item shown, instead of doing nothing.
- Made for Survival Log 1.1.20719.

## 0.15.0

- A new kind of list for mods: one that picks from entries the mod lists itself, such as buffs, beds or furniture actions. It shows as cards with each entry's picture and name, and the same picker as items, with the mod's own groups and a line about each entry. Vital Signs is the first to use it.
- An entry in such a list can also carry a choice, shown on its card and changed with a click, or a number, stepped on its card like an amount.
- Made for Survival Log 1.1.20719.

## 0.14.2

- A crafting recipe list now warns you, under the list, when another recipe in the game already uses exactly those materials. Two recipes can't share the same materials, so the mod can't use that list.
- The mouse wheel changes an amount only while it is over the - and + buttons, so scrolling down the page past an item card no longer changes it.
- Typing an amount in the item window and then clicking ADD now adds the item.
- Changing an amount and then quickly changing another list keeps both changes.
- Made for Survival Log 1.1.20719.

## 0.14.1

- **Warning:** needs Exiled's Toolkit 0.19.0 or newer. Update the Toolkit first.
- Now uses Exiled's Toolkit for its shared parts.
- Made for Survival Log 1.1.20719.

## 0.14.0

- Item lists in mod settings now show as item cards with each item's picture, name and amount. Click a card to swap its item, drag cards to reorder them, and change an amount with the - and + buttons or the mouse wheel over them (hold Shift for 10 at a time).
- ADD ITEM opens a window of every item in the game, sorted into the game's categories with the items recipes use first. Search by name in English or Chinese, or by id, and see each item's weight, value and how many recipes use it before you add it.
- A crafting recipe setting shows the item it makes, with the recipe's total pieces, weight and value.
- A setting for what dismantling gives back shows the items you get while it is left blank, and SET BY HAND turns that into a list you can edit. It warns you when it would give back more than the recipe is worth.
- When a mod cannot use a list (for example, the game already has a recipe with exactly those materials), the reason shows under the list.
- The plain text of a list sits below its cards; click it to type or paste a list.
- Mod authors: new tags `ModManager.MaxKinds`, `ModManager.MaxCount`, `ModManager.AutoWhenBlank`, `ModManager.Makes` and `ModManager.ReturnsOf`, and an optional `ModManagerAnswers` class. See "For mod authors" in the README.
- Made for Survival Log 1.1.20151.

## 0.13.0

- Settings that each world can keep for itself (a mod marks them, and Exiled's Toolkit 0.16.0 or newer plays them) now say, while you play, whether this world keeps its own value. A change to one this world keeps goes to this world alone, and USE GLOBAL sends the world back to the value every other world uses. THIS WORLD ONLY makes a setting this world's own, so later changes stay with it.
- Made for Survival Log 1.1.20151.

## 0.12.1

- **Warning:** needs Exiled's Toolkit 0.14.0 or newer. Update the Toolkit first.
- An error the game's own code throws inside a method several mods patch now shows once on the game's ERRORS page, with the mods that patch that method, instead of as an error under every one of those mods.
- Made for Survival Log 1.1.20151.

## 0.12.0

- The Mods button in the Esc menu and on the title screen is now placed by Exiled's Toolkit, along with every other mod's menu buttons, so the Esc menu stays on screen however many mods add buttons to it.
- Exiled's Toolkit 0.14.0 or newer is now required.
- Made for Survival Log 1.1.20151.

## 0.11.1

- The key that opens the Mods screen is now unbound by default; use the Mods button in the Esc menu or on the title screen, or set a key in the settings.
- The open key no longer fires while you type in a text box in any mod's window.
- Exiled's Toolkit 0.12.0 or newer is now required.
- Made for Survival Log 1.1.20151.

## 0.11.0

- List settings can show a small picture of each choice, in the drop-down and in the closed box, so you can see what you are picking. A mod draws each picture with the new `ModManager.ChoicePreview` tag.
- Made for Survival Log 1.1.20151.

## 0.10.0

- List settings can show their choices in Chinese. A mod names each choice with the new `ModManager.ChoiceZh` tag; the value saved stays the same.
- Made for Survival Log 1.1.19611.

## 0.9.0

- New: `UiScale` under Interface sets the size of Mod Manager's own windows on their own, from 75% to 200%, whatever Exiled's Toolkit's UI size is. At 0, the default, they follow the Toolkit's. Needs Exiled's Toolkit 0.11.0 or newer.
- Made for Survival Log 1.1.19611.

## 0.8.5

- Smoother maps and screens: the Mods button watches only the Esc menu and the title screen. Before, it also watched every other game page, so each change the game made on the trade map, the explore map or a shop woke it for nothing.
- Made for Survival Log 1.1.19611.

## 0.8.4

- Fixed: almost every mod update was marked REFUSED with "its DLL could not be read (Failed to resolve assembly: 'BepInEx.Core ...')". Updates install again.
- Made for Survival Log 1.1.19611.

## 0.8.3

- The Mods screen can be drawn bigger or smaller with the UI size setting of Exiled's Toolkit 0.10.0 or later. A change shows at once. A bigger screen still fits the game's window.
- Tabs other mods add to the screen take the same size.
- Made for Survival Log 1.1.19611.

## 0.8.2

- The Mods button in the Esc menu and on the title screen shows the moment the menu opens, instead of popping in up to a second later.

## 0.8.1

- After INSTALL ALL UPDATES, an update that waits for INSTALL ANYWAY is marked ASK in the mod list, and a refused one is marked REFUSED. A line under the button says how many of each there are.

## 0.8.0

- Updates are checked before they are installed:
  - The new DLL must be the same mod (the same BepInPlugin GUID) as the one installed. A zip that holds a program file (.exe, .bat, .ps1 and the like), or a file name Windows would change, is refused.
  - A mod's updates must keep coming from the GitHub repository its first install came from. A repository that was deleted and made again under the same name by someone else is not installed from; use GET UPDATE to install by hand if you trust it.
  - An update that starts doing something harmful code needs and a game mod does not (starting other programs, loading other code, using the Windows registry) is refused. A mod that already did it when installed keeps updating.
  - When an update can do something else the installed version could not (connect to the internet, call native code, open links or files in other programs, or add another DLL), its page says what, and it is installed only when you click INSTALL ANYWAY. INSTALL ALL UPDATES skips these.
  - A refused update says why on its page and is not offered again until the next start. GET UPDATE still lets you install it by hand.

## 0.7.0

- Mods can add a tab of their own to their page on the Mods screen, with any page they like (New Loop Plus uses it to edit its loop rewards). See "Your own tab" under "For mod authors" in the README.
- Text boxes with several lines take the keyboard the same way one-line boxes do.
- A mod's author can list the game versions each version of the mod works on in a `game-versions.txt` in its GitHub repository, so a mod that still works after a game update is no longer marked. See "Game versions it works on" under "For mod authors" in the README.
- A mod not known to work on this game version gets a grey VERSION mark and a note that it may still work. The mark turns red, and the note says it may need an update, once the mod logs errors this session.

## 0.6.0

- INSTALL ALL UPDATES at the top of the "Updates" filter installs every available update at once.
- ROLL BACK TO (version): after an update, put the files it replaced back for the next start.
- A mod's page shows the release notes of every release since your version, not only the latest.
- A mod's page has a CONFLICTS tab listing the other mods that change the same game code, which parts, and what each mod does there. Each is marked LIKELY TO CLASH (both rewrite the game code), MAY CLASH (one can skip or rewrite it) or PROBABLY HARMLESS, riskiest first.
- Errors and warnings logged this session are listed under the mod they came from: an ERRORS tab on the mod's page (with the message, the stack, and how many times it happened), an ERROR or WARNING mark in the list, and an "Errors" filter. When an error went through game code that another mod also changes, the tab names that mod, which is evidence of a real clash. Errors that belong to no mod (BepInEx, the game itself) have their own ERRORS page beside MODS at the top. COPY MOD LIST includes them all.
- A mod that links its changelog gets a CHANGELOG tab next to CONFLICTS, which shows the changelog on the mod's page.
- A mod's page shows the version range of each mod it needs, and marks an installed version outside that range as wrong version.
- A mod's page lists the mods that need it (NEEDED BY) and the ones that can use it (USED BY (OPTIONAL)). A mod that needs it turns red while it is off.
- A mod with too many sections for one row of tabs lists them down the left side of its settings instead.
- Changing the view redraws only what changed, so the screen no longer flashes and replays its fade-in on every click.
- Fixed: a mod with an optional dependency lost its name, description, links and relations on the Mods screen, because its dependency could not be read.
- Fixed: a mod could be turned off while its update was downloading, which left its old and new DLLs in different folders and blocked ROLL BACK. While an update downloads, and after an install or rollback until the next start, the mod's switch is locked.
- Fixed: during a burst of errors too fast to read, the count of errors not shown left out the ones that were skipped.
- Update checks and changelogs read at most 4 MB from a server, so a broken changelog link cannot fill the game's memory.
- The Mod Manager's own entry shows its name in Chinese, the game version it was made for, and a link to this changelog.
- The Chinese description of the open key names the 「模组」 button as the game shows it in Chinese.

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

- First release.
- An in-game Mods screen lists every installed mod, shows whether it loaded, and changes its settings.
- Open it with F10, or the MODS button in the Esc menu and on the title screen.
- Needs Exiled's Toolkit 0.3.0 or later.
