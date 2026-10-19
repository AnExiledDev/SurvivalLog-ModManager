using System;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class SwitchNotesTests
{
    [Fact]
    public void Flipping_back_to_how_the_game_started_says_nothing_changes()
    {
        Assert.StartsWith("Back as it was", SwitchNotes.Moved(true, true, Array.Empty<string>()).English);
        Assert.StartsWith("Back as it was", SwitchNotes.Moved(false, false, Array.Empty<string>()).English);
    }

    [Fact]
    public void Turning_on_asks_for_a_restart()
    {
        Assert.Contains("Restart the game", SwitchNotes.Moved(false, true, Array.Empty<string>()).English);
    }

    [Fact]
    public void Turning_off_names_the_mods_that_stop_with_it_in_both_languages()
    {
        var note = SwitchNotes.Moved(true, false, new[] { "Child A", "Child B" });

        Assert.Contains("Child A, Child B", note.English);
        Assert.Contains("Child A, Child B", note.Chinese);
        Assert.DoesNotContain("will not start", SwitchNotes.Moved(true, false, Array.Empty<string>()).English);
    }
}
