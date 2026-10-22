using System;
using System.IO;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// GitHub's number for each repository a mod named, saved at its first install under owner\repository.txt. A pin file
/// that is there but cannot be used stops installs of that mod instead of being pinned again, as pinning again would
/// trust whatever the name points at now.
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

    public abstract record Saved
    {
        private Saved()
        {
        }

        /// <summary>No pin yet: this is the mod's first install from that repository.</summary>
        public sealed record None : Saved;

        public sealed record Pinned(long Id) : Saved;

        public sealed record Unusable(string Path, string Problem) : Saved;
    }

    public Saved Read(string repository)
    {
        var path = PathOf(repository);

        try
        {
            if (!File.Exists(path)) return new Saved.None();

            return RepositoryPin.Parse(File.ReadAllText(path)) is { } id ? new Saved.Pinned(id) : new Saved.Unusable(path, "it does not hold a repository number");
        }
        catch (Exception ex)
        {
            _log("could not read the saved number of " + repository + ": " + ex.Message);

            return new Saved.Unusable(path, ex.Message);
        }
    }

    /// <summary>Written beside the pin and then moved over it, so a crash or a second install never leaves half a file. False when it could not be saved.</summary>
    public bool Save(string repository, long repositoryId)
    {
        var path = PathOf(repository);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _root);
            File.WriteAllText(temporary, RepositoryPin.Format(repositoryId));
            File.Move(temporary, path, true);

            return true;
        }
        catch (Exception ex)
        {
            _log("could not save the number of " + repository + ": " + ex.Message);

            try
            {
                File.Delete(temporary);
            }
            catch
            {
                // Best effort: a leftover .tmp file is never read as a pin.
            }

            return false;
        }
    }

    /// <summary>Repository names are checked as GitHub names when read, so neither part can climb out of the folder.</summary>
    private string PathOf(string repository)
    {
        var parts = repository.ToLowerInvariant().Split('/');

        return Path.Combine(_root, parts[0], parts[1] + ".txt");
    }
}
