using System;
using System.IO;

namespace ModManager.Adapters;

/// <summary>
/// One text file the Mod Manager keeps between starts, such as the wiki's index, so a start without a connection still
/// has the last copy. Fails soft: a file that cannot be read or written is simply not there. A save writes a new file
/// and swaps it in, so a game that closes halfway leaves the old copy, never half of the new one.
/// </summary>
internal sealed class SavedFile
{
    private readonly string _path;
    private readonly string _what;
    private readonly Action<string> _log;

    public SavedFile(string path, string what, Action<string> log)
    {
        _path = path;
        _what = what;
        _log = log;
    }

    public string? Read() => TryRead(out var text) ? text : null;

    /// <summary>False when the file is there but could not be read. A missing file reads as null, and true.</summary>
    public bool TryRead(out string? text)
    {
        try
        {
            text = File.Exists(_path) ? File.ReadAllText(_path) : null;

            return true;
        }
        catch (Exception ex)
        {
            _log("could not read the saved " + _what + ": " + ex.Message);
            text = null;

            return false;
        }
    }

    /// <summary>False when it could not be written; the old copy is then still there.</summary>
    public bool Save(string text)
    {
        var temporary = _path + ".new";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            File.WriteAllText(temporary, text);
            File.Move(temporary, _path, true);

            return true;
        }
        catch (Exception ex)
        {
            _log("could not save the " + _what + ": " + ex.Message);

            return false;
        }
    }
}
