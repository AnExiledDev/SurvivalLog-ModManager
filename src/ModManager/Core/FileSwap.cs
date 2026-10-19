using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModManager.Core;

/// <summary>The file operations a swap needs. The game uses the real disk; tests use memory.</summary>
public interface IFileMoves
{
    bool Exists(string path);

    void MakeFolderFor(string path);

    /// <summary>Fails when <paramref name="to"/> exists.</summary>
    void Move(string from, string to);

    /// <summary>Overwrites <paramref name="to"/>.</summary>
    void Copy(string from, string to);

    void Delete(string path);
}

/// <summary>
/// Swaps a mod's files as one step. The incoming files are written beside their targets first, so a full disk or a
/// bad file changes nothing, and a failure after that puts every old file back where it was.
/// </summary>
public static class FileSwap
{
    /// <summary>
    /// Puts the new copy of each target in place and keeps the file it replaces in <paramref name="backup"/>, at the
    /// same place below it as below <paramref name="modFolder"/>. On failure it undoes what it did, then throws.
    /// </summary>
    /// <param name="writeNew">Writes the new copy of a target (first argument) to a temporary path (second).</param>
    public static void Replace(IFileMoves disk, IReadOnlyList<string> targets, Action<string, string> writeNew, string modFolder, string backup)
    {
        var written = new List<string>();
        var kept = new List<FileStep>();
        var placed = new List<string>();

        try
        {
            foreach (var target in targets)
            {
                var temporary = target + ".new";

                disk.MakeFolderFor(target);
                written.Add(temporary);
                writeNew(target, temporary);
            }

            foreach (var target in targets.Where(disk.Exists))
            {
                var copy = Path.Combine(backup, Path.GetRelativePath(modFolder, target));

                disk.MakeFolderFor(copy);

                if (disk.Exists(copy)) disk.Delete(copy);

                disk.Move(target, copy);
                kept.Add(new FileStep(copy, target));
            }

            foreach (var target in targets)
            {
                disk.Move(target + ".new", target);
                placed.Add(target);
            }
        }
        catch
        {
            Undo(disk, placed, kept, written);

            throw;
        }
    }

    /// <summary>
    /// Copies the backup in beside its targets, sets the current files aside, then moves the copies into place. On
    /// failure it undoes what it did and reports why.
    /// </summary>
    public static RollbackState Restore(IFileMoves disk, RollbackSteps steps, string version)
    {
        var written = new List<string>();
        var setAside = new List<FileStep>();
        var placed = new List<string>();

        try
        {
            foreach (var copy in steps.CopyBack)
            {
                disk.MakeFolderFor(copy.To);
                written.Add(copy.To + ".rollback");
                disk.Copy(copy.From, copy.To + ".rollback");
            }

            foreach (var step in steps.SetAside.Where(step => disk.Exists(step.From)))
            {
                disk.MakeFolderFor(step.To);

                if (disk.Exists(step.To)) disk.Delete(step.To);

                disk.Move(step.From, step.To);
                setAside.Add(new FileStep(step.To, step.From));
            }

            foreach (var copy in steps.CopyBack)
            {
                disk.Move(copy.To + ".rollback", copy.To);
                placed.Add(copy.To);
            }
        }
        catch (Exception ex)
        {
            Undo(disk, placed, setAside, written);

            return new RollbackState.Failed(version, ex.Message);
        }

        return new RollbackState.Done(version);
    }

    /// <summary>Best effort, newest change first: remove what was placed, move back what was moved away, delete the temporary copies.</summary>
    private static void Undo(IFileMoves disk, IEnumerable<string> placed, IEnumerable<FileStep> movedAway, IEnumerable<string> temporaries)
    {
        foreach (var target in placed) TryRun(() => disk.Delete(target));

        foreach (var step in movedAway) TryRun(() => disk.Move(step.From, step.To));

        foreach (var temporary in temporaries.Where(disk.Exists)) TryRun(() => disk.Delete(temporary));
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Undoing goes on with the next file; one that cannot be put back is no reason to leave the rest.
        }
    }
}
