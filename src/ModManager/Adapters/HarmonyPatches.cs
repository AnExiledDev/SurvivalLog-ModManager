using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>Reads HarmonyX's record of every patched game method, naming each patch by the plugin it came from and what it does.</summary>
internal static class HarmonyPatches
{
    /// <param name="pluginOf">A loaded plugin's GUID by the assembly it was loaded from.</param>
    /// <param name="guids">Every loaded plugin's GUID, for patches whose Harmony id is the plugin's GUID.</param>
    public static IReadOnlyList<PatchedMethod> Shared(IReadOnlyDictionary<Assembly, string> pluginOf, IReadOnlyCollection<string> guids, Action<string> report)
    {
        var methods = new List<PatchedMethod>();

        try
        {
            foreach (var method in Harmony.GetAllPatchedMethods().ToList())
            {
                if (Harmony.GetPatchInfo(method) is not { } info) continue;

                var patches = info.Prefixes.Select(patch => (patch, CanSkip(patch) ? PatchEffect.CanSkip : PatchEffect.RunsBefore))
                    .Concat(info.Postfixes.Concat(info.Finalizers).Select(patch => (patch, PatchEffect.RunsAfter)))
                    .Concat(info.Transpilers.Concat(info.ILManipulators).Select(patch => (patch, PatchEffect.Rewrites)));
                var uses = patches
                    .Select(pair => OwnerOf(pair.patch, pluginOf, guids) is { } guid ? new PatchUse(guid, pair.Item2) : null)
                    .OfType<PatchUse>()
                    .ToList();

                if (uses.Select(use => use.Guid).Distinct().Count() > 1) methods.Add(new PatchedMethod((method.DeclaringType?.Name ?? "?") + "." + method.Name, uses));
            }
        }
        catch (Exception ex)
        {
            report("could not read which game methods mods patch: " + ex.Message);
        }

        return methods;
    }

    /// <summary>A prefix that returns bool, or takes __runOriginal by ref, can stop the game's own code.</summary>
    private static bool CanSkip(Patch patch) =>
        patch.PatchMethod is { } prefix && (prefix.ReturnType == typeof(bool) || prefix.GetParameters().Any(parameter => parameter.Name == "__runOriginal" && parameter.ParameterType.IsByRef));

    private static string? OwnerOf(Patch patch, IReadOnlyDictionary<Assembly, string> pluginOf, IReadOnlyCollection<string> guids)
    {
        var assembly = patch.PatchMethod?.DeclaringType?.Assembly;

        if (assembly != null && pluginOf.TryGetValue(assembly, out var guid)) return guid;

        return guids.Contains(patch.owner) ? patch.owner : null;
    }
}
