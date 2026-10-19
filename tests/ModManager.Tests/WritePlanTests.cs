using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class WritePlanTests
{
    private static Setting Setting(SettingKind kind, bool isReadOnly = false) =>
        new(new SettingId("g", "S", "K"), "K", new Description("", ""), kind, "1", "1", "1", isReadOnly);

    [Fact]
    public void A_read_only_setting_is_never_written()
    {
        var plan = WritePlan.For(Setting(new SettingKind.Text(), isReadOnly: true), "anything");

        Assert.True(Assert.IsType<WritePlan.Refuse>(plan).Note.IsWarning);
    }

    [Fact]
    public void A_valid_value_is_written_with_no_note()
    {
        var plan = Assert.IsType<WritePlan.Write>(WritePlan.For(Setting(new SettingKind.Toggle()), "TRUE"));

        Assert.Equal("true", plan.Value);
        Assert.Null(plan.Note);
    }

    [Fact]
    public void A_clamped_value_is_written_and_the_player_is_told_what_it_became()
    {
        var plan = Assert.IsType<WritePlan.Write>(WritePlan.For(Setting(new SettingKind.Number(true, 0, 10)), "99"));
        var note = Assert.IsType<SettingNote>(plan.Note);

        Assert.Equal("10", plan.Value);
        Assert.False(note.IsWarning);
        Assert.Contains("10", note.Text.English);
    }

    [Fact]
    public void A_bad_value_is_refused_with_the_reason_as_a_warning()
    {
        var plan = Assert.IsType<WritePlan.Refuse>(WritePlan.For(Setting(new SettingKind.Number(false, null, null)), "abc"));

        Assert.True(plan.Note.IsWarning);
        Assert.Equal(new SettingId("g", "S", "K"), plan.Note.Id);
    }
}
