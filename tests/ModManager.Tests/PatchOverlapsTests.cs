using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>Two mods changing the same game method show on both mods' pages, so a player knows where to look when they misbehave together.</summary>
public class PatchOverlapsTests
{
    [Fact]
    public void A_method_two_mods_patch_shows_on_both_mods()
    {
        var overlaps = PatchOverlaps.Find(new[] { new PatchedMethod("Player.TakeDamage", new[] { "com.a", "com.b" }) });

        Assert.Equal(new[] { new PatchOverlap("com.b", new[] { "Player.TakeDamage" }) }, overlaps["com.a"]);
        Assert.Equal(new[] { new PatchOverlap("com.a", new[] { "Player.TakeDamage" }) }, overlaps["com.b"]);
    }

    [Fact]
    public void A_method_only_one_mod_patches_shows_nowhere()
    {
        var overlaps = PatchOverlaps.Find(new[] { new PatchedMethod("Player.TakeDamage", new[] { "com.a", "com.a" }), new PatchedMethod("Bag.Add", new[] { "com.b" }) });

        Assert.Empty(overlaps);
    }

    [Fact]
    public void The_mod_sharing_the_most_methods_comes_first_and_methods_are_in_name_order_once()
    {
        var overlaps = PatchOverlaps.Find(new[]
        {
            new PatchedMethod("Player.TakeDamage", new[] { "com.a", "com.c" }),
            new PatchedMethod("Bag.Add", new[] { "com.a", "com.b", "com.c" }),
            new PatchedMethod("Bag.Add", new[] { "com.a", "com.c" }),
        });

        Assert.Equal(new[] { new PatchOverlap("com.c", new[] { "Bag.Add", "Player.TakeDamage" }), new PatchOverlap("com.b", new[] { "Bag.Add" }) }, overlaps["com.a"]);
        Assert.Equal(new[] { "com.a", "com.c" }, overlaps["com.b"].Select(overlap => overlap.Guid));
    }
}
