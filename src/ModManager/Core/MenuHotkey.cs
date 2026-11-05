namespace ModManager.Core;

/// <summary>
/// The open key as the Mods screen names it. When it fires is the Toolkit's <c>Hotkey</c>: unbound by default, since
/// the Mods button in the Esc menu and on the title screen opens the screen, and silent while the player types.
/// </summary>
public static class MenuHotkey
{
    /// <summary>The key as the screen's footer names it, or nothing when it is unbound.</summary>
    public static string Name(string keyName) => keyName == "None" ? "" : keyName;
}
