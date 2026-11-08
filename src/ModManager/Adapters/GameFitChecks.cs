using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Checks once per start, on a thread of its own, every installed DLL against the game's code for this game build, so a
/// mod that uses something the game no longer has says it may not work. A DLL checked at an earlier start against the
/// same game code is not read again: the checks are saved by each DLL's SHA-256.
/// </summary>
internal sealed class GameFitChecks
{
    private readonly string _interop;
    private readonly string _core;
    private readonly SavedFile _saved;
    private readonly Action<string> _log;
    private readonly ConcurrentQueue<Action> _finished = new();
    private readonly Dictionary<string, GameFit> _fits = new(StringComparer.Ordinal);
    private bool _hasStarted;

    /// <param name="interop">BepInEx/interop, which BepInEx rebuilds when the game updates.</param>
    /// <param name="core">BepInEx/core.</param>
    public GameFitChecks(string interop, string core, SavedFile saved, Action<string> log)
    {
        _interop = interop;
        _core = core;
        _saved = saved;
        _log = log;
    }

    /// <summary>Main thread: the check of the DLL with this SHA-256, or null when it has not been checked (yet).</summary>
    public GameFit? FitOf(string? dllSha256) => dllSha256 != null && _fits.TryGetValue(dllSha256, out var fit) ? fit : null;

    /// <summary>Main thread, once: shows the saved checks that still hold, and checks the other DLLs in the background.</summary>
    /// <param name="dlls">Every installed mod's DLL, on or off, where it is now, with its SHA-256.</param>
    public void Start(IReadOnlyList<(string Path, string Sha256)> dlls)
    {
        if (_hasStarted) return;

        _hasStarted = true;

        if (!Directory.Exists(_interop))
        {
            _log("did not check the mods against the game's code: there is no " + _interop + " (BepInEx builds it at the first start)");

            return;
        }

        string stamp;

        try
        {
            stamp = GameFitCache.StampOf(new DirectoryInfo(_interop).EnumerateFiles("*.dll").Select(file => (file.Name, file.Length, file.LastWriteTimeUtc.Ticks)));
        }
        catch (Exception ex)
        {
            _log("did not check the mods against the game's code: could not read " + _interop + ": " + ex.Message);

            return;
        }

        var saved = Saved();
        var installed = dlls.Select(dll => dll.Sha256).ToList();
        var toCheck = saved.ToCheck(stamp, installed).ToHashSet(StringComparer.Ordinal);

        foreach (var (dll, fit) in saved.ByDll)
        {
            if (saved.Stamp == stamp && !toCheck.Contains(dll)) _fits[dll] = fit;
        }

        if (toCheck.Count == 0) return;

        var paths = dlls.Where(dll => toCheck.Contains(dll.Sha256)).GroupBy(dll => dll.Sha256).Select(group => group.First()).ToList();

        new Thread(() => Run(stamp, saved, paths, installed)) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Mod Manager game code check" }.Start();
    }

    /// <summary>Main thread: records the checks if they finished since the last call. True when anything changed.</summary>
    public bool TakeFinished()
    {
        var isChanged = false;

        while (_finished.TryDequeue(out var record))
        {
            record();
            isChanged = true;
        }

        return isChanged;
    }

    private GameFitCache Saved()
    {
        if (_saved.Read() is not { } text) return GameFitCache.Empty;
        if (GameFitCache.FromJson(text) is { } cache) return cache;

        _log("the saved checks of the mods against the game's code are not used: they are of another kind or cannot be read");

        return GameFitCache.Empty;
    }

    /// <summary>Its own thread: nothing here may throw, and every log line and change goes to the main thread.</summary>
    private void Run(string stamp, GameFitCache saved, IReadOnlyList<(string Path, string Sha256)> paths, IReadOnlyList<string> installed)
    {
        var fresh = new Dictionary<string, GameFit>(StringComparer.Ordinal);
        var unreadable = new List<string>();

        try
        {
            using var game = new GameReferences(_interop, _core);

            foreach (var (path, sha256) in paths)
            {
                if (game.Check(path) is { } fit) fresh[sha256] = fit;
                else unreadable.Add(Path.GetFileName(path));
            }
        }
        catch (Exception ex)
        {
            _finished.Enqueue(() => _log("the check of the mods against the game's code stopped: " + ex.Message));

            return;
        }

        var json = saved.With(stamp, fresh, installed).ToJson();
        var outdated = paths.Where(dll => fresh.TryGetValue(dll.Sha256, out var fit) && fit.IsMissingAny).Select(dll => Path.GetFileName(dll.Path) + " (" + fresh[dll.Sha256].MissingCount + ")").ToList();

        _finished.Enqueue(() =>
        {
            foreach (var (dll, fit) in fresh) _fits[dll] = fit;

            _saved.Save(json);
            _log("checked " + fresh.Count + " mod DLLs against the game's code" +
                (outdated.Count > 0 ? "; these use things this game version no longer has: " + string.Join(", ", outdated) : "; none uses anything this game version lacks") +
                (unreadable.Count > 0 ? "; could not read " + string.Join(", ", unreadable) : ""));
        });
    }
}
