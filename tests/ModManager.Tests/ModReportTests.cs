using System;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ModReportTests
{
    private static ModEntry Mod(string guid, string name, ModState state, ModSwitch modSwitch) =>
        new(guid, name, "1.2.0", name + ".dll", state, ModInfo.None, modSwitch, new UpdateState.NotChecked());

    private static readonly ModState Running = new ModState.Loaded(Array.Empty<Setting>());

    [Fact]
    public void The_report_names_the_game_each_mod_and_the_load_problems()
    {
        var view = new ModsView(
            new[]
            {
                Mod("com.a.storage", "Storage Hub", Running, new ModSwitch.Allowed(true, Array.Empty<string>())),
                Mod("com.a.broken", "Broken", new ModState.NotLoaded(), new ModSwitch.Unavailable()),
                Mod("com.a.sleepy", "Sleepy", new ModState.Off(), new ModSwitch.Allowed(false, Array.Empty<string>())),
            },
            new[] { "Broken needs\nsomething" },
            IsChinese: true,
            OpenKey: "F10") { GameVersion = "1.1.18902" };

        var text = ModReport.Text(view);

        Assert.Equal(
            "Survival Log 1.1.18902\n" +
            "Mods (3):\n" +
            "- Storage Hub 1.2.0 (com.a.storage, Storage Hub.dll): running\n" +
            "- Broken 1.2.0 (com.a.broken, Broken.dll): failed to load\n" +
            "- Sleepy 1.2.0 (com.a.sleepy, Sleepy.dll): off\n" +
            "Load problems:\n" +
            "- Broken needs something\n",
            text);
    }

    [Fact]
    public void A_mod_switched_this_session_says_what_happens_at_the_next_start()
    {
        var view = new ModsView(
            new[]
            {
                Mod("com.a.one", "One", Running, new ModSwitch.Allowed(false, Array.Empty<string>())),
                Mod("com.a.two", "Two", new ModState.Off(), new ModSwitch.Allowed(true, Array.Empty<string>())),
            },
            Array.Empty<string>(),
            false,
            "F10");

        var text = ModReport.Text(view);

        Assert.Contains("One 1.2.0 (com.a.one, One.dll): running, off from the next start", text);
        Assert.Contains("Two 1.2.0 (com.a.two, Two.dll): off, on from the next start", text);
        Assert.StartsWith("Survival Log (version unknown)\n", text);
        Assert.DoesNotContain("Load problems", text);
    }

    private static readonly ModSwitch On = new ModSwitch.Allowed(true, Array.Empty<string>());

    private static ModsView ViewOf(params ModEntry[] mods) => new(mods, Array.Empty<string>(), false, "F10");

    private static SharedMethod Shared(string method, PatchRisk risk, ClashReason reason = ClashReason.None, string? lastWord = null) =>
        new(method, risk, new[] { PatchEffect.SetsResult }, new[] { PatchEffect.SetsResult }, reason, lastWord);

    [Fact]
    public void A_patch_conflict_is_listed_once_for_the_pair_with_why_and_harmless_overlaps_are_left_out()
    {
        var getWeight = Shared("Player.GetWeight(Int32)", PatchRisk.LikelyClash, ClashReason.BothSetResult, "com.b.bags");
        var useItem = Shared("Item.Use", PatchRisk.MayClash, ClashReason.Skips);
        var tick = Shared("World.Tick", PatchRisk.Harmless);
        var rat = Mod("com.a.rat", "Pack Rat", Running, On) with
        {
            PatchOverlaps = new[]
            {
                new PatchOverlap("com.b.bags", PatchRisk.LikelyClash, new[] { getWeight, useItem, tick }),
                new PatchOverlap("com.c.clock", PatchRisk.Harmless, new[] { tick }),
            },
        };
        var bags = Mod("com.b.bags", "Bags", Running, On) with { PatchOverlaps = new[] { new PatchOverlap("com.a.rat", PatchRisk.LikelyClash, new[] { getWeight, useItem, tick }) } };
        var clock = Mod("com.c.clock", "Clock", Running, On) with { PatchOverlaps = new[] { new PatchOverlap("com.a.rat", PatchRisk.Harmless, new[] { tick }) } };

        var text = ModReport.Text(ViewOf(rat, bags, clock));

        Assert.Contains(
            "Conflicts:\n" +
            "- Pack Rat and Bags patch the same methods and are likely to clash: Player.GetWeight (both set the result, and Bags runs last), Item.Use (one can skip it)\n",
            text);
        Assert.DoesNotContain("Bags and Pack Rat", text);
        Assert.DoesNotContain("World.Tick", text);
        Assert.DoesNotContain("Clock and", text);
    }

    [Fact]
    public void Reported_changes_that_clash_say_what_both_mods_did()
    {
        var sameValue = new ClaimClash(ClaimKind.SameValue, PatchRisk.LikelyClash, "Config_Item 910001 price", "5", "9", "com.b.shop");
        var sameRow = new ClaimClash(ClaimKind.SameRow, PatchRisk.LikelyClash, "Config_Item 910002");
        var sameAnyway = new ClaimClash(ClaimKind.SameValue, PatchRisk.Harmless, "Config_Item 910003 weight", "1", "1");
        var writtenOver = new ClaimClash(ClaimKind.WrittenOver, PatchRisk.LikelyClash, "Config_Item 910004 price", "5", "7");
        var wind = Mod("com.a.wind", "Wind Power", Running, On) with
        {
            ClaimClashes = new[]
            {
                new ClaimOverlap("com.b.shop", PatchRisk.LikelyClash, new[] { sameValue, sameRow, sameAnyway }),
                new ClaimOverlap("", PatchRisk.LikelyClash, new[] { writtenOver }),
            },
        };
        var shop = Mod("com.b.shop", "Shop", Running, On) with { ClaimClashes = new[] { new ClaimOverlap("com.a.wind", PatchRisk.LikelyClash, new[] { sameValue with { Mine = "9", Theirs = "5" }, sameRow, sameAnyway }) } };

        var text = ModReport.Text(ViewOf(wind, shop));

        Assert.Contains("- Wind Power and Shop are likely to clash: both set Config_Item 910001 price (Wind Power to 5, Shop to 9, and Shop wrote last), both add the row Config_Item 910002\n", text);
        Assert.Contains("- Wind Power: Config_Item 910004 price is 7 now, not the 5 it set, and no mod that reports its changes wrote that\n", text);
        Assert.DoesNotContain("Shop and Wind Power", text);
        Assert.DoesNotContain("910003", text);
    }

    [Fact]
    public void A_list_replaced_after_another_mod_added_to_it_names_both_mods()
    {
        var replaced = new ClaimClash(ClaimKind.SetAfterAdded, PatchRisk.LikelyClash, "Config_ShopFurniture 1 ItemIdList", LastWord: "com.b.shop");
        var bothAdd = new ClaimClash(ClaimKind.BothAdd, PatchRisk.Harmless, "Config_ShopFurniture 14 ItemIdList");
        var wind = Mod("com.a.wind", "Wind Power", Running, On) with { ClaimClashes = new[] { new ClaimOverlap("com.b.shop", PatchRisk.LikelyClash, new[] { replaced, bothAdd }) } };
        var shop = Mod("com.b.shop", "Shop", Running, On) with { ClaimClashes = new[] { new ClaimOverlap("com.a.wind", PatchRisk.LikelyClash, new[] { replaced, bothAdd }) } };

        var text = ModReport.Text(ViewOf(wind, shop));

        Assert.Contains("- Wind Power and Shop are likely to clash: Shop replaces Config_ShopFurniture 1 ItemIdList after Wind Power added to it, which can drop what Wind Power added\n", text);
        Assert.DoesNotContain("14 ItemIdList", text);
    }

    [Fact]
    public void No_conflicts_heading_when_nothing_can_clash()
    {
        var text = ModReport.Text(ViewOf(Mod("com.a.one", "One", Running, On)));

        Assert.DoesNotContain("Conflicts", text);
        Assert.DoesNotContain("Game data", text);
        Assert.DoesNotContain("Game code", text);
        Assert.DoesNotContain("last session", text);
    }

    [Fact]
    public void Game_data_and_code_changes_no_mod_reported_name_the_mods_that_most_likely_made_them()
    {
        var items = new TableChanges("Config_Item", 2, 0, 1, Array.Empty<TableChange>(), new[] { "com.a.rat", "com.z.gone" });
        var recipes = new TableChanges("Config_Recipe", 0, 1, 0, Array.Empty<TableChange>(), Array.Empty<string>());
        var code = new CodeReading(1000, 5, 2, 1, new[]
        {
            new UnreportedCode(0x1000, 4, 2, 1) { Method = "Player.Hurt" },
            new UnreportedCode(0x2a00, 0, 6, 1),
        });
        var view = ViewOf(Mod("com.a.rat", "Pack Rat", Running, On)) with
        {
            DataCheck = new DataCheck.Done(40, 9000, 3, new[] { items, recipes }),
            CodeCheck = new CodeCheck.Done(code, new[] { "com.a.rat" }),
        };

        var text = ModReport.Text(view);

        Assert.Contains(
            "Game data changes no mod reported:\n" +
            "- Config_Item: 1 value changed, 2 rows added; mods whose code writes this table: Pack Rat, com.z.gone\n" +
            "- Config_Recipe: 1 row removed; no running mod's code writes this table\n" +
            "Game code changed outside Harmony that no mod reported, in 2 methods: Player.Hurt, the method at 0x2a00\n" +
            "Mods whose code can change it: Pack Rat.\n",
            text);
    }

    [Fact]
    public void Many_tables_and_methods_are_cut_short_with_a_count()
    {
        var tables = Enumerable.Range(1, 12).Select(i => new TableChanges("T" + i, 1, 0, 0, Array.Empty<TableChange>(), Array.Empty<string>())).ToList();
        var methods = Enumerable.Range(1, 13).Select(i => new UnreportedCode((uint)i, 0, 1, 1) { Method = "M" + i }).ToList();
        var view = ViewOf() with
        {
            DataCheck = new DataCheck.Done(40, 9000, 0, tables),
            CodeCheck = new CodeCheck.Done(new CodeReading(1000, 13, 0, 0, methods), Array.Empty<string>()),
        };

        var text = ModReport.Text(view);

        Assert.Contains("- T10: 1 row added", text);
        Assert.DoesNotContain("- T11:", text);
        Assert.Contains("- and 2 more tables\n", text);
        Assert.Contains("in 13 methods: M1, M2, M3, M4, M5, M6, M7, M8, M9, M10, and 3 more\n", text);
        Assert.Contains("No running mod's code changes it.\n", text);
    }

    [Fact]
    public void Checks_that_found_nothing_or_have_not_run_add_nothing()
    {
        var view = ViewOf(Mod("com.a.one", "One", Running, On)) with
        {
            DataCheck = new DataCheck.Done(40, 9000, 3, Array.Empty<TableChanges>()),
            CodeCheck = new CodeCheck.NoScan(NoScanReason.Unreadable),
        };

        var text = ModReport.Text(view);

        Assert.DoesNotContain("Game data", text);
        Assert.DoesNotContain("Game code", text);
    }

    [Fact]
    public void A_crash_last_session_names_the_mods_behind_its_newest_errors()
    {
        var view = ViewOf(Mod("com.a.rat", "Pack Rat", Running, On)) with { LastCrash = new[] { "com.a.rat" } };

        Assert.Contains("The last session did not close normally. The newest errors before it came from Pack Rat.\n", ModReport.Text(view));
        Assert.Contains("The last session did not close normally, and no mod logged an error before it.\n", ModReport.Text(view with { LastCrash = Array.Empty<string>() }));
    }

    [Fact]
    public void Mods_off_for_a_running_search_or_safe_start_are_said_so_authors_know_why()
    {
        var view = ViewOf(Mod("com.a.rat", "Pack Rat", new ModState.Off(), On), Mod("com.b.bags", "Bags", new ModState.Off(), On));

        Assert.Contains(
            "FIND THE PROBLEM MOD is running round 3. These mods are off for it: Pack Rat, Bags.\n",
            ModReport.Text(view with { StartPlan = new PlanView.Round(3, new[] { "com.a.rat", "com.b.bags" }, 4, false, true, false) }));
        Assert.Contains("SAFE START is running. These mods are off for it: Pack Rat, Bags.\n", ModReport.Text(view with { StartPlan = new PlanView.SafeNow(new[] { "com.a.rat", "com.b.bags" }) }));

        // Set up for the next start only: this session runs every mod, and each mod's line already says what changes.
        Assert.DoesNotContain("off for it", ModReport.Text(view with { StartPlan = new PlanView.Round(3, new[] { "com.a.rat" }, 4, false, false, false) }));
        Assert.DoesNotContain("off for it", ModReport.Text(view with { StartPlan = new PlanView.SafeSet(new[] { "com.a.rat" }) }));
    }
}
