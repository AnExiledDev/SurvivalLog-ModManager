using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>
/// How a game session ended. The Mod Manager writes "running" when it loads and "closed" when the game quits
/// normally, so a start that finds "running" knows the last session crashed or was killed.
/// </summary>
/// <param name="Round">The FIND THE PROBLEM MOD round whose mods the session ran, so a crash counts against that round only.</param>
/// <param name="ErrorMods">The mods behind the newest errors of the session, by GUID, newest first.</param>
public sealed record SessionMark(bool IsClosed, string? Round, IReadOnlyList<string> ErrorMods)
{
    private const int MaxErrorMods = 3;

    public static SessionMark Running(string? round) => new(false, round, Array.Empty<string>());

    public string ToText()
    {
        var lines = new List<string> { IsClosed ? "closed" : "running" };

        if (Round != null) lines.Add("round " + PlanText.Escape(Round));

        lines.AddRange(ErrorMods.Select(guid => "error " + PlanText.Escape(guid)));

        return string.Join("\n", lines) + "\n";
    }

    /// <summary>Null when there is no mark yet, or it cannot be read; neither counts as a crash.</summary>
    public static SessionMark? Parse(string? text)
    {
        var lines = PlanText.Lines(text);

        if (lines.Count == 0 || lines[0].Key is not ("closed" or "running") || lines[0].Values.Count > 0) return null;

        var round = lines.FirstOrDefault(line => line.Key == "round").Values?.FirstOrDefault();
        var errorMods = lines.Where(line => line.Key == "error").SelectMany(line => line.Values).ToList();

        return new SessionMark(lines[0].Key == "closed", round, errorMods);
    }

    /// <summary>The mods whose code logged the newest errors, newest first, at most three.</summary>
    public static IReadOnlyList<string> ErrorModsOf(IEnumerable<Problem> newestFirst, IReadOnlyList<ProblemOwner> owners) =>
        newestFirst
            .Where(problem => problem.Level == ProblemLevel.Error)
            .Select(problem => ProblemOwners.OwnerOf(problem, owners))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MaxErrorMods)
            .ToList();
}

/// <summary>What the Mod Manager has set up for the next game starts. Saved between starts, since each step needs a restart.</summary>
public abstract record StartPlan
{
    private StartPlan()
    {
    }

    /// <summary>One start with every mod the player can turn off turned off. They are turned back on during that start, for the one after.</summary>
    public sealed record Safe(IReadOnlyList<string> Off) : StartPlan;

    /// <summary>
    /// One round of FIND THE PROBLEM MOD. The kept mods and the suspects that are not off run this round. The kept
    /// mods together with every suspect make the game crash, so the mod to blame is a suspect.
    /// </summary>
    /// <param name="Id">The same for every round of one search, so a crash from another search never counts.</param>
    /// <param name="Original">The mods that were on when the search began. They are all on again when it ends, except the one it found.</param>
    /// <param name="KeptWorks">An earlier round ran the kept mods without the suspects, and the game did not crash.</param>
    /// <param name="Nodes">What each original mod needs and which DLL it is in, so a mod is never turned off without the mods that need it.</param>
    public sealed record Search(string Id, int Round, IReadOnlyList<string> Original, IReadOnlyList<string> Kept, IReadOnlyList<string> Suspects, IReadOnlyList<string> Off, bool KeptWorks, IReadOnlyList<PluginNode> Nodes) : StartPlan
    {
        /// <summary>The DLLs are already where the next round needs them if this one crashes, so a crash goes straight on to it.</summary>
        public bool IsArmed { get; init; }

        public string Token => Id + "/" + Round;

        /// <summary>Every suspect is off, to see whether the game crashes without any of them.</summary>
        public bool IsTest => Off.Count == Suspects.Count;

        public bool IsOn(string guid) => Kept.Contains(guid) || (Suspects.Contains(guid) && !Off.Contains(guid));
    }

