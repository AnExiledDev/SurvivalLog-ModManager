using System;
using System.Collections.Generic;
using System.Linq;
using ExiledToolkit;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The Mod Manager keeps a copy of the game's tables from the moment the game loads them. Comparing the tables with that
/// copy later finds every change any mod made, and the changes no mod reported to Exiled's Toolkit are the ones a player
/// can't trace any other way.
/// </summary>
public class TableCheckTests
{
    private static readonly TableField[] ItemFields = { new("price", FieldKind.Whole), new("weight", FieldKind.Float), new("canSell", FieldKind.Flag), new("name", FieldKind.Text) };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoWrites = new Dictionary<string, IReadOnlyList<string>>();

    private static ulong[] Item(long price, float weight, bool canSell, string name) =>
        new[] { TableValues.OfWhole(price), TableValues.OfFloat(weight), TableValues.OfFlag(canSell), Fingerprint.Of(name) };

    private static TableCopy Items(params (long Id, ulong[] Cells)[] rows)
    {
        var copy = new TableCopy("Config_Item", ItemFields);

        foreach (var (id, cells) in rows) copy.Add(RowKey.Of(id), cells);

        return copy;
    }

    private static IReadOnlyList<TableChange> Compare(TableCopy before, params (long Id, ulong[] Cells)[] now)
    {
        var diff = new TableDiff(before);

        foreach (var (id, cells) in now) diff.Row(RowKey.Of(id), cells);

        return diff.Finish();
    }

    private static ClaimSet Claims(IEnumerable<RowClaim>? rows = null, IEnumerable<FieldClaim>? fields = null) =>
        new((rows ?? Array.Empty<RowClaim>()).ToList(), (fields ?? Array.Empty<FieldClaim>()).ToList(), Array.Empty<HookClaim>(), Array.Empty<BytesClaim>());

    private static TableChange.ValueChanged Price(string row, string was, string now) => new("Config_Item", row, "price", was, now);

    [Fact]
    public void A_table_that_did_not_change_shows_no_changes()
    {
        var before = Items((1, Item(50, 1.5f, true, "Rope")), (2, Item(10, 0.2f, false, "Nail")));

        Assert.Empty(Compare(before, (2, Item(10, 0.2f, false, "Nail")), (1, Item(50, 1.5f, true, "Rope"))));
    }

    [Fact]
    public void A_changed_number_shows_both_values()
    {
        var before = Items((1203, Item(50, 1.5f, true, "Rope")));

        Assert.Equal(new TableChange[] { Price("1203", "50", "80") }, Compare(before, (1203, Item(80, 1.5f, true, "Rope"))));
    }

    [Fact]
    public void A_changed_decimal_and_flag_show_the_way_the_toolkit_writes_them()
    {
        var before = Items((1, Item(50, 1.5f, true, "Rope")));

        var changes = Compare(before, (1, Item(50, 0.25f, false, "Rope")));

        Assert.Equal(new TableChange[] { new TableChange.ValueChanged("Config_Item", "1", "weight", "1.5", "0.25"), new TableChange.ValueChanged("Config_Item", "1", "canSell", "True", "False") }, changes);
    }

    [Fact]
    public void Changed_text_shows_as_a_change_without_the_old_text()
    {
        var before = Items((1, Item(50, 1.5f, true, "Rope")));

        Assert.Equal(new TableChange[] { new TableChange.ValueChanged("Config_Item", "1", "name", null, null) }, Compare(before, (1, Item(50, 1.5f, true, "Strong rope"))));
    }

    [Fact]
    public void A_changed_enum_value_shows_by_name_and_a_value_with_no_name_by_its_number()
    {
        var category = new TableField("category", FieldKind.Whole) { Names = new Dictionary<long, string> { [1] = "Food", [2] = "Tool" } };
        var before = new TableCopy("Config_Item", new[] { category });

        before.Add(RowKey.Of(1), new[] { TableValues.OfWhole(1) });
        before.Add(RowKey.Of(2), new[] { TableValues.OfWhole(2) });

        var diff = new TableDiff(before);

        diff.Row(RowKey.Of(1), new[] { TableValues.OfWhole(2) });
        diff.Row(RowKey.Of(2), new[] { TableValues.OfWhole(3) });

        Assert.Equal(new TableChange[] { new TableChange.ValueChanged("Config_Item", "1", "category", "Food", "Tool"), new TableChange.ValueChanged("Config_Item", "2", "category", "Tool", "3") }, diff.Finish());
    }

