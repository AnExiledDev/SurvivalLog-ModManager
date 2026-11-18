using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Reads HarmonyX's record of every patched game method, naming each patch by the plugin it came from, what it does
/// and where it runs among the method's other patches.
/// </summary>
internal static class HarmonyPatches
{
    /// <param name="pluginOf">A loaded plugin's GUID by the assembly it was loaded from.</param>
    /// <param name="guids">Every loaded plugin's GUID, for patches whose Harmony id is the plugin's GUID.</param>
    /// <returns>Every game method a loaded plugin patches, with all the plugins' patches on it.</returns>
    public static IReadOnlyList<PatchedMethod> Patched(IReadOnlyDictionary<Assembly, string> pluginOf, IReadOnlyCollection<string> guids, Action<string> report)
    {
        var found = new List<(MethodBase Method, List<PatchUse> Uses)>();

        try
        {
            foreach (var method in Harmony.GetAllPatchedMethods().ToList())
            {
                if (Harmony.GetPatchInfo(method) is not { } info) continue;

                var uses = UsesOf(method, info, patch => OwnerOf(patch, pluginOf, guids));

                if (uses.Count > 0) found.Add((method, uses));
            }
        }
        catch (Exception ex)
        {
            report("could not read which game methods mods patch: " + ex.Message);
        }

        return Named(found);
    }

    /// <summary>Every patch in the order HarmonyX runs them: prefixes, then postfixes, then finalizers. Rewrites run at build time, so their order does not matter here.</summary>
    private static List<PatchUse> UsesOf(MethodBase method, Patches info, Func<Patch, string?> ownerOf)
    {
        var uses = new List<PatchUse>();
        var order = 0;

        void Add(Patch patch, IEnumerable<PatchEffect> effects)
        {
            var at = order++;

            if (ownerOf(patch) is not { } guid || patch.PatchMethod is not { } patchMethod) return;

            var checksSkip = patchMethod.GetParameters().Any(parameter => parameter.Name == "__runOriginal");

            uses.AddRange(effects.Select(effect => new PatchUse(guid, effect, at, checksSkip)));
        }

        foreach (var patch in Sorted(method, info.Prefixes)) Add(patch, PrefixEffects(patch.PatchMethod));

        foreach (var patch in Sorted(method, info.Postfixes).Concat(Sorted(method, info.Finalizers))) Add(patch, AfterEffects(patch.PatchMethod));

        foreach (var patch in info.Transpilers.Concat(info.ILManipulators)) Add(patch, new[] { PatchEffect.Rewrites });

        return uses;
    }

    /// <summary>Priority, then before and after, then patch order, as HarmonyX sorts them. On a sorting error, the order they were added in.</summary>
    private static IReadOnlyList<Patch> Sorted(MethodBase method, IReadOnlyCollection<Patch> patches)
    {
        try
        {
            var sorted = PatchProcessor.GetSortedPatchMethods(method, patches.ToArray());

            return patches.OrderBy(patch => sorted.IndexOf(patch.PatchMethod)).ToList();
        }
        catch (Exception)
        {
            return patches.ToList();
        }
    }

    /// <summary>A prefix that returns bool, or takes __runOriginal by ref, can stop the game's code.</summary>
    private static IEnumerable<PatchEffect> PrefixEffects(MethodInfo? prefix)
    {
        var effects = ChangeEffects(prefix).ToList();

        if (prefix != null && (prefix.ReturnType == typeof(bool) || TakesByRef(prefix, "__runOriginal"))) effects.Add(PatchEffect.CanSkip);

        return effects.Count > 0 ? effects : new[] { PatchEffect.RunsBefore };
    }

    private static IEnumerable<PatchEffect> AfterEffects(MethodInfo? patch)
    {
        var effects = ChangeEffects(patch).ToList();

        return effects.Count > 0 ? effects : new[] { PatchEffect.RunsAfter };
    }

    private static IEnumerable<PatchEffect> ChangeEffects(MethodInfo? patch)
    {
        if (patch == null) yield break;

        if (TakesByRef(patch, "__result")) yield return PatchEffect.SetsResult;

        if (patch.GetParameters().Any(IsGameArgumentByRef)) yield return PatchEffect.ChangesArgs;
    }

    private static bool TakesByRef(MethodInfo patch, string name) => patch.GetParameters().Any(parameter => parameter.Name == name && parameter.ParameterType.IsByRef);

    /// <summary>
    /// A by-ref parameter named after one of the method's own, or by its position ("__0"). Harmony's other names
    /// (__instance, __state, __result, __runOriginal) are not the game's arguments.
    /// </summary>
    private static bool IsGameArgumentByRef(ParameterInfo parameter) =>
        parameter.ParameterType.IsByRef && parameter.Name is { } name && (!name.StartsWith("__", StringComparison.Ordinal) || name.Length > 2 && name.Skip(2).All(char.IsDigit));

    /// <summary>"Type.Method", with the parameter types added when two patched methods would otherwise read the same.</summary>
    private static IReadOnlyList<PatchedMethod> Named(IReadOnlyList<(MethodBase Method, List<PatchUse> Uses)> found)
    {
        var overloaded = found.GroupBy(pair => ShortName(pair.Method)).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();

        return found.Select(pair => new PatchedMethod(overloaded.Contains(ShortName(pair.Method)) ? ShortName(pair.Method) + "(" + ParameterList(pair.Method) + ")" : ShortName(pair.Method), pair.Uses)).ToList();
    }

    private static string ShortName(MethodBase method) => (method.DeclaringType?.Name ?? "?") + "." + method.Name;

    private static string ParameterList(MethodBase method) =>
        string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name is var name && name.IndexOf('`') is var at and > 0 ? name.Substring(0, at) : parameter.ParameterType.Name));

    private static string? OwnerOf(Patch patch, IReadOnlyDictionary<Assembly, string> pluginOf, IReadOnlyCollection<string> guids)
    {
        var assembly = patch.PatchMethod?.DeclaringType?.Assembly;

        if (assembly != null && pluginOf.TryGetValue(assembly, out var guid)) return guid;

        return guids.Contains(patch.owner) ? patch.owner : null;
    }
}
