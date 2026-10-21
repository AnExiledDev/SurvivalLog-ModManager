using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>The game versions each version of a mod is known to work on, as its author lists them.</summary>
public sealed class GameVersionList
{
    public static readonly GameVersionList Empty = new(Array.Empty<(ReleaseVersion, IReadOnlyList<string>)>());

    private readonly IReadOnlyList<(ReleaseVersion ModVersion, IReadOnlyList<string> GameVersions)> _entries;

    public GameVersionList(IReadOnlyList<(ReleaseVersion ModVersion, IReadOnlyList<string> GameVersions)> entries)
    {
        _entries = entries;
    }

    /// <summary>Every game version listed for this version of the mod; "0.4" and "0.4.0" are the same version.</summary>
    public IReadOnlyList<string> For(string modVersion)
    {
        if (ReleaseVersion.Parse(modVersion) is not { } version) return Array.Empty<string>();

        return _entries.Where(entry => entry.ModVersion.CompareTo(version) == 0).SelectMany(entry => entry.GameVersions).Distinct().ToList();
    }
}

public sealed record GameVersionListRead(GameVersionList List, IReadOnlyList<string> Ignored);

/// <summary>What the repository answered when asked for its list.</summary>
public abstract record GameVersionListAnswer
{
    private GameVersionListAnswer()
    {
    }

    /// <summary>The file's text is kept as it came, so it can be saved for a start without a connection.</summary>
    public sealed record Found(string Text, GameVersionListRead Read) : GameVersionListAnswer;

    /// <summary>The repository has no list, so whatever was saved from it before no longer holds.</summary>
    public sealed record Missing : GameVersionListAnswer;

    /// <summary>No answer worth trusting; the saved list, if any, stays in use.</summary>
    public sealed record Failed(string Reason) : GameVersionListAnswer;
}

/// <summary>
/// Reads the game-versions.txt a mod's author keeps at the top of its GitHub repository, so a mod that still works on
/// a newer game version can say so without a new release:
/// <code>
/// # mod version = game versions it is known to work on
/// 0.4.0 = 1.1.18902, 1.1.19131
/// </code>
/// </summary>
public static class GameVersionLists
{
    public const string FileName = "game-versions.txt";

    private const int MaxEntries = 500;
    private const int MaxGameVersionsPerEntry = 50;

    /// <summary>The file on the repository's default branch, from raw.githubusercontent.com, which needs no account.</summary>
    public static Uri Address(string repository) => new("https://raw.githubusercontent.com/" + repository + "/HEAD/" + FileName);

    public static GameVersionListAnswer From(HttpAnswer answer) => answer switch
    {
        HttpAnswer.Unreachable unreachable => new GameVersionListAnswer.Failed(unreachable.Reason),
        HttpAnswer.Answered { Status: 200 } answered => new GameVersionListAnswer.Found(answered.Body, Parse(answered.Body)),
        HttpAnswer.Answered { Status: 404 } => new GameVersionListAnswer.Missing(),
        HttpAnswer.Answered answered => new GameVersionListAnswer.Failed("the server answered " + answered.Status),
        _ => throw new ArgumentOutOfRangeException(nameof(answer), answer, null),
    };

    /// <summary>Blank lines and lines starting with "#" are passed over. A line that cannot be read is reported and the rest are kept.</summary>
    public static GameVersionListRead Parse(string text)
    {
        var entries = new List<(ReleaseVersion, IReadOnlyList<string>)>();
        var ignored = new List<string>();
        var lines = text.Replace("\r", "").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

            if (entries.Count == MaxEntries)
            {
                ignored.Add("more than " + MaxEntries + " mod versions are listed; the rest are passed over");

                break;
            }

            if (Entry(line) is { } entry) entries.Add(entry);
            else ignored.Add("line " + (i + 1) + " \"" + line + "\" must be a mod version, \"=\", then up to " + MaxGameVersionsPerEntry + " game versions separated by commas, such as 0.4.0 = 1.1.18902, 1.1.19131");
        }

        return new GameVersionListRead(new GameVersionList(entries), ignored);
    }

    private static (ReleaseVersion, IReadOnlyList<string>)? Entry(string line)
    {
        var equals = line.IndexOf('=');

        if (equals < 0) return null;

        var modVersion = ReleaseVersion.Parse(line.Substring(0, equals).Trim());
        var gameVersions = line.Substring(equals + 1).Split(',').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
        var isReadable = modVersion != null && gameVersions.Count is > 0 and <= MaxGameVersionsPerEntry && gameVersions.All(GameVersion.IsVersion);

        return isReadable && modVersion != null ? (modVersion, gameVersions) : null;
    }
}
