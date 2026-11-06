using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ModManager.Core;

/// <summary>A recipe from the game's table: the items it makes, and its materials with each id once per piece.</summary>
public sealed record GameRecipe(IReadOnlyList<int> Products, IReadOnlyList<int> Materials);

/// <summary>
/// The game cannot have two recipes with the same materials, so a mod has to drop a recipe list that matches another
/// recipe. A recipe list (an ItemCounts setting with a Makes tag) whose mod does not check it through ModManagerAnswers
/// is checked here, against the game's recipes and the mod's other recipe lists, so the Mods screen can say why at the
/// list instead of the mod saying it only in its log.
/// </summary>
public static class RecipeClash
{
    /// <summary>Whether any of a mod's settings is a recipe list this checks, so the game's recipes are read only then.</summary>
    public static bool IsNeeded(IEnumerable<FoundSetting> settings) => settings.Any(NeedsCheck);

    /// <summary>The settings with a problem added to each recipe list whose materials another recipe already has.</summary>
    /// <param name="nameOf">An item's name in both languages, for the message.</param>
    public static IReadOnlyList<FoundSetting> Check(IReadOnlyList<FoundSetting> settings, IReadOnlyList<GameRecipe> recipes, Func<int, Description> nameOf)
    {
        if (!IsNeeded(settings)) return settings;

        return settings.Select(setting => Clash(setting, settings, recipes) is { } other ? setting with { Answer = setting.Answer with { Problem = Problem(nameOf(other)) } } : setting).ToList();
    }

    /// <summary>
    /// The item whose recipe has the same materials as this list: another recipe list of the same mod first, then any
    /// recipe in the game's table that does not make this list's item. Null when there is none, or the list is not
    /// one this checks.
    /// </summary>
    public static int? Clash(FoundSetting setting, IReadOnlyList<FoundSetting> settings, IReadOnlyList<GameRecipe> recipes)
    {
        if (!NeedsCheck(setting) || setting.Hints.Makes is not { } makes || Pieces(setting) is not { Length: > 0 } pieces) return null;

        foreach (var other in settings)
        {
            if (ReferenceEquals(other, setting) || !NeedsCheck(other) || other.Hints.Makes is not { } otherMakes || otherMakes == makes) continue;

            if (Pieces(other) is { } theirs && theirs.SequenceEqual(pieces)) return otherMakes;
        }

        foreach (var recipe in recipes)
        {
            if (recipe.Products.Contains(makes) || recipe.Materials.Count != pieces.Length) continue;

            if (recipe.Materials.OrderBy(id => id).SequenceEqual(pieces)) return recipe.Products.Count > 0 ? recipe.Products[0] : null;
        }

        return null;
    }

    /// <summary>
    /// "20004:3, 20362:2" as pieces in order of id: 20004, 20004, 20004, 20362, 20362, the way the recipe table holds
    /// them. Null when a part cannot be read; the mod says what is wrong with that.
    /// </summary>
    public static int[]? Pieces(string value)
    {
        var pieces = new List<int>();

        foreach (var part in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()).Where(part => part.Length > 0))
        {
            var pair = part.Split(':');
            var count = 1;

            if (pair.Length > 2 || !int.TryParse(pair[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0) return null;

            if (pair.Length == 2 && (!int.TryParse(pair[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1)) return null;

            pieces.AddRange(Enumerable.Repeat(id, count));
        }

        pieces.Sort();

        return pieces.ToArray();
    }

    public static Description Problem(Description other) => new(
        "Same materials as the " + other.English + " recipe. The game cannot have two recipes with the same materials, so this list is not used.",
        "和「" + other.Chinese + "」的配方材料相同。游戏里两个配方不能用相同的材料，所以这个清单不会被使用。");

    /// <summary>A recipe list its mod does not check itself. A blank list the mod fills in is the mod's own choice, so it is left out.</summary>
    private static bool NeedsCheck(FoundSetting setting) =>
        setting.Kind is SettingKind.ItemCounts counts && setting.Hints.Makes != null && !setting.Answer.IsChecked && !(counts.AutoWhenBlank && IsAutoValue(setting.Current));

    private static bool IsAutoValue(string value) => value.Trim().Length == 0 || value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);

    private static int[]? Pieces(FoundSetting setting) => Pieces(setting.Current);
}
