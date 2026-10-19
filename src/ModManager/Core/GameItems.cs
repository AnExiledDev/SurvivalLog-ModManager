using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>An item from the game's item table, for the item pickers.</summary>
/// <param name="Icon">The icon's path under the game's WebUI folder, or null when the game has no web icon for it.</param>
public sealed record CatalogItem(int Id, string Name, string? Icon, int Category);

/// <summary>One of the game's item categories, by its number, named in the game's language.</summary>
public sealed record CategoryName(int Id, string Name);

public static class GameItems
{
    /// <summary>Only a page that shows an item or category control needs the item table, which is large.</summary>
    public static bool AreNeeded(IEnumerable<ModEntry> mods) =>
        mods.Any(mod => mod.State is ModState.Loaded loaded && loaded.Settings.Any(setting => setting.Kind is SettingKind.Item or SettingKind.ItemList or SettingKind.ItemCounts or SettingKind.Category));
}
