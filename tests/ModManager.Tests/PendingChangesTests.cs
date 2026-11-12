using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The bar above the footer lists what takes effect at the next game start, each with the click that takes it back,
/// and QUIT GAME for when the player wants the changes now. A rollback is taken back by rolling forward again to the
/// version the game runs, which is then nothing to wait for.
/// </summary>
public sealed class PendingChangesTests
{
    // ===== Rolling forward again =====

    [Fact]
    public void The_files_on_disk_are_the_installed_update_then_the_rollback_then_the_running_version()
    {
        Assert.Equal("v1.3.0", Rollback.OnDisk(new InstallState.Installed("v1.3.0"), new RollbackState.Available("1.2.0"), "1.2.0"));
        Assert.Equal("1.0.0", Rollback.OnDisk(new InstallState.None(), new RollbackState.Done("1.0.0"), "1.2.0"));
        Assert.Equal("1.2.0", Rollback.OnDisk(new InstallState.None(), new RollbackState.Available("1.0.0"), "1.2.0"));
    }

    [Fact]
    public void A_rollback_to_an_older_version_is_undone_by_going_back_to_the_running_one()
    {
        Assert.Equal("1.2.0", Rollback.UndoTo(new RollbackState.Done("1.0.0"), "1.2.0"));
        Assert.False(Rollback.IsBackToRunning(new RollbackState.Done("1.0.0"), "1.2.0"));
    }

    [Fact]
    public void A_rollback_that_ends_on_the_running_version_waits_for_nothing()
    {
        // Undoing an installed update rolls back to the files the game runs now, named after the tag or the version.
        Assert.True(Rollback.IsBackToRunning(new RollbackState.Done("v1.2.0"), "1.2.0"));
        Assert.Null(Rollback.UndoTo(new RollbackState.Done("v1.2.0"), "1.2.0"));
    }

    [Theory]
    [MemberData(nameof(NotRolledBack))]
    public void Only_a_rollback_that_was_done_can_be_undone(RollbackState rollback)
    {
        Assert.Null(Rollback.UndoTo(rollback, "1.2.0"));
    }

    public static IEnumerable<object[]> NotRolledBack() => new[]
    {
        new object[] { new RollbackState.None() },
        new object[] { new RollbackState.Available("1.0.0") },
        new object[] { new RollbackState.Failed("1.0.0", "the file is in use") },
    };

    [Fact]
    public void Rolling_back_and_then_forward_puts_the_running_versions_files_back()
    {
        var mod = Path.Combine("plugins", "Mod");
        var dll = Path.Combine(mod, "Mod.dll");
        var shelf = Path.Combine("mod-manager", "backups", "com.x");
        string Folder(string version) => Path.Combine(shelf, BackupFolder.Name(version));
        var disk = new MemoryDisk((dll, "dll 1.2.0"), (Path.Combine(Folder("1.0.0"), "Mod.dll"), "dll 1.0.0"));
        RollbackState rollback = new RollbackState.Available("1.0.0");

        // What the installer does: the files on disk go to their own version's folder, the chosen backup is copied in.
        RollbackState Swap(string version)
        {
            var onDisk = Rollback.OnDisk(new InstallState.None(), rollback, "1.2.0");
            var (steps, problem) = RollbackPlan.For(mod, "Mod.dll", Folder(version), new[] { "Mod.dll" }, Array.Empty<string>(), Folder(onDisk));

            Assert.Null(problem);

            return FileSwap.Restore(disk, steps!, version);
        }

        rollback = Swap("1.0.0");

        Assert.Equal(new RollbackState.Done("1.0.0"), rollback);
        Assert.Equal("dll 1.0.0", disk.Read(dll));

        rollback = Swap(Rollback.UndoTo(rollback, "1.2.0")!);

        Assert.Equal(new RollbackState.Done("1.2.0"), rollback);
        Assert.Equal("dll 1.2.0", disk.Read(dll));
        Assert.True(Rollback.IsBackToRunning(rollback, "1.2.0"));
        Assert.Equal("dll 1.0.0", disk.Read(Path.Combine(Folder("1.0.0"), "Mod.dll")));
    }

    [Fact]
    public void The_page_asks_to_roll_forward_and_to_quit()
    {
        Assert.Equal(new PageCommand.RollForward("com.x"), PageCommand.Parse("slmod|modmanager|rollForward|com.x"));
        Assert.Equal(new PageCommand.QuitGame(), PageCommand.Parse("slmod|modmanager|quitGame"));
    }

