using Jint;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The Mods screen follows Exiled's Toolkit's UI size: page.js hands the Toolkit its window, run in Jint on a fake root
/// page (PageFake.js). The backdrop around the window fills the screen at any size, so handing it over would never let
/// the window be fitted; the window itself is what is zoomed and what must fit. Tabs from other mods sit inside it.
/// </summary>
public class ScreenUiSizeTests
{
    [Fact]
    public void The_window_is_handed_over_once_and_fitted_to_the_screen()
    {
        var page = new Page();

        Assert.Equal(1, page.Number("handed.length"));
        Assert.True(page.Bool("handed[0].el === screenRoot().querySelector('.win')"));
        Assert.True(page.Bool("!handed[0].options || handed[0].options.fit !== false"));
    }

    [Fact]
    public void Before_the_Toolkit_loads_the_window_waits_for_it()
    {
        var page = new Page("delete window.ExiledUi");

        Assert.Equal(1, page.Number("window.__exiledUiWait.length"));
        Assert.True(page.Bool("window.__exiledUiWait[0][0] === screenRoot().querySelector('.win')"));
    }

    /// <summary>page.js on PageFake.js; <paramref name="before"/> runs between the two.</summary>
    private sealed class Page
    {
        private readonly Engine _engine = new();

        public Page(string? before = null)
        {
            _engine.Execute(Resource("ModManager.Tests.PageFake.js"));
            if (before != null) _engine.Execute(before);
            _engine.Execute(Resource("ModManager.page.js"));
        }

        public int Number(string expression) => (int)_engine.Evaluate($"Number({expression})").AsNumber();

        public bool Bool(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

        private static string Resource(string name)
        {
            using var stream = typeof(ScreenUiSizeTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
