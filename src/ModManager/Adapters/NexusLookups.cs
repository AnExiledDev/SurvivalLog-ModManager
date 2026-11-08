using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Asks the public API of Nexus Mods, once a day at most and without an account or key, for the game's mods, the files
/// of the ones that are not on the wiki, and the files' manifests, so an installed mod from Nexus shows its newest
/// version there. Asks nothing when every installed mod is on the wiki. One request at a time, in the background; the
/// answers are saved, so the next start within the day asks nothing.
/// </summary>
internal sealed class NexusLookups
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>The longest answer read; a page of 80 mods is far smaller.</summary>
    private const int MaxBodyChars = 2 * 1024 * 1024;

    private readonly HttpClient _client;
    private readonly ConcurrentQueue<Action> _finished = new();
    private readonly UpdateBook _book;
    private readonly SavedFile _saved;
    private readonly Action<string> _log;
    private CancellationTokenSource? _stop;
    private bool _hasRun;

    public NexusLookups(UpdateBook book, SavedFile saved, string appVersion, Action<string> log)
    {
        _book = book;
        _saved = saved;
        _log = log;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(NexusApi.AppName + "/" + appVersion);
        _client.DefaultRequestHeaders.Add("Application-Name", NexusApi.AppName);
        _client.DefaultRequestHeaders.Add("Application-Version", appVersion);
    }

    /// <summary>Not asked yet this session, and not asking now.</summary>
    public bool IsWaiting => !_hasRun && _stop == null;

    /// <summary>Main thread: the answers saved at an earlier start, shown until (or unless) fresh ones come.</summary>
    public void LoadSaved()
    {
        if (_saved.Read() is not { } text) return;

        if (NexusCache.FromJson(text) is { } cache) _book.Record(cache);
        else _log("the saved answers from Nexus Mods are not used: they are of another kind or cannot be read");
    }

    /// <summary>Main thread, once the wiki's index has settled, so it is known which mods are the wiki's.</summary>
    /// <param name="ours">The Nexus pages of the wiki's own mods.</param>
    /// <param name="foreign">For each installed mod that is not on the wiki, the Nexus page the wiki lists for it, or null.</param>
    public void Start(IReadOnlySet<int> ours, IReadOnlyList<int?> foreign)
    {
        var saved = _book.Nexus;

        if (foreign.Count == 0)
        {
            _hasRun = true;

            return;
        }

        if (!NexusApi.IsDue(saved.CheckedAt, DateTimeOffset.UtcNow))
        {
            _hasRun = true;
            _log("Nexus Mods was asked less than a day ago, at " + saved.CheckedAt?.ToString("u") + ", so it is not asked again");

            return;
        }

        _stop = new CancellationTokenSource();
        _ = Check(_stop.Token, saved, ours, foreign);
    }

    /// <summary>Main thread: update checks were turned off, so nothing more is asked; turning them on again starts over.</summary>
    public void Stop()
    {
        _stop?.Cancel();
        _stop = null;
    }

    /// <summary>Main thread: records the answers if they came in since the last call. True when anything changed.</summary>
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

    private async Task Check(CancellationToken stop, NexusCache saved, IReadOnlySet<int> ours, IReadOnlyList<int?> foreign)
    {
        try
        {
            var (mods, problem) = await ReadList(stop).ConfigureAwait(false);

            if (mods == null)
            {
                Finish(stop, null, "could not ask Nexus Mods for the game's mods, so " + (saved.Mods.Count > 0 ? "the saved answers stay in use" : "mods from Nexus show no Nexus version") + ": " + problem);

                return;
            }

            var candidates = NexusPlan.Candidates(mods, ours, foreign);
            var fresh = new Dictionary<int, NexusFiles>();
            var updated = mods.GroupBy(mod => mod.ModId).ToDictionary(group => group.Key, group => group.First().UpdatedAt);
            string? filesProblem = null;

            foreach (var batch in NexusPlan.NeedFiles(candidates, mods, saved).Chunk(NexusApi.FilesPerRequest))
            {
                var answer = await Post(NexusApi.FilesRequest(batch), stop).ConfigureAwait(false);
                var (files, readProblem) = answer is HttpAnswer.Answered { Status: 200 } ok ? NexusApi.ReadFiles(ok.Body, batch) : (null, Problem(answer));

                if (files == null)
                {
                    filesProblem = readProblem;

                    break;
                }

                foreach (var (id, list) in files) fresh[id] = new NexusFiles(updated[id], list);
            }

            var manifests = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            string? manifestProblem = null;

            foreach (var uri in NexusPlan.NeedManifests(candidates, NexusPlan.MergeFiles(mods, saved, fresh), saved.Manifests, NexusApi.ManifestsPerDay))
            {
                var (hashes, readProblem) = ManifestOf(await Send(new HttpRequestMessage(HttpMethod.Get, NexusApi.ManifestUri(uri)), stop).ConfigureAwait(false));

                if (hashes == null)
                {
                    manifestProblem = readProblem;

                    break;
                }

                manifests[uri] = hashes;
            }

            var cache = NexusPlan.Merge(DateTimeOffset.UtcNow, mods, saved, fresh, manifests);
            var notes = new[] { filesProblem == null ? null : "some files were not read: " + filesProblem, manifestProblem == null ? null : "some manifests were not read: " + manifestProblem }.OfType<string>().ToList();

            if (stop.IsCancellationRequested) return;

            Finish(stop, cache, "Nexus Mods lists " + mods.Count + " mods for the game; read the files of " + fresh.Count + " and " + manifests.Count + " manifests" + (notes.Count > 0 ? "; " + string.Join("; ", notes) : ""));
        }
        catch (Exception ex)
        {
            Finish(stop, null, "the check of Nexus Mods stopped: " + ex.Message);
        }
    }

    /// <summary>Every page of the list, or none: a list cut short would drop the mods on the missing pages.</summary>
    private async Task<(IReadOnlyList<NexusMod>? Mods, string? Problem)> ReadList(CancellationToken stop)
    {
        var mods = new List<NexusMod>();

        for (var page = 0; page < NexusApi.MaxPages; page++)
        {
            var answer = await Post(NexusApi.ModsRequest(page * NexusApi.PageSize), stop).ConfigureAwait(false);
            var (list, total, problem) = answer is HttpAnswer.Answered { Status: 200 } ok ? NexusApi.ReadMods(ok.Body) : (null, 0, Problem(answer));

            if (list == null) return (null, problem);

            mods.AddRange(list);

            if (list.Count == 0 || mods.Count >= total) return (mods, null);
        }

        return (mods, null);
    }

    /// <summary>On the main thread, like every log line: the answers are recorded and saved unless checks were turned off since.</summary>
    private void Finish(CancellationToken stop, NexusCache? cache, string line)
    {
        var json = cache?.ToJson();

        _finished.Enqueue(() =>
        {
            if (stop.IsCancellationRequested) return;

            _stop = null;
            _hasRun = true;
            _log(line);

            if (cache == null || json == null) return;

            _book.Record(cache);
            _saved.Save(json);
        });
    }

    /// <summary>A file Nexus keeps no manifest for is noted as holding no DLL, so it is not asked for again.</summary>
    private static (IReadOnlyList<string>? Hashes, string? Problem) ManifestOf(HttpAnswer answer) => answer switch
    {
        HttpAnswer.Answered { Status: 200 } ok => NexusApi.ReadManifest(ok.Body),
        HttpAnswer.Answered { Status: 404 or 403 } => (Array.Empty<string>(), null),
        _ => (null, Problem(answer)),
    };

    private static string Problem(HttpAnswer answer) => answer switch
    {
        HttpAnswer.Unreachable unreachable => unreachable.Reason,
        HttpAnswer.Answered { Status: 429 } => "Nexus Mods asked to wait before more requests",
        HttpAnswer.Answered answered => "Nexus Mods answered " + answered.Status,
        _ => "no answer",
    };

    private Task<HttpAnswer> Post(string body, CancellationToken stop) =>
        Send(new HttpRequestMessage(HttpMethod.Post, NexusApi.GraphQl) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, stop);

    /// <summary>Every failure becomes an answer: nothing thrown here may reach the game.</summary>
    private async Task<HttpAnswer> Send(HttpRequestMessage request, CancellationToken stop)
    {
        var host = request.RequestUri?.Host ?? "the server";

        try
        {
            using (request)
            using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stop).ConfigureAwait(false))
            {
                return new HttpAnswer.Answered((int)response.StatusCode, response.Headers.Location?.OriginalString, await ReadCapped(response.Content, stop).ConfigureAwait(false));
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            return new HttpAnswer.Unreachable("update checks were turned off");
        }
        catch (TaskCanceledException)
        {
            return new HttpAnswer.Unreachable(host + " did not answer within " + Timeout.TotalSeconds + " seconds");
        }
        catch (Exception ex)
        {
            return new HttpAnswer.Unreachable("could not reach " + host + ": " + ex.Message);
        }
    }

    private static async Task<string> ReadCapped(HttpContent content, CancellationToken stop)
    {
        using var stream = await content.ReadAsStreamAsync(stop).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var body = new StringBuilder();
        var chunk = new char[16 * 1024];
        int read;

        while (body.Length < MaxBodyChars && (read = await reader.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, MaxBodyChars - body.Length)), stop).ConfigureAwait(false)) > 0) body.Append(chunk, 0, read);

        return body.ToString();
    }
}
