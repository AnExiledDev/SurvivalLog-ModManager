using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// The game's tables as they sit in memory: where ConfigManager keeps each one, how its Dictionary's entries are laid
/// out, and where each field of its row class sits, all from the game's type data. Reads a table's rows straight from
/// memory, so a check of every table makes no interop object per row, and checks each object's class before reading it.
/// Pointers are only good within the frame they were read in.
/// </summary>
internal static unsafe class TableMemory
{
    public delegate void RowSink(RowKey key, IntPtr row, ReadOnlySpan<ulong> cells);

    private const int StaticField = 0x10;
    private const int LiteralField = 0x40;

    /// <summary>Longer than any text a table holds; a longer length means the memory is not a string.</summary>
    private const int MaxTextLength = 1 << 20;

    private static readonly Regex TableField = new("^_Config_.+_Dict$", RegexOptions.CultureInvariant);
    private static readonly Regex BackingField = new("^<(.+)>k__BackingField$", RegexOptions.CultureInvariant);

    /// <summary>The game sets these itself after it loads, so a change there is not a mod's.</summary>
    private static readonly HashSet<(string Table, string Field)> SetByTheGame = new()
    {
        ("Config_Item", "Icon"),
        ("Config_Item", "WebIcon"),
        ("Config_Item", "ItemDes2"),
        ("Config_Action", "WebIcon"),
    };

    /// <summary>
    /// Every table ConfigManager keeps in a Dictionary keyed by a number or by text, one per row class. A table laid
    /// out some other way is left out and logged.
    /// </summary>
    public static IReadOnlyList<TableShape> Learn(IntPtr configClass, Action<string> log)
    {
        var shapes = new List<TableShape>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var left = new List<string>();
        var iter = IntPtr.Zero;
        IntPtr field;

        while ((field = IL2CPP.il2cpp_class_get_fields(configClass, ref iter)) != IntPtr.Zero)
        {
            if ((IL2CPP.il2cpp_field_get_flags(field) & StaticField) != 0 || IL2CPP.il2cpp_field_get_name_(field) is not { } name || !TableField.IsMatch(name)) continue;

            var (shape, problem) = ShapeOf(field);

            if (shape == null) left.Add(name + " (" + problem + ")");
            else if (names.Add(shape.Table)) shapes.Add(shape);
        }

        if (left.Count > 0) log("left out " + left.Count + " of the game's tables that are not laid out as expected: " + string.Join(", ", left));

        return shapes;
    }

    /// <summary>The table's rows, each handed to <paramref name="sink"/>; how many there were, or null when the table is not laid out as learned.</summary>
    public static int? Read(TableShape shape, IntPtr config, RowSink sink)
    {
        var dictionary = *(IntPtr*)(config + shape.DictionaryAt);

        if (dictionary == IntPtr.Zero) return 0;
        if (!shape.TryOpen(dictionary, out var entries, out var used)) return null;

        Span<ulong> cells = shape.Columns.Count <= 256 ? stackalloc ulong[shape.Columns.Count] : new ulong[shape.Columns.Count];
        var rows = 0;

        for (var index = 0; index < used; index++)
        {
            var entry = entries + index * shape.EntrySize;
            var row = *(IntPtr*)(entry + shape.ValueAt);

            // A removed entry keeps its slot with a negative hash code.
            if (*(int*)entry < 0 || row == IntPtr.Zero) continue;
            if (!shape.IsRow(row)) return null;

            for (var column = 0; column < cells.Length; column++) cells[column] = CellOf(row, shape.Columns[column]);

            sink(KeyOf(shape, entry), row, cells);
            rows++;
        }

        return rows;
    }

    /// <summary>The value the row has now the way a mod reporting it would have written it; null when that cannot be known.</summary>
    public static string? ReportedValue(IntPtr row, TableColumn column, ulong cell)
    {
        if (column.Read != CellRead.Text) return TableValues.AsReported(column.Field, cell);

        var text = *(IntPtr*)(row + column.Offset);

        return text == IntPtr.Zero ? "null" : new string(Chars(text));
    }

