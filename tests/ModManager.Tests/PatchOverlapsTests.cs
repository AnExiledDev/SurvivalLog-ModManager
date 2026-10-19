using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// Two mods changing the same game method show on both mods' pages, marked by how likely they are to get in each
/// other's way, so a player knows where to look first when mods misbehave together.
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
    [InlineData(PatchEffect.RunsBefore, PatchEffect.RunsAfter, PatchRisk.Harmless)]
    [InlineData(PatchEffect.RunsAfter, PatchEffect.RunsAfter, PatchRisk.Harmless)]
    [InlineData(PatchEffect.CanSkip, PatchEffect.RunsAfter, PatchRisk.MayClash)]
    [InlineData(PatchEffect.RunsBefore, PatchEffect.Rewrites, PatchRisk.MayClash)]
    [InlineData(PatchEffect.CanSkip, PatchEffect.CanSkip, PatchRisk.LikelyClash)]
    [InlineData(PatchEffect.Rewrites, PatchEffect.CanSkip, PatchRisk.LikelyClash)]
    public void Adding_code_is_harmless_and_each_mod_that_can_skip_or_rewrite_the_method_raises_the_risk(PatchEffect mine, PatchEffect theirs, PatchRisk risk)
    {
        Assert.Equal(risk, PatchOverlaps.RiskOf(new[] { mine }, new[] { theirs }));
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
            Method("Player.TakeDamage", ("com.a", PatchEffect.CanSkip), ("com.c", PatchEffect.CanSkip)),
        });

        Assert.Equal(new[] { "com.c", "com.b" }, overlaps["com.a"].Select(overlap => overlap.Guid));
        Assert.Equal(PatchRisk.LikelyClash, overlaps["com.a"][0].Risk);
        Assert.Equal(new[] { "Player.TakeDamage", "Hotkeys.Update" }, overlaps["com.a"][0].Methods.Select(method => method.Method));
        Assert.Equal(PatchRisk.Harmless, overlaps["com.a"][1].Risk);
    }

    [Fact]
    public void Overloads_with_one_name_are_listed_once()
    {
        var overlaps = PatchOverlaps.Find(new[] { Method("Bag.Add", ("com.a", PatchEffect.RunsAfter), ("com.b", PatchEffect.RunsAfter)), Method("Bag.Add", ("com.a", PatchEffect.CanSkip), ("com.b", PatchEffect.RunsAfter)) });

        Assert.Equal(PatchRisk.MayClash, overlaps["com.a"].Single().Methods.Single().Risk);
    }
}
