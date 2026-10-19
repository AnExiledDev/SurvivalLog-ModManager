using System;

namespace ModManager.Core;

/// <summary>
/// A message from the Mods page: "slmod|modmanager|verb|part|...". The page URI-encodes every part, so a "|" inside a
/// section, key or value cannot split it.
/// </summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|modmanager|";

    private PageCommand()
    {
    }

    /// <summary>The page script started (after an install or a page reload).</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The probe found no page script, so it has to be installed again.</summary>
    public sealed record NoScript : PageCommand;

    /// <summary>A MODS button in a game menu was clicked.</summary>
    public sealed record Open : PageCommand;

    public sealed record Close : PageCommand;

    public sealed record Set(SettingId Id, string Value) : PageCommand;

    public sealed record Reset(SettingId Id) : PageCommand;

    public sealed record ResetMod(string Guid) : PageCommand;

    /// <summary>Wait for the next key pressed and give it to this setting.</summary>
    public sealed record Bind(SettingId Id) : PageCommand;

    public sealed record BindCancel : PageCommand;

    /// <summary>A text field took or lost focus, so the keyboard goes to the page or back to the game.</summary>
    public sealed record Focus(bool IsTyping) : PageCommand;

    public sealed record ShowConfigFile(string Guid) : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static PageCommand Parse(string message)
    {
        if (!message.StartsWith(Prefix, StringComparison.Ordinal)) return new Unknown(message);

        var parts = message.Substring(Prefix.Length).Split('|');

        for (var i = 0; i < parts.Length; i++) parts[i] = Uri.UnescapeDataString(parts[i]);

        return Read(parts) ?? new Unknown(message);
    }

    private static PageCommand? Read(string[] parts) => (parts[0], parts.Length) switch
    {
        ("ready", 1) => new Ready(),
        ("noscript", 1) => new NoScript(),
        ("open", 1) => new Open(),
        ("close", 1) => new Close(),
        ("set", 5) => new Set(new SettingId(parts[1], parts[2], parts[3]), parts[4]),
        ("reset", 4) => new Reset(new SettingId(parts[1], parts[2], parts[3])),
        ("resetMod", 2) => new ResetMod(parts[1]),
        ("bind", 4) => new Bind(new SettingId(parts[1], parts[2], parts[3])),
        ("bindCancel", 1) => new BindCancel(),
        ("focus", 2) when parts[1] is "0" or "1" => new Focus(parts[1] == "1"),
        ("showCfg", 2) => new ShowConfigFile(parts[1]),
        _ => null,
    };
}
