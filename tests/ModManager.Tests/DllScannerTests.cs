using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ModManager.Adapters;
using ModManager.Core;
using Mono.Cecil;
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
    public void A_mod_with_an_optional_dependency_is_read_even_where_BepInEx_cannot_be_found()
    {
        var scan = DllScanner.Bytes(ModWithOptionalDependency());

        Assert.Equal(new[] { "ann.mod" }, Assert.IsType<DllScan.Read>(scan).PluginGuids);
    }

    /// <summary>
    /// Shaped like every mod built against BepInEx: BepInPlugin and BepInDependency(guid, DependencyFlags.SoftDependency),
    /// both from a BepInEx.Core this test never has, so the flags cannot be decoded.
    /// </summary>
    private static byte[] ModWithOptionalDependency()
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("AnnMod", new Version(1, 0)), "AnnMod", ModuleKind.Dll);
        var module = assembly.MainModule;
        var core = new AssemblyNameReference("BepInEx.Core", new Version(6, 0, 0, 0));
        var type = new TypeDefinition("AnnMod", "Plugin", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, module.TypeSystem.Object);

        module.AssemblyReferences.Add(core);
        type.CustomAttributes.Add(Plugin(module, core, "ann.mod"));
        type.CustomAttributes.Add(OptionalDependency(module, core, "bob.mod"));
        module.Types.Add(type);

        using var stream = new MemoryStream();

        assembly.Write(stream);

        return stream.ToArray();
    }

    private static CustomAttribute Plugin(ModuleDefinition module, AssemblyNameReference core, string guid)
    {
        var ctor = new MethodReference(".ctor", module.TypeSystem.Void, new TypeReference("BepInEx", "BepInPlugin", module, core)) { HasThis = true };

        for (var i = 0; i < 3; i++) ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));

        var attribute = new CustomAttribute(ctor);

        foreach (var text in new[] { guid, "Ann Mod", "1.0.0" }) attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, text));

        return attribute;
    }

    /// <summary>The flags go in as raw bytes, because writing them any other way needs BepInEx.Core too.</summary>
    private static CustomAttribute OptionalDependency(ModuleDefinition module, AssemblyNameReference core, string guid)
    {
        var attributeType = new TypeReference("BepInEx", "BepInDependency", module, core);
        var flags = new TypeReference("", "DependencyFlags", module, core) { DeclaringType = attributeType, IsValueType = true };
        var ctor = new MethodReference(".ctor", module.TypeSystem.Void, attributeType) { HasThis = true };
        const int softDependency = 2;
        var blob = new byte[] { 1, 0, (byte)guid.Length }
            .Concat(System.Text.Encoding.UTF8.GetBytes(guid))
            .Concat(BitConverter.GetBytes(softDependency))
            .Concat(new byte[] { 0, 0 })
            .ToArray();

        ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        ctor.Parameters.Add(new ParameterDefinition(flags));

        return new CustomAttribute(ctor, blob);
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

    [Fact]
    public void Saving_again_replaces_the_pin_and_leaves_no_other_file()
    {
        Assert.True(Pins().Save("Ann/Mod", 1));
        Assert.True(Pins().Save("Ann/Mod", 2));

        Assert.Equal(2L, Assert.IsType<RepositoryPins.Saved.Pinned>(Pins().Read("Ann/Mod")).Id);
        Assert.Equal(new[] { "mod.txt" }, Directory.GetFiles(Path.Combine(_root, "ann")).Select(Path.GetFileName));
    }
}
