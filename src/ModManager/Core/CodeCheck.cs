using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using ExiledToolkit;

namespace ModManager.Core;

/// <summary>A part of the game's code module, where it sits once loaded (as an offset from the module's start) and in the file.</summary>
/// <param name="Rva">Where it starts, from the module's start.</param>
/// <param name="Size">How many bytes of it are in memory.</param>
/// <param name="FileOffset">Where its bytes start in the file.</param>
/// <param name="FileSize">How many bytes of it the file holds. Past them, memory starts out as zeros.</param>
public sealed record ImageSection(string Name, uint Rva, uint Size, uint FileOffset, uint FileSize, bool IsCode)
{
    /// <summary>The bytes the file and memory both hold.</summary>
    public uint Compared => Math.Min(Size, FileSize);
}

/// <summary>A table in the module, as an offset from its start and a size in bytes.</summary>
public readonly record struct ImageRange(uint Rva, uint Size)
{
    public uint End => Rva + Size;
}

/// <summary>
/// What the PE headers of GameAssembly.dll say: where the module wants to load, when it was built, its parts, and where
/// its relocation table and its function table (.pdata) are. The headers in memory are the file's, except that Windows
/// writes the address it really loaded the module at over <see cref="ImageBase"/>.
/// </summary>
public sealed record CodeImage(ulong ImageBase, uint TimeStamp, uint SizeOfImage, IReadOnlyList<ImageSection> Sections, ImageRange Relocations, ImageRange Functions)
{
    /// <summary>
    /// The part IL2CPP puts the game's compiled C# in. The game's other code (.text) is IL2CPP's runtime, which BepInEx
    /// hooks as it starts, so it is not checked.
    /// </summary>
    public const string ManagedSection = "il2cpp";

    public const int HeaderBytes = 0x1000;

    private const int PeOffsetAt = 0x3c;
    private const int FileHeaderSize = 20;
    private const int SectionHeaderSize = 40;
    private const ushort Pe32Plus = 0x20b;
    private const uint ExecutableCode = 0x20000000;

    public ImageSection? ManagedCode => Sections.FirstOrDefault(section => section.Name == ManagedSection && section.IsCode && section.Compared > 0);

    /// <returns>The headers, or null when <paramref name="headers"/> holds no 64-bit PE header that fits in it.</returns>
    public static CodeImage? Read(ReadOnlySpan<byte> headers)
    {
        if (PeAt(headers) is not { } pe) return null;

        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(headers.Slice(pe + 6));
        var timeStamp = BinaryPrimitives.ReadUInt32LittleEndian(headers.Slice(pe + 8));
        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(headers.Slice(pe + 20));
        var optional = pe + 4 + FileHeaderSize;

        if (optionalSize < 112 || optional + optionalSize > headers.Length) return null;

        var optionalHeader = headers.Slice(optional, optionalSize);

        if (BinaryPrimitives.ReadUInt16LittleEndian(optionalHeader) != Pe32Plus) return null;

        var directories = BinaryPrimitives.ReadUInt32LittleEndian(optionalHeader.Slice(108));
        var sectionsAt = optional + optionalSize;

        if ((long)sectionsAt + (long)sectionCount * SectionHeaderSize > headers.Length) return null;

        var sections = new List<ImageSection>();

        for (var index = 0; index < sectionCount; index++)
        {
            var header = headers.Slice(sectionsAt + index * SectionHeaderSize, SectionHeaderSize);
            var name = header.Slice(0, 8);
            var length = name.IndexOf((byte)0);
            sections.Add(new ImageSection(
                System.Text.Encoding.ASCII.GetString(length < 0 ? name : name.Slice(0, length)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(12)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16)),
                (BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(36)) & ExecutableCode) != 0));
        }

        return new CodeImage(
            BinaryPrimitives.ReadUInt64LittleEndian(optionalHeader.Slice(24)),
            timeStamp,
            BinaryPrimitives.ReadUInt32LittleEndian(optionalHeader.Slice(56)),
            sections,
            Directory(optionalHeader, directories, 5),
            Directory(optionalHeader, directories, 3));
    }

    /// <returns>Where <paramref name="range"/> starts in the file, or null when no part of the file holds all of it.</returns>
    public uint? FileOffsetOf(ImageRange range)
    {
        foreach (var section in Sections)
        {
            if (range.Rva >= section.Rva && range.End <= section.Rva + section.FileSize) return section.FileOffset + (range.Rva - section.Rva);
        }

        return null;
    }

    private static int? PeAt(ReadOnlySpan<byte> headers)
    {
        if (headers.Length < PeOffsetAt + 4 || headers[0] != 'M' || headers[1] != 'Z') return null;

        var pe = BinaryPrimitives.ReadInt32LittleEndian(headers.Slice(PeOffsetAt));

        if (pe < 0 || pe > headers.Length - 4 - FileHeaderSize) return null;

        return headers[pe] == 'P' && headers[pe + 1] == 'E' && headers[pe + 2] == 0 && headers[pe + 3] == 0 ? pe : null;
    }

    private static ImageRange Directory(ReadOnlySpan<byte> optional, uint count, int index)
    {
        var at = 112 + index * 8;

        if (count <= index || at + 8 > optional.Length) return default;

        return new ImageRange(BinaryPrimitives.ReadUInt32LittleEndian(optional.Slice(at)), BinaryPrimitives.ReadUInt32LittleEndian(optional.Slice(at + 4)));
    }
}

