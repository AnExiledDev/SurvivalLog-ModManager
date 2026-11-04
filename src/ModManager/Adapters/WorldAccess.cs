using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using ExiledToolkit;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// The Toolkit's world rules (Toolkit 0.16.0 or newer). While a world plays and keeps its own value of a rule, a change
/// made on the Mods screen goes to that world alone and the cfg keeps the global value; the Toolkit does that routing,
/// so this only says which value is in play and moves a rule between this world and every world. With an older
/// Toolkit every call answers "not a world rule". Only the NoInlining methods name WorldRules.
/// </summary>
internal static class WorldAccess
{
    private static bool _isOff;

    public static WorldState? StateOf(ConfigEntryBase entry) => Try(() => StateWithToolkit(entry));

    /// <summary>This world keeps the value now in play as its own, so later changes stay with it.</summary>
    public static bool KeepForWorld(ConfigEntryBase entry) => Try(() => KeepWithToolkit(entry));

    public static bool UseGlobal(ConfigEntryBase entry) => Try(() => ClearWithToolkit(entry));

    private static T? Try<T>(Func<T> call)
    {
        if (_isOff) return default;

        try
        {
            return call();
        }
        catch (Exception ex) when (ex is TypeLoadException or MissingMethodException or MissingFieldException or FileNotFoundException or FileLoadException)
        {
            _isOff = true;

            return default;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WorldState? StateWithToolkit(ConfigEntryBase entry)
    {
        if (!WorldRules.InWorld || !WorldRules.IsRule(entry)) return null;

        return new WorldState(WorldRules.IsSetByWorld(entry), WorldRules.GlobalValue(entry) ?? entry.GetSerializedValue());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool KeepWithToolkit(ConfigEntryBase entry) => WorldRules.SetWorld(entry, entry.GetSerializedValue());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ClearWithToolkit(ConfigEntryBase entry) => WorldRules.ClearWorld(entry);
}
