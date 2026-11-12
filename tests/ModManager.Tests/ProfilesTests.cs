using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A profile is a setup the player can go back to or share: which mods are on, and the settings they changed. It
/// travels as a short code; pasting one shows what it would change before anything changes, so a code from a stranger
/// can never switch mods or settings on its own, and a broken or huge code is refused instead of read.
/// </summary>
public sealed class ProfilesTests
{
    private static Setting Setting(string guid, string key, string @default, string current, bool isReadOnly = false) =>
        new(new SettingId(guid, "General", key), key, new Description(key + ".", key + "。"), new SettingKind.Toggle(), @default, current, current, isReadOnly);

    private static ModEntry Loaded(string guid, string version, bool isOnNext, params Setting[] settings) =>
        new(guid, "Mod " + guid, version, guid + ".dll", new ModState.Loaded(settings), ModInfo.None, new ModSwitch.Allowed(isOnNext, Array.Empty<string>()), new UpdateState.NotChecked());

    private static ModEntry Off(string guid, bool isOnNext = false) =>
        new(guid, "Mod " + guid, "1.0", guid + ".dll", new ModState.Off(), ModInfo.None, new ModSwitch.Allowed(isOnNext, Array.Empty<string>()), new UpdateState.NotChecked());

    private static IReadOnlyList<ProfileStep> Plan(Profile profile, IReadOnlyList<ModEntry> mods, bool canInstall = false) =>
        ProfilePlan.For(profile, mods, _ => canInstall, _ => 4242);

    private static string Pack(string text)
    {
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);

