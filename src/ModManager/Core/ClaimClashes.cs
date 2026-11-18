using System;
using System.Collections.Generic;
using System.Linq;
using ExiledToolkit;

namespace ModManager.Core;

/// <summary>What two mods both change, from what they reported to Exiled's Toolkit.</summary>
public enum ClaimKind
{
    /// <summary>Both added a row with the same id to one table, so only one of the two rows is in the game.</summary>
    SameRow,

    /// <summary>Both wrote one field of one row. The one that wrote last has its value in the table.</summary>
    SameValue,

    /// <summary>Both put a native hook on one method. A method takes only one, so the second one fails.</summary>
    BothHook,

    /// <summary>One put a native hook on a method the other patches with Harmony, which also hooks it natively.</summary>
    HookAndPatch,

    /// <summary>Both changed some of the same bytes of a method's code.</summary>
    SameBytes,

    /// <summary>One changed bytes at the start of a method, where the other's hook or Harmony patch writes its jump.</summary>
    BytesUnderHook,

    /// <summary>A value this mod wrote is not in the table any more, and no mod that reports its changes wrote it. Found by the table check.</summary>
    WrittenOver,
}

/// <summary>
/// One thing two mods both change. Where names it: "Config_Item 910001" for a row, "Config_Item 910001 price" for a
/// value, "Type.Method" for code. Mine and Theirs are the two values of a SameValue (Theirs is what the table holds now
/// for a WrittenOver); LastWord is the mod whose value the table holds.
/// </summary>
public sealed record ClaimClash(ClaimKind Kind, PatchRisk Risk, string Where, string? Mine = null, string? Theirs = null, string? LastWord = null);

/// <summary>Another mod that changes some of the same things, or "" for changes no reporting mod made. Its risk is the worst of the clashes.</summary>
public sealed record ClaimOverlap(string Guid, PatchRisk Risk, IReadOnlyList<ClaimClash> Clashes)
{
    public bool Equals(ClaimOverlap? other) => other != null && Guid == other.Guid && Risk == other.Risk && Clashes.SequenceEqual(other.Clashes);

    public override int GetHashCode() => Guid.GetHashCode();
}

/// <summary>
/// Which mods change the same game data or the same game code outside Harmony, from what each reported through
/// Exiled's Toolkit's Claims. A mod that reports nothing shows up only through Harmony, the table check and the code scan.
/// </summary>
public static class ClaimClashes
{
    /// <summary>How many bytes at a method's start a native hook overwrites with its jump.</summary>
    public const int HookBytes = 14;

