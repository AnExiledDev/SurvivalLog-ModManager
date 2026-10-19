using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class PageCommandTests
{
    [Fact]
    public void A_set_carries_the_setting_and_the_value()
    {
        var command = PageCommand.Parse("slmod|modmanager|set|com.x|General|OpenKey|F7");

        Assert.Equal(new PageCommand.Set(new SettingId("com.x", "General", "OpenKey"), "F7"), command);
    }

    [Fact]
    public void Encoded_bars_inside_a_part_do_not_split_it()
    {
        var command = PageCommand.Parse("slmod|modmanager|set|com.x|A%7CB|Key|one%7Ctwo%20three");

        Assert.Equal(new PageCommand.Set(new SettingId("com.x", "A|B", "Key"), "one|two three"), command);
    }

    [Theory]
    [InlineData("slmod|modmanager|focus|1", true)]
    [InlineData("slmod|modmanager|focus|0", false)]
    public void Focus_says_whether_a_field_is_being_typed_in(string message, bool isTyping)
    {
        Assert.Equal(new PageCommand.Focus(isTyping), PageCommand.Parse(message));
    }

    [Theory]
    [InlineData("slmod|modmanager|ready")]
    [InlineData("slmod|modmanager|open")]
    [InlineData("slmod|modmanager|close")]
    [InlineData("slmod|modmanager|noscript")]
    [InlineData("slmod|modmanager|bindCancel")]
    [InlineData("slmod|modmanager|copyList")]
    public void Commands_without_parts_are_read(string message)
    {
        Assert.IsNotType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    [Fact]
    public void Reset_bind_resetMod_and_showCfg_name_what_they_act_on()
    {
        var id = new SettingId("g", "S", "K");

        Assert.Equal(new PageCommand.Reset(id), PageCommand.Parse("slmod|modmanager|reset|g|S|K"));
        Assert.Equal(new PageCommand.Bind(id), PageCommand.Parse("slmod|modmanager|bind|g|S|K"));
        Assert.Equal(new PageCommand.ResetMod("g"), PageCommand.Parse("slmod|modmanager|resetMod|g"));
        Assert.Equal(new PageCommand.ShowConfigFile("g"), PageCommand.Parse("slmod|modmanager|showCfg|g"));
    }

    [Theory]
    [InlineData("slmod|modmanager|switch|g|1", true)]
    [InlineData("slmod|modmanager|switch|g|0", false)]
    public void A_switch_names_the_mod_and_where_it_should_be_next_start(string message, bool isOn)
    {
        Assert.Equal(new PageCommand.Switch("g", isOn), PageCommand.Parse(message));
    }

    [Theory]
    [InlineData("nexus", LinkKind.Nexus)]
    [InlineData("github", LinkKind.GitHub)]
    [InlineData("website", LinkKind.Website)]
    [InlineData("issues", LinkKind.Issues)]
    [InlineData("changelog", LinkKind.Changelog)]
    public void A_link_names_the_mod_and_which_of_its_links(string kind, LinkKind expected)
    {
        Assert.Equal(new PageCommand.OpenLink("g", expected), PageCommand.Parse("slmod|modmanager|link|g|" + kind));
        Assert.Equal(kind, PageCommand.LinkName(expected));
    }

    [Fact]
    public void Notes_and_update_name_the_mod_only()
    {
        Assert.Equal(new PageCommand.Notes("g"), PageCommand.Parse("slmod|modmanager|notes|g"));
        Assert.Equal(new PageCommand.OpenUpdate("g"), PageCommand.Parse("slmod|modmanager|update|g"));
        Assert.Equal(new PageCommand.Changelog("g"), PageCommand.Parse("slmod|modmanager|changelog|g"));
    }

    [Theory]
    [InlineData("slmod|modmanager|update|g|https%3A%2F%2Fevil.example")]
    [InlineData("slmod|modmanager|notes")]
    [InlineData("slmod|modmanager|changelog")]
    [InlineData("slmod|modmanager|changelog|g|https%3A%2F%2Fevil.example")]
    [InlineData("slmod|othermod|open")]
    [InlineData("slmod|modmanager|switch|g|yes")]
    [InlineData("slmod|modmanager|link|g|https%3A%2F%2Fevil.example")]
    [InlineData("slmod|modmanager|link|g|Nexus")]
    [InlineData("slmod|modmanager|set|g|S|K")]
    [InlineData("slmod|modmanager|focus|yes")]
    [InlineData("slmod|modmanager|explode")]
    [InlineData("slmod|modmanager|open|extra")]
    public void Anything_else_is_unknown(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    [Fact]
    public void Install_and_turn_on_needs_name_the_mod()
    {
        Assert.Equal(new PageCommand.Install("com.x"), PageCommand.Parse("slmod|modmanager|install|com.x"));
        Assert.Equal(new PageCommand.TurnOnNeeds("com.x"), PageCommand.Parse("slmod|modmanager|needs|com.x"));
        Assert.Equal(new PageCommand.RollBack("com.x"), PageCommand.Parse("slmod|modmanager|rollback|com.x"));
        Assert.IsType<PageCommand.InstallAll>(PageCommand.Parse("slmod|modmanager|installAll"));
    }

    [Theory]
    [InlineData("slmod|modmanager|install")]
    [InlineData("slmod|modmanager|install|g|https%3A%2F%2Fevil.example")]
    [InlineData("slmod|modmanager|needs")]
    [InlineData("slmod|modmanager|rollback")]
    [InlineData("slmod|modmanager|rollback|g|1.0")]
    [InlineData("slmod|modmanager|installAll|g")]
    public void Install_or_needs_without_exactly_one_mod_is_unknown(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }
}