    /// <summary>The search found the mod to blame. It is off from now on, with the mods that cannot start without it.</summary>
    /// <param name="Culprit">One mod, or several that share a DLL and so can only be turned off together.</param>
    public sealed record Found(IReadOnlyList<string> Culprit, IReadOnlyList<string> AlsoOff) : StartPlan;

    /// <summary>The game crashed even with every suspect off, so no mod the search could turn off is to blame.</summary>
    public sealed record NotFound : StartPlan;
}

public enum StartRefusal
{
    /// <summary>No mod that runs can be turned off: the only ones on are the Mod Manager and the mods it needs.</summary>
    NothingToTurnOff,

    /// <summary>An update or rollback replaces a mod's files at the next start, so its DLL cannot be moved now.</summary>
    FilesWaiting,
}

/// <summary>What the Mod Manager does on the first frame of a start, once every plugin has loaded and DLLs can be moved.</summary>
public enum AtFirstFrame
{
    Nothing,

    /// <summary>This start runs the round: set the DLLs up for the round after a crash, so a crash goes straight on to it.</summary>
    Arm,

    /// <summary>The DLLs were set up for a crash, but the game quit without one and could not put them back. Set the round up again.</summary>
    SetRoundAgain,

    /// <summary>The round crashed before its DLLs could be set up for the next one. Set the next one up now.</summary>
    GoOnAfterCrash,

    /// <summary>This is the safe start: turn its mods back on for the start after.</summary>
    EndSafeStart,
}

/// <summary>What this start is, worked out when the Mod Manager loads, from how the last session ended and the plan it left.</summary>
/// <param name="Plan">The plan from now on.</param>
/// <param name="Loaded">The round whose mods this session runs, for its mark.</param>
/// <param name="Answers">The round the player can say how it went in this session, or null.</param>
/// <param name="HasRoundCrashed">The last session crashed while it ran a round.</param>
/// <param name="LastCrash">The mods behind the newest errors of the last session, when it crashed with no search running; null when it did not crash.</param>
/// <param name="IsPlanChanged">The plan moved on at load and has to be saved.</param>
public sealed record StartRead(StartPlan? Plan, string? Loaded, string? Answers, bool HasRoundCrashed, IReadOnlyList<string>? LastCrash, bool IsPlanChanged, AtFirstFrame Then);

/// <summary>
/// SAFE START and FIND THE PROBLEM MOD. The search halves the suspects each round: a crash means the mod to blame is
/// among those still on, no crash means it is among those turned off. A mod and the mods that need it are always
/// turned off together, so every mod that should run can.
/// </summary>
public static class CulpritSearch
{
    public static StartRefusal? WhyNot(IReadOnlyList<ModEntry> mods)
    {
        if (mods.Any(mod => mod.Switch is ModSwitch.Held)) return StartRefusal.FilesWaiting;

        return Suspects(mods).Count == 0 ? StartRefusal.NothingToTurnOff : null;
    }

