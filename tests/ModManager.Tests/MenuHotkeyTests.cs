using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>The open key as the screen's footer names it.</summary>
public class MenuHotkeyTests
{
    [Theory]
    [InlineData("None", "")]
    [InlineData("F10", "F10")]
    public void The_footer_names_a_bound_key_and_leaves_an_unbound_one_out(string keyName, string shown)
    {
        Assert.Equal(shown, MenuHotkey.Name(keyName));
    }
}
