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

    private readonly ModsPage _page;
    private readonly PluginCatalog _catalog;
    private readonly UpdateBook _updates;
    private readonly UpdateChecks _checks;
    private readonly UpdateInstaller _installer;
    private readonly PageMailbox _mailbox;
    private readonly WebKeyboard _keyboard = new();
    private readonly KeyCapture _capture = new();
    private readonly Func<KeyCode> _openKey;
    private readonly Func<bool> _isCheckingForUpdates;
    private readonly Func<bool> _isShowingUpdateToast;
    private readonly Action<string> _log;
    private bool _isOpen;
    private bool _hasStarted;
    private bool _hasToldUpdates;
    private int _typingEndedAtFrame = -100;

    public ModsSession(ModsPage page, PluginCatalog catalog, UpdateBook updates, UpdateChecks checks, UpdateInstaller installer, Func<KeyCode> openKey, Func<bool> isCheckingForUpdates, Func<bool> isShowingUpdateToast, Action<string> log)
    {
        _page = page;
        _catalog = catalog;
        _updates = updates;
        _checks = checks;
        _installer = installer;
        _mailbox = PageInbox.Listen(PageCommand.Prefix);
        _openKey = openKey;
        _isCheckingForUpdates = isCheckingForUpdates;
        _isShowingUpdateToast = isShowingUpdateToast;
        _log = log;
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
                SendIcons();
                break;
            case PageCommand.NoScript:
                Forget();
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
            case PageCommand.OpenUpdate openUpdate:
                Open(UpdateFor(openUpdate.Guid)?.PageUrl);
                break;
            case PageCommand.Install install:
                Install(install.Guid);
                break;
            case PageCommand.TurnOnNeeds needs:
                TurnOnNeeds(needs.Guid);
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

    private void SendIcons()
    {
        var icons = _catalog.Icons();

        if (icons.Count > 0) _page.Run(PageScript.Icons(icons));
    }

    private void Install(string guid)
    {
        var mod = _catalog.Mod(guid);
        var release = UpdateFor(guid);
        var (dllPath, problem) = _catalog.InstallPathOf(guid);

        if (mod == null || release == null) return;

        if (dllPath == null) _updates.Record(guid, new InstallState.Failed(problem ?? "it cannot be updated here"));
        else _installer.Start(mod, release, dllPath, _catalog.IsStraightInPlugins(dllPath));

        if (_isOpen) _page.Run(PageScript.Refresh(View()));
    }

    /// <summary>Turns on what the mod needs, then what those need, and says what could not be turned on.</summary>
    private void TurnOnNeeds(string guid)
    {
        var turnedOn = new List<string>();
        var failed = new List<string>();
        var missing = new List<string>();
        var visited = new HashSet<string> { guid };
        var waiting = new Queue<string>(new[] { guid });

        while (waiting.Count > 0 && visited.Count < MaxNeeded)
        {
            if (_catalog.Mod(waiting.Dequeue()) is not { } mod) continue;

            var (turnOn, notInstalled) = RelationResolver.Needed(mod.Relations);

            missing.AddRange(notInstalled.Where(name => !missing.Contains(name)));

            foreach (var needed in turnOn.Where(visited.Add))
            {
                var name = _catalog.Mod(needed)?.DisplayName ?? needed;

                if (_catalog.Switch(needed, true).IsWarning)
                {
                    failed.Add(name);

                    continue;
                }

                turnedOn.Add(name);
                waiting.Enqueue(needed);
            }
        }

        var isWarning = failed.Count > 0 || missing.Count > 0;

        _page.Run(PageScript.Replace(View(), new ModNote(guid, isWarning, NeedsNotes.TurnedOn(turnedOn, missing, failed))));
    }

    private void FetchNotes(string guid)
    {
        if (UpdateFor(guid) is not { } release) return;

        _checks.FetchNotes(release);

        if (_isOpen) _page.Run(PageScript.Refresh(View()));
    }

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    private ModsView View() => new(_catalog.Mods(), _catalog.Problems(), GameLanguage.IsChinese(), _openKey().ToString()) { GameVersion = _catalog.GameVersion };

    private void Open() => OpenScreen(null);

    /// <param name="filter">The list filter to start on; null keeps the one the page had.</param>
    private void OpenScreen(string? filter)
    {
        var view = View();

        _page.Install();

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
