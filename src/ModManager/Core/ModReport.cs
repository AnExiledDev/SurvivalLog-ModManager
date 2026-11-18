using System;
using System.Collections.Generic;
using System.Globalization;
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
        var names = view.Mods.GroupBy(mod => mod.Guid).ToDictionary(group => group.Key, group => OneLine(group.First().DisplayName));
        Func<string, string> nameOf = guid => names.TryGetValue(guid, out var name) ? name : guid;

        text.Append("Survival Log ").Append(view.GameVersion ?? "(version unknown)").Append('\n');
        text.Append("Mods (").Append(view.Mods.Count).Append("):\n");

        foreach (var mod in view.Mods) text.Append("- ").Append(Line(mod)).Append('\n');

        if (view.Problems.Count > 0)
        {
            text.Append("Load problems:\n");

            foreach (var problem in view.Problems) text.Append("- ").Append(OneLine(problem)).Append('\n');
        }

        AppendSession(text, view, nameOf);
        AppendConflicts(text, view, nameOf);
        AppendGameChanges(text, view, nameOf);
        AppendErrors(text, view, nameOf);

        return text.ToString();
    }

    public const int MaxReportedErrors = 30;
    private const int FramesPerError = 3;

    /// <summary>How many methods or clashes a conflict names before it says how many more there are.</summary>
    private const int ShownPerConflict = 5;
    private const int ShownTables = 10;
    private const int ShownMethods = 10;

    /// <summary>A crash at the end of the last session, and the mods SAFE START or FIND THE PROBLEM MOD has off in this one.</summary>
    private static void AppendSession(StringBuilder text, ModsView view, Func<string, string> nameOf)
    {
        if (view.LastCrash is { } crash)
            text.Append("The last session did not close normally").Append(crash.Count > 0 ? ". The newest errors before it came from " + Names(crash, nameOf) : ", and no mod logged an error before it").Append(".\n");

        var offForPlan = view.StartPlan switch
        {
            PlanView.SafeNow safe => "SAFE START is running. These mods are off for it: " + Names(safe.Off, nameOf),
            PlanView.Round { IsRunning: true } round => "FIND THE PROBLEM MOD is running round " + round.Number + ". These mods are off for it: " + Names(round.Off, nameOf),
            _ => null,
        };

        if (offForPlan != null) text.Append(offForPlan).Append(".\n");
    }

    /// <summary>
    /// Each pair of mods that may get in each other's way, once, from their Harmony patches and from what they reported
    /// through Exiled's Toolkit. Overlaps that can't do harm are left out, as they would only bury the ones that can.
    /// </summary>
    private static void AppendConflicts(StringBuilder text, ModsView view, Func<string, string> nameOf)
    {
        var patchPairs = new HashSet<string>(StringComparer.Ordinal);
        var claimPairs = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>();

        foreach (var mod in view.Mods)
        {
            foreach (var overlap in mod.PatchOverlaps.Where(overlap => overlap.Risk > PatchRisk.Harmless && patchPairs.Add(PairKey(mod.Guid, overlap.Guid))))
            {
                var methods = overlap.Methods.Where(method => method.Risk > PatchRisk.Harmless).ToList();

                lines.Add(nameOf(mod.Guid) + " and " + nameOf(overlap.Guid) + " patch the same methods and " + RiskWords(overlap.Risk) + ": " + Listed(methods.Select(method => method.Name + Why(method, nameOf)).ToList(), ShownPerConflict));
            }

            foreach (var overlap in mod.ClaimClashes.Where(overlap => overlap.Risk > PatchRisk.Harmless && claimPairs.Add(PairKey(mod.Guid, overlap.Guid))))
            {
                var clashes = overlap.Clashes.Where(clash => clash.Risk > PatchRisk.Harmless).ToList();
                var who = overlap.Guid == "" ? nameOf(mod.Guid) : nameOf(mod.Guid) + " and " + nameOf(overlap.Guid) + " " + RiskWords(overlap.Risk);

                lines.Add(who + ": " + Listed(clashes.Select(clash => Said(clash, nameOf(mod.Guid), nameOf(overlap.Guid), nameOf)).ToList(), ShownPerConflict));
            }
        }

        if (lines.Count == 0) return;

        text.Append("Conflicts:\n");

        foreach (var line in lines) text.Append("- ").Append(line).Append('\n');
    }

    /// <summary>What the checks of the game's tables and code found that no mod reported, with the mods that most likely did it.</summary>
    private static void AppendGameChanges(StringBuilder text, ModsView view, Func<string, string> nameOf)
    {
        if (view.DataCheck is DataCheck.Done { Unreported.Count: > 0 } data)
        {
            text.Append("Game data changes no mod reported:\n");

            foreach (var table in data.Unreported.Take(ShownTables))
            {
                var writers = table.Writers.Count > 0 ? "; mods whose code writes this table: " + Names(table.Writers, nameOf) : "; no running mod's code writes this table";

                text.Append("- ").Append(table.Table).Append(": ").Append(Counted(table)).Append(writers).Append('\n');
            }

            if (data.Unreported.Count > ShownTables) text.Append("- and ").Append(data.Unreported.Count - ShownTables).Append(" more tables\n");
        }

        if (view.CodeCheck is CodeCheck.Done { Reading.Unreported.Count: > 0 } code)
        {
            var methods = code.Reading.Unreported.Select(method => method.Method ?? "the method at 0x" + method.Function.ToString("x", CultureInfo.InvariantCulture)).ToList();
            var changers = code.Changers.Count > 0 ? "Mods whose code can change it: " + Names(code.Changers, nameOf) : "No running mod's code changes it";

            text.Append("Game code changed outside Harmony that no mod reported, in ").Append(methods.Count).Append(methods.Count == 1 ? " method: " : " methods: ").Append(Listed(methods, ShownMethods)).Append('\n');
            text.Append(changers).Append(".\n");
        }
    }

    /// <summary>Errors before warnings, each under the mod it came from, with its first stack lines and any clash.</summary>
    private static void AppendErrors(StringBuilder text, ModsView view, Func<string, string> nameOf)
    {
        var errors = view.Mods.SelectMany(mod => mod.Errors.Select(error => (Owner: nameOf(mod.Guid), Error: error)))
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
                var others = string.Join(", ", clash.Guids.Select(nameOf));

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
        var next = mod.Switch is ModSwitch.Uninstalled ? ", uninstalled from the next start" : mod.IsOnNextStart == isOnNow ? "" : mod.IsOnNextStart ? ", on from the next start" : ", off from the next start";

        return OneLine(mod.DisplayName) + " " + mod.Version + " (" + mod.Guid + ", " + mod.FileName + "): " + state + next;
    }

    private static string RiskWords(PatchRisk risk) => risk == PatchRisk.LikelyClash ? "are likely to clash" : "may clash";

    /// <summary>Why a shared method may be trouble, in a few words after its name.</summary>
    private static string Why(SharedMethod method, Func<string, string> nameOf) => method.Reason switch
    {
        ClashReason.BothSetResult => " (both set the result" + (method.LastWord == null ? "" : ", and " + nameOf(method.LastWord) + " runs last") + ")",
        ClashReason.BothRewrite => " (both rewrite the call)",
        ClashReason.SkipDropsRewrite => " (one can skip it, which drops the other's rewrite)",
        ClashReason.BothChangeArgs => " (both change the arguments)",
        ClashReason.Skips => " (one can skip it)",
        ClashReason.Rewrites => " (one rewrites the call)",
        _ => "",
    };

    /// <summary>One clash between two reported changes. Mine and theirs are the two mods it was found between, in that order.</summary>
    private static string Said(ClaimClash clash, string mine, string theirs, Func<string, string> nameOf) => clash.Kind switch
    {
        ClaimKind.SameRow => "both add the row " + clash.Where,
        ClaimKind.SameValue => "both set " + clash.Where + " (" + mine + " to " + clash.Mine + ", " + theirs + " to " + clash.Theirs + (clash.LastWord == null ? "" : ", and " + nameOf(clash.LastWord) + " wrote last") + ")",
        ClaimKind.BothHook => "both put a native hook on " + clash.Where,
        ClaimKind.HookAndPatch => "a native hook and a Harmony patch on " + clash.Where,
        ClaimKind.SameBytes => "both change the same bytes of " + clash.Where,
        ClaimKind.BytesUnderHook => "bytes changed where a hook goes, at the start of " + clash.Where,
        ClaimKind.BothAdd => "both add their own entries to " + clash.Where,
        ClaimKind.SetAfterAdded => LastName(clash, nameOf) + " replaces " + clash.Where + " after " + OtherName(clash, mine, theirs, nameOf) + " added to it, which can drop what " + OtherName(clash, mine, theirs, nameOf) + " added",
        ClaimKind.AddedAfterSet => OtherName(clash, mine, theirs, nameOf) + " sets " + clash.Where + " and " + LastName(clash, nameOf) + " adds to it after that",
        ClaimKind.WrittenOver => clash.Where + " is " + clash.Theirs + " now, not the " + clash.Mine + " it set, and no mod that reports its changes wrote that",
        _ => clash.Kind + " " + clash.Where,
    };

    private static string LastName(ClaimClash clash, Func<string, string> nameOf) => nameOf(clash.LastWord ?? "");

    /// <summary>Of the two mods a clash was found between, the one that did not change it last.</summary>
    private static string OtherName(ClaimClash clash, string mine, string theirs, Func<string, string> nameOf) => LastName(clash, nameOf) == mine ? theirs : mine;

    private static string Counted(TableChanges table)
    {
        var counts = new List<string>();

        if (table.Changed > 0) counts.Add(table.Changed + (table.Changed == 1 ? " value changed" : " values changed"));
        if (table.Added > 0) counts.Add(table.Added + (table.Added == 1 ? " row added" : " rows added"));
        if (table.Removed > 0) counts.Add(table.Removed + (table.Removed == 1 ? " row removed" : " rows removed"));

        return string.Join(", ", counts);
    }

    private static string Listed(IReadOnlyList<string> items, int shown) =>
        string.Join(", ", items.Take(shown)) + (items.Count > shown ? ", and " + (items.Count - shown) + " more" : "");

    private static string Names(IEnumerable<string> guids, Func<string, string> nameOf) => string.Join(", ", guids.Select(nameOf));

    /// <summary>The same for both mods of a pair, so a pair found from each side is listed once.</summary>
    private static string PairKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "\n" + b : b + "\n" + a;

    private static string OneLine(string text) => string.Join(" ", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()));
}
