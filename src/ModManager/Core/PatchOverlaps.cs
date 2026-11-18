using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>What one Harmony patch does to the game method it patches. A patch can do more than one of these.</summary>
public enum PatchEffect
{
    /// <summary>A prefix that changes nothing the game sees: it only adds code before the method.</summary>
    RunsBefore,

    /// <summary>A postfix or finalizer that changes nothing the game sees: it only adds code after the method.</summary>
    RunsAfter,

    /// <summary>Takes one of the method's arguments by ref, so it can change what the game's code is given.</summary>
    ChangesArgs,

    /// <summary>Takes __result by ref, so it can change what the method gives back.</summary>
    SetsResult,

    /// <summary>A prefix that can stop the game's code from running (returns bool or sets __runOriginal).</summary>
    CanSkip,

    /// <summary>
    /// A transpiler or IL manipulator. On this game that only changes the call into the game's code (Il2CppInterop
    /// hands Harmony a wrapper that calls the native method), never the game's logic.
    /// </summary>
    Rewrites,
}

/// <summary>How likely two mods patching one method are to get in each other's way. Higher is worse.</summary>
public enum PatchRisk
{
    /// <summary>Neither can undo or hide what the other does.</summary>
    Harmless,

    /// <summary>
    /// One can skip the method, both change its arguments, or one rewrites the call. Skipping is how most mods change
    /// a game rule, and two that skip usually skip in different cases, so this alone is not counted as likely.
    /// </summary>
    MayClash,

    /// <summary>One mod's change is lost: both set the result, both rewrite the call, or one skips past the other's rewrite.</summary>
    LikelyClash,
}

/// <summary>Why a shared method got its risk, so the page can say what happens and not only how bad it is.</summary>
public enum ClashReason
{
    None,

    /// <summary>Both change what the method gives back. Only the one that runs last counts.</summary>
    BothSetResult,

    /// <summary>Both rewrite the call into the game's code, so the second may not find what it looks for.</summary>
    BothRewrite,

    /// <summary>One can skip the method, and when it does the other's rewrite of the call never runs.</summary>
    SkipDropsRewrite,

    /// <summary>Both change the method's arguments. Their changes add up, one after the other.</summary>
    BothChangeArgs,

    /// <summary>One can skip the method, and the other may count on it having run.</summary>
    Skips,

    /// <summary>One rewrites the call into the game's code, and the other may count on the usual call.</summary>
    Rewrites,
}

/// <summary>
/// One thing one mod's patch does to a method. Order is where the patch runs among all the method's patches
/// (prefixes first, then postfixes, then finalizers), so the last one to set the result is known. ChecksSkip is
/// true when the patch reads __runOriginal, so it knows when another mod skipped the method.
/// </summary>
public sealed record PatchUse(string Guid, PatchEffect Effect, int Order = 0, bool ChecksSkip = false);

/// <summary>One game method, named as "Type.Method" (with its parameters when overloads share the name), and every plugin patch on it.</summary>
public sealed record PatchedMethod(string Method, IReadOnlyList<PatchUse> Uses);

/// <summary>
/// A method two mods both patch, what each does to it, the risk that follows and why. LastWord is the mod whose
/// result the game gets when both set it, null otherwise.
/// </summary>
public sealed record SharedMethod(string Method, PatchRisk Risk, IReadOnlyList<PatchEffect> Mine, IReadOnlyList<PatchEffect> Theirs, ClashReason Reason = ClashReason.None, string? LastWord = null)
{
    public bool Equals(SharedMethod? other) =>
        other != null && Method == other.Method && Risk == other.Risk && Mine.SequenceEqual(other.Mine) && Theirs.SequenceEqual(other.Theirs) && Reason == other.Reason && LastWord == other.LastWord;

    public override int GetHashCode() => Method.GetHashCode();

    /// <summary>The method without its parameter list, the way a stack trace names it.</summary>
    public string Name => Method.IndexOf('(') is var at and > 0 ? Method.Substring(0, at) : Method;
}

/// <summary>Another mod that patches some of the same game methods. Its risk is the worst of the shared methods.</summary>
public sealed record PatchOverlap(string Guid, PatchRisk Risk, IReadOnlyList<SharedMethod> Methods)
{
    public bool Equals(PatchOverlap? other) => other != null && Guid == other.Guid && Risk == other.Risk && Methods.SequenceEqual(other.Methods);

    public override int GetHashCode() => Guid.GetHashCode();
}

