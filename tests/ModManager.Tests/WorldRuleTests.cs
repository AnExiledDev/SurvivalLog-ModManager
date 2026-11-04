using System.Linq;
using System.Text.Json;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>The Toolkit's world rules on the Mods screen: which value is in play, and moving it between this world and every world.</summary>
public class WorldRuleTests
{
    private static readonly ModSwitch Allowed = new ModSwitch.Allowed(true, System.Array.Empty<string>());

    private static ModEntry Loaded(params Setting[] settings) => new("g", "Mod", "1.0", "Mod.dll", new ModState.Loaded(settings), ModInfo.None, Allowed, new UpdateState.NotChecked());

    [Fact]
    public void The_world_rule_tag_is_known_and_not_reported_as_a_mistake()
    {
        var read = SettingTags.Read(new object?[] { "ModManager.WorldRule", "ModManager.Live" });

        Assert.True(read.IsWorldRule);
        Assert.Empty(read.Ignored);
        Assert.False(SettingTags.Read(new object?[] { "ModManager.Live" }).IsWorldRule);
    }

    [Theory]
    [InlineData("keep", true)]
    [InlineData("global", false)]
    public void The_page_asks_to_keep_a_value_for_this_world_or_to_use_global(string scope, bool keep)
    {
        var command = PageCommand.Parse("slmod|modmanager|world|com.x|Restock|Days|" + scope);

        Assert.Equal(new PageCommand.WorldScope(new SettingId("com.x", "Restock", "Days"), keep), command);
    }

    [Fact]
    public void Any_other_scope_is_not_understood()
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|modmanager|world|com.x|Restock|Days|both"));
    }

    [Fact]
    public void A_world_value_and_the_global_value_reach_the_page()
    {
        var found = new FoundSetting("Restock", "Days", "", new SettingKind.Text(), "3", "9", Hints.None) { World = new WorldState(true, "5") };
        var setting = ModList.Arrange("g", new[] { found, new FoundSetting("Restock", "Other", "", new SettingKind.Text(), "1", "1", Hints.None) }, new StartValues());

        using var json = JsonDocument.Parse(PageScript.ModJson(Loaded(setting.ToArray())));
        var settings = json.RootElement.GetProperty("settings");

        Assert.True(settings[0].GetProperty("world").GetProperty("set").GetBoolean());
        Assert.Equal("5", settings[0].GetProperty("world").GetProperty("global").GetString());
        Assert.Equal("9", settings[0].GetProperty("current").GetString());
        Assert.Equal(JsonValueKind.Null, settings[1].GetProperty("world").ValueKind);
    }
}
