using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using GameCore.HotUpdate;
using HarmonyLib;
using Il2CppInterop.Runtime;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Keeps a copy of the game's tables from the moment the game finishes loading them, and compares the tables with it
/// when asked, a little each frame. The copy is taken in a hook on the last step of the load that runs before other
/// mods' hooks there, so a change a mod makes later shows. A change made while the tables were still loading is in the
/// copy, so it does not. The compare runs on the main thread; the copy is taken wherever the game loads its tables.
/// </summary>
internal sealed class GameTables
{
    /// <summary>How long the compare may run each frame.</summary>
    private static readonly long SliceTicks = Stopwatch.Frequency * 2 / 1000;

    /// <summary>The one the hook hands the game's tables to. Harmony hooks are static, so this is too.</summary>
    private static GameTables? _hooked;

    private readonly Action<string> _log;
    private readonly Func<bool> _isSliceSpent;
    private volatile CopyState _copy = new CopyState.Waiting();
    private Compare? _compare;
    private long _sliceEndsAt;

    public GameTables(Action<string> log)
    {
        _log = log;
        _isSliceSpent = () => Stopwatch.GetTimestamp() >= _sliceEndsAt;
    }

    /// <summary>Hooks the game's table load, unless the game has loaded them already: any mod could have changed them first.</summary>
    public void Hook(PatchKit patches)
    {
        _hooked = this;

        if (GameSingleton.Existing<ConfigManager>() is { _towerWaveRewardTiers: not null })
        {
            _copy = new CopyState.Missing(NoCopyReason.LoadedFirst);
            _log("the game loaded its tables before the Mod Manager started, so they are not checked for changes no mod reported");

            return;
        }

        var isHooked = patches.Patch(typeof(GameTables), typeof(ConfigManager), nameof(ConfigManager.InitTowerWaveRewardTiers), Type.EmptyTypes, Priority.First,
            postfix: nameof(AfterLoad),
            done: "hooked the game's table load, to keep a copy of the tables",
            consequence: "the HEALTH tab cannot check the game's tables for changes no mod reported");

        if (!isHooked) _copy = new CopyState.Missing(NoCopyReason.Unreadable);
    }

    private static void AfterLoad(ConfigManager __instance) => _hooked?.TakeCopy(__instance.Pointer);

    /// <summary>
    /// Starts comparing the tables with the copy, and reads the value the game has now of each field a mod reported
    /// writing. A compare already running starts over. <see cref="Continue"/> carries it on.
    /// </summary>
    public DataCheck Start(IReadOnlyList<FieldClaim> claimed)
    {
        switch (_copy)
        {
            case CopyState.Missing missing:
                _compare = null;

                return new DataCheck.NoCopy(missing.Reason);
            case CopyState.Kept kept:
                _compare = new Compare(kept, claimed, _log);

                return new DataCheck.Checking();
            default:
                return new DataCheck.Waiting();
        }
    }

    /// <summary>Runs the compare for this frame's slice; what it found once it is done, else null.</summary>
    public TableReading? Continue()
    {
        if (_compare is not { } compare) return null;

        _sliceEndsAt = Stopwatch.GetTimestamp() + SliceTicks;

        if (!compare.Job.Advance(_isSliceSpent)) return null;

        _compare = null;

        return compare.Reading();
    }

    /// <summary>
    /// Copies every table. The game may load its tables again, so each load takes a new copy, laid out as the first.
    /// Never throws: a failure leaves no copy, and the HEALTH tab says the tables cannot be read.
    /// </summary>
    private void TakeCopy(IntPtr config)
    {
        try
        {
            var watch = Stopwatch.StartNew();
            var shapes = _copy is CopyState.Kept kept ? kept.Tables.Select(table => table.Shape).ToList() : TableMemory.Learn(IL2CPP.il2cpp_object_get_class(config), _log);
            var tables = new List<KeptTable>();
            var rows = 0;

            foreach (var shape in shapes)
            {
                if (CopyOf(shape, config) is not { } copy) continue;

                tables.Add(new KeptTable(shape, copy));
                rows += copy.Count;
            }

            if (tables.Count == 0)
            {
                _copy = new CopyState.Missing(NoCopyReason.Unreadable);
                _log("found none of the game's tables where they should be, so they are not checked for changes no mod reported");

                return;
            }

            _copy = new CopyState.Kept(tables);
            _log("kept a copy of " + tables.Count + " of the game's tables, " + rows + " rows, in " + watch.ElapsedMilliseconds + " ms");
        }
        catch (Exception e)
        {
            _copy = new CopyState.Missing(NoCopyReason.Unreadable);
            _log("could not copy the game's tables, so they are not checked for changes no mod reported: " + e);
        }
    }

