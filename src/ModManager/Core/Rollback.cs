using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModManager.Core;

/// <summary>Whether a mod can go back to the files an earlier update replaced. The old files are used from the next game start.</summary>
public abstract record RollbackState
{
    private RollbackState()
    {
    }

    public sealed record None : RollbackState;

    public sealed record Available(string Version) : RollbackState;

    public sealed record Done(string Version) : RollbackState;

    public sealed record Failed(string Version, string Reason) : RollbackState;
}

/// <summary>Backups live in one folder per mod, then one per version: the version whose files an install replaced.</summary>
public static class BackupFolder
{
    /// <summary>A folder name made from a GUID or version, which could otherwise hold a backslash or be "..".</summary>
    public static string Name(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(text.Select(c => invalid.Contains(c) ? '_' : c));

        return safe.Trim('.').Length == 0 ? "_" : safe;
    }

    /// <summary>
    /// Whether a backup name stands for a version. An install names the list of files it placed after the release tag
    /// ("v1.2.0"), while the game later reports the plugin's version ("1.2.0"), so equal versions match too.
    /// </summary>
    public static bool IsSameVersion(string name, string version) =>
        string.Equals(name, Name(version), StringComparison.OrdinalIgnoreCase) || (ReleaseVersion.Parse(name) is { } named && ReleaseVersion.Parse(version) is { } wanted && named.CompareTo(wanted) == 0);
}

/// <summary>Which backup a mod rolls back to.</summary>
public static class Rollback
{
    /// <summary>
    /// After an install this session, the files it replaced: the backup of the running version. Otherwise the newest
    /// backup older than the running version, which is what the last update replaced. Null while an install runs.
    /// </summary>
    public static string? Target(IReadOnlyList<string> backupVersions, string runningVersion, InstallState install)
    {
        switch (install)
        {
            case InstallState.Installing:
                return null;
            case InstallState.Installed:
                return backupVersions.FirstOrDefault(name => BackupFolder.IsSameVersion(name, runningVersion));
        }

        var installed = ReleaseVersion.Parse(runningVersion);

        if (installed == null) return null;

        return backupVersions
            .Select(name => (Name: name, Version: ReleaseVersion.Parse(name)))
            .Where(backup => backup.Version != null && backup.Version.CompareTo(installed) < 0)
            .OrderByDescending(backup => backup.Version)
            .Select(backup => backup.Name)
            .FirstOrDefault();
    }

    /// <summary>The version a rollback button would go to, or null when there is nothing to try.</summary>
    public static string? VersionToTry(RollbackState state) => state switch
    {
        RollbackState.Available available => available.Version,
        RollbackState.Failed failed => failed.Version,
        _ => null,
    };
}

/// <summary>A file to move or copy, by full path.</summary>
public sealed record FileStep(string From, string To);

/// <summary>The files to set aside and the backed-up files to copy back in their place.</summary>
public sealed record RollbackSteps(IReadOnlyList<FileStep> SetAside, IReadOnlyList<FileStep> CopyBack);

/// <summary>
/// Plans a rollback. The mod's current files (those its install placed, and those the backup has) are moved to a
/// backup of their own, then the backup's files are copied in. The backup is copied, not moved, so it stays.
/// </summary>
public static class RollbackPlan
{
    /// <param name="modFolder">The folder of the mod's DLL.</param>
    /// <param name="backupFiles">The backup's files, relative to its folder.</param>
    /// <param name="installedFiles">The files the install placed, relative to the mod's folder; read from a file, so checked.</param>
    /// <param name="asideFolder">The backup the current files go to.</param>
    public static (RollbackSteps? Steps, string? Problem) For(string modFolder, string dllFileName, string backupFolder, IReadOnlyList<string> backupFiles, IReadOnlyList<string> installedFiles, string asideFolder)
    {
        var folder = Path.GetFullPath(modFolder);

        if (string.Equals(Path.GetFullPath(backupFolder), Path.GetFullPath(asideFolder), StringComparison.OrdinalIgnoreCase)) return (null, "the backup is the version installed now");

        if (!backupFiles.Any(file => string.Equals(file, dllFileName, StringComparison.OrdinalIgnoreCase))) return (null, "the backup does not hold " + dllFileName);

        var relatives = backupFiles.Concat(installedFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (relatives.FirstOrDefault(relative => !ZipPlan.IsBelow(folder, Path.GetFullPath(Path.Combine(folder, relative)))) is { } outside) return (null, "a file would land outside the mod's folder: " + outside);

        var setAside = relatives.Select(relative => new FileStep(Path.Combine(folder, relative), Path.Combine(asideFolder, relative))).ToList();
        var copyBack = backupFiles.Select(relative => new FileStep(Path.Combine(backupFolder, relative), Path.Combine(folder, relative))).ToList();

        return (new RollbackSteps(setAside, copyBack), null);
    }
}