    private static RowKey KeyOf(TableShape shape, IntPtr entry)
    {
        if (!shape.IsTextKey) return RowKey.Of(*(int*)(entry + shape.KeyAt));

        var text = *(IntPtr*)(entry + shape.KeyAt);

        return RowKey.Of(text == IntPtr.Zero ? "" : new string(Chars(text)));
    }

    private static (TableShape? Shape, string Problem) ShapeOf(IntPtr field)
    {
        var dictionaryClass = ClassOf(IL2CPP.il2cpp_field_get_type(field));

        if (dictionaryClass == IntPtr.Zero || IL2CPP.il2cpp_class_get_name_(dictionaryClass) != "Dictionary`2") return (null, "not a Dictionary");

        var entriesField = IL2CPP.il2cpp_class_get_field_from_name(dictionaryClass, "_entries");
        var countField = IL2CPP.il2cpp_class_get_field_from_name(dictionaryClass, "_count");

        if (entriesField == IntPtr.Zero || countField == IntPtr.Zero) return (null, "no _entries or _count");

        var entries = new ArrayCheck(ClassOf(IL2CPP.il2cpp_field_get_type(entriesField)));
        var entryClass = entries.ElementClass;
        var hashField = FieldOf(entryClass, "hashCode");
        var keyField = FieldOf(entryClass, "key");
        var valueField = FieldOf(entryClass, "value");

        if (hashField == IntPtr.Zero || keyField == IntPtr.Zero || valueField == IntPtr.Zero) return (null, "an entry without hashCode, key or value");

        // A struct's field offsets may count the object header it would have when boxed.
        var start = (int)IL2CPP.il2cpp_field_get_offset(hashField);
        var keyAt = (int)IL2CPP.il2cpp_field_get_offset(keyField) - start;
        var valueAt = (int)IL2CPP.il2cpp_field_get_offset(valueField) - start;
        var keyCode = TypeCode(IL2CPP.il2cpp_field_get_type(keyField));
        var valueType = IL2CPP.il2cpp_field_get_type(valueField);

        if (keyCode != Il2CppCode.Int32 && keyCode != Il2CppCode.String) return (null, "keyed by neither a number nor text");
        if (TypeCode(valueType) != Il2CppCode.Class) return (null, "rows that are not objects");
        if (keyAt <= 0 || valueAt <= 0 || valueAt + IntPtr.Size > entries.ElementSize) return (null, "an entry of an unexpected size");

        var rowClass = ClassOf(valueType);
        var table = IL2CPP.il2cpp_class_get_name_(rowClass) ?? "";

        if (table.Length == 0) return (null, "a row class with no name");

        return (new TableShape(table, (int)IL2CPP.il2cpp_field_get_offset(field), dictionaryClass, (int)IL2CPP.il2cpp_field_get_offset(entriesField), (int)IL2CPP.il2cpp_field_get_offset(countField), entries, keyAt, valueAt, keyCode == Il2CppCode.String, rowClass, ColumnsOf(table, rowClass)), "");
    }

    /// <summary>The row class's fields and its base classes', by property name.</summary>
    private static IReadOnlyList<TableColumn> ColumnsOf(string table, IntPtr rowClass)
    {
        var columns = new List<TableColumn>();

        for (var klass = rowClass; klass != IntPtr.Zero && !IsObjectClass(klass); klass = IL2CPP.il2cpp_class_get_parent(klass))
        {
            var iter = IntPtr.Zero;
            IntPtr field;

            while ((field = IL2CPP.il2cpp_class_get_fields(klass, ref iter)) != IntPtr.Zero)
            {
                var offset = (int)IL2CPP.il2cpp_field_get_offset(field);

                if ((IL2CPP.il2cpp_field_get_flags(field) & (StaticField | LiteralField)) != 0 || offset < 2 * IntPtr.Size || IL2CPP.il2cpp_field_get_name_(field) is not { } raw) continue;

                var name = BackingField.Match(raw) is { Success: true } match ? match.Groups[1].Value : raw;

                if (!SetByTheGame.Contains((table, name))) columns.Add(ColumnOf(name, offset, IL2CPP.il2cpp_field_get_type(field)));
            }
        }

        return columns;
    }

