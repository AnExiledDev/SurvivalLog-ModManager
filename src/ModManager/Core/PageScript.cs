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

    /// <param name="filter">A list filter to start on, such as "updates"; null keeps the page's own.</param>
    public static string Open(ModsView view, string? filter = null) => Call("open", ViewJson(view) + (filter == null ? "" : "," + Str(filter)));

    /// <summary>The mods' icons as data URIs, by GUID. Sent apart from the view, once per page, since they do not change while the game runs.</summary>
    public static string Icons(IReadOnlyDictionary<string, string> icons) =>
        Call("icons", "{" + string.Join(",", icons.OrderBy(icon => icon.Key, StringComparer.Ordinal).Select(icon => Str(icon.Key) + ":" + Str(icon.Value))) + "}");

    /// <summary>The mods' banners as data URIs, by GUID, sent like the icons.</summary>
    public static string Banners(IReadOnlyDictionary<string, string> banners) =>
        Call("banners", "{" + string.Join(",", banners.OrderBy(banner => banner.Key, StringComparer.Ordinal).Select(banner => Str(banner.Key) + ":" + Str(banner.Value))) + "}");

    /// <summary>The game's items and item categories for the item pickers, sent once per page and language.</summary>
    public static string Items(IReadOnlyList<CatalogItem> items, IReadOnlyList<CategoryName> categories)
    {
        var itemsJson = List(items, item => "{\"id\":" + item.Id.ToString(CultureInfo.InvariantCulture) + ",\"name\":" + Str(item.Name) + ",\"icon\":" + (item.Icon == null ? "null" : Str(item.Icon)) + ",\"cat\":" + item.Category.ToString(CultureInfo.InvariantCulture) + "}");
        var categoriesJson = List(categories, category => "{\"id\":" + category.Id.ToString(CultureInfo.InvariantCulture) + ",\"name\":" + Str(category.Name) + "}");

        return Call("items", "{\"items\":" + itemsJson + ",\"categories\":" + categoriesJson + "}");
    }

    /// <summary>Says whether the mod list reached the clipboard.</summary>
    public static string Copied(bool isCopied) => Call("copied", Bool(isCopied));

    public static string Update(ModEntry mod, SettingNote? note) => Call("update", ModJson(mod) + "," + (note == null ? "null" : NoteJson(note)));

    /// <summary>Redraws everything while the screen stays open, after a change that touches more than one mod.</summary>
    public static string Replace(ModsView view, ModNote? note) => Call("replace", ViewJson(view) + "," + (note == null ? "null" : ModNoteJson(note)));

    /// <summary>Redraws everything with news from outside the page (an update check finished), leaving the page's note alone.</summary>
    public static string Refresh(ModsView view) => Call("replace", ViewJson(view));

    public static string Close() => Call("close", "");

    /// <summary>Esc while nothing is being typed: a tab another mod added may use it first, else the page asks to close.</summary>
    public static string Escape() => Call("escape", "");

    /// <summary>Ends a key capture the player cancelled or that found nothing to write.</summary>
    public static string BindEnded() => Call("bindEnded", "");

    public static string ViewJson(ModsView view)
    {
        var json = new StringBuilder("{");

        json.Append("\"zh\":").Append(Bool(view.IsChinese));
        json.Append(",\"openKey\":").Append(Str(view.OpenKey));
        json.Append(",\"game\":").Append(view.GameVersion == null ? "null" : Str(view.GameVersion));
        json.Append(",\"mods\":").Append(List(view.Mods, ModJson));
        json.Append(",\"problems\":").Append(List(view.Problems, Str));
        json.Append(",\"otherErrors\":").Append(List(view.OtherErrors, ErrorJson));

        return json.Append('}').ToString();
    }

    public static string ModJson(ModEntry mod)
    {
        var json = new StringBuilder("{");
        var settings = mod.State is ModState.Loaded loaded ? loaded.Settings : Array.Empty<Setting>();

        json.Append("\"guid\":").Append(Str(mod.Guid));
        json.Append(",\"name\":").Append(Str(mod.DisplayName));
        json.Append(",\"nameZh\":").Append(Str(mod.Info.Name?.Chinese ?? mod.DisplayName));
        json.Append(",\"plugin\":").Append(Str(mod.Name));
        json.Append(",\"version\":").Append(Str(mod.Version));
        json.Append(",\"file\":").Append(Str(mod.FileName));
        json.Append(",\"loaded\":").Append(Bool(mod.State is ModState.Loaded));
        json.Append(",\"state\":").Append(Str(StateName(mod.State)));
        json.Append(",\"author\":").Append(mod.Info.Author == null ? "null" : Str(mod.Info.Author));
        json.Append(",\"about\":").Append(mod.Info.Description is { } about ? "{\"en\":" + Str(about.English) + ",\"zh\":" + Str(about.Chinese) + "}" : "null");
        json.Append(",\"links\":").Append(List(mod.Info.Links, link => Str(PageCommand.LinkName(link.Kind))));
        json.Append(",\"category\":").Append(mod.Info.Category is { } category ? Str(category.ToString()) : "null");
        json.Append(",\"library\":").Append(Bool(mod.Info.IsLibrary));
        json.Append(",\"builtFor\":").Append(List(mod.Info.GameVersions, Str));
        json.Append(",\"otherGame\":").Append(Bool(mod.IsForOtherGame));
        json.Append(",\"relations\":").Append(List(mod.Relations, RelationJson));
        json.Append(",\"dependents\":").Append(List(mod.Dependents, RelationJson));
        json.Append(",\"patchOverlaps\":").Append(List(mod.PatchOverlaps, OverlapJson));
        json.Append(",\"errors\":").Append(List(mod.Errors, ErrorJson));
        json.Append(",\"install\":").Append(InstallJson(mod.Install));
        json.Append(",\"rollback\":").Append(RollbackJson(mod.Rollback));
        json.Append(",\"changelog\":").Append(ChangelogJson(mod.Changelog));
        json.Append(",\"switch\":").Append(SwitchJson(mod.Switch));
        json.Append(",\"update\":").Append(UpdateJson(mod.Update));
        json.Append(",\"hotkeys\":").Append(List(mod.Info.Hotkeys, hotkey => "{\"keys\":" + Str(hotkey.Keys) + ",\"en\":" + Str(hotkey.Label.English) + ",\"zh\":" + Str(hotkey.Label.Chinese) + "}"));
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
        json.Append(",\"apply\":").Append(Str(ApplyName(setting.Apply)));
        json.Append(",\"shown\":").Append(Bool(setting.IsShown));
        json.Append(",\"advanced\":").Append(Bool(setting.IsAdvanced));
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
            case SettingKind.Item:
                json.Append(",\"kind\":\"item\"");
                break;
            case SettingKind.ItemList:
                json.Append(",\"kind\":\"itemList\"");
                break;
            case SettingKind.ItemCounts:
                json.Append(",\"kind\":\"itemCounts\"");
                break;
            case SettingKind.Category:
                json.Append(",\"kind\":\"category\"");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static string ApplyName(ApplyMode apply) => apply switch
    {
        ApplyMode.Live => "live",
        ApplyMode.Restart => "restart",
        ApplyMode.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(apply), apply, null),
    };

    private static string OverlapJson(PatchOverlap overlap) =>
        "{\"guid\":" + Str(overlap.Guid) + ",\"risk\":" + Str(RiskName(overlap.Risk)) + ",\"methods\":" + List(overlap.Methods, method =>
            "{\"name\":" + Str(method.Method) + ",\"risk\":" + Str(RiskName(method.Risk)) + ",\"mine\":" + List(method.Mine, effect => Str(EffectName(effect))) + ",\"theirs\":" + List(method.Theirs, effect => Str(EffectName(effect))) + "}") + "}";

    private static string ErrorJson(ModProblem error) =>
        "{\"level\":" + Str(error.Problem.Level == ProblemLevel.Error ? "error" : "warning") +
        ",\"source\":" + Str(error.Problem.Source) +
        ",\"message\":" + Str(error.Problem.Message) +
        ",\"stack\":" + List(error.Problem.Stack, Str) +
        ",\"count\":" + Num(error.Problem.Count) +
        ",\"clashes\":" + List(error.Clashes, clash => "{\"method\":" + Str(clash.Method) + ",\"guids\":" + List(clash.Guids, Str) + "}") + "}";

    private static string RiskName(PatchRisk risk) => risk switch
    {
        PatchRisk.Harmless => "harmless",
        PatchRisk.MayClash => "may",
        PatchRisk.LikelyClash => "likely",
        _ => throw new ArgumentOutOfRangeException(nameof(risk), risk, null),
    };

    private static string EffectName(PatchEffect effect) => effect switch
    {
        PatchEffect.RunsBefore => "before",
        PatchEffect.RunsAfter => "after",
        PatchEffect.CanSkip => "skip",
        PatchEffect.Rewrites => "rewrite",
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
    };

    private static string RelationJson(Relation relation)
    {
        var kind = relation.Kind switch
        {
            RelationKind.Needs => "needs",
            RelationKind.WorksWith => "worksWith",
            RelationKind.DoesNotWorkWith => "doesNotWorkWith",
            _ => throw new ArgumentOutOfRangeException(nameof(relation), relation.Kind, null),
        };
        var status = relation.Status switch
        {
            RelationStatus.Missing => "missing",
            RelationStatus.On => "on",
            RelationStatus.Off => "off",
            RelationStatus.WrongVersion => "wrongVersion",
            _ => throw new ArgumentOutOfRangeException(nameof(relation), relation.Status, null),
        };

        return "{\"kind\":" + Str(kind) + ",\"guid\":" + Str(relation.Guid) + ",\"name\":" + Str(relation.Name) + ",\"status\":" + Str(status) + ",\"canTurnOn\":" + Bool(relation.CanTurnOn) + ",\"range\":" + (relation.Range == null ? "null" : Str(relation.Range)) + "}";
    }

    private static string InstallJson(InstallState install) => install switch
    {
        InstallState.None => "{\"kind\":\"none\"}",
        InstallState.Installing => "{\"kind\":\"installing\"}",
        InstallState.Installed installed => "{\"kind\":\"installed\",\"tag\":" + Str(installed.Tag) + "}",
        InstallState.Failed failed => "{\"kind\":\"failed\",\"reason\":" + Str(failed.Reason) + "}",
        _ => throw new ArgumentOutOfRangeException(nameof(install), install, null),
    };

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
        ModSwitch.Held held => "{\"kind\":\"held\",\"next\":" + Bool(held.IsOnNextStart) + "}",
        ModSwitch.Unavailable => "{\"kind\":\"none\"}",
        _ => throw new ArgumentOutOfRangeException(nameof(modSwitch), modSwitch, null),
    };

    private static string UpdateJson(UpdateState update) => update switch
    {
        UpdateState.NotChecked => "{\"kind\":\"none\"}",
        UpdateState.Checking => "{\"kind\":\"checking\"}",
        UpdateState.UpToDate upToDate => "{\"kind\":\"current\",\"tag\":" + Str(upToDate.Tag) + "}",
        UpdateState.Available available => "{\"kind\":\"available\",\"tag\":" + Str(available.Tag) + ",\"notes\":" + NotesJson(available.Notes) + "}",
        UpdateState.NoReleases => "{\"kind\":\"noReleases\"}",
        UpdateState.Failed failed => "{\"kind\":\"failed\",\"reason\":" + Str(failed.Reason) + "}",
        UpdateState.Unreadable unreadable => "{\"kind\":\"unreadable\",\"tag\":" + Str(unreadable.Tag) + "}",
        _ => throw new ArgumentOutOfRangeException(nameof(update), update, null),
    };

    private static string NotesJson(NotesState notes) => notes switch
    {
        NotesState.NotAsked => "{\"kind\":\"notAsked\"}",
        NotesState.Loading => "{\"kind\":\"loading\"}",
        NotesState.Ready ready => "{\"kind\":\"releases\",\"releases\":" + List(ready.Releases, note => "{\"tag\":" + Str(note.Tag) + ",\"text\":" + Str(note.Text) + "}") + "}",
        NotesState.Empty => "{\"kind\":\"empty\"}",
        NotesState.Busy => "{\"kind\":\"busy\"}",
        NotesState.Failed failed => "{\"kind\":\"failed\",\"reason\":" + Str(failed.Reason) + "}",
        _ => throw new ArgumentOutOfRangeException(nameof(notes), notes, null),
    };

    private static string ChangelogJson(ChangelogState changelog) => changelog switch
    {
        ChangelogState.None => "{\"kind\":\"none\"}",
        ChangelogState.NotAsked => "{\"kind\":\"notAsked\"}",
        ChangelogState.Loading => "{\"kind\":\"loading\"}",
        ChangelogState.Ready ready => "{\"kind\":\"ready\",\"text\":" + Str(ready.Text) + "}",
        ChangelogState.Failed failed => "{\"kind\":\"failed\",\"reason\":" + Str(failed.Reason) + "}",
        _ => throw new ArgumentOutOfRangeException(nameof(changelog), changelog, null),
    };

    private static string RollbackJson(RollbackState rollback) => rollback switch
    {
        RollbackState.None => "{\"kind\":\"none\"}",
        RollbackState.Available available => "{\"kind\":\"available\",\"version\":" + Str(available.Version) + "}",
        RollbackState.Done done => "{\"kind\":\"done\",\"version\":" + Str(done.Version) + "}",
        RollbackState.Failed failed => "{\"kind\":\"failed\",\"version\":" + Str(failed.Version) + ",\"reason\":" + Str(failed.Reason) + "}",
        _ => throw new ArgumentOutOfRangeException(nameof(rollback), rollback, null),
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
