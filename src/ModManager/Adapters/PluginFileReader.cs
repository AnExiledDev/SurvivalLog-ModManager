using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModManager.Core;
using Mono.Cecil;

namespace ModManager.Adapters;

/// <summary>One plugin declared in a DLL, with the assembly metadata of that DLL and the plugins it names as related.</summary>
internal sealed record PluginFile(string Guid, string Name, string Version, IReadOnlyList<KeyValuePair<string, string?>> Metadata, IReadOnlyList<DeclaredRelation> Relations);

/// <summary>
/// Reads plugin DLLs without loading them, so a turned-off mod shows its name and info, and a loaded one is read the
/// same way.
/// </summary>
internal static class PluginFileReader
{
    private const string PluginAttribute = "BepInEx.BepInPlugin";
    private const string DependencyAttribute = "BepInEx.BepInDependency";
    private const string IncompatibilityAttribute = "BepInEx.BepInIncompatibility";
    private const string MetadataAttribute = "System.Reflection.AssemblyMetadataAttribute";

    /// <summary>BepInDependency.DependencyFlags.SoftDependency.</summary>
    private const int SoftFlag = 2;

    /// <summary>Shares the file with everyone, so a DLL the game has loaded can still be read and moved.</summary>
    public static IReadOnlyList<PluginFile> Read(string path)
    {
        using var assembly = Open(path);
        var metadata = assembly.CustomAttributes
            .Where(attribute => attribute.AttributeType.FullName == MetadataAttribute && attribute.ConstructorArguments.Count == 2)
            .Select(attribute => new KeyValuePair<string, string?>(attribute.ConstructorArguments[0].Value as string ?? "", attribute.ConstructorArguments[1].Value as string))
            .ToList();

        return assembly.MainModule.GetTypes()
            .SelectMany(type => type.CustomAttributes
                .Where(attribute => attribute.AttributeType.FullName == PluginAttribute && attribute.ConstructorArguments.Count == 3)
                .Select(attribute => new PluginFile(Text(attribute, 0), Text(attribute, 1), Text(attribute, 2), metadata, RelationsOf(type))))
            .Where(plugin => plugin.Guid.Length > 0)
            .ToList();
    }

    /// <summary>The bytes of a resource embedded in the DLL, or null when it has none by that name or it is larger than the limit.</summary>
    public static byte[]? Resource(string path, string name, int maxBytes)
    {
        using var assembly = Open(path);
        var resource = assembly.MainModule.Resources.OfType<EmbeddedResource>().FirstOrDefault(found => found.Name == name);

        if (resource == null) return null;

        using var stream = resource.GetResourceStream();

        if (stream.Length > maxBytes) return new byte[maxBytes + 1];

        using var copy = new MemoryStream();

        stream.CopyTo(copy);

        return copy.ToArray();
    }

    private static AssemblyDefinition Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        try
        {
            return AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { ReadingMode = ReadingMode.Deferred, InMemory = true });
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>BepInDependency(guid, flags) or BepInDependency(guid, versionRange), which is hard; BepInIncompatibility(guid).</summary>
    private static IReadOnlyList<DeclaredRelation> RelationsOf(TypeDefinition type)
    {
        var relations = new List<DeclaredRelation>();

        foreach (var attribute in type.CustomAttributes)
        {
            if (attribute.ConstructorArguments.Count == 0 || attribute.ConstructorArguments[0].Value is not string { Length: > 0 } guid) continue;

            switch (attribute.AttributeType.FullName)
            {
                case DependencyAttribute:
                    var second = attribute.ConstructorArguments.Count > 1 ? attribute.ConstructorArguments[1].Value : null;
                    var isSoft = FlagsOf(second) is { } flags && (flags & SoftFlag) != 0;

                    relations.Add(new DeclaredRelation(isSoft ? RelationKind.WorksWith : RelationKind.Needs, guid, second as string is { Length: > 0 } range ? range : null));
                    break;
                case IncompatibilityAttribute:
                    relations.Add(new DeclaredRelation(RelationKind.DoesNotWorkWith, guid));
                    break;
            }
        }

        return relations;
    }

    /// <summary>An enum argument reads as its underlying number, sometimes boxed once more inside a CustomAttributeArgument.</summary>
    private static int? FlagsOf(object? value) => value switch
    {
        int number => number,
        CustomAttributeArgument inner => FlagsOf(inner.Value),
        _ => null,
    };

    private static string Text(CustomAttribute attribute, int index) => attribute.ConstructorArguments[index].Value as string ?? "";
}
