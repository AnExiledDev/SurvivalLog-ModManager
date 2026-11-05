using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A mod's item lists: the limits and links it gives by tag, what the Mod Manager lets through to the cfg, and how a
/// give-back list finds the recipe it belongs to. A wrong value here is a recipe the mod falls back from, or a
/// setting that reads as changed when it is not.
/// </summary>
public class ItemListTests
{
    private static string Accepted(Normalized result) => Assert.IsType<Normalized.Accepted>(result).Value;

    [Fact]
    public void A_mod_gives_an_item_lists_limits_its_product_and_its_recipe_with_plain_strings()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.Control=ItemCounts", "ModManager.MaxKinds=7", "ModManager.MaxCount= 99", "ModManager.AutoWhenBlank", "ModManager.Makes=64001", "ModManager.ReturnsOf=LargeTurbine.Materials" });

        Assert.Equal(new ItemRules(7, 99, true), read.Rules);
        Assert.Equal(64001, read.Makes);
        Assert.Equal("LargeTurbine.Materials", read.ReturnsOf);
        Assert.Empty(read.Ignored);
    }

    [Theory]
    [InlineData("ModManager.MaxKinds=0")]
    [InlineData("ModManager.MaxKinds=-3")]
    [InlineData("ModManager.MaxCount=lots")]
    [InlineData("ModManager.Makes=")]
    [InlineData("ModManager.ReturnsOf= ")]
    public void A_limit_or_link_that_cannot_be_read_is_named_and_not_used(string tag)
    {
        var read = SettingTags.Read(new object?[] { "ModManager.Control=ItemCounts", tag });

        Assert.Equal(ItemRules.None, read.Rules);
        Assert.Null(read.Makes);
        Assert.Null(read.ReturnsOf);
        Assert.Contains(read.Ignored, reason => reason.StartsWith(tag.Trim(), System.StringComparison.Ordinal));
    }

    [Fact]
    public void Limits_on_a_setting_that_is_no_item_list_are_named_and_dropped()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.Control=Item", "ModManager.MaxKinds=3", "ModManager.Makes=5" });

        Assert.Equal(ItemRules.None, read.Rules);
        Assert.Null(read.Makes);
        Assert.Contains(read.Ignored, reason => reason.Contains("need ModManager.Control=ItemList or ItemCounts"));
    }

    [Fact]
    public void The_limits_go_onto_the_list_the_control_makes()
    {
        var own = SettingKinds.Classify(typeof(string), new Acceptable.Any(), System.Array.Empty<string>());

        var (counts, countsProblem) = SettingKinds.WithControl(typeof(string), own, SettingControl.ItemCounts, new ItemRules(7, 99, true));
        var (list, listProblem) = SettingKinds.WithControl(typeof(string), own, SettingControl.ItemList, new ItemRules(4, 99, false));

        Assert.Equal(new SettingKind.ItemCounts(7, 99, true), counts);
        Assert.Null(countsProblem);
        Assert.Equal(new SettingKind.ItemList(4, false), list);
        Assert.Contains("MaxCount is for ItemCounts", listProblem);
    }

    [Fact]
    public void A_list_with_more_items_than_the_mod_allows_is_refused_with_the_limit()
    {
        var refused = Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.ItemCounts(MaxKinds: 2), "1:1,2:1,3:1"));

        Assert.Contains("2", refused.Reason.English);
        Assert.Equal("1,2", Accepted(ValueInput.Normalize(new SettingKind.ItemList(MaxKinds: 2), "1, 2, 2")));
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.ItemList(MaxKinds: 2), "1,2,3"));
    }

    [Fact]
    public void An_amount_over_the_mods_limit_is_brought_down_to_it_and_says_so()
    {
        var result = Assert.IsType<Normalized.Accepted>(ValueInput.Normalize(new SettingKind.ItemCounts(MaxCount: 99), "20004:150, 20104:3"));

        Assert.Equal("20004:99,20104:3", result.Value);
        Assert.True(result.WasClamped);
    }

    // Blank lets the mod choose, so a list the player empties by hand is kept as "none" rather than going back to the mod's.
    [Theory]
    [InlineData(" None ")]
    [InlineData("none")]
    public void None_is_an_empty_list_only_where_blank_lets_the_mod_choose(string raw)
    {
        Assert.Equal("none", Accepted(ValueInput.Normalize(new SettingKind.ItemCounts(AutoWhenBlank: true), raw)));
        Assert.Equal("none", Accepted(ValueInput.Normalize(new SettingKind.ItemList(AutoWhenBlank: true), raw)));
        Assert.Equal("", Accepted(ValueInput.Normalize(new SettingKind.ItemCounts(AutoWhenBlank: true), "")));
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.ItemCounts(), raw));
    }

    [Fact]
    public void A_list_written_with_spaces_and_the_same_list_saved_plainly_are_the_same_setting()
    {
        var kind = new SettingKind.ItemCounts(7, 99);
        var found = new FoundSetting("LargeTurbine", "Materials", "", kind, "20004:5, 20104:3", "20004:5,20104:3", Hints.None);

        var setting = Assert.Single(ModList.Arrange("g", new[] { found }, new StartValues()));

        Assert.False(setting.IsChanged);
        Assert.Equal("20004:5,20104:3", setting.Default);
    }

    [Fact]
    public void A_value_the_list_cannot_read_is_shown_as_it_is()
    {
        Assert.Equal("scrap", ValueInput.Canonical(new SettingKind.ItemCounts(), "scrap"));
        Assert.Equal("1203, 7", ValueInput.Canonical(new SettingKind.Text(), "1203, 7"));
    }

    // Each turbine has Materials and Returns in its own section, so a plain key finds the one in the same section.
    [Fact]
    public void A_give_back_list_finds_its_recipe_in_its_own_section_first()
    {
        var kind = new SettingKind.ItemCounts();
        var found = new[]
        {
            new FoundSetting("SmallTurbine", "Materials", "", kind, "", "", Hints.None),
            new FoundSetting("LargeTurbine", "Materials", "", kind, "", "", Hints.None),
            new FoundSetting("LargeTurbine", "Returns", "", kind, "", "", Hints.None with { ReturnsOf = "Materials" }) { Answer = new ModAnswer(false, null, "20004:1", null) },
            new FoundSetting("SmallTurbine", "Returns", "", kind, "", "", Hints.None with { ReturnsOf = "SmallTurbine.Materials", Makes = 64001 }),
        };

        var settings = ModList.Arrange("g", found, new StartValues());

        Assert.Equal(new SettingId("g", "LargeTurbine", "Materials"), settings.Single(s => s.Id.Section == "LargeTurbine" && s.Id.Key == "Returns").ReturnsOf);
        Assert.Equal(new SettingId("g", "SmallTurbine", "Materials"), settings.Single(s => s.Id.Section == "SmallTurbine" && s.Id.Key == "Returns").ReturnsOf);
        Assert.Equal("20004:1", settings.Single(s => s.Id.Section == "LargeTurbine" && s.Id.Key == "Returns").Answer.Blank);
        Assert.Equal(64001, settings.Single(s => s.Id.Section == "SmallTurbine" && s.Id.Key == "Returns").Makes);
        Assert.Empty(ModList.LinkProblems(found));
    }

    [Fact]
    public void A_give_back_list_naming_no_setting_has_no_recipe_and_the_log_says_why()
    {
        var found = new[] { new FoundSetting("Turbine", "Returns", "", new SettingKind.ItemCounts(), "", "", Hints.None with { ReturnsOf = "Recipe" }) };

        Assert.Null(Assert.Single(ModList.Arrange("g", found, new StartValues())).ReturnsOf);
        Assert.Contains("names Recipe", Assert.Single(ModList.LinkProblems(found)));
    }

    [Fact]
    public void Each_recipe_counts_once_for_each_item_it_takes()
    {
        var uses = GameItems.RecipeUses(new[] { new[] { 1, 1, 2 }, new[] { 1, 3 }, System.Array.Empty<int>() });

        Assert.Equal(2, uses[1]);
        Assert.Equal(1, uses[2]);
        Assert.Equal(1, uses[3]);
        Assert.False(uses.ContainsKey(4));
    }
}