    private static TableColumn ColumnOf(string name, int offset, IntPtr type)
    {
        var code = TypeCode(type);

        switch (code)
        {
            case Il2CppCode.ValueType when ClassOf(type) is var klass && IL2CPP.il2cpp_class_is_enum(klass):
                var read = WholeRead(TypeCode(IL2CPP.il2cpp_class_enum_basetype(klass)));

                return read is { } whole
                    ? new TableColumn(new TableField(name, FieldKind.Whole) { Names = NamesOf(klass, whole) }, offset, whole)
                    : new TableColumn(new TableField(name, FieldKind.Other), offset, CellRead.Bytes, SizeOf(klass));
            case Il2CppCode.ValueType:
                return new TableColumn(new TableField(name, FieldKind.Other), offset, CellRead.Bytes, SizeOf(ClassOf(type)));
            case Il2CppCode.GenericInstance when ClassOf(type) is var klass && IL2CPP.il2cpp_class_is_valuetype(klass):
                return new TableColumn(new TableField(name, FieldKind.Other), offset, CellRead.Bytes, SizeOf(klass));
            case Il2CppCode.GenericInstance when ClassOf(type) is var klass && IL2CPP.il2cpp_class_get_name_(klass) == "List`1" && ListOf(klass) is { } list:
                return new TableColumn(new TableField(name, FieldKind.Other), offset, CellRead.Items) { Items = list };
            case Il2CppCode.Array when ArrayOf(ClassOf(type)) is { } array:
                return new TableColumn(new TableField(name, FieldKind.Other), offset, CellRead.Items) { Items = array };
            case Il2CppCode.Boolean:
                return new TableColumn(new TableField(name, FieldKind.Flag), offset, CellRead.Flag);
            case Il2CppCode.Single:
                return new TableColumn(new TableField(name, FieldKind.Float), offset, CellRead.Float);
            case Il2CppCode.Double:
                return new TableColumn(new TableField(name, FieldKind.Double), offset, CellRead.Double);
            case Il2CppCode.String:
                return new TableColumn(new TableField(name, FieldKind.Text), offset, CellRead.Text);
        }

        return WholeRead(code) is { } number
            ? new TableColumn(new TableField(name, FieldKind.Whole), offset, number)
            : new TableColumn(new TableField(name, FieldKind.Other), offset, CellRead.Pointer);
    }

    private static CellRead? WholeRead(int code) => code switch
    {
        Il2CppCode.SByte => CellRead.Int8,
        Il2CppCode.Byte => CellRead.UInt8,
        Il2CppCode.Int16 => CellRead.Int16,
        Il2CppCode.UInt16 or Il2CppCode.Char => CellRead.UInt16,
        Il2CppCode.Int32 => CellRead.Int32,
        Il2CppCode.UInt32 => CellRead.UInt32,
        Il2CppCode.Int64 or Il2CppCode.UInt64 or Il2CppCode.IntPtr or Il2CppCode.UIntPtr => CellRead.Int64,
        _ => null,
    };

