using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ModManager.Core;

/// <summary>What became of a value typed or picked on the page, before it is written to the config.</summary>
public abstract record Normalized
{
    private Normalized()
    {
    }

    /// <summary>The value as BepInEx writes it in the .cfg file.</summary>
    public sealed record Accepted(string Value, bool WasClamped) : Normalized;

    public sealed record Rejected(BilingualText Reason) : Normalized;
}

public sealed record BilingualText(string English, string Chinese);

/// <summary>
/// Turns page input into the text BepInEx expects, or says why it can't. BepInEx only logs a warning for text it
/// cannot read and keeps the old value, so nothing reaches it unchecked.
/// </summary>
public static class ValueInput
{
    public static Normalized Normalize(SettingKind kind, string raw) => kind switch
    {
        SettingKind.Toggle => Toggle(raw.Trim()),
        SettingKind.Number number => Number(number, raw.Trim()),
        SettingKind.Choice choice => Choice(choice, raw.Trim()),
        SettingKind.Hotkey hotkey => Hotkey(hotkey, raw),
        SettingKind.Text => new Normalized.Accepted(raw, WasClamped: false),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static Normalized Toggle(string raw)
    {
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return new Normalized.Accepted("true", false);

        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return new Normalized.Accepted("false", false);

        return Reject("Must be on or off.", "只能是开或关。");
    }

    private static Normalized Number(SettingKind.Number kind, string raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) return Reject("Must be a number.", "必须是数字。");

        if (kind.IsWhole && value != Math.Floor(value)) return Reject("Must be a whole number.", "必须是整数。");

        var clamped = Clamp(value, kind.Min, kind.Max);
        var text = kind.IsWhole ? ((long)clamped).ToString(CultureInfo.InvariantCulture) : clamped.ToString(CultureInfo.InvariantCulture);

        return new Normalized.Accepted(text, clamped != value);
    }

    private static double Clamp(double value, double? min, double? max)
    {
        if (min is { } low && value < low) return low;

        if (max is { } high && value > high) return high;

        return value;
    }

    private static Normalized Choice(SettingKind.Choice kind, string raw)
    {
        var match = kind.Options.FirstOrDefault(option => option.Equals(raw, StringComparison.OrdinalIgnoreCase));

        return match == null ? Reject("Not one of the choices.", "不在可选项中。") : new Normalized.Accepted(match, false);
    }

    private static Normalized Hotkey(SettingKind.Hotkey kind, string raw)
    {
        var parts = raw.Split('+').Select(part => part.Trim()).ToArray();
        var keys = new List<string>();

        if (parts.Length > 1 && !kind.AllowsModifiers) return Reject("Takes a single key.", "只能设置一个按键。");

        foreach (var part in parts)
        {
            var key = kind.KeyNames.FirstOrDefault(name => name.Equals(part, StringComparison.OrdinalIgnoreCase));

            if (key == null) return Reject("Not a key name: " + part, "不是有效的按键名称：" + part);

            keys.Add(key);
        }

        return new Normalized.Accepted(string.Join(" + ", keys), false);
    }

    private static Normalized.Rejected Reject(string english, string chinese) => new(new BilingualText(english, chinese));
}
