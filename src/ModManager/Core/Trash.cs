using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ModManager.Core;

/// <summary>
/// One uninstalled mod in the trash: the files moved there and where each one was, so RESTORE puts them back. Each
/// lives in a folder of its own, named by its <see cref="Id"/>, with this record beside the files.
/// </summary>
public sealed record TrashItem(string Id, string Guid, string Name, string Version, DateTime TrashedUtc, IReadOnlyList<TrashFile> Files)
{
    public const int Schema = 1;

    /// <summary>The record's name in the item's folder; no mod file can have it, as only .dll and .cfg files are moved.</summary>
    public const string FileName = "item.json";

    /// <summary>The mod's DLL, which every item has.</summary>
    public TrashFile Dll => Files.First(file => !file.IsSettings);

    public bool HasSettings => Files.Any(file => file.IsSettings);

    /// <summary>A folder name for a new item: when it was uninstalled, then the GUID, so the folder says what it holds.</summary>
    public static string IdFor(string guid, DateTime utc) => utc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + BackupFolder.Name(guid);

    public string ToJson()
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", Schema);
            json.WriteString("guid", Guid);
            json.WriteString("name", Name);
            json.WriteString("version", Version);
            json.WriteString("trashed", TrashedUtc.ToString("o", CultureInfo.InvariantCulture));
            json.WriteStartArray("files");

            foreach (var file in Files)
            {
                json.WriteStartObject();
                json.WriteString("from", file.From);
                json.WriteString("stored", file.Stored);
                json.WriteBoolean("settings", file.IsSettings);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// The item in the folder named <paramref name="id"/>, or null when its record is of another schema, cannot be read,
    /// or names a file it could not have stored: one that is not a plain file name in its folder, or no DLL at all.
    /// </summary>
    public static TrashItem? FromJson(string id, string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (Json.IntOf(root, "schema") != Schema || Json.StringOf(root, "guid") is not { } guid) return null;

            var files = new List<TrashFile>();

            foreach (var file in Json.ArrayOf(root, "files"))
            {
                if (Json.StringOf(file, "from") is not { } from || Json.StringOf(file, "stored") is not { } stored || !IsPlainName(stored) || !Path.IsPathRooted(from)) return null;

                files.Add(new TrashFile(from, stored, file.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.True));
            }

            if (files.Count(file => !file.IsSettings) != 1 || files.Select(file => file.Stored).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count) return null;

            var trashed = DateTime.TryParse(Json.StringOf(root, "trashed"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when) ? when.ToUniversalTime() : DateTime.MinValue;

            return new TrashItem(id, guid, Json.StringOf(root, "name") ?? guid, Json.StringOf(root, "version") ?? "", trashed, files);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsPlainName(string name) =>
        name != FileName && name.Trim('.').Length > 0 && name.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && Path.GetFileName(name) == name;
}

/// <param name="From">Where the file was, in full, and where RESTORE puts it.</param>
/// <param name="Stored">Its name in the item's folder.</param>
/// <param name="IsSettings">The mod's .cfg, moved only when the player ticked the box.</param>
public sealed record TrashFile(string From, string Stored, bool IsSettings);

/// <summary>What the TRASH page shows of one item.</summary>
/// <param name="When">When it was uninstalled, in the PC's own time, as the page shows it.</param>
/// <param name="IsThisSession">Uninstalled since the game started: its DLL is still running, so EMPTY TRASH leaves it.</param>
public sealed record TrashEntry(string Id, string Name, string Version, string When, bool HasSettings, bool IsThisSession)
{
    public static TrashEntry Of(TrashItem item, bool isThisSession) =>
        new(item.Id, item.Name, item.Version, item.TrashedUtc == DateTime.MinValue ? "" : item.TrashedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), item.HasSettings, isThisSession);
}

/// <summary>Whether a mod has the UninstallPrep setting our mods offer, which makes a save safe to load without the mod.</summary>
public static class UninstallPrep
{
    public const string Section = "Uninstall";
    public const string Key = "UninstallPrep";

    public static bool IsIn(IEnumerable<Setting> settings) => settings.Any(setting => setting.Id.Section == Section && setting.Id.Key == Key);

    /// <summary>For a mod that is not running: whether its .cfg holds the setting, as BepInEx writes it ("[Uninstall]", then "UninstallPrep = false").</summary>
    public static bool IsInConfig(string cfgText)
    {
        var section = "";

        foreach (var raw in cfgText.Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal)) section = line.Substring(1, line.Length - 2).Trim();
            else if (section == Section && line.IndexOf('=') is var at and > 0 && line.Substring(0, at).Trim() == Key) return true;
        }

        return false;
    }
}

