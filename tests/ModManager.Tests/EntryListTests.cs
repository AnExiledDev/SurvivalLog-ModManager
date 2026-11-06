using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A List setting picks from entries its mod lists itself (buffs, beds, actions), each optionally carrying one of the
/// mod's choices or a number. The tags set it up, the mod's Entries answer fills the picker, and the value is checked
/// before it reaches the cfg.
/// </summary>
public class EntryListTests
{
    private static readonly SettingKind.EntryList Beds = new(Choices: new[] { "Auto", "Sleep", "Nap", "Skip" });
    private static readonly SettingKind.EntryList Multipliers = new(Min: 0.1, Max: 100);

    private static string Accepted(Normalized result) => Assert.IsType<Normalized.Accepted>(result).Value;

    [Fact]
    public void The_tags_give_a_list_whose_entries_each_take_one_of_the_mods_choices()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.Control=List", "ModManager.EntryChoices=Auto| Sleep |Nap|Skip", "ModManager.MaxKinds=8", "ModManager.ChoiceZh=Nap=小睡" });
        var (kind, problem) = SettingKinds.WithControl(typeof(string), new SettingKind.Text(), read.Control!.Value, read.Rules, read.Entry);

        Assert.Empty(read.Ignored);
        Assert.Null(problem);

        var list = Assert.IsType<SettingKind.EntryList>(kind);

