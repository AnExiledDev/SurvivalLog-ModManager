using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ModManager.Adapters;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class DllScannerTests
{
    private static DllScan.Read ThisDll() => Assert.IsType<DllScan.Read>(DllScanner.File(typeof(DllScannerTests).Assembly.Location));

    [Fact]
    public void Windows_functions_a_dll_imports_are_read_by_their_real_name()
    {
        var imports = ThisDll().Facts.NativeImports;

        Assert.Contains("CreateProcessW", imports);
        Assert.Contains("RegOpenKeyExW", imports);
    }

    [Fact]
    public void A_dll_that_imports_them_can_start_programs_and_use_the_registry()
    {
        var powers = Powers.Of(ThisDll().Facts);

        Assert.Contains(Power.StartsPrograms, powers);
        Assert.Contains(Power.Registry, powers);
        Assert.Contains(Power.NativeCode, powers);
    }

    [Fact]
    public void Bytes_that_are_not_a_dll_are_unreadable_instead_of_throwing()
    {
        Assert.IsType<DllScan.Unreadable>(DllScanner.Bytes(new byte[] { 1, 2, 3 }));
    }

    // Never called: they exist so this test DLL imports these functions, the way a harmful update would.
    private static class NeverCalled
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW")]
        public static extern bool StartsAProgram(IntPtr application);

        [DllImport("advapi32.dll")]
        public static extern int RegOpenKeyExW(IntPtr key, string subKey, int options, int access, out IntPtr result);
    }
}

public class RepositoryPinsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mm-pins-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private RepositoryPins Pins() => new(_root, _ => { });

    [Fact]
    public void A_repository_never_installed_from_has_no_pin()
    {
        Assert.IsType<RepositoryPins.Saved.None>(Pins().Read("Ann/Mod"));
    }

    [Fact]
    public void A_saved_pin_reads_back_whatever_the_case_of_the_name()
    {
        Pins().Save("Ann/Mod", 42);

        Assert.Equal(42L, Assert.IsType<RepositoryPins.Saved.Pinned>(Pins().Read("ann/MOD")).Id);
    }

    [Fact]
    public void A_damaged_pin_file_stops_installs_instead_of_pinning_again()
    {
        Directory.CreateDirectory(Path.Combine(_root, "ann"));
        File.WriteAllText(Path.Combine(_root, "ann", "mod.txt"), "not a number");

        var unusable = Assert.IsType<RepositoryPins.Saved.Unusable>(Pins().Read("Ann/Mod"));

        Assert.Equal(Path.Combine(_root, "ann", "mod.txt"), unusable.Path);
    }
}
