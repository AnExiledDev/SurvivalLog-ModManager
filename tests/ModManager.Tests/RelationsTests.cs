using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ApplyModesTests
{
    [Fact]
    public void A_restart_tag_wins_even_over_a_live_tag_or_a_watched_setting()
    {
        Assert.Equal(ApplyMode.Restart, ApplyModes.ForSetting(new object?[] { ApplyModes.LiveTag, ApplyModes.RestartTag }, true));
    }

    [Fact]
    public void A_live_tag_or_a_setting_the_toolkit_watches_takes_effect_at_once()
    {
        Assert.Equal(ApplyMode.Live, ApplyModes.ForSetting(new object?[] { ApplyModes.LiveTag }, false));
        Assert.Equal(ApplyMode.Live, ApplyModes.ForSetting(null, true));
    }

    [Fact]
    public void Other_tags_say_nothing_about_when_a_change_applies()
    {
        Assert.Equal(ApplyMode.Unknown, ApplyModes.ForSetting(new object?[] { null, 5, "SomeoneElse.Live", new object() }, false));
    }
}

public class RelationResolverTests
{
    private static readonly UpdateState NotChecked = new UpdateState.NotChecked();

    private static ModEntry Mod(string guid, string name, bool isOn, ModSwitch? @switch = null) =>
        new(guid, name, "1.0", name + ".dll", isOn ? new ModState.NotLoaded() : new ModState.Off(), ModInfo.None, @switch ?? new ModSwitch.Unavailable(), NotChecked);

    [Fact]
    public void Each_relation_gets_the_other_mods_name_and_whether_it_runs_next_start()
    {
        var mods = new[]
        {
            Mod("t", "Toolkit", true),
            Mod("m", "zz_map", false, new ModSwitch.Allowed(false, System.Array.Empty<string>())) with { Info = new ModInfo { Name = new Description("Map", "") } },
        };
        var declared = new[] { new DeclaredRelation(RelationKind.Needs, "t"), new DeclaredRelation(RelationKind.WorksWith, "m"), new DeclaredRelation(RelationKind.DoesNotWorkWith, "gone") };

        var relations = RelationResolver.Resolve(declared, mods);

        Assert.Equal(new[]
        {
            new Relation(RelationKind.Needs, "t", "Toolkit", RelationStatus.On, false),
            new Relation(RelationKind.WorksWith, "m", "Map", RelationStatus.Off, true),
            new Relation(RelationKind.DoesNotWorkWith, "gone", "gone", RelationStatus.Missing, false),
        }, relations);
    }

    [Fact]
    public void A_guid_declared_twice_under_one_kind_shows_once_but_under_two_kinds_shows_twice()
    {
        var declared = new[] { new DeclaredRelation(RelationKind.Needs, "a"), new DeclaredRelation(RelationKind.Needs, "a"), new DeclaredRelation(RelationKind.DoesNotWorkWith, "a") };

        var relations = RelationResolver.Resolve(declared, System.Array.Empty<ModEntry>());

        Assert.Equal(new[] { RelationKind.Needs, RelationKind.DoesNotWorkWith }, relations.Select(relation => relation.Kind));
    }

    [Fact]
    public void Only_needed_mods_that_are_off_and_switchable_are_turned_on_and_missing_ones_are_named()
    {
        var relations = new[]
        {
            new Relation(RelationKind.Needs, "off", "Off", RelationStatus.Off, true),
            new Relation(RelationKind.Needs, "locked", "Locked", RelationStatus.Off, false),
            new Relation(RelationKind.Needs, "on", "On", RelationStatus.On, true),
            new Relation(RelationKind.Needs, "gone", "Gone", RelationStatus.Missing, false),
            new Relation(RelationKind.WorksWith, "soft", "Soft", RelationStatus.Off, true),
            new Relation(RelationKind.DoesNotWorkWith, "clash", "Clash", RelationStatus.Missing, false),
        };

        var (turnOn, missing) = RelationResolver.Needed(relations);

        Assert.Equal(new[] { "off" }, turnOn);
        Assert.Equal(new[] { "Gone" }, missing);
    }
}

public class NeedsNotesTests
{
    [Fact]
    public void The_note_says_what_was_turned_on_what_failed_and_what_to_install()
    {
        var text = NeedsNotes.TurnedOn(new[] { "Toolkit", "Map" }, new[] { "Lib" }, new[] { "Stuck" });

        Assert.Equal("On from the next start: Toolkit, Map. Could not turn on: Stuck. Not installed, so install these yourself: Lib.", text.English);
        Assert.Contains("Toolkit、Map", text.Chinese);
        Assert.Contains("Lib", text.Chinese);
    }

    [Fact]
    public void Nothing_to_do_says_everything_is_already_on()
    {
        var text = NeedsNotes.TurnedOn(System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<string>());

        Assert.Equal("Everything it needs is already on.", text.English);
    }
}
