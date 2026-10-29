using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>
/// One condition for showing a setting: the setting named by <see cref="Target"/> (a key, or "Section.Key") holds one
/// of <see cref="Values"/>, compared without case.
/// </summary>
public sealed record ShowRule(string Target, IReadOnlyList<string> Values);

/// <summary>What a setting's string tags ask of the Mods screen, plus the tags that could not be used.</summary>
public sealed record SettingTagsRead(SettingControl? Control, IReadOnlyList<ShowRule> ShowWhen, bool IsAdvanced, IReadOnlyList<string> Ignored)
{
    /// <summary>The Chinese name of each choice that has one, by the choice as the mod spells it.</summary>
    public IReadOnlyDictionary<string, string> ChoiceZh { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Reads the plain strings a mod puts in a setting's ConfigDescription tags, such as "ModManager.Control=Item". Strings
/// need no reference to the Mod Manager, so a mod that sets them runs the same without it.
/// </summary>
public static class SettingTags
{
    public const string ControlPrefix = ModInfoReader.Prefix + "Control=";
    public const string ShowWhenPrefix = ModInfoReader.Prefix + "ShowWhen=";
    public const string AdvancedTag = ModInfoReader.Prefix + "Advanced";
    public const string ChoiceZhPrefix = ModInfoReader.Prefix + "ChoiceZh=";

    public static SettingTagsRead Read(IEnumerable<object?>? tags)
    {
        SettingControl? control = null;
        var rules = new List<ShowRule>();
        var isAdvanced = false;
        var choiceZh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ignored = new List<string>();

        foreach (var tag in (tags ?? Array.Empty<object?>()).OfType<string>().Select(tag => tag.Trim()))
        {
            if (tag.StartsWith(ControlPrefix, StringComparison.Ordinal))
            {
                var name = tag.Substring(ControlPrefix.Length).Trim();

                if (ControlOf(name) is { } parsed) control ??= parsed;
                else ignored.Add(tag + ": the control must be one of " + string.Join(", ", Enum.GetNames(typeof(SettingControl))));
            }
            else if (tag.StartsWith(ShowWhenPrefix, StringComparison.Ordinal))
            {
                AddRules(rules, tag, ignored);
            }
            else if (tag == AdvancedTag)
            {
                isAdvanced = true;
            }
            else if (tag.StartsWith(ChoiceZhPrefix, StringComparison.Ordinal))
            {
                AddChoiceZh(choiceZh, tag, ignored);
            }
            else if (tag.StartsWith(ModInfoReader.Prefix, StringComparison.Ordinal) && tag != ApplyModes.LiveTag && tag != ApplyModes.RestartTag)
            {
                ignored.Add(tag + " is not a tag the Mod Manager knows");
            }
        }

        return new SettingTagsRead(control, rules, isAdvanced, ignored) { ChoiceZh = choiceZh };
    }

    private static SettingControl? ControlOf(string name)
    {
        foreach (SettingControl control in Enum.GetValues(typeof(SettingControl)))
        {
            if (control.ToString().Equals(name, StringComparison.OrdinalIgnoreCase)) return control;
        }

        return null;
    }

    /// <summary>"Choice=名称": the choice, as the mod spells it, up to the first "=", then its Chinese name.</summary>
    private static void AddChoiceZh(Dictionary<string, string> choiceZh, string tag, List<string> ignored)
    {
        var rest = tag.Substring(ChoiceZhPrefix.Length);
        var equals = rest.IndexOf('=');
        var choice = equals < 0 ? "" : rest.Substring(0, equals).Trim();
        var name = equals < 0 ? "" : rest.Substring(equals + 1).Trim();

        if (choice.Length > 0 && name.Length > 0) choiceZh[choice] = name;
        else ignored.Add(tag + " must be ChoiceZh=choice=name");
    }

    /// <summary>"Key" means that on/off setting is on; "Key=a|b" or "Section.Key=a|b" means it holds a or b. ";" joins several.</summary>
    private static void AddRules(List<ShowRule> rules, string tag, List<string> ignored)
    {
        var conditions = tag.Substring(ShowWhenPrefix.Length).Split(';').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();

        if (conditions.Count == 0) ignored.Add(tag + " names no setting");

        foreach (var condition in conditions)
        {
            var equals = condition.IndexOf('=');
            var target = (equals < 0 ? condition : condition.Substring(0, equals)).Trim();
            var values = equals < 0 ? new List<string> { "true" } : condition.Substring(equals + 1).Split('|').Select(value => value.Trim()).Where(value => value.Length > 0).ToList();

            if (target.Length > 0 && values.Count > 0) rules.Add(new ShowRule(target, values));
            else ignored.Add(tag + ": \"" + condition + "\" must be a setting's key, or key=value");
        }
    }
}

/// <summary>
/// Which settings their show-when rules let the page show. A setting is shown when every rule holds and every setting
/// a rule looks at is shown too, so a whole chain folds away together. A rule that names no setting is passed over,
/// and so is a loop, so a mistake in the rules never hides a setting for good.
/// </summary>
public static class ShowRules
{
    /// <summary>One answer per setting, in the order given.</summary>
    public static IReadOnlyList<bool> Shown(IReadOnlyList<FoundSetting> settings) => Evaluate(settings).Shown;

    /// <summary>What is wrong with the rules, for the log.</summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<FoundSetting> settings) => Evaluate(settings).Problems;

    private static (bool[] Shown, List<string> Problems) Evaluate(IReadOnlyList<FoundSetting> settings)
    {
        var shown = new bool?[settings.Count];
        var visiting = new HashSet<int>();
        var problems = new List<string>();

        for (var i = 0; i < settings.Count; i++) IsShown(i, settings, shown, visiting, problems);

        return (shown.Select(answer => answer ?? true).ToArray(), problems);
    }

    private static bool IsShown(int index, IReadOnlyList<FoundSetting> settings, bool?[] shown, HashSet<int> visiting, List<string> problems)
    {
        if (shown[index] is { } known) return known;

        var setting = settings[index];

        if (!visiting.Add(index))
        {
            problems.Add("the show-when rules of " + setting.Section + "." + setting.Key + " loop back to it, so the loop is passed over");

            return true;
        }

        var isShown = true;

        foreach (var rule in setting.Hints.ShowWhen)
        {
            var target = Find(rule.Target, setting.Section, settings);

            if (target < 0)
            {
                problems.Add("the show-when rule of " + setting.Section + "." + setting.Key + " names " + rule.Target + ", which is not a setting of this mod");
                continue;
            }

            var holds = rule.Values.Any(value => value.Equals(settings[target].Current, StringComparison.OrdinalIgnoreCase));

            if (target != index && !IsShown(target, settings, shown, visiting, problems)) holds = false;

            isShown &= holds;
        }

        visiting.Remove(index);
        shown[index] = isShown;

        return isShown;
    }

    /// <summary>A plain key in the setting's own section first, then in any section; then "Section.Key", split at the last dot.</summary>
    private static int Find(string target, string ownSection, IReadOnlyList<FoundSetting> settings)
    {
        var sameSection = IndexOf(settings, setting => setting.Section == ownSection && setting.Key == target);

        if (sameSection >= 0) return sameSection;

        var anySection = IndexOf(settings, setting => setting.Key == target);

        if (anySection >= 0) return anySection;

        var dot = target.LastIndexOf('.');

        if (dot <= 0) return -1;

        var section = target.Substring(0, dot).Trim();
        var key = target.Substring(dot + 1).Trim();

        return IndexOf(settings, setting => setting.Section == section && setting.Key == key);
    }

    private static int IndexOf(IReadOnlyList<FoundSetting> settings, Func<FoundSetting, bool> matches)
    {
        for (var i = 0; i < settings.Count; i++)
        {
            if (matches(settings[i])) return i;
        }

        return -1;
    }
}
