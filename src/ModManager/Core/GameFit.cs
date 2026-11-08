using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ModManager.Core;

/// <summary>
/// What one DLL uses from the game's code (its types, their members, and the game methods it patches), checked
/// against the game's code for the build that is running. A mod built for an older build may call a method the game
/// no longer has, which fails only when that code runs.
/// </summary>
/// <param name="Checked">How many of the game's types, members and patch targets it uses.</param>
/// <param name="MissingCount">How many of them this build no longer has.</param>
/// <param name="Missing">Their code names, sorted, at most <see cref="MaxNames"/> of them.</param>
public sealed record GameFit(int Checked, int MissingCount, IReadOnlyList<string> Missing)
{
    public const int MaxNames = 50;

    public bool IsMissingAny => MissingCount > 0;

    public static GameFit Of(int checkedCount, IEnumerable<string> missing)
    {
        var names = missing.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();

        return new GameFit(checkedCount, names.Count, names.Take(MaxNames).ToList());
    }
}

/// <summary>
/// The checks of earlier starts, by each DLL's SHA-256, for the game's code as it was then. A game update rebuilds that
/// code, which changes the stamp, and every DLL is checked again.
/// </summary>
public sealed record GameFitCache(string Stamp, IReadOnlyDictionary<string, GameFit> ByDll)
{
    public const int Schema = 1;

    /// <summary>More than any plugins folder holds; a longer saved list is cut, not trusted whole.</summary>
    public const int MaxDlls = 1000;

    public static readonly GameFitCache Empty = new("", new Dictionary<string, GameFit>());

    /// <summary>
    /// Names the game's code as BepInEx built it, from the name, size and time of each file in its interop folder.
    /// Ordered by name, so the order a folder lists its files in does not matter.
    /// </summary>
    public static string StampOf(IEnumerable<(string Name, long Length, long WrittenTicks)> files)
    {
        var text = new StringBuilder();

        foreach (var (name, length, ticks) in files.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            text.Append(name.ToLowerInvariant()).Append('|').Append(length).Append('|').Append(ticks).Append('\n');
        }

        return Sha256Text.Of(Encoding.UTF8.GetBytes(text.ToString()));
    }

    /// <summary>The DLLs that still need a check: all of them when the game's code has changed since the saved checks.</summary>
    public IReadOnlyList<string> ToCheck(string stamp, IEnumerable<string> dlls) =>
        dlls.Distinct(StringComparer.Ordinal).Where(dll => stamp != Stamp || !ByDll.ContainsKey(dll)).ToList();

    /// <summary>
    /// The saved checks with the new ones added, for the DLLs installed now only, so the file never grows past the
    /// plugins folder. Saved checks of another stamp are dropped.
    /// </summary>
    public GameFitCache With(string stamp, IReadOnlyDictionary<string, GameFit> fresh, IEnumerable<string> installed)
    {
        var kept = new Dictionary<string, GameFit>(StringComparer.Ordinal);

        foreach (var dll in installed.Distinct(StringComparer.Ordinal))
        {
            if (fresh.TryGetValue(dll, out var fit) || (stamp == Stamp && ByDll.TryGetValue(dll, out fit))) kept[dll] = fit;
        }

        return new GameFitCache(stamp, kept);
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", Schema);
            json.WriteString("interop", Stamp);
            json.WriteStartObject("dlls");

            foreach (var (dll, fit) in ByDll)
            {
                json.WriteStartObject(dll);
                json.WriteNumber("checked", fit.Checked);
                json.WriteNumber("missingCount", fit.MissingCount);
                json.WriteStartArray("missing");

                foreach (var name in fit.Missing) json.WriteStringValue(name);

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The saved copy, or null when it is of another schema or cannot be read; every DLL is then checked again.</summary>
    public static GameFitCache? FromJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var number) || number != Schema) return null;

            var byDll = new Dictionary<string, GameFit>(StringComparer.Ordinal);

            if (root.TryGetProperty("dlls", out var dlls) && dlls.ValueKind == JsonValueKind.Object)
            {
                foreach (var pair in dlls.EnumerateObject().Take(MaxDlls))
                {
                    if (!NexusApi.IsSha256(pair.Name) || Json.IntOf(pair.Value, "checked") is not { } checkedCount || Json.IntOf(pair.Value, "missingCount") is not { } missingCount) continue;

                    var names = Json.ArrayOf(pair.Value, "missing").Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString() ?? "").Where(name => name.Length > 0).Take(GameFit.MaxNames).ToList();

                    byDll[pair.Name] = new GameFit(checkedCount, Math.Max(missingCount, names.Count), names);
                }
            }

            return new GameFitCache(Json.StringOf(root, "interop") ?? "", byDll);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
