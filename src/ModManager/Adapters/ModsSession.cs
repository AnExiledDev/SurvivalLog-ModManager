using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExiledToolkit;
using ExiledToolkit.Adapters;
using ModManager.Core;
using UnityEngine;

namespace ModManager.Adapters;

/// <summary>
/// The Mods screen while the game runs: opens and closes it, carries out what the page asks, and writes settings
/// through each plugin's own ConfigFile so the plugin hears about the change as if its .cfg had been edited.
/// Main thread only.
/// </summary>
internal sealed class ModsSession
{
    private const string Holder = "modmanager";
    private const string UpdatesFilter = "updates";

    /// <summary>More than any real chain of mods needing mods, so a loop in what mods declare cannot run forever.</summary>
    private const int MaxNeeded = 50;

    /// <summary>Frames after the page gives the keyboard back in which Esc and the open key are ignored, so the Esc that left a text field does not also close the screen.</summary>
    private const int FramesAfterTyping = 2;

    /// <summary>Seconds between looks at new log errors, and between redraws of an open screen for them.</summary>
    private const float ErrorLookSeconds = 1f;
    private const float ErrorRedrawSeconds = 2f;

    private readonly ModsPage _page;
    private readonly PluginCatalog _catalog;
    private readonly UpdateBook _updates;
    private readonly UpdateChecks _checks;
    private readonly UpdateInstaller _installer;
    private readonly LogWatch _watch;
    private readonly PageMailbox _mailbox;
    private readonly WebKeyboard _keyboard = new();
    private readonly KeyCapture _capture = new();
    private readonly GameItemTable _items;
    private readonly Func<KeyCode> _openKey;
    private readonly Func<bool> _isCheckingForUpdates;
    private readonly Func<bool> _isShowingUpdateToast;
    private readonly Action<string> _log;
    private bool _isOpen;
    private bool _hasStarted;
    private bool _hasToldUpdates;
    private int _typingEndedAtFrame = -100;
    private float _errorsLookedAt = float.MinValue;
    private float _errorsDrawnAt = float.MinValue;

    /// <summary>The error book's version the open screen last showed.</summary>
    private int _errorsShown;

    /// <summary>The language the item table was sent in to the page that is there now; null when it has not been sent.</summary>
    private bool? _itemsSentIn;

    public ModsSession(ModsPage page, PluginCatalog catalog, UpdateBook updates, UpdateChecks checks, UpdateInstaller installer, LogWatch watch, Func<KeyCode> openKey, Func<bool> isCheckingForUpdates, Func<bool> isShowingUpdateToast, Action<string> log)
    {
        _page = page;
        _catalog = catalog;
        _updates = updates;
        _checks = checks;
        _installer = installer;
        _watch = watch;
        _mailbox = PageInbox.Listen(PageCommand.Prefix);
        _openKey = openKey;
        _isCheckingForUpdates = isCheckingForUpdates;
        _isShowingUpdateToast = isShowingUpdateToast;
        _log = log;
        _items = new GameItemTable(log);
    }