        Assert.Equal(new[] { "Auto", "Sleep", "Nap", "Skip" }, list.Choices);
        Assert.Equal(8, list.MaxKinds);
        Assert.False(list.HasNumber);
        Assert.Equal("小睡", read.ChoiceZh["Nap"]);
    }

    [Fact]
    public void A_range_tag_gives_each_entry_a_number_within_it()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.Control=List", "ModManager.EntryRange=0.1|100" });
        var list = Assert.IsType<SettingKind.EntryList>(SettingKinds.WithControl(typeof(string), new SettingKind.Text(), SettingControl.List, read.Rules, read.Entry).Kind);

        Assert.True(list.HasNumber);
        Assert.Equal(0.1, list.Min);
        Assert.Equal(100, list.Max);
    }

    [Theory]
    [InlineData("ModManager.EntryRange=100|0.1")]
    [InlineData("ModManager.EntryRange=a|b")]
    [InlineData("ModManager.EntryChoices=Only")]
    [InlineData("ModManager.EntryChoices=a:b|c")]
    public void A_range_or_choice_tag_that_cannot_be_used_is_named_and_dropped(string tag)
    {
        var read = SettingTags.Read(new object?[] { "ModManager.Control=List", tag });

        Assert.Single(read.Ignored);
        Assert.Equal(EntryRules.None, read.Entry);
    }

    [Fact]
    public void Entry_tags_without_the_List_control_and_recipe_tags_on_it_are_named_and_dropped()
    {
        var onItems = SettingTags.Read(new object?[] { "ModManager.Control=ItemList", "ModManager.EntryChoices=a|b" });
        var onList = SettingTags.Read(new object?[] { "ModManager.Control=List", "ModManager.Makes=64001" });

        Assert.Equal(EntryRules.None, onItems.Entry);
        Assert.Contains(onItems.Ignored, line => line.Contains("need ModManager.Control=List"));
        Assert.Null(onList.Makes);
        Assert.Contains(onList.Ignored, line => line.Contains("not used on List"));
    }

    [Fact]
    public void The_List_control_needs_a_text_setting()
    {
        var (kind, problem) = SettingKinds.WithControl(typeof(int), new SettingKind.Number(true, null, null), SettingControl.List);

        Assert.IsType<SettingKind.Number>(kind);
        Assert.Contains("a string setting", problem);
    }

    [Fact]
    public void Plain_ids_are_kept_once_each_in_order()
    {
        Assert.Equal("i12,f3", Accepted(ValueInput.Normalize(new SettingKind.EntryList(), " i12, f3 ,i12,")));
    }

    [Fact]
    public void A_choice_is_spelled_as_the_mod_spells_it_and_anything_else_is_refused()
    {
        Assert.Equal("9076:Sleep,9080:Skip", Accepted(ValueInput.Normalize(Beds, "9076:sleep, 9080:SKIP")));
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(Beds, "9076:Doze"));
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(Beds, "9076"));
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(Beds, "9076:Sleep,9076:Nap"));
    }

    [Fact]
    public void A_number_past_its_range_is_brought_into_it_and_says_so()
    {
        var result = Assert.IsType<Normalized.Accepted>(ValueInput.Normalize(Multipliers, "12:1.5,40:250,7:0"));

        Assert.Equal("12:1.5,40:100,7:0.1", result.Value);
        Assert.True(result.WasClamped);
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(Multipliers, "12:fast"));
    }

    [Fact]
    public void A_list_the_mod_fills_when_blank_saves_an_emptied_one_as_none_and_a_full_one_is_refused()
    {
        Assert.Equal("none", Accepted(ValueInput.Normalize(new SettingKind.EntryList(AutoWhenBlank: true), "None")));
        Assert.IsType<Normalized.Rejected>(ValueInput.Normalize(new SettingKind.EntryList(MaxKinds: 1), "a,b"));
    }

    [Fact]
    public void The_mods_entries_are_read_one_per_line_with_only_the_id_needed()
    {
        var entries = ListEntries.Parse("i12|Canned Beans|罐头豆|Res/beans.png|Food|食物|Satiety +20|饱腹 +20\r\nf3|Guitar\n\n bad id|x\ni12|again\n7");

        Assert.Equal(new[] { "i12", "f3", "7" }, entries.Select(entry => entry.Id));
        Assert.Equal(new Description("Canned Beans", "罐头豆"), entries[0].Name);
        Assert.Equal("Res/beans.png", entries[0].Icon);
        Assert.Equal(new Description("Food", "食物"), entries[0].Group);
        Assert.Equal(new Description("Satiety +20", "饱腹 +20"), entries[0].Note);
        Assert.Equal(new Description("Guitar", "Guitar"), entries[1].Name);
        Assert.Null(entries[1].Icon);
        Assert.Null(entries[1].Group);
        Assert.Equal(new Description("7", "7"), entries[2].Name);
        Assert.Null(entries[0].Have);
    }

    [Fact]
    public void What_the_player_has_of_an_entry_comes_after_its_note_and_reaches_the_page()
    {
        var entries = ListEntries.Parse("i12|Canned Beans|罐头豆||Food|食物|Satiety +20|饱腹 +20|You have 3|有 3 件\ni13|Rice|米饭||Food|食物|||You have 1");

        Assert.Equal(new Description("You have 3", "有 3 件"), entries[0].Have);
        Assert.Equal(new Description("You have 1", "You have 1"), entries[1].Have);
        Assert.Contains("\"note\":{\"en\":\"Satiety +20\",\"zh\":\"饱腹 +20\"},\"have\":{\"en\":\"You have 3\",\"zh\":\"有 3 件\"}", PageScript.Entries("x", entries));
    }

    [Fact]
    public void The_page_gets_the_list_with_its_choices_and_the_entries_in_their_own_call()
    {
        var setting = new Setting(new SettingId("g", "Actions", "BedModes"), "BedModes", new Description("en", "zh"), Beds, "", "9076:Sleep", "", false)
        {
            ChoiceZh = new System.Collections.Generic.Dictionary<string, string> { ["Nap"] = "小睡" },
        };
        var mod = new ModEntry("g", "Mod", "1.0", "Mod.dll", new ModState.Loaded(new[] { setting }), ModInfo.None, new ModSwitch.Allowed(true, System.Array.Empty<string>()), new UpdateState.NotChecked());
        var open = PageScript.Open(new ModsView(new[] { mod }, System.Array.Empty<string>(), false, "F10"), null);
        var entries = PageScript.Entries(PageScript.EntriesId(setting.Id), ListEntries.Parse("9076|Wooden Bed|木床"));

        Assert.Contains("\"kind\":\"entryList\"", open);
        Assert.Contains("\"choices\":[\"Auto\",\"Sleep\",\"Nap\",\"Skip\"]", open);
        Assert.Contains("\"choicesZh\":[null,null,\"小睡\",null]", open);
        Assert.Contains("\"catalog\":\"g\\nActions\\nBedModes\"", open);
        Assert.DoesNotContain("Wooden Bed", open);
        Assert.Contains("\"id\":\"9076\",\"en\":\"Wooden Bed\",\"zh\":\"木床\",\"icon\":null,\"group\":null,\"note\":null", entries);
    }
}
