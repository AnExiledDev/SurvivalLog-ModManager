using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// The trash folder: one folder per uninstalled mod holding its DLL, and its .cfg when the player chose to remove it
/// too, beside a record of where each file was. Nothing is deleted until EMPTY TRASH, and that leaves the mods
/// uninstalled this session, whose DLLs the game still has loaded. Main thread only.
/// </summary>
internal sealed class TrashCan
{
    /// <summary>More than anyone uninstalls; past this, the oldest are not listed.</summary>
    private const int MaxItems = 500;

    private readonly string _root;
    private readonly IReadOnlyList<string> _homes;
    private readonly Action<string> _log;
    private readonly HashSet<string> _thisSession = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<TrashItem>? _items;

    /// <param name="root">The trash folder.</param>
    /// <param name="homes">The folders a file may be moved out of and back into: the plugins folder, the folders for turned-off plugins, and BepInEx/config.</param>
    public TrashCan(string root, IReadOnlyList<string> homes, Action<string> log)
    {
        _root = root;
        _homes = homes;
        _log = log;
    }

    /// <summary>Newest first; an item whose record cannot be read is left out, and logged once.</summary>
    public IReadOnlyList<TrashItem> Items() => _items ??= Read();

    public bool IsThisSession(string id) => _thisSession.Contains(id);

    /// <summary>Where a file of an item is now.</summary>
    public string StoredPath(TrashItem item, TrashFile file) => Path.Combine(_root, item.Id, file.Stored);

    /// <summary>
    /// Moves the DLL, and the .cfg when one is given, into a new item. Throws when it cannot, after putting back what
    /// it had moved, so a failed uninstall changes nothing.
    /// </summary>
    public TrashItem Put(string guid, string name, string version, string dll, string? settings)
    {
        var now = DateTime.UtcNow;
        var id = TrashItem.IdFor(guid, now);

        for (var n = 2; Directory.Exists(Path.Combine(_root, id)); n++) id = TrashItem.IdFor(guid, now) + "-" + n;

        var files = new List<TrashFile> { new(Path.GetFullPath(dll), Path.GetFileName(dll), false) };

        if (settings != null) files.Add(new TrashFile(Path.GetFullPath(settings), Path.GetFileName(settings), true));

        foreach (var file in files)
        {
            if (!IsHome(file.From)) throw new IOException(file.From + " is not in the plugins, turned-off plugins or config folder");
        }

        var item = new TrashItem(id, guid, name, version, now, files);
        var folder = Path.Combine(_root, id);

        // The record first, so a folder that holds a mod's files always says where they go back to.
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, TrashItem.FileName), item.ToJson());
        Move(files.Select(file => (file.From, StoredPath(item, file))).ToList(), () => DeleteQuietly(folder));

        _thisSession.Add(id);
        _items = null;

        return item;
    }

    /// <summary>
    /// Moves every file of the item back to where it was, and removes the item. Throws <see cref="TrashTaken"/> when a
    /// file is already there, and nothing moves.
    /// </summary>
    public void Restore(TrashItem item)
    {
        foreach (var file in item.Files)
        {
            if (!IsHome(file.From)) throw new IOException(file.From + " is not in the plugins, turned-off plugins or config folder");
            if (File.Exists(file.From)) throw new TrashTaken(file.From);
            if (!File.Exists(StoredPath(item, file))) throw new IOException(file.Stored + " is no longer in the trash");
        }

        Move(item.Files.Select(file => (StoredPath(item, file), file.From)).ToList(), () => { });
        DeleteQuietly(Path.Combine(_root, item.Id));
        _thisSession.Remove(item.Id);
        _items = null;
    }

    /// <summary>
    /// Deletes every item but the ones uninstalled this session, and any folder in the trash that is not an item.
    /// Returns how many items went, how many were left as this session's, and the names of those that could not go.
    /// </summary>
    public (int Deleted, int Kept, IReadOnlyList<string> Failed) Empty()
    {
        var names = Items().ToDictionary(item => item.Id, item => item.Name, StringComparer.OrdinalIgnoreCase);
        var deleted = 0;
        var failed = new List<string>();

        foreach (var folder in Folders().Where(folder => !_thisSession.Contains(Path.GetFileName(folder))))
        {
            var id = Path.GetFileName(folder);

            try
            {
                Directory.Delete(folder, true);

                if (names.ContainsKey(id)) deleted++;

                _log("emptied from the trash: " + id);
            }
            catch (Exception ex)
            {
                failed.Add(names.TryGetValue(id, out var name) ? name : id);
                _log("could not delete " + folder + ": " + ex.Message);
            }
        }

        _items = null;

        return (deleted, _thisSession.Count, failed);
    }

    private IReadOnlyList<TrashItem> Read()
    {
        var items = new List<TrashItem>();

        foreach (var folder in Folders())
        {
            var id = Path.GetFileName(folder);
            var record = Path.Combine(folder, TrashItem.FileName);
            TrashItem? item = null;

            try
            {
                if (File.Exists(record)) item = TrashItem.FromJson(id, File.ReadAllText(record));
            }
            catch (Exception ex)
            {
                if (_reported.Add(id + "\nread")) _log("could not read " + record + ": " + ex.Message);

                continue;
            }

            if (item != null) items.Add(item);
            else if (_reported.Add(id)) _log("the trash folder " + folder + " is not shown: it has no record of a mod's files that can be read");
        }

        return items.OrderByDescending(item => item.TrashedUtc).ThenBy(item => item.Id, StringComparer.Ordinal).Take(MaxItems).ToList();
    }

    private IEnumerable<string> Folders()
    {
        try
        {
            return Directory.Exists(_root) ? Directory.GetDirectories(_root) : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            if (_reported.Add("\nroot")) _log("could not read the trash folder " + _root + ": " + ex.Message);

            return Array.Empty<string>();
        }
    }

    /// <summary>Moves each file in turn; when one fails, moves the ones already moved back, runs the clean-up, and rethrows.</summary>
    private static void Move(IReadOnlyList<(string From, string To)> moves, Action cleanUp)
    {
        var done = new List<(string From, string To)>();

        try
        {
            foreach (var (from, to) in moves)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to) ?? ".");
                File.Move(from, to);
                done.Add((from, to));
            }
        }
        catch (Exception)
        {
            foreach (var (from, to) in Enumerable.Reverse(done))
            {
                try
                {
                    File.Move(to, from);
                }
                catch (Exception)
                {
                    // Left where it is: the item's record still says where it goes back to.
                    return;
                }
            }

            cleanUp();

            throw;
        }
    }

    private bool IsHome(string path) => _homes.Any(home => IsBelow(home, path));

    private static bool IsBelow(string folder, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(folder), Path.GetFullPath(path));

        return relative != "." && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static void DeleteQuietly(string folder)
    {
        try
        {
            Directory.Delete(folder, true);
        }
        catch (Exception)
        {
            // An empty or half-written item folder is harmless: it is not listed, and EMPTY TRASH removes it.
        }
    }
}

/// <summary>A file is already where a restore would put one back.</summary>
internal sealed class TrashTaken : IOException
{
    public TrashTaken(string path) : base("a file is already at " + path) => Path = path;

    public string Path { get; }
}
