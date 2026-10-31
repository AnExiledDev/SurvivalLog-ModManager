using Jint;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The Mods buttons in the Esc menu and on the title screen are placed by Exiled's Toolkit, which fits every mod's
/// buttons into the menu together. The Mods page must leave the game's pages alone: a button of its own would be
/// placed outside that fit and push the menu off screen again, and a watcher on a game page wakes on every change the
/// game makes there. Run in Jint on PageFake.js with fake game frames.
/// </summary>
public class MenuWatchTests
{
    private const string Frames = """
        var watched = [];
        var looked = [];
        function fakeFrame(url) {
          var win = { location: { href: url } };
          var doc = {
            body: new FakeEl('body'),
            defaultView: win,
            querySelector: function (sel) { looked.push(url); return null; },
            getElementById: function () { looked.push(url); return null; }
          };
          win.MutationObserver = function () { this.observe = function () { watched.push(url); }; };
          return { src: url, contentWindow: win, contentDocument: doc, addEventListener: function () {} };
        }
        var frames = [
          fakeFrame('file:///Res/UI/OutSetting/OutSetting.html'),
          fakeFrame('file:///Res/UI/MainUI/MainUI.html'),
          fakeFrame('file:///Res/UI/TradeUI/TradeUI.html')
        ];
        document.getElementsByTagName = function (tag) { return tag === 'iframe' ? frames : []; };
        """;

    [Fact]
    public void The_esc_menu_and_title_screen_are_left_to_the_Toolkit()
    {
        var engine = Page();

        Assert.Equal(0, (int)engine.Evaluate("watched.length").AsNumber());
        Assert.Equal(0, (int)engine.Evaluate("looked.length").AsNumber());
    }

    private static Engine Page()
    {
        var engine = new Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(Frames);
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
