using System;
using System.Collections.Generic;
using System.Linq;
using ModManager.Core;
using UnityEngine;

namespace ModManager.Adapters;

/// <summary>
/// Reads the keyboard for a hotkey setting the player clicked. The page does not get key presses unless a text field
/// has focus, so the game's own input is read instead, one frame at a time.
/// </summary>
internal sealed class KeyCapture
{
    /// <summary>KeyCode has several names for some keys; one per key is enough, and ToString picks it.</summary>
    private static readonly (KeyCode Code, string Name)[] Keys = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Distinct().Select(code => (code, code.ToString())).ToArray();

    private static readonly (KeyCode Code, string Name)[] ModifierKeys = Keys.Where(key => KeyChords.Modifiers.Contains(key.Name)).ToArray();

    private SettingId? _target;
    private bool _allowsModifiers;

    public SettingId? Target => _target;

    public void Start(SettingId id, bool allowsModifiers)
    {
        _target = id;
        _allowsModifiers = allowsModifiers;
    }

    public void Stop() => _target = null;

    public CaptureStep Poll()
    {
        if (_target == null) return new CaptureStep.Waiting();

        var pressed = new List<string>();

        foreach (var key in Keys)
        {
            if (Input.GetKeyDown(key.Code)) pressed.Add(key.Name);
        }

        if (pressed.Count == 0) return new CaptureStep.Waiting();

        var held = ModifierKeys.Where(key => Input.GetKey(key.Code)).Select(key => key.Name).ToList();

        return KeyChords.Step(pressed, held, _allowsModifiers);
    }
}
