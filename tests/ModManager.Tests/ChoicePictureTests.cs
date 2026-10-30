using System.Collections.Generic;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A list setting whose mod draws a picture of each choice shows the pictures in its drop-down, so a player picks a
/// look by seeing it. page.js runs in Jint on PageFake.js, with every element it makes swapped for a stand-in that
/// accepts any call and keeps the markup written into it, so the drawn settings can be read back.
/// </summary>
public class ChoicePictureTests
{
    private const string Lamplight = "<div style=\"display:flex;gap:4px\"><svg viewBox=\"0 0 24 24\"><path fill=\"#f5b82e\" d=\"M2 2h20v20H2z\"/></svg></div>";
    private const string Classic = "<div style=\"background:#3c3c3c\">C</div>";

    private const string StandIns = """
        var drawn = [];

        function standIn() {
          var own = {};

          return new Proxy(function () {}, {
            get: function (target, key) {
              if (key === Symbol.toPrimitive) return function () { return 0; };
              if (key === Symbol.iterator) return function () { return [][Symbol.iterator](); };
              if (key === 'then') return undefined;
              if (!(key in own)) own[key] = standIn();

              return own[key];
            },
            set: function (target, key, value) {
              own[key] = value;
              if (key === 'innerHTML') drawn.push(String(value));

              return true;
            },
            apply: function () { return standIn(); },
            construct: function () { return standIn(); }
          });
        }

        document.createElement = function () { return standIn(); };
        window.localStorage = { getItem: function () { return null; }, setItem: function () {} };
        """;

    [Fact]
    public void Each_choice_shows_its_picture_beside_its_name_and_the_picked_one_shows_in_the_closed_box()
    {
        var pane = Draw("Lamplight");
        var style = Between(pane, "<div class=\"dd pv\">", "<div class=\"reset");

        Assert.Contains("<div class=\"dd-trigger\"><div class=\"dd-pv\"><div class=\"dd-pic\">" + Lamplight + "</div><span class=\"dd-name\">Lamplight</span></div>", style);
        Assert.Contains("data-option=\"0\"><div class=\"dd-pv\"><div class=\"dd-pic\">" + Lamplight + "</div><span class=\"dd-name\">Lamplight</span></div>", style);
        Assert.Contains("data-option=\"2\"><div class=\"dd-pv\"><div class=\"dd-pic\">" + Classic + "</div><span class=\"dd-name\">Classic</span></div>", style);
    }

    [Fact]
    public void A_choice_without_a_picture_shows_its_name_alone()
    {
        var pane = Draw("FieldKit");
        var style = Between(pane, "<div class=\"dd pv\">", "<div class=\"reset");

        Assert.Contains("<div class=\"dd-trigger\"><div class=\"dd-pv\"><span class=\"dd-name\">FieldKit</span></div>", style);
        Assert.Contains("data-option=\"1\"><div class=\"dd-pv\"><span class=\"dd-name\">FieldKit</span></div>", style);
    }

    [Fact]
    public void A_list_with_no_pictures_is_drawn_as_before()
    {
        var pane = Draw("Lamplight");

        Assert.Contains("<div class=\"dd\"><div class=\"dd-trigger\"><span>Off</span><span class=\"dd-arrow\"></span></div>", pane);
        Assert.Contains("data-option=\"1\">On</div>", pane);
        Assert.Single(AllIndexes(pane, "class=\"dd pv\""));
    }

    /// <summary>Opens the screen on one mod with a pictured list set to <paramref name="current"/> and a plain list; returns the drawn settings.</summary>
    private static string Draw(string current)
    {
        var pictures = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["lamplight"] = Lamplight, ["Classic"] = Classic };
        var style = Setting("HudButtonStyle", new[] { "Lamplight", "FieldKit", "Classic" }, current) with { ChoicePreview = pictures };
        var plain = Setting("Plain", new[] { "Off", "On" }, "Off");
        var mod = new ModEntry("g", "Mod", "1.0", "Mod.dll", new ModState.Loaded(new[] { style, plain }), ModInfo.None, new ModSwitch.Allowed(true, System.Array.Empty<string>()), new UpdateState.NotChecked());

        var engine = new Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(StandIns);
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(new ModsView(new[] { mod }, System.Array.Empty<string>(), false, "F10")));

        var pane = engine.Evaluate("drawn.filter(function (html) { return html.indexOf('data-row=\"') >= 0; }).pop() || ''").AsString();

        Assert.NotEqual("", pane);

        return pane;
    }

    private static Setting Setting(string key, string[] options, string current) =>
        new(new SettingId("g", "Interface", key), key, new Description("en", "zh"), new SettingKind.Choice(options), options[0], current, options[0], false);

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, System.StringComparison.Ordinal);

        Assert.True(from >= 0, start + " is not drawn");

        var to = text.IndexOf(end, from, System.StringComparison.Ordinal);

        return to < 0 ? text.Substring(from) : text.Substring(from, to - from);
    }

    private static List<int> AllIndexes(string text, string part)
    {
        var found = new List<int>();

        for (var i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + 1, System.StringComparison.Ordinal)) found.Add(i);

        return found;
    }

    private static string Resource(string name)
    {
        using var stream = typeof(ChoicePictureTests).Assembly.GetManifestResourceStream(name) ?? throw new System.InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new System.IO.StreamReader(stream);

        return reader.ReadToEnd();
    }
}