    /// <summary>Each value's name, from the enum's constants. The first name of a value wins.</summary>
    private static IReadOnlyDictionary<long, string>? NamesOf(IntPtr enumClass, CellRead read)
    {
        var names = new Dictionary<long, string>();
        var iter = IntPtr.Zero;
        IntPtr field;

        while ((field = IL2CPP.il2cpp_class_get_fields(enumClass, ref iter)) != IntPtr.Zero)
        {
            if ((IL2CPP.il2cpp_field_get_flags(field) & LiteralField) == 0 || IL2CPP.il2cpp_field_get_name_(field) is not { } name) continue;

            ulong value = 0;

            IL2CPP.il2cpp_field_static_get_value(field, &value);
            names.TryAdd(Whole(read, (IntPtr)(&value)), name);
        }

        return names.Count > 0 ? names : null;
    }

    /// <summary>A List's item array and size, or null when the List is not laid out as expected (it is then compared by which List it is).</summary>
    private static ItemsShape? ListOf(IntPtr listClass)
    {
        var items = IL2CPP.il2cpp_class_get_field_from_name(listClass, "_items");
        var size = IL2CPP.il2cpp_class_get_field_from_name(listClass, "_size");

        if (items == IntPtr.Zero || size == IntPtr.Zero) return null;

        var itemsAt = (int)IL2CPP.il2cpp_field_get_offset(items);
        var sizeAt = (int)IL2CPP.il2cpp_field_get_offset(size);
        var array = ArrayOf(ClassOf(IL2CPP.il2cpp_field_get_type(items)));

        return itemsAt > 0 && sizeAt > 0 && array != null ? array with { ListClass = listClass, ItemsAt = itemsAt, SizeAt = sizeAt } : null;
    }

    private static ItemsShape? ArrayOf(IntPtr arrayClass)
    {
        if (arrayClass == IntPtr.Zero) return null;

        var check = new ArrayCheck(arrayClass);

        if (check.ElementClass == IntPtr.Zero || check.ElementSize <= 0) return null;

        var item = IL2CPP.il2cpp_class_is_valuetype(check.ElementClass) ? CellRead.Bytes
            : TypeCode(IL2CPP.il2cpp_class_get_type(check.ElementClass)) == Il2CppCode.String ? CellRead.Text
            : CellRead.Pointer;

        return new ItemsShape(check, item);
    }

    private static ulong CellOf(IntPtr row, TableColumn column)
    {
        var at = row + column.Offset;

        return column.Read switch
        {
            CellRead.Flag => TableValues.OfFlag(*(byte*)at != 0),
            CellRead.Float => TableValues.OfFloat(*(float*)at),
            CellRead.Double => TableValues.OfDouble(*(double*)at),
            CellRead.Text => TextPrint(*(IntPtr*)at),
            CellRead.Bytes => Fingerprint.Of(new ReadOnlySpan<byte>((void*)at, column.Size)),
            CellRead.Items => column.Items is { } items ? ItemsPrint(*(IntPtr*)at, items) : AddressOf(*(IntPtr*)at),
            CellRead.Pointer => AddressOf(*(IntPtr*)at),
            _ => TableValues.OfWhole(Whole(column.Read, at)),
        };
    }

    private static long Whole(CellRead read, IntPtr at) => read switch
    {
        CellRead.Int8 => *(sbyte*)at,
        CellRead.UInt8 => *(byte*)at,
        CellRead.Int16 => *(short*)at,
        CellRead.UInt16 => *(ushort*)at,
        CellRead.Int32 => *(int*)at,
        CellRead.UInt32 => *(uint*)at,
        _ => *(long*)at,
    };

    private static ulong TextPrint(IntPtr text) => text == IntPtr.Zero ? 0 : Fingerprint.Of(Chars(text));

    private static ReadOnlySpan<char> Chars(IntPtr text)
    {
        var length = *(int*)(text + 2 * IntPtr.Size);

        return length is >= 0 and <= MaxTextLength ? new ReadOnlySpan<char>((void*)(text + 2 * IntPtr.Size + sizeof(int)), length) : ReadOnlySpan<char>.Empty;
    }

