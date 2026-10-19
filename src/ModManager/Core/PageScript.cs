using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ModManager.Core;

/// <summary>A note shown under one setting after a write: why a value was changed or refused.</summary>
public sealed record SettingNote(SettingId Id, bool IsWarning, BilingualText Text);

/// <summary>A note shown on a mod's page after the player flipped its switch: what happens next, or why it could not be done.</summary>
public sealed record ModNote(string Guid, bool IsWarning, BilingualText Text);

/// <summary>The script calls sent to the Mods page, with their data written as JSON (which is also valid JavaScript).</summary>
public static class PageScript
{
    private const string Api = "window.__modmanager";

    /// <summary>Asks the page whether the script is still there; a page reload drops it.</summary>
    public const string Probe = "if(!" + Api + ")window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    public static string Open(ModsView view) => Call("open", ViewJson(view));

    public static string Update(ModEntry mod, SettingNote? note) => Call("update", ModJson(mod) + "," + (note == null ? "null" : NoteJson(note)));

    /// <summary>Redraws everything while the screen stays open, after a change that touches more than one mod.</summary>
    public static string Replace(ModsView view, ModNote? note) => Call("replace", ViewJson(view) + "," + (note == null ? "null" : ModNoteJson(note)));

    public static string Close() => Call("close", "");

    /// <summary>Ends a key capture the player cancelled or that found nothing to write.</summary>
    public static string BindEnded() => Call("bindEnded", "");

    public static string ViewJson(ModsView view)
    {
        var json = new StringBuilder("{");

        json.Append("\"zh\":").Append(Bool(view.IsChinese));
        json.Append(",\"openKey\":").Append(Str(view.OpenKey));
        json.Append(",\"mods\":").Append(List(view.Mods, ModJson));
        json.Append(",\"problems\":").Append(List(view.Problems, Str));

        return json.Append('}').ToString();
    }

    public static string ModJson(ModEntry mod)
    {
        var json = new StringBuilder("{");
        var settings = mod.State is ModState.Loaded loaded ? loaded.Settings : Array.Empty<Setting>();

        json.Append("\"guid\":").Append(Str(mod.Guid));
        json.Append(",\"name\":").Append(Str(mod.Name));
        json.Append(",\"version\":").Append(Str(mod.Version));
        json.Append(",\"file\":").Append(Str(mod.FileName));
        json.Append(",\"loaded\":").Append(Bool(mod.State is ModState.Loaded));
        json.Append(",\"state\":").Append(Str(StateName(mod.State)));
        json.Append(",\"author\":").Append(mod.Info.Author == null ? "null" : Str(mod.Info.Author));
        json.Append(",\"about\":").Append(mod.Info.Description is { } about ? "{\"en\":" + Str(about.English) + ",\"zh\":" + Str(about.Chinese) + "}" : "null");
        json.Append(",\"links\":").Append(List(mod.Info.Links, link => Str(PageCommand.LinkName(link.Kind))));
        json.Append(",\"switch\":").Append(SwitchJson(mod.Switch));
        json.Append(",\"settings\":").Append(List(settings, SettingJson));

        return json.Append('}').ToString();
    }

    private static string SettingJson(Setting setting)
    {
        var json = new StringBuilder("{");

        json.Append("\"section\":").Append(Str(setting.Id.Section));
        json.Append(",\"key\":").Append(Str(setting.Id.Key));
        json.Append(",\"label\":").Append(Str(setting.Label));
        json.Append(",\"en\":").Append(Str(setting.Description.English));
        json.Append(",\"zh\":").Append(Str(setting.Description.Chinese));
        json.Append(",\"default\":").Append(Str(setting.Default));
        json.Append(",\"current\":").Append(Str(setting.Current));
        json.Append(",\"atStart\":").Append(Str(setting.AtStart));
        json.Append(",\"readOnly\":").Append(Bool(setting.IsReadOnly));
        AppendKind(json, setting.Kind);

        return json.Append('}').ToString();
    }

    private static void AppendKind(StringBuilder json, SettingKind kind)
    {
        switch (kind)
        {
            case SettingKind.Toggle:
                json.Append(",\"kind\":\"toggle\"");
                break;
            case SettingKind.Number number:
                json.Append(",\"kind\":\"number\",\"whole\":").Append(Bool(number.IsWhole));
                json.Append(",\"min\":").Append(number.HasRange && number.Min is { } min ? Num(min) : "null");
                json.Append(",\"max\":").Append(number.HasRange && number.Max is { } max ? Num(max) : "null");
                break;
            case SettingKind.Choice choice:
                json.Append(",\"kind\":\"choice\",\"options\":").Append(List(choice.Options, Str));
                break;
            case SettingKind.Hotkey hotkey:
                json.Append(",\"kind\":\"hotkey\",\"modifiers\":").Append(Bool(hotkey.AllowsModifiers));
                break;
            case SettingKind.Text:
                json.Append(",\"kind\":\"text\"");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static string StateName(ModState state) => state switch
    {
        ModState.Loaded => "loaded",
        ModState.NotLoaded => "notLoaded",
        ModState.Off => "off",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static string SwitchJson(ModSwitch modSwitch) => modSwitch switch
    {
        ModSwitch.Allowed allowed => "{\"kind\":\"allowed\",\"next\":" + Bool(allowed.IsOnNextStart) + ",\"stops\":" + List(allowed.AlsoStops, Str) + "}",
        ModSwitch.Locked locked => "{\"kind\":\"locked\",\"self\":" + Bool(locked.IsModManager) + ",\"neededBy\":" + List(locked.NeededBy, Str) + "}",
        ModSwitch.Unavailable => "{\"kind\":\"none\"}",
        _ => throw new ArgumentOutOfRangeException(nameof(modSwitch), modSwitch, null),
    };

    private static string ModNoteJson(ModNote note) =>
        "{\"guid\":" + Str(note.Guid) + ",\"warn\":" + Bool(note.IsWarning) + ",\"en\":" + Str(note.Text.English) + ",\"zh\":" + Str(note.Text.Chinese) + "}";

    private static string NoteJson(SettingNote note) =>
        "{\"section\":" + Str(note.Id.Section) + ",\"key\":" + Str(note.Id.Key) + ",\"warn\":" + Bool(note.IsWarning) + ",\"en\":" + Str(note.Text.English) + ",\"zh\":" + Str(note.Text.Chinese) + "}";

    private static string Call(string method, string arguments) => Api + "&&" + Api + "." + method + "(" + arguments + ")";

    private static string List<T>(IEnumerable<T> items, Func<T, string> write) => "[" + string.Join(",", items.Select(write)) + "]";

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A JSON string that is also safe inside a script: line and paragraph separators are escaped, as JavaScript once read them as line ends.</summary>
    public static string Str(string value)
    {
        var json = new StringBuilder("\"", value.Length + 2);

        foreach (var c in value)
        {
            switch (c)
            {
                case '"': json.Append("\\\""); break;
                case '\\': json.Append("\\\\"); break;
                case '\n': json.Append("\\n"); break;
                case '\r': json.Append("\\r"); break;
                case '\t': json.Append("\\t"); break;
                case '<': json.Append("\\u003c"); break;
                case '\u2028': json.Append("\\u2028"); break;
                case '\u2029': json.Append("\\u2029"); break;
                default:
                    if (c < ' ') json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else json.Append(c);
                    break;
            }
        }

        return json.Append('"').ToString();
    }
}
