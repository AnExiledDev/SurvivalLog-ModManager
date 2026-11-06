using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A recipe list the mod does not check itself is checked against the game's recipes, because the game cannot have two
/// recipes with the same materials and the mod quietly falls back from one that does. Without the check, a player who
/// picks such a list sees the old recipe at the workbench and nothing on the Mods screen says why.
/// </summary>
public class RecipeClashTests
{
    private const int SmallStation = 910101;
    private const int MediumStation = 910201;
    private const int IronParts = 20362;

    private static readonly GameRecipe IronPartsRecipe = new(new[] { IronParts }, new[] { 20004, 20361, 20004 });
    private static readonly GameRecipe SmallStationRow = new(new[] { SmallStation }, new[] { 20102, 20104 });

    private static FoundSetting Recipe(string section, int makes, string value, bool isAuto = false) =>
        new(section, "Materials", "", new SettingKind.ItemCounts(7, 99, isAuto), "", value, Hints.None with { Makes = makes });

    private static Description Name(int id) => new("Item " + id, "物品" + id);

    [Fact]
    public void A_list_with_a_game_recipes_materials_in_any_order_names_what_that_recipe_makes()
    {
        var small = Recipe("SmallStation", SmallStation, "20361:1, 20004:2");

        Assert.Equal(IronParts, RecipeClash.Clash(small, new[] { small }, new[] { SmallStationRow, IronPartsRecipe }));
    }

    [Fact]
    public void The_same_items_in_other_amounts_are_another_recipe()
    {
        var small = Recipe("SmallStation", SmallStation, "20361:1, 20004:3");

        Assert.Null(RecipeClash.Clash(small, new[] { small }, new[] { IronPartsRecipe }));
    }

    [Fact]
    public void The_lists_own_recipe_row_is_no_clash()
    {
        var small = Recipe("SmallStation", SmallStation, "20102:1, 20104:1");

        Assert.Null(RecipeClash.Clash(small, new[] { small }, new[] { SmallStationRow }));
    }

    [Fact]
    public void Two_recipe_lists_of_one_mod_with_the_same_materials_name_each_other_before_the_game_has_both()
    {
        var small = Recipe("SmallStation", SmallStation, "20210:2, 20104:1");
        var medium = Recipe("MediumStation", MediumStation, "20104:1,20210:2");
        var settings = new[] { small, medium };

        Assert.Equal(MediumStation, RecipeClash.Clash(small, settings, new[] { SmallStationRow }));
        Assert.Equal(SmallStation, RecipeClash.Clash(medium, settings, new[] { SmallStationRow }));
    }

    [Fact]
    public void A_list_its_mod_checks_through_ModManagerAnswers_keeps_the_mods_answer()
    {
        var modSaysFine = Recipe("SmallStation", SmallStation, "20004:2, 20361:1") with { Answer = new ModAnswer(true, null, null, null) };

        var checkedSettings = RecipeClash.Check(new[] { modSaysFine }, new[] { IronPartsRecipe }, Name);

        Assert.Null(checkedSettings[0].Answer.Problem);
        Assert.False(RecipeClash.IsNeeded(new[] { modSaysFine }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("none")]
    public void A_blank_list_the_mod_fills_in_is_not_checked(string value)
    {
        var returns = Recipe("SmallStation", SmallStation, value, isAuto: true);

        Assert.False(RecipeClash.IsNeeded(new[] { returns }));
    }

    [Fact]
    public void A_list_that_makes_nothing_is_not_a_recipe_and_is_not_checked()
    {
        var plain = new FoundSetting("Fuel", "Order", "", new SettingKind.ItemCounts(), "", "20004:2, 20361:1", Hints.None);

        Assert.False(RecipeClash.IsNeeded(new[] { plain }));
        Assert.Null(RecipeClash.Clash(plain, new[] { plain }, new[] { IronPartsRecipe }));
    }

    [Fact]
    public void A_clash_shows_under_the_list_in_both_languages_and_leaves_the_other_settings_alone()
    {
        var small = Recipe("SmallStation", SmallStation, "20004:2, 20361:1");
        var medium = Recipe("MediumStation", MediumStation, "20102:1, 20210:1");

        var checkedSettings = RecipeClash.Check(new[] { small, medium }, new[] { IronPartsRecipe }, Name);

        var problem = Assert.IsType<Description>(checkedSettings[0].Answer.Problem);
        Assert.Contains("Item 20362", problem.English);
        Assert.Contains("「物品20362」", problem.Chinese);
        Assert.Same(medium, checkedSettings[1]);
    }

    [Fact]
    public void A_list_is_read_as_pieces_the_way_the_recipe_table_holds_them()
    {
        Assert.Equal(new[] { 20004, 20004, 20004, 20362, 20362 }, RecipeClash.Pieces("20362:2; 20004:3"));
        Assert.Equal(new[] { 20104 }, RecipeClash.Pieces(" 20104 "));
        Assert.Null(RecipeClash.Pieces("20004:x"));
        Assert.Null(RecipeClash.Pieces("wire:2"));
        Assert.Null(RecipeClash.Pieces("20004:0"));
        Assert.Empty(RecipeClash.Pieces("")!.ToArray());
    }
}
