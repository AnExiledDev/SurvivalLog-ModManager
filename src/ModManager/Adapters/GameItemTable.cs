using System;
using System.Collections.Generic;
using System.Linq;
using ExiledToolkit.Game;
using ExiledToolkit.Web;
using GameCore.HotUpdate;
using ModManager.Core;
using UnityEngine;

namespace ModManager.Adapters;

/// <summary>
/// The game's items and item categories, named in both languages, for the item pickers. Read only once the game has
/// built its ConfigManager, then kept: the page switches language on its own.
/// </summary>
internal sealed class GameItemTable
{
    private readonly Action<string> _log;
    private (IReadOnlyList<CatalogItem> Items, IReadOnlyList<CategoryName> Categories)? _read;
    private IconIndex? _icons;
    private Dictionary<int, Description>? _names;
    private bool _hasReportedRecipes;

    public GameItemTable(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Empty lists while the game has not loaded its tables yet; those are not kept, so a later call tries again.</summary>
    public (IReadOnlyList<CatalogItem> Items, IReadOnlyList<CategoryName> Categories) Read()
    {
        if (_read is { } known) return known;

        var config = GameSingleton.Existing<ConfigManager>();
        var table = config?._Config_Item_Dict;

        if (config == null || table == null) return (Array.Empty<CatalogItem>(), Array.Empty<CategoryName>());

        _icons ??= LoadIcons();

        var uses = RecipeUses(config);
        var items = new List<CatalogItem>();

        foreach (var item in table.Values)
        {
            if (item == null) continue;

            var english = GameNames.Translate(config, item.ItemName, LanguageType.English);
            var chinese = GameNames.Translate(config, item.ItemName, LanguageType.Chinese) ?? GameNames.NonEmpty(item.ItemName_Local);

            // An item whose name is still the raw key in both languages is left out.
            if (english == null && chinese == null) continue;

            items.Add(new CatalogItem(item.ID, new Description(english ?? chinese!, chinese ?? english!), _icons.Resolve(GameNames.NonEmpty(item.WebIcon) ?? GameNames.NonEmpty(item.Icon)), item.Category)
            {
                Grams = item.weight,
                Price = item.price,
                RecipeUses = uses.TryGetValue(item.ID, out var count) ? count : 0,
            });
        }

        if (items.Count == 0) return (items, Array.Empty<CategoryName>());

        var read = (items, Categories(config));

        _read = read;
        _log(items.Count + " items read for the item pickers");

        return read;
    }

    /// <summary>An item's name in both languages for a message, its id when the table has no name for it.</summary>
    public Description NameOf(int id)
    {
        var (items, _) = Read();

        _names ??= items.Count > 0 ? items.ToDictionary(item => item.Id, item => item.Name) : null;

        return _names != null && _names.TryGetValue(id, out var name) ? name : new Description("#" + id, "#" + id);
    }

    /// <summary>
    /// Every recipe the game has now, mods' own included, read afresh each time: a mod rewrites its recipe rows when one
    /// of its settings changes. Empty while the game has not loaded its tables.
    /// </summary>
    public IReadOnlyList<GameRecipe> Recipes()
    {
        var recipes = GameSingleton.Existing<ConfigManager>()?._Config_ProductionList_Dict;

        if (recipes == null) return Array.Empty<GameRecipe>();

        try
        {
            var read = new List<GameRecipe>();

            foreach (var recipe in recipes.Values)
            {
                if (recipe?.MaterialList == null) continue;

                read.Add(new GameRecipe(Ids(recipe.ProductID), Ids(recipe.MaterialList)));
            }

            return read;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRecipes) _log("the recipes could not be read, so recipe lists are not checked against them: " + ex.Message);

            _hasReportedRecipes = true;

            return Array.Empty<GameRecipe>();
        }
    }

    private static int[] Ids(Il2CppSystem.Collections.Generic.List<int>? list)
    {
        if (list == null) return Array.Empty<int>();

        var ids = new int[list.Count];

        for (var i = 0; i < ids.Length; i++) ids[i] = list[i];

        return ids;
    }

    /// <summary>The materials of every recipe the game has now, mods' own recipes included.</summary>
    private IReadOnlyDictionary<int, int> RecipeUses(ConfigManager config)
    {
        var recipes = config._Config_ProductionList_Dict;

        if (recipes == null) return new Dictionary<int, int>();

        try
        {
            var lists = new List<int[]>();

            foreach (var recipe in recipes.Values)
            {
                var materials = recipe?.MaterialList;

                if (materials == null) continue;

                var ids = new int[materials.Count];

                for (var i = 0; i < ids.Length; i++) ids[i] = materials[i];

                lists.Add(ids);
            }

            return GameItems.RecipeUses(lists);
        }
        catch (Exception ex)
        {
            _log("the recipes could not be read, so the item pickers do not say which items recipes use: " + ex.Message);

            return new Dictionary<int, int>();
        }
    }

    /// <summary>The names the backpack shows on its category tabs, in both languages, else the enum's own name made readable.</summary>
    private static IReadOnlyList<CategoryName> Categories(ConfigManager config) =>
        Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>()
            .Where(category => (int)category > 0)
            .Select(category =>
            {
                var key = "ConstantText_Text_ItemType" + (int)category;
                var fallback = Wording.LabelFromKey(category.ToString());
                var english = GameNames.Translate(config, key, LanguageType.English) ?? fallback;

                return new CategoryName((int)category, new Description(english, GameNames.Translate(config, key, LanguageType.Chinese) ?? english));
            })
            .ToList();

    /// <summary>
    /// The icon files the game ships for its web UI, by path under the WebUI folder, which the Mods page can load as they
    /// are. The Toolkit walks the folder once for every mod, from the start; a walk not done yet is waited for, as this
    /// read once walked the folder itself.
    /// </summary>
    private IconIndex LoadIcons()
    {
        var (icons, note) = WebIcons.StartScan().Result;

        if (icons.Count == 0) _log("no item icons, the item pickers show names only: " + note);

        return icons;
    }
}
