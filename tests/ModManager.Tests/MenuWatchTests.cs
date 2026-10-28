using Jint;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The Mods buttons go into the Esc menu and the title screen, so only those two pages are watched for changes. A
/// watcher on any other game page (the trade map, the explore map, a shop) wakes on every change the game makes there,
/// which costs every frame the map is busy and does nothing. Run in Jint on PageFake.js with fake game frames.
/// </summary>
public class MenuWatchTests
{
    private const string Frames = """
        var watched = [];
        function fakeFrame(url) {
          var win = { location: { href: url } };
          var doc = {
            body: new FakeEl('body'),
            defaultView: win,
            querySelector: function () { return null; },
            getElementById: function () { return null; }
          };
          win.MutationObserver = function () { this.observe = function () { watched.push(url); }; };
          return { src: url, contentWindow: win, contentDocument: doc, addEventListener: function () {} };
        }
        var frames = [
          fakeFrame('file:///Res/UI/OutSetting/OutSetting.html'),
          fakeFrame('file:///Res/UI/MainUI/MainUI.html'),
          fakeFrame('file:///Res/UI/TradeUI/TradeUI.html'),
          fakeFrame('file:///Res/UI/PreDisasterMap/PreDisasterMap.html'),
          fakeFrame('file:///Res/UI/CoreUI1/CoreUI1.html')
        ];
        document.getElementsByTagName = function (tag) { return tag === 'iframe' ? frames : []; };
        """;

    [Fact]
    public void Only_the_esc_menu_and_title_screen_are_watched()
    {
        var engine = Page();

        Assert.Equal(2, (int)engine.Evaluate("watched.length").AsNumber());
        Assert.True(engine.Evaluate("watched.some(function (u) { return /OutSetting/.test(u); })").AsBoolean());
        Assert.True(engine.Evaluate("watched.some(function (u) { return /MainUI/.test(u); })").AsBoolean());
    }

    [Fact]
    public void A_frame_whose_page_changed_to_the_esc_menu_is_judged_by_its_page_not_its_src()
    {
        var engine = Page("frames[2].contentWindow.location.href = 'file:///Res/UI/OutSetting/OutSetting.html';");

        Assert.Equal(3, (int)engine.Evaluate("watched.length").AsNumber());
    }

    private static Engine Page(string? before = null)
    {
        var engine = new Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(Frames);
        if (before != null) engine.Execute(before);
        engine.Execute(Resource("ModManager.page.js"));

        return engine;
    }

    private static string Resource(string name)
    {
        using var stream = typeof(MenuWatchTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
