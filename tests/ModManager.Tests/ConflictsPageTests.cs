using System.Text.Json;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The clashes Harmony cannot see reach the player: a mod's hotkey on a key the game itself uses, and two mods that
/// told Exiled's Toolkit they change the same table value.
/// </summary>
public class ConflictsPageTests
{
    private static readonly GameKeyBinding Backpack = new("Backpack", "B", "Backpack");

    private static Setting Hotkey(string current) =>
        new(new SettingId("com.a", "Keys", "Open"), "Open", new Description("Opens the window.", "打开窗口。"), new SettingKind.Hotkey(new[] { "B", "F7" }, true), "None", current, current, false);

    private static ModEntry Mod(string guid, params Setting[] settings) =>
        new(guid, guid, "1.0.0", guid + ".dll", new ModState.Loaded(settings), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());

    private static Engine Open(ModsView view) => KnownFactsTests.Open(view);

    private static ModsView View(params ModEntry[] mods) => new(mods, Array.Empty<string>(), false, "F10");

    [Fact]
    public void The_view_carries_the_games_hotkeys()
    {
        using var json = JsonDocument.Parse(PageScript.ViewJson(View() with { GameKeys = new[] { Backpack } }));
        var key = json.RootElement.GetProperty("gameKeys")[0];

        Assert.Equal("Backpack", key.GetProperty("action").GetString());
        Assert.Equal("B", key.GetProperty("key").GetString());
        Assert.Equal("Backpack", key.GetProperty("label").GetString());
    }

    [Fact]
    public void A_mod_hotkey_on_a_key_the_game_uses_says_so()
    {
        var drawn = KnownFactsTests.Drawn(Open(View(Mod("com.a", Hotkey("B"))) with { GameKeys = new[] { Backpack } }));

        Assert.Contains("Also used by the game (Backpack).", drawn);
    }

    [Fact]
    public void Held_keys_do_not_keep_a_mod_hotkey_off_the_games_key()
    {
        var drawn = KnownFactsTests.Drawn(Open(View(Mod("com.a", Hotkey("B + LeftControl"))) with { GameKeys = new[] { Backpack } }));

        Assert.Contains("Also used by the game (Backpack).", drawn);
    }

    [Fact]
    public void A_mod_hotkey_on_a_key_the_game_does_not_use_shows_no_clash()
    {
        var drawn = KnownFactsTests.Drawn(Open(View(Mod("com.a", Hotkey("F7"))) with { GameKeys = new[] { Backpack } }));

        Assert.DoesNotContain("Also used by", drawn);
    }

    [Fact]
    public void A_claim_clash_reaches_the_page_with_its_values()
    {
        var clash = new ClaimClash(ClaimKind.SameValue, PatchRisk.LikelyClash, "Config_Item 1203 price", "50", "80", "com.b");
        var mod = Mod("com.a") with { ClaimClashes = new[] { new ClaimOverlap("com.b", PatchRisk.LikelyClash, new[] { clash }) } };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var sent = json.RootElement.GetProperty("claimClashes")[0];
        var first = sent.GetProperty("clashes")[0];

        Assert.Equal("com.b", sent.GetProperty("guid").GetString());
        Assert.Equal("sameValue", first.GetProperty("kind").GetString());
        Assert.Equal("80", first.GetProperty("theirs").GetString());
        Assert.Equal("com.b", first.GetProperty("lastWord").GetString());
    }

    [Fact]
    public void A_clash_with_no_values_sends_them_as_null()
    {
        var clash = new ClaimClash(ClaimKind.BothHook, PatchRisk.LikelyClash, "PowerManager.CalcSolarOutput");
        var mod = Mod("com.a") with { ClaimClashes = new[] { new ClaimOverlap("com.b", PatchRisk.LikelyClash, new[] { clash }) } };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var first = json.RootElement.GetProperty("claimClashes")[0].GetProperty("clashes")[0];

        Assert.Equal(JsonValueKind.Null, first.GetProperty("mine").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("lastWord").ValueKind);
    }

