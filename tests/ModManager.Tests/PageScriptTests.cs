using System.Linq;
using System.Text.Json;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class PageScriptTests
{
    private static Setting Setting(string key, SettingKind kind, string current = "1") =>
        new(new SettingId("g", "General", key), key, new Description("en", "zh"), kind, "1", current, "1", false);

    private static ModEntry Loaded(params Setting[] settings) => new("g", "Mod", "1.0", "Mod.dll", new ModState.Loaded(settings), ModInfo.None, Allowed, NotChecked);

    private static readonly ModSwitch Allowed = new ModSwitch.Allowed(true, System.Array.Empty<string>());

    private static readonly UpdateState NotChecked = new UpdateState.NotChecked();

    [Theory]
    [InlineData("quote \" and \\ slash")]
    [InlineData("line\nbreak\ttab\r")]
    [InlineData("</script><b>")]
    [InlineData("\u2028 and \u2029")]
    [InlineData("\u0001 control")]
    [InlineData("\u6a21\u7ec4")]
    public void Any_text_survives_the_trip_as_a_JSON_string(string text)
    {
        Assert.Equal(text, JsonSerializer.Deserialize<string>(PageScript.Str(text)));
    }

    [Fact]
    public void Text_cannot_close_a_script_tag_or_end_a_line_in_old_JavaScript()
    {
        var json = PageScript.Str("</script>\u2028\u2029");

        Assert.DoesNotContain("<", json);
        Assert.DoesNotContain('\u2028', json);
        Assert.DoesNotContain('\u2029', json);
    }

    [Fact]
    public void The_view_lists_each_mod_with_its_settings_and_kinds()
    {
        var mod = Loaded(Setting("On", new SettingKind.Toggle()), Setting("Speed", new SettingKind.Number(false, 0.5, 2)), Setting("Key", new SettingKind.Hotkey(new[] { "F7" }, true)));
        var view = new ModsView(new[] { mod, new ModEntry("h", "Broken", "2.0", "Broken.dll", new ModState.NotLoaded(), ModInfo.None, Allowed, NotChecked) }, new[] { "missing dependency" }, true, "F10");

        using var json = JsonDocument.Parse(PageScript.ViewJson(view));
        var root = json.RootElement;
        var settings = root.GetProperty("mods")[0].GetProperty("settings");

        Assert.True(root.GetProperty("zh").GetBoolean());
        Assert.Equal("F10", root.GetProperty("openKey").GetString());
        Assert.Equal("missing dependency", root.GetProperty("problems")[0].GetString());
        Assert.Equal("toggle", settings[0].GetProperty("kind").GetString());
        Assert.Equal(0.5, settings[1].GetProperty("min").GetDouble());
        Assert.Equal(2, settings[1].GetProperty("max").GetDouble());
        Assert.True(settings[2].GetProperty("modifiers").GetBoolean());
        Assert.False(root.GetProperty("mods")[1].GetProperty("loaded").GetBoolean());
        Assert.Equal(0, root.GetProperty("mods")[1].GetProperty("settings").GetArrayLength());
    }

    [Fact]
    public void A_choice_sends_its_Chinese_names_beside_its_options_and_null_where_there_is_none()
    {
        var named = Setting("Mode", new SettingKind.Choice(new[] { "Default", "Custom", "Hard limit" }), "Default") with { ChoiceZh = new System.Collections.Generic.Dictionary<string, string> { ["Default"] = "\u9ed8\u8ba4", ["Hard limit"] = "\u786c\u4e0a\u9650" } };
        var plain = Setting("Plain", new SettingKind.Choice(new[] { "A", "B" }), "A");

        using var json = JsonDocument.Parse(PageScript.ViewJson(new ModsView(new[] { Loaded(named, plain) }, System.Array.Empty<string>(), true, "F10")));
        var settings = json.RootElement.GetProperty("mods")[0].GetProperty("settings");
        var zh = settings[0].GetProperty("optionsZh");

        Assert.Equal("\u9ed8\u8ba4", zh[0].GetString());
        Assert.Equal(JsonValueKind.Null, zh[1].ValueKind);
        Assert.Equal("\u786c\u4e0a\u9650", zh[2].GetString());
        Assert.Equal("Default", settings[0].GetProperty("current").GetString());
        Assert.False(settings[1].TryGetProperty("optionsZh", out _));
    }

    [Fact]
    public void A_choice_sends_its_pictures_beside_its_options_matched_without_case_and_null_where_there_is_none()
    {
        var pictures = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["lamplight"] = "<b style=\"color:#f5b82e\">L</b>", ["Classic"] = "<i>C</i>", ["Neon"] = "<u>N</u>" };
        var pictured = Setting("Style", new SettingKind.Choice(new[] { "Lamplight", "FieldKit", "Classic" }), "Lamplight") with { ChoicePreview = pictures };
        var onlyUnknown = Setting("Other", new SettingKind.Choice(new[] { "A", "B" }), "A") with { ChoicePreview = new System.Collections.Generic.Dictionary<string, string> { ["Z"] = "<i>z</i>" } };

        using var json = JsonDocument.Parse(PageScript.ViewJson(new ModsView(new[] { Loaded(pictured, onlyUnknown) }, System.Array.Empty<string>(), false, "F10")));
        var settings = json.RootElement.GetProperty("mods")[0].GetProperty("settings");
        var sent = settings[0].GetProperty("optionsPreview");

        Assert.Equal(3, sent.GetArrayLength());
        Assert.Equal("<b style=\"color:#f5b82e\">L</b>", sent[0].GetString());
        Assert.Equal(JsonValueKind.Null, sent[1].ValueKind);
        Assert.Equal("<i>C</i>", sent[2].GetString());
        Assert.False(settings[1].TryGetProperty("optionsPreview", out _));
    }

    [Fact]
    public void A_choice_picture_cannot_close_the_script_it_travels_in()
    {
        var pictured = Setting("Style", new SettingKind.Choice(new[] { "A" }), "A") with { ChoicePreview = new System.Collections.Generic.Dictionary<string, string> { ["A"] = "</script><b>x</b>" } };

        var script = PageScript.Update(Loaded(pictured), null);

        Assert.DoesNotContain("</script>", script);
        Assert.DoesNotContain("<b>", script);
    }

    [Fact]
    public void A_number_without_a_usable_range_sends_no_ends_so_the_page_shows_a_box()
    {
        using var json = JsonDocument.Parse(PageScript.ModJson(Loaded(Setting("N", new SettingKind.Number(true, 5, 5)))));
        var setting = json.RootElement.GetProperty("settings")[0];

        Assert.Equal(JsonValueKind.Null, setting.GetProperty("min").ValueKind);
        Assert.Equal(JsonValueKind.Null, setting.GetProperty("max").ValueKind);
    }

    [Fact]
    public void Numbers_are_written_with_a_dot_whatever_the_system_language()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            Assert.Contains("\"min\":0.5", PageScript.ModJson(Loaded(Setting("N", new SettingKind.Number(false, 0.5, 2)))));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void An_update_carries_the_note_for_its_setting()
    {
        var note = new SettingNote(new SettingId("g", "General", "N"), true, new BilingualText("Bad.", "\u574f\u3002"));

        var script = PageScript.Update(Loaded(), note);

        Assert.StartsWith("window.__modmanager&&window.__modmanager.update(", script);
        Assert.Contains("\"warn\":true", script);
        Assert.EndsWith(",null)", PageScript.Update(Loaded(), null));
    }

    [Fact]
    public void A_mod_sends_its_state_info_link_kinds_and_switch_but_never_the_link_addresses()
    {
        var info = new ModInfo { Author = "Ann", Description = new Description("Does a thing.", "做事。"), Links = new[] { new ModLink(LinkKind.Nexus, "https://www.nexusmods.com/survivallog/mods/4"), new ModLink(LinkKind.GitHub, "https://github.com/a/b") } };
        var mod = new ModEntry("g", "Mod", "1.0", "Mod.dll", new ModState.Off(), info, new ModSwitch.Allowed(true, new[] { "Child" }), NotChecked);

        var text = PageScript.ModJson(mod);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;

        Assert.Equal("off", root.GetProperty("state").GetString());
        Assert.Equal("Ann", root.GetProperty("author").GetString());
        Assert.Equal("做事。", root.GetProperty("about").GetProperty("zh").GetString());
        Assert.Equal("nexus", root.GetProperty("links")[0].GetString());
        Assert.Equal("github", root.GetProperty("links")[1].GetString());
        Assert.Equal("allowed", root.GetProperty("switch").GetProperty("kind").GetString());
        Assert.True(root.GetProperty("switch").GetProperty("next").GetBoolean());
        Assert.Equal("Child", root.GetProperty("switch").GetProperty("stops")[0].GetString());
        Assert.DoesNotContain("nexusmods.com", text);
    }

    [Fact]
    public void A_mod_without_info_sends_nulls_and_a_locked_or_missing_switch_says_so()
    {
        var locked = new ModEntry("g", "Toolkit", "1.0", "T.dll", new ModState.NotLoaded(), ModInfo.None, new ModSwitch.Locked(false, new[] { "Mod Manager" }), NotChecked);
        var stuck = new ModEntry("h", "Elsewhere", "1.0", "E.dll", new ModState.Loaded(System.Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Unavailable(), NotChecked);

        using var lockedJson = JsonDocument.Parse(PageScript.ModJson(locked));
        using var stuckJson = JsonDocument.Parse(PageScript.ModJson(stuck));

        Assert.Equal("notLoaded", lockedJson.RootElement.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, lockedJson.RootElement.GetProperty("author").ValueKind);
        Assert.Equal(JsonValueKind.Null, lockedJson.RootElement.GetProperty("about").ValueKind);
        Assert.Equal(0, lockedJson.RootElement.GetProperty("links").GetArrayLength());
        Assert.Equal("locked", lockedJson.RootElement.GetProperty("switch").GetProperty("kind").GetString());
        Assert.False(lockedJson.RootElement.GetProperty("switch").GetProperty("self").GetBoolean());
        Assert.Equal("Mod Manager", lockedJson.RootElement.GetProperty("switch").GetProperty("neededBy")[0].GetString());
        Assert.Equal("none", stuckJson.RootElement.GetProperty("switch").GetProperty("kind").GetString());
    }

    [Fact]
    public void A_held_switch_tells_the_page_where_the_mod_stands_next_start()
    {
        var held = new ModEntry("g", "Mod", "1.0", "Mod.dll", new ModState.Loaded(System.Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Held(true), NotChecked);

        using var json = JsonDocument.Parse(PageScript.ModJson(held));

        Assert.Equal("held", json.RootElement.GetProperty("switch").GetProperty("kind").GetString());
        Assert.True(json.RootElement.GetProperty("switch").GetProperty("next").GetBoolean());
        Assert.True(held.IsOnNextStart);
    }

    [Fact]
    public void An_update_sends_its_tag_and_notes_but_never_the_release_address()
    {
        var notes = new NotesState.Ready(new[] { new ReleaseNote("v1.2.0", "Fixed things."), new ReleaseNote("v1.1.0", "New map.") });
        var mod = Loaded() with { Update = new UpdateState.Available("v1.2.0", notes) };

        var text = PageScript.ModJson(mod);
        using var json = JsonDocument.Parse(text);
        var update = json.RootElement.GetProperty("update");
        var releases = update.GetProperty("notes").GetProperty("releases");

        Assert.Equal("available", update.GetProperty("kind").GetString());
        Assert.Equal("v1.2.0", update.GetProperty("tag").GetString());
        Assert.Equal("releases", update.GetProperty("notes").GetProperty("kind").GetString());
        Assert.Equal(2, releases.GetArrayLength());
        Assert.Equal("v1.1.0", releases[1].GetProperty("tag").GetString());
        Assert.Equal("New map.", releases[1].GetProperty("text").GetString());
        Assert.DoesNotContain("github.com", text);
    }

    [Fact]
    public void Shared_patches_send_the_other_mod_the_risk_and_what_each_mod_does()
    {
        var method = new SharedMethod("Player.TakeDamage", PatchRisk.MayClash, new[] { PatchEffect.CanSkip }, new[] { PatchEffect.RunsBefore, PatchEffect.RunsAfter });
        using var json = JsonDocument.Parse(PageScript.ModJson(Loaded() with { PatchOverlaps = new[] { new PatchOverlap("com.b", PatchRisk.MayClash, new[] { method }) } }));
        var overlap = json.RootElement.GetProperty("patchOverlaps")[0];
        var sent = overlap.GetProperty("methods")[0];

        Assert.Equal("com.b", overlap.GetProperty("guid").GetString());
        Assert.Equal("may", overlap.GetProperty("risk").GetString());
        Assert.Equal("Player.TakeDamage", sent.GetProperty("name").GetString());
        Assert.Equal("skip", sent.GetProperty("mine")[0].GetString());
        Assert.Equal("after", sent.GetProperty("theirs")[1].GetString());
    }

    [Fact]
    public void A_rollback_sends_the_version_it_goes_back_to_and_why_it_failed()
    {
        using var available = JsonDocument.Parse(PageScript.ModJson(Loaded() with { Rollback = new RollbackState.Available("1.0.0") }));
        using var failed = JsonDocument.Parse(PageScript.ModJson(Loaded() with { Rollback = new RollbackState.Failed("1.0.0", "the file is in use") }));
        using var none = JsonDocument.Parse(PageScript.ModJson(Loaded()));

        Assert.Equal("available", available.RootElement.GetProperty("rollback").GetProperty("kind").GetString());
        Assert.Equal("1.0.0", available.RootElement.GetProperty("rollback").GetProperty("version").GetString());
        Assert.Equal("the file is in use", failed.RootElement.GetProperty("rollback").GetProperty("reason").GetString());
        Assert.Equal("none", none.RootElement.GetProperty("rollback").GetProperty("kind").GetString());
    }

    [Fact]
    public void A_changelog_sends_its_state_with_the_text_or_why_it_failed()
    {
        using var ready = JsonDocument.Parse(PageScript.ModJson(Loaded() with { Changelog = new ChangelogState.Ready("## 1.0\n- \"First\" <b>") }));
        using var failed = JsonDocument.Parse(PageScript.ModJson(Loaded() with { Changelog = new ChangelogState.Failed("offline") }));
        using var asked = JsonDocument.Parse(PageScript.ModJson(Loaded() with { Changelog = new ChangelogState.NotAsked() }));
        using var none = JsonDocument.Parse(PageScript.ModJson(Loaded()));

        Assert.Equal("ready", ready.RootElement.GetProperty("changelog").GetProperty("kind").GetString());
        Assert.Equal("## 1.0\n- \"First\" <b>", ready.RootElement.GetProperty("changelog").GetProperty("text").GetString());
        Assert.Equal("offline", failed.RootElement.GetProperty("changelog").GetProperty("reason").GetString());
        Assert.Equal("notAsked", asked.RootElement.GetProperty("changelog").GetProperty("kind").GetString());
        Assert.Equal("none", none.RootElement.GetProperty("changelog").GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("checking")]
    [InlineData("current")]
    [InlineData("noReleases")]
    [InlineData("failed")]
    [InlineData("unreadable")]
    public void Every_update_state_has_its_own_kind_for_the_page(string kind)
    {
        UpdateState state = kind switch
        {
            "none" => new UpdateState.NotChecked(),
            "checking" => new UpdateState.Checking(),
            "current" => new UpdateState.UpToDate("v1"),
            "noReleases" => new UpdateState.NoReleases(),
            "failed" => new UpdateState.Failed("offline"),
            _ => new UpdateState.Unreadable("nightly"),
        };

        using var json = JsonDocument.Parse(PageScript.ModJson(Loaded() with { Update = state }));

        Assert.Equal(kind, json.RootElement.GetProperty("update").GetProperty("kind").GetString());
    }

    [Fact]
    public void A_refresh_redraws_the_view_without_a_note_so_the_page_keeps_its_own()
    {
        var script = PageScript.Refresh(new ModsView(new[] { Loaded() }, System.Array.Empty<string>(), false, "F10"));

        Assert.Contains(".replace({", script);
        Assert.EndsWith("})", script);
        Assert.DoesNotContain("},null)", script);
    }

    [Fact]
    public void A_replace_carries_the_whole_view_and_the_note_for_the_flipped_mod()
    {
        var view = new ModsView(new[] { Loaded() }, System.Array.Empty<string>(), false, "F10");
        var note = new ModNote("g", true, new BilingualText("Could not.", "不行。"));

        var script = PageScript.Replace(view, note);

        Assert.StartsWith("window.__modmanager&&window.__modmanager.replace({", script);
        Assert.Contains("\"guid\":\"g\",\"warn\":true,\"en\":\"Could not.\"", script);
        Assert.EndsWith(",null)", PageScript.Replace(view, null));
    }

    [Fact]
    public void A_mod_sends_the_name_it_asks_for_in_each_language_and_its_plugin_name_for_search()
    {
        var mod = Loaded() with { Info = new ModInfo { Name = new Description("Better Map", "更好的地图") } };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var root = json.RootElement;

        Assert.Equal("Better Map", root.GetProperty("name").GetString());
        Assert.Equal("更好的地图", root.GetProperty("nameZh").GetString());
        Assert.Equal("Mod", root.GetProperty("plugin").GetString());
    }

    [Fact]
    public void A_mod_without_a_name_of_its_own_shows_its_plugin_name_in_both_languages()
    {
        using var json = JsonDocument.Parse(PageScript.ModJson(Loaded()));

        Assert.Equal("Mod", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("Mod", json.RootElement.GetProperty("nameZh").GetString());
    }

    [Fact]
    public void A_mod_sends_its_category_library_flag_versions_relations_and_install()
    {
        var info = new ModInfo { Category = ModCategory.QualityOfLife, IsLibrary = true, GameVersions = new[] { "1.0" } };
        var mod = Loaded() with
        {
            Info = info,
            ListedGameVersions = new[] { "1.1" },
            IsUntestedOnGame = true,
            Relations = new[] { new Relation(RelationKind.Needs, "t", "Toolkit", RelationStatus.Off, true), new Relation(RelationKind.DoesNotWorkWith, "x", "x", RelationStatus.Missing, false) },
            Install = new InstallState.Failed("no zip"),
        };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var root = json.RootElement;
        var needs = root.GetProperty("relations")[0];

        Assert.Equal("QualityOfLife", root.GetProperty("category").GetString());
        Assert.True(root.GetProperty("library").GetBoolean());
        Assert.Equal("1.0", root.GetProperty("builtFor")[0].GetString());
        Assert.Equal("1.1", root.GetProperty("listedFor")[0].GetString());
        Assert.True(root.GetProperty("untested").GetBoolean());
        Assert.Equal("needs", needs.GetProperty("kind").GetString());
        Assert.Equal("off", needs.GetProperty("status").GetString());
        Assert.True(needs.GetProperty("canTurnOn").GetBoolean());
        Assert.Equal("doesNotWorkWith", root.GetProperty("relations")[1].GetProperty("kind").GetString());
        Assert.Equal("missing", root.GetProperty("relations")[1].GetProperty("status").GetString());
        Assert.Equal("failed", root.GetProperty("install").GetProperty("kind").GetString());
        Assert.Equal("no zip", root.GetProperty("install").GetProperty("reason").GetString());
    }

    [Fact]
    public void A_refused_update_sends_why_so_the_page_offers_no_retry()
    {
        using var json = JsonDocument.Parse(PageScript.ModJson(Loaded() with { Install = new InstallState.Refused("it newly starts programs") }));
        var install = json.RootElement.GetProperty("install");

        Assert.Equal("refused", install.GetProperty("kind").GetString());
        Assert.Equal("it newly starts programs", install.GetProperty("reason").GetString());
    }

    [Fact]
    public void Every_power_has_a_name_the_page_knows()
    {
        var mod = Loaded() with { Install = new InstallState.NeedsApproval("v2.0", Enum.GetValues<Power>()) };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));

        Assert.Contains("opens", json.RootElement.GetProperty("install").GetProperty("powers").EnumerateArray().Select(power => power.GetString()));
    }

    [Fact]
    public void An_update_waiting_for_approval_sends_its_tag_and_what_it_newly_does()
    {
        var mod = Loaded() with { Install = new InstallState.NeedsApproval("v2.0", new[] { Power.Network, Power.AddsDll }) };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var install = json.RootElement.GetProperty("install");

        Assert.Equal("needsApproval", install.GetProperty("kind").GetString());
        Assert.Equal("v2.0", install.GetProperty("tag").GetString());
        Assert.Equal(new[] { "network", "newDll" }, install.GetProperty("powers").EnumerateArray().Select(power => power.GetString()).ToArray());
    }

    [Fact]
    public void A_mod_sends_the_version_range_it_needs_and_the_mods_that_depend_on_it()
    {
        var mod = Loaded() with
        {
            Relations = new[] { new Relation(RelationKind.Needs, "t", "Toolkit", RelationStatus.WrongVersion, false, ">=0.4.0"), new Relation(RelationKind.WorksWith, "s", "Stacking", RelationStatus.On, true) },
            Dependents = new[] { new Relation(RelationKind.Needs, "c", "Crafting", RelationStatus.On, false) },
        };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var root = json.RootElement;

        Assert.Equal("wrongVersion", root.GetProperty("relations")[0].GetProperty("status").GetString());
        Assert.Equal(">=0.4.0", root.GetProperty("relations")[0].GetProperty("range").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("relations")[1].GetProperty("range").ValueKind);
        Assert.Equal("c", root.GetProperty("dependents")[0].GetProperty("guid").GetString());
        Assert.Equal("needs", root.GetProperty("dependents")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void A_mod_without_a_category_sends_null()
    {
        using var json = JsonDocument.Parse(PageScript.ModJson(Loaded()));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("category").ValueKind);
        Assert.Equal("none", json.RootElement.GetProperty("install").GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData(ApplyMode.Live, "live")]
    [InlineData(ApplyMode.Restart, "restart")]
    [InlineData(ApplyMode.Unknown, "unknown")]
    public void Each_setting_says_when_a_change_takes_effect(ApplyMode apply, string expected)
    {
        var mod = Loaded(Setting("On", new SettingKind.Toggle()) with { Apply = apply });

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));

        Assert.Equal(expected, json.RootElement.GetProperty("settings")[0].GetProperty("apply").GetString());
    }

    [Fact]
    public void The_view_carries_the_game_version_or_null()
    {
        var view = new ModsView(new[] { Loaded() }, System.Array.Empty<string>(), false, "F10");

        using var known = JsonDocument.Parse(PageScript.ViewJson(view with { GameVersion = "1.1.18902" }));
        using var unknown = JsonDocument.Parse(PageScript.ViewJson(view));

        Assert.Equal("1.1.18902", known.RootElement.GetProperty("game").GetString());
        Assert.Equal(JsonValueKind.Null, unknown.RootElement.GetProperty("game").ValueKind);
    }

    [Fact]
    public void Opening_on_a_filter_names_it_and_opening_plainly_leaves_the_page_its_own()
    {
        var view = new ModsView(new[] { Loaded() }, System.Array.Empty<string>(), false, "F10");

        Assert.EndsWith("},\"updates\")", PageScript.Open(view, "updates"));
        Assert.EndsWith("]})", PageScript.Open(view));
    }

    [Fact]
    public void Icons_go_by_guid_as_one_object()
    {
        var script = PageScript.Icons(new Dictionary<string, string> { ["b"] = "data:image/png;base64,QQ==", ["a"] = "data:image/jpeg;base64,Qg==" });
        var argument = script.Substring(script.IndexOf(".icons(", System.StringComparison.Ordinal) + 7).TrimEnd(')');

        using var json = JsonDocument.Parse(argument);

        Assert.Equal("data:image/png;base64,QQ==", json.RootElement.GetProperty("b").GetString());
        Assert.Equal("data:image/jpeg;base64,Qg==", json.RootElement.GetProperty("a").GetString());
    }

    [Fact]
    public void Each_rich_control_and_whether_a_setting_is_shown_or_advanced_reach_the_page()
    {
        var mod = Loaded(
            Setting("One", new SettingKind.Item()) with { IsAdvanced = true },
            Setting("Many", new SettingKind.ItemList()) with { IsShown = false },
            Setting("Counts", new SettingKind.ItemCounts()),
            Setting("Kind", new SettingKind.Category()));

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var settings = json.RootElement.GetProperty("settings");

        Assert.Equal(new[] { "item", "itemList", "itemCounts", "category" }, settings.EnumerateArray().Select(s => s.GetProperty("kind").GetString()));
        Assert.True(settings[0].GetProperty("advanced").GetBoolean());
        Assert.True(settings[0].GetProperty("shown").GetBoolean());
        Assert.False(settings[1].GetProperty("shown").GetBoolean());
        Assert.False(settings[1].GetProperty("advanced").GetBoolean());
    }

    [Fact]
    public void A_mods_fixed_hotkeys_reach_the_page()
    {
        var info = ModInfo.None with { Hotkeys = new[] { new FixedHotkey("F11 + LeftControl", new Description("Console", "控制台")) } };
        var mod = Loaded() with { Info = info };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var hotkey = json.RootElement.GetProperty("hotkeys")[0];

        Assert.Equal("F11 + LeftControl", hotkey.GetProperty("keys").GetString());
        Assert.Equal("控制台", hotkey.GetProperty("zh").GetString());
    }

    [Fact]
    public void The_item_table_reaches_the_page_with_icons_and_categories()
    {
        var script = PageScript.Items(new[] { new CatalogItem(1203, "Apple", "Res/Food/apple.png", 1), new CatalogItem(7, "Rope", null, 9) }, new[] { new CategoryName(1, "Food") });
        var argument = script.Substring(script.IndexOf(".items(", System.StringComparison.Ordinal) + 7).TrimEnd(')');

        using var json = JsonDocument.Parse(argument);
        var items = json.RootElement.GetProperty("items");

        Assert.Equal(1203, items[0].GetProperty("id").GetInt32());
        Assert.Equal("Res/Food/apple.png", items[0].GetProperty("icon").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("icon").ValueKind);
        Assert.Equal("Food", json.RootElement.GetProperty("categories")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Esc_goes_to_the_page_so_a_tab_another_mod_added_can_use_it_before_the_screen_closes()
    {
        var script = PageScript.Escape();

        Assert.Contains(".escape(", script);
        Assert.DoesNotContain(".close(", script);
        Assert.StartsWith("window.__modmanager&&", script);
    }
}