/// <summary>A place in the module where Windows adds the distance between where it loaded the module and where the module wanted to load.</summary>
public readonly record struct Relocation(uint Rva, int Size)
{
    public uint End => Rva + (uint)Size;
}

/// <summary>
/// Reads the module's relocation table, so its file's bytes can be put the way Windows put them in memory. The table is
/// a list of blocks, one per 4 KB page: the page, the block's size, then a 2-byte entry per place, its kind in the top
/// 4 bits and where it is in the page in the rest.
/// </summary>
public static class Relocations
{
    private const int Dir64 = 10;
    private const int HighLow = 3;

    /// <returns>The places from <paramref name="start"/> up to <paramref name="end"/>, sorted.</returns>
    public static IReadOnlyList<Relocation> In(ReadOnlySpan<byte> table, uint start, uint end)
    {
        var found = new List<Relocation>();
        var at = 0;

        while (at + 8 <= table.Length)
        {
            var page = BinaryPrimitives.ReadUInt32LittleEndian(table.Slice(at));
            var size = BinaryPrimitives.ReadInt32LittleEndian(table.Slice(at + 4));

            if (size < 8 || at + size > table.Length) break;

            if (page + 0x1000 > start && page < end)
            {
                for (var entry = at + 8; entry + 2 <= at + size; entry += 2)
                {
                    var value = BinaryPrimitives.ReadUInt16LittleEndian(table.Slice(entry));
                    var kind = value >> 12;
                    var rva = page + (uint)(value & 0xfff);

                    if ((kind == Dir64 || kind == HighLow) && rva >= start && rva < end) found.Add(new Relocation(rva, kind == Dir64 ? 8 : 4));
                }
            }

            at += size;
        }

        found.Sort((a, b) => a.Rva.CompareTo(b.Rva));

        return found;
    }

    /// <returns><paramref name="end"/>, moved past a place it would cut in two, so each place is read whole in one piece.</returns>
    public static uint SafeEnd(uint end, IReadOnlyList<Relocation> relocations)
    {
        var index = FirstAtOrAfter(relocations, end) - 1;

        return index >= 0 && relocations[index].End > end ? relocations[index].End : end;
    }

    /// <summary>Adds <paramref name="delta"/> at each place inside <paramref name="bytes"/>, which start at <paramref name="rva"/>.</summary>
    public static void Apply(Span<byte> bytes, uint rva, IReadOnlyList<Relocation> relocations, long delta)
    {
        if (delta == 0) return;

        for (var index = FirstAtOrAfter(relocations, rva); index < relocations.Count; index++)
        {
            var place = relocations[index];
            var at = (long)place.Rva - rva;

            if (at + place.Size > bytes.Length) break;

            var slice = bytes.Slice((int)at, place.Size);

            if (place.Size == 8) BinaryPrimitives.WriteUInt64LittleEndian(slice, BinaryPrimitives.ReadUInt64LittleEndian(slice) + (ulong)delta);
            else BinaryPrimitives.WriteUInt32LittleEndian(slice, BinaryPrimitives.ReadUInt32LittleEndian(slice) + (uint)delta);
        }
    }

    private static int FirstAtOrAfter(IReadOnlyList<Relocation> relocations, uint rva)
    {
        var low = 0;
        var high = relocations.Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (relocations[middle].Rva < rva) low = middle + 1;
            else high = middle;
        }

        return low;
    }
}

/// <summary>A stretch of the game's code whose bytes in memory are not the file's.</summary>
public readonly record struct CodeRun(uint Rva, int Length)
{
    public uint End => Rva + (uint)Length;
}

public static class CodeDiff
{
    private const int BlockSize = 4096;

