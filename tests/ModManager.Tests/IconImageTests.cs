using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class IconImageTests
{
    private static readonly byte[] PngStart = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2 };

    [Fact]
    public void A_png_or_jpeg_becomes_a_data_uri_of_its_own_type()
    {
        Assert.Equal("data:image/png;base64," + System.Convert.ToBase64String(PngStart), IconImage.DataUri(PngStart).Uri);
        Assert.StartsWith("data:image/jpeg;base64,", IconImage.DataUri(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }).Uri);
    }

    [Theory]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E })]
    [InlineData(new byte[0])]
    public void Anything_that_is_not_a_png_or_jpeg_is_refused(byte[] bytes)
    {
        var (uri, problem) = IconImage.DataUri(bytes);

        Assert.Null(uri);
        Assert.Equal("is not a PNG or JPEG picture", problem);
    }

    [Fact]
    public void A_picture_over_the_size_limit_is_refused_and_one_at_it_is_kept()
    {
        var atLimit = new byte[IconImage.MaxBytes];
        var over = new byte[IconImage.MaxBytes + 1];

        PngStart.CopyTo(atLimit, 0);
        PngStart.CopyTo(over, 0);

        Assert.NotNull(IconImage.DataUri(atLimit).Uri);
        Assert.Null(IconImage.DataUri(over).Uri);
        Assert.Equal("is larger than 256 KB", IconImage.DataUri(over).Problem);
    }
}