    [Fact]
    public void A_value_reads_as_a_mod_would_report_it_and_an_enum_value_with_no_name_is_left_out()
    {
        var category = new TableField("category", FieldKind.Whole) { Names = new Dictionary<long, string> { [1] = "Food" } };

        Assert.Equal("Food", TableValues.AsReported(category, TableValues.OfWhole(1)));
        Assert.Null(TableValues.AsReported(category, TableValues.OfWhole(3)));
        Assert.Equal("3", TableValues.AsReported(new TableField("price", FieldKind.Whole), TableValues.OfWhole(3)));
        Assert.Equal("False", TableValues.AsReported(new TableField("canSell", FieldKind.Flag), TableValues.OfFlag(false)));
        Assert.Null(TableValues.AsReported(new TableField("name", FieldKind.Text), Fingerprint.Of("Rope")));
    }

    [Fact]
    public void A_new_row_and_a_missing_row_both_show()
    {
        var before = Items((1, Item(50, 1.5f, true, "Rope")), (2, Item(10, 0.2f, false, "Nail")));

        var changes = Compare(before, (1, Item(50, 1.5f, true, "Rope")), (910001, Item(5, 1, true, "Wind turbine")));

        Assert.Equal(new TableChange[] { new TableChange.RowAdded("Config_Item", "910001"), new TableChange.RowRemoved("Config_Item", "2") }, changes);
    }

    [Fact]
    public void Rows_keyed_by_text_name_the_row_by_its_text()
    {
        var copy = new TableCopy("Config_GlobalSetting", new[] { new TableField("value", FieldKind.Whole) });

        copy.Add(RowKey.Of("MaxDay"), new[] { TableValues.OfWhole(30) });

        var diff = new TableDiff(copy);

        diff.Row(RowKey.Of("MaxDay"), new[] { TableValues.OfWhole(60) });

        Assert.Equal(new TableChange[] { new TableChange.ValueChanged("Config_GlobalSetting", "MaxDay", "value", "30", "60") }, diff.Finish());
    }

    [Fact]
    public void A_negative_whole_number_reads_back_as_negative()
    {
        Assert.Equal("-3", TableValues.Show(FieldKind.Whole, TableValues.OfWhole(-3)));
    }

    [Fact]
    public void A_double_reads_back_as_written()
    {
        Assert.Equal("0.125", TableValues.Show(FieldKind.Double, TableValues.OfDouble(0.125)));
    }

    [Fact]
    public void A_copy_takes_one_value_per_field_and_each_row_once()
    {
        var copy = Items((1, Item(50, 1.5f, true, "Rope")));

        Assert.Throws<ArgumentException>(() => copy.Add(RowKey.Of(2), new ulong[] { 1, 2 }));
        Assert.Throws<ArgumentException>(() => copy.Add(RowKey.Of(1), Item(50, 1.5f, true, "Rope")));
    }

    [Fact]
    public void A_copy_keeps_every_row_as_it_grows()
    {
        var copy = new TableCopy("Config_Item", ItemFields);

        for (var id = 0; id < 100; id++) copy.Add(RowKey.Of(id), Item(id, id, id % 2 == 0, "n" + id));

        var now = Enumerable.Range(0, 100).Select(id => ((long)id, Item(id, id, id % 2 == 0, "n" + id))).ToArray();

        Assert.Empty(Compare(copy, now));
    }

    [Fact]
    public void Different_text_gets_a_different_fingerprint_and_the_same_text_the_same_one()
    {
        Assert.Equal(Fingerprint.Of("Rope"), Fingerprint.Of("Rope"));
        Assert.NotEqual(Fingerprint.Of("Rope"), Fingerprint.Of("Ropf"));
        Assert.NotEqual(Fingerprint.Of("ab"), Fingerprint.Of("ba"));
    }

    [Fact]
    public void A_fingerprint_counts_every_byte_of_a_number()
    {
        var low = new Fingerprint();
        var high = new Fingerprint();

        low.Add(1UL);
        high.Add(1UL << 56);

        Assert.NotEqual(low.Value, high.Value);
    }

    [Fact]
    public void A_value_a_mod_reported_writing_is_reported()
    {
        var done = DataCheck.Sort(1, 1, new[] { Price("1203", "50", "80") }, Claims(fields: new[] { new FieldClaim("com.a", "Config_Item", "1203", "price", "80", 1) }), NoWrites);

        Assert.Equal(1, done.Reported);
        Assert.Empty(done.Unreported);
    }

    [Fact]
    public void A_value_in_a_row_a_mod_reported_adding_is_reported()
    {
        var done = DataCheck.Sort(1, 1, new[] { Price("910001", "5", "9") }, Claims(rows: new[] { new RowClaim("com.a", "Config_Item", "910001") }), NoWrites);

        Assert.Equal(1, done.Reported);
        Assert.Empty(done.Unreported);
    }

