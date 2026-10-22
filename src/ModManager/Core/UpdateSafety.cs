using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ModManager.Core;

/// <summary>
/// Something a DLL's code can do that harmful code needs. Most mods use one or two of these for good reasons, so a
/// power alone means nothing; an update that gains one its installed version did not have is shown to the player first.
/// </summary>
public enum Power
{
    Network,
    StartsPrograms,
    NativeCode,
    LoadsOtherCode,
    Registry,
    AddsDll,

    /// <summary>A DLL in the update could not be read, so nothing is known about what it can do.</summary>
    Unreadable,
}

/// <summary>What a DLL uses from outside itself, read without loading it: type names, and members as "Type::Name".</summary>
public sealed record DllFacts(IReadOnlyCollection<string> Types, IReadOnlyCollection<string> Members, bool HasNativeImports, bool CarriesProgram);

/// <summary>
/// Reads powers from what a DLL references. Code that hides its calls behind reflection gets past this; it catches an
/// update that plainly starts doing something new, which is how most harmful mod updates have looked.
/// </summary>
public static class Powers
{
    private static readonly string[] NetworkNamespaces = { "System.Net.Http.", "System.Net.Sockets.", "System.Net.WebSockets.", "UnityEngine.Networking." };

    private static readonly HashSet<string> NetworkTypes = new(StringComparer.Ordinal)
    {
        "System.Net.WebClient", "System.Net.WebRequest", "System.Net.HttpWebRequest", "System.Net.FtpWebRequest", "System.Net.HttpListener", "System.Net.Dns", "UnityEngine.WWW",
    };

    private static readonly HashSet<string> ProgramTypes = new(StringComparer.Ordinal) { "System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo" };

    private static readonly HashSet<string> NativeTypes = new(StringComparer.Ordinal) { "System.Runtime.InteropServices.NativeLibrary" };

    private static readonly HashSet<string> NativeMembers = new(StringComparer.Ordinal) { "System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer" };

    private static readonly HashSet<string> LoadingMembers = new(StringComparer.Ordinal)
    {
        "System.Reflection.Assembly::Load", "System.Reflection.Assembly::LoadFrom", "System.Reflection.Assembly::LoadFile", "System.Reflection.Assembly::UnsafeLoadFrom",
        "System.AppDomain::Load",
        "System.Runtime.Loader.AssemblyLoadContext::LoadFromStream", "System.Runtime.Loader.AssemblyLoadContext::LoadFromAssemblyPath",
        "System.Runtime.Loader.AssemblyLoadContext::LoadFromNativeImagePath", "System.Runtime.Loader.AssemblyLoadContext::LoadUnmanagedDllFromPath",
    };

    private static readonly HashSet<string> RegistryTypes = new(StringComparer.Ordinal) { "Microsoft.Win32.Registry", "Microsoft.Win32.RegistryKey" };

    public static IReadOnlyCollection<Power> Of(DllFacts facts)
    {
        var types = facts.Types.Select(WithoutIl2CppPrefix).ToList();
        var members = facts.Members.Select(WithoutIl2CppPrefix).ToList();
        var powers = new HashSet<Power>();

        if (types.Any(IsNetwork)) powers.Add(Power.Network);

        if (types.Any(ProgramTypes.Contains)) powers.Add(Power.StartsPrograms);

        if (facts.HasNativeImports || types.Any(NativeTypes.Contains) || members.Any(NativeMembers.Contains)) powers.Add(Power.NativeCode);

        if (facts.CarriesProgram || members.Any(LoadingMembers.Contains)) powers.Add(Power.LoadsOtherCode);

        if (types.Any(RegistryTypes.Contains)) powers.Add(Power.Registry);

        return powers;
    }

    /// <summary>The powers <paramref name="after"/> has that <paramref name="before"/> lacks, in a fixed order. An unreadable DLL is always new: nothing says it is the same one.</summary>
    public static IReadOnlyList<Power> Added(IEnumerable<Power> before, IEnumerable<Power> after)
    {
        var had = before.ToHashSet();

        return after.Distinct().Where(power => power == Power.Unreadable || !had.Contains(power)).OrderBy(power => power).ToList();
    }

    private static bool IsNetwork(string type) => NetworkTypes.Contains(type) || NetworkNamespaces.Any(prefix => type.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>The game's own copies of .NET types are named Il2CppSystem.* and Il2CppMicrosoft.*, and do the same things.</summary>
    private static string WithoutIl2CppPrefix(string name) =>
        name.StartsWith("Il2CppSystem.", StringComparison.Ordinal) || name.StartsWith("Il2CppMicrosoft.", StringComparison.Ordinal) ? name.Substring("Il2Cpp".Length) : name;
}

/// <summary>One DLL as read for an update: the BepInEx plugins it declares and what it uses, or why it could not be read.</summary>
public abstract record DllScan
{
    private DllScan()
    {
    }

