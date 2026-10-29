using System.Collections.Generic;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class SettingTagsTests
{
    private static FoundSetting Found(string section, string key, string current, params ShowRule[] rules) =>
        new(section, key, "", new SettingKind.Text(), "", current, Hints.None with { ShowWhen = rules });

    private static ShowRule When(string target, params string[] values) => new(target, values.Length == 0 ? new[] { "true" } : values);

    [Fact]
    public void A_mod_asks_for_a_control_and_marks_a_setting_advanced_with_plain_strings()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.Control=itemcounts", "ModManager.Advanced", 42, null });

        Assert.Equal(SettingControl.ItemCounts, read.Control);
        Assert.True(read.IsAdvanced);
        Assert.Empty(read.Ignored);
    }

    [Fact]
    public void A_bare_key_means_that_toggle_is_on_and_key_equals_lists_the_values()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.ShowWhen=Enabled", "ModManager.ShowWhen=General.Mode = Fast | Faster ; Other=x" });

        Assert.Equal(new[] { "Enabled", "General.Mode", "Other" }, read.ShowWhen.Select(rule => rule.Target));
        Assert.Equal(new[] { "true" }, read.ShowWhen[0].Values);
        Assert.Equal(new[] { "Fast", "Faster" }, read.ShowWhen[1].Values);
        Assert.Empty(read.Ignored);
    }

    [Fact]
    public void A_mod_names_its_choices_in_Chinese_one_tag_each()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.ChoiceZh=Default=\u9ed8\u8ba4", "ModManager.ChoiceZh= Hard limit = \u786c\u4e0a\u9650" });

        Assert.Equal("\u9ed8\u8ba4", read.ChoiceZh["Default"]);
        Assert.Equal("\u786c\u4e0a\u9650", read.ChoiceZh["hard limit"]);
        Assert.Empty(read.Ignored);
    }

    [Theory]
    [InlineData("ModManager.ChoiceZh=Default")]
    [InlineData("ModManager.ChoiceZh==\u9ed8\u8ba4")]
    [InlineData("ModManager.ChoiceZh=Default=")]
    public void A_choice_name_without_both_parts_is_reported(string tag)
    {
        var read = SettingTags.Read(new object?[] { tag });

        Assert.Empty(read.ChoiceZh);
        Assert.Single(read.Ignored);
    }

    [Theory]
    [InlineData("ModManager.Control=Colour")]
    [InlineData("ModManager.ShowWhen=")]
    [InlineData("ModManager.ShowWhen=Mode=")]
    [InlineData("ModManager.Advance")]
    public void A_tag_the_Mod_Manager_cannot_use_is_reported_so_the_author_can_fix_it(string tag)
    {
        var read = SettingTags.Read(new object?[] { tag });

        Assert.Null(read.Control);
        Assert.Empty(read.ShowWhen);
        Assert.False(read.IsAdvanced);
        Assert.Single(read.Ignored);
    }

    [Fact]
    public void The_apply_tags_and_other_mods_tags_are_not_reported()
    {
        var read = SettingTags.Read(new object?[] { ApplyModes.LiveTag, ApplyModes.RestartTag, "OtherMod.Thing" });

        Assert.Empty(read.Ignored);
    }

    [Fact]
    public void A_setting_shows_only_while_its_toggle_is_on()
    {
        var on = new[] { Found("A", "Enabled", "true"), Found("A", "Speed", "1", When("Enabled")) };
        var off = new[] { Found("A", "Enabled", "False"), Found("A", "Speed", "1", When("Enabled")) };

        Assert.Equal(new[] { true, true }, ShowRules.Shown(on));
        Assert.Equal(new[] { true, false }, ShowRules.Shown(off));
    }

    [Fact]
    public void A_value_rule_matches_any_listed_value_without_case()
    {
        var settings = new[] { Found("A", "Mode", "FAST"), Found("A", "Fast", "", When("Mode", "fast", "faster")), Found("A", "Slow", "", When("Mode", "slow")) };

        Assert.Equal(new[] { true, true, false }, ShowRules.Shown(settings));
    }

    [Fact]
    public void Every_rule_must_hold()
    {
        var settings = new[] { Found("A", "On", "true"), Found("A", "Mode", "slow"), Found("A", "X", "", When("On"), When("Mode", "fast")) };

        Assert.False(ShowRules.Shown(settings)[2]);
    }

    [Fact]
    public void A_setting_folds_away_with_the_setting_its_rule_looks_at()
    {
        var settings = new[] { Found("A", "Master", "false"), Found("A", "Mode", "fast", When("Master")), Found("A", "Speed", "", When("Mode", "fast")) };

        Assert.Equal(new[] { true, false, false }, ShowRules.Shown(settings));
    }

    [Fact]
    public void A_plain_key_is_found_in_the_settings_own_section_first_then_section_dot_key_anywhere()
    {
        var settings = new[]
        {
            Found("Other", "Enabled", "true"),
            Found("Mine", "Enabled", "false"),
            Found("Mine", "A", "", When("Enabled")),
            Found("Mine", "B", "", When("Other.Enabled")),
            Found("Third", "C", "", When("Enabled")),
        };

        Assert.Equal(new[] { true, true, false, true, true }, ShowRules.Shown(settings));
    }

    [Fact]
    public void A_rule_naming_no_setting_never_hides_and_is_reported()
    {
        var settings = new[] { Found("A", "Speed", "1", When("Enabld")) };

        Assert.True(ShowRules.Shown(settings)[0]);
        Assert.Contains(ShowRules.Problems(settings), problem => problem.Contains("Enabld"));
    }

    [Fact]
    public void Rules_that_loop_never_hide_for_good_and_are_reported()
    {
        var settings = new[] { Found("A", "X", "true", When("Y")), Found("A", "Y", "true", When("X")) };

        Assert.Equal(new[] { true, true }, ShowRules.Shown(settings));
        Assert.Contains(ShowRules.Problems(settings), problem => problem.Contains("loop"));
    }

    [Fact]
    public void A_hidden_setting_still_drives_the_rules_and_a_folded_setting_is_still_listed_for_reset()
    {
        var found = new List<FoundSetting>
        {
            new("A", "Unlocked", "", new SettingKind.Toggle(), "false", "false", new Hints(IsHidden: true, null, 0, false)),
            Found("A", "Extra", "1", When("Unlocked")),
        };

        var settings = ModList.Arrange("g", found, new StartValues());

        var extra = Assert.Single(settings);
        Assert.Equal("Extra", extra.Id.Key);
        Assert.False(extra.IsShown);
    }

    [Theory]
    [InlineData(typeof(int), SettingControl.Item, typeof(SettingKind.Item))]
    [InlineData(typeof(long), SettingControl.Category, typeof(SettingKind.Category))]
    [InlineData(typeof(string), SettingControl.ItemList, typeof(SettingKind.ItemList))]
    [InlineData(typeof(string), SettingControl.ItemCounts, typeof(SettingKind.ItemCounts))]
    public void A_control_replaces_the_types_own_when_the_setting_can_hold_its_values(System.Type type, SettingControl control, System.Type expected)
    {
        var own = SettingKinds.Classify(type, new Acceptable.Any(), System.Array.Empty<string>());

        var (kind, problem) = SettingKinds.WithControl(type, own, control);

        Assert.IsType(expected, kind);
        Assert.Null(problem);
    }

    [Theory]
    [InlineData(typeof(float), SettingControl.Item)]
    [InlineData(typeof(string), SettingControl.Category)]
    [InlineData(typeof(int), SettingControl.ItemList)]
    [InlineData(typeof(System.DayOfWeek), SettingControl.ItemCounts)]
    public void A_control_the_setting_cannot_hold_is_refused_and_the_setting_keeps_its_own(System.Type type, SettingControl control)
    {
        var own = SettingKinds.Classify(type, new Acceptable.Any(), System.Array.Empty<string>());

        var (kind, problem) = SettingKinds.WithControl(type, own, control);

        Assert.Equal(own, kind);
        Assert.NotNull(problem);
    }
}
