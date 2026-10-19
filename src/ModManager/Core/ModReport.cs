using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModManager.Core;

/// <summary>
/// The installed mods as plain text for a bug report. Always in English, since it goes to mod authors, and one mod per
/// line so it reads the same pasted into Discord, Nexus or GitHub.
/// </summary>
public static class ModReport
{
    public static string Text(ModsView view)
    {
        var text = new StringBuilder();

        text.Append("Survival Log ").Append(view.GameVersion ?? "(version unknown)").Append('\n');
        text.Append("Mods (").Append(view.Mods.Count).Append("):\n");

        foreach (var mod in view.Mods) text.Append("- ").Append(Line(mod)).Append('\n');

        if (view.Problems.Count > 0)
        {
            text.Append("Load problems:\n");

            foreach (var problem in view.Problems) text.Append("- ").Append(OneLine(problem)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Name, version, GUID, file and state; a switch flipped this session is said too, as the game still runs the old way.</summary>
    private static string Line(ModEntry mod)
    {
        var state = mod.State switch
        {
            ModState.Loaded => "running",
            ModState.NotLoaded => "failed to load",
            ModState.Off => "off",
            _ => throw new ArgumentOutOfRangeException(nameof(mod), mod.State, null),
        };
        var isOnNow = mod.State is not ModState.Off;
        var next = mod.IsOnNextStart == isOnNow ? "" : mod.IsOnNextStart ? ", on from the next start" : ", off from the next start";

        return OneLine(mod.DisplayName) + " " + mod.Version + " (" + mod.Guid + ", " + mod.FileName + "): " + state + next;
    }

    private static string OneLine(string text) => string.Join(" ", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()));
}
