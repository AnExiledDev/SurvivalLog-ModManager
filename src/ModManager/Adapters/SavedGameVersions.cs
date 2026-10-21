using System;
using System.IO;

namespace ModManager.Adapters;

/// <summary>
/// The last game-versions.txt each repository served, one file per repository under owner\repository.txt, so a start
/// without a connection (or with update checks off) still knows which game versions a mod works on. Fails soft: a
/// list that cannot be read or written is simply not there.
/// </summary>
internal sealed class SavedGameVersions
{
    private readonly string _root;
    private readonly Action<string> _log;

    public SavedGameVersions(string root, Action<string> log)
    {
        _root = root;
        _log = log;
    }

    public string? Read(string repository)
    {
        var path = PathOf(repository);

        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            _log("could not read the saved game versions of " + repository + ": " + ex.Message);

            return null;
        }
    }

    public void Save(string repository, string text)
    {
        var path = PathOf(repository);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _root);
            File.WriteAllText(path, text);
        }
        catch (Exception ex)
        {
            _log("could not save the game versions of " + repository + ": " + ex.Message);
        }
    }

    public void Forget(string repository)
    {
        var path = PathOf(repository);

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _log("could not delete the saved game versions of " + repository + ": " + ex.Message);
        }
    }

    /// <summary>Repository names are checked as GitHub names when read, so neither part can climb out of the folder.</summary>
    private string PathOf(string repository)
    {
        var parts = repository.ToLowerInvariant().Split('/');

        return Path.Combine(_root, parts[0], parts[1] + ".txt");
    }
}
