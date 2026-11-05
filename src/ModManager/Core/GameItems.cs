using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>An item from the game's item table, for the item pickers, named in both languages.</summary>
/// <param name="Icon">The icon's path under the game's WebUI folder, or null when the game has no web icon for it.</param>
public sealed record CatalogItem(int Id, Description Name, string? Icon, int Category)
{
    /// <summary>The item's weight field, which the game keeps in grams.</summary>
    public int Grams { get; init; }

    /// <summary>The item's price field, for comparing what lists are worth.</summary>
    public int Price { get; init; }

    /// <summary>How many of the game's recipes take it as a material.</summary>
    public int RecipeUses { get; init; }
}

/// <summary>One of the game's item categories, by its number, named in both languages.</summary>
public sealed record CategoryName(int Id, Description Name);

public static class GameItems
{
    /// <summary>Only a page that shows an item or category control needs the item table, which is large.</summary>
    public static bool AreNeeded(IEnumerable<ModEntry> mods) =>
        mods.Any(mod => mod.State is ModState.Loaded loaded && loaded.Settings.Any(setting => setting.Kind is SettingKind.Item or SettingKind.ItemList or SettingKind.ItemCounts or SettingKind.Category));

    /// <summary>How many recipes take each item, an item a recipe takes several of counted once for it.</summary>
    public static IReadOnlyDictionary<int, int> RecipeUses(IEnumerable<IEnumerable<int>> recipes)
    {
        var uses = new Dictionary<int, int>();

        foreach (var materials in recipes)
        {
            foreach (var id in materials.Distinct()) uses[id] = uses.TryGetValue(id, out var count) ? count + 1 : 1;
        }

        return uses;
    }
}
