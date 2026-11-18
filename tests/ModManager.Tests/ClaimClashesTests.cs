using System;
using System.Collections.Generic;
using System.Linq;
using ExiledToolkit;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// Mods report the changes Harmony keeps no list of (rows added to the game's tables, values written into them,
/// native hooks, changed code bytes), and two mods changing the same one show on both mods' Conflicts tabs, with the
/// mod whose value the game ends up with when that is known.
/// </summary>
public class ClaimClashesTests
{
    private static ClaimSet Set(IEnumerable<RowClaim>? rows = null, IEnumerable<FieldClaim>? fields = null, IEnumerable<HookClaim>? hooks = null, IEnumerable<BytesClaim>? bytes = null, IEnumerable<EntryClaim>? entries = null) =>
        new((rows ?? Array.Empty<RowClaim>()).ToList(), (fields ?? Array.Empty<FieldClaim>()).ToList(), (hooks ?? Array.Empty<HookClaim>()).ToList(), (bytes ?? Array.Empty<BytesClaim>()).ToList(), (entries ?? Array.Empty<EntryClaim>()).ToList());

    private static EntryClaim Stock(string guid, long order, params string[] items) => new(guid, "Config_ShopFurniture", "1", "ItemIdList", items, order);

    private static FieldClaim StockSet(string guid, long order) => new(guid, "Config_ShopFurniture", "1", "ItemIdList", "[1001, 1002]", order);

    private static PatchedMethod Patched(string method, params string[] guids) => new(method, guids.Select(guid => new PatchUse(guid, PatchEffect.RunsAfter)).ToList());

    private static IReadOnlyDictionary<string, IReadOnlyList<ClaimOverlap>> Find(ClaimSet claims, params PatchedMethod[] patched) => ClaimClashes.Find(claims, patched);

    [Fact]
    public void Two_mods_adding_the_same_row_id_clash_on_both_pages()
    {
        var clashes = Find(Set(rows: new[] { new RowClaim("com.a", "Config_Item", "910001"), new RowClaim("com.b", "Config_Item", "910001"), new RowClaim("com.b", "Config_Item", "910002") }));
        var expected = new ClaimClash(ClaimKind.SameRow, PatchRisk.LikelyClash, "Config_Item 910001");

        Assert.Equal(new[] { new ClaimOverlap("com.b", PatchRisk.LikelyClash, new[] { expected }) }, clashes["com.a"]);
        Assert.Equal(new[] { new ClaimOverlap("com.a", PatchRisk.LikelyClash, new[] { expected }) }, clashes["com.b"]);
    }

    [Fact]
    public void The_same_row_id_in_two_different_tables_is_no_clash()
    {
        Assert.Empty(Find(Set(rows: new[] { new RowClaim("com.a", "Config_Item", "910001"), new RowClaim("com.b", "Config_Furniture", "910001") })));
    }

    [Fact]
    public void Two_mods_writing_one_value_differently_clash_and_the_later_write_is_the_one_the_game_has()
    {
        var clashes = Find(Set(fields: new[] { new FieldClaim("com.a", "Config_Item", "1203", "price", "50", 7), new FieldClaim("com.b", "Config_Item", "1203", "price", "80", 3) }));
        var mine = clashes["com.a"].Single().Clashes.Single();
        var theirs = clashes["com.b"].Single().Clashes.Single();

        Assert.Equal(new ClaimClash(ClaimKind.SameValue, PatchRisk.LikelyClash, "Config_Item 1203 price", "50", "80", "com.a"), mine);
        Assert.Equal(new ClaimClash(ClaimKind.SameValue, PatchRisk.LikelyClash, "Config_Item 1203 price", "80", "50", "com.a"), theirs);
    }

    [Fact]
    public void Two_mods_writing_the_same_value_into_one_field_is_harmless()
    {
        var clashes = Find(Set(fields: new[] { new FieldClaim("com.a", "Config_Item", "1203", "price", "50", 1), new FieldClaim("com.b", "Config_Item", "1203", "price", "50", 2) }));

        Assert.Equal(new ClaimClash(ClaimKind.SameValue, PatchRisk.Harmless, "Config_Item 1203 price", "50", "50"), clashes["com.a"].Single().Clashes.Single());
    }