    [Theory]
    [InlineData("slmod|modmanager|rollForward")]
    [InlineData("slmod|modmanager|rollForward|g|1.0")]
    [InlineData("slmod|modmanager|quitGame|now")]
    public void A_roll_forward_or_quit_with_the_wrong_parts_is_not_read(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    [Fact]
    public void A_mods_json_says_whether_its_rollback_ends_on_the_running_version()
    {
        Assert.Contains("\"same\":true", PageScript.ModJson(Loaded("g") with { Version = "1.2.0", Rollback = new RollbackState.Done("v1.2.0") }));
        Assert.Contains("\"same\":false", PageScript.ModJson(Loaded("g") with { Version = "1.2.0", Rollback = new RollbackState.Done("1.0.0") }));
    }

    // ===== The bar =====

    [Fact]
    public void Nothing_waiting_draws_no_bar()
    {
        var engine = Page(View(Loaded("g", Zoom("g", "false", ApplyMode.Restart))));

        Assert.DoesNotContain("pend-hd", KnownFactsTests.Drawn(engine));
    }

    [Fact]
    public void A_mod_turned_off_this_session_is_listed_and_undo_turns_it_back_on()
    {
        var engine = Page(View(Loaded("g") with { Switch = new ModSwitch.Allowed(false, Array.Empty<string>()) }));
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("1 change waits for the next start.", drawn);
        Assert.Contains("<span class=\"pn\">g</span><span class=\"pt\">turns off</span>", drawn);

        engine.Execute("fire('click', { target: undoAt(0) })");

        Assert.EndsWith("|switch|g|1", Posted(engine, "switch"));
    }

    [Fact]
    public void A_mod_turned_on_this_session_is_undone_by_turning_it_off_again()
    {
        var engine = Page(View(Loaded("g") with { State = new ModState.Off(), Switch = new ModSwitch.Allowed(true, Array.Empty<string>()) }));

        Assert.Contains("<span class=\"pt\">turns on</span>", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: undoAt(0) })");

        Assert.EndsWith("|switch|g|0", Posted(engine, "switch"));
    }

    [Fact]
    public void An_uninstalled_mod_is_undone_by_restoring_it_from_the_trash()
    {
        var engine = Page(View(Loaded("g") with { Switch = new ModSwitch.Uninstalled("t1") }));

        Assert.Contains("<span class=\"pt\">uninstalled</span>", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: undoAt(0) })");

