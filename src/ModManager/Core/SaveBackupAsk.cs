using System;
using System.Reflection;

namespace ModManager.Core;

/// <summary>
/// Asks Save Backups to back up the saves before updates are installed. It is found by name, so neither mod needs the
/// other: a public static class SaveBackups.UpdateBackup with a public static int BeforeModUpdates() that returns how
/// many saves it is backing up. Several installs close together ask once, so a row of INSTALL UPDATE clicks does not
/// fill the backups with copies of the same moment.
/// </summary>
public sealed class SaveBackupAsk
{
    public const string TypeName = "SaveBackups.UpdateBackup";
    public const string MethodName = "BeforeModUpdates";

    /// <summary>How long after one ask the next install asks again.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMinutes(10);

    private DateTime? _askedUtc;

    /// <summary>The call into Save Backups in this assembly, or null when it has none (not installed, or too old).</summary>
    public static Func<int>? Find(Assembly? assembly)
    {
        var method = assembly?.GetType(TypeName, throwOnError: false)?.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        return method?.ReturnType == typeof(int) ? () => (int)method.Invoke(null, null)! : null;
    }

    /// <summary>True when an install now should ask, and counts it as asked.</summary>
    public bool TakeTurn(DateTime nowUtc)
    {
        if (_askedUtc is { } asked && nowUtc - asked < Quiet && nowUtc >= asked) return false;

        _askedUtc = nowUtc;

        return true;
    }
}
