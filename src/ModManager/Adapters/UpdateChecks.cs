using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Asks GitHub for the latest release of each repository in the background and hands the answers to the main thread,
/// which records them in the <see cref="UpdateBook"/>. Needs no account or key.
/// </summary>
internal sealed class UpdateChecks
{
    private const int AtOnce = 4;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _client;
    private readonly SemaphoreSlim _slots = new(AtOnce);
    private readonly ConcurrentQueue<Action> _finished = new();
    private readonly UpdateBook _book;
    private readonly Action<string> _log;

    /// <summary>Bumped when checks start or stop, so answers from an earlier run are dropped.</summary>
    private int _run;

    public UpdateChecks(UpdateBook book, string userAgent, Action<string> log)
    {
        _book = book;
        _log = log;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    public void Start(IEnumerable<string> repositories)
    {
        var run = ++_run;
        var unique = repositories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        _book.Clear();
        _log("checking GitHub for updates of " + unique.Count + (unique.Count == 1 ? " repository" : " repositories"));

        foreach (var repository in unique)
        {
            _book.Record(repository, new LatestRelease.Checking());
            _ = Check(run, repository);
        }
    }

    public void Stop()
    {
        _run++;
        _book.Clear();
    }

    /// <summary>Fetches a release's notes once per session; asking again while they load, or after, does nothing.</summary>
    public void FetchNotes(LatestRelease.Found release)
    {
        if (_book.Notes(release) is not NotesState.NotAsked) return;

        var run = _run;

        _book.Record(release, new NotesState.Loading());
        _ = Notes(run, release);
    }

    /// <summary>Main thread: records every answer that came in since the last call. True when anything changed.</summary>
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

    private async Task Check(int run, string repository)
    {
        var latest = await Limited(() => LatestReleaseFinder.Find(repository, Head)).ConfigureAwait(false);

        Finish(run, () =>
        {
            _book.Record(repository, latest);
            _log("latest release of " + repository + ": " + Describe(latest));
        });
    }

    private async Task Notes(int run, LatestRelease.Found release)
    {
        var notes = ReleaseNotes.From(await Limited(() => Get(ReleaseNotes.ApiUri(release))).ConfigureAwait(false));

        Finish(run, () =>
        {
            _book.Record(release, notes);

            var problem = notes switch
            {
                NotesState.Busy => "GitHub's hourly limit for requests without an account was reached",
                NotesState.Failed failed => failed.Reason,
                _ => null,
            };

            if (problem != null) _log("could not load the notes of " + release.Owner + "/" + release.Repository + " " + release.Tag + ": " + problem);
        });
    }

    private void Finish(int run, Action record) => _finished.Enqueue(() =>
    {
        if (run == _run) record();
    });

    private async Task<T> Limited<T>(Func<Task<T>> work)
    {
        await _slots.WaitAsync().ConfigureAwait(false);

        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    private Task<HttpAnswer> Head(Uri uri) => Send(new HttpRequestMessage(HttpMethod.Head, uri), false);

    private Task<HttpAnswer> Get(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        return Send(request, true);
    }

    /// <summary>Every failure becomes an answer: nothing thrown here may reach the game.</summary>
    private async Task<HttpAnswer> Send(HttpRequestMessage request, bool isBodyWanted)
    {
        try
        {
            using (request)
            using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                var body = isBodyWanted ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : "";

                return new HttpAnswer.Answered((int)response.StatusCode, response.Headers.Location?.OriginalString, body);
            }
        }
        catch (TaskCanceledException)
        {
            return new HttpAnswer.Unreachable("GitHub did not answer within " + Timeout.TotalSeconds + " seconds");
        }
        catch (Exception ex)
        {
            return new HttpAnswer.Unreachable("could not reach GitHub: " + ex.Message);
        }
    }

    private static string Describe(LatestRelease latest) => latest switch
    {
        LatestRelease.Found found => found.Tag,
        LatestRelease.NoReleases => "no releases yet",
        LatestRelease.Failed failed => "not found, " + failed.Reason,
        _ => latest.ToString(),
    };
}
