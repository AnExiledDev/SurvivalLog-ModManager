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
/// the new ones moved in beside them. Any failure puts the old files back. A rollback works the same way backwards.
/// </summary>
internal sealed class UpdateInstaller
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _client;
    private readonly ConcurrentQueue<Action> _finished = new();
    private readonly UpdateBook _book;
    private readonly BackupShelf _shelf;
    private readonly Action<string> _log;

    public UpdateInstaller(UpdateBook book, BackupShelf shelf, string userAgent, Action<string> log)
    {
        _book = book;
        _shelf = shelf;
        _log = log;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout, MaxResponseContentBufferSize = ReleaseAssets.MaxDownloadBytes };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    /// <summary>Main thread. Does nothing while an install of the same mod runs, after one finished, or after a rollback.</summary>
    public void Start(ModEntry mod, LatestRelease.Found release, string dllPath, bool isSharedFolder)
    {
        if (_book.InstallOf(mod.Guid) is InstallState.Installing or InstallState.Installed || _book.HasRolledBack(mod.Guid)) return;

        _book.Record(mod.Guid, new InstallState.Installing());
        _log("installing " + mod.Guid + " " + release.Tag + " over " + mod.Version);

        var backup = _shelf.FolderOf(mod.Guid, mod.Version);

        _ = Task.Run(async () =>
        {
            InstallState result;

            try
            {
                result = await Install(mod.Guid, release, dllPath, isSharedFolder, backup).ConfigureAwait(false);
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

    /// <summary>
    /// Main thread; the files are small, so it runs at once. Puts back the backup of <paramref name="version"/> and
    /// records how it went. Does nothing while an install of the mod runs.
    /// </summary>
    public void RollBack(ModEntry mod, string version, string dllPath)
    {
        if (_book.InstallOf(mod.Guid) is InstallState.Installing) return;

        var diskVersion = _book.InstallOf(mod.Guid) is InstallState.Installed installed ? installed.Tag : mod.Version;
        var backup = _shelf.FolderOf(mod.Guid, version);
        var aside = _shelf.FolderOf(mod.Guid, diskVersion);
        var (steps, problem) = RollbackPlan.For(Path.GetDirectoryName(dllPath) ?? ".", Path.GetFileName(dllPath), backup, _shelf.FilesIn(backup), _shelf.InstalledFiles(mod.Guid, diskVersion), aside);
        var result = steps == null ? new RollbackState.Failed(version, problem ?? "there is nothing to roll back") : FileSwap.Restore(DiskFiles.Instance, steps, version);

        _book.Record(mod.Guid, result);

        if (result is RollbackState.Done) _book.Record(mod.Guid, new InstallState.None());

        _log(result is RollbackState.Failed failed ? "could not roll " + mod.Guid + " back to " + version + ": " + failed.Reason : "rolled " + mod.Guid + " back to " + version + "; it is used from the next start, the files it replaced are in " + aside);
    }

    private async Task<InstallState> Install(string guid, LatestRelease.Found release, string dllPath, bool isSharedFolder, string backup)
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

        var modFolder = Path.GetDirectoryName(dllPath) ?? ".";
        var entryOf = files.ToDictionary(file => file.Target, file => file.EntryName);

        FileSwap.Replace(DiskFiles.Instance, files.Select(file => file.Target).ToList(), (target, temporary) => zip.Entries.First(entry => entry.FullName == entryOf[target]).ExtractToFile(temporary, true), modFolder, backup);

        // Without the list a rollback still restores the replaced files; it only leaves files the update added.
        TryRun(() => _shelf.RecordInstalled(guid, release.Tag, files.Select(file => Path.GetRelativePath(modFolder, file.Target))));

        return new InstallState.Installed(release.Tag);
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

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Best effort: without the list of installed files a rollback still works.
        }
    }
}
