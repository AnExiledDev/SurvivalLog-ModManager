using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExiledToolkit.Kit;

namespace ModManager.Core;

/// <summary>A mod in a profile, and whether the profile has it on.</summary>
/// <param name="SettingsKnown">Its settings were read when the profile was saved; a mod that was not running has none to keep, so using the profile leaves its settings alone.</param>
public sealed record ProfileMod(string Guid, string Name, string Version, bool IsOn, bool SettingsKnown = true);

/// <summary>A setting a profile changes from its default, written as in the .cfg file.</summary>
public sealed record ProfileSetting(string Guid, string Section, string Key, string Value)
{
    public SettingId Id => new(Guid, Section, Key);
}

/// <summary>
/// A named setup: which mods are on and off, and every setting changed from its default. A pasted mod list carries
/// no settings, so using it leaves the settings alone.
/// </summary>
public sealed record Profile(string Name, IReadOnlyList<ProfileMod> Mods, IReadOnlyList<ProfileSetting> Settings, bool CarriesSettings)
{
    public const int MaxNameLength = 60;

    /// <summary>The setup as it will be at the next start. An uninstalled mod is left out, and so are read-only settings.</summary>
    public static Profile Of(string name, IEnumerable<ModEntry> mods)
    {
        var kept = mods.Where(mod => mod.Switch is not ModSwitch.Uninstalled).ToList();
        var settings = kept
            .SelectMany(mod => mod.State is ModState.Loaded loaded ? loaded.Settings : Array.Empty<Setting>())
            .Where(setting => setting.IsChanged && !setting.IsReadOnly)
            .Select(setting => new ProfileSetting(setting.Id.Guid, setting.Id.Section, setting.Id.Key, setting.Current))
            .ToList();

        return new Profile(CleanName(name), kept.Select(mod => new ProfileMod(mod.Guid, mod.DisplayName, mod.Version, mod.IsOnNextStart, mod.State is ModState.Loaded)).ToList(), settings, true);
    }

    /// <summary>One line, no longer than a name needs to be; blank becomes "Profile".</summary>
    public static string CleanName(string name)
    {
        var line = string.Join(" ", (name ?? "").Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();

        if (line.Length > MaxNameLength) line = line.Substring(0, MaxNameLength).TrimEnd();

        return line.Length == 0 ? "Profile" : line;
    }
}

/// <summary>
/// A profile as short text a player can paste in a chat: "SLMM1-" and the profile, packed by the Toolkit's
/// <see cref="ShareCodec"/>. Reading one never trusts its size: a code that unpacks to more than a profile can hold is
/// refused, so a pasted code cannot fill the memory.
/// </summary>
public static class ShareCode
{
    public const string Prefix = "SLMM1-";
    public const int MaxCodeLength = 200_000;
    public const int MaxUnpackedBytes = 1_000_000;
    public const int MaxMods = 1000;
    public const int MaxSettings = 10_000;

    public static string Write(Profile profile)
    {
        var text = new StringBuilder();

        text.Append("n\t").Append(ShareCodec.EscapeField(profile.Name)).Append('\n');

        foreach (var mod in profile.Mods) text.Append("m\t").Append(ShareCodec.EscapeField(mod.Guid)).Append('\t').Append(ShareCodec.EscapeField(mod.Name)).Append('\t').Append(ShareCodec.EscapeField(mod.Version)).Append('\t').Append(mod.IsOn ? '1' : '0').Append('\t').Append(mod.SettingsKnown ? '1' : '0').Append('\n');

        foreach (var setting in profile.Settings) text.Append("s\t").Append(ShareCodec.EscapeField(setting.Guid)).Append('\t').Append(ShareCodec.EscapeField(setting.Section)).Append('\t').Append(ShareCodec.EscapeField(setting.Key)).Append('\t').Append(ShareCodec.EscapeField(setting.Value)).Append('\n');

        // The last line counts the lines above it, so a code cut short in a chat is refused rather than read in part.
        text.Append("e\t").Append(profile.Mods.Count + profile.Settings.Count).Append('\n');

        return ShareCodec.Pack(Prefix, text.ToString());
    }

