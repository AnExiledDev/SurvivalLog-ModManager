using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// The backups folder: one folder per mod, then one per version holding the files an install replaced, beside a
/// "version.files" list of the files that version's install placed. Reads fail soft to "no backups".
/// </summary>
internal sealed class BackupShelf
{
    private const string FilesListExtension = ".files";

    private readonly string _root;

    public BackupShelf(string root) => _root = root;

    public string FolderOf(string guid, string version) => Path.Combine(_root, BackupFolder.Name(guid), BackupFolder.Name(version));

    public IReadOnlyList<string> Versions(string guid)
    {
        var folder = Path.Combine(_root, BackupFolder.Name(guid));

        try
        {
            return Directory.Exists(folder) ? Directory.GetDirectories(folder).Select(Path.GetFileName).OfType<string>().ToList() : Array.Empty<string>();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Every file in a backup, relative to it.</summary>
    public IReadOnlyList<string> FilesIn(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(folder, path)).ToList() : Array.Empty<string>();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>The files an install of this version placed, relative to the mod's folder; none when it was not recorded.</summary>
    public IReadOnlyList<string> InstalledFiles(string guid, string version)
    {
        var folder = Path.Combine(_root, BackupFolder.Name(guid));

        try
        {
            var path = Directory.Exists(folder) ? Directory.GetFiles(folder, "*" + FilesListExtension).FirstOrDefault(list => BackupFolder.IsSameVersion(Path.GetFileNameWithoutExtension(list), version)) : null;

            return path != null ? File.ReadAllLines(path).Select(line => line.Trim()).Where(line => line.Length > 0).ToList() : Array.Empty<string>();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Throws on failure; the caller decides whether a missing list matters.</summary>
    public void RecordInstalled(string guid, string version, IEnumerable<string> relativePaths)
    {
        var path = FolderOf(guid, version) + FilesListExtension;

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllLines(path, relativePaths);
    }
}
