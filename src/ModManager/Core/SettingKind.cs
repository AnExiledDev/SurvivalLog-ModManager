using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>Which control a setting gets, and so which values it can take.</summary>
public abstract record SettingKind
{
    private SettingKind()
    {
    }

    public sealed record Toggle : SettingKind;

    /// <summary>A slider when both ends are known, a number box otherwise.</summary>
    public sealed record Number(bool IsWhole, double? Min, double? Max) : SettingKind
    {
        public bool HasRange => Min is { } min && Max is { } max && max > min;
    }

    public sealed record Choice(IReadOnlyList<string> Options) : SettingKind;

    /// <summary>A key; a shortcut also allows held keys before it, written "F5 + LeftControl" as BepInEx does.</summary>
    public sealed record Hotkey(IReadOnlyList<string> KeyNames, bool AllowsModifiers) : SettingKind;

    public sealed record Text : SettingKind;

    /// <summary>One item from the game's item table, by its id; 0 means none. Stored in a whole-number setting.</summary>
    public sealed record Item : SettingKind;

    /// <summary>Several items, written as ids separated by commas ("1203,1405"). Stored in a text setting.</summary>
    public sealed record ItemList : SettingKind;

    /// <summary>Items with an amount each, written "id:count" separated by commas ("1203:5,1405:2"). Stored in a text setting.</summary>
    public sealed record ItemCounts : SettingKind;

    /// <summary>One of the game's item categories, by its number. Stored in a whole-number setting.</summary>
    public sealed record Category : SettingKind;
}

/// <summary>A control a mod asks for by tag, in place of the one its setting's type would get.</summary>
public enum SettingControl
{
    Item,
    ItemList,
    ItemCounts,
    Category,
}

/// <summary>The values a setting's author declared acceptable, read off BepInEx's AcceptableValue classes.</summary>
public abstract record Acceptable
{
    private Acceptable()
    {
    }

    public sealed record Any : Acceptable;

    public sealed record Range(double Min, double Max) : Acceptable;

    public sealed record Values(IReadOnlyList<string> Options) : Acceptable;
}

public static class SettingKinds
{
    private static readonly HashSet<Type> WholeTypes = new() { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ulong), typeof(ushort) };
    private static readonly HashSet<Type> FractionalTypes = new() { typeof(float), typeof(double), typeof(decimal) };

    /// <summary>
    /// Picks the control from the setting's type. Unity's KeyCode and BepInEx's KeyboardShortcut are matched by name so
    /// this needs neither assembly; <paramref name="keyNames"/> are KeyCode's names, which both use.
    /// </summary>
    public static SettingKind Classify(Type type, Acceptable acceptable, IReadOnlyList<string> keyNames)
    {
        if (type == typeof(bool)) return new SettingKind.Toggle();

        if (WholeTypes.Contains(type)) return Number(isWhole: true, acceptable);

        if (FractionalTypes.Contains(type)) return Number(isWhole: false, acceptable);

        if (type.Name == "KeyboardShortcut") return new SettingKind.Hotkey(keyNames, AllowsModifiers: true);

        if (type.IsEnum) return Enum(type, acceptable, keyNames);

        return acceptable is Acceptable.Values values ? new SettingKind.Choice(values.Options) : new SettingKind.Text();
    }

    /// <summary>
    /// The asked-for control when the setting can hold its values: Item and Category need a whole-number setting,
    /// ItemList and ItemCounts a string. Otherwise the setting keeps its own control, with the reason.
    /// </summary>
    public static (SettingKind Kind, string? Problem) WithControl(Type type, SettingKind kind, SettingControl control)
    {
        var needsWhole = control is SettingControl.Item or SettingControl.Category;
        var fits = needsWhole ? kind is SettingKind.Number { IsWhole: true } : type == typeof(string);

        if (!fits) return (kind, "the control " + control + " needs " + (needsWhole ? "a whole-number setting" : "a string setting") + ", so it is not used");

        SettingKind chosen = control switch
        {
            SettingControl.Item => new SettingKind.Item(),
            SettingControl.ItemList => new SettingKind.ItemList(),
            SettingControl.ItemCounts => new SettingKind.ItemCounts(),
            SettingControl.Category => new SettingKind.Category(),
            _ => throw new ArgumentOutOfRangeException(nameof(control), control, null),
        };

        return (chosen, null);
    }

    private static SettingKind Number(bool isWhole, Acceptable acceptable) =>
        acceptable is Acceptable.Range range ? new SettingKind.Number(isWhole, range.Min, range.Max) : new SettingKind.Number(isWhole, null, null);

    /// <summary>A flags enum takes several names at once ("A, B"), which a single choice cannot show, so it stays text.</summary>
    private static SettingKind Enum(Type type, Acceptable acceptable, IReadOnlyList<string> keyNames)
    {
        if (type.IsDefined(typeof(FlagsAttribute), false)) return new SettingKind.Text();

        if (type.Name == "KeyCode") return new SettingKind.Hotkey(keyNames, AllowsModifiers: false);

        var options = acceptable is Acceptable.Values values ? values.Options : System.Enum.GetNames(type).ToArray();

        return new SettingKind.Choice(options);
    }
}