/// <summary>What the page says after an uninstall, a restore, or emptying the trash.</summary>
public static class TrashNotes
{
    /// <param name="hasSettingsFile">Whether the mod had a .cfg at all.</param>
    /// <param name="movedSettings">Whether the .cfg went to the trash too.</param>
    public static BilingualText Uninstalled(bool hasSettingsFile, bool movedSettings, IReadOnlyList<string> alsoStops)
    {
        var english = "Uninstalled from the next start: its DLL is in the trash" + (movedSettings ? ", and so is its settings file." : hasSettingsFile ? ". Its settings file is kept." : ".") + " RESTORE puts it back.";
        var chinese = "下次启动时卸载：它的 DLL 已移到回收站" + (movedSettings ? "，它的设置文件也是。" : hasSettingsFile ? "。它的设置文件会保留。" : "。") + "「恢复」可以把它放回去。";

        if (alsoStops.Count == 0) return new BilingualText(english, chinese);

        var names = string.Join(", ", alsoStops);

        return new BilingualText(english + " These need it and will not start either: " + names + ".", chinese + "以下模组需要它，也不会启动：" + names + "。");
    }

    public static BilingualText CannotUninstall() =>
        new("This mod cannot be uninstalled here.", "无法在这里卸载这个模组。");

    public static BilingualText UninstallFailed(string reason) =>
        new("Could not move its files to the trash, so nothing changed: " + reason, "无法把它的文件移到回收站，所以没有任何变化：" + reason);

    /// <param name="isOn">Whether it went back to the plugins folder, rather than a folder for turned-off plugins.</param>
    /// <param name="isThisSession">Uninstalled since the game started, so the list has it already and nothing changes at the next start.</param>
    public static BilingualText Restored(string name, bool isOn, bool isThisSession) => (isOn, isThisSession) switch
    {
        (true, true) => new BilingualText(name + " is back in the plugins folder, as if it had never been uninstalled.", name + " 已放回插件文件夹，就像从未卸载过一样。"),
        (false, true) => new BilingualText(name + " is back, still turned off.", name + " 已放回，仍是关闭状态。"),
        (true, false) => new BilingualText(name + " is back in the plugins folder. It starts at the next game start.", name + " 已放回插件文件夹，下次启动游戏时会运行。"),
        _ => new BilingualText(name + " is back, still turned off. It shows in the list from the next game start.", name + " 已放回，仍是关闭状态。下次启动游戏后会出现在列表里。"),
    };

    public static BilingualText Gone() =>
        new("That mod is no longer in the trash.", "这个模组已经不在回收站里了。");

    public static BilingualText RestoreTaken(string path) =>
        new("Nothing was restored: a file is already at " + path + ". Remove one of the two copies first.", "没有恢复任何文件：" + path + " 已有一个文件。请先删除其中一份。");

    public static BilingualText RestoreFailed(string reason) =>
        new("Could not restore it, so nothing changed: " + reason, "无法恢复它，所以没有任何变化：" + reason);

    /// <param name="kept">Uninstalled this session, so still running and left in the trash.</param>
    /// <param name="failed">Could not be deleted.</param>
    public static BilingualText Emptied(int deleted, int kept, IReadOnlyList<string> failed)
    {
        var english = new StringBuilder("Deleted " + deleted + (deleted == 1 ? " mod" : " mods") + " for good.");
        var chinese = new StringBuilder("已永久删除 " + deleted + " 个模组。");

        if (kept > 0)
        {
            english.Append(' ').Append(kept).Append(kept == 1 ? " mod uninstalled this session stays" : " mods uninstalled this session stay").Append(" until the next start, so you can still restore ").Append(kept == 1 ? "it." : "them.");
            chinese.Append("这次卸载的 ").Append(kept).Append(" 个模组会留到下次启动，所以仍可以恢复。");
        }

        if (failed.Count > 0)
        {
            english.Append(" Could not delete: ").Append(string.Join(", ", failed)).Append('.');
            chinese.Append("无法删除：").Append(string.Join(", ", failed)).Append('。');
        }

        return new BilingualText(english.ToString(), chinese.ToString());
    }
}
