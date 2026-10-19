using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace ModManager.Adapters;

/// <summary>One plugin declared in a DLL, with the assembly metadata of that DLL.</summary>
internal sealed record PluginFile(string Guid, string Name, string Version, IReadOnlyList<KeyValuePair<string, string?>> Metadata);

/// <summary>
/// Reads plugin DLLs without loading them, so a turned-off mod shows its name and info, and a loaded one is read the
/// same way.
/// </summary>
internal static class PluginFileReader
{
    private const string PluginAttribute = "BepInEx.BepInPlugin";
    private const string MetadataAttribute = "System.Reflection.AssemblyMetadataAttribute";

    /// <summary>Shares the file with everyone, so a DLL the game has loaded can still be read and moved.</summary>
    public static IReadOnlyList<PluginFile> Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { ReadingMode = ReadingMode.Deferred });
        var metadata = assembly.CustomAttributes
            .Where(attribute => attribute.AttributeType.FullName == MetadataAttribute && attribute.ConstructorArguments.Count == 2)
            .Select(attribute => new KeyValuePair<string, string?>(attribute.ConstructorArguments[0].Value as string ?? "", attribute.ConstructorArguments[1].Value as string))
            .ToList();

        return assembly.MainModule.GetTypes()
            .SelectMany(type => type.CustomAttributes)
            .Where(attribute => attribute.AttributeType.FullName == PluginAttribute && attribute.ConstructorArguments.Count == 3)
            .Select(attribute => new PluginFile(Text(attribute, 0), Text(attribute, 1), Text(attribute, 2), metadata))
            .Where(plugin => plugin.Guid.Length > 0)
            .ToList();
    }

    private static string Text(CustomAttribute attribute, int index) => attribute.ConstructorArguments[index].Value as string ?? "";
}