    /// <summary>
    /// Adds each stretch where <paramref name="actual"/> differs from <paramref name="expected"/> to <paramref name="runs"/>.
    /// A stretch that carries on from the last one found, in an earlier piece, joins it.
    /// </summary>
    /// <returns>How many bytes differ.</returns>
    public static int Add(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, uint rva, List<CodeRun> runs)
    {
        var length = Math.Min(expected.Length, actual.Length);
        var differing = 0;

        for (var block = 0; block < length; block += BlockSize)
        {
            var size = Math.Min(BlockSize, length - block);

            if (expected.Slice(block, size).SequenceEqual(actual.Slice(block, size))) continue;

            for (var at = block; at < block + size; at++)
            {
                if (expected[at] == actual[at]) continue;

                differing++;

                var here = rva + (uint)at;

                if (runs.Count > 0 && runs[runs.Count - 1].End == here) runs[runs.Count - 1] = new CodeRun(runs[runs.Count - 1].Rva, runs[runs.Count - 1].Length + 1);
                else runs.Add(new CodeRun(here, 1));
            }
        }

        return differing;
    }
}

/// <summary>Reads <paramref name="length"/> bytes of the loaded module, <paramref name="rva"/> bytes from its start; empty when they are not all inside it.</summary>
public delegate ReadOnlySpan<byte> ImageReader(uint rva, int length);

/// <summary>
/// Finds the start of the method a byte of code belongs to, from the module's function table (.pdata): one 12-byte
/// entry per piece of compiled code, sorted by start, giving the piece's start, its end and its unwind info. A long
/// method can be split over several pieces whose unwind info chains back to the method's first one.
/// </summary>
public static class FunctionStarts
{
    private const int EntrySize = 12;
    private const int ChainInfo = 0x4;
    private const int Indirect = 0x1;
    private const int MostChainLinks = 32;

    /// <returns>Where the method holding <paramref name="rva"/> starts, or null when the table has no piece there (a small method that needs none) or can't be read.</returns>
    public static uint? Of(ImageReader read, ImageRange table, uint rva)
    {
        var low = 0;
        var high = (int)(table.Size / EntrySize) - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var bytes = read(table.Rva + (uint)(middle * EntrySize), EntrySize);

            if (bytes.Length < EntrySize) return null;

            var (begin, end, unwind) = Entry(bytes);

            if (rva < begin) high = middle - 1;
            else if (rva >= end) low = middle + 1;
            else return RootOf(read, begin, unwind);
        }

        return null;
    }

    private static uint? RootOf(ImageReader read, uint begin, uint unwind)
    {
        for (var link = 0; link < MostChainLinks; link++)
        {
            if ((unwind & Indirect) != 0) return null;

            var header = read(unwind, 4);

            if (header.Length < 4) return null;

            if (((header[0] >> 3) & ChainInfo) == 0) return begin;

            // The unwind codes are 2 bytes each, padded to an even count; the entry of the piece this one belongs to follows them.
            var codes = (header[2] + 1) & ~1;
            var parent = read(unwind + 4 + (uint)(codes * 2), EntrySize);

            if (parent.Length < EntrySize) return null;

            (begin, _, unwind) = Entry(parent);
        }

        return null;
    }

    private static (uint Begin, uint End, uint Unwind) Entry(ReadOnlySpan<byte> bytes) =>
        (BinaryPrimitives.ReadUInt32LittleEndian(bytes), BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8)));
}

public enum CodeSource
{
    /// <summary>A Harmony patch: HarmonyX's record says the method is patched, and its hook is written over the method's start.</summary>
    Harmony,

    /// <summary>A native hook or a byte change a mod reported through Exiled's Toolkit.</summary>
    Claim,
}

/// <summary>Bytes some known change accounts for, in the method starting at <paramref name="Function"/>.</summary>
public sealed record CodeSpan(uint Function, uint Start, uint End, CodeSource Source)
{
    /// <summary>The bytes a hook is written over at a method's start, by Harmony or a mod.</summary>
    public static CodeSpan Hook(uint function, CodeSource source) => new(function, function, function + (uint)ClaimClashes.HookBytes, source);

    /// <summary>The spans of the native hooks and byte changes mods reported, for the methods found by each name.</summary>
    /// <param name="startsOf">Where each method of that "Type.Method" name starts; none when there is no such method.</param>
    public static IReadOnlyList<CodeSpan> OfClaims(ClaimSet claims, Func<string, IReadOnlyList<uint>> startsOf)
    {
        var spans = new List<CodeSpan>();

        foreach (var hook in claims.Hooks)
        {
            foreach (var start in startsOf(hook.Method)) spans.Add(Hook(start, CodeSource.Claim));
        }

        foreach (var bytes in claims.Bytes)
        {
            if (bytes.Offset < 0 || bytes.Length <= 0) continue;

            foreach (var start in startsOf(bytes.Method)) spans.Add(new CodeSpan(start, start + (uint)bytes.Offset, start + (uint)bytes.Offset + (uint)bytes.Length, CodeSource.Claim));
        }

        return spans;
    }
}

