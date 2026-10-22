using System;
using System.IO;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>Going back to the files the last update replaced, without ever landing on a newer version or outside the mod's folder.</summary>
public class RollbackTests
{
    // Rooted for whichever system runs the tests, so the plan's path checks see real absolute paths.
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\" : "/";
    private static readonly string ModFolder = Path.Combine(Root, "Game", "BepInEx", "plugins", "Mod");
    private static readonly string Backup = Path.Combine(Root, "Game", "BepInEx", "mod-manager", "backups", "com.x", "1.0.0");
    private static readonly string Aside = Path.Combine(Root, "Game", "BepInEx", "mod-manager", "backups", "com.x", "1.2.0");
    private static readonly string DataFile = Path.Combine("data", "a.txt");

    [Fact]
    public void After_a_restart_the_target_is_the_newest_backup_older_than_the_running_version()
    {
        var backups = new[] { "0.9.0", "1.1.0", "1.0.0", "1.3.0", "not a version" };

        Assert.Equal("1.1.0", Rollback.Target(backups, "1.2.0", new InstallState.None()));
        Assert.Null(Rollback.Target(new[] { "1.3.0" }, "1.2.0", new InstallState.None()));
        Assert.Null(Rollback.Target(backups, "unknown", new InstallState.None()));
    }

    [Fact]
    public void Right_after_an_install_the_target_is_the_running_version_it_replaced()
    {
        Assert.Equal("1.2.0", Rollback.Target(new[] { "1.1.0", "1.2.0" }, "1.2.0", new InstallState.Installed("v1.3.0")));
        Assert.Null(Rollback.Target(new[] { "1.1.0" }, "1.2.0", new InstallState.Installed("v1.3.0")));
    }

    [Fact]
    public void Nothing_rolls_back_while_an_install_runs()
    {
        Assert.Null(Rollback.Target(new[] { "1.1.0" }, "1.2.0", new InstallState.Installing()));
    }

    [Fact]
    public void A_backup_named_after_a_tag_matches_the_plugins_version()
    {
        Assert.True(BackupFolder.IsSameVersion("v1.2.0", "1.2.0"));
        Assert.True(BackupFolder.IsSameVersion("odd name", "odd name"));
        Assert.False(BackupFolder.IsSameVersion("v1.2.1", "1.2.0"));
        Assert.Equal("_", BackupFolder.Name(".."));
        Assert.Equal("a_b", BackupFolder.Name("a/b"));
        if (OperatingSystem.IsWindows()) Assert.Equal("a_b", BackupFolder.Name("a\\b"));
    }

    [Fact]
    public void A_failed_rollback_can_be_tried_again_and_a_done_one_cannot()
    {
        Assert.Equal("1.0.0", Rollback.VersionToTry(new RollbackState.Available("1.0.0")));
        Assert.Equal("1.0.0", Rollback.VersionToTry(new RollbackState.Failed("1.0.0", "in use")));
        Assert.Null(Rollback.VersionToTry(new RollbackState.Done("1.0.0")));
        Assert.Null(Rollback.VersionToTry(new RollbackState.None()));
    }

    [Fact]
    public void The_book_offers_a_rollback_until_one_is_done_and_then_blocks_installs()
    {
        var book = new UpdateBook();

        Assert.Equal(new RollbackState.Available("1.0.0"), book.RollbackOf("com.x", new[] { "1.0.0" }, "1.2.0"));
        Assert.Equal(new RollbackState.None(), book.RollbackOf("com.x", new string[0], "1.2.0"));
        Assert.False(book.HasRolledBack("com.x"));

        book.Record("com.x", new RollbackState.Done("1.0.0"));

        Assert.Equal(new RollbackState.Done("1.0.0"), book.RollbackOf("com.x", new[] { "1.0.0" }, "1.2.0"));
        Assert.True(book.HasRolledBack("com.x"));
        Assert.False(book.HasRolledBack("com.y"));
    }

    [Fact]
    public void A_rollback_sets_aside_the_current_files_and_copies_the_backup_back()
    {
        var (steps, problem) = RollbackPlan.For(ModFolder, "Mod.dll", Backup, new[] { "Mod.dll", DataFile }, new[] { "Mod.dll", "New.dll" }, Aside);

        Assert.Null(problem);
        Assert.NotNull(steps);
        Assert.Equal(new[] { "Mod.dll", DataFile, "New.dll" }, steps.SetAside.Select(step => Path.GetRelativePath(ModFolder, step.From)));
        Assert.All(steps.SetAside, step => Assert.StartsWith(Aside, step.To));
        Assert.Equal(new[] { new FileStep(Path.Combine(Backup, "Mod.dll"), Path.Combine(ModFolder, "Mod.dll")), new FileStep(Path.Combine(Backup, DataFile), Path.Combine(ModFolder, DataFile)) }, steps.CopyBack);
    }

    [Fact]
    public void A_backup_without_the_mods_dll_is_refused()
    {
        var (steps, problem) = RollbackPlan.For(ModFolder, "Mod.dll", Backup, new[] { "Mod.pdb" }, new string[0], Aside);

        Assert.Null(steps);
        Assert.Contains("Mod.dll", problem);
    }

    [Fact]
    public void A_listed_file_outside_the_mods_folder_is_refused()
    {
        var (steps, problem) = RollbackPlan.For(ModFolder, "Mod.dll", Backup, new[] { "Mod.dll" }, new[] { Path.Combine("..", "Other", "Other.dll") }, Aside);

        Assert.Null(steps);
        Assert.Contains("outside", problem);
    }

    [Fact]
    public void Rolling_back_to_the_version_on_disk_is_refused()
    {
        var (steps, _) = RollbackPlan.For(ModFolder, "Mod.dll", Backup, new[] { "Mod.dll" }, new string[0], Backup);

        Assert.Null(steps);
    }
}
