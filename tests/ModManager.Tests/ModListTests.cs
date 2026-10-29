using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ModListTests
{
    private static FoundSetting Found(string section, string key, Hints? hints = null, string current = "1") =>
        new(section, key, "", new SettingKind.Text(), "1", current, hints ?? Hints.None);

    [Fact]
    public void Hidden_settings_are_left_out()
    {
        var found = new[] { Found("A", "Shown"), Found("A", "Secret", Hints.None with { IsHidden = true }) };

        var settings = ModList.Arrange("g", found, new StartValues());

        Assert.Equal(new[] { "Shown" }, settings.Select(s => s.Id.Key));
    }

    [Fact]
    public void Sections_keep_the_order_the_mod_made_them_in()
    {
        var found = new[] { Found("Zeta", "One"), Found("Alpha", "Two"), Found("Zeta", "Three") };

        var settings = ModList.Arrange("g", found, new StartValues());

        Assert.Equal(new[] { "One", "Three", "Two" }, settings.Select(s => s.Id.Key));
    }

    [Fact]
    public void A_higher_order_hint_comes_first_and_ties_keep_the_mods_order()
    {
        var found = new[] { Found("A", "Low"), Found("A", "High", Hints.None with { Order = 5 }), Found("A", "AlsoLow") };

        var settings = ModList.Arrange("g", found, new StartValues());

        Assert.Equal(new[] { "High", "Low", "AlsoLow" }, settings.Select(s => s.Id.Key));
    }

    [Fact]
    public void A_display_name_replaces_the_label_made_from_the_key()
    {
        var settings = ModList.Arrange("g", new[] { Found("A", "OpenKey", Hints.None with { DisplayName = "Open the menu" }), Found("A", "IdleDelay", Hints.None with { DisplayName = "  " }) }, new StartValues());

        Assert.Equal(new[] { "Open the menu", "Idle delay" }, settings.Select(s => s.Label));
    }

    [Fact]
    public void The_Chinese_choice_names_reach_the_setting()
    {
        var names = new System.Collections.Generic.Dictionary<string, string> { ["Custom"] = "\u81ea\u5b9a\u4e49" };
        var setting = Assert.Single(ModList.Arrange("g", new[] { Found("A", "Mode", Hints.None with { ChoiceZh = names }) }, new StartValues()));

        Assert.Equal("\u81ea\u5b9a\u4e49", setting.ChoiceZh["Custom"]);
    }

    [Fact]
    public void A_read_only_hint_makes_the_setting_read_only()
    {
        var setting = Assert.Single(ModList.Arrange("g", new[] { Found("A", "Key", Hints.None with { IsReadOnly = true }) }, new StartValues()));

        Assert.True(setting.IsReadOnly);
    }

    [Fact]
    public void The_value_at_start_is_the_first_one_seen_this_session()
    {
        var startValues = new StartValues();

        ModList.Arrange("g", new[] { Found("A", "Key", current: "first") }, startValues);
        var later = Assert.Single(ModList.Arrange("g", new[] { Found("A", "Key", current: "second") }, startValues));

        Assert.Equal("first", later.AtStart);
        Assert.Equal("second", later.Current);
        Assert.True(later.IsChangedThisSession);
    }

    [Fact]
    public void Start_values_of_different_mods_never_mix()
    {
        var startValues = new StartValues();

        ModList.Arrange("one", new[] { Found("A", "Key", current: "x") }, startValues);
        var other = Assert.Single(ModList.Arrange("two", new[] { Found("A", "Key", current: "y") }, startValues));

        Assert.Equal("y", other.AtStart);
    }

    [Fact]
    public void Mods_sort_by_name_ignoring_case()
    {
        var mods = new[] { Mod("b", "zeta"), Mod("a", "Alpha"), Mod("c", "beta") };

        Assert.Equal(new[] { "Alpha", "beta", "zeta" }, ModList.SortByName(mods).Select(m => m.Name));
    }

    private static readonly UpdateState NotChecked = new UpdateState.NotChecked();

    private static ModEntry Mod(string guid, string name) => new(guid, name, "1.0", guid + ".dll", new ModState.NotLoaded(), ModInfo.None, new ModSwitch.Unavailable(), NotChecked);

    [Fact]
    public void A_setting_that_says_when_it_applies_wins_over_the_mod_and_the_rest_take_the_mods_word()
    {
        var found = new[] { Found("A", "Plain"), Found("A", "Tagged") with { Apply = ApplyMode.Restart } };

        var settings = ModList.Arrange("g", found, new StartValues(), ApplyMode.Live);

        Assert.Equal(ApplyMode.Live, settings[0].Apply);
        Assert.Equal(ApplyMode.Restart, settings[1].Apply);
    }

    [Fact]
    public void A_mod_that_says_nothing_leaves_its_settings_unknown()
    {
        Assert.Equal(ApplyMode.Unknown, Assert.Single(ModList.Arrange("g", new[] { Found("A", "Key") }, new StartValues())).Apply);
    }

    [Fact]
    public void Mods_sort_by_the_name_they_ask_for()
    {
        var mods = new[] { Mod("a", "Alpha"), Mod("z", "zz_internal") with { Info = new ModInfo { Name = new Description("Able", "") } } };

        Assert.Equal(new[] { "z", "a" }, ModList.SortByName(mods).Select(m => m.Guid));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void A_switchable_mod_runs_next_start_when_its_switch_says_so(bool next, bool expected)
    {
        var mod = Mod("a", "A") with { State = new ModState.Off(), Switch = new ModSwitch.Allowed(next, System.Array.Empty<string>()) };

        Assert.Equal(expected, mod.IsOnNextStart);
    }

    [Fact]
    public void A_locked_mod_always_runs_and_an_unmovable_one_keeps_its_state()
    {
        Assert.True((Mod("a", "A") with { State = new ModState.Off(), Switch = new ModSwitch.Locked(false, System.Array.Empty<string>()) }).IsOnNextStart);
        Assert.False((Mod("a", "A") with { State = new ModState.Off() }).IsOnNextStart);
        Assert.True(Mod("a", "A").IsOnNextStart);
    }
}
