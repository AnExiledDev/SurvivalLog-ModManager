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
/// Before anything is written, the update must come from the repository pinned at the mod's first install, must be the
/// same mod, and waits for the player when it can do something the installed version could not.
/// </summary>
internal sealed class UpdateInstaller
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _client;
    private readonly ConcurrentQueue<Action> _finished = new();
    private readonly UpdateBook _book;
    private readonly BackupShelf _shelf;
    private readonly RepositoryPins _pins;
    private readonly Action<string> _log;

    /// <summary>Checked updates waiting for the player, by mod GUID. Approving installs these bytes, not a second download.</summary>
    private readonly ConcurrentDictionary<string, Downloaded> _waiting = new(StringComparer.Ordinal);

    public UpdateInstaller(UpdateBook book, BackupShelf shelf, RepositoryPins pins, string userAgent, Action<string> log)
    {
        _book = book;
        _shelf = shelf;
        _pins = pins;
        _log = log;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout, MaxResponseContentBufferSize = ReleaseAssets.MaxDownloadBytes };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    /// <summary>Main thread. Does nothing while an install of the same mod runs, after one finished or was refused, or after a rollback.</summary>
    public void Start(ModEntry mod, LatestRelease.Found release, string dllPath, bool isSharedFolder)
    {
        if (_book.InstallOf(mod.Guid) is InstallState.Installing or InstallState.Installed or InstallState.Refused || _book.HasRolledBack(mod.Guid)) return;

        _waiting.TryRemove(mod.Guid, out _);
        _log("installing " + mod.Guid + " " + release.Tag + " over " + mod.Version);
        Run(mod, release.Tag, async () =>
        {
            var (download, stopped) = await Fetch(mod, release, Path.GetFileName(dllPath)).ConfigureAwait(false);

            return download == null ? stopped ?? new InstallState.Failed("nothing to install") : Place(mod, download, dllPath, isSharedFolder, isApproved: false);
        });
    }

    /// <summary>Main thread. Installs the update the player was asked about, from the bytes already checked.</summary>
    public void Approve(ModEntry mod, string dllPath, bool isSharedFolder)
    {
        if (_book.InstallOf(mod.Guid) is not InstallState.NeedsApproval || !_waiting.TryRemove(mod.Guid, out var download)) return;

        _log("installing " + mod.Guid + " " + download.Tag + " as the player approved it");
        Run(mod, download.Tag, () => Task.FromResult(Place(mod, download, dllPath, isSharedFolder, isApproved: true)));
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

    private void Run(ModEntry mod, string tag, Func<Task<InstallState>> install)
    {
        _book.Record(mod.Guid, new InstallState.Installing());

        _ = Task.Run(async () =>
        {
            InstallState result;

            try
            {
                result = await install().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = new InstallState.Failed(ex.Message);
            }

            _finished.Enqueue(() =>
            {
                _book.Record(mod.Guid, result);
                _log(Describe(mod, tag, result));
            });
        });
    }

    private string Describe(ModEntry mod, string tag, InstallState result) => result switch
    {
        InstallState.Failed failed => "could not install " + mod.Guid + " " + tag + ": " + failed.Reason,
        InstallState.Refused refused => "refused to install " + mod.Guid + " " + tag + ": " + refused.Reason,
        InstallState.NeedsApproval approval => mod.Guid + " " + tag + " waits for the player, as it gains: " + string.Join(", ", approval.NewPowers),
        _ => "installed " + mod.Guid + " " + tag + "; it is used from the next start, the old files are in " + _shelf.FolderOf(mod.Guid, mod.Version),
    };

    private sealed record Downloaded(string Tag, byte[] Zip);

    /// <summary>
    /// The release's zip, from the repository pinned at the mod's first install (pinned now when this is it). The pin is
    /// kept under the name the mod gives, not the one GitHub sends it to, so a name that starts pointing somewhere else
    /// still meets the old pin.
    /// </summary>
    private async Task<(Downloaded? Download, InstallState? Stopped)> Fetch(ModEntry mod, LatestRelease.Found release, string dllName)
    {
        var repository = mod.Info.UpdateRepository ?? release.Owner + "/" + release.Repository;
        var pin = _pins.Read(repository);

        if (pin is RepositoryPins.Saved.Unusable unusable) return (null, new InstallState.Refused(RepositoryPin.Unusable(unusable.Path, unusable.Problem)));

        long repositoryId;

        if (pin is RepositoryPins.Saved.Pinned pinned)
        {
            repositoryId = pinned.Id;
        }
        else
        {
            var about = await Get(RepositoryPin.RepositoryUri(release)).ConfigureAwait(false);

            if (about.Problem != null) return (null, new InstallState.Failed(about.Problem));

            if (RepositoryPin.IdFrom(System.Text.Encoding.UTF8.GetString(about.Body)) is not { } id) return (null, new InstallState.Failed("GitHub did not say which repository " + repository + " is"));

            repositoryId = id;
            _pins.Save(repository, repositoryId);
        }

        var answer = await Get(RepositoryPin.ReleaseUri(repositoryId, release.Tag)).ConfigureAwait(false);

        if (answer.Status == 404) return (null, new InstallState.Refused(RepositoryPin.NotInPinned(release.Tag)));

        if (answer.Problem != null) return (null, new InstallState.Failed(answer.Problem));

        var (asset, assetProblem) = ReleaseAssets.Choose(System.Text.Encoding.UTF8.GetString(answer.Body), dllName);

        if (asset == null) return (null, new InstallState.Failed(assetProblem ?? "no file to install"));

        var download = await Download(asset.Url).ConfigureAwait(false);

        return download.Problem != null ? (null, new InstallState.Failed(download.Problem)) : (new Downloaded(release.Tag, download.Body), null);
    }

    /// <param name="isApproved">The player already saw what the update gains, so it is not held again.</param>
    private InstallState Place(ModEntry mod, Downloaded download, string dllPath, bool isSharedFolder, bool isApproved)
    {
        var modFolder = Path.GetDirectoryName(dllPath) ?? ".";

        using var zip = new ZipArchive(new MemoryStream(download.Zip), ZipArchiveMode.Read);
        var items = zip.Entries.Select(entry => new ZipItem(entry.FullName, entry.Length)).ToList();
        var (files, placeProblem) = ZipPlan.Place(items, Path.GetFileName(dllPath), modFolder, isSharedFolder);

        if (files == null) return new InstallState.Refused(placeProblem ?? "nothing to install");

        var entryOf = files.ToDictionary(file => file.Target, file => zip.Entries.First(entry => entry.FullName == file.EntryName));
        UpdateVerdict verdict = isApproved ? new UpdateVerdict.Clear() : Review(mod.Guid, files, entryOf, dllPath);

        switch (verdict)
        {
            case UpdateVerdict.Rejected rejected:
                return new InstallState.Refused(rejected.Reason);
            case UpdateVerdict.NeedsApproval approval:
                _waiting[mod.Guid] = download;

                return new InstallState.NeedsApproval(download.Tag, approval.NewPowers);
        }

        FileSwap.Replace(DiskFiles.Instance, files.Select(file => file.Target).ToList(), (target, temporary) => entryOf[target].ExtractToFile(temporary, true), modFolder, _shelf.FolderOf(mod.Guid, mod.Version));

        // Without the list a rollback still restores the replaced files; it only leaves files the update added.
        TryRun(() => _shelf.RecordInstalled(mod.Guid, download.Tag, files.Select(file => Path.GetRelativePath(modFolder, file.Target))));

        return new InstallState.Installed(download.Tag);
    }

    /// <summary>The update's DLLs against the installed ones they replace: it must be the same mod, and anything it newly does is shown first.</summary>
    private static UpdateVerdict Review(string guid, IReadOnlyList<PlacedFile> files, IReadOnlyDictionary<string, ZipArchiveEntry> entryOf, string dllPath)
    {
        var modDll = Path.GetFullPath(dllPath);
        var dlls = files.Where(file => file.Target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
        var incoming = dlls.Select(file => (file.Target, Dll: new IncomingDll(DllScanner.Bytes(Unpack(entryOf[file.Target])), !File.Exists(file.Target)))).ToList();
        var installed = dlls.Where(file => File.Exists(file.Target)).Select(file => DllScanner.File(file.Target)).ToList();
        var newModDll = incoming.Where(dll => string.Equals(dll.Target, modDll, StringComparison.OrdinalIgnoreCase)).Select(dll => dll.Dll.Scan).FirstOrDefault();

        return newModDll == null ? new UpdateVerdict.Rejected("the zip does not hold " + Path.GetFileName(dllPath)) : UpdateReview.Judge(guid, newModDll, installed, incoming.Select(dll => dll.Dll));
    }

    private static byte[] Unpack(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var copy = new MemoryStream();

        stream.CopyTo(copy);

        return copy.ToArray();
    }

    /// <summary>An API answer: its status (0 when GitHub was not reached), and its body when 200, else the problem.</summary>
    private async Task<(int Status, byte[] Body, string? Problem)> Get(Uri uri)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _client.SendAsync(request).ConfigureAwait(false);
            var status = (int)response.StatusCode;

            if (status is 403 or 429) return (status, Array.Empty<byte>(), "GitHub's hourly limit for requests without an account was reached; try again within the hour");

            if (status != 200) return (status, Array.Empty<byte>(), "GitHub answered " + status);

            return (status, await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false), null);
        }
        catch (Exception ex)
        {
            return (0, Array.Empty<byte>(), "could not reach GitHub: " + ex.Message);
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
