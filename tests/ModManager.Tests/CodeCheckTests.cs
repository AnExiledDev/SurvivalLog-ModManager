using System.Buffers.Binary;
using ExiledToolkit;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A mod can change the game's compiled code without Harmony and without saying so, and then two mods can break the
/// same method with no sign of who did it. The game's code in memory is compared with GameAssembly.dll on disk: the
/// file's bytes are put where Windows put them, the changes Harmony and the mods' reports account for are left out,
/// and the rest is listed by the method it is in.
/// </summary>
public class CodeCheckTests
{
    private const ulong WantedBase = 0x180000000;
    private const uint Stamp = 0x6543210f;

    // ===== The module's headers =====

    [Fact]
    public void The_headers_give_where_the_module_loads_its_build_time_its_parts_and_its_tables()
    {
        var image = Assert.IsType<CodeImage>(CodeImage.Read(Headers()));

        Assert.Equal(WantedBase, image.ImageBase);
        Assert.Equal(Stamp, image.TimeStamp);
        Assert.Equal(0x500000u, image.SizeOfImage);
        Assert.Equal(new ImageRange(0x300000, 0x40), image.Relocations);
        Assert.Equal(new ImageRange(0x200000, 0x900), image.Functions);
        Assert.Equal(new[] { new ImageSection(".text", 0x1000, 0x8000, 0x400, 0x8000, true), new ImageSection("il2cpp", 0x10000, 0x6000, 0x8400, 0x5000, true) }, image.Sections);
    }

    [Fact]
    public void The_managed_code_is_the_il2cpp_part_and_only_the_bytes_the_file_holds_are_compared()
    {
        var code = Assert.IsType<ImageSection>(CodeImage.Read(Headers())?.ManagedCode);

        Assert.Equal("il2cpp", code.Name);
        Assert.Equal(0x5000u, code.Compared);
    }

    [Fact]
    public void An_il2cpp_part_that_is_not_code_is_not_the_managed_code()
    {
        Assert.Null(CodeImage.Read(Headers(isIl2CppCode: false))?.ManagedCode);
    }

    [Fact]
    public void Headers_that_are_not_a_64_bit_module_are_not_read()
    {
        var notMz = Headers();
        var notPe = Headers();
        var tooManySections = Headers();

        notMz[0] = (byte)'X';
        notPe[0x80] = (byte)'X';
        BinaryPrimitives.WriteUInt16LittleEndian(tooManySections.AsSpan(0x86), 200);

        Assert.Null(CodeImage.Read(notMz));
        Assert.Null(CodeImage.Read(notPe));
        Assert.Null(CodeImage.Read(Headers(magic: 0x10b)));
        Assert.Null(CodeImage.Read(tooManySections));
        Assert.Null(CodeImage.Read(Headers().AsSpan(0, 0x100)));
    }

    [Fact]
    public void A_table_is_found_in_the_file_only_when_one_part_of_the_file_holds_all_of_it()
    {
        var image = Assert.IsType<CodeImage>(CodeImage.Read(Headers()));

        Assert.Equal(0x8400u + 0x20u, image.FileOffsetOf(new ImageRange(0x10020, 0x10)));
        Assert.Null(image.FileOffsetOf(new ImageRange(0x14ff0, 0x20)));
        Assert.Null(image.FileOffsetOf(new ImageRange(0x400000, 0x10)));
    }

    // ===== Putting the file's bytes where Windows put them =====

    [Fact]
    public void Only_the_8_and_4_byte_places_inside_the_range_are_read_from_the_relocation_table_sorted()
    {
        var table = new List<byte>();

        Block(table, 0x1000, Entry(10, 0x010), Entry(3, 0x008), Entry(0, 0), Entry(10, 0x000), Entry(10, 0xff0), Entry(5, 0x020));
        Block(table, 0x3000, Entry(10, 0x000), Entry(10, 0x008));

        var found = Relocations.In(table.ToArray(), 0x1004, 0x2000);

        Assert.Equal(new[] { new Relocation(0x1008, 4), new Relocation(0x1010, 8), new Relocation(0x1ff0, 8) }, found);
    }

