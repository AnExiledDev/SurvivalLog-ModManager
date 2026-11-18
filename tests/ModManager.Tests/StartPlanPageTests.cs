using System;
using System.Collections.Generic;
using System.IO;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>SAFE START and FIND THE PROBLEM MOD on the page: the bar at the bottom, the HEALTH buttons, and the crash notice.</summary>
public class StartPlanPageTests
{
    [Fact]
    public void A_round_set_up_for_the_next_start_names_the_mods_it_turns_off_and_offers_quit_and_stop()
    {
        var view = View(Named("a", "Radar") with { Switch = TurnsOff }, Named("b", "Farm Hub"), Named("c", "Pack Rat")) with
        {
            StartPlan = new PlanView.Round(1, new[] { "a" }, 3, false, false, false),
            PlanHolds = new[] { "a", "b", "c" },
        };

        var drawn = KnownFactsTests.Drawn(Page(view));

        Assert.Contains("FIND THE PROBLEM MOD: ROUND 1", drawn);
        Assert.Contains("Round 1 is set up for the next start. Quit the game and start it again.", drawn);
        Assert.Contains("3 mods could still be the problem.", drawn);
        Assert.Contains("Off in this round: Radar.", drawn);
        Assert.Contains("data-act=\"quitGame\"", drawn);
        Assert.Contains("data-act=\"planStop\"", drawn);
        Assert.DoesNotContain("data-act=\"planFine\"", drawn);
    }

    [Fact]
    public void Mods_the_plan_moves_are_not_listed_as_changes_waiting_for_the_next_start()
    {
        var view = View(Named("a", "Radar") with { Switch = TurnsOff }, Named("b", "Farm Hub") with { Switch = TurnsOff }) with
        {
            StartPlan = new PlanView.Round(1, new[] { "a" }, 2, false, false, false),
            PlanHolds = new[] { "a" },
        };

        var drawn = KnownFactsTests.Drawn(Page(view));

        Assert.Contains("1 change waits for the next start.", drawn);
        Assert.DoesNotContain("<span class=\"pn\">Radar</span>", drawn);
        Assert.Contains("<span class=\"pn\">Farm Hub</span>", drawn);
        Assert.Equal(1, Count(drawn, "data-act=\"quitGame\""));
    }

    [Fact]
    public void The_round_this_start_runs_asks_how_it_went_and_sends_the_answer()
    {
        var view = View(Named("a", "Radar"), Named("b", "Farm Hub")) with
        {
            StartPlan = new PlanView.Round(2, new[] { "b" }, 2, false, true, false),
            PlanHolds = new[] { "a", "b" },
        };
        var engine = Page(view);
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("Play until the problem would show up, then say how it went.", drawn);
        Assert.Contains(">NO PROBLEM</div>", drawn);
        Assert.Contains(">STILL BROKEN</div>", drawn);
        Assert.DoesNotContain("data-act=\"quitGame\"", drawn);

        engine.Execute("fire('click', { target: act('planBroken') })");
        engine.Execute("fire('click', { target: act('planFine') })");

        Assert.Equal("slmod|modmanager|planAnswer|1\nslmod|modmanager|planAnswer|0", Posted(engine, "planAnswer"));
    }

    [Fact]
    public void A_round_moved_on_by_a_crash_says_so()
    {
        var view = View(Named("a", "Radar"), Named("b", "Farm Hub")) with
        {
            StartPlan = new PlanView.Round(3, new[] { "a" }, 1, true, true, true),
            PlanHolds = new[] { "a", "b" },
        };

        var drawn = KnownFactsTests.Drawn(Page(view));

        Assert.Contains("The game didn&#39;t close normally during round 2, so that round counts as still broken.", drawn);
        Assert.Contains("This round turns off the last mod that could be the problem, to check that the problem goes away without it.", drawn);
    }

    [Fact]
    public void Stop_and_ok_are_sent_as_they_are()
    {
        var engine = Page(View(Named("a", "Radar")) with { StartPlan = new PlanView.SafeSet(new[] { "a" }), PlanHolds = new[] { "a" } });

        engine.Execute("fire('click', { target: act('planStop') })");
        engine.Execute("fire('click', { target: act('planOk') })");

        Assert.EndsWith("|planStop", Posted(engine, "planStop"));
        Assert.EndsWith("|planOk", Posted(engine, "planOk"));
    }