    /// <summary>What a List or array holds, item by item; an object that is not laid out as expected is compared by which object it is.</summary>
    private static ulong ItemsPrint(IntPtr value, ItemsShape shape)
    {
        if (value == IntPtr.Zero) return 0;

        var array = value;
        long count = -1;

        if (shape.ListClass != IntPtr.Zero)
        {
            if (*(IntPtr*)value != shape.ListClass) return AddressOf(value);

            array = *(IntPtr*)(value + shape.ItemsAt);
            count = *(int*)(value + shape.SizeAt);

            if (array == IntPtr.Zero) return count == 0 ? Fingerprint.Of(ReadOnlySpan<byte>.Empty) : AddressOf(value);
        }

        if (!shape.Array.Fits(array)) return AddressOf(value);

        var length = *(long*)(array + 3 * IntPtr.Size);

        if (count < 0) count = length;
        if (count > length) return AddressOf(value);

        var data = array + 4 * IntPtr.Size;
        var size = shape.Array.ElementSize;
        var print = new Fingerprint();

        print.Add((ulong)count);

        for (var index = 0; index < count; index++)
        {
            var item = data + (int)index * size;

            if (shape.Item == CellRead.Bytes) print.Add(new ReadOnlySpan<byte>((void*)item, size));
            else if (shape.Item == CellRead.Text) print.Add(TextPrint(*(IntPtr*)item));
            else print.Add(AddressOf(*(IntPtr*)item));
        }

        return print.Value;
    }

    private static ulong AddressOf(IntPtr value) => unchecked((ulong)(long)value);

    private static IntPtr FieldOf(IntPtr klass, string name) => klass == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_get_field_from_name(klass, name);

    private static IntPtr ClassOf(IntPtr type) => type == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_from_type(type);

    private static int TypeCode(IntPtr type) => type == IntPtr.Zero ? -1 : IL2CPP.il2cpp_type_get_type(type);

    private static int SizeOf(IntPtr valueClass)
    {
        uint align = 0;

        return valueClass == IntPtr.Zero ? 0 : Math.Max(0, IL2CPP.il2cpp_class_value_size(valueClass, ref align));
    }

    private static bool IsObjectClass(IntPtr klass) => IL2CPP.il2cpp_class_get_parent(klass) == IntPtr.Zero;

    /// <summary>The IL2CPP type codes the tables use.</summary>
    private static class Il2CppCode
    {
        public const int Boolean = 0x02;
        public const int Char = 0x03;
        public const int SByte = 0x04;
        public const int Byte = 0x05;
        public const int Int16 = 0x06;
        public const int UInt16 = 0x07;
        public const int Int32 = 0x08;
        public const int UInt32 = 0x09;
        public const int Int64 = 0x0a;
        public const int UInt64 = 0x0b;
        public const int Single = 0x0c;
        public const int Double = 0x0d;
        public const int String = 0x0e;
        public const int ValueType = 0x11;
        public const int Class = 0x12;
        public const int GenericInstance = 0x15;
        public const int IntPtr = 0x18;
        public const int UIntPtr = 0x19;
        public const int Array = 0x1d;
    }
}

internal enum CellRead
{
    Flag,
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    Float,
    Double,
    Text,

    /// <summary>A struct kept in the row, by its bytes.</summary>
    Bytes,

    /// <summary>A List or array, by what it holds.</summary>
    Items,

    /// <summary>Any other object, by which object it is.</summary>
    Pointer,
}

/// <param name="Size">How many bytes a struct kept in the row takes.</param>
internal sealed record TableColumn(TableField Field, int Offset, CellRead Read, int Size = 0)
{
    public ItemsShape? Items { get; init; }
}

/// <param name="ListClass">The List class, with where its item array and size sit; zero for a plain array.</param>
/// <param name="Item">Bytes, Text or Pointer: how each item is compared.</param>
internal sealed record ItemsShape(ArrayCheck Array, CellRead Item)
{
    public IntPtr ListClass { get; init; }

    public int ItemsAt { get; init; }

    public int SizeAt { get; init; }
}

