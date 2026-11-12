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
/// Downloads a mod's update, from the wiki's copy first (while WikiDownloads is on) and its GitHub release when the
/// wiki has none or its copy fails, and puts the files in place for the next game start. The running
/// game keeps the DLL it loaded: Windows lets a loaded DLL be moved, so the old files are moved to a backup folder and
/// the new ones moved in beside them. Any failure puts the old files back. A rollback works the same way backwards.
/// Before anything is written, the update must match the SHA-256 in the wiki's index or come from the repository pinned
/// at the mod's first install, must be the same mod, and waits for the player when it can do something the installed
/// version could not, unless it is a release the wiki lists.
/// </summary>
internal sealed class UpdateInstaller
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _client;
    private readonly ConcurrentQueue<Action> _finished = new();
    private readonly UpdateBook _book;
    private readonly BackupShelf _shelf;
    private readonly RepositoryPins _pins;
    private readonly Func<bool> _isUsingWikiCopies;
    private readonly Action<string> _log;

    /// <summary>Checked updates waiting for the player, by mod GUID. Approving installs these bytes, not a second download.</summary>
    private readonly ConcurrentDictionary<string, Downloaded> _waiting = new(StringComparer.Ordinal);

    public UpdateInstaller(UpdateBook book, BackupShelf shelf, RepositoryPins pins, string userAgent, Func<bool> isUsingWikiCopies, Action<string> log)
    {
        _book = book;
        _isUsingWikiCopies = isUsingWikiCopies;
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

        var index = _book.Index;
        var mirrored = index.MirrorFor(mod.Guid, mod.Info.UpdateRepository, release.Tag, _isUsingWikiCopies());

        _waiting.TryRemove(mod.Guid, out _);
        _log("installing " + mod.Guid + " " + release.Tag + " over " + mod.Version + (mirrored != null ? ", from the wiki's copy " + mirrored.File : ""));
        Run(mod, release.Tag, async () =>
        {
            var (download, stopped) = mirrored != null && await FromMirror(mirrored).ConfigureAwait(false) is { } copy
                ? (new Downloaded(release.Tag, copy, IsWikiRelease: true), null)
                : await Fetch(mod, release, Path.GetFileName(dllPath), index).ConfigureAwait(false);

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

    /// <summary>Mods BROWSE was asked to install that wait for the player, by GUID, with the bytes already checked.</summary>
    private readonly ConcurrentDictionary<string, (FreshTarget Target, Downloaded Download)> _freshWaiting = new(StringComparer.Ordinal);

    /// <summary>
    /// Main thread. Installs a mod that is not installed yet into the plugins folder, used from the next game start.
    /// Does nothing while it installs, after it was installed or refused, or when a file it would write is there already.
    /// </summary>
    public void StartNew(FreshTarget target, string pluginsFolder)
    {
        if (_book.InstallOf(target.Guid) is InstallState.Installing or InstallState.Installed or InstallState.Refused) return;

        var index = _book.Index;

        _freshWaiting.TryRemove(target.Guid, out _);
        _log("installing " + target.Guid + (target.WikiCopy != null ? " from the wiki's copy " + target.WikiCopy.File : " from its GitHub releases, " + target.GitHub));
        RunNew(target, async () =>
        {
            var (download, stopped) = target.WikiCopy is { } copy ? await FromWiki(copy).ConfigureAwait(false) : await FetchNewest(target, index).ConfigureAwait(false);

            return download == null ? (stopped ?? new InstallState.Failed("nothing to install"), null) : PlaceNew(target, download, pluginsFolder, isApproved: false);
        });
    }

    /// <summary>Main thread. Installs the mod the player was shown, from the bytes already checked.</summary>
    public void ApproveNew(string guid, string pluginsFolder)
    {
        if (_book.InstallOf(guid) is not InstallState.NeedsApproval || !_freshWaiting.TryRemove(guid, out var waiting)) return;

        _log("installing " + guid + " " + waiting.Download.Tag + " as the player approved it");
        RunNew(waiting.Target, () => Task.FromResult(PlaceNew(waiting.Target, waiting.Download, pluginsFolder, isApproved: true)));
    }

    /// <summary>
    /// Main thread: deletes the files BROWSE wrote for the mod this session. The game has not loaded them, so they can
    /// go. Returns why some could not be deleted, or null when it is gone or there was nothing to take out.
    /// </summary>
    public string? UndoNew(string guid)
    {
        if (_book.InstallOf(guid) is InstallState.Installing || _book.FreshOf(guid) is not { } fresh) return null;

        var left = new List<string>();
        var problems = new List<string>();

        foreach (var file in fresh.Files)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch (Exception ex)
            {
                left.Add(file);
                problems.Add(Path.GetFileName(file) + " (" + ex.Message + ")");
            }
        }

        if (left.Count > 0)
        {
            _book.RecordFresh(guid, fresh with { Files = left });
            _log("could not take out " + guid + ": could not delete " + string.Join(", ", problems));

            return "could not delete " + string.Join(", ", problems);
        }

        _book.RecordFresh(guid, null);
        _book.Record(guid, new InstallState.None());
        _log("took out " + guid + ", installed this session, as the player asked");

        return null;
    }

    private void RunNew(FreshTarget target, Func<Task<(InstallState State, FreshInstall? Fresh)>> install)
    {
        _book.Record(target.Guid, new InstallState.Installing());

        _ = Task.Run(async () =>
        {
            (InstallState State, FreshInstall? Fresh) result;

            try
            {
                result = await install().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = (new InstallState.Failed(ex.Message), null);
            }

            _finished.Enqueue(() =>
            {
                _book.Record(target.Guid, result.State);

                if (result.Fresh != null) _book.RecordFresh(target.Guid, result.Fresh);

                _log(result.State switch
                {
                    InstallState.Failed failed => "could not install " + target.Guid + ": " + failed.Reason,
                    InstallState.Refused refused => "refused to install " + target.Guid + ": " + refused.Reason,
                    InstallState.NeedsApproval approval => target.Guid + " waits for the player, as its code can: " + string.Join(", ", approval.NewPowers),
                    InstallState.Installed installed => "installed " + target.Guid + " " + installed.Tag + "; it is used from the next start" + (result.Fresh?.Needs.Count > 0 ? "; it needs " + string.Join(", ", result.Fresh.Needs) : ""),
                    _ => target.Guid + ": " + result.State,
                });
            });
        });
    }

    /// <summary>The wiki's copy of the mod's newest release, which must match the index's hash.</summary>
    private async Task<(Downloaded? Download, InstallState? Stopped)> FromWiki(IndexRelease release)
    {
        var (body, problem) = await Download(MirrorAddress.FileUri(release.File)).ConfigureAwait(false);

        problem ??= Sha256Text.Mismatch(body, release);

        return problem == null ? (new Downloaded("v" + release.Version, body, IsWikiRelease: true), null) : (null, new InstallState.Failed("the wiki's copy " + release.File + " could not be used: " + problem));
    }

    /// <summary>The newest release of a listed mod, from its repository, pinned at this first install like any update's.</summary>
    private async Task<(Downloaded? Download, InstallState? Stopped)> FetchNewest(FreshTarget target, ModIndex index)
    {
        var repository = target.GitHub ?? "";
        var pin = _pins.Read(repository);

        if (pin is RepositoryPins.Saved.Unusable unusable) return (null, new InstallState.Refused(RepositoryPin.Unusable(unusable.Path, unusable.Problem)));

        long repositoryId;

        if (pin is RepositoryPins.Saved.Pinned pinned)
        {
            repositoryId = pinned.Id;
        }
        else
        {
            var about = await Get(RepositoryPin.RepositoryUri(repository)).ConfigureAwait(false);

            if (about.Problem != null) return (null, new InstallState.Failed(about.Problem));

            if (RepositoryPin.IdFrom(System.Text.Encoding.UTF8.GetString(about.Body)) is not { } id) return (null, new InstallState.Failed("GitHub did not say which repository " + repository + " is"));

            repositoryId = id;

            if (!_pins.Save(repository, repositoryId)) return (null, new InstallState.Failed("could not save which GitHub repository " + repository + " is"));
        }

        var answer = await Get(RepositoryPin.LatestUri(repositoryId)).ConfigureAwait(false);

        if (answer.Status == 404) return (null, new InstallState.Failed(repository + " has no release on GitHub yet"));

        if (answer.Problem != null) return (null, new InstallState.Failed(answer.Problem));

        var json = System.Text.Encoding.UTF8.GetString(answer.Body);

        if (RepositoryPin.TagFrom(json) is not { } tag) return (null, new InstallState.Failed("GitHub's newest release of " + repository + " has no tag"));

        var (asset, assetProblem) = ReleaseAssets.Choose(json, target.Dll);

        if (asset == null) return (null, new InstallState.Failed(assetProblem ?? "no file to install"));

        var download = await Download(asset.Url).ConfigureAwait(false);

        return download.Problem != null ? (null, new InstallState.Failed(download.Problem)) : (FromGitHub(target.Guid, repository, tag, download.Body, index), null);
    }

    /// <summary>
    /// Writes only the files named after the mod's DLL straight into the plugins folder, as with any mod there, and
    /// never over a file that is there: anything already written is deleted again when one write fails.
    /// </summary>
    private (InstallState State, FreshInstall? Fresh) PlaceNew(FreshTarget target, Downloaded download, string pluginsFolder, bool isApproved)
    {
        using var zip = new ZipArchive(new MemoryStream(download.Zip), ZipArchiveMode.Read);
        var items = zip.Entries.Select(entry => new ZipItem(entry.FullName, entry.Length)).ToList();
        var (files, placeProblem) = ZipPlan.Place(items, target.Dll, pluginsFolder, isSharedFolder: true);

        if (files == null) return (new InstallState.Refused(placeProblem ?? "nothing to install"), null);

        var modDll = Path.GetFullPath(Path.Combine(pluginsFolder, target.Dll));
        var dllFile = files.FirstOrDefault(file => string.Equals(file.Target, modDll, StringComparison.OrdinalIgnoreCase));

        if (dllFile == null) return (new InstallState.Refused("the zip does not hold " + target.Dll), null);

        if (files.FirstOrDefault(file => File.Exists(file.Target)) is { } taken) return (new InstallState.Failed("a file is already at " + taken.Target), null);

        var entryOf = files.ToDictionary(file => file.Target, file => zip.Entries.First(entry => entry.FullName == file.EntryName));
        var dll = Unpack(entryOf[dllFile.Target]);

        if (target.WikiCopy?.DllSha256 is { } sha && !string.Equals(Sha256Text.Of(dll), sha, StringComparison.OrdinalIgnoreCase)) return (new InstallState.Refused("its DLL is not the one the wiki's index names"), null);

        if (!isApproved)
        {
            switch (Browse.Judge(target.Guid, DllScanner.Bytes(dll), download.IsWikiRelease))
            {
                case UpdateVerdict.Rejected rejected:
                    return (new InstallState.Refused(rejected.Reason), null);
                case UpdateVerdict.NeedsApproval approval:
                    _freshWaiting[target.Guid] = (target, download);

                    return (new InstallState.NeedsApproval(download.Tag, approval.NewPowers), null);
            }
        }

        var written = new List<string>();

        try
        {
            foreach (var file in files)
            {
                var temporary = file.Target + ".mmnew";

                entryOf[file.Target].ExtractToFile(temporary, true);

                try
                {
                    File.Move(temporary, file.Target);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }

                written.Add(file.Target);
            }
        }
        catch (Exception ex)
        {
            foreach (var path in written) TryRun(() => File.Delete(path));

            return (new InstallState.Failed("could not write its files: " + ex.Message), null);
        }

        return (new InstallState.Installed(download.Tag), new FreshInstall(written, NeedsOf(dll)));
    }

    /// <summary>The GUIDs the mod's plugins name as hard dependencies; none when the DLL cannot be read for them.</summary>
    private static IReadOnlyList<string> NeedsOf(byte[] dll)
    {
        try
        {
            using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(new MemoryStream(dll));

            return PluginFileReader.Read(assembly).SelectMany(plugin => plugin.Relations).Where(relation => relation.Kind == RelationKind.Needs).Select(relation => relation.Guid).Distinct(StringComparer.Ordinal).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
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
    /// records how it went, which it also returns. Does nothing while an install of the mod runs, and returns null.
    /// </summary>
    public RollbackState? RollBack(ModEntry mod, string version, string dllPath)
    {
        if (_book.InstallOf(mod.Guid) is InstallState.Installing) return null;

        var diskVersion = Rollback.OnDisk(_book.InstallOf(mod.Guid), mod.Rollback, mod.Version);
        var backup = _shelf.FolderOf(mod.Guid, version);
        var aside = _shelf.FolderOf(mod.Guid, diskVersion);
        var (steps, problem) = RollbackPlan.For(Path.GetDirectoryName(dllPath) ?? ".", Path.GetFileName(dllPath), backup, _shelf.FilesIn(backup), _shelf.InstalledFiles(mod.Guid, diskVersion), aside);
        var result = steps == null ? new RollbackState.Failed(version, problem ?? "there is nothing to roll back") : FileSwap.Restore(DiskFiles.Instance, steps, version);

        // Undoing a rollback that fails leaves the rolled-back files in place, as the swap puts back what it moved, so the rollback still stands.
        _book.Record(mod.Guid, result is RollbackState.Failed && mod.Rollback is RollbackState.Done ? mod.Rollback : result);

        if (result is RollbackState.Done) _book.Record(mod.Guid, new InstallState.None());

        _log(result is RollbackState.Failed failed ? "could not roll " + mod.Guid + " back to " + version + ": " + failed.Reason : "rolled " + mod.Guid + " back to " + version + "; it is used from the next start, the files it replaced are in " + aside);

        return result;
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

    /// <param name="IsWikiRelease">The zip is a release the wiki lists, from the wiki or the same file from GitHub.</param>
    private sealed record Downloaded(string Tag, byte[] Zip, bool IsWikiRelease);

    /// <summary>
    /// The wiki's copy of the release, or null when it could not be had, so GitHub is asked instead. The repository
    /// pin is not needed here: the index names the mod's GUID and repository and the zip's SHA-256, and a copy that
    /// does not match the hash is thrown away. The same checks of what the zip holds run on it as on GitHub's.
    /// </summary>
    private async Task<byte[]?> FromMirror(IndexRelease release)
    {
        var (body, problem) = await Download(MirrorAddress.FileUri(release.File)).ConfigureAwait(false);

        problem ??= Sha256Text.Mismatch(body, release);

        if (problem == null) return body;

        _finished.Enqueue(() => _log("the wiki's copy " + release.File + " is not used, so GitHub is asked instead: " + problem));

        return null;
    }

    /// <summary>The release's zip, from the repository pinned at the mod's first install (pinned now when this is it).</summary>
    private async Task<(Downloaded? Download, InstallState? Stopped)> Fetch(ModEntry mod, LatestRelease.Found release, string dllName, ModIndex index)
    {
        var repository = RepositoryPin.KeyOf(mod.Info.UpdateRepository, release);
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

            // Unsaved, every later install would pin again by name, which is the check skipped.
            if (!_pins.Save(repository, repositoryId)) return (null, new InstallState.Failed("could not save which GitHub repository " + repository + " is"));
        }

        var answer = await Get(RepositoryPin.ReleaseUri(repositoryId, release.Tag)).ConfigureAwait(false);

        if (answer.Status == 404) return (null, new InstallState.Refused(RepositoryPin.NotInPinned(release.Tag)));

        if (answer.Problem != null) return (null, new InstallState.Failed(answer.Problem));

        var (asset, assetProblem) = ReleaseAssets.Choose(System.Text.Encoding.UTF8.GetString(answer.Body), dllName);

        if (asset == null) return (null, new InstallState.Failed(assetProblem ?? "no file to install"));

        var download = await Download(asset.Url).ConfigureAwait(false);

        return download.Problem != null ? (null, new InstallState.Failed(download.Problem)) : (FromGitHub(mod.Guid, repository, release.Tag, download.Body, index), null);
    }

    /// <summary>A zip from GitHub, trusted like the wiki's copy when it is a release the wiki lists.</summary>
    private Downloaded FromGitHub(string guid, string repository, string tag, byte[] zip, ModIndex index)
    {
        var isWikiRelease = index.IsWikiRelease(guid, repository, tag, zip);

        if (isWikiRelease) _finished.Enqueue(() => _log(guid + " " + tag + " from GitHub matches the wiki's list, so it is not held for the player"));

        return new Downloaded(tag, zip, isWikiRelease);
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
        UpdateVerdict verdict = isApproved ? new UpdateVerdict.Clear() : Review(mod.Guid, files, entryOf, dllPath, download.IsWikiRelease);

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
    private static UpdateVerdict Review(string guid, IReadOnlyList<PlacedFile> files, IReadOnlyDictionary<string, ZipArchiveEntry> entryOf, string dllPath, bool isWikiRelease)
    {
        var modDll = Path.GetFullPath(dllPath);
        var dlls = files.Where(file => file.Target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
        var incoming = dlls.Select(file => (file.Target, Dll: new IncomingDll(DllScanner.Bytes(Unpack(entryOf[file.Target])), !File.Exists(file.Target)))).ToList();
        var installed = dlls.Where(file => File.Exists(file.Target)).Select(file => DllScanner.File(file.Target)).ToList();
        var newModDll = incoming.Where(dll => string.Equals(dll.Target, modDll, StringComparison.OrdinalIgnoreCase)).Select(dll => dll.Dll.Scan).FirstOrDefault();

        return newModDll == null ? new UpdateVerdict.Rejected("the zip does not hold " + Path.GetFileName(dllPath)) : UpdateReview.Judge(guid, newModDll, installed, incoming.Select(dll => dll.Dll), isWikiRelease);
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

    /// <summary>Follows redirects by hand, so no hop can leave GitHub and, while WikiDownloads is on, the wiki.</summary>
    private async Task<(byte[] Body, string? Problem)> Download(Uri uri)
    {
        var wikiDownloads = _isUsingWikiCopies();

        if (!DownloadAddress.IsAllowed(uri, wikiDownloads)) return (Array.Empty<byte>(), "downloads from " + uri.Host + " are turned off");

        try
        {
            for (var hop = 0; hop <= DownloadAddress.MaxHops; hop++)
            {
                using var response = await _client.GetAsync(uri).ConfigureAwait(false);
                var status = (int)response.StatusCode;

                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    uri = DownloadAddress.Next(uri, response.Headers.Location?.OriginalString, wikiDownloads) ?? throw new InvalidOperationException("the download was sent away from GitHub" + (wikiDownloads ? " and the wiki" : "") + ": " + response.Headers.Location);

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
