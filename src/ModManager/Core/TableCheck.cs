using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExiledToolkit;

namespace ModManager.Core;

/// <summary>How a table field's value is kept in a copy, and so whether a change to it can show the two values.</summary>
public enum FieldKind
{
    Flag,
    Whole,
    Float,
    Double,

    /// <summary>Kept as a fingerprint of the text: a change shows, the old text does not.</summary>
    Text,

    /// <summary>Lists, arrays, structs and other objects, kept as a fingerprint of what they hold.</summary>
    Other,
}

/// <param name="Name">The property's name, as mods name it when they report a change (price, not its backing field).</param>
public sealed record TableField(string Name, FieldKind Kind)
{
    /// <summary>For an enum, the name of each value, as mods write it when they report a change. Null for anything else.</summary>
    public IReadOnlyDictionary<long, string>? Names { get; init; }
}

/// <summary>A row's id: a number for most tables, text for the few keyed by text.</summary>
public readonly record struct RowKey(long Number, string? Text)
{
    public static RowKey Of(long number) => new(number, null);

    public static RowKey Of(string text) => new(0, text);

    public override string ToString() => Text ?? Number.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// One of the game's tables as it was: every row's value of every field, as 64 bits each. Numbers and flags are kept
/// as they are; text, lists and objects as a fingerprint (<see cref="Fingerprint"/>).
/// </summary>
public sealed class TableCopy
{
    private readonly Dictionary<RowKey, int> _indexOf = new();
    private readonly List<RowKey> _keys = new();
    private ulong[] _cells = Array.Empty<ulong>();

    public TableCopy(string table, IReadOnlyList<TableField> fields)
    {
        Table = table;
        Fields = fields;
    }

    /// <summary>The row class's name (Config_Item), as mods name the table when they report a change.</summary>
    public string Table { get; }

    public IReadOnlyList<TableField> Fields { get; }

    public int Count => _keys.Count;

    /// <param name="cells">One value per field, in the order of <see cref="Fields"/>.</param>
    public void Add(RowKey key, ReadOnlySpan<ulong> cells)
    {
        if (cells.Length != Fields.Count) throw new ArgumentException("needs one value per field: " + Fields.Count + ", not " + cells.Length, nameof(cells));
        if (_indexOf.ContainsKey(key)) throw new ArgumentException("row " + key + " is already in the copy of " + Table, nameof(key));

        var start = _keys.Count * Fields.Count;

        if (start + cells.Length > _cells.Length) Array.Resize(ref _cells, Math.Max(start + cells.Length, _cells.Length * 2));

        cells.CopyTo(_cells.AsSpan(start));
        _indexOf[key] = _keys.Count;
        _keys.Add(key);
    }

    internal bool TryFind(RowKey key, out int index) => _indexOf.TryGetValue(key, out index);

    internal RowKey KeyAt(int index) => _keys[index];

    internal ReadOnlySpan<ulong> CellsAt(int index) => _cells.AsSpan(index * Fields.Count, Fields.Count);
}

/// <summary>A difference between a table and its copy.</summary>
public abstract record TableChange(string Table, string Row)
{
    public sealed record RowAdded(string Table, string Row) : TableChange(Table, Row);

    public sealed record RowRemoved(string Table, string Row) : TableChange(Table, Row);

    /// <param name="Was">Null for text, lists and objects, whose copy keeps only a fingerprint.</param>
    public sealed record ValueChanged(string Table, string Row, string Field, string? Was, string? Now) : TableChange(Table, Row);
}

/// <summary>
/// Compares a table as it is now with its copy, one row at a time, so the check can be spread over frames. Each row
/// of the table is handed in once; whatever the copy has that was never handed in was removed.
/// </summary>
public sealed class TableDiff
{
    private readonly TableCopy _before;
    private readonly bool[] _isSeen;
    private readonly List<TableChange> _changes = new();

    public TableDiff(TableCopy before)
    {
        _before = before;
        _isSeen = new bool[before.Count];
    }

    public void Row(RowKey key, ReadOnlySpan<ulong> cells)
    {
        if (cells.Length != _before.Fields.Count) throw new ArgumentException("needs one value per field: " + _before.Fields.Count + ", not " + cells.Length, nameof(cells));

        if (!_before.TryFind(key, out var index))
        {
            _changes.Add(new TableChange.RowAdded(_before.Table, key.ToString()));

            return;
        }

        _isSeen[index] = true;

        var was = _before.CellsAt(index);

        for (var field = 0; field < cells.Length; field++)
        {
            if (was[field] == cells[field]) continue;

            var shown = _before.Fields[field];

            _changes.Add(new TableChange.ValueChanged(_before.Table, key.ToString(), shown.Name, TableValues.Show(shown, was[field]), TableValues.Show(shown, cells[field])));
        }
    }

    /// <summary>Every change found, with the rows that were never handed in as removed.</summary>
    public IReadOnlyList<TableChange> Finish()
    {
        for (var index = 0; index < _isSeen.Length; index++)
        {
            if (!_isSeen[index]) _changes.Add(new TableChange.RowRemoved(_before.Table, _before.KeyAt(index).ToString()));
        }

        return _changes;
    }
}

/// <summary>What one compare of the game's tables with their copy found.</summary>
/// <param name="Tables">How many tables were compared.</param>
/// <param name="Rows">How many rows the tables have now.</param>
/// <param name="Current">
/// The value the game has now of each field a mod reported writing, by table, row and field, written the way Exiled's
/// Toolkit writes reported values. Null for lists and objects; a row the game no longer has is left out.
/// </param>
public sealed record TableReading(int Tables, int Rows, IReadOnlyList<TableChange> Changes, IReadOnlyDictionary<(string Table, string Row, string Field), string?> Current);

/// <summary>How a table value kept as bits reads, the way Exiled's Toolkit writes reported values.</summary>
public static class TableValues
{
    public static ulong OfFlag(bool value) => value ? 1UL : 0UL;

    public static ulong OfWhole(long value) => unchecked((ulong)value);

    public static ulong OfFloat(float value) => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    public static ulong OfDouble(double value) => unchecked((ulong)BitConverter.DoubleToInt64Bits(value));

    /// <summary>Null for text, lists and objects: only their fingerprint is kept.</summary>
    public static string? Show(FieldKind kind, ulong bits) => kind switch
    {
        FieldKind.Flag => Convert.ToString(bits != 0, CultureInfo.InvariantCulture),
        FieldKind.Whole => unchecked((long)bits).ToString(CultureInfo.InvariantCulture),
        FieldKind.Float => BitConverter.Int32BitsToSingle(unchecked((int)(uint)bits)).ToString(CultureInfo.InvariantCulture),
        FieldKind.Double => BitConverter.Int64BitsToDouble(unchecked((long)bits)).ToString(CultureInfo.InvariantCulture),
        FieldKind.Text or FieldKind.Other => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The same, with an enum value shown by its name, and by its number when it has none.</summary>
    public static string? Show(TableField field, ulong bits) => NameOf(field, bits) ?? Show(field.Kind, bits);

    /// <summary>
    /// The value the way a mod reporting it would have written it, to compare with what it reported; null when that
    /// cannot be known. An enum value with no name of its own (two flags at once) is written by the mod as names, so it
    /// is left out rather than compared as a number.
    /// </summary>
    public static string? AsReported(TableField field, ulong bits) => field.Names == null ? Show(field.Kind, bits) : NameOf(field, bits);

    private static string? NameOf(TableField field, ulong bits) =>
        field.Kind == FieldKind.Whole && field.Names != null && field.Names.TryGetValue(unchecked((long)bits), out var name) ? name : null;
}

/// <summary>
/// A 64-bit FNV-1a hash, for keeping text, lists and objects in a table copy as one number. Two different values can
/// in theory get the same fingerprint, so a change can be missed, but never reported where there is none.
/// </summary>
public struct Fingerprint
{
    private const ulong Start = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    private ulong _hash;
    private bool _isStarted;

    public ulong Value => _isStarted ? _hash : Start;

    public static ulong Of(ReadOnlySpan<char> text)
    {
        var print = new Fingerprint();

        print.Add(text);

        return print.Value;
    }

    public static ulong Of(ReadOnlySpan<byte> bytes)
    {
        var print = new Fingerprint();

        print.Add(bytes);

        return print.Value;
    }

    public void Add(ReadOnlySpan<char> text)
    {
        foreach (var unit in text) Add((ulong)unit, 2);
    }

    public void Add(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) Add(value, 1);
    }

    public void Add(ulong value) => Add(value, 8);

    private void Add(ulong value, int bytes)
    {
        var hash = Value;

        for (var at = 0; at < bytes; at++)
        {
            hash = unchecked((hash ^ ((value >> (8 * at)) & 0xff)) * Prime);
        }

        _hash = hash;
        _isStarted = true;
    }
}

/// <summary>One table's changes that no mod reported, and the running mods whose code writes that table.</summary>
/// <param name="Shown">The first few changes, at most <see cref="DataCheck.ShownPerTable"/>.</param>
/// <param name="Writers">Running mods whose code changes this table's values or rows, sorted: the likely source.</param>
public sealed record TableChanges(string Table, int Added, int Removed, int Changed, IReadOnlyList<TableChange> Shown, IReadOnlyList<string> Writers)
{
    public int Count => Added + Removed + Changed;
}

/// <summary>What the check of the game's tables found, or why there is nothing to show.</summary>
public abstract record DataCheck
{
    public const int ShownPerTable = 10;

    private DataCheck()
    {
    }

    /// <summary>The game has not loaded its tables yet.</summary>
    public sealed record Waiting : DataCheck;

    /// <summary>The tables are being compared with the copy, a little each frame.</summary>
    public sealed record Checking : DataCheck;

    /// <summary>There is no copy to compare with.</summary>
    public sealed record NoCopy(NoCopyReason Reason) : DataCheck;

    /// <param name="Tables">How many tables were compared.</param>
    /// <param name="Reported">How many changes some mod reported through Exiled's Toolkit.</param>
    /// <param name="Unreported">The tables with changes no mod reported, most changes first.</param>
    public sealed record Done(int Tables, int Rows, int Reported, IReadOnlyList<TableChanges> Unreported) : DataCheck;

    /// <summary>
    /// Sorts the changes into the ones a mod reported and the ones no mod did. A changed value is reported when a mod
    /// reported writing that field, adding entries to it, or adding that row (a mod can put its own row where a game row
    /// was); an added row when a mod reported adding it. No mod can report removing a game row, so a removed row is never reported.
    /// </summary>
    /// <param name="writes">What each running mod's code changes, by its guid: "Config_Item.price" for a field, "Config_Item" for adding, replacing or removing rows.</param>
    public static Done Sort(int tables, int rows, IEnumerable<TableChange> changes, ClaimSet claims, IReadOnlyDictionary<string, IReadOnlyList<string>> writes)
    {
        var addedRows = claims.Rows.Select(claim => (claim.Table, claim.Row)).ToHashSet();
        var wroteFields = claims.Fields.Select(claim => (claim.Table, claim.Row, claim.Field)).Concat(claims.Entries.Select(claim => (claim.Table, claim.Row, claim.Field))).ToHashSet();
        var reported = 0;
        var unreported = new List<TableChange>();

        foreach (var change in changes)
        {
            var isReported = change switch
            {
                TableChange.ValueChanged value => wroteFields.Contains((value.Table, value.Row, value.Field)) || addedRows.Contains((value.Table, value.Row)),
                TableChange.RowAdded added => addedRows.Contains((added.Table, added.Row)),
                _ => false,
            };

            if (isReported) reported++;
            else unreported.Add(change);
        }

        var groups = unreported
            .GroupBy(change => change.Table, StringComparer.Ordinal)
            .Select(group => Group(group.Key, group.ToList(), writes))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Table, StringComparer.Ordinal)
            .ToList();

        return new Done(tables, rows, reported, groups);
    }

    private static TableChanges Group(string table, IReadOnlyList<TableChange> changes, IReadOnlyDictionary<string, IReadOnlyList<string>> writes)
    {
        // A mod that adds or replaces rows can change any value in them, so it is named for every change in the table.
        var touched = changes.OfType<TableChange.ValueChanged>().Select(value => table + "." + value.Field).Append(table).ToHashSet(StringComparer.Ordinal);
        var writers = writes
            .Where(mod => mod.Value.Any(touched.Contains))
            .Select(mod => mod.Key)
            .OrderBy(guid => guid, StringComparer.Ordinal)
            .ToList();

        return new TableChanges(
            table,
            changes.Count(change => change is TableChange.RowAdded),
            changes.Count(change => change is TableChange.RowRemoved),
            changes.Count(change => change is TableChange.ValueChanged),
            changes.Take(ShownPerTable).ToList(),
            writers);
    }
}

public enum NoCopyReason
{
    /// <summary>The game had already loaded its tables when the Mod Manager started, so any mod could have changed them first.</summary>
    LoadedFirst,

    /// <summary>
    /// The Mod Manager could not hook the game's table load, or the tables are not laid out the way it reads them. The
    /// log says which; a game update may have changed them.
    /// </summary>
    Unreadable,
}
