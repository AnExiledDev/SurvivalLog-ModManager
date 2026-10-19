using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class WordingTests
{
    private const string Chinese = "\u6253\u5f00\u754c\u9762\u3002";

    [Fact]
    public void English_lines_and_Chinese_lines_go_to_their_own_language()
    {
        var description = Wording.SplitDescription("Opens the screen.\nDefault: F10.\n" + Chinese);

        Assert.Equal("Opens the screen.\nDefault: F10.", description.English);
        Assert.Equal(Chinese, description.Chinese);
    }

    [Fact]
    public void A_line_mixing_key_names_and_Chinese_counts_as_Chinese()
    {
        var description = Wording.SplitDescription("Key.\n\u9ed8\u8ba4 F10\u3002");

        Assert.Equal("Key.", description.English);
        Assert.Equal("\u9ed8\u8ba4 F10\u3002", description.Chinese);
    }

    [Fact]
    public void A_description_in_one_language_shows_in_both()
    {
        var english = Wording.SplitDescription("Only English.");
        var chinese = Wording.SplitDescription(Chinese);

        Assert.Equal("Only English.", english.Chinese);
        Assert.Equal(Chinese, chinese.English);
    }

    [Fact]
    public void Windows_line_ends_and_blank_lines_are_dropped()
    {
        var description = Wording.SplitDescription("One.\r\n\r\n  Two.  \r\n");

        Assert.Equal("One.\nTwo.", description.English);
    }

    [Theory]
    [InlineData("IdleFreezeDelay", "Idle freeze delay")]
    [InlineData("LogUIEvents", "Log UI events")]
    [InlineData("Slot2", "Slot 2")]
    [InlineData("open_key", "open key")]
    [InlineData("OpenKey", "Open key")]
    [InlineData("HUD", "HUD")]
    public void A_key_becomes_a_readable_label(string key, string expected)
    {
        Assert.Equal(expected, Wording.LabelFromKey(key));
    }

    [Fact]
    public void A_key_with_no_words_is_shown_as_it_is()
    {
        Assert.Equal("__", Wording.LabelFromKey("__"));
    }
}
