using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManager.Core;

/// <summary>What the wiki's index and Nexus Mods know about one installed mod.</summary>
public sealed record KnownFacts
{
    public static readonly KnownFacts None = new();

    /// <summary>Why the installed version is known to be broken; null when it is not.</summary>
    public BilingualText? Broken { get; init; }

    /// <summary>Why the update on offer is known to be broken, so it is not installed; null when it is not.</summary>
    public BilingualText? UpdateHeld { get; init; }

    /// <summary>The installed mods it is known not to work with.</summary>
    public IReadOnlyList<KnownConflict> Conflicts { get; init; } = Array.Empty<KnownConflict>();

    /// <summary>The mod on Nexus Mods, for mods that are not on the wiki; null when Nexus has no match for it.</summary>
    public NexusNewest? Nexus { get; init; }
}

public static class KnownFactsOf
{
    /// <param name="installed">The GUIDs of every installed mod, on or off: a conflict with a mod that is not there is not shown.</param>
    /// <param name="dllSha256">The SHA-256 of the mod's DLL, which names its release more surely than its version.</param>
    public static KnownFacts For(ModEntry mod, ModIndex index, NexusCache nexus, IReadOnlySet<string> installed, string? dllSha256)
    {
        var installedVersion = index.Identify(mod.Guid, mod.Version, dllSha256)?.Version ?? mod.Version;
        var isOurs = index.ModOf(mod.Guid) != null;

        return new KnownFacts
        {
            Broken = index.BrokenWhy(mod.Guid, installedVersion),
            UpdateHeld = mod.Update is UpdateState.Available available ? index.BrokenWhy(mod.Guid, available.Tag) : null,
            Conflicts = index.ConflictsOf(mod.Guid).Where(conflict => installed.Contains(conflict.OtherGuid)).ToList(),
            Nexus = isOurs ? null : NexusMatch.Find(nexus, index.NexusIdOf(mod.Guid), mod.Version, dllSha256),
        };
    }
}
