using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>One game method and the GUIDs of the mods whose Harmony patches change it.</summary>
public sealed record PatchedMethod(string Method, IReadOnlyList<string> Guids);

/// <summary>Another mod that patches some of the same game methods, named as "Type.Method".</summary>
public sealed record PatchOverlap(string Guid, IReadOnlyList<string> Methods)
{
    public bool Equals(PatchOverlap? other) => other != null && Guid == other.Guid && Methods.SequenceEqual(other.Methods);

    public override int GetHashCode() => Guid.GetHashCode();
}

/// <summary>
/// Which mods change the same game code. Two patches on one method often work together, so this only tells the
/// player where to look when two mods misbehave together.
/// </summary>
public static class PatchOverlaps
{
    /// <summary>For each mod, the other mods sharing a method with it: most shared methods first, methods in name order.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<PatchOverlap>> Find(IEnumerable<PatchedMethod> methods)
    {
        var shared = new Dictionary<string, Dictionary<string, SortedSet<string>>>(StringComparer.Ordinal);

        foreach (var method in methods)
        {
            var guids = method.Guids.Distinct(StringComparer.Ordinal).ToList();

            foreach (var guid in guids)
            {
                foreach (var other in guids.Where(other => other != guid))
                {
                    Methods(shared, guid, other).Add(method.Method);
                }
            }
        }

        return shared.ToDictionary(pair => pair.Key, pair => Overlaps(pair.Value), StringComparer.Ordinal);
    }

    private static SortedSet<string> Methods(Dictionary<string, Dictionary<string, SortedSet<string>>> shared, string guid, string other)
    {
        if (!shared.TryGetValue(guid, out var others)) shared[guid] = others = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        if (!others.TryGetValue(other, out var names)) others[other] = names = new SortedSet<string>(StringComparer.Ordinal);

        return names;
    }

    private static IReadOnlyList<PatchOverlap> Overlaps(Dictionary<string, SortedSet<string>> others) =>
        others
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new PatchOverlap(pair.Key, pair.Value.ToList()))
            .ToList();
}