    private TableCopy? CopyOf(TableShape shape, IntPtr config)
    {
        try
        {
            var copy = new TableCopy(shape.Table, shape.Fields);

            if (TableMemory.Read(shape, config, (key, _, cells) => copy.Add(key, cells)) != null) return copy;

            _log("left out the game's table " + shape.Table + ": it is not laid out as expected");
        }
        catch (Exception e)
        {
            _log("left out the game's table " + shape.Table + ": " + e.Message);
        }

        return null;
    }

    private sealed record KeptTable(TableShape Shape, TableCopy Copy);

    private abstract record CopyState
    {
        private CopyState()
        {
        }

        /// <summary>The game has not loaded its tables yet.</summary>
        public sealed record Waiting : CopyState;

        public sealed record Missing(NoCopyReason Reason) : CopyState;

        public sealed record Kept(IReadOnlyList<KeptTable> Tables) : CopyState;
    }

    /// <summary>One compare of every table with the copy, one table a step. A new copy taken meanwhile is compared next time.</summary>
    private sealed class Compare
    {
        private readonly IReadOnlyList<KeptTable> _tables;
        private readonly Action<string> _log;

        /// <summary>For each table, by row, the fields mods reported writing there, as they named the row and the field.</summary>
        private readonly Dictionary<RowKey, List<(string Row, string Field, int Column)>>[] _claimed;

        private readonly List<TableChange> _changes = new();
        private readonly Dictionary<(string Table, string Row, string Field), string?> _current = new();
        private int _compared;
        private int _rows;

        public Compare(CopyState.Kept kept, IReadOnlyList<FieldClaim> claimed, Action<string> log)
        {
            _tables = kept.Tables;
            _log = log;
            _claimed = ClaimedByTable(kept.Tables, claimed);
            Job = new SlicedJob(Steps());
        }

        public SlicedJob Job { get; }

        public TableReading Reading() => new(_compared, _rows, _changes, _current);

        private IEnumerable<bool> Steps()
        {
            for (var index = 0; index < _tables.Count; index++)
            {
                CompareTable(index);

                yield return true;
            }
        }

        private void CompareTable(int index)
        {
            var (shape, copy) = _tables[index];
            var claimed = _claimed[index];

            try
            {
                var config = GameSingleton.Pointer<ConfigManager>();

                if (config == IntPtr.Zero) return;

                var diff = new TableDiff(copy);
                var rows = TableMemory.Read(shape, config, (key, row, cells) =>
                {
                    diff.Row(key, cells);

                    if (!claimed.TryGetValue(key, out var fields)) return;

                    foreach (var (name, field, column) in fields) _current[(shape.Table, name, field)] = TableMemory.ReportedValue(row, shape.Columns[column], cells[column]);
                });

                if (rows is not { } count)
                {
                    _log("left the game's table " + shape.Table + " out of the check: it is no longer laid out as it was");

                    return;
                }

                _changes.AddRange(diff.Finish());
                _compared++;
                _rows += count;
            }
            catch (Exception e)
            {
                _log("left the game's table " + shape.Table + " out of the check: " + e.Message);
            }
        }

        private static Dictionary<RowKey, List<(string Row, string Field, int Column)>>[] ClaimedByTable(IReadOnlyList<KeptTable> tables, IReadOnlyList<FieldClaim> claimed)
        {
            var byTable = tables.Select(_ => new Dictionary<RowKey, List<(string Row, string Field, int Column)>>()).ToArray();
            var indexOf = tables.Select((table, index) => (table.Shape.Table, index)).ToDictionary(pair => pair.Table, pair => pair.index, StringComparer.Ordinal);

            foreach (var claim in claimed)
            {
                if (!indexOf.TryGetValue(claim.Table, out var index)) continue;

                var shape = tables[index].Shape;
                var column = FieldIndex(shape.Fields, claim.Field);

                if (column < 0 || KeyOf(shape, claim.Row) is not { } key) continue;

                if (!byTable[index].TryGetValue(key, out var fields)) byTable[index][key] = fields = new List<(string Row, string Field, int Column)>();

                fields.Add((claim.Row, claim.Field, column));
            }

            return byTable;
        }

        private static int FieldIndex(IReadOnlyList<TableField> fields, string name)
        {
            for (var index = 0; index < fields.Count; index++)
            {
                if (fields[index].Name == name) return index;
            }

            return -1;
        }

        /// <summary>The row a mod named, as the table keys it; null for a number that is not one.</summary>
        private static RowKey? KeyOf(TableShape shape, string row)
        {
            if (shape.IsTextKey) return RowKey.Of(row);

            return long.TryParse(row, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? RowKey.Of(number) : null;
        }
    }
}
