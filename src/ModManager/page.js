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
      footerSome: n => '<b>' + n + '</b> ' + (n === 1 ? 'setting' : 'settings') + ' changed this session, saved. <em>Some mods only read settings at start: restart the game if a change does not show.</em>',
      menuButton: 'Mods',
      groupOff: 'Off', offSub: 'off', statusOff: 'OFF', statusRequired: 'REQUIRED', pipRestart: 'RESTART',
      switchOnHint: 'turning it off applies next start', switchOffHint: 'turning it on applies next start',
      offNext: 'off from the next start', onNext: 'on from the next start',
      lockedSelf: 'this is the Mod Manager', lockedNeeded: 'the Mod Manager needs it', cannotMove: 'its DLL is outside the plugins folder',
      by: 'by', neededBy: list => 'Needed by ' + list, linkNexus: 'NEXUS', linkGithub: 'GITHUB', linkWebsite: 'WEBSITE',
      offTitle: 'Turned off.', offBody: 'Its DLL is in a folder BepInEx skips, so it did not start and its settings cannot be shown. Its settings file is kept.',
      pendingOffTitle: 'Off from the next start.', pendingOffBody: 'It keeps running until you restart the game.',
      pendingOnTitle: 'On from the next start.', pendingOnBody: 'Restart the game to start it.',
      alsoStops: list => 'These need it and will not start either: ' + list + '.',
      footerRestart: n => (n === 1 ? '1 mod turns' : n + ' mods turn') + ' on or off at the next start.'
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
      footerSome: n => '本次修改了 <b>' + n + '</b> 项设置，已保存。<em>有些模组只在启动时读取设置：如果修改没有生效，请重启游戏。</em>',
      menuButton: '模组',
      groupOff: '已关闭', offSub: '已关闭', statusOff: '已关闭', statusRequired: '必需', pipRestart: '重启',
      switchOnHint: '关闭将在下次启动时生效', switchOffHint: '开启将在下次启动时生效',
      offNext: '下次启动时关闭', onNext: '下次启动时开启',
      lockedSelf: '这就是模组管理器', lockedNeeded: '模组管理器需要它', cannotMove: '它的 DLL 不在 plugins 文件夹中',
      by: '作者', neededBy: list => '被以下模组需要：' + list, linkNexus: 'NEXUS', linkGithub: 'GITHUB', linkWebsite: '网站',
      offTitle: '已关闭。', offBody: '它的 DLL 在 BepInEx 会跳过的文件夹中，所以没有启动，也无法显示设置。它的设置文件会保留。',
      pendingOffTitle: '下次启动时关闭。', pendingOffBody: '在重启游戏之前它会继续运行。',
      pendingOnTitle: '下次启动时开启。', pendingOnBody: '重启游戏后它就会启动。',
      alsoStops: list => '以下模组需要它，也不会启动：' + list + '。',
      footerRestart: n => n + ' 个模组将在下次启动时开启或关闭。'
    }
  };

  const state = {
    view: null,
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
    modNote: null
  };

  const LINK_WORDS = { nexus: 'linkNexus', github: 'linkGithub', website: 'linkWebsite' };

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
  const settingId = (mod, s) => mod.guid + '\n' + s.section + '\n' + s.key;
  const mods = () => (state.view ? state.view.mods : []);
  const modByGuid = guid => mods().find(m => m.guid === guid);

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

  function sections(mod) {
    const order = [];

    for (const s of mod.settings) if (order.indexOf(s.section) < 0) order.push(s.section);

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
#slmm .problem { font-family: var(--mono); font-size: .75rem; color: #c98a8a; line-height: 1.5; padding: 4px 18px 6px; white-space: pre-wrap; word-break: break-word; }

#slmm .pane { overflow-y: auto; min-height: 0; position: relative; }
#slmm .pane > * { animation: slmm-pin .3s both; }
@keyframes slmm-pin { from { opacity: 0; transform: translateY(6px); } }
#slmm .hero { position: relative; height: 200px; overflow: hidden; border-bottom: 1px solid var(--line); }
#slmm .gen { position: absolute; inset: 0; display: flex; align-items: center; gap: 30px; padding: 0 44px; background: radial-gradient(ellipse at 20% 50%, var(--hue-a) 0%, transparent 60%), linear-gradient(135deg, rgba(255,255,255,0.03), transparent); }
#slmm .gen .big { flex-shrink: 0; width: 112px; height: 112px; font-size: 2.8rem; display: grid; place-items: center; font-family: var(--display); color: #fff; border: 1px solid rgba(255,255,255,0.18); border-radius: 4px; box-shadow: 0 10px 30px rgba(0,0,0,0.6); }
#slmm .gen .gt { font-family: var(--display); font-size: 3.6rem; color: #fff; letter-spacing: 2px; line-height: 1; text-shadow: 0 4px 12px rgba(0,0,0,0.6); max-width: 820px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
#slmm .gen .gs { font-size: 1.1rem; color: #bbb; margin-top: 10px; }
#slmm .hero-side { position: absolute; right: 30px; top: 22px; z-index: 2; }
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
#slmm .empty { padding: 70px 40px; text-align: center; color: #777; }
#slmm .empty .big { font-family: var(--display); font-size: 2rem; color: #555; letter-spacing: 3px; margin-bottom: 10px; }

#slmm .ft { display: flex; align-items: center; gap: 16px; padding: 0 24px 0 26px; background: rgba(0,0,0,0.35); border-top: 1px solid rgba(255,255,255,0.06); }
#slmm .status-dot { flex-shrink: 0; width: 7px; height: 7px; background: var(--teal); border-radius: 50%; box-shadow: 0 0 5px var(--teal); }
#slmm .status-dot.warn { background: var(--amber); box-shadow: 0 0 5px var(--amber); animation: slmm-blink 2s infinite; }
@keyframes slmm-blink { 0%,100% { opacity: 1; } 50% { opacity: .3; } }
#slmm .ft-t { font-family: var(--mono); font-size: .85rem; color: #999; letter-spacing: .5px; }
#slmm .ft-t b { color: #fff; font-weight: 400; }
#slmm .ft-t em { color: var(--amber); font-style: normal; }
`;

  // ===== The window =====

  const root = document.createElement('div');
  root.id = 'slmm';
  root.innerHTML = `
<div class="stage"><div class="win">
  <span class="corner tl"></span><span class="corner tr"></span><span class="corner bl"></span><span class="corner br"></span>
  <div class="hd">
    <div class="hd-title">MODS</div>
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
      </div>
      <div class="list"></div>
    </div>
    <div class="pane"></div>
  </div>
  <div class="ft"><div class="status-dot"></div><div class="ft-t"></div></div>
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
    syncTyping();
  }

  function renderChrome() {
    const all = mods();
    const openKey = state.view ? state.view.openKey : '';

    root.querySelectorAll('[data-word]').forEach(el => { el.textContent = word(el.dataset.word); });
    root.querySelectorAll('[data-lang]').forEach(el => el.classList.toggle('on', el.dataset.lang === state.lang));
    root.querySelectorAll('[data-filter]').forEach(el => el.classList.toggle('on', el.dataset.filter === state.filter));
    searchBox.placeholder = word('search');
    setCount('loaded', all.filter(m => m.loaded).length);
    setCount('settings', all.reduce((sum, m) => sum + m.settings.length, 0));
    setCount('all', all.length);
    setCount('changed', all.filter(m => m.settings.some(isChanged)).length);
    setCount('openKey', openKey);
    $('[data-part="openKey"]').style.display = openKey && openKey !== 'None' ? '' : 'none';
  }

  function setCount(name, value) {
    $('[data-count="' + name + '"]').textContent = String(value);
  }

  function visibleMods() {
    const q = state.query.toLowerCase();
    let shown = mods();

    if (state.filter === 'changed') shown = shown.filter(m => m.settings.some(isChanged));

    if (q) shown = shown.filter(m => m.name.toLowerCase().indexOf(q) >= 0 || m.guid.toLowerCase().indexOf(q) >= 0 || m.settings.some(s => matchesQuery(m, s, q)));

    return shown;
  }

  function renderList() {
    const shown = visibleMods();
    const groups = [[word('groupLoaded'), shown.filter(m => m.state === 'loaded')], [word('groupNotLoaded'), shown.filter(m => m.state === 'notLoaded')], [word('groupOff'), shown.filter(m => m.state === 'off')]];
    const problems = state.view ? state.view.problems : [];
    let html = '';
    let delay = 0;

    for (const [title, members] of groups) {
      if (!members.length) continue;

      html += '<div class="grp">' + esc(title) + ' &middot; ' + members.length + '</div>';

      for (const m of members) {
        html += modRow(m, Math.min(delay++ * 18, 500));
      }
    }

    if (!html) html = '<div class="empty"><div class="big">' + esc(word('nothing')) + '</div>' + esc(word('noModMatches')) + '</div>';

    if (problems.length) html += '<div class="grp">' + esc(word('problems')) + ' &middot; ' + problems.length + '</div>' + problems.map(p => '<div class="problem">' + esc(p) + '</div>').join('');

    list.innerHTML = html;
  }

  function modRow(m, delay) {
    const changed = m.settings.filter(isChanged).length;
    const sub = m.loaded ? esc(m.version) + ' &middot; ' + esc(m.settings.length ? word('settingCount')(m.settings.length) : word('noSettings')) : esc(word(m.state === 'off' ? 'offSub' : 'notLoadedSub'));
    const isSelected = m.guid === state.selected && !state.query;
    const pips = (changed ? '<span class="pip chg">' + changed + '</span>' : '') +
      (m.state === 'off' ? '<span class="pip offp">' + esc(word('statusOff')) + '</span>' : '') +
      (isPending(m) ? '<span class="pip chg">' + esc(word('pipRestart')) + '</span>' : '');

    return '<div class="mod' + (isSelected ? ' sel' : '') + (m.loaded ? '' : ' off') + '" data-mod="' + esc(m.guid) + '" style="animation-delay:' + delay + 'ms">' +
      '<div class="mono" style="' + monoStyle(m.name) + '">' + esc(initials(m.name)) + '</div>' +
      '<div><div class="mname">' + esc(m.name) + '</div><div class="msub">' + sub + '</div></div>' +
      '<div>' + pips + '</div></div>';
  }

  function renderPane() {
    if (state.query) {
      renderSearch();

      return;
    }

    const m = modByGuid(state.selected);

    if (!m) {
      pane.innerHTML = '';

      return;
    }

    const names = sections(m);

    if (names.indexOf(state.tab) < 0) state.tab = '';

    pane.innerHTML = hero(m) + meta(m) + about(m) + links(m) + tabs(m, names) + modNotices(m) + paneBody(m, names);
  }

  // A mod whose switch now points the other way from how the game started it.
  function isPending(m) {
    return m.switch.kind === 'allowed' && m.switch.next !== (m.state !== 'off');
  }

  function stateWord(m) {
    if (m.state === 'off') return word('statusOff');

    return m.loaded ? word('statusLoaded') : word('statusNotLoaded');
  }

  function hero(m) {
    const sub = m.loaded ? (m.settings.length ? word('found') : word('foundNone')) : '';

    return '<div class="hero"><div class="gen" style="--hue-a:hsla(' + hue(m.name) + ',50%,35%,.45)">' +
      '<div class="big" style="' + monoStyle(m.name) + '">' + esc(initials(m.name)) + '</div>' +
      '<div><div class="gt">' + esc(m.name) + '</div><div class="gs">' + esc(sub) + '</div></div></div>' +
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
      case 'locked':
        return box(word('statusRequired'), word(sw.self ? 'lockedSelf' : 'lockedNeeded'), false, toggle(true, true), sw.neededBy.length ? word('neededBy')(sw.neededBy.join(', ')) : '');
      default:
        return box(stateWord(m), word('cannotMove'), !m.loaded, '', '');
    }
  }

  function meta(m) {
    const file = m.file ? '<span class="id"><b>' + esc(m.file) + '</b></span>' : '';
    const author = m.author ? '<span class="id">' + esc(word('by')) + ' <b>' + esc(m.author) + '</b></span>' : '';

    return '<div class="meta"><span class="id">v<b>' + esc(m.version) + '</b></span>' + author + '<span class="id"><b>' + esc(m.guid) + '</b></span>' + file + '</div>';
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

  function tabs(m, names) {
    if (names.length < 2) return '';

    const tab = (name, label, members) => '<div class="tab' + (state.tab === name ? ' on' : '') + '" data-tab="' + esc(name) + '">' + esc(label) + ' <i>' + members.length + '</i>' + (members.some(isChanged) ? '<span class="cd"></span>' : '') + '</div>';

    return '<div class="tabs">' + tab('', word('tabAll'), m.settings) + names.map(n => tab(n, n, m.settings.filter(s => s.section === n))).join('') + '</div>';
  }

  function paneBody(m, names) {
    if (m.state === 'off') return notice('', word('offTitle'), word('offBody'));

    if (!m.loaded) return notice('', word('notLoadedTitle'), word('notLoadedBody'));

    if (!m.settings.length) return '<div class="empty"><div class="big">' + esc(word('noSettingsTitle')) + '</div>' + esc(word('noSettingsBody')) + '</div>';

    const shown = state.tab ? [state.tab] : names;

    return '<div class="sets">' + shown.map(n => '<div class="sec"><div class="sec-t">' + esc(n) + '</div>' + m.settings.filter(s => s.section === n).map(s => row(m, s, null)).join('') + '</div>').join('') + '</div>';
  }

  function renderSearch() {
    const q = state.query.toLowerCase();
    const pattern = new RegExp('(' + esc(state.query).replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + ')', 'ig');
    const highlight = text => esc(text).replace(pattern, '<mark>$1</mark>');
    let count = 0;
    let html = '';

    for (const m of visibleMods()) {
      const hits = m.settings.filter(s => matchesQuery(m, s, q));

      if (!hits.length) continue;

      count += hits.length;
      html += '<div class="sr-mod" data-mod="' + esc(m.guid) + '"><div class="mono" style="' + monoStyle(m.name) + '">' + esc(initials(m.name)) + '</div>' + esc(m.name) + ' <span>' + esc(word('match')(hits.length)) + '</span></div>' + hits.map(s => row(m, s, highlight)).join('');
    }

    const empty = '<div class="empty"><div class="big">' + esc(word('nothing')) + '</div>' + esc(word('tryAnother')) + '</div>';

    pane.innerHTML = '<div class="sr-head"><div class="big">' + count + '</div><div class="q">' + esc(word('settingsMatch')(state.query)) + '</div></div><div class="sets">' + (html || empty) + '</div>';
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
    const badge = isChangedThisSession(s) ? '<span class="badge">&#x21BB; ' + esc(word('mayRestart')) + '</span>' : '';
    const canReset = !s.readOnly;

    return '<div class="row' + (isChanged(s) ? ' chg' : '') + (id === state.pending ? ' flash' : '') + '" data-row="' + index + '">' +
      '<div><div class="lab">' + show(s.label) + readOnly + '</div>' + lines.join('') + '</div>' +
      '<div>' + badge + '</div>' +
      '<div class="ctl' + (s.readOnly ? ' locked' : '') + '">' + control(m, s, index) + '</div>' +
      '<div class="reset' + (canReset ? ' can' : '') + '" data-act="reset" title="' + esc(word('resetTo')(shown(s, s.default))) + '">&#x21BA;</div></div>';
  }

  function shown(s, value) {
    if (s.kind === 'hotkey' && (value === '' || value === 'None')) return word('none');

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
      default:
        return '<input class="txt" data-input="text" value="' + esc(s.current) + '" spellcheck="false">';
    }
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

  // Every other hotkey setting, in any mod, set to exactly this key.
  function conflicts(m, s) {
    if (!s.current || s.current === 'None') return [];

    const others = [];

    for (const mod of mods()) {
      for (const other of mod.settings) {
        if (other.kind !== 'hotkey' || other.current !== s.current || (mod === m && other === s)) continue;

        others.push(mod === m ? other.label : mod.name + ' (' + other.label + ')');
      }
    }

    return others;
  }

  function renderFooter() {
    const changed = mods().reduce((sum, m) => sum + m.settings.filter(isChangedThisSession).length, 0);
    const restarts = mods().filter(isPending).length;
    const restartText = restarts ? ' <em>' + esc(word('footerRestart')(restarts)) + '</em>' : '';

    $('.status-dot').classList.toggle('warn', changed > 0 || restarts > 0);
    $('.ft-t').innerHTML = (changed ? word('footerSome')(changed) : esc(word('footerNone'))) + restartText;
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

    if (actName === 'close') {
      hide();
      send('close');

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
      render();

      return;
    }

    if (actName === 'showCfg') send('showCfg', [state.selected]);
    else if (actName === 'link') send('link', [state.selected, act.dataset.kind]);
    else if (actName === 'resetMod') resetMod();
    else if (actName === 'reset' && row) resetRow(row);
    else if (actName === 'bind' && row) bind(row, act);
    else pickOption(target, row);
  }

  function select(guid) {
    if (guid !== state.selected) state.modNote = null;

    state.selected = guid;
    state.query = '';
    state.tab = '';
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
      write(row, row.setting.options[Number(item.dataset.option)]);
    } else if (dd) {
      dd.classList.toggle('open');
    }
  }

  function onChange(e) {
    const target = e.target;
    const row = rowOf(target);

    if (target.dataset.switch && state.selected) {
      send('switch', [state.selected, target.checked ? '1' : '0']);

      return;
    }

    if (!row) return;

    if (target.dataset.input === 'toggle') write(row, target.checked ? 'true' : 'false');
    else if (target.dataset.input === 'range' || target.dataset.input === 'number' || target.dataset.input === 'text') write(row, target.value);
  }

  function onInput(e) {
    const target = e.target;

    if (target === searchBox) {
      state.query = searchBox.value.trim();
      render();

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
    if (e.key !== 'Escape' || !isTextField(e.target)) return;

    e.preventDefault();

    if (e.target === searchBox && searchBox.value) {
      searchBox.value = '';
      state.query = '';
      render();
    } else {
      e.target.blur();
    }
  }

  function isTextField(el) {
    return !!el && el.tagName === 'INPUT' && el.type !== 'checkbox' && el.type !== 'range' && root.contains(el);
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
    state.binding = null;
    closeDropdowns(null);

    if (document.activeElement && root.contains(document.activeElement)) document.activeElement.blur();

    syncTyping();
  }

  // ===== Calls from the C# side =====

  function open(view) {
    const saved = savedLanguage();

    state.view = view;
    state.lang = saved === 'zh' || saved === 'en' ? saved : view.zh ? 'zh' : 'en';
    state.notes = {};
    state.pending = null;
    state.binding = null;
    state.modNote = null;

    if (!modByGuid(state.selected)) state.selected = view.mods.length ? (view.mods.find(m => m.loaded) || view.mods[0]).guid : null;

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

  // A switch flip can change more than one mod, so the whole list comes back; the selection and language stay.
  function replace(view, note) {
    if (!state.view) return;

    state.view = view;
    state.modNote = note;
    state.binding = null;

    if (!modByGuid(state.selected)) state.selected = view.mods.length ? view.mods[0].guid : null;

    render();
  }

  function bindEnded() {
    state.binding = null;
    render();
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

    item.querySelector('.menu-text').textContent = isCjk(exitText.textContent) ? WORDS.zh.menuButton : WORDS.en.menuButton.toUpperCase();
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

  window.__modmanager = { open: open, update: update, replace: replace, close: hide, bindEnded: bindEnded };
  sendReady();
})();