    /// <summary>The profile in a pasted text: a share code anywhere in it, else a list from COPY MOD LIST.</summary>
    public static (Profile? Profile, BilingualText? Problem) Read(string pasted)
    {
        var (text, problem) = ShareCodec.Unpack(pasted, Prefix, MaxCodeLength, MaxUnpackedBytes);

        return problem switch
        {
            null => Lines(text ?? ""),
            ShareProblem.Empty => (null, Problems.Empty),
            ShareProblem.NotACode => ModList.Read(pasted) is { } list ? (list, null) : (null, Problems.NotACode),
            ShareProblem.Broken => (null, Problems.Broken),
            _ => (null, Problems.TooBig),
        };
    }

    private static (Profile?, BilingualText?) Lines(string text)
    {
        string? name = null;
        int? ends = null;
        var mods = new List<ProfileMod>();
        var settings = new List<ProfileSetting>();

        foreach (var line in text.Split('\n'))
        {
            var parts = line.Split('\t').Select(ShareCodec.UnescapeField).ToArray();

            switch (parts[0])
            {
                case "n" when parts.Length == 2:
                    name = parts[1];
                    break;
                case "m" when parts.Length == 6 && parts[4] is "0" or "1" && parts[1].Length > 0:
                    mods.Add(new ProfileMod(parts[1], parts[2], parts[3], parts[4] == "1", parts[5] == "1"));
                    break;
                case "s" when parts.Length == 5 && parts[1].Length > 0:
                    settings.Add(new ProfileSetting(parts[1], parts[2], parts[3], parts[4]));
                    break;
                case "e" when parts.Length == 2 && int.TryParse(parts[1], out var count):
                    ends = count;
                    break;
            }

            if (mods.Count > MaxMods || settings.Count > MaxSettings) return (null, Problems.TooBig);
        }

        if (name == null || ends != mods.Count + settings.Count) return (null, Problems.Broken);

        // A mod named twice keeps its first line, so the plan never turns one mod both on and off.
        var distinctMods = mods.GroupBy(mod => mod.Guid, StringComparer.Ordinal).Select(group => group.First()).ToList();
        var distinctSettings = settings.GroupBy(setting => setting.Id).Select(group => group.First()).ToList();

        return (new Profile(Profile.CleanName(name), distinctMods, distinctSettings, true), null);
    }

    public static class Problems
    {
        public static readonly BilingualText Empty = new("Paste a profile code or a mod list first.", "请先粘贴配置方案代码或模组列表。");
        public static readonly BilingualText NotACode = new("This is not a profile code or a list from COPY MOD LIST.", "这不是配置方案代码，也不是「复制模组列表」复制的列表。");
        public static readonly BilingualText Broken = new("The code is cut off or changed. Copy it again from where it was shared.", "代码不完整或已被改动。请从分享处重新复制。");
        public static readonly BilingualText TooBig = new("This is far bigger than any profile, so it was not read.", "内容远大于任何配置方案，因此没有读取。");
    }