    /// <summary>For each mod, the other mods it shares a reported change with: riskiest first, then most clashes.</summary>
    /// <param name="patched">Every game method Harmony patches and who patches it, so a native hook on one can be found.</param>
    public static IReadOnlyDictionary<string, IReadOnlyList<ClaimOverlap>> Find(ClaimSet claims, IEnumerable<PatchedMethod> patched)
    {
        var found = new List<(string Guid, string Other, ClaimClash Clash)>();
        var patchers = patched
            .GroupBy(method => BareName(method.Method), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(method => method.Uses).Select(use => use.Guid).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

        foreach (var group in claims.Rows.GroupBy(row => (row.Table, row.Row)))
        {
            foreach (var (mine, theirs) in Pairs(group.Select(row => row.Guid))) found.Add((mine, theirs, new ClaimClash(ClaimKind.SameRow, PatchRisk.LikelyClash, group.Key.Table + " " + group.Key.Row)));
        }

        foreach (var group in claims.Fields.GroupBy(field => (field.Table, field.Row, field.Field)))
        {
            var where = group.Key.Table + " " + group.Key.Row + " " + group.Key.Field;
            var writes = group.ToList();

            foreach (var mine in writes)
            {
                foreach (var theirs in writes.Where(write => write.Guid != mine.Guid))
                {
                    var same = mine.Value == theirs.Value;
                    var last = mine.Order > theirs.Order ? mine.Guid : theirs.Guid;

                    found.Add((mine.Guid, theirs.Guid, new ClaimClash(ClaimKind.SameValue, same ? PatchRisk.Harmless : PatchRisk.LikelyClash, where, mine.Value, theirs.Value, same ? null : last)));
                }
            }
        }

        foreach (var group in claims.Hooks.GroupBy(hook => hook.Method, StringComparer.Ordinal))
        {
            foreach (var (mine, theirs) in Pairs(group.Select(hook => hook.Guid))) found.Add((mine, theirs, new ClaimClash(ClaimKind.BothHook, PatchRisk.LikelyClash, group.Key)));

            if (!patchers.TryGetValue(group.Key, out var byHarmony)) continue;

            foreach (var hooker in group.Select(hook => hook.Guid).Distinct(StringComparer.Ordinal))
            {
                foreach (var patcher in byHarmony.Where(guid => guid != hooker)) Both(found, hooker, patcher, new ClaimClash(ClaimKind.HookAndPatch, PatchRisk.LikelyClash, group.Key));
            }
        }

        var hookers = claims.Hooks.ToLookup(hook => hook.Method, hook => hook.Guid, StringComparer.Ordinal);

        foreach (var group in claims.Bytes.GroupBy(bytes => bytes.Method, StringComparer.Ordinal))
        {
            var changes = group.ToList();

            foreach (var (mine, theirs) in Pairs(changes.Select(bytes => bytes.Guid)))
            {
                if (changes.Where(bytes => bytes.Guid == mine).Any(a => changes.Where(bytes => bytes.Guid == theirs).Any(b => Overlap(a, b)))) found.Add((mine, theirs, new ClaimClash(ClaimKind.SameBytes, PatchRisk.LikelyClash, group.Key)));
            }

            var atStart = changes.Where(bytes => bytes.Offset < HookBytes).Select(bytes => bytes.Guid).Distinct(StringComparer.Ordinal).ToList();
            var hooked = hookers[group.Key].Concat(patchers.TryGetValue(group.Key, out var byHarmony) ? byHarmony : new List<string>()).Distinct(StringComparer.Ordinal).ToList();

            foreach (var changer in atStart)
            {
                foreach (var hooker in hooked.Where(guid => guid != changer)) Both(found, changer, hooker, new ClaimClash(ClaimKind.BytesUnderHook, PatchRisk.LikelyClash, group.Key));
            }
        }

        return Grouped(found);
    }

    /// <summary>
    /// The values mods reported writing that the table no longer holds, each on the mod whose value it was, under the
    /// mod "" when no other reporting mod wrote that field last. A field two reporting mods wrote is already a SameValue.
    /// </summary>
    /// <param name="current">What the table holds now for one field, or null when the row or field cannot be read.</param>
    public static IReadOnlyDictionary<string, IReadOnlyList<ClaimOverlap>> WrittenOver(ClaimSet claims, Func<FieldClaim, string?> current)
    {
        var found = new List<(string Guid, string Other, ClaimClash Clash)>();

        foreach (var group in claims.Fields.GroupBy(field => (field.Table, field.Row, field.Field)))
        {
            var last = group.OrderBy(field => field.Order).Last();

            if (current(last) is not { } now || SameValue(now, last.Value)) continue;

            found.Add((last.Guid, "", new ClaimClash(ClaimKind.WrittenOver, PatchRisk.LikelyClash, last.Table + " " + last.Row + " " + last.Field, last.Value, now)));
        }

        return Grouped(found);
    }

    /// <summary>Two values as text, the same when they read the same or are the same number (a float written as 1.5 reads back as 1.5 or 1.50).</summary>
    public static bool SameValue(string a, string b) =>
        a == b || double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) && double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) && Math.Abs(x - y) <= 1e-6 * Math.Max(1, Math.Abs(x));

    /// <summary>Both lists merged per mod pair, so a mod's clashes with one other mod read as one block.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<ClaimOverlap>> Merge(IReadOnlyDictionary<string, IReadOnlyList<ClaimOverlap>> a, IReadOnlyDictionary<string, IReadOnlyList<ClaimOverlap>> b) =>
        Grouped(a.Concat(b).SelectMany(pair => pair.Value.SelectMany(overlap => overlap.Clashes.Select(clash => (pair.Key, overlap.Guid, clash)))).ToList());

    private static void Both(List<(string Guid, string Other, ClaimClash Clash)> found, string a, string b, ClaimClash clash)
    {
        found.Add((a, b, clash));
        found.Add((b, a, clash));
    }

    private static IEnumerable<(string Mine, string Theirs)> Pairs(IEnumerable<string> guids)
    {
        var distinct = guids.Distinct(StringComparer.Ordinal).OrderBy(guid => guid, StringComparer.Ordinal).ToList();

        return distinct.SelectMany(mine => distinct.Where(theirs => theirs != mine).Select(theirs => (mine, theirs)));
    }

    private static bool Overlap(BytesClaim a, BytesClaim b) => a.Offset < b.Offset + b.Length && b.Offset < a.Offset + a.Length;

    /// <summary>"Type.Method(Int32)" read as "Type.Method", the way claims name methods.</summary>
    private static string BareName(string method) => method.IndexOf('(') is var at and > 0 ? method.Substring(0, at) : method;

    private static IReadOnlyDictionary<string, IReadOnlyList<ClaimOverlap>> Grouped(IReadOnlyList<(string Guid, string Other, ClaimClash Clash)> found) =>
        found
            .GroupBy(entry => entry.Guid, StringComparer.Ordinal)
            .ToDictionary(
                mod => mod.Key,
                mod => (IReadOnlyList<ClaimOverlap>)mod
                    .GroupBy(entry => entry.Other, StringComparer.Ordinal)
                    .Select(other => new ClaimOverlap(other.Key, other.Max(entry => entry.Clash.Risk), other.Select(entry => entry.Clash).Distinct().OrderByDescending(clash => clash.Risk).ThenBy(clash => clash.Kind).ThenBy(clash => clash.Where, StringComparer.Ordinal).ToList()))
                    .OrderByDescending(overlap => overlap.Risk)
                    .ThenByDescending(overlap => overlap.Clashes.Count)
                    .ThenBy(overlap => overlap.Guid, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);
}
