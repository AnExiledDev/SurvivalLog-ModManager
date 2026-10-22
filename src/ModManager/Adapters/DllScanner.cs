using System;
using System.IO;
using System.Linq;
using ModManager.Core;
using Mono.Cecil;

namespace ModManager.Adapters;

/// <summary>Reads a DLL's plugins and what it uses from outside itself, without loading it. Never throws.</summary>
internal static class DllScanner
{
    public static DllScan File(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            return Scan(stream);
        }
        catch (Exception ex)
        {
            return new DllScan.Unreadable(ex.Message);
        }
    }

    /// <param name="bytes">A DLL unpacked from an update's zip.</param>
    public static DllScan Bytes(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, false);

            return Scan(stream);
        }
        catch (Exception ex)
        {
            return new DllScan.Unreadable(ex.Message);
        }
    }

    private static DllScan Scan(Stream stream)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true });
        var module = assembly.MainModule;
        var guids = PluginFileReader.Read(assembly).Select(plugin => plugin.Guid).ToList();
        var types = module.GetTypeReferences().Select(type => type.FullName).ToList();
        var members = module.GetMemberReferences().Where(member => member.DeclaringType != null).Select(member => member.DeclaringType.GetElementType().FullName + "::" + member.Name).ToList();
        var hasNativeImports = module.GetTypes().SelectMany(type => type.Methods).Any(method => method.IsPInvokeImpl);
        var carriesProgram = module.Resources.OfType<EmbeddedResource>().Any(IsProgram);

        return new DllScan.Read(guids, new DllFacts(types, members, hasNativeImports, carriesProgram));
    }

    /// <summary>Every Windows program and DLL starts with "MZ"; a mod's pictures and pages never do.</summary>
    private static bool IsProgram(EmbeddedResource resource)
    {
        using var stream = resource.GetResourceStream();

        return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
    }
}
