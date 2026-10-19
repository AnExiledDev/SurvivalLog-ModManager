using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

public enum ProblemLevel
{
    Warning,
    Error,
}

/// <summary>A warning or error from the BepInEx log: the source that logged it and its whole text, stack included.</summary>
public sealed record LoggedEvent(ProblemLevel Level, string Source, string Text);

/// <summary>
/// One distinct problem seen this session. Repeats of it only raise the count. Methods are the methods its stack goes
/// through, as "Type.Method", the way the CONFLICTS tab names a patched game method.
/// </summary>
public sealed record Problem(ProblemLevel Level, string Source, string Message, IReadOnlyList<string> Stack, IReadOnlyList<string> Methods, int Count, int LastSeen);

/// <summary>A mod BepInEx loaded, as the problem list needs it: the names its log source and its code go by.</summary>
public sealed record ProblemOwner(string Guid, string Name, IReadOnlyList<string> CodeRoots, bool IsLibrary);

/// <summary>A game method the problem's stack goes through that other mods patch too.</summary>
public sealed record ClashHint(string Method, IReadOnlyList<string> Guids);

/// <summary>A problem as one mod's ERRORS tab shows it.</summary>
public sealed record ModProblem(Problem Problem, IReadOnlyList<ClashHint> Clashes);

/// <summary>
/// Every distinct warning and error this session, newest first. Kept small: a problem that repeats every frame is one
/// entry with a count, and past the cap new kinds are only counted.
/// </summary>
public sealed class ProblemBook
{
    public const int MaxProblems = 200;
    public const int MaxMessageLength = 1500;
    public const int MaxStackLines = 12;

    private readonly Dictionary<string, int> _indexByKey = new(StringComparer.Ordinal);
    private readonly List<Problem> _problems = new();
    private int _seen;

    /// <summary>Goes up with every event kept, so a screen that is open knows when to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>New kinds of problem seen after the cap was reached.</summary>
    public int Dropped { get; private set; }

    public IReadOnlyList<Problem> All() => _problems.OrderByDescending(problem => problem.LastSeen).ToList();

    public void Add(LoggedEvent logged)
    {
        if (IsNoise(logged)) return;

        var (message, stack) = Split(logged.Text);
        var key = logged.Level + "\n" + logged.Source + "\n" + message + "\n" + (stack.Count > 0 ? stack[0] : "");

        _seen++;
        Version++;

        if (_indexByKey.TryGetValue(key, out var index))
        {
            _problems[index] = _problems[index] with { Count = _problems[index].Count + 1, LastSeen = _seen };

            return;
        }

        if (_problems.Count >= MaxProblems)
        {
            Dropped++;

            return;
        }

        _indexByKey[key] = _problems.Count;
        _problems.Add(new Problem(logged.Level, logged.Source, message, stack, Frames.Methods(stack), 1, _seen));
    }

    /// <summary>Warnings every modded game logs and nothing can act on.</summary>
    public static bool IsNoise(LoggedEvent logged) =>
        logged.Text.StartsWith("Class::Init signatures have been exhausted", StringComparison.Ordinal) ||
        logged.Source == "Il2CppInterop" && logged.Level == ProblemLevel.Warning && logged.Text.Contains(" has unsupported parameter ", StringComparison.Ordinal) ||
        logged.Source == "HarmonyX" && logged.Text.StartsWith("AccessTools.GetTypesFromAssembly", StringComparison.Ordinal);

    /// <summary>The text without its stack, cut to length, and the first stack lines.</summary>
    public static (string Message, IReadOnlyList<string> Stack) Split(string text)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith("---", StringComparison.Ordinal)).ToList();
        var stack = lines.Where(IsFrame).Take(MaxStackLines).ToList();
        var message = string.Join("\n", lines.Where(line => !IsFrame(line)));

        if (message.Length > MaxMessageLength) message = message.Substring(0, MaxMessageLength) + "...";

        return (message, stack);
    }

    private static bool IsFrame(string line) => line.StartsWith("at ", StringComparison.Ordinal);
}

