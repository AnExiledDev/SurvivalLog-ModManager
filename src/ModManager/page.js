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
      otherErrors: 'BepInEx and game errors', pageErrors: 'ERRORS',
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
      pipAsk: 'ASK', pipRefused: 'REFUSED',
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
      pickAdd: 'ADD', pickSet: 'SET COUNT', pickSwap: 'SWAP IN', pickOne: 'PICK', pickIn: 'IN THE LIST',
      titleAdd: 'ADD AN ITEM', titleSwap: name => 'SWAP ' + name, titleOne: 'PICK AN ITEM',
      clickToSee: 'Click an item to see it here.', dblClickAdds: 'Double-click adds it at once',
      category: 'Category', weight: 'Weight', value: 'Value', recipes: 'Recipes',
      usesIn: n => n ? 'a material in ' + n + ' of the game\'s recipes' : 'not used by any of the game\'s recipes',
      listFull: n => n + ' items already. Remove one first.', noMatch: q => 'No item matches "' + q + '".',
      showing: (n, of) => 'Showing ' + n + ' of ' + of + '. Scroll for more, or search.',
      copyList: 'COPY MOD LIST', copied: 'COPIED', copyFailed: 'COULD NOT COPY', copyHint: 'Copies every mod with its version and state, and the errors this session, to paste into a bug report.'
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
      otherErrors: 'BepInEx 和游戏的错误', pageErrors: '错误',
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
      pipAsk: '待确认', pipRefused: '已拒绝',
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
      pickAdd: '添加', pickSet: '设置数量', pickSwap: '替换', pickOne: '选择', pickIn: '已在清单中',
      titleAdd: '添加物品', titleSwap: name => '替换 ' + name, titleOne: '选择物品',
      clickToSee: '点击物品可在这里查看。', dblClickAdds: '双击直接添加',
      category: '类别', weight: '重量', value: '价值', recipes: '配方',
      usesIn: n => n ? '游戏中 ' + n + ' 个配方的材料' : '游戏配方都不用它',
      listFull: n => '已经有 ' + n + ' 种物品，请先移除一种。', noMatch: q => '没有物品符合「' + q + '」。',
      showing: (n, of) => '已显示 ' + n + ' / ' + of + '，继续滚动或搜索。',
      copyList: '复制模组列表', copied: '已复制', copyFailed: '无法复制', copyHint: '复制所有模组及其版本和状态，以及这次游戏的错误，可粘贴到问题反馈中。'
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
    ownFooter: null
  };

  const LINK_WORDS = { nexus: 'linkNexus', github: 'linkGithub', website: 'linkWebsite', issues: 'linkIssues', changelog: 'linkChangelog' };
  const PICKER_PAGE = 300;
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
  const canInstall = m => hasUpdate(m) && (m.install.kind === 'none' || m.install.kind === 'failed') && m.rollback.kind !== 'done';
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
#slmm .hd-err i { font-style: normal; font-family: var(--mono); font-size: .85rem; letter-spacing: 0; color: #111; background: var(--red); border-radius: 3px; padding: 2px 7px; }
#slmm .hd-err.warn i { background: #e8813a; }
#slmm .win.page-errors .bd { grid-template-columns: 1fr; }
#slmm .win.page-errors .rail { display: none; }
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
#slmm .pk-keys { font: .72rem var(--mono); color: #666; letter-spacing: 1px; }
@keyframes mf-pop { 0% { transform: scale(1); } 50% { transform: scale(1.25); } 100% { transform: scale(1); } }
`;

  // ===== The window =====

  const root = document.createElement('div');
  root.id = 'slmm';
  root.innerHTML = `