    /// <summary>The mods on for the next start that can be switched and that BepInEx starts. One that failed to load cannot cause a crash.</summary>
    public static IReadOnlyList<ModEntry> Suspects(IReadOnlyList<ModEntry> mods) =>
        mods.Where(mod => mod.Switch is ModSwitch.Allowed { IsOnNextStart: true } && mod.State is not ModState.NotLoaded)
            .GroupBy(mod => mod.Guid, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

    public static StartPlan.Safe SafeStart(IReadOnlyList<ModEntry> mods) => new(Suspects(mods).Select(mod => mod.Guid).ToList());

    public static StartPlan.Search Begin(string id, IReadOnlyList<ModEntry> mods)
    {
        var suspects = Suspects(mods);
        var guids = suspects.Select(mod => mod.Guid).ToList();
        var nodes = suspects.Select(mod => new PluginNode(mod.Guid, mod.DisplayName, mod.FileName, mod.Relations.Where(relation => relation.Kind == RelationKind.Needs).Select(relation => relation.Guid).ToList())).ToList();
        var start = new StartPlan.Search(id, 0, guids, Array.Empty<string>(), guids, Array.Empty<string>(), false, nodes);
        var off = Split(guids, nodes);

        return RoundOf(start, Array.Empty<string>(), guids, off.Count > 0 ? off : guids, false);
    }

    /// <summary>What comes after the round, once the player has said whether the game crashed (or broke some other way).</summary>
    public static StartPlan After(StartPlan.Search round, bool hasCrashed)
    {
        if (round.IsTest) return hasCrashed ? new StartPlan.NotFound() : Found(round.Suspects, round);

        var stillOn = round.Suspects.Where(guid => !round.Off.Contains(guid)).ToList();

        return hasCrashed
            ? Next(round, round.Kept, stillOn, round.KeptWorks)
            : Next(round, round.Kept.Concat(stillOn).ToList(), round.Off, true);
    }

    /// <summary>
    /// Which of the search's mods are on for what follows the round: the next round's mods, every mod but the one
    /// found, or every one of them when the search ends without a culprit or is stopped.
    /// </summary>
    public static IReadOnlyDictionary<string, bool> Layout(StartPlan.Search from, StartPlan? next) =>
        from.Original.ToDictionary(guid => guid, guid => next switch
        {
            StartPlan.Search round => round.IsOn(guid),
            StartPlan.Found found => !found.Culprit.Contains(guid) && !found.AlsoOff.Contains(guid),
            _ => true,
        }, StringComparer.Ordinal);

    public static StartRead AtLoad(SessionMark? last, StartPlan? plan)
    {
        var crashedRound = last is { IsClosed: false } crash ? crash.Round : null;
        var hasCrashed = last is { IsClosed: false };

        switch (plan)
        {
            case StartPlan.Search { IsArmed: true } armed when crashedRound == armed.Token:
                var next = After(armed, true);
                var token = next is StartPlan.Search round ? round.Token : null;

                return new StartRead(next, token, token, true, null, true, token != null ? AtFirstFrame.Arm : AtFirstFrame.Nothing);
            case StartPlan.Search { IsArmed: true } armed:
                return new StartRead(armed, null, null, false, null, false, AtFirstFrame.SetRoundAgain);
            case StartPlan.Search search when crashedRound == search.Token:
                return new StartRead(search, search.Token, null, true, null, false, AtFirstFrame.GoOnAfterCrash);
            case StartPlan.Search search:
                return new StartRead(search, search.Token, search.Token, false, null, false, AtFirstFrame.Arm);
            case StartPlan.Safe:
                return new StartRead(plan, null, null, false, null, false, AtFirstFrame.EndSafeStart);
            default:
                return new StartRead(plan, null, null, false, hasCrashed ? last?.ErrorMods ?? Array.Empty<string>() : null, false, AtFirstFrame.Nothing);
        }
    }

    public static PlanView ViewOf(StartPlan? plan, string? answers, bool hasRoundCrashed) => plan switch
    {
        StartPlan.Safe safe => new PlanView.SafeSet(safe.Off),
        StartPlan.Search search => new PlanView.Round(search.Round, search.Original.Where(guid => !search.IsOn(guid)).ToList(), search.Suspects.Count, search.IsTest, answers == search.Token, hasRoundCrashed),
        StartPlan.Found found => new PlanView.Found(found.Culprit, found.AlsoOff),
        StartPlan.NotFound => new PlanView.NotFound(),
        _ => new PlanView.None(),
    };

    private static StartPlan Next(StartPlan.Search last, IReadOnlyList<string> kept, IReadOnlyList<string> suspects, bool keptWorks)
    {
        var off = Split(suspects, last.Nodes);

        // The kept mods run fine without the suspects and crash with them, and the suspects cannot be split any more.
        if (off.Count == 0 && keptWorks) return Found(suspects, last);

        return RoundOf(last, kept, suspects, off.Count > 0 ? off : suspects, keptWorks);
    }

    private static StartPlan.Search RoundOf(StartPlan.Search last, IReadOnlyList<string> kept, IReadOnlyList<string> suspects, IReadOnlyList<string> off, bool keptWorks) =>
        last with { Round = last.Round + 1, Kept = kept, Suspects = suspects, Off = off, KeptWorks = keptWorks, IsArmed = false };

    /// <summary>
    /// About half of the suspects to turn off, taken as whole groups: a mod with every suspect that cannot start
    /// without it. Empty when the suspects are one group, which cannot be split.
    /// </summary>
    private static IReadOnlyList<string> Split(IReadOnlyList<string> suspects, IReadOnlyList<PluginNode> nodes)
    {
        var inSearch = nodes.Where(node => suspects.Contains(node.Guid)).ToList();
        var groups = suspects
            .Select(guid => (Guid: guid, Group: GroupOf(guid, inSearch)))
            .OrderBy(candidate => candidate.Group.Count)
            .ThenBy(candidate => NameOf(candidate.Guid, inSearch), StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Guid, StringComparer.Ordinal)
            .Select(candidate => candidate.Group);
        var target = Math.Max(1, suspects.Count / 2);
        var off = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in groups)
        {
            if (off.Count >= target) break;

            var joined = off.Union(group).ToHashSet(StringComparer.Ordinal);

            if (joined.Count == suspects.Count) continue;

            if (off.Count == 0 || joined.Count <= target) off = joined;
        }

        return suspects.Where(off.Contains).ToList();
    }