    /// <summary>Reads the list COPY MOD LIST makes, so a list pasted in a bug report or a chat works as a profile too.</summary>
    private static class ModList
    {
        private static readonly Regex Line = new("^- (.+) (\\S+) \\(([^,\\s()]+), [^()]+\\): (running|failed to load|off)(, (?:on|off|uninstalled) from the next start)?\\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex Game = new("^Survival Log (\\d\\S*)", RegexOptions.CultureInvariant);

        public static Profile? Read(string text)
        {
            var mods = new List<ProfileMod>();
            var game = "";
            var isInMods = false;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r').Trim();

                if (Game.Match(line) is { Success: true } version) game = version.Groups[1].Value;

                if (line.StartsWith("Mods (", StringComparison.Ordinal))
                {
                    isInMods = true;

                    continue;
                }

                if (!isInMods) continue;

                if (Line.Match(line) is not { Success: true } mod)
                {
                    if (!line.StartsWith("- ", StringComparison.Ordinal)) isInMods = false;

                    continue;
                }

                var next = mod.Groups[5].Value;
                var isOn = next.Length > 0 ? next.Contains(", on from") : mod.Groups[4].Value != "off";

                if (next.Contains("uninstalled")) continue;

                if (mods.Count >= MaxMods) return null;

                if (mods.All(have => have.Guid != mod.Groups[3].Value)) mods.Add(new ProfileMod(mod.Groups[3].Value, mod.Groups[1].Value, mod.Groups[2].Value, isOn));
            }

            return mods.Count == 0 ? null : new Profile(Profile.CleanName(game.Length > 0 ? "Mod list (Survival Log " + game + ")" : "Mod list"), mods, Array.Empty<ProfileSetting>(), false);
        }
    }
}

/// <summary>One thing using a profile would change, or could not.</summary>
public abstract record ProfileStep
{
    private ProfileStep()
    {
    }

    public sealed record TurnOn(string Guid, string Name) : ProfileStep;

    public sealed record TurnOff(string Guid, string Name) : ProfileStep;

    /// <summary>Not installed, and BROWSE can install it.</summary>
    public sealed record Install(string Guid, string Name) : ProfileStep
    {
        /// <summary>How far its install from BROWSE has got, once USE started it.</summary>
        public InstallState State { get; init; } = new InstallState.None();
    }

    /// <summary>Not installed, and it has to be downloaded by hand.</summary>
    /// <param name="NexusId">Its Nexus page, when the wiki or Nexus knows it.</param>
    public sealed record Missing(string Guid, string Name, int? NexusId) : ProfileStep;

    /// <summary>A setting that changes, live, to the profile's value (its default, when the profile leaves it unchanged).</summary>
    public sealed record Set(SettingId Id, string ModName, string Label, string From, string To) : ProfileStep;

    /// <summary>The profile changes settings of a mod that is not running now, so they are set when it runs.</summary>
    public sealed record SettingsWait(string Guid, string Name, int Count) : ProfileStep;

    /// <summary>The installed version differs from the profile's, which may change what its settings mean.</summary>
    public sealed record OtherVersion(string Guid, string Name, string Installed, string InProfile) : ProfileStep;

    /// <summary>The profile turns it off, but it cannot be turned off from here (the Mod Manager, or a mod it needs).</summary>
    public sealed record StaysOn(string Guid, string Name) : ProfileStep;
}

/// <summary>A profile the player opened to see what it changes: a saved one, or one pasted in.</summary>
public sealed record ProfileCheck(Profile Profile, bool IsPasted, IReadOnlyList<ProfileStep> Steps);

/// <summary>What the PROFILES page shows.</summary>
/// <param name="Problem">Why the last pasted text could not be read.</param>
/// <param name="Note">What the last button did.</param>
public sealed record ProfilesView(IReadOnlyList<Profile> Saved, ProfileCheck? Check, BilingualText? Problem, BilingualText? Note)
{
    public static readonly ProfilesView Empty = new(Array.Empty<Profile>(), null, null, null);
}

public static class ProfileNotes
{
    public static readonly BilingualText Full = new("There are " + ProfileShelf.MaxProfiles + " profiles already. Delete one first.", "已有 " + ProfileShelf.MaxProfiles + " 个配置方案，请先删除一个。");

    public static BilingualText Saved(string name, bool isNew) => new((isNew ? "Saved" : "Replaced") + " the profile \"" + name + "\".", (isNew ? "已保存" : "已替换") + "配置方案「" + name + "」。");

    public static BilingualText Deleted(string name) => new("Deleted the profile \"" + name + "\".", "已删除配置方案「" + name + "」。");

