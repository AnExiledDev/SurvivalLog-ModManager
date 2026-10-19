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

    /// <summary>Turn a mod on or off from the next game start.</summary>
    public sealed record Switch(string Guid, bool IsOn) : PageCommand;

    /// <summary>Open one of the mod's links. The page names the kind only; the address comes from the mod's own info.</summary>
    public sealed record OpenLink(string Guid, LinkKind Kind) : PageCommand;

    /// <summary>Fetch the notes of the release this mod can update to. The page asks when it first shows the update.</summary>
    public sealed record Notes(string Guid) : PageCommand;

    /// <summary>Fetch the mod's changelog. The page asks when the player first opens its CHANGELOG tab.</summary>
    public sealed record Changelog(string Guid) : PageCommand;

    /// <summary>Open the page of the release this mod can update to. The address comes from GitHub's own redirect.</summary>
    public sealed record OpenUpdate(string Guid) : PageCommand;

    /// <summary>Download the release this mod can update to and put its files in place for the next game start.</summary>
    public sealed record Install(string Guid) : PageCommand;

    /// <summary>Install every update that can be installed now.</summary>
    public sealed record InstallAll : PageCommand;

    /// <summary>Put back the files this mod's last update replaced, for the next game start.</summary>
    public sealed record RollBack(string Guid) : PageCommand;

    /// <summary>Turn on, from the next start, the installed mods this one needs that are off.</summary>
    public sealed record TurnOnNeeds(string Guid) : PageCommand;

    /// <summary>Put the list of installed mods on the clipboard, for a bug report.</summary>
    public sealed record CopyList : PageCommand;

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
        ("switch", 3) when parts[2] is "0" or "1" => new Switch(parts[1], parts[2] == "1"),
        ("link", 3) when LinkKindOf(parts[2]) is { } kind => new OpenLink(parts[1], kind),
        ("notes", 2) => new Notes(parts[1]),
        ("changelog", 2) => new Changelog(parts[1]),
        ("update", 2) => new OpenUpdate(parts[1]),
        ("install", 2) => new Install(parts[1]),
        ("installAll", 1) => new InstallAll(),
        ("rollback", 2) => new RollBack(parts[1]),
        ("needs", 2) => new TurnOnNeeds(parts[1]),
        ("copyList", 1) => new CopyList(),
        _ => null,
    };

    /// <summary>The page's name for each kind of link, shared by both directions so they cannot drift apart.</summary>
    public static string LinkName(LinkKind kind) => kind switch
    {
        LinkKind.Nexus => "nexus",
        LinkKind.GitHub => "github",
        LinkKind.Website => "website",
        LinkKind.Issues => "issues",
        LinkKind.Changelog => "changelog",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static LinkKind? LinkKindOf(string name)
    {
        foreach (LinkKind kind in Enum.GetValues(typeof(LinkKind)))
        {
            if (LinkName(kind) == name) return kind;
        }

        return null;
    }
}