    [Fact]
    public void Two_mods_writing_one_value_show_on_the_conflicts_tab_with_the_one_the_game_has()
    {
        var clash = new ClaimClash(ClaimKind.SameValue, PatchRisk.LikelyClash, "Config_Item 1203 price", "50", "80", "com.b");
        var engine = Open(View(Mod("com.a") with { ClaimClashes = new[] { new ClaimOverlap("com.b", PatchRisk.LikelyClash, new[] { clash }) } }, Mod("com.b")));

        Assert.Contains("data-side=\"conflicts\"", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: el('DIV', ['tab'], { side: 'conflicts' }, { '[data-side]': { dataset: { side: 'conflicts' } } }) })");

        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("Change the same things", drawn);
        Assert.Contains("Config_Item 1203 price", drawn);
        Assert.Contains("This mod sets it to 50 and com.b sets it to 80. The game has the value from com.b, which wrote it last.", drawn);
    }

    [Theory]
    [InlineData("com.a", "This mod replaces it after com.b added its own entries to it. What com.b added may be gone.")]
    [InlineData("com.b", "com.b replaces it after this mod added its own entries to it. What this mod added may be gone.")]
    public void A_list_replaced_after_a_mod_added_to_it_names_who_replaced_it(string replacer, string said)
    {
        var clash = new ClaimClash(ClaimKind.SetAfterAdded, PatchRisk.LikelyClash, "Config_ShopFurniture 1 ItemIdList", LastWord: replacer);
        var engine = Open(View(Mod("com.a") with { ClaimClashes = new[] { new ClaimOverlap("com.b", PatchRisk.LikelyClash, new[] { clash }) } }, Mod("com.b")));

        engine.Execute("fire('click', { target: el('DIV', ['tab'], { side: 'conflicts' }, { '[data-side]': { dataset: { side: 'conflicts' } } }) })");

        Assert.Contains(said, KnownFactsTests.Drawn(engine));
    }

    [Theory]
    [InlineData("com.a", "com.b sets it and this mod adds its own entries after that. The game keeps both.")]
    [InlineData("com.b", "This mod sets it and com.b adds its own entries after that. The game keeps both.")]
    public void Entries_added_after_a_list_was_set_name_who_added_them(string adder, string said)
    {
        var clash = new ClaimClash(ClaimKind.AddedAfterSet, PatchRisk.Harmless, "Config_ShopFurniture 1 ItemIdList", LastWord: adder);
        var engine = Open(View(Mod("com.a") with { ClaimClashes = new[] { new ClaimOverlap("com.b", PatchRisk.Harmless, new[] { clash }) } }, Mod("com.b")));

        engine.Execute("fire('click', { target: el('DIV', ['tab'], { side: 'conflicts' }, { '[data-side]': { dataset: { side: 'conflicts' } } }) })");

        Assert.Contains(said, KnownFactsTests.Drawn(engine));
    }

    [Fact]
    public void Two_mods_adding_to_one_list_reach_the_page_as_both_add()
    {
        var clash = new ClaimClash(ClaimKind.BothAdd, PatchRisk.Harmless, "Config_ShopFurniture 1 ItemIdList");
        var mod = Mod("com.a") with { ClaimClashes = new[] { new ClaimOverlap("com.b", PatchRisk.Harmless, new[] { clash }) } };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));

        Assert.Equal("bothAdd", json.RootElement.GetProperty("claimClashes")[0].GetProperty("clashes")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void A_value_written_over_by_a_mod_that_reports_nothing_names_no_mod()
    {
        var clash = new ClaimClash(ClaimKind.WrittenOver, PatchRisk.LikelyClash, "Config_Item 1203 price", "50", "120");
        var engine = Open(View(Mod("com.a") with { ClaimClashes = new[] { new ClaimOverlap("", PatchRisk.LikelyClash, new[] { clash }) } }));

        engine.Execute("fire('click', { target: el('DIV', ['tab'], { side: 'conflicts' }, { '[data-side]': { dataset: { side: 'conflicts' } } }) })");

        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("Unknown mod", drawn);
        Assert.Contains("This mod set it to 50, but the game now has 120.", drawn);
    }
}
