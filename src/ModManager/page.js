// Mod Manager: the Mods screen, run in the game's root web document above every page.
// The C# side reads every plugin and its settings, checks and writes each value, and sends the result back through
// window.__modmanager; this script only draws and reports clicks. Data is put in the page through esc() or textContent,
// except a choice's picture, which is markup the mod drew for it.
(function () {
  if (window.__modmanager) return;

  const PREFIX = 'slmod|modmanager|';
  const STAGE_WIDTH = 1920;
  const STAGE_HEIGHT = 1080;
  const LANG_KEY = 'slmm-lang';

  const WORDS = {
    en: {
      loaded: 'LOADED', settings: 'SETTINGS', toggle: 'toggle', close: 'close', search: 'SEARCH EVERY SETTING',
      all: 'All', changed: 'Changed', groupLoaded: 'Loaded', groupNotLoaded: 'Not loaded', problems: 'Problems',
      nothing: 'NOTHING', noModMatches: 'No mod matches.', tryAnother: 'Try another word.',
      settingCount: n => n === 1 ? '1 setting' : n + ' settings', noSettings: 'no settings', notLoadedSub: 'not loaded',
      match: n => n === 1 ? '1 MATCH' : n + ' MATCHES', settingsMatch: q => 'SETTINGS MATCH "' + q + '"',
      statusLoaded: 'LOADED', statusNotLoaded: 'NOT LOADED', sinceStart: 'since the game started',
      found: 'Settings found in its BepInEx config.', foundNone: 'This mod has no settings to show.',
      openCfg: 'OPEN .CFG FOLDER', resetAll: 'RESET ALL TO DEFAULT', tabAll: 'All',
      notLoadedTitle: 'Not loaded.', notLoadedBody: 'BepInEx found this mod but did not start it, so its settings cannot be shown. The BepInEx log says why.',
      noSettingsTitle: 'NO SETTINGS', noSettingsBody: 'This mod has nothing to set.',
      mayRestart: 'MAY NEED RESTART', resetTo: v => 'Reset to ' + v, pressKey: 'PRESS A KEY', none: 'None',
      alsoUsed: list => 'Also used by ' + list + '.', readOnly: 'Read only',
      worldSet: v => 'This world keeps its own value. Every other world: ' + v + '.', worldGlobal: 'Changes here are for every world.',
      useGlobal: 'USE GLOBAL', keepWorld: 'THIS WORLD ONLY',
      footerNone: 'No changes this session. Everything saves the moment you change it.',
      footerSome: n => '<b>' + n + '</b> ' + (n === 1 ? 'setting' : 'settings') + ' changed this session, saved.',
      footerRestartHint: 'Some of them apply only after a restart: see the marks beside them.',
      groupOff: 'Off', offSub: 'off', statusOff: 'OFF', statusRequired: 'REQUIRED', pipRestart: 'RESTART',
      switchOnHint: 'turning it off applies next start', switchOffHint: 'turning it on applies next start',
      offNext: 'off from the next start', onNext: 'on from the next start',
      lockedSelf: 'this is the Mod Manager', lockedNeeded: 'the Mod Manager needs it', cannotMove: 'its DLL is outside the plugins folder', heldUntilRestart: 'its files change at the next start; restart first',
      by: 'by', neededBy: list => 'Needed by ' + list, linkNexus: 'NEXUS', linkGithub: 'GITHUB', linkWebsite: 'WEBSITE',
      offTitle: 'Turned off.', offBody: 'Its DLL is in a folder BepInEx skips, so it did not start and its settings cannot be shown. Its settings file is kept.',
      pendingOffTitle: 'Off from the next start.', pendingOffBody: 'It keeps running until you restart the game.',
      pendingOnTitle: 'On from the next start.', pendingOnBody: 'Restart the game to start it.',
      alsoStops: list => 'These need it and will not start either: ' + list + '.',
      footerRestart: n => (n === 1 ? '1 mod turns' : n + ' mods turn') + ' on or off at the next start.',
      updates: 'Updates', pipUpdate: 'UPDATE', getUpdate: 'GET UPDATE',
      updateTitle: tag => 'Version ' + tag + ' is out.', updateBody: version => 'You have ' + version + '.',
      updChecking: 'checking for updates', updCurrent: 'up to date', updNoReleases: 'no releases on GitHub yet',
      updFailed: 'could not check for updates', updUnreadable: tag => 'latest release ' + tag,
      whatsNew: "WHAT'S NEW", notesLoading: 'Loading the release notes.', notesEmpty: 'The releases since yours have no notes.',
      notesBusy: 'GitHub is busy for now (it limits how often anyone without an account can ask). The release page has the notes.',
      notesFailed: 'Could not load the release notes. The release page has them.',
      footerUpdates: n => (n === 1 ? '1 mod has' : n + ' mods have') + ' an update.',
      linkIssues: 'REPORT A PROBLEM', groupLibraries: 'Libraries', library: 'LIBRARY', needsRestart: 'NEEDS RESTART',
      categories: { Gameplay: 'Gameplay', Balance: 'Balance', QualityOfLife: 'Quality of life', Interface: 'Interface', Visuals: 'Visuals', Audio: 'Audio', Cheats: 'Cheats', Tools: 'Tools' },
      pipUntested: 'VERSION', untestedTitle: 'Not checked on this game version yet.',
      untestedBody: (known, game) => 'It is known to work on ' + known + (game ? ', and this game is ' + game : '') + '. It may still work.',
      untestedErrorsTitle: 'May need an update for this game version.',
      untestedErrorsBody: (known, game) => 'It is known to work on ' + known + (game ? ', and this game is ' + game : '') + '. It logged errors this session.',
      relNeeds: 'NEEDS', relWorksWith: 'WORKS WITH (OPTIONAL)', relNot: 'DOES NOT WORK WITH',
      relNeededBy: 'NEEDED BY', relUsedBy: 'USED BY (OPTIONAL)', hasVersion: (have, range) => have + ', needs ' + range,
      relStopsThem: 'Needs this mod, so it will not start while this one is off',
      relStatus: { missing: 'not installed', on: 'on', off: 'off', wrongVersion: 'wrong version' },
      tabSettings: 'Settings', tabConflicts: 'Conflicts', tabChangelog: 'Changelog', thisMod: 'This mod',
      tabErrors: 'Errors', errors: 'Errors', pipError: 'ERROR', pipWarning: 'WARNING', level: { error: 'ERROR', warning: 'WARNING' },
      errorsIntro: 'Warnings and errors this mod logged this session, newest first. They help the mod author find the cause: COPY MOD LIST at the bottom puts them in a bug report.',
      seenTimes: n => n === 1 ? 'once' : n + ' times', loggedBy: source => 'logged by ' + source,
      clashLine: method => 'It went through ' + method + ', which these mods also patch:',
      otherErrors: 'BepInEx and game errors',
      otherErrorsIntro: 'Warnings and errors this session that no mod could be matched to: from BepInEx, the game itself, or a mod that logged without its name. COPY MOD LIST at the bottom puts them in a bug report.',
      changelogNote: 'The published changelog. It can be ahead of the version you have.',
      changelogLoading: 'Loading the changelog.', changelogEmpty: 'The changelog is empty.',
      changelogFailed: 'Could not load the changelog. The CHANGELOG link above opens it in your browser.',
      conflictsIntro: 'Other mods that patch the same game code as this one. It is a guide for where to look when mods misbehave together, not proof of a problem. If two mods act up while both are on, try turning one off.',
      risk: { likely: 'LIKELY TO CLASH', may: 'MAY CLASH', harmless: 'PROBABLY HARMLESS' },
      riskWhy: {
        likely: 'Both mods rewrite the same game code, so one may break what the other expects to find.',
        may: 'At least one mod can skip or rewrite the game code. They usually work together, but one may change what the other expects.',
        harmless: 'Both mods only add their own code before or after the game code. That rarely causes trouble.'
      },
      effect: { before: 'runs before it', after: 'runs after it', skip: 'can skip it', rewrite: 'rewrites it' },
      sharedCount: n => n === 1 ? '1 shared' : n + ' shared',
      moreMethods: n => 'and ' + n + ' more',
      needsProblem: list => 'It needs these, which are off, not installed or the wrong version, so it will not start: ' + list + '.',
      clashProblem: list => 'It does not work with these, which are on, so it will not start: ' + list + '.',
      turnOnNeeds: 'TURN ON WHAT IT NEEDS',
      installUpdate: 'INSTALL UPDATE', tryAgain: 'TRY AGAIN', installing: 'Installing the update.',
      installed: tag => 'Version ' + tag + ' is installed. Restart the game to use it.',
      installFailed: reason => 'Could not install the update: ' + reason + '.',
      installRefused: reason => 'The Mod Manager will not install this update: ' + reason + '. GET UPDATE still lets you install it by hand.',
      installAll: n => 'INSTALL ALL UPDATES (' + n + ')',
      pipAsk: 'ASK', pipRefused: 'REFUSED', pipHeld: 'HELD', pipBroken: 'BROKEN', pipNexus: 'NEXUS',
      heldBroken: n => (n === 1 ? '1 update is' : n + ' updates are') + ' known not to work (marked HELD), so the Mod Manager will not install ' + (n === 1 ? 'it' : 'them') + '. Open ' + (n === 1 ? 'it' : 'one') + ' to see why.',
      heldUpdate: tag => 'Version ' + tag + ' is known not to work, so the Mod Manager will not install it:',
      brokenTitle: 'This version is known not to work.',
      nexusTitle: version => 'Version ' + version + ' is on Nexus Mods.',
      nexusBody: version => 'You have ' + version + '. The Mod Manager cannot install mods from Nexus Mods, so download it from its page there.',
      nexusCurrent: 'up to date on Nexus',
      pipOutdated: 'OUTDATED',
      fitTitle: 'May not work with this game version.',
      fitBody: (n, total) => n + ' of the ' + total + ' parts of the game code it uses ' + (n === 1 ? 'is' : 'are') + ' not in this game version, so what the mod does with ' + (n === 1 ? 'it' : 'them') + ' will fail until the mod is updated:',
      knownConflicts: 'Known not to work together',
      knownConflictsWhy: 'These mods are known to break each other. Keep only one of them on.',
      heldAsking: n => (n === 1 ? '1 update waits for you' : n + ' updates wait for you') + ' (marked ASK): it can do things the installed version cannot. Open it to see what, and click INSTALL ANYWAY if you trust where it comes from.',
      heldRefused: n => 'The Mod Manager will not install ' + (n === 1 ? '1 update' : n + ' updates') + ' (marked REFUSED). Open it to see why.',
      needsApproval: (tag, powers) => 'Version ' + tag + ' can do things the installed version cannot: it can ' + andList(powers) + '. Mods can have good reasons for this. Install it only if you trust where it comes from.',
      installAnyway: 'INSTALL ANYWAY',
      powers: { network: 'connect to the internet', programs: 'start other programs', native: 'call Windows or other native code directly', loadsCode: 'load code that is not in its DLL', registry: 'read or change the Windows registry', opens: 'open links or files in other programs', newDll: 'add another DLL file', unreadable: 'include a DLL the Mod Manager cannot read' },
      rollbackTitle: 'An earlier version is kept.', rollbackBody: version => 'The last update replaced ' + version + '. If this version gives you trouble, you can go back to it.',
      rollbackButton: version => 'ROLL BACK TO ' + version,
      rolledBack: version => 'Rolled back to ' + version + '. Restart the game to use it.',
      rollbackFailed: reason => 'Could not roll back: ' + reason + '.',
      advanced: 'Advanced', hotkeys: 'HOTKEYS', linkChangelog: 'CHANGELOG', remove: 'Remove',
      addItem: 'ADD ITEM', setByHand: 'SET BY HAND', swapItem: 'Pick another item', nothing: 'Nothing', ofCount: n => 'of ' + n,
      editText: 'Edit as text', blankText: '(blank)', noneText: '(nothing)', kinds: 'items', blankChooses: 'blank, the mod chooses',
      modCanUse: 'The mod can use this list.', modCannotUse: 'The mod cannot use this list as it is.',
      autoNote: 'Left blank, so the mod chooses this list.', byHandNote: 'Set by hand. Reset it to let the mod choose again.',
      profit: (back, made) => 'Worth more than what it takes to make (' + back + ' against ' + made + '): making it and taking it down makes a profit.',
      atBench: 'AT THE WORKBENCH', noMaterials: 'No materials yet', pieces: 'pieces', kgCarry: 'kg to carry', tradeValue: 'trade value',
      givesBack: 'Taking it down gives back', autoMark: 'auto', byHandMark: 'by hand',
      usedInRecipes: 'Used in recipes', everyItem: 'Every item', searchItems: n => 'Search ' + n + ' items by name, 中文 or id', used: 'used',
      addEntry: 'ADD', titleAddEntry: 'ADD TO THE LIST', swapEntry: 'Pick another', everything: 'Everything', haveShelf: 'You have', searchEntries: n => 'Search ' + n + ' by name or 中文',
      noEntryMatch: q => 'Nothing matches "' + q + '".', entriesFull: n => 'The list holds ' + n + ' already. Remove one first.',
      nothingChosen: 'Nothing in the list yet.', chosen: n => n + ' in the list', added: 'ADDED', notListed: 'Not one the mod lists now.',
      moveUp: 'Move up', moveDown: 'Move down',
      entriesWaiting: 'The mod has not listed what this can hold yet. Once a save is loaded it will.',
      pickAdd: 'ADD', pickSet: 'SET COUNT', pickSwap: 'SWAP IN', pickOne: 'PICK', pickIn: 'IN THE LIST',
      titleAdd: 'ADD AN ITEM', titleSwap: name => 'SWAP ' + name, titleOne: 'PICK AN ITEM',
      clickToSee: 'Click an item to see it here.', dblClickAdds: 'Double-click adds it at once',
      category: 'Category', weight: 'Weight', value: 'Value', recipes: 'Recipes',
      usesIn: n => n ? 'a material in ' + n + ' of the game\'s recipes' : 'not used by any of the game\'s recipes',
      listFull: n => n + ' items already. Remove one first.', noMatch: q => 'No item matches "' + q + '".',
      showing: (n, of) => 'Showing ' + n + ' of ' + of + '. Scroll for more, or search.',
      copyList: 'COPY MOD LIST', copied: 'COPIED', copyFailed: 'COULD NOT COPY', copyHint: 'Copies every mod with its version and state, and the errors this session, to paste into a bug report.',
      uninstall: 'UNINSTALL', restore: 'RESTORE', cancel: 'CANCEL', pipUninstalled: 'UNINSTALLED', statusUninstalled: 'UNINSTALLED', uninstalledNext: 'gone from the next start',
      uninstallTitle: name => 'Uninstall ' + name + '?',
      uninstallBody: 'Its DLL moves to the trash now, and the game keeps running it until you quit. RESTORE on the TRASH page puts it back.',
      prepOff: 'This mod can get your save ready to load without it: turn on its Uninstall Prep setting, save your game, then uninstall.',
      prepOn: 'Uninstall Prep is on. Save your game before you quit, so the save loads without this mod.',
      prepNotRunning: 'This mod can get saves ready to load without it, but only while it runs. To do that first, turn it on, restart the game and use its Uninstall Prep setting.',
      prepTurnOn: 'TURN ON UNINSTALL PREP', removeSettings: 'Also remove its settings file',
      uninstalledTitle: 'Uninstalled.', uninstalledBody: 'Its DLL is in the trash, so it will not start again. It keeps running until you quit the game. RESTORE puts it back.',
      pageTrash: 'TRASH', trashTitle: 'Uninstalled mods',
      trashIntro: 'Mods you uninstalled, with their settings file when you chose to remove it too. RESTORE puts a mod back where it was. EMPTY TRASH deletes them for good, except the ones uninstalled this session, which the game is still running.',
      trashEmpty: 'The trash is empty.', emptyTrash: 'EMPTY TRASH', emptyYes: 'DELETE FOR GOOD',
      emptyConfirm: n => 'Delete ' + n + (n === 1 ? ' mod' : ' mods') + ' for good? This cannot be undone.',
      trashSettings: 'with its settings file', trashThisSession: 'this session',
      pageHealth: 'HEALTH', healthTitle: 'Install check', healthNone: 'Nothing wrong was found.',
      healthAllClear: 'Nothing wrong was found: the install check is clean and nothing logged an error no mod could be matched to.',
      healthSum: { err: n => n + (n === 1 ? ' problem' : ' problems'), warn: n => n + (n === 1 ? ' warning' : ' warnings'), info: n => n + (n === 1 ? ' note' : ' notes') },
      pageBrowse: 'BROWSE', browseTitle: 'Mods for Survival Log',
      browseIntro: 'Every mod for the game on Nexus Mods. The ones set up for the Mod Manager install from here and get their updates in the Mods list; the rest you download from Nexus by hand.',
      browseOff: 'BROWSE needs the update checks, which are off. Turn on 「Check For Updates」 in the Mod Manager\'s settings to see the mods.',
      browseAsking: 'Asking Nexus Mods and the wiki for the list of mods.',
      browseNone: 'No mod matches.', browseFind: 'Search mods',
      bwAll: 'ALL', bwInstallable: 'ONE-CLICK INSTALL', bwManual: 'MANUAL DOWNLOAD', bwInstalledF: 'INSTALLED',
      bwSortEndorsed: 'MOST ENDORSED', bwSortDownloads: 'MOST DOWNLOADED', bwSortUpdated: 'RECENTLY UPDATED', bwSortName: 'NAME',
      bwInstalled: 'INSTALLED', bwInstall: 'INSTALL', bwOpenNexus: 'OPEN ON NEXUS', bwAsk: 'ASK THE AUTHOR',
      bwShameTitle: 'No Mod Manager support',
      bwShameBody: 'Manual download. Its author has not set it up for the Mod Manager yet, so you download it from Nexus and unpack it by hand, and it never tells you about updates. ASK THE AUTHOR copies a short request and opens the mod\'s posts on Nexus, where you can paste it.',
      bwAsked: 'The request is copied. Paste it in the mod\'s posts on Nexus.', bwAskFailed: 'Could not copy the request. The mod\'s posts are open on Nexus.',
      bwNoIndex: 'The wiki\'s list of mods set up for the Mod Manager could not be read, so nothing here can be installed and no mod is marked as a manual download. The Mod Manager asks for it again the next time it checks for updates.',
      bwUnknown: 'SUPPORT NOT KNOWN',
      bwInstalling: 'Installing.',
      bwDone: tag => 'Installed ' + tag + '. The game starts it from the next start.',
      bwFailed: reason => 'Could not install it: ' + reason + '.',
      bwRefused: reason => 'The Mod Manager will not install it: ' + reason + '.',
      bwApproval: (tag, powers) => 'Version ' + tag + ' comes from its author\'s GitHub, and its code can ' + andList(powers) + '. Mods can have good reasons for this. Install it only if you trust where it comes from.',
      bwNeeds: names => 'It also needs ' + andList(names) + ', which you download by hand.',
      bwEndorsed: n => n + ' endorsements', bwDownloads: n => n + ' downloads',
      pendNew: 'Installed, starts from the next start',
      pageProfiles: 'PROFILES', pfTitle: 'Profiles',
      pfIntro: 'A profile is a setup you can go back to: which mods are on and which are off, and every setting you changed. Mods turn on and off from the next start; settings change at once. COPY CODE gives a short code to share, and anyone with the Mod Manager can paste it here to get the same setup, with the mods it needs installed from BROWSE.',
      pfName: 'Name this setup', pfSave: 'SAVE THIS SETUP', pfNone: 'No profiles yet. Save your setup to make one.',
      pfFacts: (mods, on, settings) => mods + (mods === 1 ? ' mod, ' : ' mods, ') + on + ' on, ' + settings + (settings === 1 ? ' changed setting' : ' changed settings'),
      pfShow: 'SEE CHANGES', pfCopy: 'COPY CODE', pfDelete: 'DELETE', pfDeleteYes: 'DELETE PROFILE',
      pfDeleteAsk: name => 'Delete the profile "' + name + '"?',
      pfPasteTitle: 'Paste a code', pfPasteHint: 'A profile code someone shared, or a list from COPY MOD LIST.',
      pfChanges: name => 'What "' + name + '" changes',
      pfListOnly: 'This is a mod list, so it only turns mods on and off and leaves your settings alone.',
      pfNothing: 'Your setup already matches it.',
      pfOn: 'Turn on from the next start', pfOff: 'Turn off from the next start', pfInstall: 'Install from BROWSE',
      pfMissing: 'Not installed: download by hand', pfSet: 'Settings that change now', pfWait: 'Settings set once the mod runs',
      pfWaitRow: (name, n) => name + ': ' + n + (n === 1 ? ' setting' : ' settings') + '. Use the profile again after the next start.',
      pfVersion: 'Another version installed', pfVersionRow: (name, have, wanted) => name + ': you have ' + have + ', the profile has ' + wanted + '.',
      pfStays: 'Cannot be turned off here', pfBlank: '(blank)',
      pfUse: 'USE THIS SETUP', pfKeep: 'SAVE AS PROFILE', pfClose: 'CLOSE',
      healthIntro: 'Problems with how BepInEx and the mods are installed, found by looking at the game folder each time this screen opens.',
      hBepInEx: v => 'BepInEx ' + v + ' is installed',
      hBepInExBody: v => 'These mods are made and tested on BepInEx ' + v + ' for IL2CPP games. Another build can keep mods from starting or cause errors. If mods misbehave, install that build.',
      hToolkit: name => name + ' needs a newer Exiled\'s Toolkit',
      hToolkitBody: (range, installed) => 'It needs ' + range + ', and you have version ' + installed + ', so BepInEx will not start it. Update Exiled\'s Toolkit.',
      hSameGuid: guid => 'One mod is in more than one DLL: ' + guid,
      hSameGuidBody: 'BepInEx starts only one of them, and it may not be the newest. Keep one and delete the others:',
      hSameDll: 'The same DLL is installed twice',
      hSameDllBody: 'Only one copy is used, and it may be the older one. Keep one and delete the others:',
      hNested: 'This folder should not be in plugins',
      hNestedBody: 'This usually means a mod was unpacked one folder too deep. Its DLLs still start, but anything in it meant for BepInEx/config or BepInEx/patchers does nothing there. Move what is inside to the right folders:',
      hUnused: 'Settings files without a mod',
      hUnusedBody: 'No installed mod goes by these names, so they are most likely left from mods you removed. They do nothing and can be deleted from BepInEx/config:',
      pendTitle: n => (n === 1 ? '1 change waits' : n + ' changes wait') + ' for the next start.',
      pendShow: 'SHOW', pendHide: 'HIDE', undo: 'UNDO', quitGame: 'QUIT GAME',
      pendOn: 'turns on', pendOff: 'turns off', pendUninstalled: 'uninstalled',
      pendUpdate: tag => 'update to ' + tag + ' installed', pendRollback: version => 'rolled back to ' + version,
      pendSetting: label => label + ' changed, applies after a restart', pendSettingMaybe: label => label + ' changed, may apply only after a restart',
      quitTitle: 'Quit the game now?', quitBody: 'The game saves your run as it does when you close its window. Start it again to use the changes.', quitYes: 'QUIT NOW'
    },
    zh: {
      loaded: '已加载', settings: '项设置', toggle: '切换', close: '关闭', search: '搜索所有设置',
      all: '全部', changed: '已修改', groupLoaded: '已加载', groupNotLoaded: '未加载', problems: '问题',
      nothing: '无结果', noModMatches: '没有匹配的模组。', tryAnother: '换个词试试。',
      settingCount: n => n + ' 项设置', noSettings: '无设置', notLoadedSub: '未加载',
      match: n => n + ' 项匹配', settingsMatch: q => '项设置匹配「' + q + '」',
      statusLoaded: '已加载', statusNotLoaded: '未加载', sinceStart: '自游戏启动起',
      found: '设置读取自它的 BepInEx 配置。', foundNone: '这个模组没有可显示的设置。',
      openCfg: '打开 .CFG 文件夹', resetAll: '全部恢复默认', tabAll: '全部',
      notLoadedTitle: '未加载。', notLoadedBody: 'BepInEx 找到了这个模组，但没有启动它，所以无法显示它的设置。原因见 BepInEx 日志。',
      noSettingsTitle: '无设置', noSettingsBody: '这个模组没有可以设置的内容。',
      mayRestart: '可能需要重启', resetTo: v => '恢复为 ' + v, pressKey: '请按一个键', none: '无',
      alsoUsed: list => '也被 ' + list + ' 使用。', readOnly: '只读',
      worldSet: v => '这个存档使用自己的值。其他存档：' + v + '。', worldGlobal: '这里的改动对所有存档生效。',
      useGlobal: '使用全局', keepWorld: '仅限这个存档',
      footerNone: '本次没有修改。每项设置一改就会保存。',
      footerSome: n => '本次修改了 <b>' + n + '</b> 项设置，已保存。',
      footerRestartHint: '其中有些需要重启游戏才会生效：见设置旁的标记。',
      groupOff: '已关闭', offSub: '已关闭', statusOff: '已关闭', statusRequired: '必需', pipRestart: '重启',
      switchOnHint: '关闭将在下次启动时生效', switchOffHint: '开启将在下次启动时生效',
      offNext: '下次启动时关闭', onNext: '下次启动时开启',
      lockedSelf: '这就是模组管理器', lockedNeeded: '模组管理器需要它', cannotMove: '它的 DLL 不在 plugins 文件夹中', heldUntilRestart: '它的文件将在下次启动时更换，请先重启游戏',
      by: '作者', neededBy: list => '被以下模组需要：' + list, linkNexus: 'NEXUS', linkGithub: 'GITHUB', linkWebsite: '网站',
      offTitle: '已关闭。', offBody: '它的 DLL 在 BepInEx 会跳过的文件夹中，所以没有启动，也无法显示设置。它的设置文件会保留。',
      pendingOffTitle: '下次启动时关闭。', pendingOffBody: '在重启游戏之前它会继续运行。',
      pendingOnTitle: '下次启动时开启。', pendingOnBody: '重启游戏后它就会启动。',
      alsoStops: list => '以下模组需要它，也不会启动：' + list + '。',
      footerRestart: n => n + ' 个模组将在下次启动时开启或关闭。',
      updates: '可更新', pipUpdate: '更新', getUpdate: '获取更新',
      updateTitle: tag => '新版本 ' + tag + ' 已发布。', updateBody: version => '你的版本是 ' + version + '。',
      updChecking: '正在检查更新', updCurrent: '已是最新', updNoReleases: 'GitHub 上还没有发布版本',
      updFailed: '无法检查更新', updUnreadable: tag => '最新发布 ' + tag,
      whatsNew: '更新内容', notesLoading: '正在加载更新说明。', notesEmpty: '你的版本之后的发布都没有更新说明。',
      notesBusy: 'GitHub 暂时繁忙（它限制了未登录时的查询次数）。发布页面上有更新说明。',
      notesFailed: '无法加载更新说明。发布页面上有。',
      footerUpdates: n => n + ' 个模组有更新。',
      linkIssues: '反馈问题', groupLibraries: '前置库', library: '前置库', needsRestart: '需要重启',
      categories: { Gameplay: '玩法', Balance: '平衡', QualityOfLife: '便利', Interface: '界面', Visuals: '画面', Audio: '音频', Cheats: '作弊', Tools: '工具' },
      pipUntested: '版本', untestedTitle: '尚未在此游戏版本上验证。',
      untestedBody: (known, game) => '已知可在 ' + known + ' 上运行' + (game ? '，当前游戏是 ' + game : '') + '。它可能仍可正常运行。',
      untestedErrorsTitle: '可能需要为此游戏版本更新。',
      untestedErrorsBody: (known, game) => '已知可在 ' + known + ' 上运行' + (game ? '，当前游戏是 ' + game : '') + '。本次运行中它记录了错误。',
      relNeeds: '需要', relWorksWith: '可配合（可选）', relNot: '不兼容',
      relNeededBy: '被这些模组需要', relUsedBy: '可被这些模组使用（可选）', hasVersion: (have, range) => have + '，需要 ' + range,
      relStopsThem: '它需要本模组，本模组关闭时它不会启动',
      relStatus: { missing: '未安装', on: '已开启', off: '已关闭', wrongVersion: '版本不符' },
      tabSettings: '设置', tabConflicts: '冲突', tabChangelog: '更新日志', thisMod: '本模组',
      tabErrors: '错误', errors: '错误', pipError: '错误', pipWarning: '警告', level: { error: '错误', warning: '警告' },
      errorsIntro: '本模组在这次游戏中记录的警告和错误，最新的在前。它们能帮模组作者找到原因：底部的「复制模组列表」会把它们放进问题反馈。',
      seenTimes: n => n === 1 ? '1 次' : n + ' 次', loggedBy: source => '来源：' + source,
      clashLine: method => '它经过了 ' + method + '，以下模组也修改了这段代码：',
      otherErrors: 'BepInEx 和游戏的错误',
      otherErrorsIntro: '这次游戏中无法对应到某个模组的警告和错误：来自 BepInEx、游戏本身，或没有写明名字的模组。底部的「复制模组列表」会把它们放进问题反馈。',
      changelogNote: '这是已发布的更新日志，可能比你安装的版本更新。',
      changelogLoading: '正在加载更新日志。', changelogEmpty: '更新日志是空的。',
      changelogFailed: '无法加载更新日志。上方的「更新日志」链接可以在浏览器中打开它。',
      conflictsIntro: '与本模组修改了相同游戏代码的其他模组。这只是模组一起出问题时的排查线索，不代表一定有问题。如果两个模组同时开启时出现异常，试着关闭其中一个。',
      risk: { likely: '很可能冲突', may: '可能冲突', harmless: '大概无害' },
      riskWhy: {
        likely: '两个模组都改写了同一段游戏代码，一方可能会破坏另一方所依赖的内容。',
        may: '至少有一个模组能跳过或改写这段游戏代码。它们通常可以一起使用，但一方可能会改变另一方所预期的结果。',
        harmless: '两个模组都只是在这段游戏代码之前或之后加入自己的代码，很少引起问题。'
      },
      effect: { before: '在它之前运行', after: '在它之后运行', skip: '可以跳过它', rewrite: '改写了它' },
      sharedCount: n => '共用 ' + n + ' 处',
      moreMethods: n => '另外 ' + n + ' 处',
      needsProblem: list => '它需要的以下模组已关闭、未安装或版本不符，所以它不会启动：' + list + '。',
      clashProblem: list => '它与以下已开启的模组不兼容，所以它不会启动：' + list + '。',
      turnOnNeeds: '开启它需要的模组',
      installUpdate: '安装更新', tryAgain: '重试', installing: '正在安装更新。',
      installed: tag => '版本 ' + tag + ' 已安装。重启游戏后生效。',
      installFailed: reason => '无法安装更新：' + reason + '。',
      installRefused: reason => '模组管理器不会安装此更新：' + reason + '。仍可通过「获取更新」手动安装。',
      installAll: n => '安装全部更新（' + n + '）',
      pipAsk: '待确认', pipRefused: '已拒绝', pipHeld: '已暂缓', pipBroken: '已知故障', pipNexus: 'NEXUS',
      heldBroken: n => n + ' 个更新已知无法正常工作（标为「已暂缓」），模组管理器不会安装。打开查看原因。',
      heldUpdate: tag => '版本 ' + tag + ' 已知无法正常工作，模组管理器不会安装它：',
      brokenTitle: '此版本已知无法正常工作。',
      nexusTitle: version => 'Nexus Mods 上已有版本 ' + version + '。',
      nexusBody: version => '你的版本是 ' + version + '。模组管理器无法从 Nexus Mods 安装模组，请到它在那里的页面下载。',
      nexusCurrent: 'Nexus 上已是最新',
      pipOutdated: '可能过时',
      fitTitle: '可能不适用于此游戏版本。',
      fitBody: (n, total) => '它用到的 ' + total + ' 处游戏代码中，有 ' + n + ' 处在此游戏版本中已不存在。在模组更新之前，用到它们的功能会失败：',
      knownConflicts: '已知不能一起使用',
      knownConflictsWhy: '这些模组已知会互相破坏。只保留其中一个开启。',
      heldAsking: n => n + ' 个更新等你确认（标为「待确认」）：它能做到当前版本做不到的事。打开查看详情，信任它的来源时点「仍然安装」。',
      heldRefused: n => '模组管理器不会安装 ' + n + ' 个更新（标为「已拒绝」）。打开查看原因。',
      needsApproval: (tag, powers) => '版本 ' + tag + ' 能做到当前版本做不到的事：它可以' + powers.join('、') + '。模组这样做可能有正当理由。只有在信任它的来源时才安装。',
      installAnyway: '仍然安装',
      powers: { network: '连接互联网', programs: '启动其他程序', native: '直接调用 Windows 或其他原生代码', loadsCode: '加载不在其 DLL 中的代码', registry: '读取或修改 Windows 注册表', opens: '用其他程序打开链接或文件', newDll: '添加另一个 DLL 文件', unreadable: '包含一个模组管理器无法读取的 DLL' },
      rollbackTitle: '保留了一个旧版本。', rollbackBody: version => '上次更新替换了 ' + version + '。如果这个版本有问题，可以退回到它。',
      rollbackButton: version => '退回到 ' + version,
      rolledBack: version => '已退回到 ' + version + '。重启游戏后生效。',
      rollbackFailed: reason => '无法退回：' + reason + '。',
      advanced: '高级', hotkeys: '快捷键', linkChangelog: '更新日志', remove: '移除',
      addItem: '添加物品', setByHand: '手动设置', swapItem: '换一个物品', nothing: '无', ofCount: n => '共 ' + n,
      editText: '以文本编辑', blankText: '（留空）', noneText: '（无）', kinds: '种', blankChooses: '留空，由模组决定',
      modCanUse: '模组可以使用这份清单。', modCannotUse: '模组无法按现在的样子使用这份清单。',
      autoNote: '留空，由模组决定这份清单。', byHandNote: '已手动设置。重置后由模组重新决定。',
      profit: (back, made) => '返还价值高于制作所需（' + back + ' 对 ' + made + '）：做了再拆可以赚钱。',
      atBench: '工作台', noMaterials: '还没有材料', pieces: '件数', kgCarry: '公斤', tradeValue: '交易价值',
      givesBack: '拆除后返还', autoMark: '自动', byHandMark: '手动',
      usedInRecipes: '常用材料', everyItem: '全部物品', searchItems: n => '按名称、英文或 id 搜索 ' + n + ' 种物品', used: '已用',
      addEntry: '添加', titleAddEntry: '加入清单', swapEntry: '换一个', everything: '全部', haveShelf: '你有的', searchEntries: n => '按名称或英文搜索 ' + n + ' 项',
      noEntryMatch: q => '没有与「' + q + '」匹配的项。', entriesFull: n => '清单里已有 ' + n + ' 项。请先移除一项。',
      nothingChosen: '清单里还没有内容。', chosen: n => '清单里有 ' + n + ' 项', added: '已添加', notListed: '模组现在没有列出这一项。',
      moveUp: '上移', moveDown: '下移',
      entriesWaiting: '模组还没有列出可选的内容。载入存档后就会列出。',
      pickAdd: '添加', pickSet: '设置数量', pickSwap: '替换', pickOne: '选择', pickIn: '已在清单中',
      titleAdd: '添加物品', titleSwap: name => '替换 ' + name, titleOne: '选择物品',
      clickToSee: '点击物品可在这里查看。', dblClickAdds: '双击直接添加',
      category: '类别', weight: '重量', value: '价值', recipes: '配方',
      usesIn: n => n ? '游戏中 ' + n + ' 个配方的材料' : '游戏配方都不用它',
      listFull: n => '已经有 ' + n + ' 种物品，请先移除一种。', noMatch: q => '没有物品符合「' + q + '」。',
      showing: (n, of) => '已显示 ' + n + ' / ' + of + '，继续滚动或搜索。',
      copyList: '复制模组列表', copied: '已复制', copyFailed: '无法复制', copyHint: '复制所有模组及其版本和状态，以及这次游戏的错误，可粘贴到问题反馈中。',
      uninstall: '卸载', restore: '恢复', cancel: '取消', pipUninstalled: '已卸载', statusUninstalled: '已卸载', uninstalledNext: '下次启动时移除',
      uninstallTitle: name => '卸载 ' + name + '？',
      uninstallBody: '它的 DLL 现在会移到回收站，在你退出游戏之前游戏会继续运行它。在「回收站」页面点「恢复」可以把它放回去。',
      prepOff: '这个模组可以让你的存档在没有它时也能读取：先开启它的「Uninstall Prep」设置，保存游戏，然后再卸载。',
      prepOn: '「Uninstall Prep」已开启。退出前请保存游戏，这样存档在没有这个模组时也能读取。',
      prepNotRunning: '这个模组可以让存档在没有它时也能读取，但只能在它运行时做到。如果要先这样做，请开启它，重启游戏，然后使用它的「Uninstall Prep」设置。',
      prepTurnOn: '开启 UNINSTALL PREP', removeSettings: '同时移除它的设置文件',
      uninstalledTitle: '已卸载。', uninstalledBody: '它的 DLL 在回收站中，所以不会再启动。在你退出游戏之前它会继续运行。「恢复」可以把它放回去。',
      pageTrash: '回收站', trashTitle: '已卸载的模组',
      trashIntro: '你卸载的模组，以及你选择一起移除的设置文件。「恢复」会把模组放回原处。「清空回收站」会永久删除它们，但这次卸载的除外，因为游戏仍在运行它们。',
      trashEmpty: '回收站是空的。', emptyTrash: '清空回收站', emptyYes: '永久删除',
      emptyConfirm: n => '永久删除 ' + n + ' 个模组？此操作无法撤销。',
      trashSettings: '含设置文件', trashThisSession: '这次卸载',
      pageHealth: '健康', healthTitle: '安装检查', healthNone: '没有发现问题。',
      healthAllClear: '没有发现问题：安装检查正常，也没有无法对应到模组的错误。',
      healthSum: { err: n => n + ' 个问题', warn: n => n + ' 个警告', info: n => n + ' 条提示' },
      pageBrowse: '浏览', browseTitle: 'Survival Log 的模组',
      browseIntro: 'Nexus Mods 上本游戏的所有模组。支持模组管理器的模组可以在这里安装，并在模组列表中获得更新；其他的需要从 Nexus 手动下载。',
      browseOff: '浏览需要开启更新检查，而它现在是关闭的。在模组管理器的设置中开启「Check For Updates」即可查看模组。',
      browseAsking: '正在向 Nexus Mods 和维基获取模组列表。',
      browseNone: '没有匹配的模组。', browseFind: '搜索模组',
      bwAll: '全部', bwInstallable: '一键安装', bwManual: '手动下载', bwInstalledF: '已安装',
      bwSortEndorsed: '最多点赞', bwSortDownloads: '最多下载', bwSortUpdated: '最近更新', bwSortName: '名称',
      bwInstalled: '已安装', bwInstall: '安装', bwOpenNexus: '在 NEXUS 打开', bwAsk: '请作者支持',
      bwShameTitle: '不支持模组管理器',
      bwShameBody: '需要手动下载。作者还没有为模组管理器做好设置，所以你得从 Nexus 下载并手动解压，而且它不会提示更新。「请作者支持」会复制一段简短的请求，并打开该模组在 Nexus 上的帖子页面，你可以把请求粘贴到那里。',
      bwAsked: '请求已复制。请粘贴到该模组在 Nexus 上的帖子里。', bwAskFailed: '无法复制请求。该模组在 Nexus 上的帖子页面已打开。',
      bwNoIndex: '无法读取维基上支持模组管理器的模组列表，所以这里暂时无法安装，也不会把任何模组标为手动下载。模组管理器下次检查更新时会再次获取。',
      bwUnknown: '是否支持未知',
      bwInstalling: '正在安装。',
      bwDone: tag => '已安装 ' + tag + '。下次启动游戏时生效。',
      bwFailed: reason => '无法安装：' + reason + '。',
      bwRefused: reason => '模组管理器不会安装它：' + reason + '。',
      bwApproval: (tag, powers) => '版本 ' + tag + ' 来自作者的 GitHub，它的代码可以' + powers.join('、') + '。模组这样做可能有正当理由。只有在信任它的来源时才安装。',
      bwNeeds: names => '它还需要' + names.join('、') + '，需要手动下载。',
      bwEndorsed: n => n + ' 个点赞', bwDownloads: n => n + ' 次下载',
      pendNew: '已安装，下次启动时生效',
      pageProfiles: '配置方案', pfTitle: '配置方案',
      pfIntro: '配置方案是一套可以随时换回去的配置：哪些模组开启、哪些关闭，以及你改过的每项设置。模组的开关从下次启动起生效，设置立即生效。「复制代码」会生成一段可分享的短代码，任何使用模组管理器的人都可以把它粘贴到这里，得到同样的配置，所需的模组从「浏览」安装。',
      pfName: '为当前配置命名', pfSave: '保存当前配置', pfNone: '还没有配置方案。保存当前配置即可创建一个。',
      pfFacts: (mods, on, settings) => mods + ' 个模组，' + on + ' 个开启，' + settings + ' 项已改设置',
      pfShow: '查看变化', pfCopy: '复制代码', pfDelete: '删除', pfDeleteYes: '删除配置方案',
      pfDeleteAsk: name => '删除配置方案「' + name + '」？',
      pfPasteTitle: '粘贴代码', pfPasteHint: '别人分享的配置方案代码，或「复制模组列表」复制的列表。',
      pfChanges: name => '「' + name + '」会改变的内容',
      pfListOnly: '这是模组列表，只会开关模组，不会改动你的设置。',
      pfNothing: '你的配置已经与它一致。',
      pfOn: '下次启动起开启', pfOff: '下次启动起关闭', pfInstall: '从「浏览」安装',
      pfMissing: '未安装：需要手动下载', pfSet: '立即更改的设置', pfWait: '模组运行后才能设置',
      pfWaitRow: (name, n) => name + '：' + n + ' 项设置。下次启动后请再使用一次此配置方案。',
      pfVersion: '安装的版本不同', pfVersionRow: (name, have, wanted) => name + '：你的是 ' + have + '，配置方案中是 ' + wanted + '。',
      pfStays: '无法在这里关闭', pfBlank: '（空）',
      pfUse: '使用此配置', pfKeep: '保存为配置方案', pfClose: '关闭',
      healthIntro: '每次打开这个界面时都会查看游戏文件夹，列出 BepInEx 和模组在安装上的问题。',
      hBepInEx: v => '安装的是 BepInEx ' + v,
      hBepInExBody: v => '这些模组是在适用于 IL2CPP 游戏的 BepInEx ' + v + ' 上制作和测试的。其他版本可能让模组无法启动或出错。如果模组出现问题，请安装这个版本。',
      hToolkit: name => name + ' 需要更新版本的 Exiled\'s Toolkit',
      hToolkitBody: (range, installed) => '它需要 ' + range + '，而你的版本是 ' + installed + '，所以 BepInEx 不会启动它。请更新 Exiled\'s Toolkit。',
      hSameGuid: guid => '同一个模组出现在多个 DLL 中：' + guid,
      hSameGuidBody: 'BepInEx 只会启动其中一个，而且可能不是最新的。保留一个，删除其他的：',
      hSameDll: '同一个 DLL 安装了两次',
      hSameDllBody: '只会用到其中一份，而且可能是旧的那份。保留一个，删除其他的：',
      hNested: '这个文件夹不应该在 plugins 里面',
      hNestedBody: '这通常说明模组解压时多了一层文件夹。里面的 DLL 仍会启动，但本该放进 BepInEx/config 或 BepInEx/patchers 的文件放在这里不起作用。请把里面的内容移到正确的文件夹：',
      hUnused: '没有对应模组的设置文件',
      hUnusedBody: '没有已安装的模组用到这些名字，所以它们很可能是已移除的模组留下的。它们不起作用，可以从 BepInEx/config 删除：',
      pendTitle: n => n + ' 项改动将在下次启动时生效。',
      pendShow: '展开', pendHide: '收起', undo: '撤销', quitGame: '退出游戏',
      pendOn: '将开启', pendOff: '将关闭', pendUninstalled: '已卸载',
      pendUpdate: tag => '已安装更新 ' + tag, pendRollback: version => '已退回到 ' + version,
      pendSetting: label => '「' + label + '」已修改，重启后生效', pendSettingMaybe: label => '「' + label + '」已修改，可能需要重启才生效',
      quitTitle: '现在退出游戏？', quitBody: '游戏会像关闭窗口时一样保存你的进度。重新启动游戏即可使用这些改动。', quitYes: '立即退出'
    }
  };

  const state = {
    view: null,
    page: 'mods',
    selected: null,
    filter: 'all',
    query: '',
    lang: 'en',
    tab: '',
    binding: null,
    pending: null,
    notes: {},
    isTyping: false,
    rows: [],
    modNote: null,
    notesAsked: {},
    changelogAsked: {},
    icons: {},
    banners: {},
    items: null,
    // The entries of each List setting, by the setting's catalog name.
    entries: {},
    picker: null,
    drafts: {},
    editing: null,
    drag: null,
    flash: null,
    openAdvanced: {},
    side: '',
    rail: {},
    copied: null,
    showLibraries: false,
    ownShown: null,
    ownFooter: null,
    // The mod whose UNINSTALL waits for the player to confirm, and whether its settings file goes too.
    confirmUninstall: null,
    removeSettings: false,
    confirmEmpty: false,
    // The bar of changes that wait for the next start: whether its list is open, and whether QUIT GAME asks to confirm.
    pendOpen: true,
    confirmQuit: false,
    pendItems: [],
    // Set while a trash action is on its way, so its note shows on the TRASH page.
    noteFor: '',
    // BROWSE: what the player searched, which mods and which order, and the mod whose request was last copied.
    browseQuery: '',
    browseFilter: 'all',
    bwShameOpen: {},
    browseSort: 'endorsed',
    asked: null,
    // PROFILES: the profile whose DELETE asks first, and the one whose code was last copied.
    pfDelete: null,
    pfCopied: null
  };

  const LINK_WORDS = { nexus: 'linkNexus', github: 'linkGithub', website: 'linkWebsite', issues: 'linkIssues', changelog: 'linkChangelog' };
  const PICKER_PAGE = 300;
  // The picker shelf of the entries the mod says the player has. Other shelf ids are the mod's group names.
  const HAVE = '~have';
  const DRAFT_MS = 300;
  const NO_ITEMS = 'none';
  const COPIED_MS = 2500;

  // ===== Talking to the C# side =====

  function send(verb, parts) {
    const text = PREFIX + [verb].concat((parts || []).map(encodeURIComponent)).join('|');

    try {
      if (window.vuplex) window.vuplex.postMessage(text);
    } catch (e) {
      // Nothing to fall back to: without the bridge the screen cannot change anything.
    }
  }

  function sendReady() {
    if (window.vuplex) send('ready');
    else window.addEventListener('vuplexready', () => send('ready'));
  }

  function settingParts(row) {
    return [row.mod.guid, row.setting.section, row.setting.key];
  }

  // ===== Small helpers =====

  const esc = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const word = key => WORDS[state.lang][key];
  const isChanged = s => s.current !== s.default;
  const isChangedThisSession = s => s.current !== s.atStart;
  const hasUpdate = m => m.update.kind === 'available';
  const isHeld = m => hasUpdate(m) && !!m.known.updateHeld;
  const canInstall = m => hasUpdate(m) && !isHeld(m) && (m.install.kind === 'none' || m.install.kind === 'failed') && m.rollback.kind !== 'done';
  // A newer version on Nexus Mods, for a mod the update check has nothing for: shown, not installed.
  const hasNexusUpdate = m => !hasUpdate(m) && !!m.known.nexus && m.known.nexus.newer;
  const isUpdatable = m => hasUpdate(m) || hasNexusUpdate(m);
  const tagText = tag => /^[vV]\d/.test(tag) ? tag.slice(1) : tag;
  const settingId = (mod, s) => mod.guid + '\n' + s.section + '\n' + s.key;
  const mods = () => (state.view ? state.view.mods : []);
  const modByGuid = guid => mods().find(m => m.guid === guid);
  const nameOf = m => (state.lang === 'zh' ? m.nameZh : m.name);
  const isLibraryShelf = m => m.library && m.state === 'loaded';

  // The mod's own icon when it embeds one, else its initials on a colour taken from its name.
  function badge(m, cls) {
    const icon = state.icons[m.guid];

    if (icon) return '<div class="' + cls + ' has-icon"><img src="' + esc(icon) + '" alt=""></div>';

    return '<div class="' + cls + '" style="' + monoStyle(m.name) + '">' + esc(initials(m.name)) + '</div>';
  }

  // Whether the mod will run from the next start, which is when what it needs has to be there.
  function isOnNext(m) {
    if (m.switch.kind === 'allowed' || m.switch.kind === 'held') return m.switch.next;

    if (m.switch.kind === 'uninstalled') return false;

    return m.switch.kind === 'locked' || m.state !== 'off';
  }

  function relationProblems(m) {
    if (!isOnNext(m)) return { needs: [], clashes: [] };

    return {
      needs: m.relations.filter(r => r.kind === 'needs' && r.status !== 'on'),
      clashes: m.relations.filter(r => r.kind === 'doesNotWorkWith' && r.status === 'on')
    };
  }

  function hue(name) {
    let h = 0;

    for (const c of name) h = (h * 31 + c.charCodeAt(0)) % 360;

    return h;
  }

  function initials(name) {
    const words = name.replace(/'s\b/g, '').replace(/([a-z])([A-Z])/g, '$1 $2').split(/[\s_.-]+/).filter(Boolean);

    return words.map(w => w[0]).join('').slice(0, 2).toUpperCase() || '?';
  }

  const monoStyle = name => 'background:linear-gradient(135deg,hsl(' + hue(name) + ' 45% 32%),hsl(' + ((hue(name) + 40) % 360) + ' 40% 16%))';

  // A setting a show-when rule hides is still sent, so Reset All still resets it, but it is not drawn.
  const shownSettings = mod => mod.settings.filter(s => s.shown !== false);

  function sections(mod) {
    const order = [];

    for (const s of shownSettings(mod)) if (order.indexOf(s.section) < 0) order.push(s.section);

    return order;
  }

  function matchesQuery(mod, s, q) {
    return (s.label + ' ' + s.key + ' ' + s.section + ' ' + s.en + ' ' + s.zh).toLowerCase().indexOf(q) >= 0;
  }

  // ===== Style =====

  const style = document.createElement('style');
  style.id = 'slmm-style';
  style.textContent = `
#slmm { --amber: #EBA308; --teal: #8fc7bf; --red: #ff5a5a; --ink: #121214; --line: rgba(255,255,255,0.08);
  --mono: Consolas, "Cascadia Mono", monospace; --display: Impact, "Arial Black", "Microsoft YaHei", sans-serif; --body: "Microsoft YaHei", "Noto Sans SC", sans-serif;
  position: fixed; inset: 0; z-index: 2147482000; display: none; background: rgba(0,0,0,0.6); font-family: var(--body); color: #d6d6d6; user-select: none; }
#slmm.on { display: block; animation: slmm-fade .2s ease; }
@keyframes slmm-fade { from { opacity: 0; } }
#slmm * { box-sizing: border-box; margin: 0; padding: 0; }
#slmm .stage { position: absolute; left: 50%; top: 50%; width: ${STAGE_WIDTH}px; height: ${STAGE_HEIGHT}px; display: flex; align-items: center; justify-content: center; transform-origin: center center; }
#slmm .win { position: relative; width: 1640px; height: 940px; display: grid; grid-template-rows: 70px 1fr 56px; background: var(--ink); background-image: linear-gradient(rgba(255,255,255,0.02) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.02) 1px, transparent 1px); background-size: 20px 20px; border: 1px solid #3a3a3a; box-shadow: 0 30px 80px rgba(0,0,0,0.85), 0 0 0 1px rgba(255,255,255,0.05); animation: slmm-pop .3s cubic-bezier(.2,.8,.2,1); }
@keyframes slmm-pop { from { transform: scale(.96); } }
#slmm .corner { position: absolute; width: 18px; height: 18px; border-color: #777; border-style: solid; pointer-events: none; z-index: 5; }
#slmm .tl { top: -1px; left: -1px; border-width: 2px 0 0 2px; } #slmm .tr { top: -1px; right: -1px; border-width: 2px 2px 0 0; }
#slmm .bl { bottom: -1px; left: -1px; border-width: 0 0 2px 2px; } #slmm .br { bottom: -1px; right: -1px; border-width: 0 2px 2px 0; }

#slmm .hd { display: flex; align-items: center; gap: 22px; padding: 0 30px; background: rgba(255,255,255,0.03); border-bottom: 1px solid rgba(255,255,255,0.1); }
#slmm .hd-title { font-family: var(--display); font-size: 2.3rem; color: #fff; letter-spacing: 3px; line-height: 1; }
#slmm .hd-tabs { display: flex; align-items: center; gap: 28px; }
#slmm .hd-tabs .hd-title { cursor: pointer; transition: color .2s; display: flex; align-items: center; gap: 10px; }
#slmm .hd-tabs .hd-title:not(.on) { color: #555; }
#slmm .hd-tabs .hd-title:not(.on):hover { color: #999; }
#slmm .win.page-trash .bd { grid-template-columns: 1fr; }
#slmm .win.page-trash .rail { display: none; }
#slmm .hd-trash i { font-style: normal; font-family: var(--mono); font-size: .85rem; letter-spacing: 0; color: #111; background: #888; border-radius: 3px; padding: 2px 7px; }
#slmm .trash-page .notice { margin: 16px 0 0; }
#slmm .win.page-health .bd { grid-template-columns: 1fr; }
#slmm .win.page-browse .bd { grid-template-columns: 1fr; }
#slmm .win.page-browse .rail { display: none; }
#slmm .bw-bar { display: flex; flex-wrap: wrap; gap: 12px 18px; align-items: center; margin: 16px 0 18px; }
#slmm .bw-find, #slmm .pf-name { width: 280px; background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.12); color: #ddd; padding: 7px 10px; font-size: .95rem; outline: none; }
#slmm .bw-find:focus, #slmm .pf-name:focus { border-color: var(--amber); }
#slmm .bw-bar .chips { display: flex; flex-wrap: wrap; gap: 6px; }
#slmm .bw-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(420px, 1fr)); gap: 14px; }
#slmm .bw-card { display: grid; grid-template-columns: 140px 1fr; gap: 14px; padding: 12px; border: 1px solid rgba(255,255,255,0.09); background: rgba(255,255,255,0.02); }
#slmm .bw-card.manual { border-color: rgba(220,80,60,0.35); }
#slmm .bw-th { width: 140px; height: 79px; background: #222 center / cover no-repeat; }
#slmm .bw-th.none { background: repeating-linear-gradient(45deg, #1c1c1f, #1c1c1f 8px, #202024 8px, #202024 16px); }
#slmm .bw-body { min-width: 0; }
#slmm .bw-body .msub { margin-top: 2px; white-space: normal; }
#slmm .bw-needs { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 6px; }
#slmm .bw-sum { color: #999; font-size: .9rem; line-height: 1.45; margin-top: 6px; }
#slmm .bw-tag { display: inline-block; margin-top: 8px; font-family: var(--mono); font-size: .75rem; letter-spacing: 1px; padding: 2px 7px; border: 1px solid rgba(255,255,255,0.15); color: #999; }
#slmm .bw-tag.ok { color: #7c7; border-color: rgba(120,200,120,0.4); }
#slmm .bw-shame { margin-top: 8px; padding: 6px 10px; border-left: 3px solid #d0503c; background: rgba(208,80,60,0.08); color: #bbb; font-size: .88rem; line-height: 1.5; }
#slmm .bw-shame-t { display: flex; align-items: center; gap: 8px; cursor: pointer; color: #e8735f; font-weight: 700; }
#slmm .bw-shame-t:hover { color: #ff9a85; }
#slmm .bw-shame-t .chev { font-family: var(--mono); font-size: .75rem; color: #a55; }
#slmm .bw-shame-b { margin-top: 6px; }
#slmm .bw-shame .acts { margin-top: 8px; }
#slmm .bw-card .acts { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 10px; }
#slmm .bw-card .upd-line { margin-top: 8px; }
#slmm .win.page-health .rail { display: none; }
#slmm .win.page-profiles .bd { grid-template-columns: 1fr; }
#slmm .win.page-profiles .rail { display: none; }
#slmm .profiles-page .notice { margin: 16px 0 0; }
#slmm .pf-save { display: flex; gap: 10px; align-items: center; margin: 18px 0 6px; }
#slmm .pf-acts { display: flex; gap: 8px; flex-wrap: wrap; justify-content: flex-end; }
#slmm .pf-sub { margin-top: 26px; }
#slmm .pf-paste { display: block; width: 100%; box-sizing: border-box; margin-top: 10px; background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.12); color: #ddd; padding: 8px 10px; font-family: var(--mono); font-size: .85rem; outline: none; resize: vertical; }
#slmm .pf-paste:focus { border-color: var(--amber); }
#slmm .pf-check { margin-top: 10px; padding: 4px 16px 16px; border: 1px solid rgba(235,163,8,0.35); background: rgba(235,163,8,0.04); }
#slmm .pf-line { margin-top: 4px; }
#slmm .pf-line .key-btn { display: inline-block; padding: 2px 8px; font-size: .75rem; margin-left: 6px; }
#slmm .pf-st { color: #999; }
#slmm .pf-val { font-family: var(--mono); color: #aaa; }
#slmm .pf-val.to { color: var(--amber); }
#slmm .hd-health i { font-style: normal; font-family: var(--mono); font-size: .85rem; letter-spacing: 0; color: #111; background: #888; border-radius: 3px; padding: 2px 7px; }
#slmm .hd-health.err i { background: var(--red); }
#slmm .hd-health.warn i { background: #e8813a; }
#slmm .hl-sum { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 22px; }
#slmm .hl-pip { font-family: var(--mono); font-size: .85rem; letter-spacing: 1px; padding: 4px 10px; border: 1px solid rgba(255,255,255,0.15); color: #bbb; }
#slmm .hl-pip.err { color: #f08070; border-color: rgba(220,80,60,0.5); background: rgba(220,80,60,0.08); }
#slmm .hl-pip.warn { color: #f0a060; border-color: rgba(232,129,58,0.5); background: rgba(232,129,58,0.08); }
#slmm .hl-sec + .hl-sec { margin-top: 34px; padding-top: 22px; border-top: 1px solid var(--line); }
#slmm .hl-clear { margin-top: 12px; color: #7c7; font-size: .95rem; }
#slmm .hl-item { padding: 14px 16px; margin-top: 12px; border: 1px solid rgba(255,255,255,0.08); border-left: 3px solid #888; background: rgba(0,0,0,0.25); }
#slmm .hl-item.err { border-left-color: var(--red); }
#slmm .hl-item.warn { border-left-color: #e8813a; }
#slmm .hl-b { margin-top: 6px; color: #ccc; font-size: .92rem; line-height: 1.45; }
#slmm .hl-files { margin-top: 6px; font-family: var(--mono); font-size: .82rem; color: #aaa; word-break: break-all; }
#slmm .tr-acts { display: flex; gap: 10px; margin-top: 16px; }
#slmm .pend { display: none; padding: 8px 24px 8px 26px; border-top: 1px solid rgba(235,163,8,0.35); background: rgba(235,163,8,0.06); }
#slmm .win.has-pend { grid-template-rows: 70px 1fr auto 56px; }
#slmm .win.has-pend .pend { display: block; }
#slmm .pend-hd { display: flex; align-items: center; gap: 12px; font-family: var(--mono); font-size: .85rem; color: var(--amber); letter-spacing: .5px; }
#slmm .pend-hd b { flex: 1; font-weight: 400; }
#slmm .pend .key-btn { padding: 4px 10px; font-size: .78rem; }
#slmm .pend-list { max-height: 132px; overflow-y: auto; margin-top: 6px; }
#slmm .pend-row { display: flex; align-items: center; gap: 14px; padding: 3px 0; font-size: .88rem; }
#slmm .pend-row .pn { color: #fff; min-width: 240px; }
#slmm .pend-row .pt { flex: 1; color: #aaa; }
#slmm .pend-quit { display: flex; align-items: center; gap: 12px; margin-top: 6px; font-size: .88rem; color: #ccc; }
#slmm .pend-quit span { flex: 1; }
#slmm .tr-item { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 12px 16px; margin-top: 10px; border: 1px solid rgba(255,255,255,0.08); background: rgba(0,0,0,0.25); }
#slmm .un-cfg { display: flex; align-items: center; gap: 8px; margin-top: 10px; cursor: pointer; }
#slmm .errs-page { padding: 26px 40px 40px; }
#slmm .errs-page .sec-t { margin-bottom: 8px; }
#slmm .hd-deco { font-family: var(--mono); font-size: .85rem; color: var(--amber); opacity: .75; letter-spacing: 1px; display: flex; gap: 14px; }
#slmm .hd-deco b { color: #fff; font-weight: 400; opacity: .8; }
#slmm .hd-sp { flex: 1; }
#slmm .seg { display: flex; border: 1px solid #444; border-radius: 3px; overflow: hidden; }
#slmm .seg span { font-family: var(--mono); font-size: .85rem; padding: 5px 12px; color: #888; cursor: pointer; }
#slmm .seg span.on { background: rgba(235,163,8,0.18); color: var(--amber); }
#slmm .kbd { font-family: var(--mono); font-size: .8rem; color: #aaa; border: 1px solid #555; border-bottom-width: 2px; border-radius: 3px; padding: 1px 7px; }
#slmm .hint-k { font-family: var(--mono); font-size: .8rem; color: #666; display: flex; gap: 8px; align-items: center; }
#slmm .close-btn { cursor: pointer; font-size: 1.7rem; color: #888; transition: color .2s; line-height: 1; padding: 4px; }
#slmm .close-btn:hover { color: #fff; }

#slmm .bd { display: grid; grid-template-columns: 400px 1fr; min-height: 0; }
#slmm .rail { display: flex; flex-direction: column; min-height: 0; border-right: 1px solid var(--line); background: rgba(0,0,0,0.25); }
#slmm .search { margin: 18px 18px 10px; position: relative; }
#slmm .search input { width: 100%; background: #000; border: 1px solid #444; border-radius: 3px; padding: 11px 14px 11px 40px; color: #eee; font: 700 1rem var(--body); letter-spacing: .5px; outline: none; transition: border-color .15s, box-shadow .15s; }
#slmm .search input::placeholder { color: #666; font-family: var(--mono); font-weight: 400; font-size: .85rem; letter-spacing: 1px; }
#slmm .search input:focus { border-color: var(--amber); box-shadow: 0 0 12px rgba(235,163,8,0.2); }
#slmm .search svg { position: absolute; left: 13px; top: 50%; transform: translateY(-50%); width: 17px; height: 17px; color: #777; }
#slmm .chips { display: flex; flex-wrap: wrap; gap: 6px; padding: 0 18px 14px; border-bottom: 1px solid var(--line); }
#slmm .chip { font-family: var(--mono); font-size: .78rem; letter-spacing: 1px; padding: 4px 9px; border: 1px solid rgba(255,255,255,0.12); color: #999; border-radius: 2px; cursor: pointer; text-transform: uppercase; display: flex; gap: 6px; align-items: center; }
#slmm .chip:hover { border-color: rgba(235,163,8,0.6); color: #ddd; }
#slmm .chip.on { background: rgba(235,163,8,0.15); border-color: var(--amber); color: var(--amber); }
#slmm .chip i { font-style: normal; color: #fff; opacity: .55; }
#slmm .list { overflow-y: auto; flex: 1; padding: 8px 0 20px; }
#slmm .list::-webkit-scrollbar, #slmm .pane::-webkit-scrollbar, #slmm .dd-list::-webkit-scrollbar { width: 6px; }
#slmm .list::-webkit-scrollbar-thumb, #slmm .pane::-webkit-scrollbar-thumb, #slmm .dd-list::-webkit-scrollbar-thumb { background: rgba(235,163,8,0.35); border-radius: 3px; }
#slmm .list::-webkit-scrollbar-track, #slmm .pane::-webkit-scrollbar-track, #slmm .dd-list::-webkit-scrollbar-track { background: rgba(255,255,255,0.03); }
#slmm .grp { font-family: var(--mono); font-size: .75rem; color: var(--amber); opacity: .75; letter-spacing: 3px; text-transform: uppercase; display: flex; align-items: center; gap: 10px; padding: 14px 18px 6px; }
#slmm .grp::after { content: ""; flex: 1; height: 1px; background: linear-gradient(90deg, rgba(235,163,8,0.25), transparent); }
#slmm .mod { position: relative; display: grid; grid-template-columns: 40px 1fr auto; gap: 12px; align-items: center; padding: 8px 16px 8px 15px; border-left: 3px solid transparent; cursor: pointer; transition: background .15s; animation: slmm-rin .35s both; }
@keyframes slmm-rin { from { opacity: 0; transform: translateX(-10px); } }
#slmm .mod:hover { background: linear-gradient(90deg, rgba(235,163,8,0.07), transparent); }
#slmm .mod.sel { border-left-color: var(--amber); background: linear-gradient(90deg, rgba(235,163,8,0.16), transparent 90%); }
#slmm .mod.off .mono, #slmm .mod.off .mname { opacity: .45; }
#slmm .mono { width: 40px; height: 40px; display: grid; place-items: center; font-family: var(--display); font-size: 1.05rem; letter-spacing: 1px; color: #fff; border: 1px solid rgba(255,255,255,0.14); border-radius: 3px; text-shadow: 0 1px 2px rgba(0,0,0,0.6); }
#slmm .mname { font-size: 1.02rem; font-weight: 700; color: #ddd; letter-spacing: .5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
#slmm .mod.sel .mname { color: #fff; }
#slmm .msub { font-family: var(--mono); font-size: .75rem; color: #6f6f6f; letter-spacing: .5px; margin-top: 2px; white-space: nowrap; }
#slmm .pip { font-family: var(--mono); font-size: .7rem; line-height: 1; padding: 3px 5px; border-radius: 2px; letter-spacing: .5px; }
#slmm .pip.chg { color: var(--amber); border: 1px solid rgba(235,163,8,0.45); }
#slmm .pip.offp { color: #999; border: 1px dashed #666; }
#slmm .pip.upd { color: #111; background: var(--teal); border: 1px solid var(--teal); }
#slmm .pip.warnp { color: var(--red); border: 1px solid rgba(255,90,90,0.5); }
#slmm .pip.errp { color: #111; background: var(--red); border: 1px solid var(--red); }
#slmm .pip.wrnp { color: #e8813a; border: 1px solid rgba(232,129,58,0.5); }
#slmm .pip.untp { color: #aaa; border: 1px solid #666; }
#slmm .has-icon { padding: 0; overflow: hidden; background: #000; }
#slmm .has-icon img { width: 100%; height: 100%; object-fit: cover; display: block; }
#slmm .grp.tog { cursor: pointer; }
#slmm .grp.tog:hover { opacity: 1; }
#slmm .grp .arrow { font-size: .7rem; letter-spacing: 0; }
#slmm .problem { font-family: var(--mono); font-size: .75rem; color: #c98a8a; line-height: 1.5; padding: 4px 18px 6px; white-space: pre-wrap; word-break: break-word; }
#slmm .problem.wrn { color: #c9a07a; }

#slmm .pane { overflow-y: auto; min-height: 0; position: relative; }
#slmm .pane > * { animation: slmm-pin .3s both; }
@keyframes slmm-pin { from { opacity: 0; transform: translateY(6px); } }
#slmm .hero { position: relative; height: 200px; overflow: hidden; border-bottom: 1px solid var(--line); }
#slmm .gen { position: absolute; inset: 0; display: flex; align-items: center; gap: 30px; padding: 0 44px; background: radial-gradient(ellipse at 20% 50%, var(--hue-a) 0%, transparent 60%), linear-gradient(135deg, rgba(255,255,255,0.03), transparent); }
#slmm .gen .big { flex-shrink: 0; width: 112px; height: 112px; font-size: 2.8rem; display: grid; place-items: center; font-family: var(--display); color: #fff; border: 1px solid rgba(255,255,255,0.18); border-radius: 4px; box-shadow: 0 10px 30px rgba(0,0,0,0.6); }
#slmm .gen .gt { font-family: var(--display); font-size: 3.6rem; color: #fff; letter-spacing: 2px; line-height: 1; text-shadow: 0 4px 12px rgba(0,0,0,0.6); max-width: 820px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
#slmm .gen .gs { font-size: 1.1rem; color: #bbb; margin-top: 10px; }
#slmm .hero-side { position: absolute; right: 30px; top: 22px; z-index: 2; }
#slmm .hero.compact { height: 92px; }
#slmm .hero.compact .gen { gap: 18px; padding: 0 30px; }
#slmm .hero.compact .gen .big { width: 60px; height: 60px; font-size: 1.5rem; }
#slmm .hero.compact .gen .gt { font-size: 2.1rem; max-width: 640px; }
#slmm .hero.compact .gen .gs { display: none; }
#slmm .hero.compact .hero-side { top: 50%; transform: translateY(-50%); }
#slmm .en-switch { display: flex; align-items: center; gap: 14px; background: rgba(0,0,0,0.55); border: 1px solid rgba(255,255,255,0.12); padding: 10px 14px 10px 18px; border-radius: 3px; }
#slmm .en-switch.pend { border-color: rgba(235,163,8,0.55); }
#slmm .en-switch .lbl { font-family: var(--mono); font-size: .82rem; letter-spacing: 2px; color: var(--teal); text-align: right; }
#slmm .en-switch .lbl.off { color: #999; }
#slmm .en-switch .lbl small { display: block; font-size: .72rem; color: #777; letter-spacing: 1px; margin-top: 2px; }
#slmm .en-switch.pend .lbl small { color: var(--amber); }
#slmm .setting-toggle.locked { opacity: .4; pointer-events: none; }
#slmm .desc { padding: 10px 40px 0; color: #b8b8b8; font-size: 1.02rem; line-height: 1.6; max-width: 1050px; white-space: pre-line; }
#slmm .meta { display: flex; align-items: center; gap: 18px; padding: 16px 40px 0; flex-wrap: wrap; }
#slmm .meta .id { font-family: var(--mono); font-size: .82rem; color: #777; letter-spacing: .5px; }
#slmm .meta .id b { color: #bbb; font-weight: 400; }
#slmm .meta .id.upd-ok { color: var(--teal); }
#slmm .meta .id.upd-bad { color: #c98a8a; }
#slmm .links { display: flex; gap: 10px; padding: 16px 40px 0; flex-wrap: wrap; }
#slmm .key-btn { font-family: var(--mono); cursor: pointer; padding: 7px 14px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.15); color: #ccc; font-size: .85rem; letter-spacing: 1px; transition: all .15s; border-radius: 2px; white-space: nowrap; }
#slmm .key-btn:hover { background: rgba(235,163,8,0.15); border-color: var(--amber); color: var(--amber); }
#slmm .key-btn.danger:hover { background: rgba(200,50,50,0.15); border-color: #ff4444; color: #ff6b6b; }
#slmm .tabs { position: sticky; top: 0; z-index: 4; display: flex; flex-wrap: wrap; padding: 0 30px; margin-top: 18px; border-bottom: 1px solid var(--line); background: rgba(18,18,20,0.97); }
#slmm .tab { position: relative; padding: 13px 16px 11px; font-size: 1rem; font-weight: 700; color: #888; letter-spacing: 2px; cursor: pointer; text-transform: uppercase; display: flex; gap: 7px; align-items: center; }
#slmm .tab:hover { color: #ddd; }
#slmm .tab.on { color: #fff; }
#slmm .tab.on::after { content: ""; position: absolute; left: 12px; right: 12px; bottom: -1px; height: 2px; background: var(--amber); }
#slmm .tab i { font-style: normal; font-family: var(--mono); font-size: .72rem; color: #666; letter-spacing: 0; }
#slmm .tab .cd { width: 6px; height: 6px; border-radius: 50%; background: var(--amber); }

#slmm .sets { padding: 6px 40px 40px; }
#slmm .sec { margin-top: 22px; }
#slmm .sec-t { font-family: var(--mono); font-size: .78rem; color: var(--amber); opacity: .75; letter-spacing: 3px; text-transform: uppercase; display: flex; align-items: center; gap: 10px; margin-bottom: 4px; }
#slmm .sec-t::after { content: ""; flex: 1; height: 1px; background: linear-gradient(90deg, rgba(235,163,8,0.25), transparent); }
#slmm .row { position: relative; display: grid; grid-template-columns: 1fr 150px 360px 34px; gap: 16px; align-items: center; padding: 12px 10px 12px 18px; border-bottom: 1px solid rgba(255,255,255,0.035); transition: background .15s; }
#slmm .row:hover { background: rgba(255,255,255,0.025); }
#slmm .row.chg::before { content: ""; position: absolute; left: 0; top: 10px; bottom: 10px; width: 3px; background: var(--amber); box-shadow: 0 0 8px rgba(235,163,8,0.6); }
#slmm .row.flash { animation: slmm-flash .9s ease-out; }
@keyframes slmm-flash { from { background: rgba(143,199,191,0.18); } to { background: transparent; } }
#slmm .lab { font-size: 1.05rem; font-weight: 700; color: #ddd; letter-spacing: .5px; display: flex; align-items: center; gap: 10px; }
#slmm .lab .ro { font-family: var(--mono); font-size: .7rem; color: #777; font-weight: 400; letter-spacing: 1px; border-left: 1px solid #444; padding-left: 8px; }
#slmm .hint { font-size: .86rem; color: rgba(200,200,200,0.55); letter-spacing: .3px; margin-top: 3px; line-height: 1.45; white-space: pre-line; }
#slmm .hint.note { color: var(--teal); }
#slmm .hint.wld.on { color: var(--teal); }
#slmm .wld-a { font-family: var(--mono); font-size: .72rem; letter-spacing: 1px; margin-left: 6px; color: var(--teal); cursor: pointer; border-bottom: 1px solid currentColor; white-space: nowrap; }
#slmm .wld-a:hover { color: #fff; }
#slmm .hint.warn { color: var(--red); opacity: .9; }
#slmm .badge { justify-self: start; font-family: var(--mono); font-size: .7rem; letter-spacing: 1.5px; padding: 3px 7px; border-radius: 2px; white-space: nowrap; color: var(--amber); background: rgba(235,163,8,0.07); border: 1px solid rgba(235,163,8,0.35); }
#slmm .ctl { display: flex; justify-content: flex-end; align-items: center; gap: 12px; }
#slmm .ctl.locked { opacity: .45; pointer-events: none; }
#slmm .reset { width: 30px; height: 30px; display: grid; place-items: center; border-radius: 2px; color: var(--amber); cursor: pointer; opacity: 0; pointer-events: none; transition: opacity .15s, background .15s; font-size: 1.15rem; }
#slmm .row.chg .reset.can { opacity: .8; pointer-events: auto; }
#slmm .reset:hover { background: rgba(235,163,8,0.15); opacity: 1; }

#slmm .setting-toggle { position: relative; width: 50px; height: 26px; flex-shrink: 0; cursor: pointer; }
#slmm .setting-toggle input { position: absolute; opacity: 0; width: 100%; height: 100%; margin: 0; cursor: pointer; z-index: 2; }
#slmm .setting-toggle .track { position: absolute; inset: 0; border-radius: 13px; background: rgba(255,255,255,0.14); border: 1px solid rgba(255,255,255,0.18); transition: background .18s, border-color .18s; }
#slmm .setting-toggle .knob { position: absolute; top: 3px; left: 3px; width: 20px; height: 20px; border-radius: 50%; background: #ddd; box-shadow: 0 1px 3px rgba(0,0,0,0.6); transition: transform .18s cubic-bezier(0.2,1,0.4,1), background .18s; }
#slmm .setting-toggle input:checked ~ .track { background: rgba(224,190,85,0.55); border-color: rgba(224,190,85,0.8); }
#slmm .setting-toggle input:checked ~ .knob { transform: translateX(24px); background: #fff; }
#slmm .slider { flex: 1; display: flex; align-items: center; gap: 12px; }
#slmm .slider input[type=range] { -webkit-appearance: none; flex: 1; height: 8px; border-radius: 4px; cursor: pointer; outline: none; }
#slmm .slider input[type=range]::-webkit-slider-thumb { -webkit-appearance: none; width: 20px; height: 20px; border-radius: 50%; background: #fff; box-shadow: 0 0 8px rgba(0,0,0,0.6); transition: transform .1s; }
#slmm .slider input[type=range]::-webkit-slider-thumb:hover { transform: scale(1.15); }
#slmm .num { width: 90px; background: #000; border: 1px solid #444; border-radius: 3px; color: var(--amber); font: 700 .98rem var(--mono); text-align: right; padding: 6px 9px; outline: none; user-select: text; }
#slmm .num.wide { width: 160px; }
#slmm .num:focus, #slmm .txt:focus { border-color: var(--amber); box-shadow: 0 0 10px rgba(235,163,8,0.2); }
#slmm .txt { width: 100%; background: #000; border: 1px solid #444; border-radius: 3px; color: #eee; font: 700 .95rem var(--mono); padding: 7px 10px; outline: none; user-select: text; }
#slmm .keycap { min-width: 120px; text-align: center; font-family: var(--mono); font-weight: 700; font-size: .95rem; color: var(--amber); background: rgba(235,163,8,0.06); border: 1px solid rgba(235,163,8,0.45); border-bottom-width: 3px; border-radius: 4px; padding: 6px 14px; cursor: pointer; transition: all .15s; letter-spacing: 1px; white-space: nowrap; }
#slmm .keycap:hover { background: rgba(235,163,8,0.16); border-color: var(--amber); }
#slmm .keycap.listening { color: #111; background: var(--amber); border-color: var(--amber); animation: slmm-pulse .9s ease-in-out infinite; }
#slmm .keycap.conflict { color: var(--red); border-color: rgba(255,90,90,0.6); background: rgba(255,90,90,0.06); }
@keyframes slmm-pulse { 0%,100% { opacity: 1; } 50% { opacity: .55; } }
#slmm .dd { position: relative; width: 240px; }
#slmm .dd-trigger { display: flex; align-items: center; justify-content: space-between; background: #000; color: var(--amber); border: 1px solid #444; border-radius: 4px; padding: 7px 12px 7px 15px; font-size: .98rem; font-weight: 700; cursor: pointer; }
#slmm .dd-trigger span:first-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
#slmm .dd-trigger:hover, #slmm .dd.open .dd-trigger { border-color: var(--amber); box-shadow: 0 0 10px rgba(235,163,8,0.2); }
#slmm .dd-arrow { flex-shrink: 0; width: 0; height: 0; margin-left: 8px; border-left: 6px solid transparent; border-right: 6px solid transparent; border-top: 7px solid var(--amber); transition: transform .2s; }
#slmm .dd.open .dd-arrow { transform: rotate(180deg); }
#slmm .dd-list { display: none; position: absolute; top: calc(100% + 4px); left: 0; right: 0; max-height: 320px; overflow-y: auto; background: #121214; border: 1px solid #444; border-radius: 4px; box-shadow: 0 10px 30px rgba(0,0,0,0.8); z-index: 30; }
#slmm .dd.open .dd-list { display: block; }
#slmm .dd-item { padding: 8px 15px; color: #ccc; font-size: .95rem; font-weight: 700; cursor: pointer; }
#slmm .dd-item:hover { background: rgba(235,163,8,0.15); color: #fff; }
#slmm .dd-item.selected { color: var(--amber); }
#slmm .dd.pv { width: 360px; }
#slmm .dd.pv .dd-trigger { padding: 6px 12px 6px 6px; }
#slmm .dd.pv .dd-list { max-height: 340px; padding: 4px; }
#slmm .dd.pv .dd-item { padding: 6px; border-radius: 3px; }
#slmm .dd.pv .dd-item.selected { background: rgba(235,163,8,0.1); }
#slmm .dd-pv { display: flex; align-items: center; gap: 12px; min-width: 0; }
#slmm .dd-pic { flex: none; max-width: 220px; max-height: 56px; overflow: hidden; border-radius: 4px; line-height: 0; pointer-events: none; }
#slmm .dd-name { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

#slmm .sr-head { padding: 30px 40px 6px; display: flex; align-items: baseline; gap: 14px; }
#slmm .sr-head .big { font-family: var(--display); font-size: 2.6rem; color: #fff; letter-spacing: 2px; }
#slmm .sr-head .q { font-family: var(--mono); color: var(--amber); font-size: 1rem; }
#slmm .sr-mod { display: flex; align-items: center; gap: 12px; margin-top: 22px; font-size: 1.1rem; font-weight: 700; color: #fff; cursor: pointer; }
#slmm .sr-mod .mono { width: 28px; height: 28px; font-size: .8rem; }
#slmm .sr-mod span { font-family: var(--mono); font-size: .75rem; color: #777; font-weight: 400; letter-spacing: 1px; }
#slmm mark { background: rgba(235,163,8,0.28); color: #fff; border-radius: 2px; padding: 0 1px; }
#slmm .notice { margin: 22px 40px 0; padding: 16px 18px; border: 1px dashed rgba(255,255,255,0.15); color: #999; font-size: .95rem; line-height: 1.6; display: flex; gap: 14px; align-items: flex-start; }
#slmm .notice b { color: #ddd; }
#slmm .notice .ico { font-family: var(--mono); color: var(--amber); font-size: 1.1rem; }
#slmm .notice.ok { border-color: rgba(143,199,191,0.45); }
#slmm .notice.ok .ico { color: var(--teal); }
#slmm .notice.warn { border-color: rgba(255,90,90,0.5); color: #c98a8a; }
#slmm .notice.warn .ico { color: var(--red); }
#slmm .notice.upd { border: 1px solid rgba(143,199,191,0.55); background: rgba(143,199,191,0.06); color: #bbb; }
#slmm .notice.upd .ico { color: var(--teal); }
#slmm .upd-main { flex: 1; min-width: 0; }
#slmm .upd-notes { margin-top: 12px; max-height: 260px; overflow-y: auto; white-space: pre-wrap; word-break: break-word; font-size: .9rem; color: #aaa; border-left: 2px solid rgba(143,199,191,0.35); padding: 2px 0 2px 12px; }
#slmm .upd-h { font-family: var(--mono); font-size: .75rem; letter-spacing: 2px; color: var(--teal); margin-bottom: 6px; }
#slmm .upd-line { margin-top: 8px; font-size: .88rem; color: #888; }
#slmm .upd-act { margin-top: 14px; display: flex; gap: 10px; }
#slmm .upd-line.ok { color: var(--teal); }
#slmm .upd-line.bad { color: #c98a8a; }
#slmm .upd-tag { font-family: var(--mono); font-size: .8rem; color: #ccc; margin: 10px 0 2px; }
#slmm .upd-h + .upd-tag { margin-top: 0; }
#slmm .held-line { margin: 10px 18px 2px; }
#slmm .all-btn { display: block; margin: 12px 18px 2px; text-align: center; border-color: rgba(143,199,191,0.55); color: var(--teal); }
#slmm .rels { padding: 16px 40px 0; display: grid; grid-template-columns: auto 1fr; gap: 8px 18px; align-items: baseline; }
#slmm .rel-h { font-family: var(--mono); font-size: .75rem; letter-spacing: 2px; color: #777; white-space: nowrap; }
#slmm .rel-items { display: flex; flex-wrap: wrap; gap: 8px; }
#slmm .rel { font-size: .9rem; color: #bbb; padding: 3px 9px; border: 1px solid rgba(255,255,255,0.12); border-radius: 2px; }
#slmm .rel[data-mod] { cursor: pointer; }
#slmm .rel[data-mod]:hover { border-color: var(--amber); color: #fff; }
#slmm .rel i { font-style: normal; font-family: var(--mono); font-size: .72rem; color: #777; margin-left: 6px; }
#slmm .rel u { text-decoration: none; font-family: var(--mono); font-size: .72rem; color: #9a8a60; margin-left: 6px; }
#slmm .tab:not(.tab-x) + .tab-x { margin-left: auto; }
#slmm .tab .rd { width: 7px; height: 7px; border-radius: 50%; }
#slmm .rd.likely { background: var(--red); }
#slmm .rd.may { background: #e8813a; }
#slmm .rd.harmless { background: var(--teal); opacity: .7; }
#slmm .railed { display: grid; grid-template-columns: 220px minmax(0, 1fr); align-items: start; margin-top: 18px; border-top: 1px solid var(--line); }
#slmm .railed .tabs { flex-direction: column; flex-wrap: nowrap; margin-top: 0; padding: 12px 0 16px; border-bottom: 0; border-right: 1px solid var(--line); max-height: 72vh; overflow-y: auto; background: rgba(18,18,20,0.97); }
#slmm .railed .tab { padding: 8px 18px 8px 30px; font-size: .88rem; letter-spacing: 1.5px; justify-content: flex-start; }
#slmm .railed .tab.on { background: rgba(235,163,8,0.07); }
#slmm .railed .tab.on::after { left: 0; right: auto; top: 5px; bottom: 5px; width: 3px; height: auto; }
#slmm .railed .tab:not(.tab-x) + .tab-x { margin: 10px 0 0; padding-top: 14px; border-top: 1px solid var(--line); }
#slmm .railed .sets, #slmm .railed .cf, #slmm .railed .cl { padding-left: 30px; }
#slmm .cl { padding: 18px 40px 40px; max-width: 860px; color: #bbb; font-size: .92rem; line-height: 1.5; }
#slmm .cl-note { color: #888; margin-bottom: 14px; }
#slmm .cl-h { font-family: var(--mono); color: var(--teal); letter-spacing: 1px; margin: 18px 0 6px; }
#slmm .cl-h.h1 { font-size: 1.05rem; color: var(--amber); }
#slmm .cl-h.h2 { font-size: .95rem; }
#slmm .cl-h.h3 { font-size: .85rem; color: #ccc; }
#slmm .cl-li { position: relative; padding-left: 16px; margin: 3px 0; word-break: break-word; }
#slmm .cl-li::before { content: '-'; position: absolute; left: 2px; color: #777; }
#slmm .cl-p { margin: 6px 0; word-break: break-word; }
#slmm .cl code { font-family: var(--mono); font-size: .85em; color: #ddd; background: rgba(255,255,255,0.06); padding: 0 4px; border-radius: 2px; }
#slmm .cl b { color: #eee; }
#slmm .cf { padding: 18px 40px 40px; }
#slmm .cf-intro { font-size: .92rem; color: #999; line-height: 1.5; max-width: 760px; }
#slmm .cf-sum { display: flex; flex-wrap: wrap; gap: 8px; margin: 14px 0 6px; }
#slmm .cf-pill { display: flex; align-items: center; gap: 8px; font-family: var(--mono); font-size: .75rem; letter-spacing: 1.5px; color: #bbb; padding: 4px 10px; border: 1px solid rgba(255,255,255,0.12); border-radius: 2px; }
#slmm .cf-pill .rd { width: 7px; height: 7px; border-radius: 50%; }
#slmm .cf-why { font-size: .85rem; color: #888; margin: 0 0 10px; max-width: 760px; }
#slmm .cf-mod { border-left: 2px solid rgba(255,255,255,0.1); padding: 8px 0 8px 14px; margin-bottom: 8px; }
#slmm .cf-mod.likely { border-left-color: var(--red); }
#slmm .cf-mod.may { border-left-color: #e8813a; }
#slmm .cf-head { display: flex; align-items: baseline; gap: 10px; margin-bottom: 6px; }
#slmm .cf-count { font-family: var(--mono); font-size: .72rem; color: #777; }
#slmm .cf-m { display: grid; grid-template-columns: 10px minmax(160px, auto) 1fr; gap: 10px; align-items: baseline; padding: 3px 0; }
#slmm .cf-m .rd { width: 6px; height: 6px; border-radius: 50%; align-self: center; }
#slmm .cf-name { font-family: var(--mono); font-size: .78rem; color: #ccc; }
#slmm .cf-eff { font-size: .82rem; color: #888; }
#slmm .cf-eff b { font-weight: 600; color: #aaa; }
#slmm .fit-names { margin-top: 6px; font-family: var(--mono); font-size: .78rem; color: #ccc; line-height: 1.6; word-break: break-all; }
#slmm .cf-more { font-family: var(--mono); font-size: .72rem; color: #666; padding: 3px 0 0 20px; }
#slmm .er { border-left: 2px solid #e8813a; padding: 8px 0 8px 14px; margin: 12px 0; max-width: 900px; }
#slmm .er.error { border-left-color: var(--red); }
#slmm .er-head { display: flex; align-items: baseline; gap: 10px; margin-bottom: 4px; }
#slmm .er-lv { font-family: var(--mono); font-size: .72rem; letter-spacing: 1.5px; color: #e8813a; }
#slmm .er.error .er-lv { color: var(--red); }
#slmm .er-msg { font-family: var(--mono); font-size: .8rem; color: #ddd; white-space: pre-wrap; word-break: break-word; line-height: 1.45; }
#slmm .er-clash { font-size: .85rem; color: #e0a0a0; margin-top: 8px; display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
#slmm .er-stack { font-family: var(--mono); font-size: .72rem; color: #777; white-space: pre-wrap; word-break: break-word; margin-top: 6px; line-height: 1.4; }
#slmm .rel.bad { border-color: rgba(255,90,90,0.5); color: #e0a0a0; }
#slmm .rel.bad i { color: var(--red); }
#slmm .notice .acts { margin-top: 10px; display: flex; gap: 10px; }
#slmm .empty { padding: 70px 40px; text-align: center; color: #777; }
#slmm .empty .big { font-family: var(--display); font-size: 2rem; color: #555; letter-spacing: 3px; margin-bottom: 10px; }

#slmm .ft { display: flex; align-items: center; gap: 16px; padding: 0 24px 0 26px; background: rgba(0,0,0,0.35); border-top: 1px solid rgba(255,255,255,0.06); }
#slmm .status-dot { flex-shrink: 0; width: 7px; height: 7px; background: var(--teal); border-radius: 50%; box-shadow: 0 0 5px var(--teal); }
#slmm .status-dot.warn { background: var(--amber); box-shadow: 0 0 5px var(--amber); animation: slmm-blink 2s infinite; }
@keyframes slmm-blink { 0%,100% { opacity: 1; } 50% { opacity: .3; } }
#slmm .ft-t { font-family: var(--mono); font-size: .85rem; color: #999; letter-spacing: .5px; }
#slmm .ft-t b { color: #fff; font-weight: 400; }
#slmm .ft-t em { color: var(--amber); font-style: normal; }
#slmm .ft-t { flex: 1; min-width: 0; }
#slmm .ft .key-btn { padding: 5px 12px; font-size: .8rem; }
#slmm .ft .key-btn.ok { border-color: var(--teal); color: var(--teal); }
#slmm .ft .key-btn.bad { border-color: rgba(255,90,90,0.6); color: var(--red); }

#slmm .hero .banner { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; display: block; }
#slmm .gen.on-banner { background: linear-gradient(90deg, rgba(18,18,20,0.92) 0%, rgba(18,18,20,0.55) 55%, rgba(18,18,20,0.1) 100%); }
#slmm .rel .hk { font-family: var(--mono); font-size: .8rem; color: var(--amber); margin-right: 8px; }
#slmm .rel.bad .hk { color: var(--red); }
#slmm .adv-t { display: flex; align-items: center; gap: 10px; margin-top: 10px; padding: 8px 18px; font-family: var(--mono); font-size: .75rem; letter-spacing: 2px; color: #888; text-transform: uppercase; cursor: pointer; border-bottom: 1px dashed rgba(255,255,255,0.08); }
#slmm .adv-t:hover { color: #ddd; }
#slmm .adv-t .cd { width: 6px; height: 6px; border-radius: 50%; background: var(--amber); }

#slmm .ipk { position: relative; width: 100%; display: flex; flex-direction: column; align-items: flex-end; gap: 6px; }
#slmm .ipk-pick { display: flex; align-items: center; gap: 9px; min-width: 240px; max-width: 100%; background: #000; color: var(--amber); border: 1px solid #444; border-radius: 4px; padding: 5px 12px 5px 6px; font-size: .95rem; font-weight: 700; cursor: pointer; }
#slmm .ipk-pick:hover { border-color: var(--amber); }
#slmm .ipk-pick .ipk-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1; text-align: left; }
#slmm .ico-sq { flex-shrink: 0; width: 28px; height: 28px; border-radius: 3px; background: rgba(255,255,255,0.06); object-fit: contain; display: block; }
/* An item list drawn as cards (ItemList, ItemCounts), and the big item picker. */
#slmm .row.wide { grid-template-columns: 1fr 34px; align-items: start; }
#slmm .row.wide .mf-host { grid-column: 1 / -1; }
#slmm .row.wide .lab .badge { margin-left: 4px; }
#slmm .mf-host.locked { opacity: .45; pointer-events: none; }
#slmm .mf { --slot: 128px; display: grid; grid-template-columns: minmax(0, 1fr) 390px; gap: 22px; margin-top: 12px; }
#slmm .mf.solo { grid-template-columns: minmax(0, 1fr); }
#slmm .mf-slots { display: flex; gap: 12px; flex-wrap: wrap; }
#slmm .mf .slot { position: relative; width: var(--slot); height: 178px; border: 1px solid rgba(220,188,120,0.18); background: linear-gradient(180deg, rgba(44,36,26,0.92), rgba(20,17,13,0.95)); border-radius: 3px; display: flex; flex-direction: column; align-items: center; padding: 10px 8px 8px; transition: border-color .15s, transform .15s, box-shadow .15s; }
#slmm .mf .slot.plain { height: 150px; }
#slmm .mf .slot:hover { border-color: rgba(255,200,58,0.6); box-shadow: 0 6px 22px rgba(0,0,0,0.55), 0 0 0 1px rgba(255,200,58,0.12) inset; transform: translateY(-2px); }
#slmm .mf .slot.drag { opacity: .35; }
#slmm .mf .slot.over { border-color: var(--teal); box-shadow: 0 0 0 2px rgba(143,199,191,0.35); }
#slmm .mf .tile { position: relative; width: 84px; height: 84px; border-radius: 3px; background: radial-gradient(circle at 50% 40%, rgba(255,214,140,0.16), rgba(0,0,0,0) 70%), #0e0c0a; border: 1px solid rgba(255,255,255,0.07); display: grid; place-items: center; cursor: grab; }
#slmm .mf .tile img { width: 72px; height: 72px; object-fit: contain; filter: drop-shadow(0 3px 4px rgba(0,0,0,0.7)); pointer-events: none; }
#slmm .mf .tile .noimg { font: 700 .8rem var(--mono); color: #666; }
#slmm .mf .qty { position: absolute; right: -6px; bottom: -6px; min-width: 30px; padding: 2px 7px; background: #0b0a09; border: 1px solid var(--amber); color: var(--amber); font: 700 .9rem var(--mono); text-align: center; border-radius: 2px; box-shadow: 0 2px 6px rgba(0,0,0,0.7); }
#slmm .mf .qty.pop { animation: mf-pop .22s; }
#slmm .mf .nm { margin-top: 10px; height: 2.5em; font-size: .86rem; line-height: 1.25em; color: #eee; text-align: center; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
#slmm .mf .stp { margin-top: auto; display: flex; align-items: stretch; border: 1px solid rgba(255,255,255,0.14); border-radius: 2px; overflow: hidden; }
#slmm .mf .stp span { width: 30px; display: grid; place-items: center; cursor: pointer; color: #aaa; font: 700 1.05rem var(--mono); background: rgba(255,255,255,0.04); }
#slmm .mf .stp span:hover { background: rgba(235,163,8,0.2); color: var(--amber); }
#slmm .mf .stp input { width: 44px; background: #000; border: 0; color: #fff; text-align: center; font: 700 .95rem var(--mono); outline: none; padding: 4px 0; user-select: text; }
#slmm .mf .sx, #slmm .mf .sw { position: absolute; top: 4px; width: 22px; height: 22px; display: grid; place-items: center; color: #888; cursor: pointer; border-radius: 2px; opacity: 0; transition: opacity .15s; font-size: .85rem; }
#slmm .mf .sx { right: 4px; }
#slmm .mf .sw { left: 4px; font-size: .8rem; }
#slmm .mf .slot:hover .sx, #slmm .mf .slot:hover .sw { opacity: 1; }
#slmm .mf .sx:hover { background: rgba(200,50,50,0.25); color: #ff8080; }
#slmm .mf .sw:hover { background: rgba(143,199,191,0.2); color: var(--teal); }
#slmm .mf .slot.add { border-style: dashed; border-color: rgba(235,163,8,0.4); background: rgba(235,163,8,0.03); cursor: pointer; justify-content: center; gap: 10px; color: var(--amber); }
#slmm .mf .slot.add .plus { width: 54px; height: 54px; border-radius: 50%; border: 1px solid rgba(235,163,8,0.55); display: grid; place-items: center; font-size: 1.9rem; line-height: 1; transition: background .15s, transform .15s; }
#slmm .mf .slot.add:hover .plus { background: rgba(235,163,8,0.18); transform: scale(1.06); }
#slmm .mf .slot.add .al { font: 700 .78rem var(--mono); letter-spacing: 2px; text-align: center; }
#slmm .mf .slot.auto { height: 160px; border-color: rgba(143,199,191,0.18); background: linear-gradient(180deg, rgba(26,34,33,0.85), rgba(14,17,17,0.95)); }
#slmm .mf .slot.auto:hover { transform: none; box-shadow: none; border-color: rgba(143,199,191,0.35); }
#slmm .mf .slot.auto .tile { cursor: default; }
#slmm .mf .slot.auto .qty { border-color: var(--teal); color: var(--teal); }
#slmm .mf .slot.auto .from { margin-top: auto; font: .72rem var(--mono); color: #6f8582; letter-spacing: 1px; }
#slmm .mf .slot.auto.none { justify-content: center; color: #6f8582; }
#slmm .mf .slot.add.hand { height: 160px; border-color: rgba(143,199,191,0.45); background: rgba(143,199,191,0.03); color: var(--teal); }
#slmm .mf .slot.add.hand .plus { border-color: rgba(143,199,191,0.55); font-size: 1.4rem; }
#slmm .mf .slot.add.hand:hover .plus { background: rgba(143,199,191,0.16); }
#slmm .mf-under { display: flex; align-items: center; gap: 14px; margin-top: 14px; flex-wrap: wrap; }
#slmm .mf-status { display: flex; align-items: center; gap: 8px; font-size: .9rem; color: var(--teal); }
#slmm .mf-status.bad { color: #ff8a7a; }
#slmm .mf-status.note { color: #9fb8b4; }
#slmm .mf-status i { font-style: normal; width: 20px; height: 20px; flex-shrink: 0; border-radius: 50%; display: grid; place-items: center; font: 700 .75rem var(--mono); background: rgba(143,199,191,0.15); border: 1px solid rgba(143,199,191,0.5); }
#slmm .mf-status.bad i { background: rgba(255,90,90,0.12); border-color: rgba(255,90,90,0.6); }
#slmm .mf-status.note i { background: rgba(143,199,191,0.08); border-color: rgba(143,199,191,0.35); font-style: italic; }
#slmm .mf-cap { display: flex; align-items: center; gap: 8px; font: .78rem var(--mono); color: #777; letter-spacing: 1px; }
#slmm .mf-cap b { color: #bbb; font-weight: 400; }
#slmm .mf-pips { display: flex; gap: 3px; }
#slmm .mf-pips i { width: 10px; height: 10px; border: 1px solid rgba(235,163,8,0.4); border-radius: 1px; }
#slmm .mf-pips i.on { background: var(--amber); box-shadow: 0 0 6px rgba(235,163,8,0.5); }
#slmm .mf-text { margin-left: auto; display: flex; align-items: center; gap: 8px; }
#slmm .mf-text code { font: .85rem var(--mono); color: #9a9a9a; background: #000; border: 1px solid #2c2c2c; padding: 5px 10px; border-radius: 2px; cursor: text; }
#slmm .mf-text code:hover { color: #ddd; border-color: #555; }
#slmm .mf-text .mf-in { width: 420px; background: #000; border: 1px solid var(--amber); color: #fff; font: .9rem var(--mono); padding: 6px 10px; outline: none; border-radius: 2px; user-select: text; }
#slmm .mf-warn { grid-column: 1 / -1; display: flex; gap: 12px; align-items: flex-start; padding: 10px 14px; border: 1px solid rgba(255,90,90,0.4); background: rgba(255,90,90,0.06); color: #f0c0b8; font-size: .9rem; line-height: 1.5; border-radius: 2px; }
#slmm .mf-warn b { color: #ff9a8a; }
#slmm .mf-card { border: 1px solid rgba(220,188,120,0.2); background: linear-gradient(180deg, rgba(36,30,22,0.95), rgba(16,14,11,0.97)); border-radius: 3px; overflow: hidden; align-self: start; }
#slmm .mf-card .hd2 { display: flex; gap: 12px; align-items: center; padding: 12px 14px; border-bottom: 1px solid rgba(220,188,120,0.12); background: rgba(0,0,0,0.25); }
#slmm .mf-card .hd2 img { width: 46px; height: 46px; object-fit: contain; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.8)); }
#slmm .mf-card .hd2 .t { color: #fff; font-weight: 700; font-size: .98rem; }
#slmm .mf-card .hd2 .s { font: .72rem var(--mono); letter-spacing: 2px; color: var(--amber); opacity: .8; }
#slmm .mf-card .ln { display: flex; align-items: center; gap: 10px; padding: 5px 14px; font-size: .88rem; color: #ddd; }
#slmm .mf-card .ln img { width: 26px; height: 26px; object-fit: contain; }
#slmm .mf-card .ln .n { flex: 1; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
#slmm .mf-card .ln .n.dim { color: #777; }
#slmm .mf-card .ln .c { font: 700 .88rem var(--mono); color: var(--amber); }
#slmm .mf-card .stats { display: grid; grid-template-columns: repeat(3, 1fr); border-top: 1px solid rgba(220,188,120,0.12); border-bottom: 1px solid rgba(220,188,120,0.12); margin-top: 8px; }
#slmm .mf-card .stats div { padding: 9px 12px; border-right: 1px solid rgba(220,188,120,0.1); }
#slmm .mf-card .stats div:last-child { border-right: 0; }
#slmm .mf-card .stats b { display: block; font: 700 1.1rem var(--mono); color: #fff; }
#slmm .mf-card .stats span { font: .68rem var(--mono); letter-spacing: 1.5px; color: #8a7f6c; text-transform: uppercase; }
#slmm .mf-card .back { padding: 10px 14px 12px; }
#slmm .mf-card .back .bt { font: .68rem var(--mono); letter-spacing: 1.5px; color: #8a7f6c; text-transform: uppercase; margin-bottom: 6px; }
#slmm .mf-card .back .chs { display: flex; gap: 8px; flex-wrap: wrap; }
#slmm .mf-card .back .ch { display: flex; align-items: center; gap: 5px; padding: 3px 8px 3px 4px; background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08); border-radius: 2px; font: .8rem var(--mono); color: #ccc; }
#slmm .mf-card .back .ch img { width: 20px; height: 20px; object-fit: contain; }
#slmm .pk-veil { position: absolute; inset: 0; z-index: 30; background: rgba(0,0,0,0.62); display: flex; align-items: center; justify-content: center; animation: slmm-fade .15s both; }
#slmm .pk { width: 1460px; height: 840px; display: grid; grid-template-rows: auto minmax(0, 1fr); background: #121110; border: 1px solid rgba(235,163,8,0.35); box-shadow: 0 30px 80px rgba(0,0,0,0.85), 0 0 40px rgba(235,163,8,0.08); border-radius: 3px; }
#slmm .pk-hd { display: flex; align-items: center; gap: 16px; padding: 14px 18px; border-bottom: 1px solid var(--line); }
#slmm .pk-hd .ti { font: 700 .82rem var(--mono); letter-spacing: 3px; color: var(--amber); white-space: nowrap; max-width: 520px; overflow: hidden; text-overflow: ellipsis; }
#slmm .pk-hd .ti em { font-style: normal; color: #fff; letter-spacing: 1px; }
#slmm .pk-q { flex: 1; position: relative; }
#slmm .pk-q svg { position: absolute; left: 12px; top: 50%; width: 18px; height: 18px; transform: translateY(-50%); color: #777; }
#slmm .pk-find { width: 100%; background: #000; border: 1px solid #444; border-radius: 3px; padding: 10px 12px 10px 40px; color: #fff; font: 700 1rem var(--body); outline: none; user-select: text; }
#slmm .pk-find:focus { border-color: var(--amber); box-shadow: 0 0 12px rgba(235,163,8,0.2); }
#slmm .pk-hd .used { font: .8rem var(--mono); color: #888; white-space: nowrap; }
#slmm .pk-hd .used b { color: var(--amber); }
#slmm .pk-bd { display: grid; grid-template-columns: 250px minmax(0, 1fr) 330px; min-height: 0; }
#slmm .pk-cats { border-right: 1px solid var(--line); overflow: auto; padding: 8px 0; }
#slmm .pk-cat { display: flex; justify-content: space-between; align-items: center; padding: 8px 16px; color: #aaa; cursor: pointer; font-size: .92rem; border-left: 3px solid transparent; }
#slmm .pk-cat:hover { background: rgba(255,255,255,0.03); color: #fff; }
#slmm .pk-cat.on { border-left-color: var(--amber); background: linear-gradient(90deg, rgba(235,163,8,0.16), transparent); color: #fff; }
#slmm .pk-cat i { font: normal .75rem var(--mono); color: #666; }
#slmm .pk-cat.star { color: var(--amber); }
#slmm .pk-sep { height: 1px; background: var(--line); margin: 8px 16px; }
#slmm .pk-grid { overflow: auto; padding: 14px; display: grid; grid-template-columns: repeat(auto-fill, minmax(104px, 1fr)); grid-auto-rows: 132px; gap: 10px; align-content: start; }
#slmm .pk-cats::-webkit-scrollbar, #slmm .pk-grid::-webkit-scrollbar, #slmm .pk-info::-webkit-scrollbar { width: 6px; }
#slmm .pk-cats::-webkit-scrollbar-thumb, #slmm .pk-grid::-webkit-scrollbar-thumb, #slmm .pk-info::-webkit-scrollbar-thumb { background: rgba(235,163,8,0.35); border-radius: 3px; }
#slmm .pk .it { position: relative; border: 1px solid rgba(255,255,255,0.07); background: linear-gradient(180deg, rgba(40,34,26,0.7), rgba(18,16,13,0.9)); border-radius: 3px; display: flex; flex-direction: column; align-items: center; padding: 8px 6px 6px; cursor: pointer; transition: border-color .12s, background .12s, transform .12s; }
#slmm .pk .it:hover, #slmm .pk .it.hi { border-color: rgba(255,200,58,0.7); background: linear-gradient(180deg, rgba(70,56,30,0.8), rgba(24,20,14,0.95)); transform: translateY(-1px); }
#slmm .pk .it img { width: 60px; height: 60px; object-fit: contain; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.7)); pointer-events: none; }
#slmm .pk .it .noimg { width: 60px; height: 60px; display: grid; place-items: center; font: 700 .75rem var(--mono); color: #555; }
#slmm .pk .it .in { margin-top: 6px; font-size: .76rem; line-height: 1.2em; height: 2.4em; overflow: hidden; color: #ddd; text-align: center; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
#slmm .pk .it .id { font: .66rem var(--mono); color: #666; margin-top: 2px; }
#slmm .pk .it .got { position: absolute; top: 5px; right: 5px; font: 700 .72rem var(--mono); color: #111; background: var(--amber); padding: 1px 5px; border-radius: 2px; }
#slmm .pk .it.taken { border-color: rgba(235,163,8,0.45); }
#slmm .pk-none { grid-column: 1 / -1; padding: 60px 0; text-align: center; color: #777; }
#slmm .pk-more { grid-column: 1 / -1; text-align: center; padding: 10px; font: .8rem var(--mono); color: #888; cursor: pointer; }
#slmm .pk-info { border-left: 1px solid var(--line); padding: 18px; display: flex; flex-direction: column; gap: 12px; overflow: auto; }
#slmm .pk-info .big { width: 100%; aspect-ratio: 1.3; flex-shrink: 0; border: 1px solid rgba(255,255,255,0.07); border-radius: 3px; background: radial-gradient(circle at 50% 45%, rgba(255,214,140,0.18), rgba(0,0,0,0) 65%), #0d0b09; display: grid; place-items: center; }
#slmm .pk-info .big img { width: 150px; height: 150px; object-fit: contain; filter: drop-shadow(0 6px 8px rgba(0,0,0,0.8)); }
#slmm .pk-info .t { color: #fff; font-weight: 700; font-size: 1.15rem; }
#slmm .pk-info .t2 { color: #9a8f7a; font-size: .9rem; margin-top: -8px; }
#slmm .pk-info dl { display: grid; grid-template-columns: auto 1fr; gap: 6px 14px; font-size: .88rem; }
#slmm .pk-info dt { font: .72rem var(--mono); letter-spacing: 1.5px; color: #7a6e5a; text-transform: uppercase; padding-top: 2px; }
#slmm .pk-info dd { color: #ddd; }
#slmm .pk-info .act { margin-top: auto; display: flex; gap: 10px; align-items: center; }
#slmm .pk-info .act .stp { display: flex; border: 1px solid rgba(255,255,255,0.14); border-radius: 2px; overflow: hidden; }
#slmm .pk-info .act .stp span { width: 34px; display: grid; place-items: center; cursor: pointer; color: #aaa; font: 700 1.1rem var(--mono); background: rgba(255,255,255,0.04); }
#slmm .pk-info .act .stp span:hover { background: rgba(235,163,8,0.2); color: var(--amber); }
#slmm .pk-info .act .stp input { width: 50px; background: #000; border: 0; color: #fff; text-align: center; font: 700 1rem var(--mono); outline: none; padding: 8px 0; user-select: text; }
#slmm .pk-info .go { flex: 1; text-align: center; padding: 10px; background: rgba(235,163,8,0.2); border: 1px solid var(--amber); color: var(--amber); font: 700 .9rem var(--mono); letter-spacing: 2px; cursor: pointer; border-radius: 2px; }
#slmm .pk-info .go:hover { background: rgba(235,163,8,0.35); color: #fff; }
#slmm .pk-info .go.off { opacity: .35; pointer-events: none; }
#slmm .pk-info .empty-info { margin: auto; color: #666; text-align: center; font-size: .9rem; line-height: 1.6; }
#slmm .mf .tile .noimg, #slmm .pk .it .noimg, #slmm .pk-info .big .noimg { font-size: 1.6rem; color: #8a7a5c; }
#slmm .pk-info .big .noimg { font: 700 3.5rem var(--mono); }
#slmm .pk-keys { font: .72rem var(--mono); color: #666; letter-spacing: 1px; }
@keyframes mf-pop { 0% { transform: scale(1); } 50% { transform: scale(1.25); } 100% { transform: scale(1); } }
/* A mod's own list (List) as rows: picture, name, what it does, its choice or number. The picker shows the same rows with an Add button. */
#slmm .el { margin-top: 12px; display: flex; flex-direction: column; gap: 10px; }
#slmm .el-rows { border: 1px solid rgba(220,188,120,0.16); border-radius: 3px; background: rgba(0,0,0,0.22); max-height: 460px; overflow: auto; }
#slmm .el-rows::-webkit-scrollbar { width: 6px; }
#slmm .el-rows::-webkit-scrollbar-thumb { background: rgba(235,163,8,0.35); border-radius: 3px; }
#slmm .el-row, #slmm .pk-row { display: flex; align-items: center; gap: 12px; padding: 8px 12px; border-bottom: 1px solid rgba(255,255,255,0.05); }
#slmm .el-row:last-child, #slmm .pk-row:last-child { border-bottom: 0; }
#slmm .el-row:hover, #slmm .pk-row:hover { background: rgba(235,163,8,0.05); }
#slmm .el-row.pop { animation: el-flash .5s; }
#slmm .el-ic { width: 46px; height: 46px; flex-shrink: 0; display: grid; place-items: center; border-radius: 3px; border: 1px solid rgba(255,255,255,0.07); background: radial-gradient(circle at 50% 40%, rgba(255,214,140,0.14), rgba(0,0,0,0) 70%), #0e0c0a; }
#slmm .el-ic img { width: 40px; height: 40px; object-fit: contain; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.7)); }
#slmm .el-ic .noimg { font: 700 1.15rem var(--mono); color: #8a7a5c; }
#slmm .el-tx { flex: 1; min-width: 0; }
#slmm .el-nm { color: #eee; font-size: .95rem; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
#slmm .el-nm span { margin-left: 8px; color: #8a7f6c; font-weight: 400; font-size: .82rem; }
#slmm .el-ds { margin-top: 2px; color: #a9a08c; font-size: .84rem; line-height: 1.35; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; }
#slmm .el-row.lost .el-nm { color: #888; }
#slmm .el-seg { display: flex; flex-shrink: 0; border: 1px solid rgba(143,199,191,0.35); border-radius: 2px; overflow: hidden; }
#slmm .el-seg span { padding: 6px 12px; border-right: 1px solid rgba(143,199,191,0.2); color: #9fb8b4; font: 700 .78rem var(--mono); letter-spacing: 1px; cursor: pointer; }
#slmm .el-seg span:last-child { border-right: 0; }
#slmm .el-seg span:hover { background: rgba(143,199,191,0.15); color: #fff; }
#slmm .el-seg span.on { background: rgba(143,199,191,0.3); color: #fff; }
#slmm .el-row .stp { flex-shrink: 0; display: flex; align-items: stretch; border: 1px solid rgba(255,255,255,0.14); border-radius: 2px; overflow: hidden; }
#slmm .el-row .stp span { width: 30px; display: grid; place-items: center; cursor: pointer; color: #aaa; font: 700 1.05rem var(--mono); background: rgba(255,255,255,0.04); }
#slmm .el-row .stp span:hover { background: rgba(235,163,8,0.2); color: var(--amber); }
#slmm .el-row .stp input { width: 54px; background: #000; border: 0; color: #fff; text-align: center; font: 700 .95rem var(--mono); outline: none; padding: 5px 0; user-select: text; }
#slmm .el-mv { display: flex; flex-direction: column; gap: 2px; flex-shrink: 0; }
#slmm .el-mv span { width: 22px; height: 18px; display: grid; place-items: center; font-size: .62rem; color: #777; cursor: pointer; border-radius: 2px; }
#slmm .el-mv span:hover { color: var(--amber); background: rgba(235,163,8,0.12); }
#slmm .el-mv span.off { visibility: hidden; }
#slmm .el-x { width: 28px; height: 28px; flex-shrink: 0; display: grid; place-items: center; color: #777; cursor: pointer; border-radius: 2px; }
#slmm .el-x:hover { background: rgba(200,50,50,0.25); color: #ff8080; }
#slmm .el-empty { padding: 16px; color: #9a927e; font-size: .9rem; }
#slmm .el-bar { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
#slmm .el-add { padding: 8px 18px; border: 1px solid var(--amber); border-radius: 2px; background: rgba(235,163,8,0.12); color: var(--amber); font: 700 .82rem var(--mono); letter-spacing: 2px; cursor: pointer; }
#slmm .el-add:hover { background: rgba(235,163,8,0.3); color: #fff; }
#slmm .el-add.off { opacity: .35; pointer-events: none; }
#slmm .el-count { font: .8rem var(--mono); color: #888; }
#slmm .pk.rows .pk-bd { grid-template-columns: 250px minmax(0, 1fr); }
#slmm .pk.rows .pk-info { display: none; }
#slmm .pk.rows .pk-grid { display: block; padding: 0; }
#slmm .pk-row.in { background: rgba(143,199,191,0.04); }
#slmm .pk-row.has, #slmm .el-row.has { box-shadow: inset 3px 0 0 var(--amber); }
#slmm .el-have { flex-shrink: 0; padding: 3px 8px; border: 1px solid rgba(235,163,8,0.45); border-radius: 2px; background: rgba(235,163,8,0.1); color: var(--amber); font: 700 .74rem var(--mono); letter-spacing: 1px; white-space: nowrap; }
#slmm .pk-grp { flex-shrink: 0; font: .72rem var(--mono); letter-spacing: 1px; color: #7a6e5a; white-space: nowrap; }
#slmm .pk-val { flex-shrink: 0; font: 700 .8rem var(--mono); color: var(--teal); }
#slmm .pk-btn { flex-shrink: 0; min-width: 116px; padding: 7px 12px; text-align: center; border: 1px solid var(--amber); border-radius: 2px; background: rgba(235,163,8,0.1); color: var(--amber); font: 700 .8rem var(--mono); letter-spacing: 1.5px; cursor: pointer; }
#slmm .pk-btn:hover { background: rgba(235,163,8,0.3); color: #fff; }
#slmm .pk-btn.off { opacity: .35; pointer-events: none; }
#slmm .pk-btn.in { border-color: rgba(143,199,191,0.5); background: rgba(143,199,191,0.08); color: var(--teal); }
#slmm .pk-btn i { display: none; font-style: normal; }
#slmm .pk-btn.in:hover { border-color: rgba(255,90,90,0.6); background: rgba(255,90,90,0.08); color: #ff8a7a; }
#slmm .pk-btn.in:hover b { display: none; }
#slmm .pk-btn.in:hover i { display: inline; }
@keyframes el-flash { 0% { background: rgba(235,163,8,0.25); } 100% { background: transparent; } }
`;

  // ===== The window =====

  const root = document.createElement('div');
  root.id = 'slmm';
  root.innerHTML = `
<div class="stage"><div class="win">
  <span class="corner tl"></span><span class="corner tr"></span><span class="corner bl"></span><span class="corner br"></span>
  <div class="hd">
    <div class="hd-tabs"><div class="hd-title hd-browse" data-page="browse"><span data-word="pageBrowse"></span></div><div class="hd-title on" data-page="mods">MODS</div><div class="hd-title hd-profiles" data-page="profiles"><span data-word="pageProfiles"></span></div><div class="hd-title hd-health" data-page="health"><span data-word="pageHealth"></span><i data-count="health"></i></div><div class="hd-title hd-trash" data-page="trash"><span data-word="pageTrash"></span><i data-count="trash"></i></div></div>
    <div class="hd-deco"><span>// MOD MANAGER</span><span><b data-count="loaded">0</b> <span data-word="loaded"></span></span><span><b data-count="settings">0</b> <span data-word="settings"></span></span></div>
    <div class="hd-sp"></div>
    <div class="seg"><span data-lang="en">EN</span><span data-lang="zh">中文</span></div>
    <div class="hint-k"><span data-part="openKey"><span class="kbd" data-count="openKey"></span> <span data-word="toggle"></span></span><span class="kbd">ESC</span> <span data-word="close"></span></div>
    <div class="close-btn" data-act="close">&#x2715;</div>
  </div>
  <div class="bd">
    <div class="rail">
      <div class="search">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/></svg>
        <input class="find" autocomplete="off" spellcheck="false">
      </div>
      <div class="chips">
        <div class="chip on" data-filter="all"><span data-word="all"></span> <i data-count="all"></i></div>
        <div class="chip" data-filter="changed"><span data-word="changed"></span> <i data-count="changed"></i></div>
        <div class="chip" data-filter="updates"><span data-word="updates"></span> <i data-count="updates"></i></div>
        <div class="chip" data-filter="errors"><span data-word="errors"></span> <i data-count="errors"></i></div>
      </div>
      <div class="list"></div>
    </div>
    <div class="pane"></div>
  </div>
  <div class="pend"></div>
  <div class="ft"><div class="status-dot"></div><div class="ft-t"></div><div class="key-btn" data-act="copyList"></div></div>
  <div class="pk-host"></div>
</div></div>`;

  const $ = selector => root.querySelector(selector);
  const searchBox = $('.find');
  const list = $('.list');
  const pane = $('.pane');

  function fit() {
    const scale = Math.min((window.innerWidth || STAGE_WIDTH) / STAGE_WIDTH, (window.innerHeight || STAGE_HEIGHT) / STAGE_HEIGHT);

    $('.stage').style.transform = 'translate(-50%,-50%) scale(' + scale + ')';
  }

  // Hands a panel to Exiled's Toolkit to be drawn at the player's UI size. Before the Toolkit's script has loaded it
  // waits in a list the Toolkit reads; without the Toolkit nothing happens.
  function uiSize(node, options) {
    options = Object.assign({ mod: 'ModManager' }, options);
    const toolkit = window.ExiledUi;

    if (toolkit) {
      toolkit.size(node, options);
      return;
    }

    const waiting = window.__exiledUiWait = window.__exiledUiWait || [];
    waiting.push([node, options]);
    if (waiting.length > 32) waiting.shift();
  }

  // ===== Drawing =====

  // Patches a container to match new markup and keeps every node that is still there, so a redraw changes only what
  // changed: entry animations do not replay, and focus and scroll stay put.
  function morph(container, html) {
    const next = document.createElement(container.tagName);

    next.innerHTML = html;
    morphChildren(container, next);
  }

  // Rows keyed by data-mod are matched by key, so a list that moves or filters keeps each mod's row; the rest by position.
  function morphChildren(current, next) {
    const keyed = new Map();
    const wanted = Array.from(next.childNodes);

    for (const child of current.children) {
      if (keyOf(child)) keyed.set(keyOf(child), child);
    }

    wanted.forEach((want, index) => {
      const at = current.childNodes[index] || null;
      const key = keyOf(want);
      const match = key ? keyed.get(key) : at && !keyOf(at) ? at : null;

      if (key) keyed.delete(key);

      if (!match || !isSameKind(match, want)) {
        current.insertBefore(want, at);

        return;
      }

      if (match !== at) current.insertBefore(match, at);

      morphNode(match, want);
    });

    while (current.childNodes.length > wanted.length) current.removeChild(current.lastChild);
  }

  function morphNode(have, want) {
    if (have.nodeType !== 1) {
      if (have.nodeValue !== want.nodeValue) have.nodeValue = want.nodeValue;

      return;
    }

    if (have.hasAttribute('data-own')) return;

    syncAttributes(have, want);

    if (have.tagName === 'INPUT') {
      // A text box drawn without a value keeps what the player typed; a redraw on each key would wipe it otherwise.
      if (want.hasAttribute('value') && have.value !== want.value) have.value = want.value;

      if (have.checked !== want.checked) have.checked = want.checked;
    }

    morphChildren(have, want);
  }

  function syncAttributes(have, want) {
    for (const attribute of Array.from(have.attributes)) {
      if (!want.hasAttribute(attribute.name)) have.removeAttribute(attribute.name);
    }

    for (const attribute of Array.from(want.attributes)) {
      if (have.getAttribute(attribute.name) !== attribute.value) have.setAttribute(attribute.name, attribute.value);
    }
  }

  // A tab another mod adds is keyed by data-own, so its node, and whatever the mod drew in it, survives a redraw.
  function keyOf(node) {
    return node.nodeType === 1 ? node.getAttribute('data-mod') || node.getAttribute('data-own') : null;
  }

  function isSameKind(have, want) {
    return have.nodeType === want.nodeType && have.nodeName === want.nodeName;
  }

  function render() {
    const listScroll = list.scrollTop;
    const paneScroll = pane.scrollTop;

    state.rows = [];
    renderChrome();
    renderList();
    renderPane();
    renderFooter();
    renderPend();
    list.scrollTop = listScroll;
    pane.scrollTop = paneScroll;

    if (state.picker || $('.pk-host').firstChild) drawPicker(false);

    syncTyping();
  }

  function renderChrome() {
    const all = mods();
    const openKey = state.view ? state.view.openKey : '';
    const updates = all.filter(isUpdatable).length;

    const withErrors = all.filter(hasErrors).length;

    if (!updates && state.filter === 'updates' || !withErrors && state.filter === 'errors') state.filter = 'all';

    const others = otherErrors();
    const trashed = trash();
    const found = health();
    const healthCount = found.length + others.length;
    const healthWorst = healthLevel(found) === 'err' || others.some(e => e.level === 'error') ? 'err' : healthLevel(found) === 'warn' || others.length ? 'warn' : 'info';

    // HEALTH holds the install check and the errors no mod could be matched to. TRASH stays while it shows what its last button did, even when empty.
    if (state.page === 'trash' && !trashed.length && !trashNote() || state.page === 'health' && !healthCount) state.page = 'mods';

    $('.win').classList.toggle('page-trash', state.page === 'trash');
    $('.win').classList.toggle('page-health', state.page === 'health');
    $('.win').classList.toggle('page-browse', state.page === 'browse');
    $('.win').classList.toggle('page-profiles', state.page === 'profiles');
    $('[data-page="trash"]').style.display = trashed.length || state.page === 'trash' ? '' : 'none';
    setCount('trash', trashed.length);
    $('[data-page="health"]').style.display = healthCount || state.page === 'health' ? '' : 'none';
    $('[data-page="health"]').classList.toggle('err', healthWorst === 'err');
    $('[data-page="health"]').classList.toggle('warn', healthWorst === 'warn');
    setCount('health', healthCount);
    root.querySelectorAll('[data-page]').forEach(el => el.classList.toggle('on', el.dataset.page === state.page));

    root.querySelectorAll('[data-word]').forEach(el => { el.textContent = word(el.dataset.word); });
    root.querySelectorAll('[data-lang]').forEach(el => el.classList.toggle('on', el.dataset.lang === state.lang));
    root.querySelectorAll('[data-filter]').forEach(el => el.classList.toggle('on', el.dataset.filter === state.filter));
    searchBox.placeholder = word('search');
    setCount('loaded', all.filter(m => m.loaded).length);
    setCount('settings', all.reduce((sum, m) => sum + m.settings.length, 0));
    setCount('all', all.length);
    setCount('changed', all.filter(m => m.settings.some(isChanged)).length);
    setCount('updates', updates);
    setCount('errors', withErrors);
    setCount('openKey', openKey);
    $('[data-filter="updates"]').style.display = updates ? '' : 'none';
    $('[data-filter="errors"]').style.display = withErrors ? '' : 'none';
    $('[data-part="openKey"]').style.display = openKey && openKey !== 'None' ? '' : 'none';
  }

  function setCount(name, value) {
    $('[data-count="' + name + '"]').textContent = String(value);
  }

  function visibleMods() {
    const q = state.query.toLowerCase();
    let shown = mods();

    if (state.filter === 'changed') shown = shown.filter(m => m.settings.some(isChanged));
    else if (state.filter === 'updates') shown = shown.filter(isUpdatable);
    else if (state.filter === 'errors') shown = shown.filter(hasErrors);

    if (q) shown = shown.filter(m => [m.name, m.nameZh, m.plugin, m.guid].some(text => text.toLowerCase().indexOf(q) >= 0) || m.settings.some(s => matchesQuery(m, s, q)));

    return shown;
  }

  function renderList() {
    const shown = visibleMods();
    const groups = [
      [word('groupLoaded'), shown.filter(m => m.state === 'loaded' && !isLibraryShelf(m)), false],
      [word('groupNotLoaded'), shown.filter(m => m.state === 'notLoaded'), false],
      [word('groupOff'), shown.filter(m => m.state === 'off'), false],
      [word('groupLibraries'), shown.filter(isLibraryShelf), true]
    ];
    const problems = state.view ? state.view.problems : [];
    let html = '';
    let delay = 0;

    for (const [title, members, isShelf] of groups) {
      if (!members.length) continue;

      const isOpen = !isShelf || state.showLibraries || !!state.query;
      const arrow = isShelf ? ' <span class="arrow">' + (isOpen ? '&#x25BE;' : '&#x25B8;') + '</span>' : '';

      html += '<div class="grp' + (isShelf ? ' tog" data-act="libraries' : '') + '">' + esc(title) + ' &middot; ' + members.length + arrow + '</div>';

      if (!isOpen) continue;

      for (const m of members) {
        html += modRow(m, Math.min(delay++ * 18, 500));
      }
    }

    if (!html) html = '<div class="empty"><div class="big">' + esc(word('nothing')) + '</div>' + esc(word('noModMatches')) + '</div>';

    const installable = mods().filter(canInstall).length;

    if (state.filter === 'updates' && !state.query) html = installAllHeader(installable) + html;

    if (problems.length) html += '<div class="grp">' + esc(word('problems')) + ' &middot; ' + problems.length + '</div>' + problems.map(p => '<div class="problem">' + esc(p) + '</div>').join('');

    morph(list, html);
  }

  // INSTALL ALL UPDATES, then a line for each kind of update it left alone, so none is missed in the list.
  function installAllHeader(installable) {
    const asking = mods().filter(m => hasUpdate(m) && m.install.kind === 'needsApproval').length;
    const refused = mods().filter(m => hasUpdate(m) && m.install.kind === 'refused').length;
    const broken = mods().filter(isHeld).length;
    const held = (count, key) => count ? '<div class="upd-line bad held-line">' + esc(word(key)(count)) + '</div>' : '';

    return (installable ? '<div class="key-btn all-btn" data-act="installAll">' + esc(word('installAll')(installable)) + '</div>' : '') +
      held(asking, 'heldAsking') + held(refused, 'heldRefused') + held(broken, 'heldBroken');
  }

  function otherErrors() {
    return state.view ? state.view.otherErrors : [];
  }


  function trash() {
    return state.view && state.view.trash ? state.view.trash : [];
  }

  function trashNote() {
    return state.modNote && state.modNote.onTrash ? state.modNote : null;
  }

  // The TRASH page beside MODS: every uninstalled mod, with RESTORE, and EMPTY TRASH for the ones the game is not running.
  function trashPage() {
    const items = trash();
    const note = trashNote();
    const deletable = items.filter(t => !t.thisSession).length;
    const shownNote = note ? notice(note.warn ? 'warn' : 'ok', '', state.lang === 'zh' ? note.zh : note.en) : '';
    const confirm = state.confirmEmpty && deletable
      ? '<div class="notice warn"><span class="ico">!</span><div><b>' + esc(word('emptyConfirm')(deletable)) + '</b><div class="acts"><div class="key-btn danger" data-act="emptyYes">' + esc(word('emptyYes')) + '</div><div class="key-btn" data-act="emptyNo">' + esc(word('cancel')) + '</div></div></div></div>'
      : '';
    const button = deletable && !state.confirmEmpty ? '<div class="tr-acts"><div class="key-btn danger" data-act="emptyTrash">' + esc(word('emptyTrash')) + '</div></div>' : '';
    const rows = items.length ? items.map(trashRow).join('') : '<div class="empty"><div class="big">' + esc(word('trashEmpty')) + '</div></div>';

    return '<div class="errs-page trash-page"><div class="sec-t">' + esc(word('trashTitle')) + ' &middot; ' + items.length + '</div>' +
      '<div class="cf-intro">' + esc(word('trashIntro')) + '</div>' + shownNote + button + confirm + rows + '</div>';
  }

  function browse() {
    return state.view && state.view.browse ? state.view.browse : [];
  }

  const BROWSE_FILTERS = { all: 'bwAll', installable: 'bwInstallable', manual: 'bwManual', installed: 'bwInstalledF' };
  const BROWSE_SORTS = { endorsed: 'bwSortEndorsed', downloads: 'bwSortDownloads', updated: 'bwSortUpdated', name: 'bwSortName' };

  function shownBrowse() {
    const q = state.browseQuery.toLowerCase();
    const sorts = {
      endorsed: (a, b) => b.endorsements - a.endorsements || a.name.localeCompare(b.name),
      downloads: (a, b) => b.downloads - a.downloads || a.name.localeCompare(b.name),
      updated: (a, b) => (b.updated || '').localeCompare(a.updated || '') || a.name.localeCompare(b.name),
      name: (a, b) => a.name.localeCompare(b.name)
    };

    return browse()
      .filter(b => state.browseFilter === 'all' || b.support === state.browseFilter)
      .filter(b => !q || [b.name, b.author, b.summary, b.guid || ''].some(text => text.toLowerCase().indexOf(q) >= 0))
      .sort(sorts[state.browseSort] || sorts.endorsed);
  }

  // The BROWSE page beside MODS: every mod for the game, installed from here when its author set it up for the Mod Manager.
  function browsePage() {
    const feed = state.view ? state.view.browseFeed : 'off';
    const all = browse();
    const head = '<div class="errs-page browse-page"><div class="sec-t">' + esc(word('browseTitle')) + ' &middot; ' + all.length + '</div><div class="cf-intro">' + esc(word('browseIntro')) + '</div>';

    if (feed === 'off') return head + notice('warn', '', word('browseOff')) + '</div>';

    if (!all.length) return head + '<div class="empty"><div class="big">' + esc(word(feed === 'asking' ? 'browseAsking' : 'browseNone')) + '</div></div></div>';

    const count = kind => all.filter(b => kind === 'all' || b.support === kind).length;
    const chips = (table, current, attribute, withCount) => Object.keys(table).map(key => '<div class="chip' + (key === current ? ' on' : '') + '" ' + attribute + '="' + key + '"><span>' + esc(word(table[key])) + '</span>' + (withCount ? ' <i>' + count(key) + '</i>' : '') + '</div>').join('');
    const bar = '<div class="bw-bar"><input class="bw-find" autocomplete="off" spellcheck="false" placeholder="' + esc(word('browseFind')) + '">' +
      '<div class="chips">' + chips(BROWSE_FILTERS, state.browseFilter, 'data-bwfilter', true) + '</div><div class="chips">' + chips(BROWSE_SORTS, state.browseSort, 'data-bwsort', false) + '</div></div>';
    const shown = shownBrowse();
    const asking = feed === 'asking' ? '<div class="cf-intro">' + esc(word('browseAsking')) + '</div>'
      : all.some(b => b.support === 'unknown') ? notice('warn', '', word('bwNoIndex')) : '';
    const cards = shown.length ? '<div class="bw-grid">' + shown.map(browseCard).join('') + '</div>' : '<div class="empty"><div class="big">' + esc(word('browseNone')) + '</div></div>';

    return head + bar + asking + cards + '</div>';
  }

  function browseCard(b) {
    const button = (act, text, cls) => '<div class="key-btn' + (cls ? ' ' + cls : '') + '" data-act="' + act + '">' + esc(text) + '</div>';
    const facts = [b.author ? word('by') + ' ' + b.author : '', b.version ? 'v' + b.version : '', b.nexus ? word('bwEndorsed')(b.endorsements) : '', b.nexus ? word('bwDownloads')(b.downloads) : ''].filter(Boolean).join(' \u00b7 ');
    const thumb = b.thumbnail ? '<div class="bw-th" style="background-image:url(&quot;' + esc(b.thumbnail) + '&quot;)"></div>' : '<div class="bw-th none"></div>';
    const nexus = b.nexus ? button('bwNexus', word('bwOpenNexus') + ' \u2197') : '';
    let status = '';
    let acts = '';

    if (b.support === 'installed') status = '<div class="bw-tag ok">' + esc(word('bwInstalled')) + '</div>';
    else if (b.support === 'unknown') status = '<div class="bw-tag">' + esc(word('bwUnknown')) + '</div>';
    else if (b.support === 'manual') {
      // Folded to its one-line title until the player opens it, so the list stays about the mods.
      const open = !!state.bwShameOpen[b.key];
      const asked = state.asked && state.asked.key === b.key ? '<div class="upd-line' + (state.asked.ok ? ' ok' : ' bad') + '">' + esc(word(state.asked.ok ? 'bwAsked' : 'bwAskFailed')) + '</div>' : '';
      const body = open ? '<div class="bw-shame-b">' + esc(word('bwShameBody')) + '</div>' + (b.nexus ? '<div class="acts">' + button('bwAsk', word('bwAsk')) + '</div>' : '') : '';

      status = '<div class="bw-shame' + (open ? ' open' : '') + '"><div class="bw-shame-t" data-act="bwShame"><span class="chev">' + (open ? '\u25BE' : '\u25B8') + '</span>' + esc(word('bwShameTitle')) + '</div>' + body + '</div>' + asked;
    } else {
      const install = b.install;

      switch (install.kind) {
        case 'installing': status = '<div class="upd-line">' + esc(word('bwInstalling')) + '</div>'; break;
        case 'installed': status = '<div class="upd-line ok">' + esc(word('bwDone')(tagText(install.tag))) + '</div>'; break;
        case 'failed': status = '<div class="upd-line bad">' + esc(word('bwFailed')(install.reason)) + '</div>'; break;
        case 'refused': status = '<div class="upd-line bad">' + esc(word('bwRefused')(install.reason)) + '</div>'; break;
        case 'needsApproval': status = '<div class="upd-line bad">' + esc(word('bwApproval')(tagText(install.tag), install.powers.map(power => word('powers')[power] || power))) + '</div>'; break;
      }

      if (b.needs.length) status += '<div class="upd-line bad">' + esc(word('bwNeeds')(b.needs.map(n => n.name))) + '</div>' + (b.needs.some(n => n.nexus) ? '<div class="bw-needs">' + b.needs.filter(n => n.nexus).map(n => '<div class="key-btn" data-act="bwNeedNexus" data-nexus="' + n.nexus + '">' + esc(n.name) + ' \u2197</div>').join('') + '</div>' : '');

      acts = install.kind === 'none' ? button('bwInstall', word('bwInstall'), 'go')
        : install.kind === 'failed' ? button('bwInstall', word('tryAgain'))
        : install.kind === 'needsApproval' ? button('bwApprove', word('installAnyway'))
        : install.kind === 'installed' && b.fresh ? button('bwUndo', word('undo'))
        : '';
    }

    return '<div class="bw-card ' + esc(b.support) + '" data-bw="' + esc(b.key) + '">' + thumb + '<div class="bw-body"><div class="mname">' + esc(b.name) + '</div>' +
      (facts ? '<div class="msub">' + esc(facts) + '</div>' : '') + (b.summary ? '<div class="bw-sum">' + esc(b.summary) + '</div>' : '') + status +
      '<div class="acts">' + acts + nexus + '</div></div></div>';
  }

  function browseOf(el) {
    const card = el.closest('[data-bw]');

    return card ? browse().find(b => b.key === card.dataset.bw) || null : null;
  }

  function profiles() {
    return state.view && state.view.profiles ? state.view.profiles : { saved: [], check: null, problem: null, note: null };
  }

  // The PROFILES page: saved setups, a box to paste a shared code or mod list in, and what the one opened would change.
  function profilesPage() {
    const p = profiles();
    const button = (act, text, cls, name) => '<div class="key-btn' + (cls ? ' ' + cls : '') + '" data-act="' + act + '"' + (name !== undefined ? ' data-pf="' + esc(name) + '"' : '') + '>' + esc(text) + '</div>';
    const bilingual = text => text ? (state.lang === 'zh' ? text.zh : text.en) : '';
    const note = p.note ? notice('ok', '', bilingual(p.note)) : '';
    const save = '<div class="pf-save"><input class="pf-name" autocomplete="off" spellcheck="false" maxlength="60" placeholder="' + esc(word('pfName')) + '">' + button('pfSave', word('pfSave'), 'go') + '</div>';
    const rows = p.saved.length ? p.saved.map(profileRow).join('') : '<div class="empty"><div class="big">' + esc(word('pfNone')) + '</div></div>';
    const paste = '<div class="sec-t pf-sub">' + esc(word('pfPasteTitle')) + '</div><div class="cf-intro">' + esc(word('pfPasteHint')) + '</div>' +
      '<textarea class="pf-paste" spellcheck="false" rows="3"></textarea><div class="tr-acts">' + button('pfPaste', word('pfShow')) + '</div>' +
      (p.problem ? notice('warn', '', bilingual(p.problem)) : '');

    return '<div class="errs-page profiles-page"><div class="sec-t">' + esc(word('pfTitle')) + ' &middot; ' + p.saved.length + '</div>' +
      '<div class="cf-intro">' + esc(word('pfIntro')) + '</div>' + note + profileCheck(p.check) + save + rows + paste + '</div>';
  }

  function profileRow(pf) {
    const button = (act, text, cls) => '<div class="key-btn' + (cls ? ' ' + cls : '') + '" data-act="' + act + '" data-pf="' + esc(pf.name) + '">' + esc(text) + '</div>';
    const copied = state.pfCopied && state.pfCopied.name === pf.name && state.pfCopied.ok !== null ? state.pfCopied.ok : null;
    const copy = '<div class="key-btn' + (copied === true ? ' ok' : copied === false ? ' bad' : '') + '" data-act="pfCopy" data-pf="' + esc(pf.name) + '">' + esc(word(copied === null ? 'pfCopy' : copied ? 'copied' : 'copyFailed')) + '</div>';
    const confirm = state.pfDelete === pf.name
      ? '<div class="notice warn"><span class="ico">!</span><div><b>' + esc(word('pfDeleteAsk')(pf.name)) + '</b><div class="acts">' + button('pfDeleteYes', word('pfDeleteYes'), 'danger') + button('pfDeleteNo', word('cancel')) + '</div></div></div>'
      : '';

    return '<div class="tr-item pf-item" data-profile="' + esc(pf.name) + '"><div><div class="mname">' + esc(pf.name) + '</div><div class="msub">' + esc(word('pfFacts')(pf.mods, pf.on, pf.settings)) + '</div></div>' +
      '<div class="pf-acts">' + button('pfShow', word('pfShow')) + copy + button('pfDelete', word('pfDelete'), 'danger') + '</div></div>' + confirm;
  }

  // What using the profile opened would change, grouped the way it happens: switches and installs next start, settings now.
  function profileCheck(check) {
    if (!check) return '';

    const value = v => v === '' ? word('pfBlank') : v;
    const of = kind => check.steps.filter(step => step.kind === kind);
    const group = (title, items, row) => items.length ? '<div class="hl-item info pf-group"><div class="mname">' + esc(title) + ' &middot; ' + items.length + '</div><div class="hl-b">' + items.map(row).join('') + '</div></div>' : '';
    const line = text => '<div class="pf-line">' + esc(text) + '</div>';
    const installLine = step => {
      const install = step.install || { kind: 'none' };
      const status = install.kind === 'installing' ? word('bwInstalling') : install.kind === 'installed' ? word('bwDone')(tagText(install.tag)) : install.kind === 'failed' ? word('bwFailed')(install.reason) : install.kind === 'refused' ? word('bwRefused')(install.reason) : install.kind === 'needsApproval' ? word('bwApproval')(tagText(install.tag), install.powers.map(power => word('powers')[power] || power)) : '';

      return '<div class="pf-line">' + esc(step.name) + (status ? ' <span class="pf-st">' + esc(status) + '</span>' : '') + (install.kind === 'needsApproval' ? ' <span class="key-btn" data-act="pfApprove" data-guid="' + esc(step.guid) + '">' + esc(word('installAnyway')) + '</span>' : '') + '</div>';
    };
    const missingLine = step => '<div class="pf-line">' + esc(step.name) + (step.nexus ? ' <span class="key-btn" data-act="bwNeedNexus" data-nexus="' + step.nexus + '">' + esc(word('bwOpenNexus')) + '</span>' : '') + '</div>';
    const setLine = step => '<div class="pf-line">' + esc(step.name + ': ' + step.label) + ' <span class="pf-val">' + esc(value(step.from)) + '</span> &rarr; <span class="pf-val to">' + esc(value(step.to)) + '</span></div>';
    const acts = '<div class="tr-acts">' + (check.steps.some(step => step.kind === 'on' || step.kind === 'off' || step.kind === 'set' || step.kind === 'install' && (step.install.kind === 'none' || step.install.kind === 'failed')) ? '<div class="key-btn go" data-act="pfUse">' + esc(word('pfUse')) + '</div>' : '') +
      (check.pasted ? '<div class="key-btn" data-act="pfKeep">' + esc(word('pfKeep')) + '</div>' : '') + '<div class="key-btn" data-act="pfClose">' + esc(word('pfClose')) + '</div></div>';
    const body = check.steps.length
      ? group(word('pfOn'), of('on'), step => line(step.name)) + group(word('pfOff'), of('off'), step => line(step.name)) + group(word('pfInstall'), of('install'), installLine) +
        group(word('pfMissing'), of('missing'), missingLine) + group(word('pfSet'), of('set'), setLine) + group(word('pfWait'), of('wait'), step => line(word('pfWaitRow')(step.name, step.count))) +
        group(word('pfVersion'), of('version'), step => line(word('pfVersionRow')(step.name, step.installed, step.wanted))) + group(word('pfStays'), of('stays'), step => line(step.name))
      : '<div class="cf-intro">' + esc(word('pfNothing')) + '</div>';

    return '<div class="pf-check"><div class="sec-t pf-sub">' + esc(word('pfChanges')(check.name)) + '</div>' + (check.withSettings ? '' : '<div class="cf-intro">' + esc(word('pfListOnly')) + '</div>') + body + acts + '</div>';
  }

  function health() {
    return state.view && state.view.health ? state.view.health : [];
  }

  // How much each finding matters: err keeps a mod from starting the way it should, warn may, info only explains.
  const HEALTH_LEVEL = { bepinex: 'warn', toolkit: 'err', sameGuid: 'err', sameDll: 'warn', nested: 'warn', unusedCfg: 'info' };

  function healthLevel(found) {
    return found.some(f => HEALTH_LEVEL[f.kind] === 'err') ? 'err' : found.some(f => HEALTH_LEVEL[f.kind] === 'warn') ? 'warn' : 'info';
  }

  // The HEALTH page beside MODS: what is wrong with how BepInEx and the mods are installed, and every error no mod could
  // be found for. Each part is worst first, the part with the worst finding comes first, and one line counts them all.
  function healthPage() {
    const rank = { err: 0, warn: 1, info: 2 };
    const found = health().slice().sort((a, b) => rank[HEALTH_LEVEL[a.kind] || 'info'] - rank[HEALTH_LEVEL[b.kind] || 'info']);
    const others = otherErrors().slice().sort((a, b) => (a.level === 'error' ? 0 : 1) - (b.level === 'error' ? 0 : 1) || b.count - a.count);

    if (!found.length && !others.length) return '<div class="errs-page health-page"><div class="empty"><div class="big">' + esc(word('healthAllClear')) + '</div></div></div>';

    const tally = { err: 0, warn: 0, info: 0 };

    found.forEach(f => { tally[HEALTH_LEVEL[f.kind] || 'info']++; });
    others.forEach(e => { tally[e.level === 'error' ? 'err' : 'warn']++; });

    const summary = '<div class="hl-sum">' + ['err', 'warn', 'info'].filter(level => tally[level]).map(level => '<span class="hl-pip ' + level + '">' + esc(word('healthSum')[level](tally[level])) + '</span>').join('') + '</div>';
    const install = '<div class="hl-sec"><div class="sec-t">' + esc(word('healthTitle')) + ' &middot; ' + found.length + '</div><div class="cf-intro">' + esc(word('healthIntro')) + '</div>' +
      (found.length ? found.map(healthItem).join('') : '<div class="hl-clear">' + esc(word('healthNone')) + '</div>') + '</div>';
    const logged = others.length ? '<div class="hl-sec"><div class="sec-t">' + esc(word('otherErrors')) + ' &middot; ' + others.length + '</div><div class="cf-intro">' + esc(word('otherErrorsIntro')) + '</div>' + others.map(errorBlock).join('') + '</div>' : '';
    const installFirst = found.length && (healthLevel(found) === 'err' || !others.some(e => e.level === 'error'));

    return '<div class="errs-page health-page">' + summary + (installFirst || !others.length ? install + logged : logged + install) + '</div>';
  }

  function healthItem(f) {
    const files = list => '<div class="hl-files">' + list.map(file => '<div>' + esc(file) + '</div>').join('') + '</div>';
    const item = (title, body, more) => '<div class="hl-item ' + (HEALTH_LEVEL[f.kind] || 'info') + '" data-health="' + esc(f.kind) + '"><div class="mname">' + esc(title) + '</div>' +
      (body ? '<div class="hl-b">' + esc(body) + '</div>' : '') + (more || '') + '</div>';

    switch (f.kind) {
      case 'bepinex': return item(word('hBepInEx')(f.found), word('hBepInExBody')(f.tested));
      case 'toolkit': return item(word('hToolkit')(f.mod), word('hToolkitBody')(f.range, f.installed));
      case 'sameGuid': return item(word('hSameGuid')(f.guid), word('hSameGuidBody'), files(f.files));
      case 'sameDll': return item(word('hSameDll'), word('hSameDllBody'), files(f.files));
      case 'nested': return item(word('hNested'), word('hNestedBody'), files([f.folder]));
      case 'unusedCfg': return item(word('hUnused'), word('hUnusedBody'), files(f.files));
      default: return '';
    }
  }

  function trashRow(t) {
    const tags = [t.version ? 'v' + t.version : '', t.when, t.settings ? word('trashSettings') : '', t.thisSession ? word('trashThisSession') : ''].filter(tag => tag);

    return '<div class="tr-item" data-trash-item="' + esc(t.id) + '"><div><div class="mname">' + esc(t.name) + '</div><div class="msub">' + tags.map(esc).join(' &middot; ') + '</div></div>' +
      '<div class="key-btn" data-act="restore" data-trash="' + esc(t.id) + '">' + esc(word('restore')) + '</div></div>';
  }

  // The question UNINSTALL asks first: what the mod can do for saves, what stops with it, and whether its settings file goes too.
  function uninstallConfirm(m) {
    if (state.confirmUninstall !== m.guid || m.switch.kind !== 'allowed') return '';

    const prep = m.settings.find(s => s.section === 'Uninstall' && s.key === 'UninstallPrep');
    let prepLine = '';

    if (prep && prep.current === 'true') prepLine = '<div>' + esc(word('prepOn')) + '</div>';
    else if (prep) prepLine = '<div>' + esc(word('prepOff')) + '</div><div class="acts"><div class="key-btn" data-act="prepOn">' + esc(word('prepTurnOn')) + '</div></div>';
    else if (m.uninstallPrep) prepLine = '<div>' + esc(word('prepNotRunning')) + '</div>';

    const stops = m.switch.stops.length ? '<div>' + esc(word('alsoStops')(m.switch.stops.join(', '))) + '</div>' : '';
    const hasSettingsFile = !m.loaded || m.settings.length > 0;
    const settingsBox = hasSettingsFile ? '<label class="un-cfg"><input type="checkbox" data-uncfg="1"' + (state.removeSettings ? ' checked' : '') + '><span>' + esc(word('removeSettings')) + '</span></label>' : '';

    return '<div class="notice warn"><span class="ico">!</span><div><b>' + esc(word('uninstallTitle')(nameOf(m))) + '</b> ' + esc(word('uninstallBody')) + prepLine + stops + settingsBox +
      '<div class="acts"><div class="key-btn danger" data-act="uninstallYes">' + esc(word('uninstall')) + '</div><div class="key-btn" data-act="uninstallNo">' + esc(word('cancel')) + '</div></div></div></div>';
  }

  function hasErrors(m) {
    return m.errors.length > 0;
  }

  function hasLoggedErrors(m) {
    return m.errors.some(e => e.level === 'error');
  }

  // The update's mark in the list: RESTART once it is in place, ASK or REFUSED when its checks stopped it.
  function updatePip(m) {
    const pip = (cls, key) => '<span class="pip ' + cls + '">' + esc(word(key)) + '</span>';

    if (m.install.kind === 'installed' || (m.rollback.kind === 'done' && !m.rollback.same)) return pip('chg', 'pipRestart');

    if (hasNexusUpdate(m)) return pip('upd', 'pipNexus');

    if (!hasUpdate(m)) return '';

    if (isHeld(m)) return pip('wrnp', 'pipHeld');

    switch (m.install.kind) {
      case 'needsApproval': return pip('wrnp', 'pipAsk');
      case 'refused': return pip('errp', 'pipRefused');
      default: return pip('upd', 'pipUpdate');
    }
  }

  function modRow(m, delay) {
    const changed = m.settings.filter(isChanged).length;
    const sub = m.loaded ? esc(m.version) + ' &middot; ' + esc(m.settings.length ? word('settingCount')(m.settings.length) : word('noSettings')) : esc(word(m.state === 'off' ? 'offSub' : 'notLoadedSub'));
    const isSelected = m.guid === state.selected && !state.query;
    const pips = (changed ? '<span class="pip chg">' + changed + '</span>' : '') +
      (m.state === 'off' && m.switch.kind !== 'uninstalled' ? '<span class="pip offp">' + esc(word('statusOff')) + '</span>' : '') +
      (m.switch.kind === 'uninstalled' ? '<span class="pip offp">' + esc(word('pipUninstalled')) + '</span>' : '') +
      (isPending(m) ? '<span class="pip chg">' + esc(word('pipRestart')) + '</span>' : '') +
      (m.untested ? '<span class="pip ' + (hasLoggedErrors(m) ? 'warnp' : 'untp') + '">' + esc(word('pipUntested')) + '</span>' : '') +
      (hasLoggedErrors(m) ? '<span class="pip errp">' + esc(word('pipError')) + '</span>' : hasErrors(m) ? '<span class="pip wrnp">' + esc(word('pipWarning')) + '</span>' : '') +
      (m.known.broken ? '<span class="pip errp">' + esc(word('pipBroken')) + '</span>' : '') +
      (isMissingGameCode(m) ? '<span class="pip wrnp">' + esc(word('pipOutdated')) + '</span>' : '') +
      updatePip(m);

    return '<div class="mod' + (isSelected ? ' sel' : '') + (m.loaded ? '' : ' off') + '" data-mod="' + esc(m.guid) + '" style="animation-delay:' + delay + 'ms">' +
      badge(m, 'mono') +
      '<div><div class="mname">' + esc(nameOf(m)) + '</div><div class="msub">' + sub + '</div></div>' +
      '<div>' + pips + '</div></div>';
  }

  function renderPane() {
    if (state.page === 'trash' || state.page === 'health' || state.page === 'browse' || state.page === 'profiles') {
      showOwnTab(null, null);
      morph(pane, state.page === 'trash' ? trashPage() : state.page === 'health' ? healthPage() : state.page === 'browse' ? browsePage() : profilesPage());

      return;
    }

    if (state.query) {
      showOwnTab(null, null);
      renderSearch();

      return;
    }

    const m = modByGuid(state.selected);

    if (!m) {
      showOwnTab(null, null);
      morph(pane, '');

      return;
    }

    const names = sections(m);

    if (names.indexOf(state.tab) < 0) state.tab = '';

    if (state.side === 'conflicts' && !conflictCount(m) || state.side === 'changelog' && m.changelog.kind === 'none' || state.side === 'errors' && !hasErrors(m)) state.side = '';

    if (isOwnSide(state.side) && !ownTabOf(m, state.side)) state.side = '';

    const ownTab = ownTabOf(m, state.side);
    const railKey = m.guid + '\n' + state.lang;
    const top = ownTab ? hero(m, true) : hero(m, false) + meta(m) + about(m) + links(m) + uninstallConfirm(m) + relations(m) + fixedHotkeys(m) + untestedNotice(m) + fitNotice(m) + brokenNotice(m) + relationNotice(m) + updateNotice(m) + nexusNotice(m) + rollbackNotice(m);
    const body = modNotices(m) + paneBody(m, names);

    morph(pane, top + (state.rail[railKey] ? '<div class="railed">' + tabs(m, names) + '<div>' + body + '</div></div>' : tabs(m, names) + body));
    showOwnTab(m, ownTab);
    askForNotes(m);
    askForChangelog(m);

    if (!state.rail[railKey] && tabsWrap()) {
      state.rail[railKey] = true;
      renderPane();
    }
  }

  // Tabs that need a second row move to a column beside the settings, which has room for any number of them.
  function tabsWrap() {
    const bar = pane.querySelector('.tabs');

    if (!bar || !bar.children.length) return false;

    return bar.lastElementChild.offsetTop > bar.firstElementChild.offsetTop;
  }

  // Release notes are fetched the first time a mod's update is shown, not for every mod at the start.
  function askForNotes(m) {
    if (!hasUpdate(m) || m.update.notes.kind !== 'notAsked' || state.notesAsked[m.guid]) return;

    state.notesAsked[m.guid] = true;
    send('notes', [m.guid]);
  }

  // A changelog is fetched the first time its tab is opened.
  function askForChangelog(m) {
    if (state.side !== 'changelog' || m.changelog.kind !== 'notAsked' || state.changelogAsked[m.guid]) return;

    state.changelogAsked[m.guid] = true;
    send('changelog', [m.guid]);
  }

  // A mod whose switch now points the other way from how the game started it.
  function isPending(m) {
    return m.switch.kind === 'allowed' && m.switch.next !== (m.state !== 'off');
  }

  function stateWord(m) {
    if (m.state === 'off') return word('statusOff');

    return m.loaded ? word('statusLoaded') : word('statusNotLoaded');
  }

  // Compact while another mod's tab is on, so its page gets the room.
  function hero(m, isCompact) {
    const sub = m.loaded ? (m.settings.length ? word('found') : word('foundNone')) : '';
    const banner = state.banners[m.guid];

    return '<div class="hero' + (isCompact ? ' compact' : '') + '">' + (banner ? '<img class="banner" src="' + esc(banner) + '" alt="">' : '') +
      '<div class="gen' + (banner ? ' on-banner' : '') + '" style="--hue-a:hsla(' + hue(m.name) + ',50%,35%,.45)">' +
      badge(m, 'big') +
      '<div><div class="gt">' + esc(nameOf(m)) + '</div><div class="gs">' + esc(sub) + '</div></div></div>' +
      '<div class="hero-side">' + enSwitch(m) + '</div></div>';
  }

  // The on/off switch: where the mod's DLL is now, which is what the next start uses.
  function enSwitch(m) {
    const sw = m.switch;
    const toggle = (isOn, isLocked) => '<label class="setting-toggle' + (isLocked ? ' locked' : '') + '"><input type="checkbox" data-switch="1"' + (isOn ? ' checked' : '') + (isLocked ? ' disabled' : '') + '><span class="track"></span><span class="knob"></span></label>';
    const box = (label, small, isOff, extra, title) => '<div class="en-switch' + (isPending(m) ? ' pend' : '') + '"' + (title ? ' title="' + esc(title) + '"' : '') + '><div class="lbl' + (isOff ? ' off' : '') + '">' + esc(label) + '<small>' + esc(small) + '</small></div>' + extra + '</div>';

    switch (sw.kind) {
      case 'allowed': {
        const small = isPending(m) ? word(sw.next ? 'onNext' : 'offNext') : word('sinceStart');

        return box(stateWord(m), small, !m.loaded, toggle(sw.next, false), '');
      }
      case 'held':
        return box(stateWord(m), word('heldUntilRestart'), !m.loaded, toggle(sw.next, true), '');
      case 'uninstalled':
        return box(word('statusUninstalled'), word('uninstalledNext'), true, '<div class="key-btn" data-act="restore" data-trash="' + esc(sw.trash) + '">' + esc(word('restore')) + '</div>', '');
      case 'locked':
        return box(word('statusRequired'), word(sw.self ? 'lockedSelf' : 'lockedNeeded'), false, toggle(true, true), sw.neededBy.length ? word('neededBy')(sw.neededBy.join(', ')) : '');
      default:
        return box(stateWord(m), word('cannotMove'), !m.loaded, '', '');
    }
  }

  function meta(m) {
    const file = m.file ? '<span class="id"><b>' + esc(m.file) + '</b></span>' : '';
    const author = m.author ? '<span class="id">' + esc(word('by')) + ' <b>' + esc(m.author) + '</b></span>' : '';
    const category = m.category && word('categories')[m.category] ? '<span class="id"><b>' + esc(word('categories')[m.category]) + '</b></span>' : '';
    const library = m.library ? '<span class="id"><b>' + esc(word('library')) + '</b></span>' : '';

    const nexus = m.known.nexus && !m.known.nexus.newer ? '<span class="id upd-ok" title="' + esc(m.known.nexus.version) + '">' + esc(word('nexusCurrent')) + '</span>' : '';

    return '<div class="meta"><span class="id">v<b>' + esc(m.version) + '</b></span>' + updateStatus(m.update) + nexus + author + category + library + '<span class="id"><b>' + esc(m.guid) + '</b></span>' + file + '</div>';
  }

  // How the update check went, beside the version. An available update gets its own notice instead.
  function updateStatus(update) {
    const status = (kind, text, title) => '<span class="id' + (kind ? ' ' + kind : '') + '"' + (title ? ' title="' + esc(title) + '"' : '') + '>' + esc(text) + '</span>';

    switch (update.kind) {
      case 'checking': return status('', word('updChecking'), '');
      case 'current': return status('upd-ok', word('updCurrent'), update.tag);
      case 'noReleases': return status('', word('updNoReleases'), '');
      case 'failed': return status('upd-bad', word('updFailed'), update.reason);
      case 'unreadable': return status('', word('updUnreadable')(update.tag), '');
      default: return '';
    }
  }

  // An update known not to work says why in place of the install line and offers no install button.
  function updateNotice(m) {
    if (!hasUpdate(m)) return '';

    const line = isHeld(m) ? '<div class="upd-line bad">' + esc(word('heldUpdate')(tagText(m.update.tag))) + ' ' + esc(textOf(m.known.updateHeld)) + '</div>' : installLine(m.install);
    const button = m.rollback.kind === 'done' || isHeld(m) ? '' : installButton(m.install);

    return '<div class="notice upd"><span class="ico">&#x2191;</span><div class="upd-main"><b>' + esc(word('updateTitle')(tagText(m.update.tag))) + '</b> ' + esc(word('updateBody')(m.version)) +
      releaseNotes(m.update.notes) + line + '<div class="upd-act">' + button + '<div class="key-btn" data-act="update">' + esc(word('getUpdate')) + ' &#x2197;</div></div></div></div>';
  }

  function isMissingGameCode(m) {
    return !!m.fit && m.fit.missingCount > 0;
  }

  // What the mod's DLL uses from the game's code that this game version no longer has, read at the start.
  function fitNotice(m) {
    if (!isMissingGameCode(m)) return '';

    const shown = 8;
    const names = m.fit.missing.slice(0, shown).map(esc).join('<br>') +
      (m.fit.missingCount > shown ? '<br>' + esc(word('moreMethods')(m.fit.missingCount - shown)) : '');

    return '<div class="notice warn"><span class="ico">!</span><div><b>' + esc(word('fitTitle')) + '</b> ' + esc(word('fitBody')(m.fit.missingCount, m.fit.checked)) + '<div class="fit-names">' + names + '</div></div></div>';
  }

  // The installed version is on the wiki's list of broken builds.
  function brokenNotice(m) {
    if (!m.known.broken) return '';

    return '<div class="notice warn"><span class="ico">!</span><div><b>' + esc(word('brokenTitle')) + '</b> ' + esc(textOf(m.known.broken)) + '</div></div>';
  }

  // A newer version on Nexus Mods, with a button to its page: the Mod Manager cannot download from Nexus.
  function nexusNotice(m) {
    if (!hasNexusUpdate(m)) return '';

    return '<div class="notice upd"><span class="ico">&#x2191;</span><div class="upd-main"><b>' + esc(word('nexusTitle')(m.known.nexus.version)) + '</b> ' + esc(word('nexusBody')(m.version)) +
      '<div class="upd-act"><div class="key-btn" data-act="nexus">' + esc(word('linkNexus')) + ' &#x2197;</div></div></div></div>';
  }

  // "a, b and c": the English list of what an update newly does.
  function andList(items) {
    return items.length < 2 ? items.join('') : items.slice(0, -1).join(', ') + ' and ' + items[items.length - 1];
  }

  function installLine(install) {
    switch (install.kind) {
      case 'installing': return '<div class="upd-line">' + esc(word('installing')) + '</div>';
      case 'installed': return '<div class="upd-line ok">' + esc(word('installed')(tagText(install.tag))) + '</div>';
      case 'failed': return '<div class="upd-line bad">' + esc(word('installFailed')(install.reason)) + '</div>';
      case 'refused': return '<div class="upd-line bad">' + esc(word('installRefused')(install.reason)) + '</div>';
      case 'needsApproval': return '<div class="upd-line bad">' + esc(word('needsApproval')(tagText(install.tag), install.powers.map(power => word('powers')[power] || power))) + '</div>';
      default: return '';
    }
  }

  function installButton(install) {
    switch (install.kind) {
      case 'none': return '<div class="key-btn" data-act="install">' + esc(word('installUpdate')) + '</div>';
      case 'failed': return '<div class="key-btn" data-act="install">' + esc(word('tryAgain')) + '</div>';
      case 'needsApproval': return '<div class="key-btn" data-act="approve">' + esc(word('installAnyway')) + '</div>';
      default: return '';
    }
  }

  // Going back to the files the last update replaced. Once done, the line stays until the next start.
  function rollbackNotice(m) {
    const button = text => '<div class="acts"><div class="key-btn" data-act="rollback">' + esc(text) + '</div></div>';

    switch (m.rollback.kind) {
      case 'available': return '<div class="notice"><span class="ico">&#x21BA;</span><div><b>' + esc(word('rollbackTitle')) + '</b> ' + esc(word('rollbackBody')(m.rollback.version)) + button(word('rollbackButton')(m.rollback.version)) + '</div></div>';
      case 'done': return '<div class="notice ok"><span class="ico">&#x21BA;</span><div>' + esc(word('rolledBack')(m.rollback.version)) + '</div></div>';
      case 'failed': return '<div class="notice warn"><span class="ico">!</span><div>' + esc(word('rollbackFailed')(m.rollback.reason)) + button(word('tryAgain')) + '</div></div>';
      default: return '';
    }
  }

  // A mod not known to work on this game version only turns red once it logs errors; until then it may well still work.
  function untestedNotice(m) {
    if (!m.untested) return '';

    const known = m.builtFor.concat(m.listedFor).filter((v, i, all) => all.indexOf(v) === i).join(' / ');

    if (hasLoggedErrors(m)) return '<div class="notice warn"><span class="ico">!</span><div><b>' + esc(word('untestedErrorsTitle')) + '</b> ' + esc(word('untestedErrorsBody')(known, state.view.game)) + '</div></div>';

    return '<div class="notice"><span class="ico">?</span><div><b>' + esc(word('untestedTitle')) + '</b> ' + esc(word('untestedBody')(known, state.view.game)) + '</div></div>';
  }

  // What the mod declares it needs, works with, or clashes with, and the mods that need or use it in turn, each with whether it will be there at the next start.
  function relations(m) {
    if (!m.relations.length && !m.dependents.length) return '';

    const isBad = r => (r.kind === 'needs' && r.status !== 'on') || (r.kind === 'doesNotWorkWith' && r.status === 'on');
    const stopsDependent = r => r.kind === 'needs' && r.status === 'on' && !isOnNext(m);
    const groups = [
      [m.relations, 'needs', 'relNeeds', r => isBad(r) && isOnNext(m), () => ''],
      [m.relations, 'worksWith', 'relWorksWith', () => false, () => ''],
      [m.relations, 'doesNotWorkWith', 'relNot', r => isBad(r) && isOnNext(m), () => ''],
      [m.dependents, 'needs', 'relNeededBy', stopsDependent, r => stopsDependent(r) ? word('relStopsThem') : ''],
      [m.dependents, 'worksWith', 'relUsedBy', () => false, () => '']
    ];
    let html = '';

    for (const [list, kind, label, bad, title] of groups) {
      const members = list.filter(r => r.kind === kind);

      if (members.length) html += '<div class="rel-h">' + esc(word(label)) + '</div><div class="rel-items">' + members.map(r => relationChip(r, bad(r), title(r))).join('') + '</div>';
    }

    return '<div class="rels">' + html + '</div>';
  }

  // The other mod's name, the version range it must be in (with the installed version when outside it), and its state.
  function relationChip(r, isBad, title) {
    const other = modByGuid(r.guid);
    const range = !r.range ? '' : '<u>' + esc(r.status === 'wrongVersion' && other ? word('hasVersion')(other.version, r.range) : r.range) + '</u>';

    return '<span class="rel' + (isBad ? ' bad' : '') + '"' + (title ? ' title="' + esc(title) + '"' : '') + (r.status === 'missing' ? '' : ' data-mod="' + esc(r.guid) + '"') + '>' + esc(relationName(r)) + range + '<i>' + esc(word('relStatus')[r.status]) + '</i></span>';
  }

  // The Conflicts tab: other mods whose Harmony patches change the same game methods, grouped by how likely they are to clash.
  function conflictsBody(m) {
    const risks = ['likely', 'may', 'harmless'];
    const shown = 8;
    const effects = list => list.map(e => word('effect')[e]).join(', ');
    const methodRow = (o, x) => '<div class="cf-m"><span class="rd ' + x.risk + '"></span><span class="cf-name">' + esc(x.name) + '</span><span class="cf-eff"><b>' + esc(word('thisMod')) + '</b> ' + esc(effects(x.mine)) + ' &middot; <b>' + esc(otherName(o)) + '</b> ' + esc(effects(x.theirs)) + '</span></div>';
    const modBlock = o => '<div class="cf-mod ' + o.risk + '"><div class="cf-head"><span class="rel" data-mod="' + esc(o.guid) + '">' + esc(otherName(o)) + '</span><span class="cf-count">' + esc(word('sharedCount')(o.methods.length)) + '</span></div>' +
      o.methods.slice(0, shown).map(x => methodRow(o, x)).join('') +
      (o.methods.length > shown ? '<div class="cf-more">' + esc(word('moreMethods')(o.methods.length - shown)) + '</div>' : '') + '</div>';
    const pill = risk => {
      const count = m.patchOverlaps.filter(o => o.risk === risk).length;

      return count ? '<span class="cf-pill"><span class="rd ' + risk + '"></span>' + count + ' ' + esc(word('risk')[risk]) + '</span>' : '';
    };
    const knownBlock = c => '<div class="cf-mod likely"><div class="cf-head"><span class="rel" data-mod="' + esc(c.guid) + '">' + esc(otherName(c)) + '</span></div><div class="cf-why">' + esc(textOf(c)) + '</div></div>';
    const known = m.known.conflicts.length ? '<div class="sec"><div class="sec-t">' + esc(word('knownConflicts')) + '</div><div class="cf-why">' + esc(word('knownConflictsWhy')) + '</div>' + m.known.conflicts.map(knownBlock).join('') + '</div>' : '';

    if (!m.patchOverlaps.length) return '<div class="cf">' + known + '</div>';

    let html = '<div class="cf">' + known + '<div class="cf-intro">' + esc(word('conflictsIntro')) + '</div><div class="cf-sum">' + risks.map(pill).join('') + '</div>';

    for (const risk of risks) {
      const members = m.patchOverlaps.filter(o => o.risk === risk);

      if (members.length) html += '<div class="sec"><div class="sec-t">' + esc(word('risk')[risk]) + '</div><div class="cf-why">' + esc(word('riskWhy')[risk]) + '</div>' + members.map(modBlock).join('') + '</div>';
    }

    return html + '</div>';
  }

  // The Errors tab: what this mod logged this session, and any game method it went through that another mod patches too.
  function errorsBody(m) {
    return '<div class="cf"><div class="cf-intro">' + esc(word('errorsIntro')) + '</div>' + m.errors.map(errorBlock).join('') + '</div>';
  }

  function errorBlock(e) {
    const clash = c => '<div class="er-clash">' + esc(word('clashLine')(c.method)) + c.guids.map(g => '<span class="rel" data-mod="' + esc(g) + '">' + esc(otherName({ guid: g })) + '</span>').join('') + '</div>';

    return '<div class="er ' + e.level + '"><div class="er-head"><span class="er-lv">' + esc(word('level')[e.level]) + '</span><span class="cf-count">' + esc(word('seenTimes')(e.count)) + ' &middot; ' + esc(word('loggedBy')(e.source)) + '</span></div>' +
      '<div class="er-msg">' + esc(e.message) + '</div>' + e.clashes.map(clash).join('') +
      (e.stack.length ? '<div class="er-stack">' + esc(e.stack.join('\n')) + '</div>' : '') + '</div>';
  }

  function changelogBody(m) {
    const log = m.changelog;
    const note = '<div class="cl-note">' + esc(word('changelogNote')) + '</div>';
    const line = (text, title) => '<div class="cl"><div class="upd-line"' + (title ? ' title="' + esc(title) + '"' : '') + '>' + esc(text) + '</div></div>';

    switch (log.kind) {
      case 'ready': return log.text.trim() ? '<div class="cl">' + note + markdown(log.text) + '</div>' : line(word('changelogEmpty'), '');
      case 'failed': return line(word('changelogFailed'), log.reason);
      default: return line(word('changelogLoading'), '');
    }
  }

  // Enough markdown for a changelog: headings, bullets (nested by indent) and paragraphs. Everything is escaped first.
  function markdown(text) {
    const html = [];

    for (const raw of text.split('\n')) {
      const heading = /^(#{1,6})\s+(.*)$/.exec(raw);
      const bullet = /^(\s*)[-*+]\s+(.*)$/.exec(raw);

      if (heading) html.push('<div class="cl-h h' + Math.min(heading[1].length, 3) + '">' + inline(heading[2].replace(/\s+#+\s*$/, '')) + '</div>');
      else if (bullet) html.push('<div class="cl-li" style="margin-left:' + Math.min(Math.floor(bullet[1].length / 2), 4) * 16 + 'px">' + inline(bullet[2]) + '</div>');
      else if (raw.trim() && !/^\s*(-{3,}|\*{3,}|_{3,})\s*$/.test(raw)) html.push('<div class="cl-p">' + inline(raw.trim()) + '</div>');
    }

    return html.join('');
  }

  // Code spans, bold, and links shown as their text; the page opens no address from a changelog.
  function inline(text) {
    return esc(text)
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(/\*\*([^*]+)\*\*/g, '<b>$1</b>')
      .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1');
  }

  function otherName(o) {
    const other = modByGuid(o.guid);

    return other ? nameOf(other) : o.guid;
  }

  // Keys the mod listens for that are not settings. A clash with any other mod's key is marked like a hotkey setting's.
  function fixedHotkeys(m) {
    if (!m.hotkeys || !m.hotkeys.length) return '';

    const chip = h => {
      const others = keyUsers(h.keys, m, h);
      const title = others.length ? ' title="' + esc(word('alsoUsed')(others.join(', '))) + '"' : '';

      return '<span class="rel' + (others.length ? ' bad' : '') + '"' + title + '><span class="hk">' + esc(h.keys) + '</span>' + esc(state.lang === 'zh' ? h.zh : h.en) + '</span>';
    };

    return '<div class="rels"><div class="rel-h">' + esc(word('hotkeys')) + '</div><div class="rel-items">' + m.hotkeys.map(chip).join('') + '</div></div>';
  }

  function relationName(r) {
    const other = modByGuid(r.guid);

    return other ? nameOf(other) : r.name;
  }

  function relationNotice(m) {
    const problems = relationProblems(m);
    const lines = [];

    if (problems.needs.length) lines.push(esc(word('needsProblem')(problems.needs.map(relationName).join(', '))));

    if (problems.clashes.length) lines.push(esc(word('clashProblem')(problems.clashes.map(relationName).join(', '))));

    if (!lines.length) return '';

    const canTurnOn = problems.needs.some(r => r.status === 'off' && r.canTurnOn);
    const button = canTurnOn ? '<div class="acts"><div class="key-btn" data-act="needs">' + esc(word('turnOnNeeds')) + '</div></div>' : '';

    return '<div class="notice warn"><span class="ico">!</span><div>' + lines.join('<br>') + button + '</div></div>';
  }

  function releaseNotes(notes) {
    const line = (text, title) => '<div class="upd-line"' + (title ? ' title="' + esc(title) + '"' : '') + '>' + esc(text) + '</div>';

    switch (notes.kind) {
      case 'releases': return '<div class="upd-notes"><div class="upd-h">' + esc(word('whatsNew')) + '</div>' + releaseTexts(notes.releases) + '</div>';
      case 'empty': return line(word('notesEmpty'), '');
      case 'busy': return line(word('notesBusy'), '');
      case 'failed': return line(word('notesFailed'), notes.reason);
      default: return line(word('notesLoading'), '');
    }
  }

  // One release shows its text alone; several are each headed by their version, newest first.
  function releaseTexts(releases) {
    if (releases.length === 1) return esc(releases[0].text);

    return releases.map(r => '<div class="upd-tag">' + esc(tagText(r.tag)) + '</div>' + esc(r.text)).join('');
  }

  function about(m) {
    if (!m.about) return '';

    const text = state.lang === 'zh' && m.about.zh ? m.about.zh : m.about.en;

    return text ? '<div class="desc">' + esc(text) + '</div>' : '';
  }

  // The note from the last switch flip on this mod, or else what the next start will do.
  function modNotices(m) {
    const note = state.modNote && state.modNote.guid === m.guid && !state.modNote.onTrash ? state.modNote : null;

    if (note) return notice(note.warn ? 'warn' : 'ok', '', state.lang === 'zh' ? note.zh : note.en);

    if (!isPending(m)) return '';

    if (m.switch.next) return notice('', word('pendingOnTitle'), word('pendingOnBody'));

    const stops = m.switch.stops.length ? ' ' + word('alsoStops')(m.switch.stops.join(', ')) : '';

    return notice('', word('pendingOffTitle'), word('pendingOffBody') + stops);
  }

  function notice(kind, title, body) {
    return '<div class="notice' + (kind ? ' ' + kind : '') + '"><span class="ico">&#x23FB;</span><div>' + (title ? '<b>' + esc(title) + '</b> ' : '') + esc(body) + '</div></div>';
  }

  function links(m) {
    const buttons = [];

    for (const kind of m.links) {
      if (LINK_WORDS[kind]) buttons.push('<div class="key-btn" data-act="link" data-kind="' + esc(kind) + '">' + esc(word(LINK_WORDS[kind])) + ' &#x2197;</div>');
    }

    // Its page on Nexus Mods when Nexus knows it and the mod names no Nexus page itself; a newer version has its own notice.
    if (m.known.nexus && !m.known.nexus.newer && m.links.indexOf('nexus') < 0) buttons.push('<div class="key-btn" data-act="nexus">' + esc(word('linkNexus')) + ' &#x2197;</div>');

    const isUninstalled = m.switch.kind === 'uninstalled';

    if (m.loaded && !isUninstalled) buttons.push('<div class="key-btn" data-act="showCfg">' + esc(word('openCfg')) + '</div>');

    if (!isUninstalled && m.settings.some(s => isChanged(s) && !s.readOnly)) buttons.push('<div class="key-btn danger" data-act="resetMod">' + esc(word('resetAll')) + '</div>');

    if (m.switch.kind === 'allowed' && state.confirmUninstall !== m.guid) buttons.push('<div class="key-btn danger" data-act="uninstall">' + esc(word('uninstall')) + '</div>');

    return buttons.length ? '<div class="links">' + buttons.join('') + '</div>' : '';
  }

  // Mods known not to work with this one, and mods that patch the same game code.
  function conflictCount(m) {
    return m.known.conflicts.length + m.patchOverlaps.length;
  }

  // A tab per settings section once there are two, then a Changelog tab when the mod names one the page can show,
  // a Conflicts tab when other mods are known not to work with it or patch the same game code, and an Errors tab when
  // the mod logged any this session.
  function tabs(m, names) {
    const hasConflicts = conflictCount(m) > 0;
    const hasChangelog = m.changelog.kind !== 'none';

    const own = ownTabs(m);

    if (names.length < 2 && !hasConflicts && !hasChangelog && !hasErrors(m) && !own.length) return '';

    const tab = (name, label, members) => '<div class="tab' + (!state.side && state.tab === name ? ' on' : '') + '" data-tab="' + esc(name) + '">' + esc(label) + ' <i>' + members.length + '</i>' + (members.some(isChanged) ? '<span class="cd"></span>' : '') + '</div>';
    const sideTab = (side, inner) => '<div class="tab tab-x' + (state.side === side ? ' on' : '') + '" data-side="' + esc(side) + '">' + inner + '</div>';
    const shown = shownSettings(m);
    const sectionTabs = names.length < 2 ? '' : names.map(n => tab(n, n, shown.filter(s => s.section === n))).join('');
    const worst = m.known.conflicts.length ? 'likely' : ['likely', 'may', 'harmless'].find(risk => m.patchOverlaps.some(o => o.risk === risk));
    const ownTabsHtml = own.map(t => sideTab(OWN_SIDE + t.id, esc(ownTitle(t)))).join('');
    const changelogTab = hasChangelog ? sideTab('changelog', esc(word('tabChangelog'))) : '';
    const conflictsTab = hasConflicts ? sideTab('conflicts', esc(word('tabConflicts')) + ' <i>' + conflictCount(m) + '</i><span class="rd ' + worst + '"></span>') : '';
    const errorsTab = hasErrors(m) ? sideTab('errors', esc(word('tabErrors')) + ' <i>' + m.errors.length + '</i><span class="rd ' + (m.errors.some(e => e.level === 'error') ? 'likely' : 'may') + '"></span>') : '';

    return '<div class="tabs">' + tab('', word(names.length < 2 ? 'tabSettings' : 'tabAll'), shown) + sectionTabs + ownTabsHtml + changelogTab + conflictsTab + errorsTab + '</div>';
  }

  function paneBody(m, names) {
    if (isOwnSide(state.side)) return '<div class="own-tab" data-own="' + esc(m.guid + '\n' + state.side.slice(OWN_SIDE.length)) + '"></div>';

    if (state.side === 'conflicts') return conflictsBody(m);

    if (state.side === 'changelog') return changelogBody(m);

    if (state.side === 'errors') return errorsBody(m);

    if (m.switch.kind === 'uninstalled') return notice('', word('uninstalledTitle'), word('uninstalledBody'));

    if (m.state === 'off') return notice('', word('offTitle'), word('offBody'));

    if (!m.loaded) return notice('', word('notLoadedTitle'), word('notLoadedBody'));

    if (!m.settings.length) return '<div class="empty"><div class="big">' + esc(word('noSettingsTitle')) + '</div>' + esc(word('noSettingsBody')) + '</div>';

    const shown = state.tab ? [state.tab] : names;

    return '<div class="sets">' + shown.map(n => section(m, n)).join('') + '</div>';
  }

  // Advanced settings sit folded under their section's ordinary ones; a changed one marks the fold.
  function section(m, name) {
    const members = shownSettings(m).filter(s => s.section === name);
    const advanced = members.filter(s => s.advanced);
    const foldId = m.guid + '\n' + name;
    const isOpen = !!state.openAdvanced[foldId];
    const arrow = isOpen ? '&#x25BE;' : '&#x25B8;';
    let html = '<div class="sec"><div class="sec-t">' + esc(name) + '</div>' + members.filter(s => !s.advanced).map(s => row(m, s, null)).join('');

    if (advanced.length) {
      html += '<div class="adv-t" data-fold="' + esc(foldId) + '">' + arrow + ' ' + esc(word('advanced')) + ' &middot; ' + advanced.length + (advanced.some(isChanged) ? '<span class="cd"></span>' : '') + '</div>';

      if (isOpen) html += advanced.map(s => row(m, s, null)).join('');
    }

    return html + '</div>';
  }

  function renderSearch() {
    const q = state.query.toLowerCase();
    const pattern = new RegExp('(' + esc(state.query).replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + ')', 'ig');
    const highlight = text => esc(text).replace(pattern, '<mark>$1</mark>');
    let count = 0;
    let html = '';

    for (const m of visibleMods()) {
      const hits = shownSettings(m).filter(s => matchesQuery(m, s, q));

      if (!hits.length) continue;

      count += hits.length;
      html += '<div class="sr-mod" data-mod="' + esc(m.guid) + '">' + badge(m, 'mono') + esc(nameOf(m)) + ' <span>' + esc(word('match')(hits.length)) + '</span></div>' + hits.map(s => row(m, s, highlight)).join('');
    }

    const empty = '<div class="empty"><div class="big">' + esc(word('nothing')) + '</div>' + esc(word('tryAnother')) + '</div>';

    morph(pane, '<div class="sr-head"><div class="big">' + count + '</div><div class="q">' + esc(word('settingsMatch')(state.query)) + '</div></div><div class="sets">' + (html || empty) + '</div>');
  }

  // ===== One setting =====

  function row(m, s, highlight) {
    const index = state.rows.push({ mod: m, setting: s }) - 1;
    const id = settingId(m, s);
    const hintText = state.lang === 'zh' ? s.zh : s.en;
    const show = text => (highlight ? highlight(text) : esc(text));
    const note = state.notes[id];
    const lines = [];

    if (hintText) lines.push('<div class="hint">' + show(hintText) + '</div>');

    if (s.kind === 'hotkey') {
      const others = conflicts(m, s);

      if (others.length) lines.push('<div class="hint warn">' + esc(word('alsoUsed')(others.join(', '))) + '</div>');
    }

    if (note) lines.push('<div class="hint ' + (note.warn ? 'warn' : 'note') + '">' + esc(state.lang === 'zh' ? note.zh : note.en) + '</div>');

    if (s.world && !s.readOnly) lines.push(worldLine(s));

    const readOnly = s.readOnly ? '<span class="ro">' + esc(word('readOnly')) + '</span>' : '';
    const restartMark = restartBadge(s);
    const canReset = !s.readOnly;

    // An item list takes the row's whole width for its cards, under the label, with the restart mark beside the label.
    if (isListField(s)) {
      return '<div class="row wide' + (isChanged(s) ? ' chg' : '') + (id === state.pending ? ' flash' : '') + '" data-row="' + index + '">' +
        '<div><div class="lab">' + show(s.label) + readOnly + restartMark + '</div>' + lines.join('') + '</div>' +
        '<div class="reset' + (canReset ? ' can' : '') + '" data-act="reset" title="' + esc(word('resetTo')(shown(s, s.default))) + '">&#x21BA;</div>' +
        '<div class="mf-host' + (s.readOnly ? ' locked' : '') + '">' + (isEntries(s) ? entryField(m, s) : itemField(m, s)) + '</div></div>';
    }

    return '<div class="row' + (isChanged(s) ? ' chg' : '') + (id === state.pending ? ' flash' : '') + '" data-row="' + index + '">' +
      '<div><div class="lab">' + show(s.label) + readOnly + '</div>' + lines.join('') + '</div>' +
      '<div>' + restartMark + '</div>' +
      '<div class="ctl' + (s.readOnly ? ' locked' : '') + '">' + control(m, s, index) + '</div>' +
      '<div class="reset' + (canReset ? ' can' : '') + '" data-act="reset" title="' + esc(word('resetTo')(shown(s, s.default))) + '">&#x21BA;</div></div>';
  }

  // A world rule while a world plays: which value is in play, and the link that moves it between this world and every world.
  function worldLine(s) {
    const text = s.world.set ? word('worldSet')(shown(s, s.world.global)) : word('worldGlobal');
    const link = '<span class="wld-a" data-act="' + (s.world.set ? 'worldGlobal' : 'worldKeep') + '">' + esc(word(s.world.set ? 'useGlobal' : 'keepWorld')) + '</span>';

    return '<div class="hint wld' + (s.world.set ? ' on' : '') + '">' + esc(text) + ' ' + link + '</div>';
  }

  // A changed setting says whether the change needs a restart; a setting the mod applies at once says nothing.
  function restartBadge(s) {
    if (!isChangedThisSession(s) || s.apply === 'live') return '';

    return '<span class="badge">&#x21BB; ' + esc(word(s.apply === 'restart' ? 'needsRestart' : 'mayRestart')) + '</span>';
  }

  function shown(s, value) {
    if (s.kind === 'hotkey' && (value === '' || value === 'None')) return word('none');

    if (s.kind === 'choice') return choiceLabel(s, value);

    if (isEntries(s)) {
      if (!catalogOf(s)) return value;

      if (s.auto && value === '') return word('blankChooses');

      return entriesOf(s, value).map(e => entryName(s, e.id) + (hasValue(s) ? ' ' + valueLabel(s, e.count) : '')).join(', ') || word('none');
    }

    if (!state.items) return value;

    if (s.kind === 'item') return itemName(value);

    if (s.kind === 'category') return categoryName(value);

    if (isList(s) && s.auto && value === '') return word('blankChooses');

    if (s.kind === 'itemList') return entriesOf(s, value).map(e => itemName(e.id)).join(', ') || word('none');

    if (s.kind === 'itemCounts') return entriesOf(s, value).map(e => itemName(e.id) + ' x' + e.count).join(', ') || word('none');

    return value;
  }

  function control(m, s, index) {
    switch (s.kind) {
      case 'toggle':
        return '<label class="setting-toggle"><input type="checkbox" data-input="toggle"' + (s.current === 'true' ? ' checked' : '') + '><span class="track"></span><span class="knob"></span></label>';
      case 'number':
        return numberControl(s);
      case 'choice':
        return choiceControl(s);
      case 'hotkey': {
        const isListening = state.binding === settingId(m, s);
        const label = isListening ? word('pressKey') : shown(s, s.current);

        return '<div class="keycap' + (isListening ? ' listening' : '') + (conflicts(m, s).length ? ' conflict' : '') + '" data-act="bind">' + esc(label) + '</div>';
      }
      case 'item':
        if (state.items) return itemPick(s);
        break;
      case 'category':
        if (state.items) return categoryControl(s);
        break;
      default:
        break;
    }

    // Also the fallback for the item and category controls until the game's item table has arrived.
    return '<input class="txt" data-input="text" value="' + esc(s.current) + '" spellcheck="false">';
  }

  // ===== Item and category controls =====

  // A List setting picks from the entries its mod gives (state.entries); every other list picks from the game's items.
  const isEntries = s => s.kind === 'entryList';
  const isList = s => s.kind === 'itemList' || s.kind === 'itemCounts' || isEntries(s);
  const catalogOf = s => (isEntries(s) ? state.entries[s.catalog] || null : state.items);
  const isListField = s => isList(s) && !!catalogOf(s);
  const itemOf = id => state.items.byId[String(id)];
  const entryOf = (s, id) => (catalogOf(s) ? catalogOf(s).byId[String(id)] : null);
  // Each entry of the list carries a value: an amount, a mod's number, or one of the mod's choices.
  const hasValue = s => s.kind === 'itemCounts' || (isEntries(s) && (!!s.choices || s.min !== null));
  const hasChoice = s => isEntries(s) && !!s.choices;
  const hasStepper = s => hasValue(s) && !hasChoice(s);
  const isDecimal = s => isEntries(s) && s.min !== null;
  const forList = (s, itemWord, entryWord) => word(isEntries(s) ? entryWord : itemWord);
  const itemText = item => (state.lang === 'zh' ? item.zh : item.en);
  const textOf = text => (state.lang === 'zh' ? text.zh : text.en);
  const icon = (item, cls) => (item && item.icon ? '<img src="' + esc(item.icon) + '" alt=""' + (cls ? ' class="' + cls + '"' : '') + '>' : '<span class="noimg">' + esc(initial(item)) + '</span>');

  // An entry of the mod's with no picture shows the first letter of its name; an item without one, a question mark.
  function initial(item) {
    if (!item || !item.entry) return '?';

    return Array.from(itemText(item))[0] || '?';
  }

  function entryName(s, id) {
    if (!isEntries(s)) return itemName(id);

    const entry = entryOf(s, id);

    return entry ? itemText(entry) : String(id);
  }

  // How an entry's value reads: the mod's name for a choice, a multiplier for a number, an amount for an item.
  function valueLabel(s, value) {
    if (!hasChoice(s)) return '\u00d7' + value;

    const at = s.choices.findIndex(c => c.toLowerCase() === String(value).toLowerCase());

    return at >= 0 && state.lang === 'zh' && s.choicesZh && s.choicesZh[at] ? s.choicesZh[at] : String(value);
  }

  // The value a new entry starts with: one, brought into the mod's range, or the mod's first choice.
  const firstValue = s => (hasChoice(s) ? s.choices[0] : clampValue(s, 1));

  function itemName(id) {
    if (String(id) === '0') return word('none');

    const item = itemOf(id);

    return item ? itemText(item) : '#' + id;
  }

  function categoryName(id) {
    if (String(id) === '0') return word('none');

    const category = state.items.categories.find(c => String(c.id) === String(id));

    return category ? textOf(category) : '#' + id;
  }

  function itemIcon(id) {
    const item = itemOf(id);

    return item && item.icon ? '<img class="ico-sq" src="' + esc(item.icon) + '" alt="">' : '<span class="ico-sq"></span>';
  }

  // A list's entries, in order: "1203,7" or "1203:5,7:2". "none" is a list the mod would fill in when blank, set to hold nothing.
  function entriesOf(s, value) {
    const text = String(value).trim();

    if (!text || (s.auto && text.toLowerCase() === NO_ITEMS)) return [];

    return text.split(',').map(part => part.trim()).filter(Boolean).map(part => {
      const pair = part.split(':');

      return { id: pair[0].trim(), count: hasValue(s) ? (pair[1] || String(firstValue(s))).trim() : '1' };
    });
  }

  // What is written back. An emptied list the mod fills in when blank is saved as "none", so it stays empty.
  function valueText(s, entries) {
    if (!entries.length) return s.auto ? NO_ITEMS : '';

    return entries.map(e => (hasValue(s) ? e.id + ':' + e.count : e.id)).join(',');
  }

  const lineText = (s, entries) => entries.map(e => (hasValue(s) ? e.id + ':' + e.count : e.id)).join(', ');
  const isAuto = (m, s) => s.auto && valueNow(m, s) === '';
  const valueNow = (m, s) => (state.drafts[settingId(m, s)] !== undefined ? state.drafts[settingId(m, s)] : s.current);

  // The list as it shows now: a count still being stepped, else the value; a blank the mod fills in shows the mod's list.
  function listOf(m, s) {
    if (isAuto(m, s)) return s.blank ? entriesOf(s, s.blank) : [];

    return entriesOf(s, valueNow(m, s));
  }

  const clampCount = (s, n) => Math.max(1, Math.min(s.maxCount || 9999, Math.round(n) || 1));
  // A mod's number keeps two decimals inside its range; an amount is a whole number from 1.
  const clampValue = (s, n) => (isDecimal(s) ? Math.max(s.min, Math.min(s.max, Math.round((isNaN(n) ? 1 : n) * 100) / 100)) : clampCount(s, n));
  const stepOf = s => (isDecimal(s) ? 0.1 : 1);
  const worth = entries => entries.reduce((sum, e) => sum + (itemOf(e.id) ? itemOf(e.id).v : 0) * Number(e.count), 0);
  const settingOf = (m, ref) => (ref ? m.settings.find(o => o.section === ref.section && o.key === ref.key) : null);
  const givesBackFor = (m, s) => m.settings.find(o => o.returnsOf && o.returnsOf.section === s.section && o.returnsOf.key === s.key);

  // A single item: its icon and name, opening the big picker.
  function itemPick(s) {
    return '<div class="ipk"><div class="ipk-pick" data-act="pick">' + itemIcon(s.current) + '<span class="ipk-name">' + esc(itemName(s.current)) + '</span><span class="dd-arrow"></span></div></div>';
  }

  // An item list as cards: each item with its picture, name and amount, an add card, the mod's verdict and the list as text.
  function itemField(m, s) {
    const id = settingId(m, s);
    const auto = isAuto(m, s);
    const list = listOf(m, s);
    const max = s.maxKinds;
    const recipe = settingOf(m, s.returnsOf);
    let slots = '';

    list.forEach((e, i) => {
      slots += auto ? autoSlot(s, e, recipe ? listOf(m, recipe) : null) : slot(m, s, e, i);
    });

    if (auto && !list.length) slots += '<div class="slot auto none"><div class="nm">' + esc(word('nothing')) + '</div></div>';

    if (auto) slots += '<div class="slot add hand" data-act="byHand"><div class="plus">&#x270E;</div><div class="al">' + esc(word('setByHand')) + '</div></div>';
    else if (!max || list.length < max) slots += '<div class="slot add' + (hasValue(s) ? '' : ' plain') + '" data-act="pick"><div class="plus">+</div><div class="al">' + esc(forList(s, 'addItem', 'addEntry')) + '</div></div>';

    const pips = max && max <= 12 ? '<span class="mf-pips">' + '<i class="on"></i>'.repeat(Math.min(list.length, max)) + '<i></i>'.repeat(Math.max(0, max - list.length)) + '</span>' : '';
    const cap = max && !auto ? '<span class="mf-cap">' + pips + '<b>' + list.length + '</b> / ' + max + ' ' + esc(word('kinds')) + '</span>' : '';
    const text = state.editing === id
      ? '<input class="mf-in" data-input="text" value="' + esc(auto ? '' : lineText(s, list) || (s.auto ? NO_ITEMS : '')) + '" spellcheck="false" autocomplete="off">'
      : '<code data-act="editText" title="' + esc(word('editText')) + '">' + esc(auto ? word('blankText') : lineText(s, list) || word('noneText')) + '</code>';
    const warn = s.problem ? '<div class="mf-warn"><span>&#x26A0;</span><div>' + esc(textOf(s.problem)) + '</div></div>' : '';
    const card = s.makes ? recipeCard(m, s, list) : '';

    return '<div class="mf' + (card ? '' : ' solo') + '">' + warn +
      '<div><div class="mf-slots">' + slots + '</div><div class="mf-under">' + statusOf(m, s, list, recipe) + cap + '<div class="mf-text">' + text + '</div></div></div>' + card + '</div>';
  }

  function slot(m, s, e, i) {
    const item = entryOf(s, e.id);
    const counts = hasStepper(s);
    const title = (item ? item.en + ' / ' + item.zh + '  ' : '') + '#' + e.id + (item && item.note ? '\n' + textOf(item.note) : '');
    const pop = state.flash === settingId(m, s) + '\n' + e.id ? ' pop' : '';
    const dragged = state.drag && state.drag.id === settingId(m, s) && state.drag.from === i ? ' drag' : '';

    return '<div class="slot' + (hasValue(s) ? '' : ' plain') + dragged + '" data-slot="' + i + '" title="' + esc(title) + '">' +
      '<span class="sw" data-act="swap" data-index="' + i + '" title="' + esc(forList(s, 'swapItem', 'swapEntry')) + '">&#x21C4;</span>' +
      '<span class="sx" data-act="dropItem" data-index="' + i + '" title="' + esc(word('remove')) + '">&#x2715;</span>' +
      '<div class="tile" data-act="swap" data-index="' + i + '">' + icon(item) + (counts ? '<span class="qty' + pop + '">&times;' + esc(e.count) + '</span>' : '') + '</div>' +
      '<div class="nm">' + esc(item ? itemText(item) : '#' + e.id) + '</div>' +
      (counts ? '<div class="stp"><span data-act="dec" data-index="' + i + '">&minus;</span><input data-input="count" data-index="' + i + '" value="' + esc(e.count) + '" inputmode="' + (isDecimal(s) ? 'decimal' : 'numeric') + '"><span data-act="inc" data-index="' + i + '">+</span></div>' : '') +
      '</div>';
  }

  // One item of the list the mod chose for a blank value, with how many the recipe it belongs to takes.
  function autoSlot(s, e, recipe) {
    const item = entryOf(s, e.id);
    const from = recipe ? recipe.find(r => r.id === e.id) : null;

    return '<div class="slot auto" title="' + esc((item ? item.en + ' / ' + item.zh + '  ' : '') + '#' + e.id) + '"><div class="tile">' + icon(item) +
      (hasStepper(s) ? '<span class="qty">&times;' + esc(e.count) + '</span>' : '') + '</div>' +
      '<div class="nm">' + esc(item ? itemText(item) : '#' + e.id) + '</div>' + (from ? '<div class="from">' + esc(word('ofCount')(from.count)) + '</div>' : '') + '</div>';
  }

  // A mod's own list as rows, one per entry: its picture, name and what it does, its choice or number, and buttons to
  // move it or take it out. A blank the mod fills in shows only the mod's line about it, never the whole list it stands for.
  function entryField(m, s) {
    const id = settingId(m, s);
    const auto = isAuto(m, s);
    const list = auto ? [] : entriesOf(s, valueNow(m, s));
    const full = !!s.maxKinds && list.length >= s.maxKinds;
    const rows = list.map((e, i) => entryRow(m, s, e, i, list.length)).join('');
    const empty = '<div class="el-empty">' + esc(s.blankNote ? textOf(s.blankNote) : word('nothingChosen')) + '</div>';
    const waiting = !catalogOf(s).list.length ? '<div class="mf-status note"><i>i</i>' + esc(word('entriesWaiting')) + '</div>' : '';
    const problem = s.problem ? '<div class="mf-status bad"><i>!</i>' + esc(word('modCannotUse')) + '</div>' : '';
    const warn = s.problem ? '<div class="mf-warn"><span>&#x26A0;</span><div>' + esc(textOf(s.problem)) + '</div></div>' : '';
    const count = list.length ? '<span class="el-count">' + esc(word('chosen')(list.length)) + (s.maxKinds ? ' / ' + s.maxKinds : '') + '</span>' : '';
    const text = state.editing === id
      ? '<input class="mf-in" data-input="text" value="' + esc(auto ? '' : lineText(s, list) || (s.auto ? NO_ITEMS : '')) + '" spellcheck="false" autocomplete="off">'
      : '<code data-act="editText" title="' + esc(word('editText')) + '">' + esc(auto ? word('blankText') : lineText(s, list) || word('noneText')) + '</code>';

    return '<div class="el">' + warn + '<div class="el-rows">' + (rows || empty) + '</div>' +
      '<div class="el-bar"><div class="el-add' + (full ? ' off' : '') + '" data-act="pick">+ ' + esc(word('addEntry')) + '</div>' + count + problem + waiting + '<div class="mf-text">' + text + '</div></div></div>';
  }

  function entryRow(m, s, e, i, total) {
    const entry = entryOf(s, e.id);
    const name = entry ? itemText(entry) : '#' + e.id;
    const pop = state.flash === settingId(m, s) + '\n' + e.id ? ' pop' : '';
    const move = total > 1
      ? '<div class="el-mv"><span data-act="moveUp" data-index="' + i + '"' + (i === 0 ? ' class="off"' : '') + ' title="' + esc(word('moveUp')) + '">&#x25B2;</span>' +
        '<span data-act="moveDown" data-index="' + i + '"' + (i === total - 1 ? ' class="off"' : '') + ' title="' + esc(word('moveDown')) + '">&#x25BC;</span></div>'
      : '';
    const value = hasChoice(s)
      ? '<div class="el-seg">' + s.choices.map(c => '<span data-act="setChoice" data-index="' + i + '" data-value="' + esc(c) + '"' + (c.toLowerCase() === String(e.count).toLowerCase() ? ' class="on"' : '') + '>' + esc(valueLabel(s, c)) + '</span>').join('') + '</div>'
      : hasStepper(s)
        ? '<div class="stp"><span data-act="dec" data-index="' + i + '">&minus;</span><input data-input="count" data-index="' + i + '" value="' + esc(e.count) + '" inputmode="decimal"><span data-act="inc" data-index="' + i + '">+</span></div>'
        : '';

    return '<div class="el-row' + pop + (entry ? '' : ' lost') + (entry && entry.have ? ' has' : '') + '" data-slot="' + i + '">' + move + entryText(entry, name, entry ? '' : word('notListed')) + haveTag(entry) + value +
      '<span class="el-x" data-act="dropItem" data-index="' + i + '" title="' + esc(word('remove')) + '">&#x2715;</span></div>';
  }

  // What the player has of an entry, as the mod words it ("You have 3"); nothing when the mod says nothing.
  const haveTag = entry => (entry && entry.have ? '<span class="el-have">' + esc(textOf(entry.have)) + '</span>' : '');

  // An entry's picture, its name with the other language's beside it, and what it does under them.
  function entryText(entry, name, fallback) {
    const alt = entry ? (state.lang === 'zh' ? entry.en : entry.zh) : '';
    const note = entry && entry.note ? textOf(entry.note) : fallback;

    return '<div class="el-ic">' + icon(entry) + '</div><div class="el-tx"><div class="el-nm">' + esc(name) + (alt && alt !== name ? '<span>' + esc(alt) + '</span>' : '') + '</div>' +
      (note ? '<div class="el-ds">' + esc(note) + '</div>' : '') + '</div>';
  }

  // The mod's verdict first; then whether a give-back list is worth more than its recipe; then how the list was set.
  function statusOf(m, s, list, recipe) {
    const line = (cls, mark, text) => '<div class="mf-status' + cls + '"><i>' + mark + '</i>' + esc(text) + '</div>';

    if (s.problem) return line(' bad', '!', word('modCannotUse'));

    if (isAuto(m, s)) return line(' note', 'i', s.blankNote ? textOf(s.blankNote) : word('autoNote'));

    if (recipe && worth(list) > worth(listOf(m, recipe))) return line(' bad', '!', word('profit')(worth(list), worth(listOf(m, recipe))));

    if (s.auto) return line('', '&#x2713;', word('byHandNote'));

    return s.checked ? line('', '&#x2713;', word('modCanUse')) : '';
  }

  // How the recipe reads at the workbench, what it weighs and is worth, and what taking the thing down gives back.
  function recipeCard(m, s, list) {
    const made = itemOf(s.makes);
    const back = givesBackFor(m, s);
    const grams = list.reduce((sum, e) => sum + (itemOf(e.id) ? itemOf(e.id).g : 0) * Number(e.count), 0);
    const pieces = list.reduce((sum, e) => sum + Number(e.count), 0);
    const lines = list.map(e => '<div class="ln">' + icon(itemOf(e.id)) + '<span class="n">' + esc(itemName(e.id)) + '</span><span class="c">&times;' + esc(e.count) + '</span></div>').join('');
    let backPart = '';

    if (back) {
      const chips = listOf(m, back).map(e => '<span class="ch">' + icon(itemOf(e.id)) + '&times;' + esc(e.count) + '</span>').join('');

      backPart = '<div class="back"><div class="bt">' + esc(word('givesBack')) + ' &middot; ' + esc(word(isAuto(m, back) ? 'autoMark' : 'byHandMark')) + '</div><div class="chs">' + (chips || '<span class="ch">-</span>') + '</div></div>';
    }

    return '<div class="mf-card"><div class="hd2">' + (made && made.icon ? '<img src="' + esc(made.icon) + '" alt="">' : '') +
      '<div><div class="s">' + esc(word('atBench')) + '</div><div class="t">' + esc(itemName(s.makes)) + '</div></div></div>' +
      (lines || '<div class="ln"><span class="n dim">' + esc(word('noMaterials')) + '</span></div>') +
      '<div class="stats"><div><b>' + pieces + '</b><span>' + esc(word('pieces')) + '</span></div>' +
      '<div><b>' + (grams / 1000).toLocaleString('en', { maximumFractionDigits: 1 }) + '</b><span>' + esc(word('kgCarry')) + '</span></div>' +
      '<div><b>' + worth(list).toLocaleString('en') + '</b><span>' + esc(word('tradeValue')) + '</span></div></div>' + backPart + '</div>';
  }

  function categoryControl(s) {
    const options = [{ id: 0, en: word('none'), zh: word('none') }].concat(state.items.categories);
    const items = options.map(c => '<div class="dd-item' + (String(c.id) === s.current ? ' selected' : '') + '" data-value="' + c.id + '">' + esc(textOf(c)) + '</div>').join('');

    return '<div class="dd"><div class="dd-trigger"><span>' + esc(categoryName(s.current)) + '</span><span class="dd-arrow"></span></div><div class="dd-list">' + items + '</div></div>';
  }

  // ===== Changing an item list =====

  function writeList(row, entries, flashId) {
    state.flash = flashId ? settingId(row.mod, row.setting) + '\n' + flashId : null;
    write(row, valueText(row.setting, entries));
  }

  function dropItem(row, index) {
    writeList(row, listOf(row.mod, row.setting).filter((e, i) => i !== index));
  }

  function setItemCount(row, index, count) {
    const list = listOf(row.mod, row.setting);

    if (!list[index]) return;

    list[index].count = String(clampValue(row.setting, parseFloat(count)));
    writeList(row, list, list[index].id);
  }

  let draftTimer = 0;
  let draftId = null;
  let draftRow = null;

  // Steps and wheel turns show at once and are written once they stop, so a run of clicks is one write, not twenty.
  function bump(row, index, delta) {
    const s = row.setting;
    const id = settingId(row.mod, s);
    const list = listOf(row.mod, s);

    if (!list[index] || !hasStepper(s)) return;

    if (draftId !== id) flushDraft();

    list[index].count = String(clampValue(s, Number(list[index].count) + delta * stepOf(s)));
    state.drafts[id] = valueText(s, list);
    state.flash = id + '\n' + list[index].id;
    draftId = id;
    draftRow = row;
    render();
    clearTimeout(draftTimer);
    draftTimer = setTimeout(flushDraft, DRAFT_MS);
  }

  // A count still waiting to be written goes out now, before anything else is written or the page starts over.
  function flushDraft() {
    if (draftId === null) return;

    const id = draftId;
    const row = draftRow;

    clearTimeout(draftTimer);
    draftId = null;
    draftRow = null;

    if (state.drafts[id] !== undefined) write(row, state.drafts[id]);
  }

  function setChoice(row, index, value) {
    const list = listOf(row.mod, row.setting);

    if (!list[index] || list[index].count === value) return;

    list[index].count = value;
    writeList(row, list, list[index].id);
  }

  function moveItem(row, from, to) {
    const list = listOf(row.mod, row.setting);

    if (!list[from] || to < 0 || to >= list.length) return;
    const moved = list.splice(from, 1)[0];

    list.splice(to, 0, moved);
    writeList(row, list);
  }

  function editText(row) {
    state.editing = settingId(row.mod, row.setting);
    render();

    const input = pane.querySelector('.mf-in');

    if (input) {
      input.focus();
      input.setSelectionRange(input.value.length, input.value.length);
    }
  }

  // ===== The item picker =====

  const pickerRow = () => (state.picker ? state.rows.find(r => settingId(r.mod, r.setting) === state.picker.id) : null);

  // The slot being swapped, or null to add; a single item has no slots.
  function openPicker(row, slotIndex) {
    const s = row.setting;
    const list = isList(s) ? listOf(row.mod, s) : [];
    const swapping = slotIndex !== null && list[slotIndex] ? list[slotIndex] : null;
    const pick = swapping ? swapping.id : s.kind === 'item' && s.current !== '0' ? s.current : null;

    state.editing = null;
    state.picker = { id: settingId(row.mod, s), slot: swapping ? slotIndex : null, q: '', cat: isEntries(s) ? 'all' : 'common', pick: pick, count: swapping ? pickedValue(s, swapping.count) : firstValue(s), shown: PICKER_PAGE, top: true };
    drawPicker(true);
  }

  function closePicker() {
    if (!state.picker) return;

    state.picker = null;
    drawPicker(false);
    syncTyping();
  }

  // A choice is kept as the mod spells it; a number or an amount as a number.
  const pickedValue = (s, value) => (hasChoice(s) ? String(value) : clampValue(s, Number(value)));

  function matches(item, q) {
    const low = q.toLowerCase();

    return item.en.toLowerCase().indexOf(low) >= 0 || item.zh.indexOf(q) >= 0 || String(item.id).toLowerCase().indexOf(low) === 0;
  }

  // The items on show: the search over every item, most used by recipes first, else the chosen shelf.
  function shelf(s) {
    const p = state.picker;
    const catalog = catalogOf(s);
    const all = catalog.list;
    const q = p.q.trim();

    // The mod's entries keep the mod's order; items put the ones most recipes take first.
    if (q) return isEntries(s) ? all.filter(item => matches(item, q)) : all.filter(item => matches(item, q)).sort((a, b) => b.uses - a.uses || a.id - b.id);

    if (p.cat === 'common' && !isEntries(s)) return catalog.common;

    if (p.cat === HAVE) return catalog.haveCount ? all.filter(item => item.have) : all;

    if (p.cat === 'all') return all;

    return all.filter(item => String(item.cat) === String(p.cat));
  }

  // Draws the picker into its own layer above the window. The search box is made once, so typing keeps the keyboard.
  function drawPicker(isNew) {
    const host = $('.pk-host');
    const p = state.picker;
    const row = pickerRow();

    if (!p || !row || !catalogOf(row.setting)) {
      // A picker whose setting is no longer on the page is closed, or the next click would only close it.
      if (p && !row) state.picker = null;

      host.innerHTML = '';

      return;
    }

    if (isNew || !host.firstChild) {
      host.innerHTML = '<div class="pk-veil"><div class="pk' + (isEntries(row.setting) ? ' rows' : '') + '"><div class="pk-hd"><div class="ti"></div>' +
        '<div class="pk-q"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/></svg><input class="pk-find" spellcheck="false" autocomplete="off"></div>' +
        '<div class="used"></div><div class="close-btn" data-act="pkClose">&#x2715;</div></div>' +
        '<div class="pk-bd"><div class="pk-cats"></div><div class="pk-grid"></div><div class="pk-info"></div></div></div></div>';
    }

    fillPicker(row);

    const find = host.querySelector('.pk-find');

    if (isNew && find) {
      find.placeholder = searchText(row.setting);
      find.focus();
    }
  }

  const searchText = s => word(isEntries(s) ? 'searchEntries' : 'searchItems')(catalogOf(s).list.length.toLocaleString('en'));

  function fillPicker(row) {
    const host = $('.pk-host');
    const p = state.picker;
    const s = row.setting;
    const list = isList(s) ? listOf(row.mod, s) : [];
    const grid = host.querySelector('.pk-grid');
    const top = p.top ? 0 : grid.scrollTop;
    const swapping = p.slot !== null ? list[p.slot] : null;
    const title = s.kind === 'item' ? word('titleOne') : swapping ? word('titleSwap')(entryName(s, swapping.id)) : forList(s, 'titleAdd', 'titleAddEntry');

    host.querySelector('.ti').innerHTML = esc(title) + ' &middot; <em>' + (s.section ? esc(s.section) + ' &middot; ' : '') + esc(s.label) + '</em>';
    host.querySelector('.used').innerHTML = isList(s) && s.maxKinds ? '<b>' + list.length + '</b> / ' + s.maxKinds + ' ' + esc(word('used')) : '';
    host.querySelector('.pk-find').placeholder = searchText(s);
    host.querySelector('.pk-cats').innerHTML = pickerShelves(s);
    grid.innerHTML = isEntries(s) ? pickerRows(s, list) : pickerGrid(s, list);
    grid.scrollTop = top;
    host.querySelector('.pk-info').innerHTML = isEntries(s) ? '' : pickerInfo(s, list);
    p.top = false;
  }

  function pickerShelves(s) {
    const p = state.picker;
    const catalog = catalogOf(s);
    const on = cat => (!p.q.trim() && String(p.cat) === String(cat) ? ' on' : '');
    const star = isEntries(s) ? '' : '<div class="pk-cat star' + on('common') + '" data-cat="common"><span>&#x2605; ' + esc(word('usedInRecipes')) + '</span><i>' + catalog.common.length + '</i></div>';
    const have = isEntries(s) && catalog.haveCount ? '<div class="pk-cat star' + on(HAVE) + '" data-cat="' + HAVE + '"><span>&#x2605; ' + esc(word('haveShelf')) + '</span><i>' + catalog.haveCount + '</i></div>' : '';
    let out = star + have + '<div class="pk-cat' + on('all') + '" data-cat="all"><span>' + esc(forList(s, 'everyItem', 'everything')) + '</span><i>' + catalog.list.length + '</i></div><div class="pk-sep"></div>';

    for (const c of catalog.shelves) out += '<div class="pk-cat' + on(c.id) + '" data-cat="' + esc(c.id) + '"><span>' + esc(textOf(c)) + '</span><i>' + catalog.perCat[c.id] + '</i></div>';

    return out;
  }

  function pickerGrid(s, list) {
    const p = state.picker;
    const found = shelf(s);
    const inList = {};
    const tile = item => {
      const got = inList[String(item.id)];

      return '<div class="it' + (String(item.id) === String(p.pick) ? ' hi' : '') + (got ? ' taken' : '') + '" data-id="' + esc(item.id) + '">' + (got && hasValue(s) ? '<span class="got">' + esc(hasChoice(s) ? valueLabel(s, got) : '\u00d7' + got) + '</span>' : '') +
        (item.icon ? '<img loading="lazy" src="' + esc(item.icon) + '" alt="">' : '<span class="noimg">' + esc(initial(item)) + '</span>') + '<div class="in">' + esc(itemText(item)) + '</div><div class="id">#' + esc(item.id) + '</div></div>';
    };

    for (const e of list) inList[e.id] = e.count;

    let out = s.kind === 'item' && !p.q.trim() ? '<div class="it' + (String(p.pick) === '0' ? ' hi' : '') + '" data-id="0"><span class="noimg">-</span><div class="in">' + esc(word('none')) + '</div><div class="id">#0</div></div>' : '';

    out += found.slice(0, p.shown).map(tile).join('');

    if (!found.length) return '<div class="pk-none">' + esc(word('noMatch')(p.q.trim())) + '</div>';

    if (found.length > p.shown) out += '<div class="pk-more" data-act="pkMore">' + esc(word('showing')(p.shown, found.length)) + '</div>';

    return out;
  }

  // A mod's entries as rows, each with an Add button; one already in the list says so, and a click takes it out again.
  function pickerRows(s, list) {
    const p = state.picker;
    const found = shelf(s);
    const inList = {};
    const full = !!s.maxKinds && list.length >= s.maxKinds;
    const showGroup = !!p.q.trim() || p.cat === 'all' || p.cat === HAVE;

    for (const e of list) inList[e.id] = e;

    if (!found.length) return '<div class="pk-none">' + esc(word('noEntryMatch')(p.q.trim())) + '</div>';

    const line = item => {
      const got = inList[String(item.id)];
      const group = showGroup && item.group && catalogOf(s).shelves.length ? '<span class="pk-grp">' + esc(textOf(item.group)) + '</span>' : '';
      const value = got && hasValue(s) ? '<span class="pk-val">' + esc(hasChoice(s) ? valueLabel(s, got.count) : '\u00d7' + got.count) + '</span>' : '';
      const button = got
        ? '<span class="pk-btn in" data-act="pkDrop" data-id="' + esc(item.id) + '"><b>&#x2713; ' + esc(word('added')) + '</b><i>' + esc(word('remove')) + '</i></span>'
        : '<span class="pk-btn' + (full ? ' off' : '') + '" data-act="pkAdd" data-id="' + esc(item.id) + '">+ ' + esc(word('addEntry')) + '</span>';

      return '<div class="pk-row' + (got ? ' in' : '') + (item.have ? ' has' : '') + '">' + entryText(item, itemText(item), '') + haveTag(item) + group + value + button + '</div>';
    };

    let out = found.slice(0, p.shown).map(line).join('');

    if (found.length > p.shown) out += '<div class="pk-more" data-act="pkMore">' + esc(word('showing')(p.shown, found.length)) + '</div>';

    if (full) out = '<div class="mf-status bad" style="padding:10px 14px"><i>!</i>' + esc(word('entriesFull')(s.maxKinds)) + '</div>' + out;

    return out;
  }

  // Adds an entry, or takes it out, and keeps the picker open. The list shows the change at once; the mod's answer replaces it.
  function toggleEntry(row, id, isAdd) {
    const s = row.setting;
    const list = listOf(row.mod, s);
    const at = list.findIndex(e => e.id === String(id));

    if (isAdd) {
      if (at >= 0 || (s.maxKinds && list.length >= s.maxKinds)) return;

      list.push({ id: String(id), count: String(firstValue(s)) });
    } else {
      if (at < 0) return;

      list.splice(at, 1);
    }

    const value = valueText(s, list);

    state.drafts[settingId(row.mod, s)] = value;
    state.flash = isAdd ? settingId(row.mod, s) + '\n' + id : null;
    write(row, value);
    render();
  }

  function pickerInfo(s, list) {
    const p = state.picker;
    const item = p.pick !== null && String(p.pick) !== '0' ? entryOf(s, p.pick) : null;

    if (p.pick === null || (String(p.pick) !== '0' && !item)) {
      return '<div class="empty-info">' + esc(word('clickToSee')) + '<br><span class="pk-keys">' + esc(word('dblClickAdds')) + '</span></div>';
    }

    const at = list.findIndex(e => e.id === String(p.pick));
    const isFull = isList(s) && p.slot === null && at < 0 && !!s.maxKinds && list.length >= s.maxKinds;
    const isIn = isList(s) && !hasValue(s) && p.slot === null && at >= 0;
    const verb = s.kind === 'item' ? 'pickOne' : p.slot !== null ? 'pickSwap' : isIn ? 'pickIn' : at >= 0 ? 'pickSet' : 'pickAdd';
    const stepper = hasStepper(s) ? '<div class="stp"><span data-act="pkDec">&minus;</span><input data-input="pkCount" value="' + esc(p.count) + '" inputmode="numeric"><span data-act="pkInc">+</span></div>' : '';
    const full = isFull ? '<div class="mf-status bad"><i>!</i>' + esc(word('listFull')(s.maxKinds)) + '</div>' : '';
    const go = '<div class="go' + (isFull || isIn ? ' off' : '') + '" data-act="pkGo">' + esc(word(verb)) + '</div>';

    if (!item) return '<div class="t">' + esc(word('none')) + '</div><div class="act">' + go + '</div>';

    return '<div class="big">' + icon(item) + '</div><div class="t">' + esc(itemText(item)) + '</div><div class="t2">' + esc(state.lang === 'zh' ? item.en : item.zh) + '</div>' +
      '<dl><dt>id</dt><dd>' + item.id + '</dd><dt>' + esc(word('category')) + '</dt><dd>' + esc(categoryName(item.cat)) + '</dd>' +
      '<dt>' + esc(word('weight')) + '</dt><dd>' + (item.g / 1000).toLocaleString('en', { maximumFractionDigits: 2 }) + ' kg</dd><dt>' + esc(word('value')) + '</dt><dd>' + item.v + '</dd>' +
      '<dt>' + esc(word('recipes')) + '</dt><dd>' + esc(word('usesIn')(item.uses)) + '</dd></dl>' +
      full + '<div class="act">' + stepper + go + '</div>';
  }

  // Clicking an item shows it, with the amount it already has; double-clicking puts it in.
  function pickTile(id, andCommit) {
    const p = state.picker;
    const row = pickerRow();

    if (!row) return;

    const list = isList(row.setting) ? listOf(row.mod, row.setting) : [];
    const got = list.find(e => e.id === String(id));

    p.pick = String(id);
    p.count = got ? pickedValue(row.setting, got.count) : p.slot !== null && list[p.slot] ? pickedValue(row.setting, list[p.slot].count) : firstValue(row.setting);

    if (andCommit) commit();
    else fillPicker(row);
  }

  // Puts the picked item in: in place of the one being swapped, onto the amount it already has, or at the end.
  function commit() {
    const p = state.picker;
    const row = pickerRow();

    if (!p || !row || p.pick === null) return;

    const s = row.setting;

    if (s.kind === 'item') {
      closePicker();
      write(row, String(p.pick));

      return;
    }

    const list = listOf(row.mod, s);
    const id = String(p.pick);
    const count = String(hasValue(s) ? pickedValue(s, p.count) : 1);
    const at = list.findIndex(e => e.id === id);

    // Swapping in an item the list already has elsewhere merges the two into that one.
    if (p.slot !== null && at >= 0 && at !== p.slot) {
      list[at].count = count;
      list.splice(p.slot, 1);
    } else if (p.slot !== null) {
      list[p.slot] = { id: id, count: count };
    } else if (at >= 0) {
      if (!hasValue(s)) return;

      list[at].count = count;
    } else if (!s.maxKinds || list.length < s.maxKinds) {
      list.push({ id: id, count: count });
    } else {
      return;
    }

    closePicker();
    writeList(row, list, id);
  }

  // A second click on an item puts it in. The first click redraws the grid, so the browser's own double-click never
  // reaches the item and the click count is read instead.
  function onPickerClick(target, shift, second) {
    const p = state.picker;
    const act = target.closest('[data-act]');
    const actName = act ? act.dataset.act : '';
    const row = pickerRow();

    if (!row || actName === 'pkClose' || target.classList.contains('pk-veil')) {
      closePicker();

      return;
    }

    const cat = target.closest('.pk-cat');
    const tile = target.closest('.it');

    if (cat) {
      p.cat = cat.dataset.cat;
      p.q = '';
      p.shown = PICKER_PAGE;
      p.top = true;
      $('.pk-host .pk-find').value = '';
      fillPicker(row);
    } else if (tile) {
      pickTile(tile.dataset.id, second);
    } else if (actName === 'pkDec' || actName === 'pkInc') {
      p.count = clampValue(row.setting, Number(p.count) + (actName === 'pkInc' ? 1 : -1) * (shift ? 10 : 1) * stepOf(row.setting));
      fillPicker(row);
    } else if (actName === 'pkAdd' || actName === 'pkDrop') {
      toggleEntry(row, act.dataset.id, actName === 'pkAdd');
    } else if (actName === 'pkGo') {
      commit();
    } else if (actName === 'pkMore') {
      p.shown += PICKER_PAGE;
      fillPicker(row);
    }
  }

  // The grid grows by a page as it is scrolled near its end.
  function onPickerScroll(e) {
    const grid = e.target;
    const p = state.picker;

    if (!p || !grid.classList || !grid.classList.contains('pk-grid') || !grid.querySelector('.pk-more')) return;

    if (grid.scrollTop + grid.clientHeight < grid.scrollHeight - 200) return;

    p.shown += PICKER_PAGE;

    const row = pickerRow();

    if (row) fillPicker(row);
  }


  function numberControl(s) {
    if (s.min === null || s.max === null) return '<input class="num wide" data-input="number" value="' + esc(s.current) + '">';

    const value = parseFloat(s.current);
    const step = s.whole ? 1 : Math.pow(10, Math.floor(Math.log10((s.max - s.min) / 100)));

    return '<div class="slider"><input type="range" data-input="range" min="' + s.min + '" max="' + s.max + '" step="' + step + '" value="' + (isFinite(value) ? value : s.min) + '" style="' + track(s, value) + '">' +
      '<input class="num" data-input="number" value="' + esc(s.current) + '"></div>';
  }

  function track(s, value) {
    const percent = s.max > s.min && isFinite(value) ? Math.max(0, Math.min(100, ((value - s.min) / (s.max - s.min)) * 100)) : 0;

    return 'background:linear-gradient(to right,#EBA308 0%,#EBA308 ' + percent + '%,#444 ' + percent + '%,#444 100%)';
  }

  function choiceIndex(s, option) {
    return s.options.findIndex(o => o.toLowerCase() === option.toLowerCase());
  }

  // The mod may name its choices in Chinese; the value written stays the option itself.
  function choiceLabel(s, option) {
    const i = choiceIndex(s, option);
    const zh = state.lang === 'zh' && Array.isArray(s.optionsZh) && i >= 0 ? s.optionsZh[i] : null;

    return typeof zh === 'string' && zh ? zh : option;
  }

  // The mod may draw a picture of each choice. It is the mod's own markup, so it goes in as it is, clipped to its box.
  function choicePicture(s, option) {
    const i = choiceIndex(s, option);
    const html = Array.isArray(s.optionsPreview) && i >= 0 ? s.optionsPreview[i] : null;

    return typeof html === 'string' && html ? '<div class="dd-pic">' + html + '</div>' : '';
  }

  function choiceControl(s) {
    const pictured = Array.isArray(s.optionsPreview) && s.optionsPreview.some(html => typeof html === 'string' && html);
    const body = option => pictured ? '<div class="dd-pv">' + choicePicture(s, option) + '<span class="dd-name">' + esc(choiceLabel(s, option)) + '</span></div>' : esc(choiceLabel(s, option));
    const isSelected = option => option.toLowerCase() === s.current.toLowerCase();
    const items = s.options.map((option, i) => '<div class="dd-item' + (isSelected(option) ? ' selected' : '') + '" data-option="' + i + '">' + body(option) + '</div>').join('');
    const shown = pictured ? body(s.current) : '<span>' + body(s.current) + '</span>';

    return '<div class="dd' + (pictured ? ' pv' : '') + '"><div class="dd-trigger">' + shown + '<span class="dd-arrow"></span></div><div class="dd-list">' + items + '</div></div>';
  }

  function conflicts(m, s) {
    if (!s.current || s.current === 'None') return [];

    return keyUsers(s.current, m, s);
  }

  // Every hotkey setting, in any mod, set to exactly these keys, and every running mod's fixed hotkey on them, but self.
  function keyUsers(keys, m, self) {
    const others = [];
    const name = (mod, label) => (mod === m ? label : nameOf(mod) + ' (' + label + ')');

    for (const mod of mods()) {
      for (const other of mod.settings) {
        if (other.kind === 'hotkey' && other.current === keys && other !== self) others.push(name(mod, other.label));
      }

      if (!mod.loaded || !mod.hotkeys) continue;

      for (const fixed of mod.hotkeys) {
        if (fixed.keys === keys && fixed !== self) others.push(name(mod, state.lang === 'zh' ? fixed.zh : fixed.en));
      }
    }

    return others;
  }

  function renderFooter() {
    const changed = mods().reduce((sum, m) => sum + m.settings.filter(isChangedThisSession).length, 0);
    const waiting = mods().some(m => m.settings.some(s => isChangedThisSession(s) && s.apply !== 'live'));
    const restarts = mods().filter(isPending).length;
    const restartText = restarts ? ' <em>' + esc(word('footerRestart')(restarts)) + '</em>' : '';
    const updates = mods().filter(isUpdatable).length;
    const updateText = updates ? ' <em>' + esc(word('footerUpdates')(updates)) + '</em>' : '';

    const changedText = changed ? word('footerSome')(changed) + (waiting ? ' <em>' + esc(word('footerRestartHint')) + '</em>' : '') : esc(word('footerNone'));
    const own = state.ownShown && state.ownFooter;

    $('.status-dot').classList.toggle('warn', own ? own.isWarn : waiting || restarts > 0);
    morph($('.ft-t'), own ? own.html : changedText + restartText + updateText);
    renderCopyButton();
  }

  // Everything that changes when the game starts again, each with what takes it back (null when nothing can yet).
  function pendingChanges() {
    const items = [];
    const add = (m, text, undo) => items.push({ mod: m, text: text, undo: undo });
    const isOnNow = m => m.state !== 'off';

    for (const m of mods()) {
      if (m.switch.kind === 'uninstalled') add(m, word('pendUninstalled'), () => send('restore', [m.switch.trash]));
      else if (isPending(m)) add(m, word(m.switch.next ? 'pendOn' : 'pendOff'), () => send('switch', [m.guid, isOnNow(m) ? '1' : '0']));
      else if (m.switch.kind === 'held' && m.switch.next !== isOnNow(m)) add(m, word(m.switch.next ? 'pendOn' : 'pendOff') + (state.lang === 'zh' ? '，' : ', ') + word('heldUntilRestart'), null);

      if (m.install.kind === 'installed') add(m, word('pendUpdate')(tagText(m.install.tag)), m.rollback.kind === 'available' ? () => send('rollback', [m.guid]) : null);

      if (m.rollback.kind === 'done' && !m.rollback.same) add(m, word('pendRollback')(m.rollback.version), () => send('rollForward', [m.guid]));

      for (const s of m.settings) {
        if (isChangedThisSession(s) && s.apply !== 'live' && !s.readOnly) add(m, word(s.apply === 'restart' ? 'pendSetting' : 'pendSettingMaybe')(s.label), () => send('set', [m.guid, s.section, s.key, s.atStart]));
      }
    }

    for (const b of browse()) {
      if (b.fresh && b.install.kind === 'installed') add({ name: b.name, nameZh: b.name }, word('pendNew'), () => send('browseUndo', [b.guid]));
    }

    return items;
  }

  function renderPend() {
    const items = pendingChanges();
    const host = $('.pend');

    state.pendItems = items;
    $('.win').classList.toggle('has-pend', items.length > 0);

    if (!items.length) {
      state.confirmQuit = false;
      if (host.innerHTML) host.innerHTML = '';

      return;
    }

    const button = (act, text, cls) => '<div class="key-btn' + (cls ? ' ' + cls : '') + '" data-act="' + act + '">' + esc(text) + '</div>';
    const head = '<div class="pend-hd"><span>&#x21BB;</span><b>' + esc(word('pendTitle')(items.length)) + '</b>' + button('pendToggle', word(state.pendOpen ? 'pendHide' : 'pendShow')) + (state.confirmQuit ? '' : button('quitGame', word('quitGame'))) + '</div>';
    const quit = state.confirmQuit ? '<div class="pend-quit"><span><b>' + esc(word('quitTitle')) + '</b> ' + esc(word('quitBody')) + '</span>' + button('quitYes', word('quitYes'), 'danger') + button('quitNo', word('cancel')) + '</div>' : '';
    const row = (item, i) => '<div class="pend-row"><span class="pn">' + esc(nameOf(item.mod)) + '</span><span class="pt">' + esc(item.text) + '</span>' + (item.undo ? '<div class="key-btn" data-act="undo" data-undo="' + i + '">' + esc(word('undo')) + '</div>' : '') + '</div>';

    morph(host, head + quit + (state.pendOpen ? '<div class="pend-list">' + items.map(row).join('') + '</div>' : ''));
  }

  function renderCopyButton() {
    const button = $('[data-act="copyList"]');

    button.textContent = word(state.copied === null ? 'copyList' : state.copied ? 'copied' : 'copyFailed');
    button.title = word('copyHint');
    button.classList.toggle('ok', state.copied === true);
    button.classList.toggle('bad', state.copied === false);
  }

  // ===== What the player does =====

  function rowOf(el) {
    const host = el.closest('[data-row]');

    return host ? state.rows[Number(host.dataset.row)] : null;
  }

  function write(row, value) {
    const id = settingId(row.mod, row.setting);

    // Another setting's waiting count goes first; this setting's own is already in the value, which replaces it.
    if (draftId !== null && draftId !== id) flushDraft();
    else if (draftId === id) {
      clearTimeout(draftTimer);
      draftId = null;
      draftRow = null;
    }

    state.pending = id;
    send('set', settingParts(row).concat([value]));
  }

  function cancelBinding() {
    if (!state.binding) return;

    state.binding = null;
    send('bindCancel');
    render();
  }

  function closeDropdowns(except) {
    root.querySelectorAll('.dd.open').forEach(dd => { if (dd !== except) dd.classList.remove('open'); });
  }

  function onClick(e) {
    const target = e.target;
    const row = rowOf(target);
    const act = target.closest('[data-act]');
    const actName = act ? act.dataset.act : '';

    if (state.binding && actName !== 'bind') cancelBinding();

    // The picker covers the window, so every click while it is open is the picker's.
    if (state.picker) {
      if (target.closest('.pk-host')) onPickerClick(target, e.shiftKey, e.detail >= 2);
      else closePicker();

      return;
    }

    if (target.closest('[data-own]')) return;

    if (actName === 'close') {
      hide();
      send('close');

      return;
    }

    if (actName === 'libraries') {
      state.showLibraries = !state.showLibraries;
      render();

      return;
    }

    const bwFilter = target.closest('[data-bwfilter]');

    if (bwFilter || target.closest('[data-bwsort]')) {
      if (bwFilter) state.browseFilter = bwFilter.dataset.bwfilter;
      else state.browseSort = target.closest('[data-bwsort]').dataset.bwsort;

      render();

      return;
    }

    const page = target.closest('[data-page]');

    if (page) {
      state.page = page.dataset.page;
      state.confirmEmpty = false;
      render();
      pane.scrollTop = 0;

      return;
    }

    const modLink = target.closest('[data-mod]');

    if (modLink) {
      select(modLink.dataset.mod);

      return;
    }

    const lang = target.closest('[data-lang]');

    if (lang) {
      setLanguage(lang.dataset.lang);

      return;
    }

    const chip = target.closest('[data-filter]');

    if (chip) {
      state.filter = chip.dataset.filter;
      render();

      return;
    }

    const tab = target.closest('[data-tab]');

    if (tab) {
      state.tab = tab.dataset.tab;
      state.side = '';
      render();

      return;
    }

    const side = target.closest('[data-side]');

    if (side) {
      state.side = side.dataset.side;
      render();

      return;
    }

    const fold = target.closest('[data-fold]');

    if (fold) {
      state.openAdvanced[fold.dataset.fold] = !state.openAdvanced[fold.dataset.fold];
      render();

      return;
    }

    if (actName === 'showCfg') send('showCfg', [state.selected]);
    else if (actName === 'link') send('link', [state.selected, act.dataset.kind]);
    else if (actName === 'update') send('update', [state.selected]);
    else if (actName === 'nexus') send('nexus', [state.selected]);
    else if (actName === 'install') send('install', [state.selected]);
    else if (actName === 'approve') send('approve', [state.selected]);
    else if (actName === 'installAll') send('installAll');
    else if (actName === 'rollback') send('rollback', [state.selected]);
    else if (actName === 'needs') send('needs', [state.selected]);
    else if (actName === 'copyList') send('copyList');
    else if (actName === 'bwInstall' && browseOf(act)) send('browseInstall', [browseOf(act).guid]);
    else if (actName === 'bwApprove' && browseOf(act)) send('browseApprove', [browseOf(act).guid]);
    else if (actName === 'bwUndo' && browseOf(act)) send('browseUndo', [browseOf(act).guid]);
    else if (actName === 'bwNexus' && browseOf(act)) send('nexusPage', [String(browseOf(act).nexus)]);
    else if (actName === 'bwNeedNexus') send('nexusPage', [act.dataset.nexus]);
    else if (actName === 'bwShame' && browseOf(act)) {
      const key = browseOf(act).key;

      state.bwShameOpen[key] = !state.bwShameOpen[key];
      render();
    } else if (actName === 'bwAsk' && browseOf(act)) {
      state.asked = { key: browseOf(act).key, ok: null };
      send('askAuthor', [String(browseOf(act).nexus)]);
    }
    else if (actName === 'pfSave') {
      const field = root.querySelector('.pf-name');

      send('profileSave', [field ? field.value : '']);

      if (field) field.value = '';
    } else if (actName === 'pfShow') send('profileShow', [act.dataset.pf]);
    else if (actName === 'pfCopy') {
      state.pfCopied = { name: act.dataset.pf, ok: null };
      send('profileCopy', [act.dataset.pf]);
    } else if (actName === 'pfDelete' || actName === 'pfDeleteNo') {
      state.pfDelete = actName === 'pfDelete' ? act.dataset.pf : null;
      render();
    } else if (actName === 'pfDeleteYes' && state.pfDelete) {
      send('profileDelete', [state.pfDelete]);
      state.pfDelete = null;
    } else if (actName === 'pfPaste') {
      const field = root.querySelector('.pf-paste');

      send('profilePaste', [field ? field.value : '']);
    } else if (actName === 'pfUse') send('profileUse');
    else if (actName === 'pfKeep') send('profileKeep');
    else if (actName === 'pfClose') send('profileClose');
    else if (actName === 'pfApprove') send('browseApprove', [act.dataset.guid]);
    else if (actName === 'resetMod') resetMod();
    else if (actName === 'uninstall' || actName === 'uninstallNo') {
      state.confirmUninstall = actName === 'uninstall' ? state.selected : null;
      state.removeSettings = false;
      render();
    } else if (actName === 'uninstallYes' && state.confirmUninstall) {
      const guid = state.confirmUninstall;

      state.confirmUninstall = null;
      state.noteFor = '';
      send('uninstall', [guid, state.removeSettings ? '1' : '0']);
    } else if (actName === 'prepOn' && state.selected) send('set', [state.selected, 'Uninstall', 'UninstallPrep', 'true']);
    else if (actName === 'restore') {
      state.noteFor = state.page === 'trash' ? 'trash' : '';
      send('restore', [act.dataset.trash]);
    } else if (actName === 'emptyTrash' || actName === 'emptyNo') {
      state.confirmEmpty = actName === 'emptyTrash';
      render();
    } else if (actName === 'emptyYes') {
      state.confirmEmpty = false;
      state.noteFor = 'trash';
      send('emptyTrash');
    } else if (actName === 'pendToggle') {
      state.pendOpen = !state.pendOpen;
      renderPend();
    } else if (actName === 'undo') {
      const item = state.pendItems[Number(act.dataset.undo)];

      state.noteFor = '';
      if (item && item.undo) item.undo();
    } else if (actName === 'quitGame' || actName === 'quitNo') {
      state.confirmQuit = actName === 'quitGame';
      renderPend();
    } else if (actName === 'quitYes' && state.confirmQuit) send('quitGame');
    else if (actName === 'reset' && row) resetRow(row);
    else if ((actName === 'worldKeep' || actName === 'worldGlobal') && row) send('world', settingParts(row).slice(0, 3).concat([actName === 'worldKeep' ? 'keep' : 'global']));
    else if (actName === 'bind' && row) bind(row, act);
    else if (actName === 'pick' && row) openPicker(row, null);
    else if (actName === 'swap' && row) openPicker(row, Number(act.dataset.index));
    else if (actName === 'dropItem' && row) dropItem(row, Number(act.dataset.index));
    else if ((actName === 'dec' || actName === 'inc') && row) bump(row, Number(act.dataset.index), (actName === 'inc' ? 1 : -1) * (e.shiftKey ? 10 : 1));
    else if (actName === 'byHand' && row) writeList(row, listOf(row.mod, row.setting));
    else if (actName === 'setChoice' && row) setChoice(row, Number(act.dataset.index), act.dataset.value);
    else if ((actName === 'moveUp' || actName === 'moveDown') && row) moveItem(row, Number(act.dataset.index), Number(act.dataset.index) + (actName === 'moveUp' ? -1 : 1));
    else if (actName === 'editText' && row) editText(row);
    else pickOption(target, row);
  }

  // The wheel over a card's amount steps it, one a notch or ten with Shift; anywhere else on the card it scrolls the page.
  function onWheel(e) {
    const stepper = e.target.closest('.mf .slot[data-slot] .stp') || e.target.closest('.el-row[data-slot] .stp');
    const card = stepper ? stepper.closest('.slot[data-slot]') || stepper.closest('.el-row[data-slot]') : null;
    const row = card ? rowOf(card) : null;
    const turn = e.deltaY || e.deltaX;

    if (!row || !hasStepper(row.setting) || row.setting.readOnly || !turn) return;

    e.preventDefault();
    bump(row, Number(card.dataset.slot), (turn < 0 ? 1 : -1) * (e.shiftKey ? 10 : 1));
  }

  // Cards are reordered by holding one and letting go over another; a press and release on the same card is a click.
  function onMouseDown(e) {
    const tile = e.button === 0 ? e.target.closest('.mf .slot[data-slot] .tile') : null;
    const row = tile ? rowOf(tile) : null;

    if (!row || row.setting.readOnly) return;

    state.drag = { id: settingId(row.mod, row.setting), from: Number(tile.closest('.slot').dataset.slot) };
  }

  function onMouseOver(e) {
    if (!state.drag) return;

    root.querySelectorAll('.mf .slot.over').forEach(el => el.classList.remove('over'));

    const card = e.target.closest('.mf .slot[data-slot]');
    const row = card ? rowOf(card) : null;

    if (row && settingId(row.mod, row.setting) === state.drag.id && Number(card.dataset.slot) !== state.drag.from) card.classList.add('over');
  }

  function onMouseUp(e) {
    const drag = state.drag;

    if (!drag) return;

    root.querySelectorAll('.mf .slot.over').forEach(el => el.classList.remove('over'));

    const card = e.target.closest('.mf .slot[data-slot]');
    const row = card ? rowOf(card) : null;
    const to = card ? Number(card.dataset.slot) : -1;

    state.drag = null;

    // The click that follows lands on the row of cards, which does nothing, so only a release on the same card opens the picker.
    if (row && settingId(row.mod, row.setting) === drag.id && to !== drag.from) moveItem(row, drag.from, to);
  }

  function select(guid) {
    if (guid !== state.selected) {
      state.modNote = null;
      state.confirmUninstall = null;
    }

    state.selected = guid;
    state.page = 'mods';
    state.query = '';
    state.tab = '';
    state.side = '';
    state.editing = null;
    searchBox.value = '';
    render();
    pane.scrollTop = 0;
  }

  function resetMod() {
    state.pending = null;
    send('resetMod', [state.selected]);
  }

  function resetRow(row) {
    state.pending = settingId(row.mod, row.setting);
    send('reset', settingParts(row));
  }

  function bind(row, keycap) {
    const id = settingId(row.mod, row.setting);

    if (state.binding === id) return;

    state.binding = id;
    keycap.classList.add('listening');
    keycap.textContent = word('pressKey');
    send('bind', settingParts(row));
  }

  function pickOption(target, row) {
    const dd = target.closest('.dd');
    const item = target.closest('.dd-item');

    closeDropdowns(dd);

    if (item && row) {
      dd.classList.remove('open');
      write(row, item.dataset.value !== undefined ? item.dataset.value : row.setting.options[Number(item.dataset.option)]);
    } else if (dd) {
      dd.classList.toggle('open');
    }
  }

  function onChange(e) {
    const target = e.target;

    if (target.closest('[data-own]')) return;

    const row = rowOf(target);

    if (target.dataset.switch && state.selected) {
      send('switch', [state.selected, target.checked ? '1' : '0']);

      return;
    }

    if (target.dataset.uncfg) {
      state.removeSettings = target.checked;

      return;
    }

    if (target.dataset.input === 'pkCount') {
      const picking = pickerRow();

      // Only the box changes: redrawing the panel here would swap out the button a click on it is landing on.
      if (picking) {
        state.picker.count = clampValue(picking.setting, parseFloat(target.value));
        target.value = String(state.picker.count);
      }

      return;
    }

    if (!row) return;

    // The list typed as text is written as it is; the C# side reads it and says why when it cannot. Esc has already
    // ended the edit when the browser sends the change of the line it is removing, and that change is dropped.
    if (target.classList.contains('mf-in')) {
      if (state.editing === null) return;

      state.editing = null;
    }

    if (target.dataset.input === 'toggle') write(row, target.checked ? 'true' : 'false');
    else if (target.dataset.input === 'range' || target.dataset.input === 'number' || target.dataset.input === 'text') write(row, target.value);
    else if (target.dataset.input === 'count') setItemCount(row, Number(target.dataset.index), target.value);
  }

  function onInput(e) {
    const target = e.target;

    if (target.closest('[data-own]')) return;

    if (target === searchBox) {
      state.query = searchBox.value.trim();
      render();

      return;
    }

    if (target.classList.contains('bw-find')) {
      state.browseQuery = target.value.trim();
      render();

      return;
    }

    if (target.classList.contains('pk-find') && state.picker) {
      const picking = pickerRow();

      state.picker.q = target.value;
      state.picker.shown = PICKER_PAGE;
      state.picker.top = true;

      if (picking) fillPicker(picking);

      return;
    }

    if (target.dataset.input !== 'range') return;

    const row = rowOf(target);
    const box = target.parentElement.querySelector('.num');

    if (box) box.value = target.value;

    if (row) target.style.cssText = track(row.setting, parseFloat(target.value));
  }

  // The keyboard reaches this page only while a text field has focus; Esc there leaves the field, not the screen.
  // Enter in the picker's search puts in the item shown on the right, else the first one found.
  function onKeyDown(e) {
    if (e.defaultPrevented || !isTextField(e.target)) return;

    if (e.key === 'Enter' && e.target.classList.contains('pk-find') && state.picker) {
      e.preventDefault();

      const picking = pickerRow();
      const first = picking ? shelf(picking.setting)[0] : null;

      if (picking && isEntries(picking.setting)) {
        if (first) toggleEntry(picking, first.id, true);

        return;
      }

      if (state.picker.pick === null && first) state.picker.pick = String(first.id);

      commit();

      return;
    }

    if (e.key !== 'Escape') return;

    e.preventDefault();

    if (e.target === searchBox && searchBox.value) {
      searchBox.value = '';
      state.query = '';
      render();
    } else if (state.picker && e.target.closest('.pk-host')) {
      closePicker();
    } else if (e.target.classList.contains('mf-in')) {
      state.editing = null;
      render();
    } else {
      e.target.blur();
    }
  }

  // Leaving the list's text line without a change puts the cards back.
  function onFocusOut(e) {
    if (!e.target.classList || !e.target.classList.contains('mf-in')) return;

    setTimeout(() => {
      if (!state.editing || document.activeElement === e.target) return;

      state.editing = null;
      render();
    }, 0);
  }

  function isTextField(el) {
    const isField = !!el && (el.tagName === 'TEXTAREA' || el.tagName === 'INPUT' && el.type !== 'checkbox' && el.type !== 'range');

    return isField && root.contains(el);
  }

  // Tells the C# side when a field takes or loses the keyboard. A redraw can drop the focused field without a blur.
  function syncTyping() {
    const isTyping = root.classList.contains('on') && isTextField(document.activeElement);

    if (isTyping === state.isTyping) return;

    state.isTyping = isTyping;
    send('focus', [isTyping ? '1' : '0']);
  }

  function setLanguage(lang) {
    state.lang = lang === 'zh' ? 'zh' : 'en';

    try {
      window.localStorage.setItem(LANG_KEY, state.lang);
    } catch (e) {
      // A page without storage just forgets the choice.
    }

    render();
  }

  function savedLanguage() {
    try {
      return window.localStorage.getItem(LANG_KEY);
    } catch (e) {
      return null;
    }
  }

  function hide() {
    root.classList.remove('on');
    showOwnTab(null, null);
    state.binding = null;
    closeDropdowns(null);
    closePicker();

    if (document.activeElement && root.contains(document.activeElement)) document.activeElement.blur();

    syncTyping();
  }

  // ===== Tabs other mods add =====

  // A mod adds a tab to its own page by pushing { guid, id, title: { en, zh }, show(el, ctx), hide(), escape() } onto
  // window.__modmanagerTabs, then calling window.__modmanager.tabsChanged() when Mod Manager is already there. show runs
  // on every redraw while the tab is on; el may be a fresh empty node, which the tab fills again. The tab talks to its
  // own mod through its own messages; Mod Manager only gives it room, the footer and Esc.
  const OWN_SIDE = 'own:';
  const isOwnSide = side => side.indexOf(OWN_SIDE) === 0;

  function ownTabs(m) {
    const all = Array.isArray(window.__modmanagerTabs) ? window.__modmanagerTabs : [];

    return all.filter(t => t && t.guid === m.guid && typeof t.id === 'string' && t.id && typeof t.show === 'function');
  }

  function ownTabOf(m, side) {
    return isOwnSide(side) ? ownTabs(m).find(t => OWN_SIDE + t.id === side) || null : null;
  }

  function ownTitle(t) {
    const title = t.title || {};

    return (state.lang === 'zh' && title.zh) || title.en || t.id;
  }

  // A tab that throws is the other mod's bug; the screen carries on without it.
  function callTab(tab, method, args) {
    try {
      return typeof tab[method] === 'function' ? tab[method].apply(tab, args || []) : undefined;
    } catch (e) {
      return undefined;
    }
  }

  function showOwnTab(m, tab) {
    const key = tab ? m.guid + '\n' + tab.id : null;
    const shown = state.ownShown;

    if (shown && shown.key !== key) {
      state.ownShown = null;
      state.ownFooter = null;
      callTab(shown.tab, 'hide');
    }

    const host = tab ? pane.querySelector('[data-own]') : null;

    if (!host) return;

    state.ownShown = { key: key, tab: tab };
    callTab(tab, 'show', [host, ownContext(key)]);
  }

  function ownContext(key) {
    const isCurrent = () => !!state.ownShown && state.ownShown.key === key;

    return {
      lang: state.lang,
      footer: (html, isWarn) => {
        if (!isCurrent()) return;

        state.ownFooter = { html: String(html || ''), isWarn: !!isWarn };
        renderFooter();
      }
    };
  }

  // ===== Calls from the C# side =====

  // filter: a list filter to start on (the update toast opens on "updates"); without one the page keeps its own.
  function open(view, filter) {
    const saved = savedLanguage();

    state.view = view;
    state.lang = saved === 'zh' || saved === 'en' ? saved : view.zh ? 'zh' : 'en';
    state.notes = {};
    state.pending = null;
    state.binding = null;
    state.modNote = null;
    state.notesAsked = {};
    state.picker = null;
    flushDraft();
    state.drafts = {};
    state.editing = null;
    state.copied = null;
    state.confirmUninstall = null;
    state.confirmEmpty = false;
    state.confirmQuit = false;
    state.noteFor = '';

    if (!modByGuid(state.selected)) state.selected = view.mods.length ? (view.mods.find(m => m.loaded) || view.mods[0]).guid : null;

    if (filter) openOn(filter);

    fit();
    root.classList.add('on');
    render();
  }

  function update(mod, note) {
    if (!state.view) return;

    const index = state.view.mods.findIndex(m => m.guid === mod.guid);

    if (index < 0) return;

    state.view.mods[index] = mod;

    for (const id of Object.keys(state.notes)) if (id.indexOf(mod.guid + '\n') === 0) delete state.notes[id];

    // A count still being stepped stays on show until it is written; the rest now show what the mod has.
    for (const id of Object.keys(state.drafts)) if (id.indexOf(mod.guid + '\n') === 0 && id !== draftId) delete state.drafts[id];

    if (note) state.notes[mod.guid + '\n' + note.section + '\n' + note.key] = note;

    state.binding = null;
    render();
    state.pending = null;
    state.flash = null;
  }

  // A switch flip can change more than one mod, so the whole list comes back; the selection and language stay. With no
  // note it is news from outside (an update check), which waits while the player types rather than taking the field away.
  function replace(view, note) {
    if (!state.view) return;

    state.view = view;

    if (note === undefined && state.isTyping) return;

    if (note !== undefined) {
      state.modNote = note ? Object.assign({}, note, { onTrash: state.noteFor === 'trash' }) : note;
      state.noteFor = '';
      state.binding = null;
    }

    if (!modByGuid(state.selected)) state.selected = view.mods.length ? view.mods[0].guid : null;

    render();
  }

  function openOn(filter) {
    const first = filter === 'updates' ? mods().find(isUpdatable) : null;

    state.filter = filter;
    state.query = '';
    searchBox.value = '';

    if (first) state.selected = first.guid;
  }

  function bindEnded() {
    state.binding = null;
    render();
  }

  // Esc while no field has the keyboard: the tab on show may use it first, to close something of its own.
  function escape() {
    if (!root.classList.contains('on')) return;

    if (state.picker) {
      closePicker();

      return;
    }

    const shown = state.ownShown;

    if (shown && callTab(shown.tab, 'escape') === true) return;

    hide();
    send('close');
  }

  function tabsChanged() {
    if (root.classList.contains('on')) render();
  }

  // Icons come once per page, apart from the view: they are large and do not change while the game runs.
  function icons(map) {
    state.icons = map || {};

    if (root.classList.contains('on')) render();
  }

  function banners(map) {
    state.banners = map || {};

    if (root.classList.contains('on')) render();
  }

  // The game's item table, in the game's language, for the item and category controls. Sent before the screen opens.
  // The picker's first shelf is the items recipes take most; its category shelves put the ones with the most recipe
  // materials first, so Materials leads, then the rest in the game's order.
  function items(table) {
    const byId = {};
    const perCat = {};
    const usedPerCat = {};

    for (const item of table.items) {
      byId[String(item.id)] = item;
      perCat[item.cat] = (perCat[item.cat] || 0) + 1;

      if (item.uses) usedPerCat[item.cat] = (usedPerCat[item.cat] || 0) + 1;
    }

    const common = table.items.filter(item => item.uses > 0).sort((a, b) => b.uses - a.uses || a.id - b.id);
    const shelves = table.categories.filter(c => perCat[c.id]).sort((a, b) => (usedPerCat[b.id] || 0) - (usedPerCat[a.id] || 0) || a.id - b.id);

    state.items = { list: table.items, byId: byId, categories: table.categories, common: common, perCat: perCat, shelves: shelves };

    if (root.classList.contains('on')) render();
  }

  // The entries one List setting picks from, as its mod gave them. Each one's group is its shelf in the picker.
  function entries(id, list) {
    const byId = {};
    const perCat = {};
    const shelves = [];
    let haveCount = 0;

    for (const entry of list) {
      entry.entry = true;
      byId[entry.id] = entry;

      if (entry.have) haveCount++;

      if (!entry.group) continue;

      entry.cat = entry.group.en;

      if (!perCat[entry.cat]) shelves.push({ id: entry.cat, en: entry.group.en, zh: entry.group.zh });

      perCat[entry.cat] = (perCat[entry.cat] || 0) + 1;
    }

    state.entries[id] = { list: list, byId: byId, perCat: perCat, shelves: shelves.length > 1 ? shelves : [], haveCount: haveCount };

    if (root.classList.contains('on')) render();

    if (state.picker) drawPicker(false);
  }

  let copiedTimer = 0;

  function copied(isCopied) {
    state.copied = !!isCopied;
    clearTimeout(copiedTimer);
    copiedTimer = setTimeout(() => {
      state.copied = null;
      renderCopyButton();
    }, COPIED_MS);
    renderCopyButton();
  }

  let pfCopiedTimer = 0;

  function profileCopied(name, isCopied) {
    state.pfCopied = { name: name, ok: !!isCopied };
    clearTimeout(pfCopiedTimer);
    pfCopiedTimer = setTimeout(() => {
      state.pfCopied = null;

      if (root.classList.contains('on')) render();
    }, COPIED_MS);
    render();
  }

  function asked(isCopied) {
    if (state.asked) state.asked.ok = !!isCopied;

    render();
  }

  // ===== Start =====

  document.head.appendChild(style);
  document.body.appendChild(root);
  // The backdrop fills the screen at any size, so the window inside it is what follows the UI size and has to fit.
  uiSize($('.win'));
  root.addEventListener('click', onClick);
  root.addEventListener('change', onChange);
  root.addEventListener('input', onInput);
  root.addEventListener('keydown', onKeyDown);
  root.addEventListener('wheel', onWheel, { passive: false });
  root.addEventListener('mousedown', onMouseDown);
  root.addEventListener('mouseover', onMouseOver);
  root.addEventListener('mouseup', onMouseUp);
  // A scroll does not bubble, so the picker's grid is watched on the way down.
  root.addEventListener('scroll', onPickerScroll, true);
  root.addEventListener('focusin', () => setTimeout(syncTyping, 0));
  root.addEventListener('focusout', e => {
    onFocusOut(e);
    setTimeout(syncTyping, 0);
  });
  window.addEventListener('resize', fit);

  window.__modmanager = { open: open, update: update, replace: replace, close: hide, bindEnded: bindEnded, escape: escape, tabsChanged: tabsChanged, icons: icons, banners: banners, items: items, entries: entries, copied: copied, asked: asked, profileCopied: profileCopied };
  sendReady();
})();
