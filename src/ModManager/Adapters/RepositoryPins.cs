using System;
using System.IO;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// GitHub's number for each repository a mod named, saved at its first install under owner\repository.txt. Fails
/// soft: a pin that cannot be read is fetched and saved again, as at a first install.
/// </summary>
internal sealed class RepositoryPins
{
    private readonly string _root;
    private readonly Action<string> _log;

    public RepositoryPins(string root, Action<string> log)
    {
        _root = root;
        _log = log;
    }

    public long? Read(string repository)
    {
        var path = PathOf(repository);

        try
        {
            return File.Exists(path) ? RepositoryPin.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception ex)
        {
            _log("could not read the saved number of " + repository + ": " + ex.Message);

            return null;
        }
    }

    public void Save(string repository, long repositoryId)
    {
        var path = PathOf(repository);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _root);
            File.WriteAllText(path, RepositoryPin.Format(repositoryId));
        }
        catch (Exception ex)
        {
            _log("could not save the number of " + repository + ": " + ex.Message);
        }
    }

    /// <summary>Repository names are checked as GitHub names when read, so neither part can climb out of the folder.</summary>
    private string PathOf(string repository)
    {
        var parts = repository.ToLowerInvariant().Split('/');

        return Path.Combine(_root, parts[0], parts[1] + ".txt");
    }
}
