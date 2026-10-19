using System.Text;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class DependencyBlobTests
{
    [Fact]
    public void A_dependency_with_the_soft_flag_is_optional_and_one_without_it_is_needed()
    {
        Assert.Equal(new DeclaredRelation(RelationKind.WorksWith, "com.x.toolkit"), DependencyBlob.Parse(Blob(Text("com.x.toolkit"), Int(2)), false));
        Assert.Equal(new DeclaredRelation(RelationKind.Needs, "com.x.toolkit"), DependencyBlob.Parse(Blob(Text("com.x.toolkit"), Int(1)), false));
    }

    [Fact]
    public void A_dependency_with_a_version_range_is_needed_with_that_range()
    {
        Assert.Equal(new DeclaredRelation(RelationKind.Needs, "com.x.toolkit", ">=0.4.0"), DependencyBlob.Parse(Blob(Text("com.x.toolkit"), Text(">=0.4.0")), true));
        Assert.Equal(new DeclaredRelation(RelationKind.Needs, "com.x.toolkit"), DependencyBlob.Parse(Blob(Text("com.x.toolkit"), Text("")), true));
    }

    [Fact]
    public void A_guid_longer_than_255_bytes_reads_its_two_byte_length()
    {
        var guid = new string('g', 300);

        Assert.Equal(guid, DependencyBlob.Parse(Blob(Text(guid), Int(0)), false)?.Guid);
    }

    [Fact]
    public void Bytes_that_are_cut_short_or_lack_the_prolog_or_a_guid_give_nothing()
    {
        var whole = Blob(Text("com.x.toolkit"), Int(2));

        Assert.Null(DependencyBlob.Parse(whole[..^4], false));
        Assert.Null(DependencyBlob.Parse(whole[..5], false));
        Assert.Null(DependencyBlob.Parse(new byte[] { 0x02, 0x00 }, false));
        Assert.Null(DependencyBlob.Parse(Blob(new byte[] { 0xFF }, Int(2)), false));
        Assert.Null(DependencyBlob.Parse(Blob(Text(""), Int(2)), false));
    }

    private static byte[] Blob(params byte[][] arguments)
    {
        var bytes = new List<byte> { 0x01, 0x00 };

        foreach (var argument in arguments) bytes.AddRange(argument);

        bytes.AddRange(new byte[] { 0x00, 0x00 });

        return bytes.ToArray();
    }

    private static byte[] Text(string text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);
        var length = utf8.Length < 0x80 ? new[] { (byte)utf8.Length } : new[] { (byte)(0x80 | (utf8.Length >> 8)), (byte)(utf8.Length & 0xFF) };

        return length.Concat(utf8).ToArray();
    }

    private static byte[] Int(int value) => BitConverter.GetBytes(value);
}