    private static IReadOnlyList<string> GroupOf(string guid, IReadOnlyList<PluginNode> nodes) =>
        NodeOf(guid, nodes) is { } node ? SwitchPlanner.StopsWith(node, nodes).Select(other => other.Guid).Prepend(guid).ToList() : new[] { guid };

    private static StartPlan.Found Found(IReadOnlyList<string> culprit, StartPlan.Search search)
    {
        var stopped = culprit.SelectMany(guid => GroupOf(guid, search.Nodes)).ToHashSet(StringComparer.Ordinal);

        return new StartPlan.Found(culprit, search.Original.Where(guid => stopped.Contains(guid) && !culprit.Contains(guid)).ToList());
    }

    private static PluginNode? NodeOf(string guid, IReadOnlyList<PluginNode> nodes) => nodes.FirstOrDefault(node => node.Guid == guid);

    private static string NameOf(string guid, IReadOnlyList<PluginNode> nodes) => NodeOf(guid, nodes)?.Name ?? guid;
}

/// <summary>The start plan as the Mods page shows it, in the bar at the bottom.</summary>
public abstract record PlanView
{
    private PlanView()
    {
    }

    public sealed record None : PlanView;

    /// <summary>SAFE START is set up for the next start.</summary>
    public sealed record SafeSet(IReadOnlyList<string> Off) : PlanView;

    /// <summary>This is the safe start. The mods it turned off are already back on for the next start.</summary>
    public sealed record SafeNow(IReadOnlyList<string> Off) : PlanView;

    /// <param name="Off">Every mod the round has off, by GUID.</param>
    /// <param name="Suspects">How many mods could still be the one to blame.</param>
    /// <param name="IsRunning">This session runs the round, so the player can say how it went.</param>
    /// <param name="HasCrashed">The last session crashed while it ran the round before this one.</param>
    public sealed record Round(int Number, IReadOnlyList<string> Off, int Suspects, bool IsTest, bool IsRunning, bool HasCrashed) : PlanView;

    public sealed record Found(IReadOnlyList<string> Culprit, IReadOnlyList<string> AlsoOff) : PlanView;

    public sealed record NotFound : PlanView;

    /// <summary>The player stopped the search or the safe start, and every mod is back as it was.</summary>
    public sealed record Stopped(bool WasSearch) : PlanView;
}