<div class="stage"><div class="win">
  <span class="corner tl"></span><span class="corner tr"></span><span class="corner bl"></span><span class="corner br"></span>
  <div class="hd">
    <div class="hd-tabs"><div class="hd-title on" data-page="mods">MODS</div><div class="hd-title hd-err" data-page="errors"><span data-word="pageErrors"></span><i data-count="otherErrors"></i></div></div>
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
      if (have.value !== want.value) have.value = want.value;

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
    list.scrollTop = listScroll;
    pane.scrollTop = paneScroll;

    if (state.picker || $('.pk-host').firstChild) drawPicker(false);

    syncTyping();
  }

  function renderChrome() {
    const all = mods();
    const openKey = state.view ? state.view.openKey : '';
    const updates = all.filter(hasUpdate).length;

    const withErrors = all.filter(hasErrors).length;

    if (!updates && state.filter === 'updates' || !withErrors && state.filter === 'errors') state.filter = 'all';

    const others = otherErrors();

    if (!others.length) state.page = 'mods';

    $('.win').classList.toggle('page-errors', state.page === 'errors');
    root.querySelectorAll('[data-page]').forEach(el => el.classList.toggle('on', el.dataset.page === state.page));
    $('[data-page="errors"]').style.display = others.length ? '' : 'none';
    $('[data-page="errors"]').classList.toggle('warn', !others.some(e => e.level === 'error'));
    setCount('otherErrors', others.length);

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
    else if (state.filter === 'updates') shown = shown.filter(hasUpdate);
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
    const held = (count, key) => count ? '<div class="upd-line bad held-line">' + esc(word(key)(count)) + '</div>' : '';

    return (installable ? '<div class="key-btn all-btn" data-act="installAll">' + esc(word('installAll')(installable)) + '</div>' : '') +
      held(asking, 'heldAsking') + held(refused, 'heldRefused');
  }

  function otherErrors() {
    return state.view ? state.view.otherErrors : [];
  }

  // The ERRORS page beside MODS: every error no mod could be found for, in full.
  function otherErrorsPage() {
    const others = otherErrors();

    return '<div class="errs-page"><div class="sec-t">' + esc(word('otherErrors')) + ' &middot; ' + others.length + '</div>' +
      '<div class="cf-intro">' + esc(word('otherErrorsIntro')) + '</div>' + others.map(errorBlock).join('') + '</div>';
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

    if (m.install.kind === 'installed' || m.rollback.kind === 'done') return pip('chg', 'pipRestart');

    if (!hasUpdate(m)) return '';

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
      (m.state === 'off' ? '<span class="pip offp">' + esc(word('statusOff')) + '</span>' : '') +
      (isPending(m) ? '<span class="pip chg">' + esc(word('pipRestart')) + '</span>' : '') +
      (m.untested ? '<span class="pip ' + (hasLoggedErrors(m) ? 'warnp' : 'untp') + '">' + esc(word('pipUntested')) + '</span>' : '') +
      (hasLoggedErrors(m) ? '<span class="pip errp">' + esc(word('pipError')) + '</span>' : hasErrors(m) ? '<span class="pip wrnp">' + esc(word('pipWarning')) + '</span>' : '') +
      updatePip(m);

    return '<div class="mod' + (isSelected ? ' sel' : '') + (m.loaded ? '' : ' off') + '" data-mod="' + esc(m.guid) + '" style="animation-delay:' + delay + 'ms">' +
      badge(m, 'mono') +
      '<div><div class="mname">' + esc(nameOf(m)) + '</div><div class="msub">' + sub + '</div></div>' +
      '<div>' + pips + '</div></div>';
  }

  function renderPane() {
    if (state.page === 'errors') {
      showOwnTab(null, null);
      morph(pane, otherErrorsPage());

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

    if (state.side === 'conflicts' && !m.patchOverlaps.length || state.side === 'changelog' && m.changelog.kind === 'none' || state.side === 'errors' && !hasErrors(m)) state.side = '';

    if (isOwnSide(state.side) && !ownTabOf(m, state.side)) state.side = '';

    const ownTab = ownTabOf(m, state.side);
    const railKey = m.guid + '\n' + state.lang;
    const top = ownTab ? hero(m, true) : hero(m, false) + meta(m) + about(m) + links(m) + relations(m) + fixedHotkeys(m) + untestedNotice(m) + relationNotice(m) + updateNotice(m) + rollbackNotice(m);
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

    return '<div class="meta"><span class="id">v<b>' + esc(m.version) + '</b></span>' + updateStatus(m.update) + author + category + library + '<span class="id"><b>' + esc(m.guid) + '</b></span>' + file + '</div>';
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

  function updateNotice(m) {
    if (!hasUpdate(m)) return '';

    return '<div class="notice upd"><span class="ico">&#x2191;</span><div class="upd-main"><b>' + esc(word('updateTitle')(tagText(m.update.tag))) + '</b> ' + esc(word('updateBody')(m.version)) +
      releaseNotes(m.update.notes) + installLine(m.install) + '<div class="upd-act">' + (m.rollback.kind === 'done' ? '' : installButton(m.install)) + '<div class="key-btn" data-act="update">' + esc(word('getUpdate')) + ' &#x2197;</div></div></div></div>';
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
    let html = '<div class="cf"><div class="cf-intro">' + esc(word('conflictsIntro')) + '</div><div class="cf-sum">' + risks.map(pill).join('') + '</div>';

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
    const note = state.modNote && state.modNote.guid === m.guid ? state.modNote : null;

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

    if (m.loaded) buttons.push('<div class="key-btn" data-act="showCfg">' + esc(word('openCfg')) + '</div>');

    if (m.settings.some(s => isChanged(s) && !s.readOnly)) buttons.push('<div class="key-btn danger" data-act="resetMod">' + esc(word('resetAll')) + '</div>');

    return buttons.length ? '<div class="links">' + buttons.join('') + '</div>' : '';
  }

  // A tab per settings section once there are two, then a Changelog tab when the mod names one the page can show,
  // a Conflicts tab when other mods patch the same game code, and an Errors tab when the mod logged any this session.
  function tabs(m, names) {
    const hasConflicts = m.patchOverlaps.length > 0;
    const hasChangelog = m.changelog.kind !== 'none';

    const own = ownTabs(m);

    if (names.length < 2 && !hasConflicts && !hasChangelog && !hasErrors(m) && !own.length) return '';

    const tab = (name, label, members) => '<div class="tab' + (!state.side && state.tab === name ? ' on' : '') + '" data-tab="' + esc(name) + '">' + esc(label) + ' <i>' + members.length + '</i>' + (members.some(isChanged) ? '<span class="cd"></span>' : '') + '</div>';
    const sideTab = (side, inner) => '<div class="tab tab-x' + (state.side === side ? ' on' : '') + '" data-side="' + esc(side) + '">' + inner + '</div>';
    const shown = shownSettings(m);
    const sectionTabs = names.length < 2 ? '' : names.map(n => tab(n, n, shown.filter(s => s.section === n))).join('');
    const worst = ['likely', 'may', 'harmless'].find(risk => m.patchOverlaps.some(o => o.risk === risk));
    const ownTabsHtml = own.map(t => sideTab(OWN_SIDE + t.id, esc(ownTitle(t)))).join('');
    const changelogTab = hasChangelog ? sideTab('changelog', esc(word('tabChangelog'))) : '';
    const conflictsTab = hasConflicts ? sideTab('conflicts', esc(word('tabConflicts')) + ' <i>' + m.patchOverlaps.length + '</i><span class="rd ' + worst + '"></span>') : '';
    const errorsTab = hasErrors(m) ? sideTab('errors', esc(word('tabErrors')) + ' <i>' + m.errors.length + '</i><span class="rd ' + (m.errors.some(e => e.level === 'error') ? 'likely' : 'may') + '"></span>') : '';

    return '<div class="tabs">' + tab('', word(names.length < 2 ? 'tabSettings' : 'tabAll'), shown) + sectionTabs + ownTabsHtml + changelogTab + conflictsTab + errorsTab + '</div>';
  }

  function paneBody(m, names) {
    if (isOwnSide(state.side)) return '<div class="own-tab" data-own="' + esc(m.guid + '\n' + state.side.slice(OWN_SIDE.length)) + '"></div>';

    if (state.side === 'conflicts') return conflictsBody(m);

    if (state.side === 'changelog') return changelogBody(m);

    if (state.side === 'errors') return errorsBody(m);

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
        '<div class="mf-host' + (s.readOnly ? ' locked' : '') + '">' + itemField(m, s) + '</div></div>';
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

  const isList = s => s.kind === 'itemList' || s.kind === 'itemCounts';
  const isListField = s => isList(s) && !!state.items;
  const itemOf = id => state.items.byId[String(id)];
  const itemText = item => (state.lang === 'zh' ? item.zh : item.en);
  const textOf = text => (state.lang === 'zh' ? text.zh : text.en);
  const icon = (item, cls) => (item && item.icon ? '<img src="' + esc(item.icon) + '" alt=""' + (cls ? ' class="' + cls + '"' : '') + '>' : '<span class="noimg">?</span>');

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

      return { id: pair[0].trim(), count: s.kind === 'itemCounts' ? (pair[1] || '1').trim() : '1' };
    });
  }

  // What is written back. An emptied list the mod fills in when blank is saved as "none", so it stays empty.
  function valueText(s, entries) {
    if (!entries.length) return s.auto ? NO_ITEMS : '';

    return entries.map(e => (s.kind === 'itemCounts' ? e.id + ':' + e.count : e.id)).join(',');
  }

  const lineText = (s, entries) => entries.map(e => (s.kind === 'itemCounts' ? e.id + ':' + e.count : e.id)).join(', ');
  const isAuto = (m, s) => s.auto && valueNow(m, s) === '';
  const valueNow = (m, s) => (state.drafts[settingId(m, s)] !== undefined ? state.drafts[settingId(m, s)] : s.current);

  // The list as it shows now: a count still being stepped, else the value; a blank the mod fills in shows the mod's list.
  function listOf(m, s) {
    if (isAuto(m, s)) return s.blank ? entriesOf(s, s.blank) : [];

    return entriesOf(s, valueNow(m, s));
  }

  const clampCount = (s, n) => Math.max(1, Math.min(s.maxCount || 9999, Math.round(n) || 1));
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
    else if (!max || list.length < max) slots += '<div class="slot add' + (s.kind === 'itemList' ? ' plain' : '') + '" data-act="pick"><div class="plus">+</div><div class="al">' + esc(word('addItem')) + '</div></div>';

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
    const item = itemOf(e.id);
    const counts = s.kind === 'itemCounts';
    const title = (item ? item.en + ' / ' + item.zh + '  ' : '') + '#' + e.id;
    const pop = state.flash === settingId(m, s) + '\n' + e.id ? ' pop' : '';
    const dragged = state.drag && state.drag.id === settingId(m, s) && state.drag.from === i ? ' drag' : '';

    return '<div class="slot' + (counts ? '' : ' plain') + dragged + '" data-slot="' + i + '" title="' + esc(title) + '">' +
      '<span class="sw" data-act="swap" data-index="' + i + '" title="' + esc(word('swapItem')) + '">&#x21C4;</span>' +
      '<span class="sx" data-act="dropItem" data-index="' + i + '" title="' + esc(word('remove')) + '">&#x2715;</span>' +
      '<div class="tile" data-act="swap" data-index="' + i + '">' + icon(item) + (counts ? '<span class="qty' + pop + '">&times;' + esc(e.count) + '</span>' : '') + '</div>' +
      '<div class="nm">' + esc(item ? itemText(item) : '#' + e.id) + '</div>' +
      (counts ? '<div class="stp"><span data-act="dec" data-index="' + i + '">&minus;</span><input data-input="count" data-index="' + i + '" value="' + esc(e.count) + '" inputmode="numeric"><span data-act="inc" data-index="' + i + '">+</span></div>' : '') +
      '</div>';
  }

  // One item of the list the mod chose for a blank value, with how many the recipe it belongs to takes.
  function autoSlot(s, e, recipe) {
    const item = itemOf(e.id);
    const from = recipe ? recipe.find(r => r.id === e.id) : null;

    return '<div class="slot auto" title="' + esc((item ? item.en + ' / ' + item.zh + '  ' : '') + '#' + e.id) + '"><div class="tile">' + icon(item) +
      (s.kind === 'itemCounts' ? '<span class="qty">&times;' + esc(e.count) + '</span>' : '') + '</div>' +
      '<div class="nm">' + esc(item ? itemText(item) : '#' + e.id) + '</div>' + (from ? '<div class="from">' + esc(word('ofCount')(from.count)) + '</div>' : '') + '</div>';
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

    list[index].count = String(clampCount(row.setting, parseInt(count, 10)));
    writeList(row, list, list[index].id);
  }

  let draftTimer = 0;
  let draftId = null;

  // Steps and wheel turns show at once and are written once they stop, so a run of clicks is one write, not twenty.
  function bump(row, index, delta) {
    const s = row.setting;
    const id = settingId(row.mod, s);
    const list = listOf(row.mod, s);

    if (!list[index]) return;

    list[index].count = String(clampCount(s, Number(list[index].count) + delta));
    state.drafts[id] = valueText(s, list);
    state.flash = id + '\n' + list[index].id;
    draftId = id;
    render();
    clearTimeout(draftTimer);
    draftTimer = setTimeout(() => {
      draftId = null;

      if (state.drafts[id] !== undefined) write(row, state.drafts[id]);
    }, DRAFT_MS);
  }

  function moveItem(row, from, to) {
    const list = listOf(row.mod, row.setting);
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
    state.picker = { id: settingId(row.mod, s), slot: swapping ? slotIndex : null, q: '', cat: 'common', pick: pick, count: swapping ? Number(swapping.count) || 1 : 1, shown: PICKER_PAGE, top: true };
    drawPicker(true);
  }

  function closePicker() {
    if (!state.picker) return;

    state.picker = null;
    drawPicker(false);
    syncTyping();
  }

  function matches(item, q) {
    const low = q.toLowerCase();

    return item.en.toLowerCase().indexOf(low) >= 0 || item.zh.indexOf(q) >= 0 || String(item.id).indexOf(low) === 0;
  }

  // The items on show: the search over every item, most used by recipes first, else the chosen shelf.
  function shelf() {
    const p = state.picker;
    const all = state.items.list;
    const q = p.q.trim();

    if (q) return all.filter(item => matches(item, q)).sort((a, b) => b.uses - a.uses || a.id - b.id);

    if (p.cat === 'common') return state.items.common;

    if (p.cat === 'all') return all;

    return all.filter(item => String(item.cat) === String(p.cat));
  }

  // Draws the picker into its own layer above the window. The search box is made once, so typing keeps the keyboard.
  function drawPicker(isNew) {
    const host = $('.pk-host');
    const p = state.picker;
    const row = pickerRow();

    if (!p || !row || !state.items) {
      host.innerHTML = '';

      return;
    }

    if (isNew || !host.firstChild) {
      host.innerHTML = '<div class="pk-veil"><div class="pk"><div class="pk-hd"><div class="ti"></div>' +
        '<div class="pk-q"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4"><circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/></svg><input class="pk-find" spellcheck="false" autocomplete="off"></div>' +
        '<div class="used"></div><div class="close-btn" data-act="pkClose">&#x2715;</div></div>' +
        '<div class="pk-bd"><div class="pk-cats"></div><div class="pk-grid"></div><div class="pk-info"></div></div></div></div>';
    }

    fillPicker(row);

    const find = host.querySelector('.pk-find');

    if (isNew && find) {
      find.placeholder = word('searchItems')(state.items.list.length.toLocaleString('en'));
      find.focus();
    }
  }

  function fillPicker(row) {
    const host = $('.pk-host');
    const p = state.picker;
    const s = row.setting;
    const list = isList(s) ? listOf(row.mod, s) : [];
    const grid = host.querySelector('.pk-grid');
    const top = p.top ? 0 : grid.scrollTop;
    const swapping = p.slot !== null ? list[p.slot] : null;
    const title = s.kind === 'item' ? word('titleOne') : swapping ? word('titleSwap')(itemName(swapping.id)) : word('titleAdd');

    host.querySelector('.ti').innerHTML = esc(title) + ' &middot; <em>' + (s.section ? esc(s.section) + ' &middot; ' : '') + esc(s.label) + '</em>';
    host.querySelector('.used').innerHTML = isList(s) && s.maxKinds ? '<b>' + list.length + '</b> / ' + s.maxKinds + ' ' + esc(word('used')) : '';
    host.querySelector('.pk-find').placeholder = word('searchItems')(state.items.list.length.toLocaleString('en'));
    host.querySelector('.pk-cats').innerHTML = pickerShelves();
    grid.innerHTML = pickerGrid(s, list);
    grid.scrollTop = top;
    host.querySelector('.pk-info').innerHTML = pickerInfo(s, list);
    p.top = false;
  }

  function pickerShelves() {
    const p = state.picker;
    const on = cat => (!p.q.trim() && String(p.cat) === String(cat) ? ' on' : '');
    let out = '<div class="pk-cat star' + on('common') + '" data-cat="common"><span>&#x2605; ' + esc(word('usedInRecipes')) + '</span><i>' + state.items.common.length + '</i></div>' +
      '<div class="pk-cat' + on('all') + '" data-cat="all"><span>' + esc(word('everyItem')) + '</span><i>' + state.items.list.length + '</i></div><div class="pk-sep"></div>';

    for (const c of state.items.shelves) out += '<div class="pk-cat' + on(c.id) + '" data-cat="' + c.id + '"><span>' + esc(textOf(c)) + '</span><i>' + state.items.perCat[c.id] + '</i></div>';

    return out;
  }

  function pickerGrid(s, list) {
    const p = state.picker;
    const found = shelf();
    const inList = {};
    const tile = item => {
      const got = inList[String(item.id)];

      return '<div class="it' + (String(item.id) === String(p.pick) ? ' hi' : '') + (got ? ' taken' : '') + '" data-id="' + item.id + '">' + (got && s.kind === 'itemCounts' ? '<span class="got">&times;' + esc(got) + '</span>' : '') +
        (item.icon ? '<img loading="lazy" src="' + esc(item.icon) + '" alt="">' : '<span class="noimg">?</span>') + '<div class="in">' + esc(itemText(item)) + '</div><div class="id">#' + item.id + '</div></div>';
    };

    for (const e of list) inList[e.id] = e.count;

    let out = s.kind === 'item' && !p.q.trim() ? '<div class="it' + (String(p.pick) === '0' ? ' hi' : '') + '" data-id="0"><span class="noimg">-</span><div class="in">' + esc(word('none')) + '</div><div class="id">#0</div></div>' : '';

    out += found.slice(0, p.shown).map(tile).join('');

    if (!found.length) return '<div class="pk-none">' + esc(word('noMatch')(p.q.trim())) + '</div>';

    if (found.length > p.shown) out += '<div class="pk-more" data-act="pkMore">' + esc(word('showing')(p.shown, found.length)) + '</div>';

    return out;
  }

  function pickerInfo(s, list) {
    const p = state.picker;
    const item = p.pick !== null && String(p.pick) !== '0' ? itemOf(p.pick) : null;

    if (p.pick === null || (String(p.pick) !== '0' && !item)) {
      return '<div class="empty-info">' + esc(word('clickToSee')) + '<br><span class="pk-keys">' + esc(word('dblClickAdds')) + '</span></div>';
    }

    const at = list.findIndex(e => e.id === String(p.pick));
    const isFull = isList(s) && p.slot === null && at < 0 && !!s.maxKinds && list.length >= s.maxKinds;
    const isIn = s.kind === 'itemList' && p.slot === null && at >= 0;
    const verb = s.kind === 'item' ? 'pickOne' : p.slot !== null ? 'pickSwap' : isIn ? 'pickIn' : at >= 0 ? 'pickSet' : 'pickAdd';
    const stepper = s.kind === 'itemCounts' ? '<div class="stp"><span data-act="pkDec">&minus;</span><input data-input="pkCount" value="' + p.count + '" inputmode="numeric"><span data-act="pkInc">+</span></div>' : '';
    const go = '<div class="go' + (isFull || isIn ? ' off' : '') + '" data-act="pkGo">' + esc(word(verb)) + '</div>';

    if (!item) return '<div class="t">' + esc(word('none')) + '</div><div class="act">' + go + '</div>';

    return '<div class="big">' + icon(item) + '</div><div class="t">' + esc(itemText(item)) + '</div><div class="t2">' + esc(state.lang === 'zh' ? item.en : item.zh) + '</div>' +
      '<dl><dt>id</dt><dd>' + item.id + '</dd><dt>' + esc(word('category')) + '</dt><dd>' + esc(categoryName(item.cat)) + '</dd>' +
      '<dt>' + esc(word('weight')) + '</dt><dd>' + (item.g / 1000).toLocaleString('en', { maximumFractionDigits: 2 }) + ' kg</dd><dt>' + esc(word('value')) + '</dt><dd>' + item.v + '</dd>' +
      '<dt>' + esc(word('recipes')) + '</dt><dd>' + esc(word('usesIn')(item.uses)) + '</dd></dl>' +
      (isFull ? '<div class="mf-status bad"><i>!</i>' + esc(word('listFull')(s.maxKinds)) + '</div>' : '') +
      '<div class="act">' + stepper + go + '</div>';
  }

  // Clicking an item shows it, with the amount it already has; double-clicking puts it in.
  function pickTile(id, andCommit) {
    const p = state.picker;
    const row = pickerRow();

    if (!row) return;

    const list = isList(row.setting) ? listOf(row.mod, row.setting) : [];
    const got = list.find(e => e.id === String(id));

    p.pick = String(id);
    p.count = got ? Number(got.count) : p.slot !== null && list[p.slot] ? Number(list[p.slot].count) : 1;

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
    const count = String(s.kind === 'itemCounts' ? clampCount(s, p.count) : 1);
    const at = list.findIndex(e => e.id === id);

    // Swapping in an item the list already has elsewhere merges the two into that one.
    if (p.slot !== null && at >= 0 && at !== p.slot) {
      list[at].count = count;
      list.splice(p.slot, 1);
    } else if (p.slot !== null) {
      list[p.slot] = { id: id, count: count };
    } else if (at >= 0) {
      if (s.kind === 'itemList') return;

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
      p.count = clampCount(row.setting, p.count + (actName === 'pkInc' ? 1 : -1) * (shift ? 10 : 1));
      fillPicker(row);
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
    const updates = mods().filter(hasUpdate).length;
    const updateText = updates ? ' <em>' + esc(word('footerUpdates')(updates)) + '</em>' : '';

    const changedText = changed ? word('footerSome')(changed) + (waiting ? ' <em>' + esc(word('footerRestartHint')) + '</em>' : '') : esc(word('footerNone'));
    const own = state.ownShown && state.ownFooter;

    $('.status-dot').classList.toggle('warn', own ? own.isWarn : waiting || restarts > 0);
    morph($('.ft-t'), own ? own.html : changedText + restartText + updateText);
    renderCopyButton();
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
    state.pending = settingId(row.mod, row.setting);
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

    const page = target.closest('[data-page]');

    if (page) {
      state.page = page.dataset.page;
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
    else if (actName === 'install') send('install', [state.selected]);
    else if (actName === 'approve') send('approve', [state.selected]);
    else if (actName === 'installAll') send('installAll');
    else if (actName === 'rollback') send('rollback', [state.selected]);
    else if (actName === 'needs') send('needs', [state.selected]);
    else if (actName === 'copyList') send('copyList');
    else if (actName === 'resetMod') resetMod();
    else if (actName === 'reset' && row) resetRow(row);
    else if ((actName === 'worldKeep' || actName === 'worldGlobal') && row) send('world', settingParts(row).slice(0, 3).concat([actName === 'worldKeep' ? 'keep' : 'global']));
    else if (actName === 'bind' && row) bind(row, act);
    else if (actName === 'pick' && row) openPicker(row, null);
    else if (actName === 'swap' && row) openPicker(row, Number(act.dataset.index));
    else if (actName === 'dropItem' && row) dropItem(row, Number(act.dataset.index));
    else if ((actName === 'dec' || actName === 'inc') && row) bump(row, Number(act.dataset.index), (actName === 'inc' ? 1 : -1) * (e.shiftKey ? 10 : 1));
    else if (actName === 'byHand' && row) writeList(row, listOf(row.mod, row.setting));
    else if (actName === 'editText' && row) editText(row);
    else pickOption(target, row);
  }

  // The wheel over an item's card steps its amount, one a notch.
  function onWheel(e) {
    const card = e.target.closest('.mf .slot[data-slot]');
    const row = card ? rowOf(card) : null;

    if (!row || row.setting.kind !== 'itemCounts' || row.setting.readOnly) return;

    e.preventDefault();
    bump(row, Number(card.dataset.slot), e.deltaY < 0 ? 1 : -1);
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
    if (guid !== state.selected) state.modNote = null;

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

    if (target.dataset.input === 'pkCount') {
      const picking = pickerRow();

      if (picking) {
        state.picker.count = clampCount(picking.setting, parseInt(target.value, 10));
        fillPicker(picking);
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

      const first = shelf()[0];

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
    state.drafts = {};
    state.editing = null;
    state.copied = null;

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
      state.modNote = note;
      state.binding = null;
    }

    if (!modByGuid(state.selected)) state.selected = view.mods.length ? view.mods[0].guid : null;

    render();
  }

  function openOn(filter) {
    const first = filter === 'updates' ? mods().find(hasUpdate) : null;

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

  window.__modmanager = { open: open, update: update, replace: replace, close: hide, bindEnded: bindEnded, escape: escape, tabsChanged: tabsChanged, icons: icons, banners: banners, items: items, copied: copied };
  sendReady();
})();
