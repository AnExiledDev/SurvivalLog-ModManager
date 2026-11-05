using BepInEx.Configuration;
using ExiledToolkit;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// The Toolkit's world rules. While a world plays and keeps its own value of a rule, a change made on the Mods screen
/// goes to that world alone and the cfg keeps the global value; the Toolkit does that routing, so this only says which
/// value is in play and moves a rule between this world and every world.
/// </summary>
internal static class WorldAccess
{
    public static WorldState? StateOf(ConfigEntryBase entry)
    {
        if (!WorldRules.InWorld || !WorldRules.IsRule(entry)) return null;

        return new WorldState(WorldRules.IsSetByWorld(entry), WorldRules.GlobalValue(entry) ?? entry.GetSerializedValue());
    }

    /// <summary>This world keeps the value now in play as its own, so later changes stay with it.</summary>
    public static bool KeepForWorld(ConfigEntryBase entry) => WorldRules.SetWorld(entry, entry.GetSerializedValue());

    public static bool UseGlobal(ConfigEntryBase entry) => WorldRules.ClearWorld(entry);
}
