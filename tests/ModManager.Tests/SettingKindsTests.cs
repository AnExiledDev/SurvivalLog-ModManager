using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

// Stand-ins for Unity's KeyCode and BepInEx's KeyboardShortcut, which Classify matches by name.
public enum KeyCode
{
    None,
    F7
}

public struct KeyboardShortcut
{
}

public enum Quality
{
    Low,
    High
}

[System.Flags]
public enum Parts
{
    None = 0,
    Head = 1,
    Tail = 2
}

public class SettingKindsTests
{
    private static readonly string[] Keys = { "None", "F7" };
    private static readonly Acceptable Any = new Acceptable.Any();

    [Fact]
    public void A_bool_is_a_toggle()
    {
        Assert.IsType<SettingKind.Toggle>(SettingKinds.Classify(typeof(bool), Any, Keys));
    }

    [Fact]
    public void An_int_with_a_range_is_a_whole_number_slider()
    {
        var kind = Assert.IsType<SettingKind.Number>(SettingKinds.Classify(typeof(int), new Acceptable.Range(1, 9), Keys));

        Assert.True(kind.IsWhole);
        Assert.True(kind.HasRange);
    }

    [Fact]
    public void A_float_without_a_range_is_a_fractional_number_box()
    {
        var kind = Assert.IsType<SettingKind.Number>(SettingKinds.Classify(typeof(float), Any, Keys));

        Assert.False(kind.IsWhole);
        Assert.False(kind.HasRange);
    }

    [Fact]
    public void A_KeyCode_is_a_single_key_and_a_KeyboardShortcut_allows_modifiers()
    {
        Assert.False(Assert.IsType<SettingKind.Hotkey>(SettingKinds.Classify(typeof(KeyCode), Any, Keys)).AllowsModifiers);
        Assert.True(Assert.IsType<SettingKind.Hotkey>(SettingKinds.Classify(typeof(KeyboardShortcut), Any, Keys)).AllowsModifiers);
    }

    [Fact]
    public void An_enum_is_a_choice_of_its_names_unless_the_mod_narrowed_them()
    {
        Assert.Equal(new[] { "Low", "High" }, Assert.IsType<SettingKind.Choice>(SettingKinds.Classify(typeof(Quality), Any, Keys)).Options);
        Assert.Equal(new[] { "High" }, Assert.IsType<SettingKind.Choice>(SettingKinds.Classify(typeof(Quality), new Acceptable.Values(new[] { "High" }), Keys)).Options);
    }

    [Fact]
    public void A_flags_enum_stays_text_since_it_can_hold_several_names()
    {
        Assert.IsType<SettingKind.Text>(SettingKinds.Classify(typeof(Parts), Any, Keys));
    }

    [Fact]
    public void A_string_is_text_unless_it_has_a_list_of_values()
    {
        Assert.IsType<SettingKind.Text>(SettingKinds.Classify(typeof(string), Any, Keys));
        Assert.IsType<SettingKind.Choice>(SettingKinds.Classify(typeof(string), new Acceptable.Values(new[] { "a", "b" }), Keys));
    }
}
