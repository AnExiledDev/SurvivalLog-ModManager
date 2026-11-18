using System;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// Two mods changing the same game method show on both mods' pages, marked by how likely they are to get in each
/// other's way and why, so a player knows where to look first when mods misbehave together. HarmonyX runs every
/// prefix and postfix whatever the others do, so the risk is about one mod's change undoing or hiding another's.
/// </summary>
public class PatchOverlapsTests
{
    private static PatchedMethod Method(string name, params (string Guid, PatchEffect Effect)[] uses) =>
        new(name, uses.Select(use => new PatchUse(use.Guid, use.Effect)).ToList());

    [Fact]
    public void A_method_two_mods_patch_shows_on_both_mods_with_what_each_does()
    {
        var overlaps = PatchOverlaps.Find(new[] { Method("Player.TakeDamage", ("com.a", PatchEffect.RunsBefore), ("com.b", PatchEffect.RunsAfter)) });

        Assert.Equal(new[] { new PatchOverlap("com.b", PatchRisk.Harmless, new[] { new SharedMethod("Player.TakeDamage", PatchRisk.Harmless, new[] { PatchEffect.RunsBefore }, new[] { PatchEffect.RunsAfter }) }) }, overlaps["com.a"]);
        Assert.Equal(new[] { PatchEffect.RunsAfter }, overlaps["com.b"].Single().Methods.Single().Mine);
    }

    [Fact]
    public void A_method_only_one_mod_patches_shows_nowhere()
    {
        var overlaps = PatchOverlaps.Find(new[] { Method("Player.TakeDamage", ("com.a", PatchEffect.CanSkip), ("com.a", PatchEffect.RunsAfter)), Method("Bag.Add", ("com.b", PatchEffect.Rewrites)) });

        Assert.Empty(overlaps);
    }

    [Theory]
    [InlineData(PatchEffect.RunsBefore, PatchEffect.RunsAfter, PatchRisk.Harmless, ClashReason.None)]
    [InlineData(PatchEffect.RunsAfter, PatchEffect.RunsAfter, PatchRisk.Harmless, ClashReason.None)]
    [InlineData(PatchEffect.SetsResult, PatchEffect.RunsAfter, PatchRisk.Harmless, ClashReason.None)]
    [InlineData(PatchEffect.ChangesArgs, PatchEffect.SetsResult, PatchRisk.Harmless, ClashReason.None)]
    [InlineData(PatchEffect.CanSkip, PatchEffect.RunsAfter, PatchRisk.MayClash, ClashReason.Skips)]
    [InlineData(PatchEffect.CanSkip, PatchEffect.CanSkip, PatchRisk.MayClash, ClashReason.Skips)]
    [InlineData(PatchEffect.ChangesArgs, PatchEffect.ChangesArgs, PatchRisk.MayClash, ClashReason.BothChangeArgs)]
    [InlineData(PatchEffect.RunsBefore, PatchEffect.Rewrites, PatchRisk.MayClash, ClashReason.Rewrites)]
    [InlineData(PatchEffect.SetsResult, PatchEffect.SetsResult, PatchRisk.LikelyClash, ClashReason.BothSetResult)]
    [InlineData(PatchEffect.Rewrites, PatchEffect.Rewrites, PatchRisk.LikelyClash, ClashReason.BothRewrite)]
    [InlineData(PatchEffect.Rewrites, PatchEffect.CanSkip, PatchRisk.LikelyClash, ClashReason.SkipDropsRewrite)]
    public void A_clash_is_likely_only_when_one_mods_change_can_be_lost(PatchEffect mine, PatchEffect theirs, PatchRisk risk, ClashReason reason)
    {
        Assert.Equal((risk, reason), PatchOverlaps.Judge(new PatchOverlaps.Side(new[] { mine }), new PatchOverlaps.Side(new[] { theirs })));
        Assert.Equal((risk, reason), PatchOverlaps.Judge(new PatchOverlaps.Side(new[] { theirs }), new PatchOverlaps.Side(new[] { mine })));
    }

    [Fact]
    public void When_two_mods_set_the_result_the_one_that_runs_last_has_the_last_word()
    {
        var method = new PatchedMethod("Recipe.Cost", new[]
        {
            new PatchUse("com.a", PatchEffect.SetsResult, Order: 3),
            new PatchUse("com.b", PatchEffect.SetsResult, Order: 1),
            new PatchUse("com.b", PatchEffect.CanSkip, Order: 1),
        });

        var overlaps = PatchOverlaps.Find(new[] { method });

        Assert.Equal("com.a", overlaps["com.a"].Single().Methods.Single().LastWord);
        Assert.Equal("com.a", overlaps["com.b"].Single().Methods.Single().LastWord);
        Assert.Equal(ClashReason.BothSetResult, overlaps["com.b"].Single().Methods.Single().Reason);
        Assert.Equal(PatchRisk.LikelyClash, overlaps["com.b"].Single().Risk);
    }

    [Fact]
    public void Only_a_result_clash_names_a_mod_with_the_last_word()
    {
        var overlaps = PatchOverlaps.Find(new[] { new PatchedMethod("Bag.Add", new[] { new PatchUse("com.a", PatchEffect.CanSkip, Order: 0), new PatchUse("com.b", PatchEffect.SetsResult, Order: 5) }) });

        Assert.Null(overlaps["com.a"].Single().Methods.Single().LastWord);
    }

