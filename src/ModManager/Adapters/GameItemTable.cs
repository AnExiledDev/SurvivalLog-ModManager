using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCore.HotUpdate;
using ModManager.Core;
using UnityEngine;

namespace ModManager.Adapters;

/// <summary>
/// The game's items and item categories, named in the game's language, for the item pickers. Read only once the game
/// has built its ConfigManager, and kept per language.
/// </summary>
internal sealed class GameItemTable
{
    private readonly Action<string> _log;
    private readonly Dictionary<bool, (IReadOnlyList<CatalogItem> Items, IReadOnlyList<CategoryName> Categories)> _byLanguage = new();
    private IconIndex? _icons;

    public GameItemTable(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Empty lists while the game has not loaded its tables yet; those are not kept, so a later call tries again.</summary>
    public (IReadOnlyList<CatalogItem> Items, IReadOnlyList<CategoryName> Categories) Read(bool isChinese)
    {
        if (_byLanguage.TryGetValue(isChinese, out var known)) return known;

        var config = GameSingleton.Existing<ConfigManager>();
        var table = config?._Config_Item_Dict;

        if (config == null || table == null) return (Array.Empty<CatalogItem>(), Array.Empty<CategoryName>());

        _icons ??= LoadIcons();

        var items = new List<CatalogItem>();

        foreach (var item in table.Values)
        {
            var name = item == null ? null : Translate(config, item.ItemName) ?? NonEmpty(item.ItemName_Local);

            if (item != null && name != null) items.Add(new CatalogItem(item.ID, name, _icons.Resolve(NonEmpty(item.WebIcon) ?? NonEmpty(item.Icon)), item.Category));
        }

        if (items.Count == 0) return (items, Array.Empty<CategoryName>());

        var read = (items, Categories());

        _byLanguage[isChinese] = read;
        _log(items.Count + " items read for the item pickers");

        return read;
    }

    /// <summary>The names the backpack shows on its category tabs ("ItemType" and the number), else the enum's own name made readable.</summary>
    private static IReadOnlyList<CategoryName> Categories() =>
        Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>()
            .Where(category => (int)category > 0)
            .Select(category => new CategoryName((int)category, NonEmpty(ConstantTextTools.ToConstantTextOrEmpty("ItemType" + (int)category)) ?? Wording.LabelFromKey(category.ToString())))
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

    /// <summary>Null when the game has no text for the key, so an item whose name is still the raw key is left out.</summary>
    private static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var text = config.GetLocalTxt(key);

        return string.IsNullOrEmpty(text) || text == key ? null : text;
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
