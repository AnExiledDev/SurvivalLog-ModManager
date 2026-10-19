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

    /// <summary>A fake range check: a range holds exactly the versions it lists, separated by "|".</summary>
    private static bool IsInRange(string version, string range) => range.Split('|').Contains(version);

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

        var relations = RelationResolver.Resolve(declared, mods, IsInRange);

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

        var relations = RelationResolver.Resolve(declared, System.Array.Empty<ModEntry>(), IsInRange);

        Assert.Equal(new[] { RelationKind.Needs, RelationKind.DoesNotWorkWith }, relations.Select(relation => relation.Kind));
    }

    [Fact]
    public void A_needed_mod_outside_the_asked_version_range_is_the_wrong_version_even_when_on_and_cannot_be_turned_on_to_fix_it()
    {
        var mods = new[] { Mod("t", "Toolkit", true), Mod("m", "Map", false, new ModSwitch.Allowed(false, System.Array.Empty<string>())) };
        var declared = new[] { new DeclaredRelation(RelationKind.Needs, "t", "2.0"), new DeclaredRelation(RelationKind.Needs, "m", "0.9|2.0") };

        var relations = RelationResolver.Resolve(declared, mods, IsInRange);

        Assert.Equal(new Relation(RelationKind.Needs, "t", "Toolkit", RelationStatus.WrongVersion, false, "2.0"), relations[0]);
        Assert.Equal(RelationStatus.WrongVersion, relations[1].Status);
        Assert.False(relations[1].CanTurnOn);
    }

    [Fact]
    public void A_needed_mod_inside_the_range_keeps_its_state_and_the_range_to_show()
    {
        var relations = RelationResolver.Resolve(new[] { new DeclaredRelation(RelationKind.Needs, "t", "0.9|1.0") }, new[] { Mod("t", "Toolkit", true) }, IsInRange);

        Assert.Equal(new Relation(RelationKind.Needs, "t", "Toolkit", RelationStatus.On, false, "0.9|1.0"), relations.Single());
    }

    [Fact]
    public void A_mod_lists_the_mods_that_need_it_first_then_those_that_can_use_it_each_with_whether_it_runs()
    {
        var mods = new[] { Mod("lib", "Lib", true), Mod("b", "Beta", true), Mod("a", "Alpha", false), Mod("s", "Soft", true), Mod("x", "Clash", true) };
        var declaredBy = new Dictionary<string, IReadOnlyList<DeclaredRelation>>
        {
            ["lib"] = new[] { new DeclaredRelation(RelationKind.Needs, "lib") },
            ["b"] = new[] { new DeclaredRelation(RelationKind.Needs, "lib"), new DeclaredRelation(RelationKind.Needs, "lib", "1.0") },
            ["a"] = new[] { new DeclaredRelation(RelationKind.Needs, "lib") },
            ["s"] = new[] { new DeclaredRelation(RelationKind.WorksWith, "lib") },
            ["x"] = new[] { new DeclaredRelation(RelationKind.DoesNotWorkWith, "lib"), new DeclaredRelation(RelationKind.Needs, "other") },
        };

        var dependents = RelationResolver.Dependents("lib", declaredBy, mods);

        Assert.Equal(new[]
        {
            new Relation(RelationKind.Needs, "a", "Alpha", RelationStatus.Off, false),
            new Relation(RelationKind.Needs, "b", "Beta", RelationStatus.On, false),
            new Relation(RelationKind.WorksWith, "s", "Soft", RelationStatus.On, false),
        }, dependents);
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
            new Relation(RelationKind.Needs, "old", "Old", RelationStatus.WrongVersion, false, ">=2.0"),
            new Relation(RelationKind.WorksWith, "oldSoft", "OldSoft", RelationStatus.WrongVersion, false, ">=2.0"),
        };

        var needed = RelationResolver.Needed(relations);

        Assert.Equal(new[] { "off" }, needed.TurnOn);
        Assert.Equal(new[] { "Gone" }, needed.Missing);
        Assert.Equal(new[] { "Old >=2.0" }, needed.WrongVersion);
    }
}

public class NeedsNotesTests
{
    [Fact]
    public void The_note_says_what_was_turned_on_what_failed_and_what_to_install()
    {
        var text = NeedsNotes.TurnedOn(new[] { "Toolkit", "Map" }, new[] { "Lib" }, new[] { "Stuck" }, new[] { "Old >=2.0" });

        Assert.Equal("On from the next start: Toolkit, Map. Could not turn on: Stuck. Not installed, so install these yourself: Lib. Wrong version, so install one that fits: Old >=2.0.", text.English);
        Assert.Contains("Toolkit、Map", text.Chinese);
        Assert.Contains("Lib", text.Chinese);
    }

    [Fact]
    public void Nothing_to_do_says_everything_is_already_on()
    {
        var text = NeedsNotes.TurnedOn(System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<string>());

        Assert.Equal("Everything it needs is already on.", text.English);
    }
}