    [Fact]
    public void A_value_a_mod_reported_for_another_row_or_field_is_not_reported()
    {
        var claims = Claims(fields: new[] { new FieldClaim("com.a", "Config_Item", "1203", "weight", "1", 1), new FieldClaim("com.a", "Config_Item", "1204", "price", "80", 2) });

        var done = DataCheck.Sort(1, 1, new[] { Price("1203", "50", "80") }, claims, NoWrites);

        Assert.Equal(0, done.Reported);
        Assert.Equal(new[] { Price("1203", "50", "80") }, done.Unreported.Single().Shown);
    }

    [Fact]
    public void A_row_added_is_reported_only_when_a_mod_reported_that_row()
    {
        var changes = new TableChange[] { new TableChange.RowAdded("Config_Item", "910001"), new TableChange.RowAdded("Config_Item", "910002") };

        var done = DataCheck.Sort(1, 1, changes, Claims(rows: new[] { new RowClaim("com.a", "Config_Item", "910001") }), NoWrites);

        Assert.Equal(1, done.Reported);
        Assert.Equal(1, done.Unreported.Single().Added);
        Assert.Equal("910002", done.Unreported.Single().Shown.Single().Row);
    }

    [Fact]
    public void A_removed_row_is_never_reported_as_no_mod_can_report_one()
    {
        var done = DataCheck.Sort(1, 1, new[] { new TableChange.RowRemoved("Config_Item", "2") }, Claims(rows: new[] { new RowClaim("com.a", "Config_Item", "2") }), NoWrites);

        Assert.Equal(0, done.Reported);
        Assert.Equal(1, done.Unreported.Single().Removed);
    }

    [Fact]
    public void Each_table_counts_its_changes_and_shows_only_the_first_few()
    {
        var changes = Enumerable.Range(1, DataCheck.ShownPerTable + 5).Select(row => (TableChange)Price(row.ToString(), "1", "2")).Append(new TableChange.RowRemoved("Config_Item", "99")).ToList();

        var table = DataCheck.Sort(1, 1, changes, Claims(), NoWrites).Unreported.Single();

        Assert.Equal(DataCheck.ShownPerTable + 5, table.Changed);
        Assert.Equal(1, table.Removed);
        Assert.Equal(DataCheck.ShownPerTable + 6, table.Count);
        Assert.Equal(DataCheck.ShownPerTable, table.Shown.Count);
    }

    [Fact]
    public void The_table_with_the_most_changes_comes_first_then_by_name()
    {
        var changes = new TableChange[]
        {
            new TableChange.RowAdded("Config_Wish", "1"),
            new TableChange.RowAdded("Config_Action", "1"),
            new TableChange.RowAdded("Config_Item", "1"),
            new TableChange.RowAdded("Config_Item", "2"),
        };

        var done = DataCheck.Sort(3, 4, changes, Claims(), NoWrites);

        Assert.Equal(new[] { "Config_Item", "Config_Action", "Config_Wish" }, done.Unreported.Select(table => table.Table));
        Assert.Equal(3, done.Tables);
        Assert.Equal(4, done.Rows);
    }

    [Fact]
    public void A_mod_whose_code_writes_the_changed_field_is_named_and_one_writing_another_field_is_not()
    {
        var writes = new Dictionary<string, IReadOnlyList<string>>
        {
            ["com.price"] = new[] { "Config_Item.price" },
            ["com.weight"] = new[] { "Config_Item.weight" },
            ["com.other"] = new[] { "Config_Action.price" },
        };

        var table = DataCheck.Sort(1, 1, new[] { Price("1203", "50", "80") }, Claims(), writes).Unreported.Single();

        Assert.Equal(new[] { "com.price" }, table.Writers);
    }

    [Fact]
    public void A_mod_whose_code_adds_or_replaces_rows_is_named_for_any_change_in_that_table()
    {
        var writes = new Dictionary<string, IReadOnlyList<string>>
        {
            ["com.rows"] = new[] { "Config_Item" },
            ["com.actions"] = new[] { "Config_Action" },
        };

        Assert.Equal(new[] { "com.rows" }, DataCheck.Sort(1, 1, new[] { Price("1203", "50", "80") }, Claims(), writes).Unreported.Single().Writers);
        Assert.Equal(new[] { "com.rows" }, DataCheck.Sort(1, 1, new[] { new TableChange.RowRemoved("Config_Item", "2") }, Claims(), writes).Unreported.Single().Writers);
    }

    [Fact]
    public void Writers_are_named_in_a_fixed_order()
    {
        var writes = new Dictionary<string, IReadOnlyList<string>>
        {
            ["com.zeta"] = new[] { "Config_Item" },
            ["com.alpha"] = new[] { "Config_Item.price" },
        };

        Assert.Equal(new[] { "com.alpha", "com.zeta" }, DataCheck.Sort(1, 1, new[] { Price("1203", "50", "80") }, Claims(), writes).Unreported.Single().Writers);
    }
}
