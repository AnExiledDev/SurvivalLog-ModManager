using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The version is written in four files. The game and the Mods screen show the one in Plugin.cs, the DLL's file
/// details show the csproj's, and Mod Manager reads CHANGELOG.md and game-versions.txt from GitHub, so a bump that
/// misses one leaves the mod naming two versions.
/// </summary>
public class VersionBumpTests
{
    [Fact]
    public void Every_file_that_names_the_version_names_the_one_in_Plugin()
    {
        var repo = RepoRoot();
        var version = Find(Read(repo, "src/ModManager/Plugin.cs"), "public const string Version = \"([^\"]+)\"");

        Assert.Equal(version, Find(Read(repo, "src/ModManager/ModManager.csproj"), "<Version>([^<]+)</Version>"));
        Assert.Equal(version, Find(Read(repo, "CHANGELOG.md"), @"^## (\S+)"));
        Assert.Matches(new Regex("^" + Regex.Escape(version) + " = ", RegexOptions.Multiline), Read(repo, "game-versions.txt"));
    }

    // The tests run from bin under the repo; source paths are mapped away at build time, so walk up from there.
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "game-versions.txt"))) return dir.FullName;

        throw new InvalidOperationException("no game-versions.txt above " + AppContext.BaseDirectory);
    }

    private static string Read(string repo, string file) => File.ReadAllText(Path.Combine(repo, file));

    private static string Find(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.Multiline);

        Assert.True(match.Success, "not found: " + pattern);

        return match.Groups[1].Value;
    }
}
