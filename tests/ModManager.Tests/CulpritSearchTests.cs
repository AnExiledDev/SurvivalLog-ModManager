using System;
using System.Collections.Generic;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class CulpritSearchTests
{
    private static readonly string[] Eight = { "a", "b", "c", "d", "e", "f", "g", "h" };

    [Theory]
    [InlineData("a")]
    [InlineData("c")]
    [InlineData("e")]
    [InlineData("h")]
    public void Each_mod_that_crashes_the_game_is_found_in_a_few_rounds(string culprit)
    {
        var (found, rounds) = Run(Eight.Select(Mod).ToList(), on => on.Contains(culprit));

        Assert.Equal(new[] { culprit }, Assert.IsType<StartPlan.Found>(found).Culprit);
        Assert.True(rounds <= 4, rounds + " rounds");
    }

    [Fact]
    public void The_last_suspect_gets_a_round_of_its_own_only_when_no_round_has_shown_the_other_mods_run_fine()
    {
        var mods = Eight.Select(Mod).ToList();

        // Round 1 runs E to H without a crash, so once A is the last suspect it is named at once.
        Assert.Equal(3, Run(mods, on => on.Contains("a")).Rounds);

        // Every round with H on crashes, so the other mods never ran without it and one more round checks that.
        Assert.Equal(4, Run(mods, on => on.Contains("h")).Rounds);
    }

    [Fact]
    public void When_two_mods_crash_only_together_turning_off_the_one_found_stops_the_crash()
    {
        static bool Crashes(ISet<string> on) => on.Contains("b") && on.Contains("g");
        var mods = Eight.Select(Mod).ToList();

        var (found, _) = Run(mods, Crashes);
        var after = CulpritSearch.Layout(Begin(mods), found).Where(pair => pair.Value).Select(pair => pair.Key).ToHashSet();

        Assert.Single(Assert.IsType<StartPlan.Found>(found).Culprit);
        Assert.False(Crashes(after));
        Assert.Equal(7, after.Count);
    }

    [Fact]
    public void A_crash_with_every_mod_off_ends_the_search_with_no_mod_to_blame()
    {
        var (end, _) = Run(Eight.Select(Mod).ToList(), _ => true);

        Assert.IsType<StartPlan.NotFound>(end);
    }

    [Fact]
    public void A_mod_never_runs_without_a_mod_it_needs_and_the_mods_that_need_the_one_found_stay_off_with_it()
    {
        var mods = new[] { Mod("base"), Mod("mid", "base"), Mod("top", "mid"), Mod("x"), Mod("y"), Mod("z", "x") };
        var needs = mods.ToDictionary(mod => mod.Guid, mod => mod.Relations.Select(relation => relation.Guid).ToList());
        var seen = new List<ISet<string>>();

        var (found, _) = Run(mods, on =>
        {
            seen.Add(on);
            return on.Contains("base");
        });

        Assert.All(seen, on => Assert.All(on, guid => Assert.All(needs[guid], need => Assert.Contains(need, on))));
        Assert.Equal(new[] { "base" }, Assert.IsType<StartPlan.Found>(found).Culprit);
        Assert.Equal(new[] { "mid", "top" }, ((StartPlan.Found)found).AlsoOff);
    }

    [Fact]
    public void A_mod_that_others_need_is_found_when_it_is_the_one_that_crashes_and_not_one_of_the_mods_that_need_it()
    {
        var mods = new[] { Mod("lib"), Mod("one", "lib"), Mod("two", "lib"), Mod("three", "lib") };

        var (found, _) = Run(mods, on => on.Contains("two"));

        Assert.Equal(new[] { "two" }, Assert.IsType<StartPlan.Found>(found).Culprit);
        Assert.Empty(((StartPlan.Found)found).AlsoOff);
    }

    [Fact]
    public void Mods_in_one_DLL_can_only_be_turned_off_together_so_they_are_found_together()
    {
        var mods = new[] { Mod("a") with { FileName = "Pack.dll" }, Mod("b") with { FileName = "pack.DLL" }, Mod("c"), Mod("d") };

        var (found, _) = Run(mods, on => on.Contains("b"));

        Assert.Equal(new[] { "a", "b" }, Assert.IsType<StartPlan.Found>(found).Culprit.OrderBy(guid => guid));
    }

    [Fact]
    public void A_search_of_one_mod_runs_one_round_with_it_off()
    {
        var round = Begin(new[] { Mod("a") });

        Assert.True(round.IsTest);
        Assert.Equal(new[] { "a" }, round.Off);
        Assert.IsType<StartPlan.NotFound>(CulpritSearch.After(round, true));
        Assert.Equal(new[] { "a" }, Assert.IsType<StartPlan.Found>(CulpritSearch.After(round, false)).Culprit);
    }

    [Fact]
    public void Stopping_the_search_turns_every_mod_it_began_with_back_on()
    {
        var mods = Eight.Select(Mod).ToList();
        var round = (StartPlan.Search)CulpritSearch.After(Begin(mods), true);

        Assert.Contains(false, CulpritSearch.Layout(round, round).Values);
        Assert.All(CulpritSearch.Layout(round, null), pair => Assert.True(pair.Value));
        Assert.Equal(Eight, CulpritSearch.Layout(round, null).Keys.OrderBy(guid => guid));
    }

    [Fact]
    public void Only_mods_on_for_the_next_start_that_can_be_turned_off_and_that_started_are_suspects()
    {
        var mods = new[]
        {
            Mod("on"),
            Mod("off") with { Switch = new ModSwitch.Allowed(false, Array.Empty<string>()) },
            Mod("failed") with { State = new ModState.NotLoaded() },
            Mod("on-next") with { State = new ModState.Off() },
            Mod("mm") with { Switch = new ModSwitch.Locked(true, Array.Empty<string>()) },
            Mod("gone") with { Switch = new ModSwitch.Uninstalled("t") },
        };

        Assert.Equal(new[] { "on", "on-next" }, CulpritSearch.Suspects(mods).Select(mod => mod.Guid));
        Assert.Equal(new[] { "on", "on-next" }, CulpritSearch.SafeStart(mods).Off);
    }

    [Fact]
    public void Nothing_starts_while_an_update_waits_to_replace_files_or_when_no_mod_can_be_turned_off()
    {
        var held = new[] { Mod("a"), Mod("b") with { Switch = new ModSwitch.Held(true) } };
        var locked = new[] { Mod("mm") with { Switch = new ModSwitch.Locked(true, Array.Empty<string>()) } };

        Assert.Equal(StartRefusal.FilesWaiting, CulpritSearch.WhyNot(held));
        Assert.Equal(StartRefusal.NothingToTurnOff, CulpritSearch.WhyNot(locked));
        Assert.Null(CulpritSearch.WhyNot(new[] { Mod("a") }));
    }

    [Fact]
    public void A_crash_while_a_round_ran_with_the_next_round_set_up_goes_straight_on_to_it()
    {
        var round = Begin(Eight.Select(Mod).ToList()) with { IsArmed = true };

        var read = CulpritSearch.AtLoad(SessionMark.Running(round.Token), round);

        var next = Assert.IsType<StartPlan.Search>(read.Plan);
        Assert.Equal(2, next.Round);
        Assert.False(next.IsArmed);
        Assert.Equal(next.Token, read.Loaded);
        Assert.Equal(next.Token, read.Answers);
        Assert.True(read.HasRoundCrashed);
        Assert.True(read.IsPlanChanged);
        Assert.Equal(AtFirstFrame.Arm, read.Then);
        Assert.Null(read.LastCrash);
    }

    [Fact]
    public void A_crash_on_the_last_round_ends_the_search_at_load()
    {
        var round = Begin(new[] { Mod("a") }) with { IsArmed = true };

        var read = CulpritSearch.AtLoad(SessionMark.Running(round.Token), round);

        Assert.IsType<StartPlan.NotFound>(read.Plan);
        Assert.Null(read.Answers);
        Assert.True(read.IsPlanChanged);
        Assert.Equal(AtFirstFrame.Nothing, read.Then);
    }

    [Fact]
    public void A_round_set_up_for_a_crash_that_never_came_is_set_up_again_and_not_counted()
    {
        var round = Begin(Eight.Select(Mod).ToList()) with { IsArmed = true };

        foreach (var last in new[] { SessionMark.Running(null) with { IsClosed = true }, SessionMark.Running(round.Id + "/9"), null })
        {
            var read = CulpritSearch.AtLoad(last, round);

            Assert.Same(round, read.Plan);
            Assert.Null(read.Loaded);
            Assert.Null(read.Answers);
            Assert.False(read.HasRoundCrashed);
            Assert.Equal(AtFirstFrame.SetRoundAgain, read.Then);
        }
    }

    [Fact]
    public void A_crash_before_the_next_round_was_set_up_still_counts_and_sets_it_up_at_the_first_frame()
    {
        var round = Begin(Eight.Select(Mod).ToList());

        var read = CulpritSearch.AtLoad(SessionMark.Running(round.Token), round);

        Assert.Same(round, read.Plan);
        Assert.Equal(round.Token, read.Loaded);
        Assert.Null(read.Answers);
        Assert.True(read.HasRoundCrashed);
        Assert.Equal(AtFirstFrame.GoOnAfterCrash, read.Then);
    }

    [Fact]
    public void A_round_waiting_to_run_runs_and_the_player_can_answer_for_it()
    {
        var round = Begin(Eight.Select(Mod).ToList());
        var otherSearch = round with { Id = "other" };

        foreach (var last in new[] { SessionMark.Running(otherSearch.Token), new SessionMark(true, round.Token, Array.Empty<string>()) })
        {
            var read = CulpritSearch.AtLoad(last, round);

            Assert.Equal(round.Token, read.Loaded);
            Assert.Equal(round.Token, read.Answers);
            Assert.False(read.HasRoundCrashed);
            Assert.Equal(AtFirstFrame.Arm, read.Then);
        }
    }

    [Fact]
    public void A_safe_start_puts_its_mods_back_on_at_the_first_frame()
    {
        var read = CulpritSearch.AtLoad(SessionMark.Running(null), new StartPlan.Safe(new[] { "a" }));

        Assert.Equal(AtFirstFrame.EndSafeStart, read.Then);
        Assert.Null(read.LastCrash);
    }

    [Fact]
    public void A_crash_with_no_search_running_names_the_mods_behind_its_last_errors()
    {
        var crashed = CulpritSearch.AtLoad(new SessionMark(false, null, new[] { "a", "b" }), null);
        var closed = CulpritSearch.AtLoad(new SessionMark(true, null, new[] { "a" }), null);
        var first = CulpritSearch.AtLoad(null, null);

        Assert.Equal(new[] { "a", "b" }, crashed.LastCrash);
        Assert.Null(closed.LastCrash);
        Assert.Null(first.LastCrash);
        Assert.Equal(AtFirstFrame.Nothing, crashed.Then);
    }

    [Fact]
    public void The_round_view_names_every_mod_off_and_whether_this_session_can_answer()
    {
        var round = Begin(Eight.Select(Mod).ToList());

        var view = Assert.IsType<PlanView.Round>(CulpritSearch.ViewOf(round, round.Token, false));
        var notThisSession = Assert.IsType<PlanView.Round>(CulpritSearch.ViewOf(round, null, false));

        Assert.Equal(1, view.Number);
        Assert.Equal(4, view.Off.Count);
        Assert.Equal(8, view.Suspects);
        Assert.True(view.IsRunning);
        Assert.False(notThisSession.IsRunning);
    }

    [Fact]
    public void Every_plan_reads_back_as_it_was_written()
    {
        var mods = new[] { Mod("base mod") with { FileName = "Base Mod.dll" }, Mod("mid", "base mod"), Mod("x"), Mod("y") };
        var round = (StartPlan.Search)CulpritSearch.After(Begin(mods), false) with { IsArmed = true };

        var search = Assert.IsType<StartPlan.Search>(PlanText.Read(PlanText.Write(round)));

        Assert.Equal(round.Token, search.Token);
        Assert.True(search.IsArmed);
        Assert.Equal(round.KeptWorks, search.KeptWorks);
        Assert.Equal(round.Original, search.Original);
        Assert.Equal(round.Kept, search.Kept);
        Assert.Equal(round.Suspects, search.Suspects);
        Assert.Equal(round.Off, search.Off);
        Assert.Equal(round.Nodes.Select(Describe), search.Nodes.Select(Describe));
        Assert.Equal(new[] { "a" }, Assert.IsType<StartPlan.Safe>(PlanText.Read(PlanText.Write(new StartPlan.Safe(new[] { "a" })))).Off);
        var found = Assert.IsType<StartPlan.Found>(PlanText.Read(PlanText.Write(new StartPlan.Found(new[] { "a" }, new[] { "b", "c" }))));
        Assert.Equal(new[] { "b", "c" }, found.AlsoOff);
        Assert.IsType<StartPlan.NotFound>(PlanText.Read(PlanText.Write(new StartPlan.NotFound())));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("safe")]
    [InlineData("found\nalsoOff b")]
    [InlineData("search\nid s\nround 1\noriginal a b\nsuspects a b")]
    [InlineData("search\nid s\nround 0\noriginal a b\nsuspects a b\noff a")]
    [InlineData("search\nround 1\noriginal a b\nsuspects a b\noff a")]
    [InlineData("search\nid s\nround 1\noriginal a b\nsuspects a b\noff c")]
    [InlineData("search\nid s\nround 1\noriginal a b\nkept a\nsuspects a b\noff b")]
    [InlineData("search\nid s\nround 1\noriginal a\nsuspects a b\noff b")]
    public void A_plan_file_that_is_empty_or_not_a_whole_plan_reads_as_no_plan(string text)
    {
        Assert.Null(PlanText.Read(text));
    }

    [Fact]
    public void The_session_mark_reads_back_with_its_round_and_error_mods()
    {
        var mark = new SessionMark(false, "id 1/2", new[] { "com.a mod", "b" });

        var read = SessionMark.Parse(mark.ToText());

        Assert.NotNull(read);
        Assert.False(read!.IsClosed);
        Assert.Equal("id 1/2", read.Round);
        Assert.Equal(new[] { "com.a mod", "b" }, read.ErrorMods);
        Assert.True(SessionMark.Parse(new SessionMark(true, null, Array.Empty<string>()).ToText())!.IsClosed);
        Assert.Null(SessionMark.Parse(""));
        Assert.Null(SessionMark.Parse("crashed"));
    }

    [Fact]
    public void The_mods_behind_a_crash_are_the_owners_of_the_newest_errors_at_most_three()
    {
        var owners = new[] { "a", "b", "c", "d" }.Select(guid => new ProblemOwner(guid, guid.ToUpperInvariant(), Array.Empty<string>(), false)).ToList();
        static Problem Logged(ProblemLevel level, string source) => new(level, source, "m", Array.Empty<string>(), Array.Empty<string>(), 1, 0);
        var newestFirst = new[] { Logged(ProblemLevel.Warning, "A"), Logged(ProblemLevel.Error, "B"), Logged(ProblemLevel.Error, "nobody"), Logged(ProblemLevel.Error, "B"), Logged(ProblemLevel.Error, "D"), Logged(ProblemLevel.Error, "C"), Logged(ProblemLevel.Error, "A") };

        Assert.Equal(new[] { "b", "d", "c" }, SessionMark.ErrorModsOf(newestFirst, owners));
    }

    /// <summary>Runs a whole search, answering each round from whether the mods it has on crash the game.</summary>
    private static (StartPlan End, int Rounds) Run(IReadOnlyList<ModEntry> mods, Func<ISet<string>, bool> crashes)
    {
        StartPlan plan = Begin(mods);
        var rounds = 0;

        while (plan is StartPlan.Search round)
        {
            Assert.True(++rounds < 20, "the search never ended");

            var on = round.Original.Where(round.IsOn).ToHashSet();

            plan = CulpritSearch.After(round, crashes(on));
        }

        return (plan, rounds);
    }

    private static StartPlan.Search Begin(IReadOnlyList<ModEntry> mods) => CulpritSearch.Begin("s", mods);

    private static string Describe(PluginNode node) => node.Guid + "|" + node.Name + "|" + node.File + "|" + string.Join(",", node.HardDependencies);

    private static ModEntry Mod(string guid) => Mod(guid, Array.Empty<string>());

    private static ModEntry Mod(string guid, params string[] needs) =>
        new ModEntry(guid, guid.ToUpperInvariant(), "1.0", guid + ".dll", new ModState.Loaded(Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked())
        {
            Relations = needs.Select(need => new Relation(RelationKind.Needs, need, need, RelationStatus.On, false)).ToList(),
        };
}
