using System.Text.Json;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class PageScriptTests
{
    private static Setting Setting(string key, SettingKind kind, string current = "1") =>
        new(new SettingId("g", "General", key), key, new Description("en", "zh"), kind, "1", current, "1", false);

    private static ModEntry Loaded(params Setting[] settings) => new("g", "Mod", "1.0", "Mod.dll", new ModState.Loaded(settings));

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
        var view = new ModsView(new[] { mod, new ModEntry("h", "Broken", "2.0", "Broken.dll", new ModState.NotLoaded()) }, new[] { "missing dependency" }, true, "F10");

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
}