            deflate.Write(bytes, 0, bytes.Length);
        }

        return ShareCode.Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// How Mod Manager 0.20.8 and earlier wrote a code, copied from ShareCode.Write before the packing moved to the
    /// Toolkit. Every code players have already shared was made this way, so it must keep reading, and a new code must
    /// come out the same.
    /// </summary>
    private static string OldPack(Profile profile)
    {
        static string Field(string value) => value.Replace("%", "%25").Replace("\t", "%09").Replace("\n", "%0A").Replace("\r", "%0D");

        var text = new StringBuilder();

        text.Append("n\t").Append(Field(profile.Name)).Append('\n');

        foreach (var mod in profile.Mods) text.Append("m\t").Append(Field(mod.Guid)).Append('\t').Append(Field(mod.Name)).Append('\t').Append(Field(mod.Version)).Append('\t').Append(mod.IsOn ? '1' : '0').Append('\t').Append(mod.SettingsKnown ? '1' : '0').Append('\n');

        foreach (var setting in profile.Settings) text.Append("s\t").Append(Field(setting.Guid)).Append('\t').Append(Field(setting.Section)).Append('\t').Append(Field(setting.Key)).Append('\t').Append(Field(setting.Value)).Append('\n');

        text.Append("e\t").Append(profile.Mods.Count + profile.Settings.Count).Append('\n');

        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
        {
            var bytes = Encoding.UTF8.GetBytes(text.ToString());

            deflate.Write(bytes, 0, bytes.Length);
        }

        return ShareCode.Prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // ===== Saving the setup =====

    [Fact]
    public void A_saved_profile_has_every_mod_as_it_will_be_next_start_and_only_the_changed_settings()
    {
        var mods = new[]
        {
            Loaded("a", "1.2", true, Setting("a", "Zoom", "false", "true"), Setting("a", "Speed", "1", "1"), Setting("a", "Fixed", "x", "y", isReadOnly: true)),
            Loaded("b", "2.0", false),
            Off("c", isOnNext: true),
            Loaded("d", "1.0", true) with { Switch = new ModSwitch.Uninstalled("t1") },
        };

        var profile = Profile.Of("  Night\nrun  ", mods);

        Assert.Equal("Night run", profile.Name);
        Assert.True(profile.CarriesSettings);
        Assert.Equal(new[] { "a:1", "b:0", "c:1" }, profile.Mods.Select(mod => mod.Guid + ":" + (mod.IsOn ? 1 : 0)));
        Assert.Equal(new[] { true, true, false }, profile.Mods.Select(mod => mod.SettingsKnown));
        Assert.Equal(new[] { new ProfileSetting("a", "General", "Zoom", "true") }, profile.Settings);
    }

    [Fact]
    public void A_blank_name_becomes_Profile_and_a_long_one_is_cut()
    {
        Assert.Equal("Profile", Profile.CleanName(" \n\t "));
        Assert.Equal(Profile.MaxNameLength, Profile.CleanName(new string('x', 200)).Length);
    }

    // ===== The share code =====

    [Fact]
    public void A_code_reads_back_as_the_same_profile_even_with_tabs_lines_and_percents_in_it()
    {
        var profile = new Profile("50% \"loot\"", new[] { new ProfileMod("com.x", "Name\twith tab", "1.0", true), new ProfileMod("com.y", "Y", "", false, false) },
            new[] { new ProfileSetting("com.x", "Shop", "Stock", "20004:5\n%0A,1") }, true);

        var code = ShareCode.Write(profile);
        var (read, problem) = ShareCode.Read(code);

        Assert.StartsWith("SLMM1-", code);
        Assert.Matches("^[A-Za-z0-9_-]+$", code.Substring(ShareCode.Prefix.Length));
        Assert.Null(problem);
        Assert.Equal(profile.Name, read!.Name);
        Assert.Equal(profile.Mods, read.Mods);
        Assert.Equal(profile.Settings, read.Settings);
        Assert.True(read.CarriesSettings);
    }

    [Fact]
    public void A_code_broken_across_lines_by_a_chat_app_with_words_around_it_still_reads()
    {
        var code = ShareCode.Write(new Profile("Mine", new[] { new ProfileMod("com.x", "X", "1.0", true) }, Array.Empty<ProfileSetting>(), true));
        var chat = "here is my setup: " + code.Substring(0, 10) + "\n  " + code.Substring(10) + " have fun";

        Assert.Equal("Mine", ShareCode.Read(chat).Profile!.Name);
    }

    [Theory]
    [InlineData("", "Empty")]
    [InlineData("hello there", "NotACode")]
    [InlineData("SLMM1-AAAA", "Broken")]
    [InlineData("SLMM1-%%%", "NotACode")]
    public void A_text_that_is_not_a_whole_code_says_why(string pasted, string problem)
    {
        var expected = problem switch
        {
            "Empty" => ShareCode.Problems.Empty,
            "NotACode" => ShareCode.Problems.NotACode,
            _ => ShareCode.Problems.Broken,
        };

        var (profile, said) = ShareCode.Read(pasted);

        Assert.Null(profile);
        Assert.Equal(expected, said);
    }

    [Fact]
    public void A_code_cut_short_is_broken_not_read_in_part()
    {
        var code = ShareCode.Write(new Profile("Mine", Enumerable.Range(0, 40).Select(i => new ProfileMod("com.mod" + i, "Mod " + i, "1.0", true)).ToList(), Array.Empty<ProfileSetting>(), true));

        Assert.Equal(ShareCode.Problems.Broken, ShareCode.Read(code.Substring(0, code.Length / 2)).Problem);
    }

    [Fact]
    public void A_code_that_unpacks_huge_is_refused_before_it_fills_the_memory()
    {
        // A few kilobytes of code that would unpack to megabytes of one repeated letter.
        var bomb = Pack("n\tx\n" + new string('a', ShareCode.MaxUnpackedBytes * 3));

        Assert.True(bomb.Length < ShareCode.MaxCodeLength);
        Assert.Equal(ShareCode.Problems.TooBig, ShareCode.Read(bomb).Problem);
        Assert.Equal(ShareCode.Problems.TooBig, ShareCode.Read(new string('x', ShareCode.MaxCodeLength + 1)).Problem);
    }

    [Fact]
    public void A_code_with_more_mods_than_any_game_has_is_refused()
    {
        var lines = "n\tx\n" + string.Concat(Enumerable.Range(0, ShareCode.MaxMods + 1).Select(i => "m\tg" + i + "\tN\t1\t1\t1\n"));

        Assert.Equal(ShareCode.Problems.TooBig, ShareCode.Read(Pack(lines)).Problem);
    }

    [Fact]
    public void A_mod_named_twice_in_a_code_keeps_its_first_line()
    {
        var (profile, _) = ShareCode.Read(Pack("n\tx\nm\tg\tG\t1\t1\t1\nm\tg\tG\t1\t0\t1\ns\tg\tA\tB\tone\ns\tg\tA\tB\ttwo\ne\t4\n"));

        Assert.True(Assert.Single(profile!.Mods).IsOn);
        Assert.Equal("one", Assert.Single(profile.Settings).Value);
    }

    // ===== Codes stay the same now that the Toolkit packs them =====

    [Fact]
    public void A_new_code_is_the_same_text_as_before_even_with_escaped_fields_and_Chinese()
    {
        var profile = new Profile("我的 50%\tsetup", new[] { new ProfileMod("com.x", "物品\nName", "1.0\r", true), new ProfileMod("com.y", "Y", "", false, false) },
            new[] { new ProfileSetting("com.x", "商店", "Stock\t2", "20004:5\n%0A,1 一百") }, true);

        Assert.Equal(OldPack(profile), ShareCode.Write(profile));
    }

    [Fact]
    public void A_new_code_for_an_empty_profile_is_the_same_text_as_before()
    {
        var profile = new Profile("P", Array.Empty<ProfileMod>(), Array.Empty<ProfileSetting>(), true);

        Assert.Equal(OldPack(profile), ShareCode.Write(profile));
    }

    [Fact]
    public void A_long_code_shared_before_still_reads_as_the_same_profile()
    {
        var random = new Random(7);
        string Word() => string.Concat(Enumerable.Range(0, 12).Select(_ => (char)random.Next('a', 'z' + 1)));
        var mods = Enumerable.Range(0, 300).Select(i => new ProfileMod("com." + Word(), Word() + " 模组", "1." + i, i % 3 != 0, i % 5 != 0)).ToList();
        var settings = mods.Take(100).Select(mod => new ProfileSetting(mod.Guid, "General", Word(), Word() + "%\t" + Word())).ToList();
        var profile = new Profile("Old code", mods, settings, true);

        var code = OldPack(profile);
        var (read, problem) = ShareCode.Read(code);

        Assert.Contains("-", code.Substring(ShareCode.Prefix.Length));
        Assert.Contains("_", code.Substring(ShareCode.Prefix.Length));
        Assert.Null(problem);
        Assert.Equal(profile.Name, read!.Name);
        Assert.Equal(profile.Mods, read.Mods);
        Assert.Equal(profile.Settings, read.Settings);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("spaces")]
    [InlineData("not a code")]
    [InlineData("bad base64")]
    [InlineData("bad deflate")]
    [InlineData("too long")]
    [InlineData("unpacks too big")]
    public void Each_problem_with_a_pasted_code_still_says_the_same_words(string pasted)
    {
        var (text, english) = pasted switch
        {
            "empty" => ("", "Paste a profile code or a mod list first."),
            "spaces" => (" \n\t ", "Paste a profile code or a mod list first."),
            "not a code" => ("my mods are great", "This is not a profile code or a list from COPY MOD LIST."),
            "bad base64" => ("SLMM1-A", "The code is cut off or changed. Copy it again from where it was shared."),
            "bad deflate" => ("SLMM1-AAAA", "The code is cut off or changed. Copy it again from where it was shared."),
            "too long" => (new string('x', ShareCode.MaxCodeLength + 1), "This is far bigger than any profile, so it was not read."),
            _ => (Pack(new string('a', ShareCode.MaxUnpackedBytes + 1)), "This is far bigger than any profile, so it was not read."),
        };

        var (profile, problem) = ShareCode.Read(text);

        Assert.Null(profile);
        Assert.Equal(english, problem!.English);
    }

    [Fact]
    public void A_code_that_unpacks_to_exactly_the_limit_still_reads_and_one_byte_more_does_not()
    {
        const string head = "n\tx\n", tail = "e\t0\n";
        var filler = new string('a', ShareCode.MaxUnpackedBytes - head.Length - tail.Length - 1) + "\n";

        Assert.Equal("x", ShareCode.Read(Pack(head + filler + tail)).Profile!.Name);
        Assert.Equal(ShareCode.Problems.TooBig, ShareCode.Read(Pack(head + "a" + filler + tail)).Problem);
    }

    // ===== A pasted mod list =====

    [Fact]
    public void A_list_from_COPY_MOD_LIST_reads_as_a_profile_of_on_and_off_without_settings()
    {
        var mods = new[]
        {
            Loaded("com.a", "1.2", true, Setting("com.a", "Zoom", "false", "true")),
            Loaded("com.b", "2.0", false),
            Off("com.c", isOnNext: true),
            Off("com.d"),
            Loaded("com.e", "1.0", true) with { Switch = new ModSwitch.Uninstalled("t1") },
        };
        var report = "Copied from the game:\n" + ModReport.Text(new ModsView(mods, Array.Empty<string>(), false, "F10") { GameVersion = "1.1.20719" });

        var (profile, problem) = ShareCode.Read(report);

        Assert.Null(problem);
        Assert.Equal("Mod list (Survival Log 1.1.20719)", profile!.Name);
        Assert.False(profile.CarriesSettings);
        Assert.Empty(profile.Settings);
        Assert.Equal(new[] { "com.a:1:1.2", "com.b:0:2.0", "com.c:1:1.0", "com.d:0:1.0" }, profile.Mods.Select(mod => mod.Guid + ":" + (mod.IsOn ? 1 : 0) + ":" + mod.Version));
        Assert.Equal("Mod com.a", profile.Mods[0].Name);
    }

    [Fact]
    public void A_list_whose_game_version_is_unknown_is_just_a_mod_list()
    {
        var report = ModReport.Text(new ModsView(new[] { Loaded("com.a", "1.0", true) }, Array.Empty<string>(), false, "F10"));

        Assert.Equal("Mod list", ShareCode.Read(report).Profile!.Name);
    }

    // ===== What using it changes =====

    [Fact]
    public void Mods_it_names_turn_on_or_off_and_mods_it_does_not_name_are_left_alone()
    {
        var profile = new Profile("p", new[] { new ProfileMod("a", "A", "1.0", false), new ProfileMod("b", "B", "1.0", true), new ProfileMod("c", "C", "1.0", true) }, Array.Empty<ProfileSetting>(), false);
        var mods = new[] { Loaded("a", "1.0", true), Off("b"), Loaded("c", "1.0", true), Loaded("z", "1.0", true) };

        var steps = Plan(profile, mods);

        Assert.Equal(new ProfileStep[] { new ProfileStep.TurnOff("a", "Mod a"), new ProfileStep.TurnOn("b", "Mod b") }, steps);
    }

    [Fact]
    public void A_mod_that_cannot_be_switched_off_here_says_so_instead_of_trying()
    {
        var profile = new Profile("p", new[] { new ProfileMod("a", "A", "1.0", false) }, Array.Empty<ProfileSetting>(), false);
        var locked = Loaded("a", "1.0", true) with { Switch = new ModSwitch.Locked(false, new[] { "b" }) };

        Assert.Equal(new ProfileStep[] { new ProfileStep.StaysOn("a", "Mod a") }, Plan(profile, new[] { locked }));
    }

    [Fact]
    public void A_mod_it_needs_that_is_not_installed_comes_from_BROWSE_when_it_can_and_points_to_Nexus_when_not()
    {
        var profile = new Profile("p", new[] { new ProfileMod("new", "New One", "1.0", true), new ProfileMod("gone", "Gone", "1.0", false) }, Array.Empty<ProfileSetting>(), false);

        Assert.Equal(new ProfileStep[] { new ProfileStep.Install("new", "New One") }, Plan(profile, Array.Empty<ModEntry>(), canInstall: true));
        Assert.Equal(new ProfileStep[] { new ProfileStep.Missing("new", "New One", 4242) }, Plan(profile, Array.Empty<ModEntry>()));
    }

    [Fact]
    public void An_uninstalled_mod_counts_as_not_installed()
    {
        var profile = new Profile("p", new[] { new ProfileMod("a", "A", "1.0", true) }, Array.Empty<ProfileSetting>(), false);
        var uninstalled = Loaded("a", "1.0", true) with { Switch = new ModSwitch.Uninstalled("t1") };

        Assert.IsType<ProfileStep.Missing>(Assert.Single(Plan(profile, new[] { uninstalled })));
    }

    [Fact]
    public void Another_installed_version_is_said_but_not_changed()
    {
        var profile = new Profile("p", new[] { new ProfileMod("a", "A", "1.4", true) }, Array.Empty<ProfileSetting>(), false);

        Assert.Equal(new ProfileStep[] { new ProfileStep.OtherVersion("a", "Mod a", "1.2", "1.4") }, Plan(profile, new[] { Loaded("a", "1.2", true) }));
    }

    [Fact]
    public void A_running_mod_gets_exactly_the_profiles_settings_so_its_other_changes_go_back_to_default()
    {
        var profile = new Profile("p", new[] { new ProfileMod("a", "A", "1.0", true) }, new[] { new ProfileSetting("a", "General", "Zoom", "true") }, true);
        var mod = Loaded("a", "1.0", true, Setting("a", "Zoom", "false", "false"), Setting("a", "Speed", "1", "3"), Setting("a", "Same", "x", "x"), Setting("a", "Fixed", "x", "y", isReadOnly: true));

        var steps = Plan(profile, new[] { mod });

        Assert.Equal(new ProfileStep[]
        {
            new ProfileStep.Set(new SettingId("a", "General", "Zoom"), "Mod a", "Zoom", "false", "true"),
            new ProfileStep.Set(new SettingId("a", "General", "Speed"), "Mod a", "Speed", "3", "1"),
        }, steps);
    }

    [Fact]
    public void Settings_of_a_mod_that_is_not_running_wait_and_a_mod_whose_settings_were_not_read_keeps_its_own()
    {
        var profile = new Profile("p", new[] { new ProfileMod("a", "A", "1.0", true), new ProfileMod("b", "B", "1.0", true, SettingsKnown: false) },
            new[] { new ProfileSetting("a", "General", "Zoom", "true"), new ProfileSetting("a", "General", "Speed", "2") }, true);
        var mods = new[] { Off("a", isOnNext: true), Loaded("b", "1.0", true, Setting("b", "Speed", "1", "5")) };

        Assert.Equal(new ProfileStep[] { new ProfileStep.SettingsWait("a", "Mod a", 2) }, Plan(profile, mods));
    }

    [Fact]
    public void A_pasted_mod_list_never_touches_settings()
    {
        var profile = new Profile("p", new[] { new ProfileMod("a", "A", "1.0", true) }, Array.Empty<ProfileSetting>(), false);

        Assert.Empty(Plan(profile, new[] { Loaded("a", "1.0", true, Setting("a", "Speed", "1", "5")) }));
    }

    [Fact]
    public void A_profile_saved_from_the_setup_changes_nothing_when_used_on_it()
    {
        var mods = new[] { Loaded("a", "1.0", true, Setting("a", "Zoom", "false", "true"), Setting("a", "Speed", "1", "1")), Loaded("b", "1.0", false), Off("c") };

        Assert.Empty(Plan(Profile.Of("p", mods), mods));
        Assert.Empty(Plan(ShareCode.Read(ShareCode.Write(Profile.Of("p", mods))).Profile!, mods));
    }

    // ===== The saved profiles =====

    [Fact]
    public void The_shelf_keeps_one_profile_per_name_in_name_order_and_skips_lines_that_do_not_read()
    {
        var one = new Profile("beta", new[] { new ProfileMod("a", "A", "1.0", true) }, Array.Empty<ProfileSetting>(), true);
        var two = new Profile("Alpha", new[] { new ProfileMod("b", "B", "1.0", true) }, Array.Empty<ProfileSetting>(), true);
        var again = new Profile("BETA", new[] { new ProfileMod("c", "C", "1.0", true) }, Array.Empty<ProfileSetting>(), true);

        var shelf = ProfileShelf.Put(ProfileShelf.Put(ProfileShelf.Put(Array.Empty<Profile>(), one), two), again);

        Assert.Equal(new[] { "Alpha", "BETA" }, shelf.Select(profile => profile.Name));

        var read = ProfileShelf.Read(ProfileShelf.Write(shelf) + "SLMM1-broken\nnot a code\n");

        Assert.Equal(new[] { "Alpha", "BETA" }, read.Select(profile => profile.Name));
        Assert.Equal("c", read[1].Mods[0].Guid);
        Assert.Equal(new[] { "BETA" }, ProfileShelf.Remove(read, "alpha").Select(profile => profile.Name));
        Assert.Empty(ProfileShelf.Read(null));
    }

    [Fact]
    public void Saving_the_shelf_writes_back_the_lines_that_did_not_read()
    {
        var one = new Profile("Alpha", new[] { new ProfileMod("b", "B", "1.0", true) }, Array.Empty<ProfileSetting>(), true);
        var saved = ProfileShelf.Write(new[] { one }) + "SLMM1-fromanewerversion\n\nnotes by hand\n";

        var unread = ProfileShelf.Unread(saved);
        var written = ProfileShelf.Write(ProfileShelf.Read(saved), unread);

        Assert.Equal(new[] { "SLMM1-fromanewerversion", "notes by hand" }, unread);
        Assert.Contains("SLMM1-fromanewerversion\n", written);
        Assert.Contains("notes by hand\n", written);
        Assert.Equal(new[] { "Alpha" }, ProfileShelf.Read(written).Select(profile => profile.Name));
        Assert.Empty(ProfileShelf.Unread(ProfileShelf.Write(new[] { one })));
    }

    [Fact]
    public void A_profile_whose_code_would_be_refused_is_not_saved()
    {
        var small = new Profile("Small", new[] { new ProfileMod("a", "A", "1.0", true) }, Array.Empty<ProfileSetting>(), true);
        var huge = new Profile("Huge", Enumerable.Range(0, ShareCode.MaxMods + 1).Select(n => new ProfileMod("mod" + n, "Mod " + n, "1.0", true)).ToList(), Array.Empty<ProfileSetting>(), true);

        Assert.True(ProfileShelf.FitsACode(small));
        Assert.False(ProfileShelf.FitsACode(huge));
    }

    [Fact]
    public void Using_a_profile_says_what_changed_and_what_could_not()
    {
        var note = ProfileNotes.Used("Night", 2, 1, 0, new[] { "Mod a: Zoom" });

        Assert.Equal("Used \"Night\": 2 mods turned on or off from the next start, 1 mod installing for the next start. Could not change: Mod a: Zoom.", note.English);
        Assert.Equal("Used \"Night\": nothing needed changing.", ProfileNotes.Used("Night", 0, 0, 0, Array.Empty<string>()).English);
    }

    // ===== The page's messages =====

    [Fact]
    public void Profile_commands_are_read_and_a_garbled_one_is_not()
    {
        Assert.Equal(new PageCommand.ProfileSave("Night run"), PageCommand.Parse("slmod|modmanager|profileSave|Night%20run"));
        Assert.Equal(new PageCommand.ProfileDelete("a|b"), PageCommand.Parse("slmod|modmanager|profileDelete|a%7Cb"));
        Assert.Equal(new PageCommand.ProfileCopy("x"), PageCommand.Parse("slmod|modmanager|profileCopy|x"));
        Assert.Equal(new PageCommand.ProfileShow("x"), PageCommand.Parse("slmod|modmanager|profileShow|x"));
        Assert.Equal(new PageCommand.ProfilePaste("SLMM1-x"), PageCommand.Parse("slmod|modmanager|profilePaste|SLMM1-x"));
        Assert.Equal(new PageCommand.ProfileUse(), PageCommand.Parse("slmod|modmanager|profileUse"));
        Assert.Equal(new PageCommand.ProfileKeep(), PageCommand.Parse("slmod|modmanager|profileKeep"));
        Assert.Equal(new PageCommand.ProfileClose(), PageCommand.Parse("slmod|modmanager|profileClose"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|modmanager|profileUse|now"));
    }

    // ===== The page =====

    private static readonly Profile Night = new("Night", new[] { new ProfileMod("a", "A", "1.0", true), new ProfileMod("b", "B", "1.0", false) }, new[] { new ProfileSetting("a", "General", "Zoom", "true") }, true);

    private static Jint.Engine Page(ProfilesView profiles)
    {
        var engine = new Jint.Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(ItemFieldPageTests.StandIns);
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(new ModsView(new[] { Loaded("a", "1.0", true) }, Array.Empty<string>(), false, "F10") { Profiles = profiles }));
        engine.Execute("function pf(name, profile) { return el('SPAN', [], { act: name, pf: profile }, { '[data-act]': { dataset: { act: name, pf: profile } } }); }");
        engine.Execute("drawn.length = 0");
        engine.Execute("fire('click', { target: el('DIV', [], { page: 'profiles' }, { '[data-page]': { dataset: { page: 'profiles' } } }) })");

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

    [Fact]
    public void The_page_lists_each_saved_profile_with_what_it_holds()
    {
        var drawn = KnownFactsTests.Drawn(Page(new ProfilesView(new[] { Night }, null, null, null)));

        Assert.Contains("<div class=\"mname\">Night</div>", drawn);
        Assert.Contains("2 mods, 1 on, 1 changed setting", drawn);
        Assert.Contains("data-act=\"pfCopy\" data-pf=\"Night\"", drawn);
        Assert.Contains("SAVE THIS SETUP", drawn);
    }

    [Fact]
    public void Delete_asks_first_and_cancel_sends_nothing()
    {
        var engine = Page(new ProfilesView(new[] { Night }, null, null, null));

        var asked = DrawnAfter(engine, "fire('click', { target: pf('pfDelete', 'Night') })");

        Assert.Contains("Delete the profile &quot;Night&quot;?", asked);
        Assert.Equal("", Posted(engine, "profileDelete"));

        engine.Execute("fire('click', { target: pf('pfDeleteNo') })");
        engine.Execute("fire('click', { target: pf('pfDeleteYes') })");

        Assert.Equal("", Posted(engine, "profileDelete"));

        engine.Execute("fire('click', { target: pf('pfDelete', 'Night') })");
        engine.Execute("fire('click', { target: pf('pfDeleteYes') })");

        Assert.EndsWith("|profileDelete|Night", Posted(engine, "profileDelete"));
    }

    [Fact]
    public void A_pasted_code_shows_its_changes_grouped_with_USE_and_SAVE_and_nothing_changes_until_USE()
    {
        var steps = new ProfileStep[]
        {
            new ProfileStep.TurnOff("b", "Mod b"),
            new ProfileStep.Missing("c", "Mod c", 4242),
            new ProfileStep.Set(new SettingId("a", "General", "Zoom"), "Mod a", "Zoom", "false", "true"),
            new ProfileStep.SettingsWait("d", "Mod d", 3),
        };
        var engine = Page(new ProfilesView(Array.Empty<Profile>(), new ProfileCheck(Night, true, steps), null, null));
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("What &quot;Night&quot; changes", drawn);
        Assert.Contains("Turn off from the next start &middot; 1", drawn);
        Assert.Contains("data-act=\"bwNeedNexus\" data-nexus=\"4242\"", drawn);
        Assert.Contains("Mod a: Zoom <span class=\"pf-val\">false</span>", drawn);
        Assert.Contains("Mod d: 3 settings. Use the profile again after the next start.", drawn);
        Assert.Contains("data-act=\"pfUse\"", drawn);
        Assert.Contains("data-act=\"pfKeep\"", drawn);
        Assert.Equal("", Posted(engine, "profileUse"));

        engine.Execute("fire('click', { target: pf('pfUse') })");

        Assert.EndsWith("|profileUse", Posted(engine, "profileUse"));
    }

    [Fact]
    public void A_profile_that_changes_nothing_offers_no_USE()
    {
        var drawn = KnownFactsTests.Drawn(Page(new ProfilesView(new[] { Night }, new ProfileCheck(Night, false, Array.Empty<ProfileStep>()), null, null)));

        Assert.Contains("Your setup already matches it.", drawn);
        Assert.DoesNotContain("data-act=\"pfUse\"", drawn);
        Assert.DoesNotContain("data-act=\"pfKeep\"", drawn);
    }

    [Fact]
    public void A_problem_with_the_paste_and_a_note_are_shown_in_the_players_language()
    {
        var engine = Page(new ProfilesView(Array.Empty<Profile>(), null, ShareCode.Problems.Broken, ProfileNotes.Deleted("Night")));
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("The code is cut off or changed.", drawn);
        Assert.Contains("Deleted the profile &quot;Night&quot;.", drawn);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(ProfilesTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
