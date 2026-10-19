namespace ModManager.Core;

/// <summary>What to do with a value the player gave a setting.</summary>
public abstract record WritePlan
{
    private WritePlan()
    {
    }

    /// <summary>Write <see cref="Value"/>; the note, when there is one, says how it differs from what was typed.</summary>
    public sealed record Write(string Value, SettingNote? Note) : WritePlan;

    public sealed record Refuse(SettingNote Note) : WritePlan;

    public static WritePlan For(Setting setting, string raw)
    {
        if (setting.IsReadOnly) return Refused(setting, new BilingualText("This setting can't be changed here.", "此设置不能在这里修改。"));

        return ValueInput.Normalize(setting.Kind, raw) switch
        {
            Normalized.Accepted { WasClamped: true } clamped => new Write(clamped.Value, new SettingNote(setting.Id, false, new BilingualText("Out of range, set to " + clamped.Value + ".", "超出范围，已设为 " + clamped.Value + "。"))),
            Normalized.Accepted accepted => new Write(accepted.Value, null),
            Normalized.Rejected rejected => Refused(setting, rejected.Reason),
            _ => throw new System.ArgumentOutOfRangeException(nameof(raw)),
        };
    }

    private static Refuse Refused(Setting setting, BilingualText reason) => new(new SettingNote(setting.Id, true, reason));
}
