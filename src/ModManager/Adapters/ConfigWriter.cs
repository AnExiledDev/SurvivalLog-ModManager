using System;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>Writes one value through the plugin's ConfigFile, so the plugin's own change handlers run and the .cfg is saved.</summary>
internal static class ConfigWriter
{
    private static readonly BilingualText NotSaved = new("Could not save this value. The log has the reason.", "无法保存此值，原因见日志。");

    /// <summary>Null when the value was written; otherwise a warning to show under the setting.</summary>
    public static SettingNote? Write(PluginCatalog catalog, Setting setting, string value, Action<string> log)
    {
        var entry = catalog.EntryOf(setting.Id);
        var config = catalog.ConfigOf(setting.Id.Guid);

        if (entry == null || config == null) return Failed(setting, "the setting is gone", log);

        var before = entry.GetSerializedValue();

        try
        {
            entry.SetSerializedValue(value);

            if (!config.SaveOnConfigSet) config.Save();
        }
        catch (Exception ex)
        {
            return Failed(setting, ex.Message, log);
        }

        // BepInEx keeps the old value and only logs a warning when it cannot read the text.
        if (value != before && entry.GetSerializedValue() == before) return Failed(setting, "BepInEx did not take the value \"" + value + "\"", log);

        log("set " + setting.Id + " from " + before + " to " + entry.GetSerializedValue());

        return null;
    }

    private static SettingNote Failed(SettingId id, string reason, Action<string> log)
    {
        log("could not set " + id + ": " + reason);

        return new SettingNote(id, true, NotSaved);
    }

    private static SettingNote Failed(Setting setting, string reason, Action<string> log) => Failed(setting.Id, reason, log);
}
