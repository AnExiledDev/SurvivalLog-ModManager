using System;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class GameItemsTests
{
    private static ModEntry Mod(ModState state) =>
        new("g", "Mod", "1.0", "Mod.dll", state, ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());

    private static Setting Setting(SettingKind kind) =>
        new(new SettingId("g", "General", "Key"), "Key", new Description("en", "zh"), kind, "0", "0", "0", false);

    [Fact]
    public void The_item_table_is_sent_only_when_a_running_mod_shows_an_item_or_category_control()
    {
        Assert.False(GameItems.AreNeeded(new[] { Mod(new ModState.Loaded(new[] { Setting(new SettingKind.Text()) })), Mod(new ModState.Off()) }));
        Assert.True(GameItems.AreNeeded(new[] { Mod(new ModState.Loaded(new[] { Setting(new SettingKind.Category()) })) }));
        Assert.True(GameItems.AreNeeded(new[] { Mod(new ModState.Loaded(new[] { Setting(new SettingKind.ItemCounts()) })) }));
    }
}
