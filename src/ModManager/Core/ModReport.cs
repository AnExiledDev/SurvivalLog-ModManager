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

        AppendErrors(text, view);

        return text.ToString();
    }

    public const int MaxReportedErrors = 30;
    private const int FramesPerError = 3;

    /// <summary>Errors before warnings, each under the mod it came from, with its first stack lines and any clash.</summary>
    private static void AppendErrors(StringBuilder text, ModsView view)
    {
        var nameOf = view.Mods.GroupBy(mod => mod.Guid).ToDictionary(group => group.Key, group => OneLine(group.First().DisplayName));
        var errors = view.Mods.SelectMany(mod => mod.Errors.Select(error => (Owner: nameOf[mod.Guid], Error: error)))
            .Concat(view.OtherErrors.Select(error => (Owner: "BepInEx or the game", Error: error)))
            .OrderByDescending(pair => pair.Error.Problem.Level)
            .ToList();

        if (errors.Count == 0) return;

        text.Append("Errors this session:\n");

        foreach (var (owner, error) in errors.Take(MaxReportedErrors))
        {
            var problem = error.Problem;
            var level = problem.Level == ProblemLevel.Error ? "error" : "warning";
            var count = problem.Count > 1 ? " x" + problem.Count : "";

            text.Append("- ").Append(owner).Append(": ").Append(level).Append(count).Append(": ").Append(OneLine(problem.Message)).Append('\n');

            foreach (var frame in problem.Stack.Take(FramesPerError)) text.Append("    ").Append(frame).Append('\n');

            foreach (var clash in error.Clashes)
            {
                var others = string.Join(", ", clash.Guids.Select(guid => nameOf.TryGetValue(guid, out var name) ? name : guid));

                text.Append("    went through ").Append(clash.Method).Append(", which ").Append(others).Append(" also patch").Append(clash.Guids.Count == 1 ? "es" : "").Append('\n');
            }
        }

        var notShown = Math.Max(0, errors.Count - MaxReportedErrors) + view.ErrorsNotKept;

        if (notShown > 0) text.Append("- and ").Append(notShown).Append(" more\n");
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
