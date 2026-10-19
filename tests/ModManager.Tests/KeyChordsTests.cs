using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class KeyChordsTests
{
    private static readonly string[] Nothing = System.Array.Empty<string>();

    [Fact]
    public void Nothing_pressed_keeps_waiting()
    {
        Assert.IsType<CaptureStep.Waiting>(KeyChords.Step(Nothing, Nothing, true));
    }

    [Fact]
    public void Escape_cancels_even_alongside_another_key()
    {
        Assert.IsType<CaptureStep.Cancelled>(KeyChords.Step(new[] { "F7", "Escape" }, Nothing, false));
    }

    [Fact]
    public void A_single_key_setting_takes_a_modifier_as_the_key()
    {
        Assert.Equal(new CaptureStep.Captured("LeftShift"), KeyChords.Step(new[] { "LeftShift" }, new[] { "LeftShift" }, false));
    }

    [Fact]
    public void A_shortcut_waits_while_only_modifiers_are_pressed()
    {
        Assert.IsType<CaptureStep.Waiting>(KeyChords.Step(new[] { "LeftControl" }, new[] { "LeftControl" }, true));
    }

    [Fact]
    public void A_shortcut_writes_the_main_key_then_the_held_modifiers_in_BepInEx_order()
    {
        var step = KeyChords.Step(new[] { "F7" }, new[] { "LeftShift", "LeftControl" }, true);

        Assert.Equal(new CaptureStep.Captured("F7 + LeftControl + LeftShift"), step);
    }

    [Fact]
    public void Held_modifiers_are_ignored_by_a_single_key_setting()
    {
        Assert.Equal(new CaptureStep.Captured("F7"), KeyChords.Step(new[] { "F7" }, new[] { "LeftControl" }, false));
    }

    [Fact]
    public void The_mouse_buttons_that_click_the_page_never_become_the_key()
    {
        Assert.IsType<CaptureStep.Waiting>(KeyChords.Step(new[] { "Mouse0", "Mouse1" }, Nothing, false));
        Assert.Equal(new CaptureStep.Captured("Mouse2"), KeyChords.Step(new[] { "Mouse0", "Mouse2" }, Nothing, false));
    }
}