/// <summary>
/// That an object is an array of the expected items. Its class is the one the type data names; failing that, the first
/// other class seen is taken when its items are the expected class and size, and kept.
/// </summary>
internal sealed unsafe class ArrayCheck
{
    private readonly IntPtr _arrayClass;
    private IntPtr _seen;

    public ArrayCheck(IntPtr arrayClass)
    {
        _arrayClass = arrayClass;
        ElementClass = arrayClass == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_class_get_element_class(arrayClass);
        ElementSize = arrayClass == IntPtr.Zero ? 0 : IL2CPP.il2cpp_array_element_size(arrayClass);
    }

    public IntPtr ElementClass { get; }

    public int ElementSize { get; }

    public bool Fits(IntPtr array)
    {
        var arrayClass = *(IntPtr*)array;

        if (arrayClass == _arrayClass || arrayClass == _seen) return true;
        if (_seen != IntPtr.Zero || IL2CPP.il2cpp_array_element_size(arrayClass) != ElementSize || IL2CPP.il2cpp_class_get_element_class(arrayClass) != ElementClass) return false;

        _seen = arrayClass;

        return true;
    }
}

/// <summary>Where one table sits and how its rows are laid out.</summary>
internal sealed unsafe class TableShape
{
    private readonly IntPtr _dictionaryClass;
    private readonly int _entriesAt;
    private readonly int _countAt;
    private readonly ArrayCheck _entries;
    private readonly IntPtr _rowClass;
    private IntPtr _rowSubclass;

    public TableShape(string table, int dictionaryAt, IntPtr dictionaryClass, int entriesAt, int countAt, ArrayCheck entries, int keyAt, int valueAt, bool isTextKey, IntPtr rowClass, IReadOnlyList<TableColumn> columns)
    {
        Table = table;
        DictionaryAt = dictionaryAt;
        _dictionaryClass = dictionaryClass;
        _entriesAt = entriesAt;
        _countAt = countAt;
        _entries = entries;
        KeyAt = keyAt;
        ValueAt = valueAt;
        IsTextKey = isTextKey;
        _rowClass = rowClass;
        Columns = columns;
        Fields = columns.Select(column => column.Field).ToList();
    }

    /// <summary>The row class's name (Config_Item), as mods name the table when they report a change.</summary>
    public string Table { get; }

    /// <summary>Where ConfigManager keeps the table's Dictionary.</summary>
    public int DictionaryAt { get; }

    public int EntrySize => _entries.ElementSize;

    public int KeyAt { get; }

    public int ValueAt { get; }

    public bool IsTextKey { get; }

    public IReadOnlyList<TableColumn> Columns { get; }

    public IReadOnlyList<TableField> Fields { get; }

    /// <summary>The entry array and how many of its slots were ever used; false when the Dictionary is not laid out as learned.</summary>
    public bool TryOpen(IntPtr dictionary, out IntPtr entries, out int used)
    {
        entries = IntPtr.Zero;
        used = 0;

        if (*(IntPtr*)dictionary != _dictionaryClass) return false;

        var array = *(IntPtr*)(dictionary + _entriesAt);
        var count = *(int*)(dictionary + _countAt);

        if (array == IntPtr.Zero) return count == 0;
        if (!_entries.Fits(array)) return false;

        var length = *(long*)(array + 3 * IntPtr.Size);

        if (count < 0 || count > length) return false;

        entries = array + 4 * IntPtr.Size;
        used = count;

        return true;
    }

    /// <summary>Whether the object is a row of this table, or of a class made from its row class.</summary>
    public bool IsRow(IntPtr row)
    {
        var klass = *(IntPtr*)row;

        if (klass == _rowClass || klass == _rowSubclass) return true;
        if (!IL2CPP.il2cpp_class_is_assignable_from(_rowClass, klass)) return false;

        _rowSubclass = klass;

        return true;
    }
}
