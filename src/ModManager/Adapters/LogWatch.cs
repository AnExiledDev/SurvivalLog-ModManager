using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Hears every warning and error BepInEx logs from the moment the Mod Manager loads, and reads the ones logged before
/// that out of LogOutput.log. BepInEx calls LogEvent on whatever thread logged, so it only queues there; the book is
/// filled on the main thread.
/// </summary>
internal sealed class LogWatch : ILogListener
{
    /// <summary>A storm of errors between two frames is cut here rather than growing without end.</summary>
    private const int MaxWaiting = 5000;

    /// <summary>LogOutput.log is flushed every two seconds, so the start line can take a few tries to show up.</summary>
    private const int MaxFileTries = 20;

    private const long MaxFileBytes = 16 * 1024 * 1024;

    private readonly ConcurrentQueue<LoggedEvent> _waiting = new();
    private readonly string _logFolder;
    private readonly string _startLine;
    private readonly Action<string> _log;
    private int _waitingCount;
    private int _lost;
    private int _fileTries;
    private bool _hasReadEarlier;

    /// <param name="startLine">Text of the line the Mod Manager logs as it starts listening; the file is read up to it.</param>
    public LogWatch(string logFolder, string startLine, Action<string> log)
    {
        _logFolder = logFolder;
        _startLine = startLine;
        _log = log;
    }

    public ProblemBook Book { get; } = new();

    public LogLevel LogLevelFilter => LogLevel.Fatal | LogLevel.Error | LogLevel.Warning;

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        try
        {
            if ((eventArgs.Level & LogLevelFilter) == 0) return;

            var level = (eventArgs.Level & (LogLevel.Fatal | LogLevel.Error)) != 0 ? ProblemLevel.Error : ProblemLevel.Warning;

            if (Interlocked.Increment(ref _waitingCount) > MaxWaiting)
            {
                Interlocked.Decrement(ref _waitingCount);
                Interlocked.Increment(ref _lost);

                return;
            }

            _waiting.Enqueue(new LoggedEvent(level, eventArgs.Source?.SourceName ?? "", eventArgs.Data?.ToString() ?? ""));
        }
        catch (Exception)
        {
            // Logging here could call straight back in; a lost event is the lesser harm.
        }
    }

    /// <summary>Main thread. Events from before the Mod Manager come first, so the book stays in the order things happened.</summary>
    public void Collect()
    {
        if (!_hasReadEarlier) ReadEarlier();

        if (!_hasReadEarlier) return;

        while (_waiting.TryDequeue(out var logged))
        {
            Interlocked.Decrement(ref _waitingCount);
            Book.Add(logged);
        }

        Book.AddLost(Interlocked.Exchange(ref _lost, 0));
    }

    public void Dispose()
    {
    }

    private void ReadEarlier()
    {
        _fileTries++;

        try
        {
            // The newest only: an older LogOutput.1.log from another start can hold the same start line.
            var newest = Directory.EnumerateFiles(_logFolder, "LogOutput*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            var earlier = newest == null ? null : LogFile.Before(ReadShared(newest), _startLine);

            if (earlier != null)
            {
                foreach (var logged in earlier) Book.Add(logged);

                _hasReadEarlier = true;
            }
        }
        catch (Exception ex)
        {
            _log("errors logged before the Mod Manager started are not listed: could not read the BepInEx log: " + ex.Message);
            _hasReadEarlier = true;

            return;
        }

        if (_hasReadEarlier || _fileTries < MaxFileTries) return;

        _log("errors logged before the Mod Manager started are not listed: the BepInEx log did not show its start line");
        _hasReadEarlier = true;
    }

    /// <summary>BepInEx still has the file open for writing.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length > MaxFileBytes) return "";

        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
