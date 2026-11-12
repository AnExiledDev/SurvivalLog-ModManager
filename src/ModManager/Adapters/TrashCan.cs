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
    private readonly Action<string, string> _move;
    private readonly HashSet<string> _thisSession = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<TrashItem>? _items;

    /// <param name="root">The trash folder.</param>
    /// <param name="homes">The folders a file may be moved out of and back into: the plugins folder, the folders for turned-off plugins, and BepInEx/config.</param>
    /// <param name="move">Moves a file without replacing one; tests pass one that fails.</param>
    public TrashCan(string root, IReadOnlyList<string> homes, Action<string> log, Action<string, string>? move = null)
    {
        _root = root;
        _homes = homes;
        _log = log;
        _move = move ?? ((from, to) => File.Move(from, to));
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
        try
        {
            Move(files.Select(file => (file.From, StoredPath(item, file))).ToList(), () => DeleteQuietly(folder));
        }
        catch (TrashPartial partial)
        {
            // The DLL moves first, so it is in the trash and the settings file is still at home. Kept from EMPTY TRASH
            // like any mod uninstalled this session.
            _thisSession.Add(id);

            throw new TrashPartial(partial.Message, partial.InnerException, item);
        }

        _thisSession.Add(id);
        _items = null;

        return item;
    }

    /// <summary>
    /// Moves every file of the item back to where it was, and removes the item. Throws <see cref="TrashTaken"/> when a
    /// file is already there, and nothing moves. A file that is back where it was and no longer in the trash is left
    /// alone: an uninstall that failed and could not put everything back leaves an item like that.
    /// </summary>
    public void Restore(TrashItem item)
    {
        var stored = new List<TrashFile>();

        foreach (var file in item.Files)
        {
            if (!IsHome(file.From)) throw new IOException(file.From + " is not in the plugins, turned-off plugins or config folder");

            var isStored = File.Exists(StoredPath(item, file));

            if (File.Exists(file.From))
            {
                if (isStored) throw new TrashTaken(file.From);

                continue;
            }

            if (!isStored) throw new IOException(file.Stored + " is no longer in the trash");

            stored.Add(file);
        }

        try
        {
            Move(stored.Select(file => (StoredPath(item, file), file.From)).ToList(), () => { });
        }
        catch (TrashPartial)
        {
            // Some files are back and some are not: EMPTY TRASH must not delete the rest.
            _thisSession.Add(item.Id);

            throw;
        }

        // Only once none of the mod's files is left in it, so a folder still holding one is never deleted.
        if (item.Files.All(file => !File.Exists(StoredPath(item, file)))) DeleteQuietly(Path.Combine(_root, item.Id));
        _thisSession.Remove(item.Id);
        _items = null;
    }

    /// <summary>
    /// Deletes every item but the ones uninstalled this session, and any empty or half-written folder in the trash. A
    /// folder whose record cannot be read but that holds other files is left alone: those may be a mod and its settings.
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

            if (!names.ContainsKey(id) && HoldsFiles(folder))
            {
                _log("left in the trash, since it holds files but no record that can be read: " + folder);

                continue;
            }

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

    /// <summary>
    /// Moves each file in turn; when one fails, moves the ones already moved back and rethrows. The clean-up runs only
    /// when every file went back, so a record is never deleted while a file it lists is still away from home.
    /// </summary>
    private void Move(IReadOnlyList<(string From, string To)> moves, Action cleanUp)
    {
        var done = new List<(string From, string To)>();

        try
        {
            foreach (var (from, to) in moves)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to) ?? ".");
                _move(from, to);
                done.Add((from, to));
            }
        }
        catch (Exception failure)
        {
            var isAllBack = true;

            foreach (var (from, to) in Enumerable.Reverse(done))
            {
                try
                {
                    _move(to, from);
                }
                catch (Exception ex)
                {
                    // Left where it is: the item's record still says where it goes back to.
                    isAllBack = false;
                    _log("could not move " + to + " back to " + from + ": " + ex.Message);
                }
            }

            if (!isAllBack)
            {
                _items = null;

                throw new TrashPartial(failure.Message, failure);
            }

            cleanUp();

            throw;
        }
    }

    private static bool HoldsFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any(file => !string.Equals(Path.GetFileName(file), TrashItem.FileName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return true;
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

/// <summary>A move failed and not every file could go back, so some are in the trash and some are not.</summary>
internal sealed class TrashPartial : IOException
{
    /// <param name="item">The item an uninstall made, when it is the uninstall that stopped halfway.</param>
    public TrashPartial(string message, Exception? inner, TrashItem? item = null) : base(message, inner) => Item = item;

    public TrashItem? Item { get; }
}