    [Fact]
    public void The_load_distance_is_added_at_each_place_and_a_place_past_the_end_is_left_alone()
    {
        var bytes = new byte[0x20];
        var places = new[] { new Relocation(0x1008, 4), new Relocation(0x1010, 8), new Relocation(0x101c, 8) };

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x08), 0x40001000);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x10), 0x140001000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x1c), 0x12345678);

        Relocations.Apply(bytes, 0x1000, places, 0x200000010);

        Assert.Equal(0x40001010u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x08)));
        Assert.Equal(0x340001010ul, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(0x10)));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x1c)));
    }

    [Fact]
    public void A_piece_never_ends_inside_a_place()
    {
        var places = new[] { new Relocation(0x1ffc, 8) };

        Assert.Equal(0x2004u, Relocations.SafeEnd(0x2000, places));
        Assert.Equal(0x1ffcu, Relocations.SafeEnd(0x1ffc, places));
        Assert.Equal(0x2008u, Relocations.SafeEnd(0x2008, places));
    }

    // ===== What differs =====

    [Fact]
    public void Each_stretch_of_changed_bytes_is_found_and_one_that_carries_on_into_the_next_piece_joins_it()
    {
        var runs = new List<CodeRun>();
        var first = new byte[16];
        var second = new byte[16];

        first[2] = first[3] = first[15] = 0xcc;
        second[0] = 0xcc;

        var changed = CodeDiff.Add(new byte[16], first, 0x100, runs) + CodeDiff.Add(new byte[16], second, 0x110, runs);

        Assert.Equal(4, changed);
        Assert.Equal(new[] { new CodeRun(0x102, 2), new CodeRun(0x10f, 2) }, runs);
    }

    [Fact]
    public void A_change_past_the_first_block_of_a_piece_is_found()
    {
        var runs = new List<CodeRun>();
        var actual = new byte[3 * 4096];

        actual[9000] = 0xe9;

        Assert.Equal(1, CodeDiff.Add(new byte[actual.Length], actual, 0x10000, runs));
        Assert.Equal(new[] { new CodeRun(0x10000 + 9000, 1) }, runs);
    }

    // ===== Which method a change is in =====

    [Theory]
    [InlineData(0x1050u, 0x1000u)]
    [InlineData(0x1000u, 0x1000u)]
    [InlineData(0x1120u, 0x1000u)]
    [InlineData(0x2000u, 0x2000u)]
    public void A_change_is_put_in_the_method_whose_piece_holds_it_following_a_split_method_back_to_its_start(uint rva, uint method)
    {
        Assert.Equal(method, FunctionStarts.Of(Read(Functions()), new ImageRange(0x100, 36), rva));
    }

    [Theory]
    [InlineData(0x1800u)]
    [InlineData(0x3000u)]
    [InlineData(0x0800u)]
    public void A_change_no_piece_holds_has_no_method(uint rva)
    {
        Assert.Null(FunctionStarts.Of(Read(Functions()), new ImageRange(0x100, 36), rva));
    }

    [Fact]
    public void A_function_table_that_cannot_be_read_gives_no_method()
    {
        Assert.Null(FunctionStarts.Of((_, _) => ReadOnlySpan<byte>.Empty, new ImageRange(0x100, 36), 0x1050));
    }

    // ===== Sorting the changes =====

    [Fact]
    public void Changes_Harmony_and_reports_account_for_are_left_out_and_the_rest_is_listed_by_method()
    {
        var runs = new[] { new CodeRun(0x1000, 14), new CodeRun(0x2000, 20), new CodeRun(0x3010, 2), new CodeRun(0x3020, 3), new CodeRun(0x4005, 1), new CodeRun(0x5005, 1) };
        var known = new[] { CodeSpan.Hook(0x1000, CodeSource.Harmony), CodeSpan.Hook(0x2000, CodeSource.Claim) };
        uint? FunctionOf(uint rva) => rva switch
        {
            >= 0x2000 and < 0x3000 => 0x2000,
            >= 0x3000 and < 0x4000 => 0x3000,
            >= 0x5000 and < 0x6000 => 0x6000,
            _ => null,
        };

        var reading = CodeReading.Explain(1234, runs, known, FunctionOf);

        Assert.Equal(1234, reading.Scanned);
        Assert.Equal(5, reading.Changed);
        Assert.Equal(1, reading.ByHarmony);
        Assert.Equal(1, reading.ByClaims);
        Assert.Equal(1, reading.Reported);
        Assert.Equal(new[] { new UnreportedCode(0x2000, 14, 6, 1), new UnreportedCode(0x3000, 0x10, 5, 2), new UnreportedCode(0x4005, 0, 1, 1), new UnreportedCode(0x5005, 0, 1, 1) }, reading.Unreported);
    }

    [Fact]
    public void A_change_between_two_known_spans_is_still_listed()
    {
        var known = new[] { new CodeSpan(0x1000, 0x1000, 0x1004, CodeSource.Claim), new CodeSpan(0x1000, 0x1008, 0x100c, CodeSource.Claim) };

        var reading = CodeReading.Explain(1, new[] { new CodeRun(0x1000, 12) }, known, _ => 0x1000);

        Assert.Equal(new[] { new UnreportedCode(0x1000, 4, 4, 1) }, reading.Unreported);
        Assert.Equal(1, reading.ByClaims);
        Assert.Equal(0, reading.Reported);
    }

    [Fact]
    public void With_everything_accounted_for_nothing_is_listed()
    {
        var reading = CodeReading.Explain(1, new[] { new CodeRun(0x1000, 14) }, new[] { CodeSpan.Hook(0x1000, CodeSource.Harmony) }, _ => 0x1000);

        Assert.Empty(reading.Unreported);
        Assert.Equal(1, reading.Reported);
    }

    [Fact]
    public void Named_methods_get_their_names_and_one_already_named_keeps_its_name()
    {
        var reading = new CodeReading(1, 3, 0, 0, new[] { new UnreportedCode(0x1000, 0, 1, 1), new UnreportedCode(0x2000, 0, 1, 1) { Method = "Gate.Shut" }, new UnreportedCode(0x3000, 0, 1, 1) });

        var named = reading.Named(rva => rva == 0x3000 ? null : "Door.Open");

        Assert.Equal(new string?[] { "Door.Open", "Gate.Shut", null }, named.Unreported.Select(code => code.Method));
        Assert.Equal(new[] { 0x3000u }, named.Unnamed);
    }

    [Fact]
    public void A_reported_hook_covers_the_start_of_each_method_by_that_name_and_a_byte_change_its_own_bytes()
    {
        var claims = new ClaimSet(
            Array.Empty<RowClaim>(),
            Array.Empty<FieldClaim>(),
            new[] { new HookClaim("com.a", "Door.Open") },
            new[] { new BytesClaim("com.b", "Door.Shut", 8, 4), new BytesClaim("com.c", "Door.Shut", -1, 4), new BytesClaim("com.d", "Door.Shut", 0, 0), new BytesClaim("com.e", "Door.Gone", 0, 4) });
        IReadOnlyList<uint> StartsOf(string method) => method switch
        {
            "Door.Open" => new uint[] { 0x1000, 0x5000 },
            "Door.Shut" => new uint[] { 0x2000 },
            _ => Array.Empty<uint>(),
        };

        var spans = CodeSpan.OfClaims(claims, StartsOf);

        Assert.Equal(new[] { new CodeSpan(0x1000, 0x1000, 0x1000 + ClaimClashes.HookBytes, CodeSource.Claim), new CodeSpan(0x5000, 0x5000, 0x5000 + ClaimClashes.HookBytes, CodeSource.Claim), new CodeSpan(0x2000, 0x2008, 0x200c, CodeSource.Claim) }, spans);
    }

    // ===== Sent to the page =====

    [Fact]
    public void The_code_check_is_sent_to_the_page_with_each_changed_method()
    {
        var unreported = new[] { new UnreportedCode(0x2000, 14, 6, 1) { Method = "Door.Open" }, new UnreportedCode(0x4005, 0, 1, 1) };
        var reading = new CodeReading(70L * 1024 * 1024 + 5, 3, 1, 1, unreported);

        var json = PageScript.CodeCheckJson(new CodeCheck.Done(reading, new[] { "com.a" }));

        Assert.Equal("{\"state\":\"done\",\"megabytes\":70,\"changed\":3,\"byHarmony\":1,\"byClaims\":1,\"reported\":1,\"unreportedCount\":2,\"unreported\":["
            + "{\"function\":\"0x2000\",\"offset\":14,\"bytes\":6,\"runs\":1,\"method\":\"Door.Open\"},{\"function\":\"0x4005\",\"offset\":0,\"bytes\":1,\"runs\":1,\"method\":null}],\"changers\":[\"com.a\"]}", json);
    }

    [Fact]
    public void Only_the_first_changed_methods_are_sent_with_the_count_of_all_of_them()
    {
        var unreported = Enumerable.Range(0, CodeCheck.Shown + 5).Select(index => new UnreportedCode((uint)(0x1000 + index * 0x100), 0, 1, 1)).ToList();

        using var json = System.Text.Json.JsonDocument.Parse(PageScript.CodeCheckJson(new CodeCheck.Done(new CodeReading(1, unreported.Count, 0, 0, unreported), Array.Empty<string>())));

        Assert.Equal(CodeCheck.Shown + 5, json.RootElement.GetProperty("unreportedCount").GetInt32());
        Assert.Equal(CodeCheck.Shown, json.RootElement.GetProperty("unreported").GetArrayLength());
    }

    [Theory]
    [InlineData(NoScanReason.FileChanged, "fileChanged")]
    [InlineData(NoScanReason.TooMany, "tooMany")]
    [InlineData(NoScanReason.Unreadable, "unreadable")]
    public void A_check_that_could_not_run_sends_why(NoScanReason reason, string sent)
    {
        Assert.Equal("{\"state\":\"noScan\",\"reason\":\"" + sent + "\"}", PageScript.CodeCheckJson(new CodeCheck.NoScan(reason)));
        Assert.Equal("{\"state\":\"checking\"}", PageScript.CodeCheckJson(new CodeCheck.Checking()));
    }

    // ===== Helpers =====

    /// <summary>
    /// The first 4 KB of a 64-bit module: .text, then il2cpp with 0x6000 bytes in memory of which the file holds 0x5000,
    /// a relocation table at 0x300000 and a function table at 0x200000.
    /// </summary>
    private static byte[] Headers(bool isIl2CppCode = true, ushort magic = 0x20b)
    {
        var bytes = new byte[CodeImage.HeaderBytes];
        var span = bytes.AsSpan();
        const int pe = 0x80;
        const int optional = pe + 24;
        const int sections = optional + 0xf0;

        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0x3c), pe);
        bytes[pe] = (byte)'P';
        bytes[pe + 1] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(pe + 4), 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(pe + 6), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(pe + 8), Stamp);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(pe + 20), 0xf0);
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(optional), magic);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(optional + 24), WantedBase);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(optional + 56), 0x500000);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(optional + 108), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(optional + 136), 0x200000);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(optional + 140), 0x900);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(optional + 152), 0x300000);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(optional + 156), 0x40);
        Section(span.Slice(sections), ".text", 0x8000, 0x1000, 0x8000, 0x400, true);
        Section(span.Slice(sections + 40), "il2cpp", 0x6000, 0x10000, 0x5000, 0x8400, isIl2CppCode);

        return bytes;
    }

    private static void Section(Span<byte> header, string name, uint size, uint rva, uint fileSize, uint fileOffset, bool isCode)
    {
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8), size);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12), rva);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(16), fileSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(20), fileOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(36), isCode ? 0x60000020u : 0x40000040u);
    }

    private static ushort Entry(int kind, int offset) => (ushort)((kind << 12) | offset);

    private static void Block(List<byte> table, uint page, params ushort[] entries)
    {
        var block = new byte[8 + entries.Length * 2];

        BinaryPrimitives.WriteUInt32LittleEndian(block, page);
        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(4), block.Length);

        for (var index = 0; index < entries.Length; index++) BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(8 + index * 2), entries[index]);

        table.AddRange(block);
    }

    /// <summary>
    /// A loaded module with a function table at 0x100: a method at 0x1000 in two pieces, the second chained back to the
    /// first, and a method at 0x2000. Nothing covers 0x1180 to 0x2000.
    /// </summary>
    private static byte[] Functions()
    {
        var image = new byte[0x500];

        void Piece(int at, uint begin, uint end, uint unwind)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(at), begin);
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(at + 4), end);
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(at + 8), unwind);
        }

        Piece(0x100, 0x1000, 0x1100, 0x400);
        Piece(0x10c, 0x1100, 0x1180, 0x410);
        Piece(0x118, 0x2000, 0x2040, 0x430);

        // Unwind info: version 1, no flags; then version 1 with the chained flag and one unwind code, so the entry of
        // the piece it belongs to sits after 2 padded codes.
        image[0x400] = 0x01;
        image[0x410] = 0x21;
        image[0x412] = 1;
        Piece(0x418, 0x1000, 0x1100, 0x400);
        image[0x430] = 0x01;

        return image;
    }

    private static ImageReader Read(byte[] image) => (rva, length) => (long)rva + length <= image.Length ? new ReadOnlySpan<byte>(image, (int)rva, length) : ReadOnlySpan<byte>.Empty;
}