/// <summary>
/// The plan file: its kind on the first line, then one "key value value..." line each. Every value is URI-escaped,
/// so a GUID or file name with spaces stays one value.
/// </summary>
public static class PlanText
{
    public static string Write(StartPlan plan)
    {
        var lines = plan switch
        {
            StartPlan.Safe safe => new[] { "safe", Line("off", safe.Off) },
            StartPlan.Search search => new[]
            {
                "search",
                Line("id", new[] { search.Id }),
                Line("round", new[] { search.Round.ToString(System.Globalization.CultureInfo.InvariantCulture) }),
                Line("armed", new[] { search.IsArmed ? "1" : "0" }),
                Line("keptWorks", new[] { search.KeptWorks ? "1" : "0" }),
                Line("original", search.Original),
                Line("kept", search.Kept),
                Line("suspects", search.Suspects),
                Line("off", search.Off),
            }.Concat(search.Nodes.Select(node => Line("node", new[] { node.Guid, node.File, node.Name }.Concat(node.HardDependencies).ToList()))).ToArray(),
            StartPlan.Found found => new[] { "found", Line("culprit", found.Culprit), Line("alsoOff", found.AlsoOff) },
            StartPlan.NotFound => new[] { "notfound" },
            _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, null),
        };

        return string.Join("\n", lines) + "\n";
    }

    /// <summary>Null for an empty file, and for one that is not a whole, sensible plan.</summary>
    public static StartPlan? Read(string? text)
    {
        var lines = Lines(text);

        if (lines.Count == 0) return null;

        var values = lines.Skip(1).GroupBy(line => line.Key).ToDictionary(group => group.Key, group => group.First().Values);
        IReadOnlyList<string> List(string key) => values.TryGetValue(key, out var list) ? list : Array.Empty<string>();

        return lines[0].Key switch
        {
            "safe" when List("off").Count > 0 => new StartPlan.Safe(List("off")),
            "search" => ReadSearch(List, lines.Where(line => line.Key == "node").Select(line => line.Values).ToList()),
            "found" when List("culprit").Count > 0 => new StartPlan.Found(List("culprit"), List("alsoOff")),
            "notfound" => new StartPlan.NotFound(),
            _ => null,
        };
    }

    internal static string Escape(string value) => Uri.EscapeDataString(value);

    internal static IReadOnlyList<(string Key, IReadOnlyList<string> Values)> Lines(string? text) =>
        (text ?? "").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(parts => (parts[0], (IReadOnlyList<string>)parts.Skip(1).Select(Uri.UnescapeDataString).ToList()))
            .ToList();

    private static string Line(string key, IReadOnlyList<string> values) => values.Count == 0 ? key : key + " " + string.Join(" ", values.Select(Escape));

    private static StartPlan.Search? ReadSearch(Func<string, IReadOnlyList<string>> list, IReadOnlyList<IReadOnlyList<string>> nodeLines)
    {
        var id = list("id").FirstOrDefault();
        var hasRound = int.TryParse(list("round").FirstOrDefault(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var round);
        var (original, kept, suspects, off) = (list("original"), list("kept"), list("suspects"), list("off"));
        var isWhole = !string.IsNullOrEmpty(id) && hasRound && round > 0 && suspects.Count > 0 && off.Count > 0
            && off.All(suspects.Contains) && !kept.Any(suspects.Contains) && kept.Concat(suspects).All(original.Contains);

        if (!isWhole || id == null) return null;

        var nodes = nodeLines.Where(parts => parts.Count >= 3).Select(parts => new PluginNode(parts[0], parts[2], parts[1], parts.Skip(3).ToList())).ToList();

        return new StartPlan.Search(id, round, original, kept, suspects, off, list("keptWorks").FirstOrDefault() == "1", nodes) { IsArmed = list("armed").FirstOrDefault() == "1" };
    }
}