    [Fact]
    public void Two_mods_adding_their_own_entries_to_one_list_is_harmless()
    {
        var clashes = Find(Set(entries: new[] { Stock("com.wind", 1, "910001"), Stock("com.hydro", 2, "920001") }));
        var expected = new ClaimClash(ClaimKind.BothAdd, PatchRisk.Harmless, "Config_ShopFurniture 1 ItemIdList");

        Assert.Equal(new[] { new ClaimOverlap("com.hydro", PatchRisk.Harmless, new[] { expected }) }, clashes["com.wind"]);
        Assert.Equal(new[] { new ClaimOverlap("com.wind", PatchRisk.Harmless, new[] { expected }) }, clashes["com.hydro"]);
    }

    [Fact]
    public void A_mod_replacing_a_list_after_another_added_to_it_is_likely_to_clash_on_both_pages()
    {
        var clashes = Find(Set(fields: new[] { StockSet("com.shops", 5) }, entries: new[] { Stock("com.wind", 3, "910001") }));
        var expected = new ClaimClash(ClaimKind.SetAfterAdded, PatchRisk.LikelyClash, "Config_ShopFurniture 1 ItemIdList", LastWord: "com.shops");

        Assert.Equal(new[] { new ClaimOverlap("com.shops", PatchRisk.LikelyClash, new[] { expected }) }, clashes["com.wind"]);
        Assert.Equal(new[] { new ClaimOverlap("com.wind", PatchRisk.LikelyClash, new[] { expected }) }, clashes["com.shops"]);
    }

    [Fact]
    public void Entries_added_after_another_mod_set_the_list_keep_both()
    {
        var clashes = Find(Set(fields: new[] { StockSet("com.shops", 2) }, entries: new[] { Stock("com.wind", 4, "910001") }));

        Assert.Equal(new ClaimClash(ClaimKind.AddedAfterSet, PatchRisk.Harmless, "Config_ShopFurniture 1 ItemIdList", LastWord: "com.wind"), clashes["com.shops"].Single().Clashes.Single());
    }

    [Fact]
    public void A_mod_that_sets_and_adds_to_its_own_list_is_no_clash()
    {
        Assert.Empty(Find(Set(fields: new[] { StockSet("com.wind", 5) }, entries: new[] { Stock("com.wind", 3, "910001") })));
    }

    [Fact]
    public void Different_fields_of_one_row_are_no_clash()
    {
        Assert.Empty(Find(Set(fields: new[] { new FieldClaim("com.a", "Config_Item", "1203", "price", "50", 1), new FieldClaim("com.b", "Config_Item", "1203", "weight", "2", 2) })));
    }

    [Fact]
    public void Two_native_hooks_on_one_method_clash()
    {
        var clashes = Find(Set(hooks: new[] { new HookClaim("com.a", "PowerManager.CalcSolarOutput"), new HookClaim("com.b", "PowerManager.CalcSolarOutput") }));

        Assert.Equal(ClaimKind.BothHook, clashes["com.a"].Single().Clashes.Single().Kind);
        Assert.Equal("com.a", clashes["com.b"].Single().Guid);
    }

    [Fact]
    public void A_native_hook_on_a_method_another_mod_patches_with_harmony_clashes_on_both_pages()
    {
        var clashes = Find(Set(hooks: new[] { new HookClaim("com.a", "PowerManager.CalcSolarOutput") }), Patched("PowerManager.CalcSolarOutput(Single)", "com.b"), Patched("Bag.Add", "com.c"));
        var expected = new ClaimClash(ClaimKind.HookAndPatch, PatchRisk.LikelyClash, "PowerManager.CalcSolarOutput");

        Assert.Equal(new[] { new ClaimOverlap("com.b", PatchRisk.LikelyClash, new[] { expected }) }, clashes["com.a"]);
        Assert.Equal(new[] { new ClaimOverlap("com.a", PatchRisk.LikelyClash, new[] { expected }) }, clashes["com.b"]);
        Assert.False(clashes.ContainsKey("com.c"));
    }

    [Fact]
    public void A_mod_that_both_hooks_and_patches_one_method_has_no_clash_with_itself()
    {
        Assert.Empty(Find(Set(hooks: new[] { new HookClaim("com.a", "PowerManager.CalcSolarOutput") }), Patched("PowerManager.CalcSolarOutput", "com.a")));
    }

    [Fact]
    public void Changed_bytes_clash_only_where_the_two_ranges_meet()
    {
        var apart = Find(Set(bytes: new[] { new BytesClaim("com.a", "Survival.Settle", 40, 4), new BytesClaim("com.b", "Survival.Settle", 44, 2) }));
        var meeting = Find(Set(bytes: new[] { new BytesClaim("com.a", "Survival.Settle", 40, 4), new BytesClaim("com.b", "Survival.Settle", 43, 2) }));

        Assert.Empty(apart);
        Assert.Equal(new ClaimClash(ClaimKind.SameBytes, PatchRisk.LikelyClash, "Survival.Settle"), meeting["com.b"].Single().Clashes.Single());
    }

