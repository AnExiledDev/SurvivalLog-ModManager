using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>Reads HarmonyX's record of every patched game method, naming each patch by the plugin it came from.</summary>
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

                var owners = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).Concat(info.ILManipulators)
                    .Select(patch => OwnerOf(patch, pluginOf, guids))
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (owners.Count > 1) methods.Add(new PatchedMethod((method.DeclaringType?.Name ?? "?") + "." + method.Name, owners));
            }
        }
        catch (Exception ex)
        {
            report("could not read which game methods mods patch: " + ex.Message);
        }

        return methods;
    }

    private static string? OwnerOf(Patch patch, IReadOnlyDictionary<Assembly, string> pluginOf, IReadOnlyCollection<string> guids)
    {
        var assembly = patch.PatchMethod?.DeclaringType?.Assembly;

        if (assembly != null && pluginOf.TryGetValue(assembly, out var guid)) return guid;

        return guids.Contains(patch.owner) ? patch.owner : null;
    }
}
