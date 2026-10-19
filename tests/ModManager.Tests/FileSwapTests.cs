using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>An update or rollback either lands whole or leaves the mod's files exactly as they were.</summary>
public class FileSwapTests
{
    private const string ModFolder = @"C:\Game\BepInEx\plugins\Mod";
    private const string Backup = @"C:\Game\BepInEx\mod-manager\backups\com.x\1.0.0";
    private static readonly string Dll = Path.Combine(ModFolder, "Mod.dll");
    private static readonly string Data = Path.Combine(ModFolder, "data.json");
    private static readonly string Added = Path.Combine(ModFolder, "added.txt");

    [Fact]
    public void An_update_puts_the_new_files_in_place_and_keeps_the_old_ones_in_the_backup()
    {
        var disk = new MemoryDisk((Dll, "old dll"), (Data, "old data"));

        FileSwap.Replace(disk, new[] { Dll, Data, Added }, WriteNew(disk), ModFolder, Backup);

        Assert.Equal("new " + Dll, disk.Files[Dll]);
        Assert.Equal("new " + Added, disk.Files[Added]);
        Assert.Equal("old dll", disk.Files[Path.Combine(Backup, "Mod.dll")]);
        Assert.Equal("old data", disk.Files[Path.Combine(Backup, "data.json")]);
        Assert.DoesNotContain(disk.Files.Keys, path => path.EndsWith(".new", StringComparison.Ordinal));
    }

    [Fact]
    public void A_failure_while_moving_the_new_files_in_puts_every_old_file_back()
    {
        var disk = new MemoryDisk((Dll, "old dll"), (Data, "old data"));

        disk.FailWhen = (operation, from, to) => operation == "move" && from == Data + ".new";

        Assert.ThrowsAny<IOException>(() => FileSwap.Replace(disk, new[] { Dll, Data, Added }, WriteNew(disk), ModFolder, Backup));
        Assert.Equal(new Dictionary<string, string> { [Dll] = "old dll", [Data] = "old data" }, disk.Files);
    }

    [Fact]
    public void A_failure_while_writing_the_new_files_changes_nothing()
    {
        var disk = new MemoryDisk((Dll, "old dll"));

        void WriteFirstThenFail(string target, string temporary)
        {
            if (target == Data) throw new IOException("disk full");

            disk.Files[temporary] = "new " + target;
        }

        Assert.ThrowsAny<IOException>(() => FileSwap.Replace(disk, new[] { Dll, Data }, WriteFirstThenFail, ModFolder, Backup));
        Assert.Equal(new Dictionary<string, string> { [Dll] = "old dll" }, disk.Files);
    }

    [Fact]
    public void A_rollback_puts_the_backup_back_and_sets_the_newer_files_aside()
    {
        var aside = Path.Combine(Backup, "..", "1.1.0", "Mod.dll");
        var disk = new MemoryDisk((Dll, "new dll"), (Path.Combine(Backup, "Mod.dll"), "old dll"));
        var steps = new RollbackSteps(new[] { new FileStep(Dll, aside) }, new[] { new FileStep(Path.Combine(Backup, "Mod.dll"), Dll) });

        var result = FileSwap.Restore(disk, steps, "1.0.0");

        Assert.Equal(new RollbackState.Done("1.0.0"), result);
        Assert.Equal("old dll", disk.Files[Dll]);
        Assert.Equal("new dll", disk.Files[aside]);
        Assert.DoesNotContain(disk.Files.Keys, path => path.EndsWith(".rollback", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rollback_that_fails_halfway_leaves_the_newer_files_running()
    {
        var aside = Path.Combine(Backup, "..", "1.1.0", "Mod.dll");
        var backupDll = Path.Combine(Backup, "Mod.dll");
        var disk = new MemoryDisk((Dll, "new dll"), (backupDll, "old dll"));
        var steps = new RollbackSteps(new[] { new FileStep(Dll, aside) }, new[] { new FileStep(backupDll, Dll) });

        disk.FailWhen = (operation, from, to) => operation == "move" && from == Dll + ".rollback";

        var result = FileSwap.Restore(disk, steps, "1.0.0");

        Assert.IsType<RollbackState.Failed>(result);
        Assert.Equal(new Dictionary<string, string> { [Dll] = "new dll", [backupDll] = "old dll" }, disk.Files);
    }

    private static Action<string, string> WriteNew(MemoryDisk disk) => (target, temporary) => disk.Files[temporary] = "new " + target;

    /// <summary>Files as path to content. Moves refuse a taken target the way File.Move does.</summary>
    private sealed class MemoryDisk : IFileMoves
    {
        public MemoryDisk(params (string Path, string Content)[] files)
        {
            foreach (var (path, content) in files) Files[path] = content;
        }

        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Func<string, string, string, bool> FailWhen { get; set; } = (_, _, _) => false;

        public bool Exists(string path) => Files.ContainsKey(path);

        public void MakeFolderFor(string path)
        {
        }

        public void Move(string from, string to)
        {
            if (FailWhen("move", from, to)) throw new IOException("the file is in use");

            if (!Files.ContainsKey(from)) throw new FileNotFoundException(from);

            if (Files.ContainsKey(to)) throw new IOException(to + " already exists");

            Files[to] = Files[from];
            Files.Remove(from);
        }

        public void Copy(string from, string to)
        {
            if (FailWhen("copy", from, to)) throw new IOException("the disk is full");

            Files[to] = Files.TryGetValue(from, out var content) ? content : throw new FileNotFoundException(from);
        }

        public void Delete(string path) => Files.Remove(path);
    }
}