        Assert.EndsWith("|restore|t1", Posted(engine, "restore"));
    }

    [Fact]
    public void A_held_switch_is_listed_without_undo()
    {
        var engine = Page(View(Loaded("g") with { Switch = new ModSwitch.Held(false), Rollback = new RollbackState.Done("1.0") }));
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("turns off, its files change at the next start, so restart first", drawn);
        Assert.DoesNotContain("data-undo=\"0\"", drawn);
    }

    [Fact]
    public void An_installed_update_is_undone_by_rolling_back()
    {
        var engine = Page(View(Loaded("g") with { Install = new InstallState.Installed("v1.1.0"), Rollback = new RollbackState.Available("1.0") }));

        Assert.Contains("update to 1.1.0 installed", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: undoAt(0) })");

        Assert.EndsWith("|rollback|g", Posted(engine, "rollback"));
    }

    [Fact]
    public void A_rollback_is_undone_by_rolling_forward()
    {
        var engine = Page(View(Loaded("g") with { Version = "1.2.0", Rollback = new RollbackState.Done("1.0.0") }));

        Assert.Contains("rolled back to 1.0.0", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: undoAt(0) })");

        Assert.EndsWith("|rollForward|g", Posted(engine, "rollForward"));
    }

    [Fact]
    public void A_rollback_back_to_the_running_version_is_not_listed()
    {
        var engine = Page(View(Loaded("g") with { Version = "1.2.0", Rollback = new RollbackState.Done("v1.2.0") }));

        Assert.DoesNotContain("pend-hd", KnownFactsTests.Drawn(engine));
    }

    [Fact]
    public void A_restart_setting_changed_this_session_is_undone_by_its_value_at_start()
    {
        var engine = Page(View(Loaded("g", Zoom("g", "true", ApplyMode.Restart))));

        Assert.Contains("Zoom changed, applies after a restart", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: undoAt(0) })");

        Assert.EndsWith("|set|g|General|Zoom|false", Posted(engine, "set"));
    }

    [Fact]
    public void A_setting_that_may_need_a_restart_is_listed_as_such_and_a_live_one_not_at_all()
    {
        var unknown = KnownFactsTests.Drawn(Page(View(Loaded("g", Zoom("g", "true", ApplyMode.Unknown)))));
        var live = KnownFactsTests.Drawn(Page(View(Loaded("g", Zoom("g", "true", ApplyMode.Live)))));

        Assert.Contains("Zoom changed, may apply only after a restart", unknown);
        Assert.DoesNotContain("pend-hd", live);
    }

    [Fact]
    public void Every_waiting_change_is_counted()
    {
        var engine = Page(View(
            Loaded("a") with { Switch = new ModSwitch.Allowed(false, Array.Empty<string>()) },
            Loaded("b", Zoom("b", "true", ApplyMode.Restart)) with { Switch = new ModSwitch.Uninstalled("t2") }));

        Assert.Contains("3 changes wait for the next start.", KnownFactsTests.Drawn(engine));
    }

    [Fact]
    public void Quit_game_asks_first_and_cancel_sends_nothing()
    {
        var engine = Page(View(Loaded("g") with { Switch = new ModSwitch.Allowed(false, Array.Empty<string>()) }));

        Assert.Contains("data-act=\"quitGame\"", KnownFactsTests.Drawn(engine));

        var asked = DrawnAfter(engine, "fire('click', { target: act('quitGame') })");

        Assert.Contains("<b>Quit the game now?</b>", asked);
        Assert.DoesNotContain("data-act=\"quitGame\"", asked);

        var cancelled = DrawnAfter(engine, "fire('click', { target: act('quitNo') })");

        Assert.DoesNotContain("Quit the game now?", cancelled);
        Assert.Contains("data-act=\"quitGame\"", cancelled);
        Assert.Equal("", Posted(engine, "quitGame"));
    }

    [Fact]
    public void Quit_now_after_asking_quits()
    {
        var engine = Page(View(Loaded("g") with { Switch = new ModSwitch.Allowed(false, Array.Empty<string>()) }));

        engine.Execute("fire('click', { target: act('quitYes') })");

        Assert.Equal("", Posted(engine, "quitGame"));

        engine.Execute("fire('click', { target: act('quitGame') })");
        engine.Execute("fire('click', { target: act('quitYes') })");

        Assert.EndsWith("|quitGame", Posted(engine, "quitGame"));
    }

    [Fact]
    public void Hide_folds_the_list_and_keeps_the_count()
    {
        var engine = Page(View(Loaded("g") with { Switch = new ModSwitch.Allowed(false, Array.Empty<string>()) }));

        var hidden = DrawnAfter(engine, "fire('click', { target: act('pendToggle') })");

        Assert.Contains("1 change waits for the next start.", hidden);
        Assert.Contains(">SHOW</div>", hidden);
        Assert.DoesNotContain("pend-row", hidden);

        var shown = DrawnAfter(engine, "fire('click', { target: act('pendToggle') })");

        Assert.Contains("pend-row", shown);
    }

    [Fact]
    public void The_bar_speaks_chinese_with_the_page()
    {
        var engine = Page(View(Loaded("g", Zoom("g", "true", ApplyMode.Restart))) with { IsChinese = true });
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("1 项改动将在下次启动时生效。", drawn);
        Assert.Contains("「Zoom」已修改，重启后生效", drawn);
        Assert.Contains(">退出游戏</div>", drawn);
    }

    // ===== Helpers =====

    private static ModEntry Loaded(string guid, params Setting[] settings) =>
        new(guid, guid, "1.0", guid + ".dll", new ModState.Loaded(settings), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());

    private static Setting Zoom(string guid, string current, ApplyMode apply) =>
        new(new SettingId(guid, "General", "Zoom"), "Zoom", new Description("Zoom.", "缩放。"), new SettingKind.Toggle(), "false", current, "false", false) { Apply = apply };

    private static ModsView View(params ModEntry[] mods) => new(mods, Array.Empty<string>(), false, "F10");

    private static Jint.Engine Page(ModsView view)
    {
        var engine = new Jint.Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(ItemFieldPageTests.StandIns);
        engine.Execute("function undoAt(i) { var data = { act: 'undo', undo: String(i) }; return el('DIV', [], data, { '[data-act]': { dataset: data } }); }");

        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(view));

        return engine;
    }

    private static string Posted(Jint.Engine engine, string verb) =>
        engine.Evaluate("posted.filter(function (text) { return text.indexOf('|" + verb + "|') >= 0 || text.endsWith('|" + verb + "'); }).join('\\n')").AsString();

    private static string DrawnAfter(Jint.Engine engine, string script)
    {
        engine.Execute("drawn.length = 0");
        engine.Execute(script);

        return KnownFactsTests.Drawn(engine);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PendingChangesTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>Files in memory, by full path, as the plan gives full paths.</summary>
    private sealed class MemoryDisk : IFileMoves
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public MemoryDisk(params (string Path, string Content)[] files)
        {
            foreach (var (path, content) in files) _files[Path.GetFullPath(path)] = content;
        }

        public string Read(string path) => _files[Path.GetFullPath(path)];

        public bool Exists(string path) => _files.ContainsKey(Path.GetFullPath(path));

        public void MakeFolderFor(string path)
        {
        }

        public void Move(string from, string to)
        {
            if (!Exists(from)) throw new FileNotFoundException(from);

            if (Exists(to)) throw new IOException(to + " already exists");

            _files[Path.GetFullPath(to)] = Read(from);
            _files.Remove(Path.GetFullPath(from));
        }

        public void Copy(string from, string to) => _files[Path.GetFullPath(to)] = Exists(from) ? Read(from) : throw new FileNotFoundException(from);

        public void Delete(string path) => _files.Remove(Path.GetFullPath(path));
    }
}