    [Fact]
    public void The_found_mod_is_named_with_the_mods_that_need_it()
    {
        var view = View(Named("a", "Radar"), Named("b", "Radar Icons"), Named("c", "Farm Hub")) with
        {
            StartPlan = new PlanView.Found(new[] { "a" }, new[] { "b" }),
        };

        var drawn = KnownFactsTests.Drawn(Page(view));

        Assert.Contains("The problem comes from Radar. It stays off from the next start.", drawn);
        Assert.Contains("These mods need it, so they stay off too: Radar Icons.", drawn);
        Assert.Contains("data-act=\"planOk\"", drawn);
        Assert.DoesNotContain("data-act=\"planStop\"", drawn);
    }

    [Fact]
    public void Mods_found_together_are_named_as_one_problem()
    {
        var view = View(Named("a", "Tools A"), Named("b", "Tools B")) with { StartPlan = new PlanView.Found(new[] { "a", "b" }, Array.Empty<string>()) };

        var drawn = KnownFactsTests.Drawn(Page(view));

        Assert.Contains("The problem comes from one of these mods, which can only be turned off together: Tools A, Tools B.", drawn);
        Assert.DoesNotContain("These mods need", drawn);
    }

    [Fact]
    public void A_search_with_no_mod_to_blame_and_a_stopped_one_say_every_mod_is_back_on()
    {
        var notFound = KnownFactsTests.Drawn(Page(View(Named("a", "Radar")) with { StartPlan = new PlanView.NotFound() }));
        var stopped = KnownFactsTests.Drawn(Page(View(Named("a", "Radar")) with { StartPlan = new PlanView.Stopped(false) }));

        Assert.Contains("It couldn&#39;t find one mod that causes the problem.", notFound);
        Assert.Contains("Every mod is back on from the next start.", notFound);
        Assert.Contains("<b>SAFE START</b>Stopped. Every mod it turned off is back on from the next start.", stopped);
    }

    [Fact]
    public void The_safe_start_says_how_many_mods_it_turns_off_and_when_they_come_back()
    {
        var set = KnownFactsTests.Drawn(Page(View(Named("a", "Radar"), Named("b", "Farm Hub")) with { StartPlan = new PlanView.SafeSet(new[] { "a", "b" }), PlanHolds = new[] { "a", "b" } }));
        var now = KnownFactsTests.Drawn(Page(View(Named("a", "Radar")) with { StartPlan = new PlanView.SafeNow(new[] { "a" }) }));

        Assert.Contains("The next start runs with 2 mods off. The start after that turns them back on.", set);
        Assert.Contains("Off: Radar, Farm Hub.", set);
        Assert.Contains("This start runs with 1 mod off, and they are already back on for the next start.", now);
    }

    [Fact]
    public void Files_that_could_not_be_moved_are_named()
    {
        var view = View(Named("a", "Radar")) with { StartPlan = new PlanView.SafeNow(new[] { "a" }), PlanFailed = new[] { "a" } };

        var drawn = KnownFactsTests.Drawn(Page(view));

        Assert.Contains("These mods&#39; files couldn&#39;t be moved, so they may not be on or off as planned: Radar.", drawn);
    }

    [Fact]
    public void Without_a_plan_or_waiting_changes_the_bar_stays_hidden()
    {
        var drawn = KnownFactsTests.Drawn(Page(View(Named("a", "Radar"))));

        Assert.DoesNotContain("pend-plan", drawn);
    }

    [Fact]
    public void Health_offers_both_and_sends_each()
    {
        var engine = Page(View(Named("a", "Radar")));
        var page = OpenHealth(engine);

        Assert.Contains("Find the mod behind a problem", page);
        Assert.Contains("data-act=\"safeStart\"", page);
        Assert.Contains("data-act=\"findProblem\"", page);

        engine.Execute("fire('click', { target: act('safeStart') })");
        engine.Execute("fire('click', { target: act('findProblem') })");

        Assert.EndsWith("|safeStart", Posted(engine, "safeStart"));
        Assert.EndsWith("|findProblem", Posted(engine, "findProblem"));
    }

    [Theory]
    [InlineData(StartRefusal.NothingToTurnOff, "No mod can be turned off right now, so there is nothing to test.")]
    [InlineData(StartRefusal.FilesWaiting, "Some mod files can only be moved after the game restarts. Restart the game, then try again.")]
    public void Health_says_why_neither_can_start_instead_of_offering_them(StartRefusal refusal, string reason)
    {
        var page = OpenHealth(Page(View(Named("a", "Radar")) with { PlanRefusal = refusal }));

        Assert.Contains(reason, page);
        Assert.DoesNotContain("data-act=\"findProblem\"", page);
    }

    [Fact]
    public void While_a_search_is_set_up_health_points_to_its_buttons()
    {
        var view = View(Named("a", "Radar"), Named("b", "Farm Hub")) with { StartPlan = new PlanView.Round(1, new[] { "a" }, 2, false, false, false), PlanHolds = new[] { "a", "b" } };

        var page = OpenHealth(Page(view));

        Assert.Contains("FIND THE PROBLEM MOD is running. Its buttons are at the bottom of the screen.", page);
        Assert.DoesNotContain("data-act=\"safeStart\"", page);
    }

