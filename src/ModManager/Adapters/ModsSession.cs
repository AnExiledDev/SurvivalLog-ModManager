using System;
using System.Diagnostics;
using System.Linq;
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

    /// <summary>Frames after the page gives the keyboard back in which Esc and the open key are ignored, so the Esc that left a text field does not also close the screen.</summary>
    private const int FramesAfterTyping = 2;

    private readonly ModsPage _page;
    private readonly PluginCatalog _catalog;
    private readonly PageMailbox _mailbox;
    private readonly WebKeyboard _keyboard = new();
    private readonly KeyCapture _capture = new();
    private readonly Func<KeyCode> _openKey;
    private readonly Action<string> _log;
    private bool _isOpen;
    private int _typingEndedAtFrame = -100;

    public ModsSession(ModsPage page, PluginCatalog catalog, Func<KeyCode> openKey, Action<string> log)
    {
        _page = page;
        _catalog = catalog;
        _mailbox = PageInbox.Listen(PageCommand.Prefix);
        _openKey = openKey;
        _log = log;
    }

    public void Update(float now, int frame)
    {
        _page.KeepInstalled(now);

        while (_mailbox.TryTake(out var message)) Handle(PageCommand.Parse(message), frame);

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
            case PageCommand.Unknown unknown:
                _log("unknown message from the Mods page: " + unknown.Text);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    private void Open()
    {
        var view = new ModsView(_catalog.Mods(), _catalog.Problems(), GameLanguage.IsChinese(), _openKey().ToString());

        _page.Install();

        if (!_page.Run(PageScript.Open(view))) return;

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