    [Fact]
    public void A_mod_that_checks_whether_the_method_was_skipped_is_not_caught_out_by_a_skip()
    {
        var checks = new[] { new PatchUse("com.a", PatchEffect.CanSkip), new PatchUse("com.b", PatchEffect.RunsAfter, ChecksSkip: true) };
        var doesNotCheck = new[] { new PatchUse("com.a", PatchEffect.CanSkip), new PatchUse("com.b", PatchEffect.RunsAfter, Order: 1), new PatchUse("com.b", PatchEffect.RunsBefore, Order: 2, ChecksSkip: true) };

        Assert.Equal(PatchRisk.Harmless, PatchOverlaps.Find(new[] { new PatchedMethod("Bag.Add", checks) })["com.a"].Single().Risk);
        Assert.Equal(PatchRisk.MayClash, PatchOverlaps.Find(new[] { new PatchedMethod("Bag.Add", doesNotCheck) })["com.a"].Single().Risk);
    }

    [Fact]
    public void Checking_for_a_skip_does_not_save_a_rewrite_the_skip_drops()
    {
        var uses = new[] { new PatchUse("com.a", PatchEffect.CanSkip), new PatchUse("com.b", PatchEffect.Rewrites, ChecksSkip: true) };

        Assert.Equal(ClashReason.SkipDropsRewrite, PatchOverlaps.Find(new[] { new PatchedMethod("Bag.Add", uses) })["com.a"].Single().Methods.Single().Reason);
    }

    [Fact]
    public void Performance_Analyzer_timing_the_game_code_is_not_a_clash()
    {
        var overlaps = PatchOverlaps.Find(new[] { Method("Bag.Add", ("com.a", PatchEffect.CanSkip), (PatchOverlaps.MeasuringOnly.First(), PatchEffect.Rewrites)) });

        Assert.Empty(overlaps);
    }

    [Fact]
    public void A_mod_that_adds_and_also_skips_counts_as_skipping()
    {
        var overlaps = PatchOverlaps.Find(new[] { Method("Bag.Add", ("com.a", PatchEffect.RunsAfter), ("com.a", PatchEffect.CanSkip), ("com.b", PatchEffect.RunsAfter)) });
        var shared = overlaps["com.b"].Single().Methods.Single();

        Assert.Equal(PatchRisk.MayClash, shared.Risk);
        Assert.Equal(new[] { PatchEffect.RunsAfter, PatchEffect.CanSkip }, shared.Theirs);
    }

    [Fact]
    public void The_riskiest_mod_comes_first_and_carries_its_worst_method()
    {
        var overlaps = PatchOverlaps.Find(new[]
        {
            Method("Hotkeys.Update", ("com.a", PatchEffect.RunsAfter), ("com.b", PatchEffect.RunsAfter), ("com.c", PatchEffect.RunsAfter)),
            Method("Bag.Add", ("com.a", PatchEffect.RunsAfter), ("com.b", PatchEffect.RunsAfter)),
            Method("Player.TakeDamage", ("com.a", PatchEffect.SetsResult), ("com.c", PatchEffect.SetsResult)),
        });

        Assert.Equal(new[] { "com.c", "com.b" }, overlaps["com.a"].Select(overlap => overlap.Guid));
        Assert.Equal(PatchRisk.LikelyClash, overlaps["com.a"][0].Risk);
        Assert.Equal(new[] { "Player.TakeDamage", "Hotkeys.Update" }, overlaps["com.a"][0].Methods.Select(method => method.Method));
        Assert.Equal(PatchRisk.Harmless, overlaps["com.a"][1].Risk);
    }

    [Fact]
    public void Patches_on_one_named_method_are_judged_together()
    {
        var overlaps = PatchOverlaps.Find(new[] { Method("Bag.Add", ("com.a", PatchEffect.RunsAfter), ("com.b", PatchEffect.RunsAfter)), Method("Bag.Add", ("com.a", PatchEffect.CanSkip), ("com.b", PatchEffect.RunsAfter)) });

        Assert.Equal(PatchRisk.MayClash, overlaps["com.a"].Single().Methods.Single().Risk);
    }

    [Fact]
    public void An_overload_named_with_its_parameters_still_matches_a_stack_trace_name()
    {
        Assert.Equal("Bag.Add", new SharedMethod("Bag.Add(Item, Int32)", PatchRisk.Harmless, Array.Empty<PatchEffect>(), Array.Empty<PatchEffect>()).Name);
        Assert.Equal("Bag.Add", new SharedMethod("Bag.Add", PatchRisk.Harmless, Array.Empty<PatchEffect>(), Array.Empty<PatchEffect>()).Name);
    }

    [Fact]
    public void The_conflicts_tab_says_which_mod_has_the_last_word_on_a_result()
    {
        var shared = new SharedMethod("Recipe.Cost", PatchRisk.LikelyClash, new[] { PatchEffect.SetsResult }, new[] { PatchEffect.SetsResult }, ClashReason.BothSetResult, "com.ann.map");
        var map = Mod("com.ann.map") with { PatchOverlaps = new[] { new PatchOverlap("com.bob.radar", PatchRisk.LikelyClash, new[] { shared }) } };
        var radar = Mod("com.bob.radar") with { PatchOverlaps = new[] { new PatchOverlap("com.ann.map", PatchRisk.LikelyClash, new[] { shared with { Mine = shared.Theirs } }) } };
        var engine = KnownFactsTests.Open(map, radar);

        engine.Execute("fire('click', { target: el('DIV', ['tab'], { side: 'conflicts' }, { '[data-side]': { dataset: { side: 'conflicts' } } }) })");

        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("changes what it returns", drawn);
        Assert.Contains("Only the last one to run counts, and that is this mod.", drawn);
    }

    private static ModEntry Mod(string guid) =>
        new(guid, guid, "1.0.0", guid + ".dll", new ModState.Loaded(Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());
}
