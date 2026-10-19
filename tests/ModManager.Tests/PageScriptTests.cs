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
        var info = new ModInfo("Ann", new Description("Does a thing.", "做事。"), new[] { new ModLink(LinkKind.Nexus, "https://www.nexusmods.com/survivallog/mods/4"), new ModLink(LinkKind.GitHub, "https://github.com/a/b") });
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
    public void An_update_sends_its_tag_and_notes_but_never_the_release_address()
    {
        var mod = Loaded() with { Update = new UpdateState.Available("v1.2.0", new NotesState.Ready("Fixed things.")) };

        var text = PageScript.ModJson(mod);
        using var json = JsonDocument.Parse(text);
        var update = json.RootElement.GetProperty("update");

        Assert.Equal("available", update.GetProperty("kind").GetString());
        Assert.Equal("v1.2.0", update.GetProperty("tag").GetString());
        Assert.Equal("text", update.GetProperty("notes").GetProperty("kind").GetString());
        Assert.Equal("Fixed things.", update.GetProperty("notes").GetProperty("text").GetString());
        Assert.DoesNotContain("github.com", text);
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
}
