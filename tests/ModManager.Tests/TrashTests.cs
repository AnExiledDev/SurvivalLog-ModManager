using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jint;
using ModManager.Adapters;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// UNINSTALL moves a mod's DLL, and its .cfg when the player asks, into a folder of its own in the trash, with a record
/// of where each file was. RESTORE puts them back exactly there; EMPTY TRASH deletes for good everything but what was
/// uninstalled this session, since the game still runs those DLLs and the player may still want them back.
/// </summary>
public sealed class TrashTests : IDisposable
{
    private readonly string _game = Path.Combine(Path.GetTempPath(), "mm-trash-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _logged = new();

    public TrashTests()
    {
        Directory.CreateDirectory(Plugins);
        Directory.CreateDirectory(Config);
    }

    private string Plugins => Path.Combine(_game, "plugins");
    private string Off => Path.Combine(_game, "disabled-plugins");
    private string Config => Path.Combine(_game, "config");
    private string Root => Path.Combine(_game, "mod-manager", "trash");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_game, true);
        }
        catch (IOException)
        {
            // A temp folder left behind is harmless.
        }
    }

    private TrashCan NewSession() => new(Root, new[] { Plugins, Off, Config }, _logged.Add);

    private string Write(string folder, string name, string text)
    {
        var path = Path.Combine(folder, name);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);

        return path;
    }

    [Fact]
    public void Uninstalling_moves_the_dll_and_the_chosen_settings_file_into_the_trash()
    {
        var dll = Write(Plugins, Path.Combine("Map", "Map.dll"), "map dll");
        var cfg = Write(Config, "com.ann.map.cfg", "[General]\nZoom = 2\n");
        var trash = NewSession();

        var item = trash.Put("com.ann.map", "Map", "1.2.0", dll, cfg);

        Assert.False(File.Exists(dll));
        Assert.False(File.Exists(cfg));
        Assert.Equal("map dll", File.ReadAllText(trash.StoredPath(item, item.Dll)));
        Assert.Equal(dll, item.Dll.From);
        Assert.True(item.HasSettings);
        Assert.True(trash.IsThisSession(item.Id));
        Assert.Equal(new[] { item.Id }, trash.Items().Select(each => each.Id));
    }

    [Fact]
    public void A_settings_file_the_player_keeps_stays_where_it_is()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var cfg = Write(Config, "com.ann.map.cfg", "[General]\n");

        var item = NewSession().Put("com.ann.map", "Map", "1.2.0", dll, null);

        Assert.True(File.Exists(cfg));
        Assert.False(item.HasSettings);
    }

    [Fact]
    public void A_file_outside_the_mod_folders_is_refused_and_nothing_moves()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var elsewhere = Write(_game, "notes.cfg", "mine");

        Assert.ThrowsAny<IOException>(() => NewSession().Put("com.ann.map", "Map", "1.2.0", dll, elsewhere));
        Assert.True(File.Exists(dll));
        Assert.True(File.Exists(elsewhere));
        Assert.False(Directory.Exists(Root) && Directory.EnumerateFileSystemEntries(Root).Any());
    }

    [Fact]
    public void When_the_second_file_cannot_move_the_first_goes_back_and_no_item_is_left()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var missing = Path.Combine(Config, "com.ann.map.cfg");
        var trash = NewSession();

        Assert.ThrowsAny<IOException>(() => trash.Put("com.ann.map", "Map", "1.2.0", dll, missing));
        Assert.Equal("map dll", File.ReadAllText(dll));
        Assert.Empty(trash.Items());
        Assert.False(Directory.Exists(Root) && Directory.EnumerateDirectories(Root).Any());
    }

    [Fact]
    public void Restoring_puts_every_file_back_where_it_was_and_removes_the_item()
    {
        var dll = Write(Off, Path.Combine("Map", "Map.dll"), "map dll");
        var cfg = Write(Config, "com.ann.map.cfg", "[General]\nZoom = 2\n");
        var trash = NewSession();
        var item = trash.Put("com.ann.map", "Map", "1.2.0", dll, cfg);

        trash.Restore(item);

        Assert.Equal("map dll", File.ReadAllText(dll));
        Assert.Equal("[General]\nZoom = 2\n", File.ReadAllText(cfg));
        Assert.Empty(trash.Items());
        Assert.False(trash.IsThisSession(item.Id));
        Assert.False(Directory.Exists(Path.Combine(Root, item.Id)));
    }

    [Fact]
    public void A_restore_onto_a_file_that_is_back_already_moves_nothing()
    {
        var dll = Write(Plugins, "Map.dll", "old dll");
        var cfg = Write(Config, "com.ann.map.cfg", "old settings");
        var trash = NewSession();
        var item = trash.Put("com.ann.map", "Map", "1.2.0", dll, cfg);

        Write(Plugins, "Map.dll", "reinstalled dll");

        var taken = Assert.Throws<TrashTaken>(() => trash.Restore(item));

        Assert.Equal(dll, taken.Path);
        Assert.Equal("reinstalled dll", File.ReadAllText(dll));
        Assert.False(File.Exists(cfg));
        Assert.Single(trash.Items());
    }

    [Fact]
    public void An_item_from_an_earlier_session_is_listed_and_can_be_restored()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var first = NewSession().Put("com.ann.map", "Map", "1.2.0", dll, null);
        var next = NewSession();

        var listed = Assert.Single(next.Items());

        Assert.Equal(first.Id, listed.Id);
        Assert.Equal("Map", listed.Name);
        Assert.False(next.IsThisSession(listed.Id));

        next.Restore(listed);

        Assert.Equal("map dll", File.ReadAllText(dll));
    }

    [Fact]
    public void Emptying_deletes_earlier_sessions_items_and_keeps_this_sessions()
    {
        var earlier = NewSession().Put("com.ann.map", "Map", "1.2.0", Write(Plugins, "Map.dll", "map"), null);
        var now = NewSession();
        var kept = now.Put("com.bob.radar", "Radar", "1.3", Write(Plugins, "Radar.dll", "radar"), null);

        var (deleted, keptCount, failed) = now.Empty();

        Assert.Equal(1, deleted);
        Assert.Equal(1, keptCount);
        Assert.Empty(failed);
        Assert.False(Directory.Exists(Path.Combine(Root, earlier.Id)));
        Assert.Equal(new[] { kept.Id }, now.Items().Select(item => item.Id));
    }

    [Fact]
    public void Emptying_leaves_a_folder_that_holds_files_but_no_readable_record()
    {
        var stray = Write(Root, Path.Combine("stray", "Thing.dll"), "?");
        var broken = Write(Root, Path.Combine("broken", TrashItem.FileName), "{ not json");
        var newer = Write(Root, Path.Combine("newer", "Map.dll"), "map dll");

        Write(Root, Path.Combine("newer", TrashItem.FileName), "{ \"from\": \"a newer version\" }");
        NewSession().Empty();

        Assert.True(File.Exists(stray));
        Assert.True(File.Exists(newer));
        Assert.False(Directory.Exists(Path.GetDirectoryName(broken)));
    }

    [Fact]
    public void A_trash_folder_without_a_readable_record_is_not_listed_and_is_logged_once()
    {
        Write(Root, Path.Combine("stray", "Thing.dll"), "?");
        Write(Root, Path.Combine("broken", TrashItem.FileName), "{ not json");
        var trash = NewSession();

        Assert.Empty(trash.Items());

        // A new item makes the trash read again; the two folders are not reported a second time.
        trash.Put("com.ann.map", "Map", "1.2.0", Write(Plugins, "Map.dll", "map"), null);

        Assert.Single(trash.Items());
        Assert.Single(_logged, line => line.Contains("stray", StringComparison.Ordinal));
        Assert.Single(_logged, line => line.Contains("broken", StringComparison.Ordinal));
    }

    /// <summary>A move that fails for the files it is told to, as a locked file does.</summary>
    private static Action<string, string> FailingMove(Func<string, bool> isLocked) => (from, to) =>
    {
        if (isLocked(from)) throw new IOException("locked: " + from);

        File.Move(from, to);
    };

    [Fact]
    public void A_restore_that_cannot_finish_or_go_back_keeps_the_files_still_in_the_trash()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var cfg = Write(Config, "com.ann.map.cfg", "[General]\nZoom = 2\n");
        var item = NewSession().Put("com.ann.map", "Map", "1.2.0", dll, cfg);
        var stuck = new TrashCan(Root, new[] { Plugins, Off, Config }, _logged.Add, FailingMove(from => from.EndsWith("com.ann.map.cfg", StringComparison.Ordinal) || from == Path.Combine(Plugins, "Map.dll")));

        Assert.ThrowsAny<IOException>(() => stuck.Restore(item));
        Assert.Equal("[General]\nZoom = 2\n", File.ReadAllText(stuck.StoredPath(item, item.Files[1])));
        Assert.Equal("map dll", File.ReadAllText(dll));
        Assert.Single(stuck.Items());
        Assert.Contains(_logged, line => line.Contains("could not move") && line.Contains("Map.dll"));

        var next = NewSession();

        next.Restore(Assert.Single(next.Items()));

        Assert.Equal("[General]\nZoom = 2\n", File.ReadAllText(cfg));
        Assert.Equal("map dll", File.ReadAllText(dll));
        Assert.Empty(next.Items());
    }

    [Fact]
    public void An_uninstall_that_cannot_finish_or_go_back_stays_listed_and_can_be_restored()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var cfg = Write(Config, "com.ann.map.cfg", "[General]\n");
        var stuck = new TrashCan(Root, new[] { Plugins, Off, Config }, _logged.Add, FailingMove(from => from.EndsWith("com.ann.map.cfg", StringComparison.Ordinal) || from.StartsWith(Root, StringComparison.Ordinal)));

        Assert.ThrowsAny<IOException>(() => stuck.Put("com.ann.map", "Map", "1.2.0", dll, cfg));
        Assert.False(File.Exists(dll));
        Assert.True(File.Exists(cfg));

        var next = NewSession();
        var item = Assert.Single(next.Items());

        Assert.Equal("map dll", File.ReadAllText(next.StoredPath(item, item.Dll)));

        next.Restore(item);

        Assert.Equal("map dll", File.ReadAllText(dll));
        Assert.Equal("[General]\n", File.ReadAllText(cfg));
        Assert.Empty(next.Items());
    }

    [Fact]
    public void Emptying_the_trash_after_an_uninstall_stopped_halfway_keeps_the_dll()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var cfg = Write(Config, "com.ann.map.cfg", "[General]\n");
        var stuck = new TrashCan(Root, new[] { Plugins, Off, Config }, _logged.Add, FailingMove(from => from.EndsWith("com.ann.map.cfg", StringComparison.Ordinal) || from.StartsWith(Root, StringComparison.Ordinal)));

        var partial = Assert.Throws<TrashPartial>(() => stuck.Put("com.ann.map", "Map", "1.2.0", dll, cfg));
        var item = Assert.IsType<TrashItem>(partial.Item);
        var (deleted, kept, _) = stuck.Empty();

        Assert.Equal(0, deleted);
        Assert.Equal(1, kept);
        Assert.Equal("map dll", File.ReadAllText(stuck.StoredPath(item, item.Dll)));
    }

    [Fact]
    public void Emptying_the_trash_after_a_restore_stopped_halfway_keeps_the_files_still_in_it()
    {
        var dll = Write(Plugins, "Map.dll", "map dll");
        var cfg = Write(Config, "com.ann.map.cfg", "[General]\nZoom = 2\n");
        var item = NewSession().Put("com.ann.map", "Map", "1.2.0", dll, cfg);
        var stuck = new TrashCan(Root, new[] { Plugins, Off, Config }, _logged.Add, FailingMove(from => from.EndsWith("com.ann.map.cfg", StringComparison.Ordinal) || from == Path.Combine(Plugins, "Map.dll")));

        Assert.Throws<TrashPartial>(() => stuck.Restore(item));

        stuck.Empty();

        Assert.Equal("[General]\nZoom = 2\n", File.ReadAllText(stuck.StoredPath(item, item.Files[1])));
        Assert.Equal("map dll", File.ReadAllText(dll));
    }

    [Fact]
    public void A_move_that_stopped_halfway_never_says_nothing_changed()
    {
        var uninstalled = TrashNotes.UninstallPartial(Array.Empty<string>());
        var restored = TrashNotes.RestorePartial("locked");

        Assert.Contains("its DLL is in the trash", uninstalled.English);
        Assert.Contains("stayed in the config folder", uninstalled.English);
        Assert.Contains("will not start either: Radar.", TrashNotes.UninstallPartial(new[] { "Radar" }).English);
        Assert.Contains("still in the trash", restored.English);
        Assert.DoesNotContain("nothing changed", uninstalled.English + restored.English);
    }

    [Fact]
    public void A_saved_file_that_cannot_be_read_is_told_apart_from_one_that_is_not_there()
    {
        var path = Write(_game, "profiles.txt", "SLMM1-saved\n");
        var saved = new SavedFile(path, "profiles", _logged.Add);
        var missing = new SavedFile(Path.Combine(_game, "none.txt"), "profiles", _logged.Add);

        Assert.True(missing.TryRead(out var nothing));
        Assert.Null(nothing);

        using var locked = OperatingSystem.IsWindows() ? new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;

        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.None);

        try
        {
            Assert.False(saved.TryRead(out var text));
            Assert.Null(text);
        }
        finally
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void Two_uninstalls_of_one_mod_in_the_same_second_get_their_own_folders()
    {
        var trash = NewSession();
        var first = trash.Put("com.ann.map", "Map", "1.2.0", Write(Plugins, "Map.dll", "one"), null);
        var second = trash.Put("com.ann.map", "Map", "1.2.0", Write(Plugins, "Map.dll", "two"), null);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("one", File.ReadAllText(trash.StoredPath(first, first.Dll)));
        Assert.Equal("two", File.ReadAllText(trash.StoredPath(second, second.Dll)));
    }

    [Fact]
    public void The_record_reads_back_as_it_was_written()
    {
        var item = new TrashItem("20261108-101500-com.ann.map", "com.ann.map", "Map", "1.2.0", new DateTime(2026, 11, 8, 10, 15, 0, DateTimeKind.Utc), new[]
        {
            new TrashFile(Path.Combine(Plugins, "Map.dll"), "Map.dll", false),
            new TrashFile(Path.Combine(Config, "com.ann.map.cfg"), "com.ann.map.cfg", true),
        });

        var read = TrashItem.FromJson(item.Id, item.ToJson());

        Assert.NotNull(read);
        Assert.Equal(item.Guid, read!.Guid);
        Assert.Equal(item.TrashedUtc, read.TrashedUtc);
        Assert.Equal(item.Files, read.Files);
    }

    [Theory]
    [InlineData("""{"schema":1,"guid":"g","files":[{"from":"/game/plugins/Map.dll","stored":"../Map.dll"}]}""")]
    [InlineData("""{"schema":1,"guid":"g","files":[{"from":"plugins/Map.dll","stored":"Map.dll"}]}""")]
    [InlineData("""{"schema":1,"guid":"g","files":[{"from":"/game/config/g.cfg","stored":"g.cfg","settings":true}]}""")]
    [InlineData("""{"schema":1,"guid":"g","files":[{"from":"/game/plugins/A.dll","stored":"A.dll"},{"from":"/game/plugins/B.dll","stored":"B.dll"}]}""")]
    [InlineData("""{"schema":1,"guid":"g","files":[{"from":"/game/plugins/item.json","stored":"item.json"}]}""")]
    [InlineData("""{"schema":2,"guid":"g","files":[{"from":"/game/plugins/Map.dll","stored":"Map.dll"}]}""")]
    public void A_record_that_could_put_a_file_somewhere_else_is_not_trusted(string json)
    {
        Assert.Null(TrashItem.FromJson("id", json));
    }

    [Fact]
    public void The_uninstall_prep_setting_is_found_in_a_cfg_as_BepInEx_writes_it()
    {
        Assert.True(UninstallPrep.IsInConfig("[General]\nZoom = 2\n\n[Uninstall]\n\n## Turn this on, save, then remove the mod.\n# Setting type: Boolean\nUninstallPrep = false\n"));
        Assert.False(UninstallPrep.IsInConfig("[General]\nUninstallPrep = false\n"));
        Assert.False(UninstallPrep.IsInConfig("[Uninstall]\n# UninstallPrep = false\n"));
    }

    [Fact]
    public void The_uninstall_note_says_what_happened_to_the_settings_file()
    {
        var none = Array.Empty<string>();

        Assert.Contains("and so is its settings file", TrashNotes.Uninstalled(true, true, none).English);
        Assert.Contains("Its settings file is kept", TrashNotes.Uninstalled(true, false, none).English);
        Assert.DoesNotContain("settings file", TrashNotes.Uninstalled(false, false, none).English);
        Assert.Contains("will not start either: Radar.", TrashNotes.Uninstalled(false, false, new[] { "Radar" }).English);
    }

    [Fact]
    public void Emptying_says_what_was_kept_and_what_could_not_go()
    {
        var text = TrashNotes.Emptied(2, 1, new[] { "Radar" }).English;

        Assert.Equal("Deleted 2 mods for good. 1 mod uninstalled this session stays until the next start, so you can still restore it. Could not delete: Radar.", text);
    }

    [Theory]
    [InlineData("slmod|modmanager|uninstall|com.x|0", false)]
    [InlineData("slmod|modmanager|uninstall|com.x|1", true)]
    public void Uninstall_says_whether_the_settings_file_goes_too(string message, bool withSettings)
    {
        Assert.Equal(new PageCommand.Uninstall("com.x", withSettings), PageCommand.Parse(message));
    }

    [Fact]
    public void Trash_commands_are_read_and_a_garbled_uninstall_is_not()
    {
        Assert.Equal(new PageCommand.Restore("20261108-101500-com.x"), PageCommand.Parse("slmod|modmanager|restore|20261108-101500-com.x"));
        Assert.Equal(new PageCommand.EmptyTrash(), PageCommand.Parse("slmod|modmanager|emptyTrash"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|modmanager|uninstall|com.x|yes"));
    }

    [Fact]
    public void An_uninstalled_mod_is_off_from_the_next_start_and_the_report_says_so()
    {
        var mod = Loaded("com.ann.map") with { Switch = new ModSwitch.Uninstalled("t1") };

        Assert.False(mod.IsOnNextStart);
        Assert.Contains("uninstalled from the next start", ModReport.Text(new ModsView(new[] { mod }, Array.Empty<string>(), false, "F10")));
    }

    // ===== The page =====

    private static ModEntry Loaded(string guid, params Setting[] settings) =>
        new(guid, guid, "1.0", guid + ".dll", new ModState.Loaded(settings), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());

    private static Setting Prep(string guid, bool isOn) =>
        new(new SettingId(guid, UninstallPrep.Section, UninstallPrep.Key), "Uninstall Prep", new Description("Prepares the save.", "准备存档。"), new SettingKind.Toggle(), "false", isOn ? "true" : "false", "false", false);

    private static Setting Zoom(string guid) =>
        new(new SettingId(guid, "General", "Zoom"), "Zoom", new Description("Zoom.", "缩放。"), new SettingKind.Toggle(), "false", "false", "false", false);

    private static Jint.Engine Page(ModsView view)
    {
        var engine = new Jint.Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(ItemFieldPageTests.StandIns);
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(view));

        return engine;
    }

    private static ModsView View(IReadOnlyList<ModEntry> mods, params TrashEntry[] trash) => new(mods, Array.Empty<string>(), false, "F10") { Trash = trash };

    private static string Posted(Jint.Engine engine, string verb) =>
        engine.Evaluate("posted.filter(function (text) { return text.indexOf('|" + verb + "|') >= 0 || text.endsWith('|" + verb + "'); }).join('\\n')").AsString();

    private static string DrawnAfter(Jint.Engine engine, string script)
    {
        engine.Execute("drawn.length = 0");
        engine.Execute(script);

        return KnownFactsTests.Drawn(engine);
    }

    [Fact]
    public void Uninstall_asks_first_and_then_sends_the_mod_keeping_its_settings_file()
    {
        var engine = Page(View(new[] { Loaded("g", Zoom("g")) }));

        Assert.Contains("data-act=\"uninstall\"", KnownFactsTests.Drawn(engine));

        var asked = DrawnAfter(engine, "fire('click', { target: act('uninstall') })");

        Assert.Contains("<b>Uninstall g?</b>", asked);
        Assert.Contains("data-uncfg=\"1\"", asked);
        Assert.Equal("", Posted(engine, "uninstall"));

        engine.Execute("fire('click', { target: act('uninstallYes') })");

        Assert.EndsWith("|uninstall|g|0", Posted(engine, "uninstall"));
    }

    [Fact]
    public void Ticking_the_box_sends_the_settings_file_too()
    {
        var engine = Page(View(new[] { Loaded("g", Zoom("g")) }));

        engine.Execute("fire('click', { target: act('uninstall') })");
        engine.Execute("var box = el('INPUT', [], { uncfg: '1' }); box.checked = true; fire('change', { target: box })");
        engine.Execute("fire('click', { target: act('uninstallYes') })");

        Assert.EndsWith("|uninstall|g|1", Posted(engine, "uninstall"));
    }

    [Fact]
    public void Cancel_sends_nothing_and_puts_the_button_back()
    {
        var engine = Page(View(new[] { Loaded("g", Zoom("g")) }));

        engine.Execute("fire('click', { target: act('uninstall') })");

        var after = DrawnAfter(engine, "fire('click', { target: act('uninstallNo') })");

        Assert.DoesNotContain("Uninstall g?", after);
        Assert.Contains("data-act=\"uninstall\"", after);
        Assert.Equal("", Posted(engine, "uninstall"));
    }

    [Fact]
    public void A_mod_with_uninstall_prep_off_offers_to_turn_it_on_before_uninstalling()
    {
        var engine = Page(View(new[] { Loaded("g", Prep("g", false)) with { OffersUninstallPrep = true } }));

        var asked = DrawnAfter(engine, "fire('click', { target: act('uninstall') })");

        Assert.Contains("turn on its Uninstall Prep setting, save your game, then uninstall.", asked);

        engine.Execute("fire('click', { target: act('prepOn') })");

        Assert.EndsWith("|set|g|Uninstall|UninstallPrep|true", Posted(engine, "set"));
    }

    [Fact]
    public void With_uninstall_prep_on_the_question_says_to_save_first()
    {
        var engine = Page(View(new[] { Loaded("g", Prep("g", true)) with { OffersUninstallPrep = true } }));

        var asked = DrawnAfter(engine, "fire('click', { target: act('uninstall') })");

        Assert.Contains("Uninstall Prep is on. Save your game before you quit", asked);
        Assert.DoesNotContain("data-act=\"prepOn\"", asked);
    }

    [Fact]
    public void A_turned_off_mod_that_has_uninstall_prep_says_it_only_works_while_the_mod_runs()
    {
        var off = Loaded("g") with { State = new ModState.Off(), Switch = new ModSwitch.Allowed(false, Array.Empty<string>()), OffersUninstallPrep = true };
        var engine = Page(View(new[] { off }));

        var asked = DrawnAfter(engine, "fire('click', { target: act('uninstall') })");

        Assert.Contains("but only while it runs", asked);
    }

    [Fact]
    public void An_uninstalled_mod_is_marked_offers_restore_and_no_settings_or_second_uninstall()
    {
        var mod = Loaded("g", Zoom("g")) with { Switch = new ModSwitch.Uninstalled("t1") };
        var engine = Page(View(new[] { mod }, new TrashEntry("t1", "g", "1.0", "2026-11-08 10:15", false, true)));
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains(">UNINSTALLED</span>", drawn);
        Assert.Contains("data-act=\"restore\" data-trash=\"t1\"", drawn);
        Assert.Contains("<b>Uninstalled.</b>", drawn);
        Assert.DoesNotContain("data-act=\"uninstall\"", drawn);
        Assert.DoesNotContain("data-act=\"showCfg\"", drawn);

        engine.Execute("fire('click', { target: el('DIV', ['key-btn'], { act: 'restore', trash: 't1' }, { '[data-act]': { dataset: { act: 'restore', trash: 't1' } } }) })");

        Assert.EndsWith("|restore|t1", Posted(engine, "restore"));
    }

    [Fact]
    public void The_trash_page_lists_each_mod_and_empties_only_earlier_sessions_after_asking()
    {
        var engine = Page(View(new[] { Loaded("g") },
            new TrashEntry("t2", "Radar", "1.3", "2026-11-08 10:15", false, true),
            new TrashEntry("t1", "Map", "1.2.0", "2026-11-01 09:00", true, false)));

        var page = DrawnAfter(engine, "fire('click', { target: el('DIV', ['hd-title'], { page: 'trash' }, { '[data-page]': { dataset: { page: 'trash' } } }) })");

        Assert.Contains("Uninstalled mods &middot; 2", page);
        Assert.Contains("<div class=\"mname\">Radar</div><div class=\"msub\">v1.3 &middot; 2026-11-08 10:15 &middot; this session</div>", page);
        Assert.Contains("<div class=\"mname\">Map</div><div class=\"msub\">v1.2.0 &middot; 2026-11-01 09:00 &middot; with its settings file</div>", page);

        var asked = DrawnAfter(engine, "fire('click', { target: act('emptyTrash') })");

        Assert.Contains("Delete 1 mod for good?", asked);
        Assert.Equal("", Posted(engine, "emptyTrash"));

        engine.Execute("fire('click', { target: act('emptyYes') })");

        Assert.EndsWith("|emptyTrash", Posted(engine, "emptyTrash"));
    }

    [Fact]
    public void Only_mods_uninstalled_this_session_leave_nothing_to_empty()
    {
        var engine = Page(View(new[] { Loaded("g") }, new TrashEntry("t2", "Radar", "1.3", "", false, true)));

        var page = DrawnAfter(engine, "fire('click', { target: el('DIV', ['hd-title'], { page: 'trash' }, { '[data-page]': { dataset: { page: 'trash' } } }) })");

        Assert.Contains("Radar", page);
        Assert.DoesNotContain("data-act=\"emptyTrash\"", page);
    }

    [Fact]
    public void What_emptying_did_stays_on_the_trash_page_after_the_trash_is_empty()
    {
        var mods = new[] { Loaded("g") };
        var engine = Page(View(mods, new TrashEntry("t1", "Map", "1.2.0", "", false, false)));

        engine.Execute("fire('click', { target: el('DIV', ['hd-title'], { page: 'trash' }, { '[data-page]': { dataset: { page: 'trash' } } }) })");
        engine.Execute("fire('click', { target: act('emptyTrash') })");
        engine.Execute("fire('click', { target: act('emptyYes') })");

        var after = DrawnAfter(engine, PageScript.Replace(View(mods), new ModNote("", false, TrashNotes.Emptied(1, 0, Array.Empty<string>()))));

        Assert.Contains("Deleted 1 mod for good.", after);
        Assert.Contains("The trash is empty.", after);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(TrashTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
