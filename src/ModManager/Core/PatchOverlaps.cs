using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>What one Harmony patch does to the game method it patches.</summary>
public enum PatchEffect
{
    /// <summary>A prefix that cannot stop the method: it only adds code before it.</summary>
    RunsBefore,

    /// <summary>A postfix or finalizer: it only adds code after the method.</summary>
    RunsAfter,

    /// <summary>A prefix that can stop the game's own code from running (returns bool or sets __runOriginal).</summary>
    CanSkip,

    /// <summary>A transpiler or IL manipulator: it rewrites the method's code.</summary>
    Rewrites,
}

/// <summary>How likely two mods patching one method are to get in each other's way. Higher is worse.</summary>
public enum PatchRisk
{
    /// <summary>Both only add code before or after the method.</summary>
    Harmless,

    /// <summary>One can skip or rewrite the method, the other adds to it.</summary>
    MayClash,

    /// <summary>Both can skip or rewrite the method, so one may undo the other.</summary>
    LikelyClash,
}

/// <summary>One mod's patch on a game method.</summary>
public sealed record PatchUse(string Guid, PatchEffect Effect);

/// <summary>One game method, named as "Type.Method", and every plugin patch on it.</summary>
public sealed record PatchedMethod(string Method, IReadOnlyList<PatchUse> Uses);

/// <summary>A method two mods both patch, what each does to it, and the risk that follows.</summary>
public sealed record SharedMethod(string Method, PatchRisk Risk, IReadOnlyList<PatchEffect> Mine, IReadOnlyList<PatchEffect> Theirs)
{
    public bool Equals(SharedMethod? other) =>
        other != null && Method == other.Method && Risk == other.Risk && Mine.SequenceEqual(other.Mine) && Theirs.SequenceEqual(other.Theirs);

    public override int GetHashCode() => Method.GetHashCode();
}

/// <summary>Another mod that patches some of the same game methods. Its risk is the worst of the shared methods.</summary>
public sealed record PatchOverlap(string Guid, PatchRisk Risk, IReadOnlyList<SharedMethod> Methods)
{
    public bool Equals(PatchOverlap? other) => other != null && Guid == other.Guid && Risk == other.Risk && Methods.SequenceEqual(other.Methods);

    public override int GetHashCode() => Guid.GetHashCode();
}

/// <summary>
/// Which mods change the same game code, and whether that is likely to matter. Adding code before or after a method
/// rarely clashes; two mods that can each skip or rewrite it often do. This is a guide for where to look, not proof.
/// </summary>
public static class PatchOverlaps
{
    /// <summary>For each mod, the other mods sharing a method with it: riskiest first, then most shared methods.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<PatchOverlap>> Find(IEnumerable<PatchedMethod> methods)
    {
        var effects = EffectsByMethod(methods);
        var shared = new Dictionary<string, Dictionary<string, List<SharedMethod>>>(StringComparer.Ordinal);

        foreach (var (method, byMod) in effects)
        {
            foreach (var (guid, mine) in byMod)
            {
                foreach (var (other, theirs) in byMod.Where(pair => pair.Key != guid))
                {
                    MethodsShared(shared, guid, other).Add(new SharedMethod(method, RiskOf(mine, theirs), mine, theirs));
                }
            }
        }

        return shared.ToDictionary(pair => pair.Key, pair => Overlaps(pair.Value), StringComparer.Ordinal);
    }

    public static PatchRisk RiskOf(IReadOnlyCollection<PatchEffect> mine, IReadOnlyCollection<PatchEffect> theirs)
    {
        var changes = (mine.Any(Changes) ? 1 : 0) + (theirs.Any(Changes) ? 1 : 0);

        return changes switch
        {
            0 => PatchRisk.Harmless,
            1 => PatchRisk.MayClash,
            _ => PatchRisk.LikelyClash,
        };
    }

    private static bool Changes(PatchEffect effect) => effect is PatchEffect.CanSkip or PatchEffect.Rewrites;

    /// <summary>Overloads share a name, so their patches are merged under it; each mod's effects come once, in enum order.</summary>
    private static SortedDictionary<string, SortedDictionary<string, IReadOnlyList<PatchEffect>>> EffectsByMethod(IEnumerable<PatchedMethod> methods) =>
        new(methods
            .GroupBy(method => method.Method, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new SortedDictionary<string, IReadOnlyList<PatchEffect>>(group
                    .SelectMany(method => method.Uses)
                    .GroupBy(use => use.Guid, StringComparer.Ordinal)
                    .ToDictionary(uses => uses.Key, uses => (IReadOnlyList<PatchEffect>)uses.Select(use => use.Effect).Distinct().OrderBy(effect => effect).ToList()), StringComparer.Ordinal)),
            StringComparer.Ordinal);

    private static List<SharedMethod> MethodsShared(Dictionary<string, Dictionary<string, List<SharedMethod>>> shared, string guid, string other)
    {
        if (!shared.TryGetValue(guid, out var others)) shared[guid] = others = new Dictionary<string, List<SharedMethod>>(StringComparer.Ordinal);

        if (!others.TryGetValue(other, out var methods)) others[other] = methods = new List<SharedMethod>();

        return methods;
    }

    private static IReadOnlyList<PatchOverlap> Overlaps(Dictionary<string, List<SharedMethod>> others) =>
        others
            .Select(pair => new PatchOverlap(pair.Key, pair.Value.Max(method => method.Risk), pair.Value.OrderByDescending(method => method.Risk).ThenBy(method => method.Method, StringComparer.Ordinal).ToList()))
            .OrderByDescending(overlap => overlap.Risk)
            .ThenByDescending(overlap => overlap.Methods.Count)
            .ThenBy(overlap => overlap.Guid, StringComparer.Ordinal)
            .ToList();
}