/// <summary>
/// Which mods change the same game code, and whether that is likely to matter. HarmonyX runs every prefix and every
/// postfix whatever the others do, so most shared methods are fine; trouble comes when one mod's change can undo or
/// hide another's. This is a guide for where to look, not proof.
/// </summary>
public static class PatchOverlaps
{
    /// <summary>Performance Analyzer only times the game's code, so its patches change nothing and are left out.</summary>
    public static readonly IReadOnlyCollection<string> MeasuringOnly = new[] { "com.dev.survivallog.perfanalyzer" };

    /// <summary>What one mod does to one method: its effects in enum order, whether all its patches check for a skip, and when it last sets the result.</summary>
    public sealed record Side(IReadOnlyList<PatchEffect> Effects, bool ChecksSkip = false, int LastResult = -1);

    /// <summary>For each mod, the other mods sharing a method with it: riskiest first, then most shared methods.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<PatchOverlap>> Find(IEnumerable<PatchedMethod> methods)
    {
        var shared = new Dictionary<string, Dictionary<string, List<SharedMethod>>>(StringComparer.Ordinal);

        foreach (var (method, byMod) in SidesByMethod(methods))
        {
            foreach (var (guid, mine) in byMod)
            {
                foreach (var (other, theirs) in byMod.Where(pair => pair.Key != guid))
                {
                    var (risk, reason) = Judge(mine, theirs);
                    var lastWord = reason == ClashReason.BothSetResult ? (mine.LastResult > theirs.LastResult ? guid : other) : null;

                    MethodsShared(shared, guid, other).Add(new SharedMethod(method, risk, mine.Effects, theirs.Effects, reason, lastWord));
                }
            }
        }

        return shared.ToDictionary(pair => pair.Key, pair => Overlaps(pair.Value), StringComparer.Ordinal);
    }

    public static PatchRisk RiskOf(IReadOnlyCollection<PatchEffect> mine, IReadOnlyCollection<PatchEffect> theirs) =>
        Judge(new Side(mine.ToList()), new Side(theirs.ToList())).Risk;

    /// <summary>The worst thing that can happen between two mods on one method, checked worst first.</summary>
    public static (PatchRisk Risk, ClashReason Reason) Judge(Side mine, Side theirs)
    {
        bool Both(PatchEffect effect) => mine.Effects.Contains(effect) && theirs.Effects.Contains(effect);
        bool Either(PatchEffect effect) => mine.Effects.Contains(effect) || theirs.Effects.Contains(effect);
        bool SkipsPastRewrite(Side skipper, Side other) => skipper.Effects.Contains(PatchEffect.CanSkip) && other.Effects.Contains(PatchEffect.Rewrites);
        bool SkipMatters(Side skipper, Side other) => skipper.Effects.Contains(PatchEffect.CanSkip) && !other.ChecksSkip;

        if (Both(PatchEffect.SetsResult)) return (PatchRisk.LikelyClash, ClashReason.BothSetResult);

        if (Both(PatchEffect.Rewrites)) return (PatchRisk.LikelyClash, ClashReason.BothRewrite);

        if (SkipsPastRewrite(mine, theirs) || SkipsPastRewrite(theirs, mine)) return (PatchRisk.LikelyClash, ClashReason.SkipDropsRewrite);

        if (Both(PatchEffect.ChangesArgs)) return (PatchRisk.MayClash, ClashReason.BothChangeArgs);

        if (SkipMatters(mine, theirs) || SkipMatters(theirs, mine)) return (PatchRisk.MayClash, ClashReason.Skips);

        if (Either(PatchEffect.Rewrites)) return (PatchRisk.MayClash, ClashReason.Rewrites);

        return (PatchRisk.Harmless, ClashReason.None);
    }

    /// <summary>Each mod's side on each method, measuring-only mods left out.</summary>
    private static SortedDictionary<string, SortedDictionary<string, Side>> SidesByMethod(IEnumerable<PatchedMethod> methods) =>
        new(methods
            .GroupBy(method => method.Method, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new SortedDictionary<string, Side>(group
                    .SelectMany(method => method.Uses)
                    .Where(use => !MeasuringOnly.Contains(use.Guid))
                    .GroupBy(use => use.Guid, StringComparer.Ordinal)
                    .ToDictionary(uses => uses.Key, SideOf, StringComparer.Ordinal), StringComparer.Ordinal)),
            StringComparer.Ordinal);

    private static Side SideOf(IEnumerable<PatchUse> uses)
    {
        var list = uses.ToList();
        var resultOrders = list.Where(use => use.Effect == PatchEffect.SetsResult).Select(use => use.Order).ToList();
        var effects = list.Select(use => use.Effect).Distinct().OrderBy(effect => effect).ToList();

        return new Side(effects, list.All(use => use.ChecksSkip), resultOrders.Count > 0 ? resultOrders.Max() : -1);
    }

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