/// <summary>Changed bytes in one method that neither Harmony nor any mod's report accounts for.</summary>
/// <param name="Function">Where the method starts. For a small method the function table has no entry for, where the first change starts.</param>
/// <param name="Offset">How far into the method the first changed byte is.</param>
/// <param name="Runs">How many separate stretches changed.</param>
public sealed record UnreportedCode(uint Function, int Offset, int Bytes, int Runs)
{
    /// <summary>"Type.Method", or null when it could not be found.</summary>
    public string? Method { get; init; }
}

/// <summary>What one compare of the game's code with its file found.</summary>
/// <param name="Scanned">How many bytes of code were compared.</param>
/// <param name="Changed">How many methods have changed bytes.</param>
/// <param name="ByHarmony">How many of them Harmony patches account for.</param>
/// <param name="ByClaims">How many of them reports through Exiled's Toolkit account for.</param>
/// <param name="Unreported">The methods with changes nothing accounts for, sorted by where they start.</param>
public sealed record CodeReading(long Scanned, int Changed, int ByHarmony, int ByClaims, IReadOnlyList<UnreportedCode> Unreported)
{
    /// <summary>How many methods have only changes something accounts for.</summary>
    public int Reported => Changed - Unreported.Count;

    /// <summary>
    /// Sorts the changed bytes into the ones a known change accounts for and the rest, by method. Bytes outside every
    /// span are grouped by the method <paramref name="functionOf"/> puts them in, or by their own start when it can't say.
    /// </summary>
    public static CodeReading Explain(long scanned, IReadOnlyList<CodeRun> runs, IReadOnlyList<CodeSpan> known, Func<uint, uint?> functionOf)
    {
        var spans = known.Where(span => span.End > span.Start).OrderBy(span => span.Start).ToList();
        var byHarmony = new HashSet<uint>();
        var byClaims = new HashSet<uint>();
        var unreported = new Dictionary<uint, (uint First, int Bytes, int Runs)>();

        foreach (var run in runs)
        {
            var at = run.Rva;

            foreach (var span in spans)
            {
                if (span.End <= run.Rva || span.Start >= run.End) continue;

                (span.Source == CodeSource.Harmony ? byHarmony : byClaims).Add(span.Function);

                if (span.Start > at) Unreported(at, span.Start);

                at = Math.Max(at, span.End);
            }

            if (at < run.End) Unreported(at, run.End);
        }

        var changed = byHarmony.Concat(byClaims).Concat(unreported.Keys).Distinct().Count();
        var left = unreported
            .OrderBy(pair => pair.Key)
            .Select(pair => new UnreportedCode(pair.Key, (int)(pair.Value.First - pair.Key), pair.Value.Bytes, pair.Value.Runs))
            .ToList();

        return new CodeReading(scanned, changed, byHarmony.Count, byClaims.Count, left);

        void Unreported(uint start, uint end)
        {
            var function = functionOf(start) is { } found && found <= start ? found : start;

            unreported[function] = unreported.TryGetValue(function, out var so) ? (Math.Min(so.First, start), so.Bytes + (int)(end - start), so.Runs + 1) : (start, (int)(end - start), 1);
        }
    }

    /// <summary>The functions whose names are still needed.</summary>
    public IEnumerable<uint> Unnamed => Unreported.Where(code => code.Method == null).Select(code => code.Function);

    public CodeReading Named(Func<uint, string?> nameOf) => this with { Unreported = Unreported.Select(code => code with { Method = code.Method ?? nameOf(code.Function) }).ToList() };
}

/// <summary>What the check of the game's code found, or why there is nothing to show.</summary>
public abstract record CodeCheck
{
    public const int Shown = 20;

    private CodeCheck()
    {
    }

    /// <summary>The code is being compared with its file, in the background.</summary>
    public sealed record Checking : CodeCheck;

    public sealed record NoScan(NoScanReason Reason) : CodeCheck;

    /// <param name="Changers">Running mods whose code can change the game's code outside Harmony, sorted: the likely source of an unreported change.</param>
    public sealed record Done(CodeReading Reading, IReadOnlyList<string> Changers) : CodeCheck;
}

public enum NoScanReason
{
    /// <summary>The module, its file or its managed code can't be found or read. The log says which.</summary>
    Unreadable,

    /// <summary>The file was built at another time than the module the game loaded: the game was updated while it ran.</summary>
    FileChanged,

    /// <summary>Far more code differs than mods change, so the file can't be the one the game loaded.</summary>
    TooMany,
}
