using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Downloads a mod's update from its GitHub release and puts the files in place for the next game start. The running
/// game keeps the DLL it loaded: Windows lets a loaded DLL be moved, so the old files are moved to a backup folder and
/// the new ones moved in beside them. Any failure puts the old files back.
/// </summary>
internal sealed class UpdateInstaller
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _client;
    private readonly ConcurrentQueue<Action> _finished = new();
    private readonly UpdateBook _book;
    private readonly string _backups;
    private readonly Action<string> _log;

    public UpdateInstaller(UpdateBook book, string backups, string userAgent, Action<string> log)
    {
        _book = book;
        _backups = backups;
        _log = log;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout, MaxResponseContentBufferSize = ReleaseAssets.MaxDownloadBytes };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    /// <summary>Main thread. Does nothing while an install of the same mod runs, or after one finished.</summary>
    public void Start(ModEntry mod, LatestRelease.Found release, string dllPath, bool isSharedFolder)
    {
        if (_book.InstallOf(mod.Guid) is InstallState.Installing or InstallState.Installed) return;

        _book.Record(mod.Guid, new InstallState.Installing());
        _log("installing " + mod.Guid + " " + release.Tag + " over " + mod.Version);

        var backup = Path.Combine(_backups, Safe(mod.Guid), Safe(mod.Version));

        _ = Task.Run(async () =>
        {
            InstallState result;

            try
            {
                result = await Install(release, dllPath, isSharedFolder, backup).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = new InstallState.Failed(ex.Message);
            }

            _finished.Enqueue(() =>
            {
                _book.Record(mod.Guid, result);
                _log(result is InstallState.Failed failed ? "could not install " + mod.Guid + " " + release.Tag + ": " + failed.Reason : "installed " + mod.Guid + " " + release.Tag + "; it is used from the next start, the old files are in " + backup);
            });
        });
    }

    /// <summary>Main thread: records every install that finished since the last call. True when anything changed.</summary>
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

    private async Task<InstallState> Install(LatestRelease.Found release, string dllPath, bool isSharedFolder, string backup)
    {
        var dllName = Path.GetFileName(dllPath);
        var answer = await Get(ReleaseNotes.ApiUri(release)).ConfigureAwait(false);

        if (answer.Problem != null) return new InstallState.Failed(answer.Problem);

        var (asset, assetProblem) = ReleaseAssets.Choose(System.Text.Encoding.UTF8.GetString(answer.Body), dllName);

        if (asset == null) return new InstallState.Failed(assetProblem ?? "no file to install");

        var download = await Download(asset.Url).ConfigureAwait(false);

        if (download.Problem != null) return new InstallState.Failed(download.Problem);

        using var zip = new ZipArchive(new MemoryStream(download.Body), ZipArchiveMode.Read);
        var items = zip.Entries.Select(entry => new ZipItem(entry.FullName, entry.Length)).ToList();
        var (files, placeProblem) = ZipPlan.Place(items, dllName, Path.GetDirectoryName(dllPath) ?? ".", isSharedFolder);

        if (files == null) return new InstallState.Failed(placeProblem ?? "nothing to install");

        Replace(zip, files, Path.GetDirectoryName(dllPath) ?? ".", backup);

        return new InstallState.Installed(release.Tag);
    }

    /// <summary>All new files are written beside their targets first, so a full disk or a bad entry changes nothing.</summary>
    private static void Replace(ZipArchive zip, IReadOnlyList<PlacedFile> files, string modFolder, string backup)
    {
        var written = new List<string>();
        var kept = new List<(string Target, string Backup)>();
        var placed = new List<string>();

        try
        {
            foreach (var file in files)
            {
                var temporary = file.Target + ".new";

                Directory.CreateDirectory(Path.GetDirectoryName(file.Target) ?? ".");
                zip.Entries.First(entry => entry.FullName == file.EntryName).ExtractToFile(temporary, true);
                written.Add(temporary);
            }

            foreach (var file in files.Where(file => File.Exists(file.Target)))
            {
                var copy = Path.Combine(backup, Path.GetRelativePath(modFolder, file.Target));

                Directory.CreateDirectory(Path.GetDirectoryName(copy) ?? ".");

                if (File.Exists(copy)) File.Delete(copy);

                File.Move(file.Target, copy);
                kept.Add((file.Target, copy));
            }

            foreach (var file in files)
            {
                File.Move(file.Target + ".new", file.Target);
                placed.Add(file.Target);
            }
        }
        catch
        {
            foreach (var target in placed) TryDelete(target);

            foreach (var (target, copy) in kept) TryRun(() => File.Move(copy, target));

            foreach (var temporary in written) TryDelete(temporary);

            throw;
        }
    }

    private async Task<(byte[] Body, string? Problem)> Get(Uri uri)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _client.SendAsync(request).ConfigureAwait(false);
            var status = (int)response.StatusCode;

            if (status is 403 or 429) return (Array.Empty<byte>(), "GitHub's hourly limit for requests without an account was reached; try again within the hour");

            if (status != 200) return (Array.Empty<byte>(), "GitHub answered " + status);

            return (await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false), null);
        }
        catch (Exception ex)
        {
            return (Array.Empty<byte>(), "could not reach GitHub: " + ex.Message);
        }
    }

    /// <summary>Follows GitHub's redirects to its file storage by hand, so no hop can leave GitHub.</summary>
    private async Task<(byte[] Body, string? Problem)> Download(Uri uri)
    {
        try
        {
            for (var hop = 0; hop <= DownloadAddress.MaxHops; hop++)
            {
                using var response = await _client.GetAsync(uri).ConfigureAwait(false);
                var status = (int)response.StatusCode;

                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    uri = DownloadAddress.Next(uri, response.Headers.Location?.OriginalString) ?? throw new InvalidOperationException("the download was sent away from GitHub: " + response.Headers.Location);

                    continue;
                }

                if (status != 200) return (Array.Empty<byte>(), "the download answered " + status);

                return (await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false), null);
            }

            return (Array.Empty<byte>(), "the download redirected too many times");
        }
        catch (Exception ex)
        {
            return (Array.Empty<byte>(), "could not download the update: " + ex.Message);
        }
    }

    /// <summary>A folder name made from a GUID or version, which could otherwise hold a backslash or be "..".</summary>
    private static string Safe(string name)
    {
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        return safe.Trim('.').Length == 0 ? "_" : safe;
    }

    private static void TryDelete(string path) => TryRun(() => File.Delete(path));

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Restoring is best effort; the backup folder still holds the old files.
        }
    }
}