    public void Update(float now, int frame)
    {
        if (!_hasStarted)
        {
            // On the first frame rather than in Load, so plugins that load after this one are in the list.
            _hasStarted = true;

            if (_isCheckingForUpdates()) CheckForUpdates(true);
        }

        _page.KeepInstalled(now);

        while (_mailbox.TryTake(out var message)) Handle(PageCommand.Parse(message), frame);

        var hasChecked = _checks.TakeFinished();
        var hasInstalled = _installer.TakeFinished();

        if (hasChecked) TellUpdatesOnce();

        if ((hasChecked || hasInstalled) && _isOpen) _page.Run(PageScript.Refresh(View()));
        else RedrawForNewErrors(now);

        if (_capture.Target != null)
        {
            Capture();

            return;
        }

        if (_keyboard.IsClaimed || frame <= _typingEndedAtFrame + FramesAfterTyping) return;

        if (Input.GetKeyDown(_openKey())) Toggle();
        else if (_isOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void Handle(PageCommand command, int frame)
    {
        switch (command)
        {
            case PageCommand.Ready:
                Forget();
                _itemsSentIn = null;
                SendPictures();
                break;
            case PageCommand.NoScript:
                Forget();
                _itemsSentIn = null;
                _page.Install();
                break;
            case PageCommand.Open:
                Open();
                break;
            case PageCommand.Close:
                Close();
                break;
            case PageCommand.Set set:
                Write(set.Id, setting => set.Value);
                break;
            case PageCommand.Reset reset:
                Write(reset.Id, setting => setting.Default);
                break;
            case PageCommand.ResetMod resetMod:
                ResetMod(resetMod.Guid);
                break;
            case PageCommand.Bind bind:
                StartCapture(bind.Id);
                break;
            case PageCommand.BindCancel:
                _capture.Stop();
                break;
            case PageCommand.Focus focus:
                Focus(focus.IsTyping, frame);
                break;
            case PageCommand.ShowConfigFile show:
                ShowConfigFile(show.Guid);
                break;
            case PageCommand.Switch flip:
                _page.Run(PageScript.Replace(View(), _catalog.Switch(flip.Guid, flip.IsOn)));
                break;
            case PageCommand.OpenLink link:
                OpenLink(link.Guid, link.Kind);
                break;
            case PageCommand.Notes notes:
                FetchNotes(notes.Guid);
                break;
            case PageCommand.Changelog changelog:
                FetchChangelog(changelog.Guid);
                break;
            case PageCommand.OpenUpdate openUpdate:
                Open(UpdateFor(openUpdate.Guid)?.PageUrl);
                break;
            case PageCommand.Install install:
                StartInstall(install.Guid);
                RefreshOpen();
                break;
            case PageCommand.InstallAll:
                InstallAll();
                break;
            case PageCommand.RollBack rollBack:
                RollBack(rollBack.Guid);
                break;
            case PageCommand.TurnOnNeeds needs:
                TurnOnNeeds(needs.Guid);
                break;
            case PageCommand.CopyList:
                CopyList();
                break;
            case PageCommand.Unknown unknown:
                _log("unknown message from the Mods page: " + unknown.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    /// <summary>Starts or stops the update checks, at the start and whenever the player changes the setting.</summary>
    public void CheckForUpdates(bool isOn)
    {
        if (isOn) _checks.Start(_catalog.Mods().Select(mod => mod.Info.UpdateRepository).OfType<string>());
        else _checks.Stop();

        if (_isOpen) _page.Run(PageScript.Refresh(View()));
    }

    private LatestRelease.Found? UpdateFor(string guid) => _catalog.Mod(guid) is { } mod ? _updates.UpdateFor(mod.Info.UpdateRepository, mod.Version) : null;

    /// <summary>One toast per game session, once every check has an answer, when any mod has an update.</summary>
    private void TellUpdatesOnce()
    {
        if (_hasToldUpdates || !_updates.IsSettled) return;

        _hasToldUpdates = true;

        var count = _catalog.Mods().Count(mod => mod.Update is UpdateState.Available);

        if (count == 0 || !_isShowingUpdateToast()) return;

        var text = UpdateToast.Text(count);
        var isChinese = GameLanguage.IsChinese();

        Toasts.Show(new ToastRequest("Mod Manager", isChinese ? "模组更新" : "Mod updates")
        {
            Body = isChinese ? text.Chinese : text.English,
            OnClick = () => OpenScreen(UpdatesFilter),
            Key = "modmanager-updates",
        });
    }

    private void SendPictures()
    {
        var icons = _catalog.Icons();
        var banners = _catalog.Banners();

        if (icons.Count > 0) _page.Run(PageScript.Icons(icons));

        if (banners.Count > 0) _page.Run(PageScript.Banners(banners));
    }

    /// <summary>Sent with the screen, once per page and language, and only when some mod shows an item or category control.</summary>
    private void SendItems(ModsView view)
    {
        if (_itemsSentIn == view.IsChinese || !GameItems.AreNeeded(view.Mods)) return;

        var (items, categories) = _items.Read(view.IsChinese);

        if (items.Count == 0) return;

        if (_page.Run(PageScript.Items(items, categories))) _itemsSentIn = view.IsChinese;
    }

    private void CopyList()
    {
        try
        {
            GUIUtility.systemCopyBuffer = ModReport.Text(View());
            _page.Run(PageScript.Copied(true));
        }
        catch (Exception ex)
        {
            _log("could not copy the mod list: " + ex.Message);
            _page.Run(PageScript.Copied(false));
        }
    }

    private void StartInstall(string guid)
    {
        var mod = _catalog.Mod(guid);
        var release = UpdateFor(guid);
        var (dllPath, problem) = _catalog.InstallPathOf(guid);

        if (mod == null || release == null || _updates.HasRolledBack(guid)) return;

        if (dllPath == null) _updates.Record(guid, new InstallState.Failed(problem ?? "it cannot be updated here"));
        else _installer.Start(mod, release, dllPath, _catalog.IsStraightInPlugins(dllPath));
    }

    /// <summary>Every update not installed yet, failed ones again; the installs run side by side.</summary>
    private void InstallAll()
    {
        var waiting = _catalog.Mods().Where(mod => mod.Update is UpdateState.Available && mod.Install is InstallState.None or InstallState.Failed && mod.Rollback is not RollbackState.Done).ToList();

        _log("installing all " + waiting.Count + " updates");

        foreach (var mod in waiting) StartInstall(mod.Guid);

        RefreshOpen();
    }

    private void RollBack(string guid)
    {
        var mod = _catalog.Mod(guid);
        var version = mod == null ? null : Rollback.VersionToTry(mod.Rollback);
        var (dllPath, problem) = _catalog.InstallPathOf(guid);

        if (mod == null || version == null) return;

        if (dllPath == null) _updates.Record(guid, new RollbackState.Failed(version, problem ?? "it cannot be rolled back here"));
        else _installer.RollBack(mod, version, dllPath);

        RefreshOpen();
    }

    private void RefreshOpen()
    {
        if (_isOpen) _page.Run(PageScript.Refresh(View()));
    }

    /// <summary>Turns on what the mod needs, then what those need, and says what could not be turned on.</summary>
    private void TurnOnNeeds(string guid)
    {
        var turnedOn = new List<string>();
        var failed = new List<string>();
        var missing = new List<string>();
        var wrongVersion = new List<string>();
        var visited = new HashSet<string> { guid };
        var waiting = new Queue<string>(new[] { guid });

        while (waiting.Count > 0 && visited.Count < MaxNeeded)
        {
            if (_catalog.Mod(waiting.Dequeue()) is not { } mod) continue;

            var needed = RelationResolver.Needed(mod.Relations);

            missing.AddRange(needed.Missing.Where(name => !missing.Contains(name)));
            wrongVersion.AddRange(needed.WrongVersion.Where(name => !wrongVersion.Contains(name)));

            foreach (var other in needed.TurnOn.Where(visited.Add))
            {
                var name = _catalog.Mod(other)?.DisplayName ?? other;

                if (_catalog.Switch(other, true).IsWarning)
                {
                    failed.Add(name);

                    continue;
                }

                turnedOn.Add(name);
                waiting.Enqueue(other);
            }
        }

        var isWarning = failed.Count > 0 || missing.Count > 0 || wrongVersion.Count > 0;

        _page.Run(PageScript.Replace(View(), new ModNote(guid, isWarning, NeedsNotes.TurnedOn(turnedOn, missing, failed, wrongVersion))));
    }

    private void FetchNotes(string guid)
    {
        if (_catalog.Mod(guid) is not { } mod || UpdateFor(guid) is not { } release) return;

        _checks.FetchNotes(release, mod.Version);
        RefreshOpen();
    }

    private void FetchChangelog(string guid)
    {
        if (_catalog.Mod(guid) is not { } mod || Changelogs.TextUri(mod.Info.Link(LinkKind.Changelog)?.Url) is not { } changelog) return;

        _checks.FetchChangelog(changelog);
        RefreshOpen();
    }

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    /// <summary>An open screen shows errors as they are logged, redrawn at most every two seconds through an error storm.</summary>
    private void RedrawForNewErrors(float now)
    {
        if (now - _errorsLookedAt < ErrorLookSeconds) return;

        _errorsLookedAt = now;
        _watch.Collect();

        if (!_isOpen || _watch.Book.Version == _errorsShown || now - _errorsDrawnAt < ErrorRedrawSeconds) return;

        _errorsDrawnAt = now;
        _page.Run(PageScript.Refresh(View()));
    }

    private ModsView View()
    {
        _watch.Collect();
        _errorsShown = _watch.Book.Version;

        var (mods, otherErrors, errorsNotKept) = _catalog.Listing();

        return new(mods, _catalog.Problems(), GameLanguage.IsChinese(), _openKey().ToString())
        {
            GameVersion = _catalog.GameVersion,
            OtherErrors = otherErrors,
            ErrorsNotKept = errorsNotKept,
        };
    }

    private void Open() => OpenScreen(null);

    /// <param name="filter">The list filter to start on; null keeps the one the page had.</param>
    private void OpenScreen(string? filter)
    {
        var view = View();

        _page.Install();
        SendItems(view);

        if (!_page.Run(PageScript.Open(view, filter))) return;

        _isOpen = true;
        HotkeyGate.Hold(Holder);
        ClickRouting.Capture(Holder, true);
    }

    private void Close()
    {
        if (_isOpen) _page.Run(PageScript.Close());

        Forget();
    }

    /// <summary>Lets go of everything the open screen held: after a close, or when a page reload took the screen away.</summary>
    private void Forget()
    {
        _isOpen = false;
        _capture.Stop();
        _keyboard.Release();
        HotkeyGate.Release(Holder);
        ClickRouting.Capture(Holder, false);
    }

    private void Focus(bool isTyping, int frame)
    {
        if (isTyping)
        {
            _keyboard.Claim();

            return;
        }

        _keyboard.Release();
        _typingEndedAtFrame = frame;
    }

    private void StartCapture(SettingId id)
    {
        if (_catalog.Setting(id)?.Kind is SettingKind.Hotkey hotkey) _capture.Start(id, hotkey.AllowsModifiers);
        else _page.Run(PageScript.BindEnded());
    }

    private void Capture()
    {
        switch (_capture.Poll())
        {
            case CaptureStep.Captured captured when _capture.Target is { } id:
                _capture.Stop();
                Write(id, setting => captured.Value);
                break;
            case CaptureStep.Cancelled:
                _capture.Stop();
                _page.Run(PageScript.BindEnded());
                break;
        }
    }

    private void Write(SettingId id, Func<Setting, string> valueFor)
    {
        var setting = _catalog.Setting(id);

        if (setting == null)
        {
            _log("the page asked to change " + id + ", which no loaded mod has");
            _page.Run(PageScript.BindEnded());

            return;
        }

        var note = Apply(setting, valueFor(setting));

        Refresh(id.Guid, note);
    }

    private void ResetMod(string guid)
    {
        if (_catalog.Mod(guid)?.State is not ModState.Loaded loaded) return;

        foreach (var setting in loaded.Settings.Where(setting => setting.IsChanged && !setting.IsReadOnly)) Apply(setting, setting.Default);

        Refresh(guid, null);
    }

    private SettingNote? Apply(Setting setting, string raw)
    {
        switch (WritePlan.For(setting, raw))
        {
            case WritePlan.Refuse refuse:
                return refuse.Note;
            case WritePlan.Write write:
                return ConfigWriter.Write(_catalog, setting, write.Value, _log) ?? write.Note;
            default:
                throw new InvalidOperationException("unknown write plan");
        }
    }

    private void Refresh(string guid, SettingNote? note)
    {
        var mod = _catalog.Mod(guid);

        if (mod != null) _page.Run(PageScript.Update(mod, note));
    }

    /// <summary>The address is taken from the mod's own info, which only holds checked web addresses.</summary>
    private void OpenLink(string guid, LinkKind kind) => Open(_catalog.Link(guid, kind)?.Url);

    /// <summary>Opens a checked web address in the player's browser.</summary>
    private void Open(string? url)
    {
        if (url == null) return;

        try
        {
            Application.OpenURL(url);
            _log("opened " + url);
        }
        catch (Exception ex)
        {
            _log("could not open " + url + ": " + ex.Message);
        }
    }

    /// <summary>Opens the config folder with the mod's file selected; a .cfg has no program to open it on most machines.</summary>
    private void ShowConfigFile(string guid)
    {
        var path = _catalog.ConfigOf(guid)?.ConfigFilePath;

        if (path == null) return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log("could not show " + path + ": " + ex.Message);
        }
    }
}