    [Fact]
    public void A_session_that_did_not_close_normally_is_a_warning_on_health_naming_the_mods_behind_its_errors()
    {
        var page = OpenHealth(Page(View(Named("a", "Radar"), Named("b", "Farm Hub")) with { LastCrash = new[] { "b", "a" } }));

        Assert.Contains("The game didn&#39;t close normally last time.", page);
        Assert.Contains("The newest errors before it ended came from Farm Hub, Radar.", page);
        Assert.Contains("1 warning", page);
        Assert.DoesNotContain("Nothing wrong was found: the install check is clean", page);
    }

    [Fact]
    public void A_crash_with_no_errors_before_it_says_none_were_logged()
    {
        var page = OpenHealth(Page(View(Named("a", "Radar")) with { LastCrash = Array.Empty<string>() }));

        Assert.Contains("No mod logged an error before it ended.", page);
    }

    [Fact]
    public void A_clean_last_session_shows_no_notice()
    {
        var page = OpenHealth(Page(View(Named("a", "Radar"))));

        Assert.DoesNotContain("data-health-section=\"crash\"", page);
        Assert.Contains("Nothing wrong was found", page);
    }

    [Fact]
    public void The_bar_and_health_speak_chinese_with_the_page()
    {
        var view = View(Named("a", "Radar"), Named("b", "Farm Hub")) with
        {
            IsChinese = true,
            StartPlan = new PlanView.Round(2, new[] { "a" }, 2, false, true, false),
            PlanHolds = new[] { "a", "b" },
            LastCrash = new[] { "a" },
        };
        var engine = Page(view);
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("查找问题模组：第 2 轮", drawn);
        Assert.Contains(">还是有问题</div>", drawn);

        var page = OpenHealth(engine);

        Assert.Contains("结束前最新的错误来自 Radar。", page);
        Assert.Contains("「查找问题模组」正在进行。", page);
    }

    [Fact]
    public void The_page_commands_are_read()
    {
        Assert.Equal(new PageCommand.SafeStart(), PageCommand.Parse("slmod|modmanager|safeStart"));
        Assert.Equal(new PageCommand.FindProblemMod(), PageCommand.Parse("slmod|modmanager|findProblem"));
        Assert.Equal(new PageCommand.AnswerRound(true), PageCommand.Parse("slmod|modmanager|planAnswer|1"));
        Assert.Equal(new PageCommand.AnswerRound(false), PageCommand.Parse("slmod|modmanager|planAnswer|0"));
        Assert.Equal(new PageCommand.StopPlan(), PageCommand.Parse("slmod|modmanager|planStop"));
        Assert.Equal(new PageCommand.DismissPlan(), PageCommand.Parse("slmod|modmanager|planOk"));
    }

    [Theory]
    [InlineData("slmod|modmanager|planAnswer")]
    [InlineData("slmod|modmanager|planAnswer|2")]
    [InlineData("slmod|modmanager|planAnswer|yes")]
    [InlineData("slmod|modmanager|safeStart|now")]
    [InlineData("slmod|modmanager|planStop|1")]
    public void A_plan_command_with_the_wrong_parts_is_not_read(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    // ===== Helpers =====

    private static readonly ModSwitch TurnsOff = new ModSwitch.Allowed(false, Array.Empty<string>());

    private static ModEntry Named(string guid, string name) =>
        new(guid, name, "1.0", guid + ".dll", new ModState.Loaded(Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());

    private static ModsView View(params ModEntry[] mods) => new(mods, Array.Empty<string>(), false, "F10");

    private static int Count(string text, string part)
    {
        var count = 0;

        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;

        return count;
    }

    private static Engine Page(ModsView view)
    {
        var engine = new Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(ItemFieldPageTests.StandIns);
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(view));

        return engine;
    }

    private static string OpenHealth(Engine engine) =>
        DrawnAfter(engine, "fire('click', { target: el('DIV', ['hd-title'], { page: 'health' }, { '[data-page]': { dataset: { page: 'health' } } }) })");

    private static string Posted(Engine engine, string verb) =>
        engine.Evaluate("posted.filter(function (text) { return text.indexOf('|" + verb + "|') >= 0 || text.endsWith('|" + verb + "'); }).join('\\n')").AsString();

    private static string DrawnAfter(Engine engine, string script)
    {
        engine.Execute("drawn.length = 0");
        engine.Execute(script);

        return KnownFactsTests.Drawn(engine);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(StartPlanPageTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
