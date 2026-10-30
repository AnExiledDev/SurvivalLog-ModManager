using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>The open key: unbound by default since the Mods button opens the screen, and silent while the player types.</summary>
public class MenuHotkeyTests
{
    private int _pressAsks;
    private int _typingAsks;

    private Func<bool> Pressed(bool isPressed) => () =>
    {
        _pressAsks++;

        return isPressed;
    };

    private Func<bool> Typing(bool isTyping) => () =>
    {
        _typingAsks++;

        return isTyping;
    };

    [Fact]
    public void The_open_key_is_unbound_by_default()
    {
        Assert.Equal("None", MenuHotkey.DefaultKey);
    }

    [Theory]
    [InlineData("None", "")]
    [InlineData("F10", "F10")]
    public void The_footer_names_a_bound_key_and_leaves_an_unbound_one_out(string keyName, string shown)
    {
        Assert.Equal(shown, MenuHotkey.Name(keyName));
    }

    [Fact]
    public void An_unbound_key_never_fires_and_asks_for_nothing()
    {
        Assert.False(MenuHotkey.Fires(isBound: false, Pressed(true), Typing(false)));
        Assert.Equal(0, _pressAsks);
        Assert.Equal(0, _typingAsks);
    }

    [Fact]
    public void A_bound_key_fires_on_its_press()
    {
        Assert.True(MenuHotkey.Fires(isBound: true, Pressed(true), Typing(false)));
    }

    [Fact]
    public void A_bound_key_does_nothing_while_the_player_types()
    {
        Assert.False(MenuHotkey.Fires(isBound: true, Pressed(true), Typing(true)));
    }

    [Fact]
    public void Typing_is_only_checked_when_the_key_goes_down()
    {
        Assert.False(MenuHotkey.Fires(isBound: true, Pressed(false), Typing(false)));
        Assert.Equal(0, _typingAsks);
    }
}
