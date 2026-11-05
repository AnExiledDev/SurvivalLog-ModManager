using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var english = Text(config, item.ItemName, LanguageType.English);
            var chinese = Text(config, item.ItemName, LanguageType.Chinese) ?? NonEmpty(item.ItemName_Local);

            // An item whose name is still the raw key in both languages is left out.
            if (english == null && chinese == null) continue;

            items.Add(new CatalogItem(item.ID, new Description(english ?? chinese!, chinese ?? english!), _icons.Resolve(NonEmpty(item.WebIcon) ?? NonEmpty(item.Icon)), item.Category)
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
                var english = Text(config, key, LanguageType.English) ?? fallback;

                return new CategoryName((int)category, new Description(english, Text(config, key, LanguageType.Chinese) ?? english));
            })
            .ToList();

    /// <summary>The icon files the game ships for its web UI, by path under the WebUI folder, which the Mods page can load as they are.</summary>
    private IconIndex LoadIcons()
    {
        var webUi = Path.Combine(Application.streamingAssetsPath, "WebUI");

        try
        {
            return IconIndex.Build(Directory.EnumerateFiles(Path.Combine(webUi, "Res"), "*.png", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(webUi, file)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log("no item icons, the item pickers show names only: " + ex.Message);

            return IconIndex.Empty;
        }
    }

    /// <summary>Null when the game has no text for the key in that language, so a name that is still the raw key is not shown.</summary>
    private static string? Text(ConfigManager config, string? key, LanguageType language)
    {
        if (string.IsNullOrEmpty(key)) return null;

        try
        {
            var text = config.GetLocalTxtByLanguage(key, language);

            return string.IsNullOrWhiteSpace(text) || text == key ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}

/// <summary>
/// The game's singletons build themselves on first read, and one built before the game makes it breaks the game for
/// the rest of the session, so only one the game already made is read.
/// </summary>
internal static class GameSingleton
{
    public static T? Existing<T>() where T : BaseSingleton<T>, new() =>
        BaseSingleton<T>.IsInstanceCreated ? BaseSingleton<T>.Instance : null;
}