    /// <summary>Shown on PROFILES while the saved profiles can't be read.</summary>
    public static readonly BilingualText Unreadable = new("Could not read your saved profiles. Close any program that has profiles.txt open and try again.", "无法读取已保存的配置方案。请关闭打开了 profiles.txt 的程序后重试。");

    /// <summary>A save or delete refused because the saved profiles can't be read.</summary>
    public static readonly BilingualText NotRead = new("Could not read the saved profiles, so nothing was changed. Close any program that has profiles.txt open and try again.", "无法读取已保存的配置方案，因此没有做任何更改。请关闭打开了 profiles.txt 的程序后重试。");

    public static readonly BilingualText NotSaved = new("Could not save the profiles. The log says why.", "无法保存配置方案，原因见日志。");

    public static readonly BilingualText TooBig = new("This setup is too big for a profile, so it was not saved.", "这套配置太大，无法存为配置方案，因此没有保存。");

    /// <summary>What USE did, and what it could not.</summary>
    public static BilingualText Used(string name, int switched, int installs, int settings, IReadOnlyList<string> failed)
    {
        var en = new List<string>();
        var zh = new List<string>();

        if (switched > 0)
        {
            en.Add(Count(switched, "mod", "mods") + " turned on or off from the next start");
            zh.Add(switched + " 个模组从下次启动起开启或关闭");
        }

        if (installs > 0)
        {
            en.Add(Count(installs, "mod", "mods") + " installing for the next start");
            zh.Add(installs + " 个模组正在安装，下次启动生效");
        }

        if (settings > 0)
        {
            en.Add(Count(settings, "setting", "settings") + " changed now");
            zh.Add(settings + " 项设置已更改");
        }

        var english = "Used \"" + name + "\": " + (en.Count == 0 ? "nothing needed changing" : string.Join(", ", en)) + ".";
        var chinese = "已使用「" + name + "」：" + (zh.Count == 0 ? "无需更改" : string.Join("，", zh)) + "。";

        if (failed.Count > 0)
        {
            english += " Could not change: " + string.Join(", ", failed) + ".";
            chinese += "无法更改：" + string.Join("、", failed) + "。";
        }

        return new BilingualText(english, chinese);
    }

    private static string Count(int count, string one, string many) => count + " " + (count == 1 ? one : many);
}

public static class ProfilePlan
{
    /// <summary>
    /// What using the profile would change. Mods the profile does not name are left as they are; for a profile with
    /// settings, a running mod it names gets exactly the profile's settings, so its other changed settings go back to
    /// their defaults. That holds only for a mod whose settings the profile read; one that was not running is left alone.
    /// </summary>
    /// <param name="canInstall">Whether BROWSE can install a mod that is not installed.</param>
    /// <param name="nexusIdOf">The Nexus page of a mod that is not installed, when known.</param>
    public static IReadOnlyList<ProfileStep> For(Profile profile, IReadOnlyList<ModEntry> mods, Func<string, bool> canInstall, Func<string, int?> nexusIdOf)
    {
        var steps = new List<ProfileStep>();
        var byGuid = mods.Where(mod => mod.Switch is not ModSwitch.Uninstalled).GroupBy(mod => mod.Guid, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var wanted in profile.Mods)
        {
            if (!byGuid.TryGetValue(wanted.Guid, out var mod))
            {
                if (!wanted.IsOn) continue;

                steps.Add(canInstall(wanted.Guid) ? new ProfileStep.Install(wanted.Guid, wanted.Name) : new ProfileStep.Missing(wanted.Guid, wanted.Name, nexusIdOf(wanted.Guid)));

                continue;
            }

            if (wanted.IsOn != mod.IsOnNextStart)
            {
                if (wanted.IsOn) steps.Add(new ProfileStep.TurnOn(mod.Guid, mod.DisplayName));
                else if (mod.Switch is ModSwitch.Allowed or ModSwitch.Held) steps.Add(new ProfileStep.TurnOff(mod.Guid, mod.DisplayName));
                else steps.Add(new ProfileStep.StaysOn(mod.Guid, mod.DisplayName));
            }

            if (wanted.IsOn && wanted.Version.Length > 0 && wanted.Version != mod.Version) steps.Add(new ProfileStep.OtherVersion(mod.Guid, mod.DisplayName, mod.Version, wanted.Version));
        }

        if (profile.CarriesSettings) steps.AddRange(SettingSteps(profile, byGuid));

        return steps;
    }

