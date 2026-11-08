using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// Other mods draw their own tabs inside the Mods screen, under the same #slmm root, so a class the screen styles for
/// itself also styles theirs. Pack Rat's tabs use the pr class and pr- names: when the screen's picker rows took
/// .pr (0.16.0), its display: flex laid Pack Rat's whole Grids and Items screens out in one row, most of it outside
/// the window.
/// </summary>
public class TabClassesTests
{
    [Fact]
    public void The_screen_leaves_Pack_Rats_class_names_alone()
    {
        var classes = Regex.Matches(Stylesheet(), @"\.(-?[A-Za-z_][\w-]*)").Select(m => m.Groups[1].Value).Distinct().ToList();
        var taken = classes.Where(name => name == "pr" || name.StartsWith("pr-")).ToList();

        Assert.Contains("pk", classes);
        Assert.Empty(taken);
    }

    private static string Stylesheet()
    {
        using var stream = typeof(TabClassesTests).Assembly.GetManifestResourceStream("ModManager.page.js") ?? throw new InvalidOperationException("page.js is not embedded in the test assembly");
        using var reader = new StreamReader(stream);
        var page = reader.ReadToEnd();
        var start = page.IndexOf("style.textContent = `", StringComparison.Ordinal);
        var end = page.IndexOf('`', start + "style.textContent = `".Length);

        Assert.True(start >= 0 && end > start, "page.js has no stylesheet");

        return page.Substring(start, end - start);
    }
}
