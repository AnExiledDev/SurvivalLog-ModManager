using System;

namespace ModManager.Core;

/// <summary>
/// When the open key fires. The Mods button in the Esc menu and on the title screen opens the Mods screen, so the key
/// is unbound by default, and a bound key does nothing while the player types in a text box.
/// </summary>
public static class MenuHotkey
{
    /// <summary>The Unity key name OpenKey starts with: none, so no key is taken from the game or other mods.</summary>
    public const string DefaultKey = "None";

    /// <summary>
    /// Whether this frame's press fires the key. The press and the typing check are only asked for when they can change
    /// the answer: an unbound key asks nothing, and typing is only checked on a press.
    /// </summary>
    public static bool Fires(bool isBound, Func<bool> isPressed, Func<bool> isTyping) => isBound && isPressed() && !isTyping();

    /// <summary>The key as the screen's footer names it, or nothing when it is unbound.</summary>
    public static string Name(string keyName) => keyName == "None" ? "" : keyName;
}
