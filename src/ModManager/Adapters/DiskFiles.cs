using System.IO;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>The real disk behind a file swap.</summary>
internal sealed class DiskFiles : IFileMoves
{
    public static readonly DiskFiles Instance = new();

    private DiskFiles()
    {
    }

    public bool Exists(string path) => File.Exists(path);

    public void MakeFolderFor(string path) => Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

    public void Move(string from, string to) => File.Move(from, to);

    public void Copy(string from, string to) => File.Copy(from, to, true);

    public void Delete(string path) => File.Delete(path);
}
