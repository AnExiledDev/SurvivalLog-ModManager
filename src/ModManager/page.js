// Mod Manager: the Mods screen, run in the game's root web document above every page.
// The C# side reads every plugin and its settings, checks and writes each value, and sends the result back through
// window.__modmanager; this script only draws and reports clicks. Data is put in the page through esc() or textContent.
(function () {
  if (window.__modmanager) return;

  const PREFIX = 'slmod|modmanager|';
  const MENU_POLL_MS = 1000;
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
      footerNone: 'No changes this session. Everything saves the moment you change it.',
      footerSome: n => '<b>' + n + '</b> ' + (n === 1 ? 'setting' : 'settings') + ' changed this session, saved.',
      footerRestartHint: 'Some of them apply only after a restart: see the marks beside them.',
      menuButton: 'Mods',
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
      advanced: 'Advanced', hotkeys: 'HOTKEYS', linkChangelog: 'CHANGELOG', add: '+ ADD', remove: 'Remove',
      findItem: 'FIND AN ITEM', noItemMatches: 'No item matches.', moreItems: n => n + ' more, type to narrow it down',
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
      footerNone: '本次没有修改。每项设置一改就会保存。',
      footerSome: n => '本次修改了 <b>' + n + '</b> 项设置，已保存。',
      footerRestartHint: '其中有些需要重启游戏才会生效：见设置旁的标记。',
      menuButton: '模组',
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
      advanced: '高级', hotkeys: '快捷键', linkChangelog: '更新日志', add: '+ 添加', remove: '移除',
      findItem: '搜索物品', noItemMatches: '没有匹配的物品。', moreItems: n => '还有 ' + n + ' 个，输入文字缩小范围',
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
    openAdvanced: {},
    side: '',
    rail: {},
    copied: null,
    showLibraries: false,
    ownShown: null,
    ownFooter: null
  };

  const LINK_WORDS = { nexus: 'linkNexus', github: 'linkGithub', website: 'linkWebsite', issues: 'linkIssues', changelog: 'linkChangelog' };
  const PICKER_LIMIT = 60;
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
#slmm .ipk-tags { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
#slmm .ipk-tag { display: flex; align-items: center; gap: 7px; padding: 3px 4px 3px 4px; border: 1px solid rgba(255,255,255,0.14); border-radius: 3px; font-size: .88rem; color: #ddd; max-width: 100%; }
#slmm .ipk-tag .ico-sq { width: 22px; height: 22px; }
#slmm .ipk-tag .num { width: 64px; padding: 3px 6px; font-size: .88rem; }
#slmm .ipk-x { cursor: pointer; color: #888; padding: 0 5px; font-size: 1rem; line-height: 1; }
#slmm .ipk-x:hover { color: var(--red); }
#slmm .ipk-add { font-family: var(--mono); font-size: .78rem; letter-spacing: 1px; color: var(--amber); border: 1px dashed rgba(235,163,8,0.5); border-radius: 3px; padding: 4px 10px; cursor: pointer; }
#slmm .ipk-add:hover { background: rgba(235,163,8,0.12); }
#slmm .ipk-panel { position: absolute; top: calc(100% + 4px); right: 0; width: 380px; background: #121214; border: 1px solid #555; border-radius: 4px; box-shadow: 0 10px 30px rgba(0,0,0,0.85); z-index: 30; padding: 8px; }
#slmm .ipk-find { width: 100%; background: #000; border: 1px solid #444; border-radius: 3px; color: #eee; font: 700 .95rem var(--body); padding: 7px 10px; outline: none; user-select: text; }
#slmm .ipk-find:focus { border-color: var(--amber); }
#slmm .ipk-list { max-height: 300px; overflow-y: auto; margin-top: 6px; }
#slmm .ipk-list::-webkit-scrollbar { width: 6px; }
#slmm .ipk-list::-webkit-scrollbar-thumb { background: rgba(235,163,8,0.35); border-radius: 3px; }
#slmm .ipk-item { display: flex; align-items: center; gap: 10px; padding: 4px 6px; border-radius: 3px; cursor: pointer; color: #ccc; font-size: .92rem; font-weight: 700; }
#slmm .ipk-item:hover { background: rgba(235,163,8,0.15); color: #fff; }
#slmm .ipk-item.selected { color: var(--amber); }
#slmm .ipk-item i { margin-left: auto; font-style: normal; font-family: var(--mono); font-size: .72rem; color: #666; font-weight: 400; }
#slmm .ipk-more { padding: 6px; font-family: var(--mono); font-size: .75rem; color: #777; }
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
</div></div>`;

  const $ = selector => root.querySelector(selector);
  const searchBox = $('.find');
  const list = $('.list');
  const pane = $('.pane');

  function fit() {
    const scale = Math.min((window.innerWidth || STAGE_WIDTH) / STAGE_WIDTH, (window.innerHeight || STAGE_HEIGHT) / STAGE_HEIGHT);

    $('.stage').style.transform = 'translate(-50%,-50%) scale(' + scale + ')';
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
    focusPicker();
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

    const readOnly = s.readOnly ? '<span class="ro">' + esc(word('readOnly')) + '</span>' : '';
    const restartMark = restartBadge(s);
    const canReset = !s.readOnly;

    return '<div class="row' + (isChanged(s) ? ' chg' : '') + (id === state.pending ? ' flash' : '') + '" data-row="' + index + '">' +
      '<div><div class="lab">' + show(s.label) + readOnly + '</div>' + lines.join('') + '</div>' +
      '<div>' + restartMark + '</div>' +
      '<div class="ctl' + (s.readOnly ? ' locked' : '') + '">' + control(m, s, index) + '</div>' +
      '<div class="reset' + (canReset ? ' can' : '') + '" data-act="reset" title="' + esc(word('resetTo')(shown(s, s.default))) + '">&#x21BA;</div></div>';
  }

  // A changed setting says whether the change needs a restart; a setting the mod applies at once says nothing.
  function restartBadge(s) {
    if (!isChangedThisSession(s) || s.apply === 'live') return '';

    return '<span class="badge">&#x21BB; ' + esc(word(s.apply === 'restart' ? 'needsRestart' : 'mayRestart')) + '</span>';
  }

  function shown(s, value) {
    if (s.kind === 'hotkey' && (value === '' || value === 'None')) return word('none');

    if (!state.items) return value;

    if (s.kind === 'item') return itemName(value);

    if (s.kind === 'category') return categoryName(value);

    if (s.kind === 'itemList') return idsOf(value).map(itemName).join(', ') || word('none');

    if (s.kind === 'itemCounts') return countsOf(value).map(c => itemName(c.id) + ' x' + c.count).join(', ') || word('none');

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
      case 'itemList':
      case 'itemCounts':
        if (state.items) return itemControl(m, s);
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

  const idsOf = value => String(value).split(',').map(part => part.trim()).filter(Boolean);
  const countsOf = value => idsOf(value).map(pair => ({ id: pair.split(':')[0], count: pair.split(':')[1] || '1' }));
  const countsText = counts => counts.map(c => c.id + ':' + c.count).join(',');
  const itemOf = id => state.items.byId[String(id)];

  function itemName(id) {
    if (String(id) === '0') return word('none');

    const item = itemOf(id);

    return item ? item.name : '#' + id;
  }

  function categoryName(id) {
    if (String(id) === '0') return word('none');

    const category = state.items.categories.find(c => String(c.id) === String(id));

    return category ? category.name : '#' + id;
  }

  function itemIcon(id) {
    const item = itemOf(id);

    return item && item.icon ? '<img class="ico-sq" src="' + esc(item.icon) + '" alt="">' : '<span class="ico-sq"></span>';
  }

  function itemControl(m, s) {
    const isPicking = state.picker && state.picker.id === settingId(m, s);
    const panel = isPicking ? pickerPanel(s) : '';

    if (s.kind === 'item') return '<div class="ipk"><div class="ipk-pick" data-act="pick">' + itemIcon(s.current) + '<span class="ipk-name">' + esc(itemName(s.current)) + '</span><span class="dd-arrow"></span></div>' + panel + '</div>';

    const remove = index => '<span class="ipk-x" data-act="dropItem" data-index="' + index + '" title="' + esc(word('remove')) + '">&#x2715;</span>';
    const tags = s.kind === 'itemList'
      ? idsOf(s.current).map((id, i) => '<span class="ipk-tag">' + itemIcon(id) + esc(itemName(id)) + remove(i) + '</span>')
      : countsOf(s.current).map((c, i) => '<span class="ipk-tag">' + itemIcon(c.id) + esc(itemName(c.id)) + '<input class="num" data-input="count" data-index="' + i + '" value="' + esc(c.count) + '">' + remove(i) + '</span>');

    return '<div class="ipk"><div class="ipk-tags">' + tags.join('') + '<span class="ipk-add" data-act="pick">' + esc(word('add')) + '</span></div>' + panel + '</div>';
  }

  function pickerPanel(s) {
    return '<div class="ipk-panel"><input class="ipk-find" placeholder="' + esc(word('findItem')) + '" value="' + esc(state.picker.query) + '" spellcheck="false" autocomplete="off">' +
      '<div class="ipk-list">' + pickerItems(s) + '</div></div>';
  }

  // The items matching the picker's search, by name or exact id, capped so a page of thousands stays quick.
  function pickerItems(s) {
    const q = state.picker.query.trim().toLowerCase();
    const chosen = s.kind === 'item' ? [s.current] : s.kind === 'itemList' ? idsOf(s.current) : countsOf(s.current).map(c => c.id);
    const matches = state.items.list.filter(item => !q || item.name.toLowerCase().indexOf(q) >= 0 || String(item.id) === q);
    const none = s.kind === 'item' && !q ? [{ id: 0, name: word('none'), icon: null, cat: -1 }] : [];
    const shown = none.concat(matches.slice(0, PICKER_LIMIT));
    const line = item => '<div class="ipk-item' + (chosen.indexOf(String(item.id)) >= 0 ? ' selected' : '') + '" data-act="pickItem" data-id="' + item.id + '">' +
      (item.icon ? '<img class="ico-sq" src="' + esc(item.icon) + '" alt="">' : '<span class="ico-sq"></span>') + esc(item.name) + (item.cat >= 0 ? '<i>' + esc(categoryName(item.cat)) + '</i>' : '') + '</div>';
    const more = matches.length > PICKER_LIMIT ? '<div class="ipk-more">' + esc(word('moreItems')(matches.length - PICKER_LIMIT)) + '</div>' : '';

    return shown.length ? shown.map(line).join('') + more : '<div class="ipk-more">' + esc(word('noItemMatches')) + '</div>';
  }

  function categoryControl(s) {
    const options = [{ id: 0, name: word('none') }].concat(state.items.categories);
    const items = options.map(c => '<div class="dd-item' + (String(c.id) === s.current ? ' selected' : '') + '" data-value="' + c.id + '">' + esc(c.name) + '</div>').join('');

    return '<div class="dd"><div class="dd-trigger"><span>' + esc(categoryName(s.current)) + '</span><span class="dd-arrow"></span></div><div class="dd-list">' + items + '</div></div>';
  }

  function openPicker(row) {
    state.picker = { id: settingId(row.mod, row.setting), query: '' };
    render();
  }

  function closePicker() {
    if (!state.picker) return;

    state.picker = null;
    root.querySelectorAll('.ipk-panel').forEach(panel => panel.remove());
  }

  // A single item closes the picker; a list keeps it open, with the keyboard in its search box, to add more.
  function pickItem(row, id) {
    const s = row.setting;

    if (s.kind === 'item') {
      closePicker();
      write(row, id);

      return;
    }

    focusPicker();

    if (s.kind === 'itemList') {
      const ids = idsOf(s.current);

      if (ids.indexOf(id) < 0) write(row, ids.concat([id]).join(','));

      return;
    }

    const counts = countsOf(s.current);

    if (!counts.some(c => c.id === id)) write(row, countsText(counts.concat([{ id: id, count: '1' }])));
  }

  function dropItem(row, index) {
    const s = row.setting;

    if (s.kind === 'itemList') write(row, idsOf(s.current).filter((id, i) => i !== index).join(','));
    else write(row, countsText(countsOf(s.current).filter((c, i) => i !== index)));
  }

  function setItemCount(row, index, count) {
    const counts = countsOf(row.setting.current);

    if (counts[index]) counts[index].count = count.trim();

    write(row, countsText(counts));
  }

  // A redraw replaces the picker's search box; give it the keyboard back so typing carries on.
  function focusPicker() {
    const find = state.picker ? pane.querySelector('.ipk-find') : null;

    if (!find || document.activeElement === find) return;

    find.focus();
    find.setSelectionRange(find.value.length, find.value.length);
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

  function choiceControl(s) {
    const isSelected = option => option.toLowerCase() === s.current.toLowerCase();
    const items = s.options.map((option, i) => '<div class="dd-item' + (isSelected(option) ? ' selected' : '') + '" data-option="' + i + '">' + esc(option) + '</div>').join('');

    return '<div class="dd"><div class="dd-trigger"><span>' + esc(s.current) + '</span><span class="dd-arrow"></span></div><div class="dd-list">' + items + '</div></div>';
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

    if (state.picker && !target.closest('.ipk-panel') && actName !== 'pick') closePicker();

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
    else if (actName === 'bind' && row) bind(row, act);
    else if (actName === 'pick' && row) togglePicker(row);
    else if (actName === 'pickItem' && row) pickItem(row, act.dataset.id);
    else if (actName === 'dropItem' && row) dropItem(row, Number(act.dataset.index));
    else if (!target.closest('.ipk-panel')) pickOption(target, row);
  }

  function togglePicker(row) {
    if (state.picker && state.picker.id === settingId(row.mod, row.setting)) closePicker();
    else openPicker(row);
  }

  function select(guid) {
    if (guid !== state.selected) state.modNote = null;

    state.selected = guid;
    state.page = 'mods';
    state.query = '';
    state.tab = '';
    state.side = '';
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

    if (!row) return;

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

    if (target.classList.contains('ipk-find')) {
      const row = rowOf(target);

      state.picker.query = target.value;

      if (row) target.parentElement.querySelector('.ipk-list').innerHTML = pickerItems(row.setting);

      return;
    }

    if (target.dataset.input !== 'range') return;

    const row = rowOf(target);
    const box = target.parentElement.querySelector('.num');

    if (box) box.value = target.value;

    if (row) target.style.cssText = track(row.setting, parseFloat(target.value));
  }

  // The keyboard reaches this page only while a text field has focus; Esc there leaves the field, not the screen.
  function onKeyDown(e) {
    if (e.key !== 'Escape' || e.defaultPrevented || !isTextField(e.target)) return;

    e.preventDefault();

    if (e.target === searchBox && searchBox.value) {
      searchBox.value = '';
      state.query = '';
      render();
    } else if (e.target.classList.contains('ipk-find')) {
      closePicker();
      syncTyping();
    } else {
      e.target.blur();
    }
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

    if (note) state.notes[mod.guid + '\n' + note.section + '\n' + note.key] = note;

    state.binding = null;
    render();
    state.pending = null;
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
  function items(table) {
    const byId = {};

    for (const item of table.items) byId[String(item.id)] = item;

    state.items = { list: table.items, byId: byId, categories: table.categories };

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

  // ===== MODS buttons in the game's own menus =====

  function isCjk(text) {
    return /[\u3000-\u303f\u4e00-\u9fff\uff00-\uffef]/.test(text || '');
  }

  // The Esc menu: a button like the game's own, right after Settings.
  function addEscButton(doc) {
    const settings = doc.querySelector('.menu-list > .main-btn[data-i18n="settings"]');

    if (!settings) return;

    let button = doc.getElementById('slmm-esc');

    if (!button) {
      button = doc.createElement('div');
      button.id = 'slmm-esc';
      button.className = 'main-btn';
      button.addEventListener('click', () => send('open'));
    }

    if (settings.nextElementSibling !== button) settings.parentNode.insertBefore(button, settings.nextElementSibling);

    button.textContent = isCjk(settings.textContent) ? WORDS.zh.menuButton : WORDS.en.menuButton;
  }

  // The title screen: an item like the game's own, right before Exit. The game's click handler reads data-i18n, so
  // this one has none and runs its own click.
  function addTitleButton(doc) {
    const exitText = doc.querySelector('.menu > .menu-item > .menu-text[data-i18n="exit"]');

    if (!exitText) return;

    const exit = exitText.parentNode;
    let item = doc.getElementById('slmm-title');

    if (!item) {
      item = doc.createElement('div');
      item.id = 'slmm-title';
      item.className = 'menu-item';
      item.innerHTML = '<div class="bar"></div><div class="menu-text"></div>';
      item.addEventListener('click', () => {
        item.classList.remove('clicked');
        void item.offsetWidth;
        item.classList.add('clicked');
        setTimeout(() => send('open'), 300);
        setTimeout(() => item.classList.remove('clicked'), 450);
      });
    }

    if (exit.previousElementSibling !== item) exit.parentNode.insertBefore(item, exit);

    item.querySelector('.menu-text').textContent = isCjk(exitText.textContent) ? WORDS.zh.menuButton : WORDS.en.menuButton;
  }

  function addMenuButtons() {
    const frames = document.getElementsByTagName('iframe');

    for (let i = 0; i < frames.length; i++) {
      try {
        const src = String(frames[i].src || '');
        const doc = frames[i].contentDocument;

        if (!doc || doc.readyState === 'loading') continue;

        if (/OutSetting\.html/.test(src)) addEscButton(doc);
        else if (/MainUI\.html/.test(src)) addTitleButton(doc);
      } catch (e) {
        // A page from another origin, or one being torn down: try again next time.
      }
    }
  }

  // ===== Start =====

  document.head.appendChild(style);
  document.body.appendChild(root);
  root.addEventListener('click', onClick);
  root.addEventListener('change', onChange);
  root.addEventListener('input', onInput);
  root.addEventListener('keydown', onKeyDown);
  root.addEventListener('focusin', () => setTimeout(syncTyping, 0));
  root.addEventListener('focusout', () => setTimeout(syncTyping, 0));
  window.addEventListener('resize', fit);
  setInterval(addMenuButtons, MENU_POLL_MS);
  addMenuButtons();

  window.__modmanager = { open: open, update: update, replace: replace, close: hide, bindEnded: bindEnded, escape: escape, tabsChanged: tabsChanged, icons: icons, banners: banners, items: items, copied: copied };
  sendReady();
})();