    [Fact]
    public void Bytes_changed_at_a_methods_start_clash_with_a_hook_or_harmony_patch_there()
    {
        var underHook = Find(Set(hooks: new[] { new HookClaim("com.b", "Survival.Settle") }, bytes: new[] { new BytesClaim("com.a", "Survival.Settle", ClaimClashes.HookBytes - 1, 2) }));
        var underPatch = Find(Set(bytes: new[] { new BytesClaim("com.a", "Survival.Settle", 0, 1) }), Patched("Survival.Settle", "com.c"));
        var pastTheJump = Find(Set(hooks: new[] { new HookClaim("com.b", "Survival.Settle") }, bytes: new[] { new BytesClaim("com.a", "Survival.Settle", ClaimClashes.HookBytes, 2) }));

        Assert.Equal(ClaimKind.BytesUnderHook, underHook["com.a"].Single().Clashes.Single().Kind);
        Assert.Equal(ClaimKind.BytesUnderHook, underHook["com.b"].Single().Clashes.Single().Kind);
        Assert.Equal("com.c", underPatch["com.a"].Single().Guid);
        Assert.Empty(pastTheJump);
    }

    [Fact]
    public void A_mods_clashes_are_riskiest_first_then_by_how_many()
    {
        var clashes = Find(Set(
            rows: new[] { new RowClaim("com.a", "Config_Item", "1"), new RowClaim("com.c", "Config_Item", "1") },
            fields: new[]
            {
                new FieldClaim("com.a", "Config_Item", "5", "price", "1", 1), new FieldClaim("com.b", "Config_Item", "5", "price", "1", 2),
                new FieldClaim("com.a", "Config_Item", "6", "price", "1", 3), new FieldClaim("com.d", "Config_Item", "6", "price", "1", 4),
                new FieldClaim("com.a", "Config_Item", "7", "price", "1", 5), new FieldClaim("com.d", "Config_Item", "7", "price", "1", 6),
            }));

        Assert.Equal(new[] { "com.c", "com.d", "com.b" }, clashes["com.a"].Select(overlap => overlap.Guid));
    }

    [Fact]
    public void A_value_the_table_no_longer_holds_shows_on_the_mod_that_wrote_it_last()
    {
        var claims = Set(fields: new[]
        {
            new FieldClaim("com.a", "Config_Item", "1203", "price", "50", 1),
            new FieldClaim("com.b", "Config_Item", "1203", "price", "80", 2),
            new FieldClaim("com.a", "Config_Item", "1204", "price", "9", 3),
        });
        var table = new Dictionary<string, string> { ["1203"] = "120", ["1204"] = "9" };

        var over = ClaimClashes.WrittenOver(claims, field => table[field.Row]);

        Assert.Equal(new[] { new ClaimOverlap("", PatchRisk.LikelyClash, new[] { new ClaimClash(ClaimKind.WrittenOver, PatchRisk.LikelyClash, "Config_Item 1203 price", "80", "120") }) }, over["com.b"]);
        Assert.False(over.ContainsKey("com.a"));
    }

    [Fact]
    public void A_value_that_cannot_be_read_or_reads_back_as_the_same_number_is_not_written_over()
    {
        var claims = Set(fields: new[] { new FieldClaim("com.a", "Config_Item", "1", "rate", "1.5", 1), new FieldClaim("com.a", "Config_Item", "2", "rate", "3", 2) });

        Assert.Empty(ClaimClashes.WrittenOver(claims, field => field.Row == "1" ? "1.50" : null));
    }

    [Fact]
    public void Merging_puts_clashes_with_one_other_mod_in_one_block()
    {
        var a = Find(Set(rows: new[] { new RowClaim("com.a", "Config_Item", "1"), new RowClaim("com.b", "Config_Item", "1") }));
        var b = Find(Set(hooks: new[] { new HookClaim("com.a", "Bag.Add"), new HookClaim("com.b", "Bag.Add") }));

        var merged = ClaimClashes.Merge(a, b);

        Assert.Equal(new[] { ClaimKind.SameRow, ClaimKind.BothHook }, merged["com.a"].Single().Clashes.Select(clash => clash.Kind));
    }
}