/// <summary>Reads .NET stack frames: "at Ns.Type.Method(args)", and HarmonyX's patched copies, "at DMD&lt;Ns.Type::Method&gt;(args)".</summary>
public static class Frames
{
    /// <summary>The full type name a frame runs in, or null when the line is not a frame it can read.</summary>
    public static string? TypeOf(string frame) => Parts(frame)?.Type;

    /// <summary>Each method the stack goes through, once, as "Type.Method" with the short type name.</summary>
    public static IReadOnlyList<string> Methods(IEnumerable<string> stack) =>
        stack.Select(Parts).OfType<(string Type, string Method)>().Select(parts => ShortType(parts.Type) + "." + parts.Method).Distinct().ToList();

    private static (string Type, string Method)? Parts(string frame)
    {
        var text = frame.StartsWith("at ", StringComparison.Ordinal) ? frame.Substring(3) : frame;
        var dmd = text.IndexOf("DMD<", StringComparison.Ordinal);

        if (dmd >= 0)
        {
            var end = text.IndexOf('>', dmd);
            var inner = end < 0 ? "" : text.Substring(dmd + 4, end - dmd - 4);
            var colons = inner.LastIndexOf("::", StringComparison.Ordinal);

            return colons > 0 ? (inner.Substring(0, colons), inner.Substring(colons + 2)) : null;
        }

        var call = text.IndexOf('(');
        var name = (call < 0 ? text : text.Substring(0, call)).TrimEnd();
        var dot = name.LastIndexOf('.');

        return dot > 0 && dot < name.Length - 1 ? (name.Substring(0, dot), name.Substring(dot + 1)) : null;
    }

    private static string ShortType(string type)
    {
        var generic = type.IndexOf('`');
        var plain = generic < 0 ? type : type.Substring(0, generic);

        return plain.Substring(Math.Max(plain.LastIndexOf('.'), plain.LastIndexOf('+')) + 1);
    }
}

/// <summary>
/// Which mod each problem belongs to, and whether it went through a game method another mod patches too, which is
/// evidence of a real clash rather than the CONFLICTS tab's guess.
/// </summary>
public static class ProblemOwners
{
    /// <summary>Namespaces that belong to the game, BepInEx or .NET, never to a mod, even when a mod is named after one.</summary>
    private static readonly HashSet<string> SharedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Microsoft", "Mono", "MonoMod", "HarmonyLib", "BepInEx", "Il2CppInterop", "Il2Cpp", "Il2CppSystem", "UnityEngine", "Unity", "GameCore", "Cysharp",
    };

    /// <param name="overlaps">Each mod's shared patches, by GUID, as PatchOverlaps.Find gives them.</param>
    public static (IReadOnlyDictionary<string, IReadOnlyList<ModProblem>> ByMod, IReadOnlyList<ModProblem> Unclaimed) Assign(
        IEnumerable<Problem> problems, IReadOnlyList<ProblemOwner> owners, IReadOnlyDictionary<string, IReadOnlyList<PatchOverlap>> overlaps)
    {
        var byMod = new Dictionary<string, List<ModProblem>>(StringComparer.Ordinal);
        var unclaimed = new List<ModProblem>();

        foreach (var problem in problems)
        {
            var owner = OwnerOf(problem, owners);
            var claimants = owner != null ? new[] { owner } : overlaps.Where(pair => ClashesOf(problem, pair.Value).Count > 0).Select(pair => pair.Key).ToArray();

            if (claimants.Length == 0) unclaimed.Add(new ModProblem(problem, Array.Empty<ClashHint>()));

            foreach (var guid in claimants)
            {
                var clashes = overlaps.TryGetValue(guid, out var shared) ? ClashesOf(problem, shared) : Array.Empty<ClashHint>();

                if (!byMod.TryGetValue(guid, out var list)) byMod[guid] = list = new List<ModProblem>();

                list.Add(new ModProblem(problem, clashes));
            }
        }

        return (byMod.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ModProblem>)pair.Value), unclaimed);
    }

    /// <summary>
    /// By the log source first, since most mods log under their own name; then by the first stack frame in a mod's
    /// code, passing over a library when a mod that uses it is further down; then by a mod's code named in the text.
    /// </summary>
    public static string? OwnerOf(Problem problem, IReadOnlyList<ProblemOwner> owners)
    {
        var bySource = owners.FirstOrDefault(owner => string.Equals(owner.Name, problem.Source, StringComparison.OrdinalIgnoreCase) || string.Equals(owner.Guid, problem.Source, StringComparison.OrdinalIgnoreCase));

        if (bySource != null) return bySource.Guid;

        var inStack = problem.Stack.Select(Frames.TypeOf).OfType<string>().Select(type => owners.FirstOrDefault(owner => Roots(owner).Any(root => type.StartsWith(root + ".", StringComparison.Ordinal)))).OfType<ProblemOwner>().ToList();
        var byStack = inStack.FirstOrDefault(owner => !owner.IsLibrary) ?? inStack.FirstOrDefault();

        if (byStack != null) return byStack.Guid;

        return owners.FirstOrDefault(owner => Roots(owner).Any(root => Names(problem.Message, root + ".")))?.Guid;
    }

    private static IEnumerable<string> Roots(ProblemOwner owner) => owner.CodeRoots.Where(root => root.Length > 0 && !SharedRoots.Contains(root));

    /// <summary>"root." where it starts a name, so "Trapline." is not found inside "MyTrapline.".</summary>
    private static bool Names(string text, string prefix)
    {
        for (var at = text.IndexOf(prefix, StringComparison.Ordinal); at >= 0; at = text.IndexOf(prefix, at + 1, StringComparison.Ordinal))
        {
            if (at == 0 || !char.IsLetterOrDigit(text[at - 1]) && text[at - 1] != '.' && text[at - 1] != '_') return true;
        }

        return false;
    }

    private static IReadOnlyList<ClashHint> ClashesOf(Problem problem, IReadOnlyList<PatchOverlap> overlaps) =>
        overlaps.SelectMany(overlap => overlap.Methods.Where(method => problem.Methods.Contains(method.Method)).Select(method => (method.Method, overlap.Guid)))
            .GroupBy(pair => pair.Method)
            .Select(group => new ClashHint(group.Key, group.Select(pair => pair.Guid).Distinct().ToList()))
            .ToList();
}