    public sealed record Read(IReadOnlyList<string> PluginGuids, DllFacts Facts) : DllScan;

    public sealed record Unreadable(string Reason) : DllScan;

    public IReadOnlyCollection<Power> Powers => this is Read read ? Core.Powers.Of(read.Facts) : new[] { Power.Unreadable };
}

/// <summary>A DLL the update writes, and whether the mod's folder already has a file there.</summary>
public sealed record IncomingDll(DllScan Scan, bool IsNew);

/// <summary>What the checks on a downloaded update decided.</summary>
public abstract record UpdateVerdict
{
    private UpdateVerdict()
    {
    }

    public sealed record Clear : UpdateVerdict;

    public sealed record NeedsApproval(IReadOnlyList<Power> NewPowers) : UpdateVerdict;

    public sealed record Rejected(string Reason) : UpdateVerdict;
}

public static class UpdateReview
{
    /// <param name="newModDll">The mod's own DLL from the update.</param>
    /// <param name="installed">The installed DLLs the update writes over, the mod's own among them.</param>
    /// <param name="incoming">Every DLL the update writes, the mod's own among them.</param>
    public static UpdateVerdict Judge(string guid, DllScan newModDll, IEnumerable<DllScan> installed, IEnumerable<IncomingDll> incoming)
    {
        switch (newModDll)
        {
            case DllScan.Unreadable unreadable:
                return new UpdateVerdict.Rejected("its DLL could not be read (" + unreadable.Reason + ")");
            case DllScan.Read read when !read.PluginGuids.Contains(guid, StringComparer.Ordinal):
                return new UpdateVerdict.Rejected(read.PluginGuids.Count == 0 ? "its DLL holds no BepInEx plugin" : "its DLL is a different mod (" + string.Join(", ", read.PluginGuids) + ")");
        }

        var incomingList = incoming.ToList();
        var before = installed.SelectMany(scan => scan.Powers);
        var after = incomingList.SelectMany(dll => dll.Scan.Powers).Concat(incomingList.Any(dll => dll.IsNew) ? new[] { Power.AddsDll } : Array.Empty<Power>());
        var added = Powers.Added(before, after);
        var refused = added.Where(Refused.Contains).ToList();

        if (refused.Count > 0) return new UpdateVerdict.Rejected("it newly " + string.Join(" and ", refused.Select(Describe)) + ", which no update is allowed to start doing");

        return added.Count == 0 ? new UpdateVerdict.Clear() : new UpdateVerdict.NeedsApproval(added);
    }

    /// <summary>
    /// Powers harmful code leans on and a game mod has no need to pick up in an update. A mod that already had one when
    /// it was installed keeps updating; only gaining it is refused.
    /// </summary>
    private static readonly HashSet<Power> Refused = new() { Power.StartsPrograms, Power.LoadsOtherCode, Power.Registry };

    private static string Describe(Power power) => power switch
    {
        Power.StartsPrograms => "starts programs",
        Power.LoadsOtherCode => "loads other code",
        Power.Registry => "uses the Windows registry",
        _ => power.ToString(),
    };
}

/// <summary>
/// The GitHub repository a mod's updates come from, pinned by GitHub's number for it at its first install. A renamed
/// repository keeps its number, so updates keep working; one deleted and made again under the same name by someone
/// else gets a new number, and its releases are not installed.
/// </summary>
public static class RepositoryPin
{
    public static Uri RepositoryUri(LatestRelease.Found release) => new("https://api.github.com/repos/" + release.Owner + "/" + release.Repository);

    /// <summary>The release with this tag in the pinned repository, whatever it is called now.</summary>
    public static Uri ReleaseUri(long repositoryId, string tag) =>
        new("https://api.github.com/repositories/" + repositoryId.ToString(CultureInfo.InvariantCulture) + "/releases/tags/" + Uri.EscapeDataString(tag));

    /// <summary>The number in GitHub's answer about a repository, or null when there is none.</summary>
    public static long? IdFrom(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var value) && value > 0 ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A saved pin: the number alone. Anything else counts as no pin.</summary>
    public static long? Parse(string text) =>
        long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    public static string Format(long repositoryId) => repositoryId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Why a release could not be fetched from the pinned repository, as the player reads it.</summary>
    public static string NotInPinned(string tag) =>
        "release " + tag + " is not in the GitHub repository this mod's earlier updates came from, so that repository may have been replaced. If you trust it, use GET UPDATE to install it by hand";
}
