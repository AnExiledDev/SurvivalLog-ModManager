using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Looks at the BepInEx folder for the health check: every DLL and folder below plugins, the patchers, and the settings
/// files. A DLL is read again only when its size or time changes, so a look after the first is
/// a walk of the folders and little more.
/// </summary>
internal sealed class InstallScanner
{
    private readonly object _gate = new();
    private readonly string _bepInEx;
    private readonly string _plugins;
    private readonly string _patchers;
    private readonly string _config;
    private readonly Action<string> _log;
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (long Size, DateTime Written, string? Sha256, IReadOnlyList<string> Guids)> _plugin = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (long Size, DateTime Written, IReadOnlyList<string> Guids)> _patcher = new(StringComparer.OrdinalIgnoreCase);

    public InstallScanner(string bepInEx, string plugins, string patchers, string config, Action<string> log)
    {
        _bepInEx = bepInEx;
        _plugins = plugins;
        _patchers = patchers;
        _config = config;
        _log = log;
    }

    /// <summary>Reads every DLL once on a worker, so the first look when the screen opens has little left to read.</summary>
    public void StartWarmUp() =>
        new Thread(() =>
        {
            try
            {
                Scan("", Array.Empty<string>(), Array.Empty<string>());
            }
            catch (Exception ex)
            {
                _log("could not look at the BepInEx folder for the health check: " + ex.Message);
            }
        }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Mod Manager health check" }.Start();

    /// <param name="settingsInUse">The .cfg files the running mods have open, as full paths or names.</param>
    /// <param name="otherGuids">Plugins not in the list that may still own a settings file, such as mods in the trash.</param>
    public InstallScan Scan(string bepInExVersion, IEnumerable<string> settingsInUse, IEnumerable<string> otherGuids)
    {
        lock (_gate)
        {
            var dlls = Files(_plugins, "*.dll").Select(PluginDll).ToList();
            var patchers = Files(_patchers, "*.dll").ToList();
            var patcherGuids = patchers.SelectMany(PatcherGuids);
            var folders = Folders(_plugins).Select(Relative).ToList();
            var settings = Files(_config, "*.cfg", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).OfType<string>().ToList();
            var inUse = settingsInUse.Select(Path.GetFileName).OfType<string>().ToList();

            return new InstallScan(bepInExVersion, dlls, folders, settings, inUse, patcherGuids.Concat(otherGuids).ToList());
        }
    }

    private FoundDll PluginDll(string path)
    {
        var (size, written) = Stamp(path);

        if (!_plugin.TryGetValue(path, out var known) || known.Size != size || known.Written != written)
        {
            known = (size, written, HashOf(path), Read(path, PluginFileReader.Read).Select(plugin => plugin.Guid).ToList());
            _plugin[path] = known;
        }

        return new FoundDll(Relative(path), known.Sha256, known.Guids);
    }

    private IReadOnlyList<string> PatcherGuids(string path)
    {
        var (size, written) = Stamp(path);

        if (!_patcher.TryGetValue(path, out var known) || known.Size != size || known.Written != written)
        {
            known = (size, written, Read(path, PluginFileReader.PatcherGuids));
            _patcher[path] = known;
        }

        return known.Guids;
    }

    /// <summary>A DLL that cannot be read declares nothing; a library is read without fuss and declares nothing either.</summary>
    private IReadOnlyList<T> Read<T>(string path, Func<string, IReadOnlyList<T>> read)
    {
        try
        {
            return read(path);
        }
        catch (Exception ex)
        {
            ReportOnce("read/" + path, "the health check could not read " + path + ": " + ex.Message);

            return Array.Empty<T>();
        }
    }

    private string? HashOf(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            return Sha256Text.Of(file);
        }
        catch (Exception ex)
        {
            ReportOnce("hash/" + path, "the health check could not read " + path + ": " + ex.Message);

            return null;
        }
    }

    private static (long Size, DateTime Written) Stamp(string path)
    {
        try
        {
            var file = new FileInfo(path);

            return (file.Length, file.LastWriteTimeUtc);
        }
        catch (Exception)
        {
            return (-1, DateTime.MinValue);
        }
    }

    private IEnumerable<string> Files(string folder, string pattern, SearchOption search = SearchOption.AllDirectories)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, pattern, search).ToList() : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            ReportOnce("files/" + folder, "the health check could not list " + folder + ": " + ex.Message);

            return Array.Empty<string>();
        }
    }

    private IEnumerable<string> Folders(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories).ToList() : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            ReportOnce("folders/" + folder, "the health check could not list the folders in " + folder + ": " + ex.Message);

            return Array.Empty<string>();
        }
    }

    /// <summary>Below the BepInEx folder, with "/" between folders, as the page shows it.</summary>
    private string Relative(string path) => Path.GetRelativePath(_bepInEx, path).Replace('\\', '/');

    private void ReportOnce(string key, string message)
    {
        if (_reported.Add(key)) _log(message);
    }
}
