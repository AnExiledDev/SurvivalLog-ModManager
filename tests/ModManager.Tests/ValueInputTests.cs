using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ValueInputTests
{
    private static readonly string[] Keys = { "None", "F7", "F10", "LeftControl", "LeftShift", "A" };

    private static string Accepted(Normalized result) => Assert.IsType<Normalized.Accepted>(result).Value;

    [Theory]
    [InlineData("true", "true")]
    [InlineData(" TRUE ", "true")]
    [InlineData("False", "false")]
    public void A_toggle_is_written_the_way_BepInEx_writes_it(string raw, string expected)
    {
        Assert.Equal(expected, Accepted(ValueInput.Normalize(new SettingKind.Toggle(), raw)));
    }

    [Fact]
    public void A_toggle_refuses_anything_but_on_or_off()
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.Toggle(), "yes"));
    }

    [Fact]
    public void A_number_above_the_range_is_set_to_the_top_and_says_so()
    {
        var result = Assert.IsType<Normalized.Accepted>(ValueInput.Normalize(new SettingKind.Number(true, 1, 10), "25"));

        Assert.Equal("10", result.Value);
        Assert.True(result.WasClamped);
    }

    [Fact]
    public void A_number_below_the_range_is_set_to_the_bottom()
    {
        var result = Assert.IsType<Normalized.Accepted>(ValueInput.Normalize(new SettingKind.Number(false, 0.5, 2), "0.1"));

        Assert.Equal("0.5", result.Value);
        Assert.True(result.WasClamped);
    }

    [Fact]
    public void A_number_inside_the_range_is_kept_as_typed()
    {
        var result = Assert.IsType<Normalized.Accepted>(ValueInput.Normalize(new SettingKind.Number(false, 0, 2), " 1.25 "));

        Assert.Equal("1.25", result.Value);
        Assert.False(result.WasClamped);
    }

    [Fact]
    public void A_number_without_a_range_is_never_clamped()
    {
        Assert.Equal("100000", Accepted(ValueInput.Normalize(new SettingKind.Number(true, null, null), "100000")));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Text_that_is_not_a_finite_number_is_refused(string raw)
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.Number(false, null, null), raw));
    }

    [Fact]
    public void A_whole_number_setting_refuses_a_fraction()
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.Number(true, 0, 10), "2.5"));
    }

    [Fact]
    public void A_whole_number_is_written_without_a_decimal_point()
    {
        Assert.Equal("3", Accepted(ValueInput.Normalize(new SettingKind.Number(true, null, null), "3.0")));
    }

    [Fact]
    public void A_choice_takes_the_option_spelled_as_the_mod_spells_it()
    {
        Assert.Equal("Medium", Accepted(ValueInput.Normalize(new SettingKind.Choice(new[] { "Low", "Medium" }), "medium")));
    }

    [Fact]
    public void A_choice_refuses_a_value_not_in_its_list()
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.Choice(new[] { "Low", "Medium" }), "High"));
    }

    [Fact]
    public void A_shortcut_is_written_with_the_key_names_in_their_own_case()
    {
        Assert.Equal("F7 + LeftControl", Accepted(ValueInput.Normalize(new SettingKind.Hotkey(Keys, true), "f7+leftcontrol")));
    }

    [Fact]
    public void A_single_key_setting_refuses_a_shortcut()
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.Hotkey(Keys, false), "F7 + LeftControl"));
    }

    [Fact]
    public void A_hotkey_refuses_a_name_that_is_not_a_key()
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.Hotkey(Keys, false), "F99"));
    }

    [Fact]
    public void Text_is_written_exactly_as_typed()
    {
        Assert.Equal("  spaced  ", Accepted(ValueInput.Normalize(new SettingKind.Text(), "  spaced  ")));
    }

    [Theory]
    [InlineData(" 1203 ", "1203")]
    [InlineData("0", "0")]
    [InlineData("007", "7")]
    public void An_item_or_category_is_a_whole_number_of_0_or_more(string raw, string expected)
    {
        Assert.Equal(expected, Accepted(ValueInput.Normalize(new SettingKind.Item(), raw)));
        Assert.Equal(expected, Accepted(ValueInput.Normalize(new SettingKind.Category(), raw)));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("99999999999")]
    public void An_item_that_is_not_a_plain_id_is_refused(string raw)
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.Item(), raw));
    }

    [Theory]
    [InlineData("1203, 1405", "1203,1405")]
    [InlineData(" 5,,5 ,6, ", "5,6")]
    [InlineData("", "")]
    public void An_item_list_is_written_plainly_with_each_item_once(string raw, string expected)
    {
        Assert.Equal(expected, Accepted(ValueInput.Normalize(new SettingKind.ItemList(), raw)));
    }

    [Fact]
    public void An_item_list_with_a_bad_id_is_refused()
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.ItemList(), "1203,apple"));
    }

    [Theory]
    [InlineData("1203:5, 1405 : 2", "1203:5,1405:2")]
    [InlineData("", "")]
    public void Item_amounts_are_written_as_id_colon_count(string raw, string expected)
    {
        Assert.Equal(expected, Accepted(ValueInput.Normalize(new SettingKind.ItemCounts(), raw)));
    }

    [Theory]
    [InlineData("1203")]
    [InlineData("1203:0")]
    [InlineData("1203:-2")]
    [InlineData("1203:5:1")]
    [InlineData("1203:5,1203:2")]
    public void Item_amounts_without_a_count_of_1_or_more_or_with_an_item_twice_are_refused(string raw)
    {
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.ItemCounts(), raw));
    }
}
