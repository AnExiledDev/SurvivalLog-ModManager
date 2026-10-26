using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ModProblemsTests
{
    private const string StartLine = "===== Mod Manager 0.8.2 started";

    private static LoggedEvent Error(string source, string text) => new(ProblemLevel.Error, source, text);

    private static Problem Logged(LoggedEvent logged)
    {
        var book = new ProblemBook();

        book.Add(logged);

        return book.All().Single();
    }

    private static readonly ProblemOwner Toolkit = new("com.a.toolkit", "Exiled's Toolkit", new[] { "ExiledToolkit" }, IsLibrary: true);
    private static readonly ProblemOwner Hub = new("com.a.hub", "Crafting Hub", new[] { "CraftingHub" }, IsLibrary: false);
    private static readonly ProblemOwner Trap = new("com.b.trap", "Trapline", new[] { "Trapline" }, IsLibrary: false);
    private static readonly ProblemOwner[] Owners = { Toolkit, Hub, Trap };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<PatchOverlap>> NoOverlaps = new Dictionary<string, IReadOnlyList<PatchOverlap>>();

    private static SharedMethod Shared(string method) => new(method, PatchRisk.MayClash, new[] { PatchEffect.Rewrites }, new[] { PatchEffect.Rewrites });

    [Fact]
    public void An_error_that_repeats_every_frame_is_one_problem_with_a_count()
    {
        var book = new ProblemBook();

        for (var i = 0; i < 500; i++) book.Add(Error("Crafting Hub", "boom\nat CraftingHub.Ticker.Tick()"));

        book.Add(Error("Crafting Hub", "other\nat CraftingHub.Ticker.Tick()"));

        var all = book.All();

        Assert.Equal(2, all.Count);
        Assert.Equal("other", all[0].Message);
        Assert.Equal(500, all[1].Count);
    }

    [Fact]
    public void The_same_text_from_two_sources_or_levels_stays_two_problems()
    {
        var book = new ProblemBook();

        book.Add(Error("One", "boom"));
        book.Add(Error("Two", "boom"));
        book.Add(new LoggedEvent(ProblemLevel.Warning, "One", "boom"));

        Assert.Equal(3, book.All().Count);
    }

    [Fact]
    public void Past_the_cap_new_kinds_are_only_counted_but_known_ones_still_count_up()
    {
        var book = new ProblemBook();

        for (var i = 0; i < ProblemBook.MaxProblems + 7; i++) book.Add(Error("Mod", "problem " + i));

        book.Add(Error("Mod", "problem 0"));

        Assert.Equal(ProblemBook.MaxProblems, book.All().Count);
        Assert.Equal(7, book.Dropped);
        Assert.Equal(2, book.All().Single(problem => problem.Message == "problem 0").Count);
    }

    [Fact]
    public void Events_lost_in_a_storm_count_as_not_shown_and_redraw_the_screen()
    {
        var book = new ProblemBook();

        book.AddLost(12);
        book.AddLost(0);

        Assert.Equal(12, book.Dropped);
        Assert.Equal(1, book.Version);
        Assert.Empty(book.All());
    }

    [Fact]
    public void Every_kept_event_moves_the_version_so_an_open_screen_redraws()
    {
        var book = new ProblemBook();

        book.Add(Error("Mod", "boom"));
        var first = book.Version;
        book.Add(Error("Mod", "boom"));

        Assert.True(book.Version > first);
    }

    [Theory]
    [InlineData(ProblemLevel.Warning, "Il2CppInterop", "Method System.Void CraftingHub.Adapters.CraftTicker::NoteFailure(System.Exception) has unsupported parameter System.Exception ex of type System.Exception")]
    [InlineData(ProblemLevel.Warning, "HarmonyX", "AccessTools.GetTypesFromAssembly: assembly X => System.Reflection.ReflectionTypeLoadException")]
    [InlineData(ProblemLevel.Warning, "Il2CppInterop", "Class::Init signatures have been exhausted, using a substitute!")]
    public void Warnings_every_modded_game_logs_are_not_listed(ProblemLevel level, string source, string text)
    {
        var book = new ProblemBook();

        book.Add(new LoggedEvent(level, source, text));

        Assert.Empty(book.All());
    }

    [Fact]
    public void An_interop_error_about_a_parameter_is_still_listed()
    {
        var book = new ProblemBook();

        book.Add(Error("Il2CppInterop", "Method X has unsupported parameter Y"));

        Assert.Single(book.All());
    }

    [Fact]
    public void Splitting_keeps_the_message_and_the_first_stack_lines_apart()
    {
        var text = "During invoking native->managed trampoline\r\nException: System.NullReferenceException: Object reference\n  at CraftingHub.Ticker.Tick () [0x00000]\n--- End of stack trace ---\n  at DMD<GameCore.Grid::Update>(GameCore.Grid this)\n";

        var (message, stack) = ProblemBook.Split(text);

        Assert.Equal("During invoking native->managed trampoline\nException: System.NullReferenceException: Object reference", message);
        Assert.Equal(new[] { "at CraftingHub.Ticker.Tick () [0x00000]", "at DMD<GameCore.Grid::Update>(GameCore.Grid this)" }, stack);
    }

    [Fact]
    public void A_long_message_and_a_deep_stack_are_cut()
    {
        var text = new string('x', ProblemBook.MaxMessageLength + 50) + string.Concat(Enumerable.Range(0, 40).Select(i => "\nat A.B.M" + i + "()"));

        var (message, stack) = ProblemBook.Split(text);

        Assert.Equal(ProblemBook.MaxMessageLength + 3, message.Length);
        Assert.EndsWith("...", message);
        Assert.Equal(ProblemBook.MaxStackLines, stack.Count);
    }

    [Fact]
    public void Stack_methods_are_named_like_the_conflicts_tab_names_a_patched_method()
    {
        var methods = Frames.Methods(new[]
        {
            "at DMD<GameCore.HotUpdate.ConfigManager::SwitchLanguage>(GameCore.HotUpdate.ConfigManager this)",
            "at Trapline.Patches.GridHooks.Postfix () [0x0001]",
            "at System.Collections.Generic.List`1.Add(T item)",
            "at Outer+Inner.Run()",
            "at Trapline.Patches.GridHooks.Postfix () [0x0099]",
            "at nothing-to-read",
        });

        Assert.Equal(new[] { "ConfigManager.SwitchLanguage", "GridHooks.Postfix", "List.Add", "Inner.Run" }, methods);
    }

    [Fact]
    public void A_problem_belongs_to_the_mod_whose_name_it_was_logged_under()
    {
        var problem = Logged(Error("trapline", "boom\nat CraftingHub.Ticker.Tick()"));

        Assert.Equal("com.b.trap", ProblemOwners.OwnerOf(problem, Owners));
    }

    [Fact]
    public void A_stack_through_a_library_belongs_to_the_mod_that_called_it()
    {
        var problem = Logged(Error("Il2CppInterop", "boom\nat ExiledToolkit.Adapters.Ui.Draw()\nat CraftingHub.Adapters.Panel.Show()"));

        Assert.Equal("com.a.hub", ProblemOwners.OwnerOf(problem, Owners));
    }

    [Fact]
    public void A_stack_only_in_a_library_belongs_to_the_library()
    {
        var problem = Logged(Error("Il2CppInterop", "boom\nat ExiledToolkit.Adapters.Ui.Draw()\nat GameCore.Grid.Update()"));

        Assert.Equal("com.a.toolkit", ProblemOwners.OwnerOf(problem, Owners));
    }

    [Fact]
    public void A_mod_named_in_the_text_owns_it_but_not_one_whose_name_is_only_inside_another_word()
    {
        var named = Logged(Error("Il2CppInterop", "Assembly CraftingHub.dll failed: CraftingHub.Core.Recipe missing"));
        var inside = Logged(Error("Il2CppInterop", "type MyTrapline.Thing missing"));

        Assert.Equal("com.a.hub", ProblemOwners.OwnerOf(named, Owners));
        Assert.Null(ProblemOwners.OwnerOf(inside, Owners));
    }

    [Fact]
    public void A_mod_named_after_a_game_namespace_does_not_claim_the_game_stack()
    {
        var owners = new[] { new ProblemOwner("com.x.unity", "Unity Fix", new[] { "UnityEngine" }, false) };
        var problem = Logged(Error("Unity", "boom\nat UnityEngine.Object.Destroy()"));

        Assert.Null(ProblemOwners.OwnerOf(problem, owners));
    }

    [Fact]
    public void An_ownerless_error_through_a_method_two_mods_patch_goes_to_both_with_the_clash()
    {
        var problem = Logged(Error("Il2CppInterop", "During invoking native->managed trampoline\nat DMD<GameCore.Grid::OnSwitchLanguage>(GameCore.Grid this)"));
        var overlaps = new Dictionary<string, IReadOnlyList<PatchOverlap>>
        {
            ["com.a.hub"] = new[] { new PatchOverlap("com.b.trap", PatchRisk.MayClash, new[] { Shared("Grid.OnSwitchLanguage"), Shared("Grid.Other") }) },
            ["com.b.trap"] = new[] { new PatchOverlap("com.a.hub", PatchRisk.MayClash, new[] { Shared("Grid.OnSwitchLanguage") }) },
            ["com.a.toolkit"] = new[] { new PatchOverlap("com.c.x", PatchRisk.MayClash, new[] { Shared("Grid.Other") }) },
        };

        var (byMod, unclaimed) = ProblemOwners.Assign(new[] { problem }, Owners, overlaps);

        Assert.Empty(unclaimed);
        Assert.Equal(new[] { "com.a.hub", "com.b.trap" }, byMod.Keys.OrderBy(key => key));
        Assert.Equal("Grid.OnSwitchLanguage", byMod["com.a.hub"].Single().Clashes.Single().Method);
        Assert.Equal(new[] { "com.b.trap" }, byMod["com.a.hub"].Single().Clashes.Single().Guids);
    }

    [Fact]
    public void An_owned_error_through_a_shared_method_names_the_other_mods()
    {
        var problem = Logged(Error("Crafting Hub", "boom\nat CraftingHub.Patches.Hook.Prefix()\nat DMD<GameCore.Grid::Update>(GameCore.Grid this)"));
        var overlaps = new Dictionary<string, IReadOnlyList<PatchOverlap>>
        {
            ["com.a.hub"] = new[] { new PatchOverlap("com.b.trap", PatchRisk.MayClash, new[] { Shared("Grid.Update") }) },
            ["com.b.trap"] = new[] { new PatchOverlap("com.a.hub", PatchRisk.MayClash, new[] { Shared("Grid.Update") }) },
        };

        var (byMod, _) = ProblemOwners.Assign(new[] { problem }, Owners, overlaps);

        Assert.Equal(new[] { "com.a.hub" }, byMod.Keys);
        Assert.Equal(new[] { "com.b.trap" }, byMod["com.a.hub"].Single().Clashes.Single().Guids);
    }

    [Fact]
    public void An_error_no_mod_can_be_found_for_is_unclaimed()
    {
        var problem = Logged(Error("Unity", "boom\nat GameCore.Grid.Update()"));

        var (byMod, unclaimed) = ProblemOwners.Assign(new[] { problem }, Owners, NoOverlaps);

        Assert.Empty(byMod);
        Assert.Equal("boom", unclaimed.Single().Problem.Message);
    }

    [Fact]
    public void The_log_file_gives_the_warnings_and_errors_above_the_start_line_with_their_stacks()
    {
        var text =
            "[Message:   BepInEx] BepInEx 6.0.0\r\n" +
            "[Warning:  HarmonyX] Could not find method SwitchLanguage\r\n" +
            "[Error  :Il2CppInterop] During invoking native->managed trampoline\r\n" +
            "Exception: System.Exception: bad\r\n" +
            "  at Trapline.Hooks.Run()\r\n" +
            "[Info   :   Trapline] loaded\r\n" +
            "  this line belongs to no warning\r\n" +
            "[Fatal  :     Unity] crash\r\n" +
            "[Info   :Mod Manager] " + StartLine + ", open key F10\r\n" +
            "[Error  :     Later] after the start line\r\n";

        var events = LogFile.Before(text, StartLine);

        Assert.NotNull(events);
        Assert.Equal(
            new[]
            {
                new LoggedEvent(ProblemLevel.Warning, "HarmonyX", "Could not find method SwitchLanguage"),
                new LoggedEvent(ProblemLevel.Error, "Il2CppInterop", "During invoking native->managed trampoline\nException: System.Exception: bad\n  at Trapline.Hooks.Run()"),
                new LoggedEvent(ProblemLevel.Error, "Unity", "crash"),
            },
            events);
    }

    [Fact]
    public void The_log_file_gives_nothing_until_the_start_line_is_written()
    {
        Assert.Null(LogFile.Before("[Error  :     Unity] crash\n", StartLine));
    }

    [Fact]
    public void A_bracketed_line_that_is_not_a_header_stays_part_of_the_error()
    {
        var events = LogFile.Before("[Error  :Mod] list:\n[1, 2]\n[note: see above]\n" + StartLine, StartLine);

        Assert.Equal("list:\n[1, 2]\n[note: see above]", Assert.Single(events!).Text);
    }

    [Fact]
    public void The_report_lists_errors_first_under_their_mod_with_the_clash()
    {
        var error = new Problem(ProblemLevel.Error, "Il2CppInterop", "trampoline\nException: bad", new[] { "at A.B.C()", "at A.B.D()", "at A.B.E()", "at A.B.F()" }, new[] { "Grid.Update" }, 3, 2);
        var warning = new Problem(ProblemLevel.Warning, "Trapline", "target missing", Array.Empty<string>(), Array.Empty<string>(), 1, 1);
        var trap = Mod("com.b.trap", "Trapline") with
        {
            Errors = new[] { new ModProblem(warning, Array.Empty<ClashHint>()), new ModProblem(error, new[] { new ClashHint("Grid.Update", new[] { "com.a.hub" }) }) },
        };
        var view = new ModsView(new[] { Mod("com.a.hub", "Crafting Hub"), trap }, Array.Empty<string>(), false, "F10")
        {
            OtherErrors = new[] { new ModProblem(warning with { Source = "Unity", Message = "game warning" }, Array.Empty<ClashHint>()) },
            ErrorsNotKept = 4,
        };

        var text = ModReport.Text(view);

        Assert.EndsWith(
            "Errors this session:\n" +
            "- Trapline: error x3: trampoline Exception: bad\n" +
            "    at A.B.C()\n    at A.B.D()\n    at A.B.E()\n" +
            "    went through Grid.Update, which Crafting Hub also patches\n" +
            "- Trapline: warning: target missing\n" +
            "- BepInEx or the game: warning: game warning\n" +
            "- and 4 more\n",
            text);
    }

    [Fact]
    public void The_page_gets_each_mods_errors_and_the_unclaimed_ones()
    {
        var problem = new Problem(ProblemLevel.Error, "Trapline", "boom", new[] { "at A.B.C()" }, new[] { "B.C" }, 2, 1);
        var mod = Mod("com.b.trap", "Trapline") with { Errors = new[] { new ModProblem(problem, new[] { new ClashHint("B.C", new[] { "com.a.hub" }) }) } };
        var view = new ModsView(new[] { mod }, Array.Empty<string>(), false, "F10") { OtherErrors = new[] { new ModProblem(problem with { Level = ProblemLevel.Warning }, Array.Empty<ClashHint>()) } };

        using var json = JsonDocument.Parse(PageScript.ViewJson(view));
        var error = json.RootElement.GetProperty("mods")[0].GetProperty("errors")[0];

        Assert.Equal("error", error.GetProperty("level").GetString());
        Assert.Equal("boom", error.GetProperty("message").GetString());
        Assert.Equal(2, error.GetProperty("count").GetInt32());
        Assert.Equal("at A.B.C()", error.GetProperty("stack")[0].GetString());
        Assert.Equal("com.a.hub", error.GetProperty("clashes")[0].GetProperty("guids")[0].GetString());
        Assert.Equal("warning", json.RootElement.GetProperty("otherErrors")[0].GetProperty("level").GetString());
    }

    private static ModEntry Mod(string guid, string name) =>
        new(guid, name, "1.0", name + ".dll", new ModState.Loaded(Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());
}