/// <summary>Reads the warnings and errors logged before the Mod Manager could listen, from the text of LogOutput.log.</summary>
public static class LogFile
{
    /// <summary>
    /// Each Warning, Error or Fatal entry before the first line that holds <paramref name="stopAt"/>, with the
    /// lines that follow it (a stack, an exception) as part of its text. Null when that line is not in the file yet.
    /// </summary>
    public static IReadOnlyList<LoggedEvent>? Before(string text, string stopAt)
    {
        var events = new List<LoggedEvent>();
        (ProblemLevel Level, string Source, List<string> Lines)? open = null;

        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.Contains(stopAt, StringComparison.Ordinal))
            {
                Close();

                return events;
            }

            if (!raw.StartsWith("[", StringComparison.Ordinal) || Header(raw) is not { } header)
            {
                open?.Lines.Add(raw);

                continue;
            }

            Close();
            open = header.Level is { } level ? (level, header.Source, new List<string> { header.Text }) : null;
        }

        return null;

        void Close()
        {
            if (open is { } done) events.Add(new LoggedEvent(done.Level, done.Source, string.Join("\n", done.Lines)));

            open = null;
        }
    }

    /// <summary>"[Error  :   Source] text": the level (null for one not kept), the source and the text.</summary>
    private static (ProblemLevel? Level, string Source, string Text)? Header(string line)
    {
        var colon = line.IndexOf(':');
        var close = line.IndexOf(']');

        if (colon < 0 || close < colon) return null;

        var name = line.Substring(1, colon - 1).Trim();

        if (name is not ("Warning" or "Error" or "Fatal" or "Info" or "Message" or "Debug")) return null;

        ProblemLevel? level = name switch
        {
            "Warning" => ProblemLevel.Warning,
            "Error" or "Fatal" => ProblemLevel.Error,
            _ => null,
        };

        return (level, line.Substring(colon + 1, close - colon - 1).Trim(), line.Substring(close + 1).TrimStart());
    }
}
