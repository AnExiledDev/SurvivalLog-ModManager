using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>One entry a mod offers for a List setting: its id as the setting writes it, its names, and how the picker shows it.</summary>
/// <param name="Icon">A picture the game's web page can load, or null.</param>
/// <param name="Group">The shelf the picker files it under, or null.</param>
/// <param name="Note">A line about it for the picker, such as what it does, or null.</param>
public sealed record ListEntry(string Id, Description Name, string? Icon, Description? Group, Description? Note);

/// <summary>
/// Reads the entries a mod's ModManagerAnswers.Entries gives: one per line, as
/// "id|English|Chinese|icon|group|group in Chinese|note|note in Chinese". Only the id is needed; a missing Chinese part
/// takes the English one. An id the setting could not write (empty, or with a comma, colon or space) is skipped, and
/// so is an id seen before.
/// </summary>
public static class ListEntries
{
    public static IReadOnlyList<ListEntry> Parse(string? text)
    {
        var entries = new List<ListEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in (text ?? "").Split('\n'))
        {
            var parts = line.TrimEnd('\r').Split('|').Select(part => part.Trim()).ToArray();
            var id = parts[0];

            if (id.Length == 0 || id.IndexOfAny(Unwritable) >= 0 || !seen.Add(id)) continue;

            var name = Words(Part(parts, 1), Part(parts, 2)) ?? new Description(id, id);
            var icon = Part(parts, 3);

            entries.Add(new ListEntry(id, name, icon.Length > 0 ? icon : null, Words(Part(parts, 4), Part(parts, 5)), Words(Part(parts, 6), Part(parts, 7))));
        }

        return entries;
    }

    private static readonly char[] Unwritable = { ',', ':', ' ', '\t' };

    private static string Part(string[] parts, int index) => index < parts.Length ? parts[index] : "";

    private static Description? Words(string english, string chinese)
    {
        if (english.Length == 0 && chinese.Length == 0) return null;

        return new Description(english.Length > 0 ? english : chinese, chinese.Length > 0 ? chinese : english);
    }
}