    private static IEnumerable<ProfileStep> SettingSteps(Profile profile, IReadOnlyDictionary<string, ModEntry> byGuid)
    {
        var wantedOf = profile.Settings.ToDictionary(setting => setting.Id);

        foreach (var wanted in profile.Mods.Where(wanted => wanted.IsOn))
        {
            var count = profile.Settings.Count(setting => setting.Guid == wanted.Guid);

            if (!wanted.SettingsKnown) continue;

            if (!byGuid.TryGetValue(wanted.Guid, out var mod) || mod.State is not ModState.Loaded loaded)
            {
                if (count > 0) yield return new ProfileStep.SettingsWait(wanted.Guid, mod?.DisplayName ?? wanted.Name, count);

                continue;
            }

            // A setting the mod no longer has is skipped: the profile came from another version of it.
            foreach (var setting in loaded.Settings.Where(setting => !setting.IsReadOnly))
            {
                var to = wantedOf.TryGetValue(setting.Id, out var value) ? value.Value : setting.Default;

                if (to != setting.Current) yield return new ProfileStep.Set(setting.Id, mod.DisplayName, setting.Label, setting.Current, to);
            }
        }
    }
}

/// <summary>
/// The player's saved profiles, one share code per line in a text file, so the file can be read, backed up and
/// shared as it is. A line that does not read is skipped.
/// </summary>
public static class ProfileShelf
{
    public const int MaxProfiles = 100;

    public static IReadOnlyList<Profile> Read(string? text) =>
        (text ?? "").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(ShareCode.Prefix, StringComparison.Ordinal))
            .Select(line => ShareCode.Read(line).Profile)
            .OfType<Profile>()
            .Take(MaxProfiles)
            .ToList();

    /// <summary>
    /// The lines of a saved file that do not read as a profile (a code a newer version wrote, or one changed by hand).
    /// They are written back as they were, so saving a profile never loses them.
    /// </summary>
    public static IReadOnlyList<string> Unread(string? text) =>
        (text ?? "").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !(line.StartsWith(ShareCode.Prefix, StringComparison.Ordinal) && ShareCode.Read(line).Profile != null))
            .ToList();

    public static string Write(IEnumerable<Profile> profiles, IEnumerable<string>? unread = null) =>
        string.Concat(profiles.Take(MaxProfiles).Select(profile => ShareCode.Write(profile) + "\n").Concat((unread ?? Array.Empty<string>()).Select(line => line + "\n")));

    /// <summary>Whether the profile's share code reads back, so a profile too big to share is never saved.</summary>
    public static bool FitsACode(Profile profile) => ShareCode.Read(ShareCode.Write(profile)).Profile != null;

    /// <summary>The shelf with the profile added, replacing one of the same name (any case).</summary>
    public static IReadOnlyList<Profile> Put(IReadOnlyList<Profile> shelf, Profile profile) =>
        shelf.Where(have => !string.Equals(have.Name, profile.Name, StringComparison.OrdinalIgnoreCase)).Append(profile).OrderBy(have => have.Name, StringComparer.OrdinalIgnoreCase).Take(MaxProfiles).ToList();

    public static IReadOnlyList<Profile> Remove(IReadOnlyList<Profile> shelf, string name) =>
        shelf.Where(have => !string.Equals(have.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
}
