using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModManager.Core;
using Mono.Cecil;

namespace ModManager.Adapters;

/// <summary>
/// Checks what a DLL uses from the game's code against BepInEx's interop folder, which BepInEx builds from the game
/// build that is running: every game type it names, every member of one it calls or reads, and every game method a
/// HarmonyPatch attribute names. Reads DLLs without loading them. Only what the game's own code was read for and does
/// not have counts as missing: a reference that cannot be followed is left out, never called missing.
/// </summary>
internal sealed class GameReferences : IDisposable
{
    /// <summary>Base types followed when looking for a patch target, as Harmony looks in them too; far more than any game class has.</summary>
    private const int MaxBaseTypes = 32;

    private readonly DefaultAssemblyResolver _resolver = new();
    private readonly HashSet<string> _game;

    /// <param name="interop">BepInEx/interop: the game's code, as BepInEx built it for this game build.</param>
    /// <param name="core">BepInEx/core, with Harmony and Il2CppInterop, which the game's code and the patches refer to.</param>
    public GameReferences(string interop, string core)
    {
        _resolver.RemoveSearchDirectory(".");
        _resolver.RemoveSearchDirectory("bin");
        _resolver.AddSearchDirectory(interop);
        _resolver.AddSearchDirectory(core);

        // The runtime's own assemblies, where the game's base classes end.
        if (Path.GetDirectoryName(typeof(object).Assembly.Location) is { Length: > 0 } runtime) _resolver.AddSearchDirectory(runtime);

        _game = Directory.EnumerateFiles(interop, "*.dll").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>What the DLL uses from the game that it has and lacks; null when the DLL cannot be read. Never throws.</summary>
    public GameFit? Check(string dllPath)
    {
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var module = ModuleDefinition.ReadModule(stream, new ReaderParameters { AssemblyResolver = _resolver, ReadingMode = ReadingMode.Deferred, InMemory = true });

            return Check(module);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private GameFit Check(ModuleDefinition module)
    {
        var missing = new List<string>();
        var checkedCount = 0;

        // Null when the game's code could not be read for it, so it is neither counted nor called missing.
        void Note(bool? isThere, string name)
        {
            if (isThere is not { } there) return;

            checkedCount++;

            if (!there) missing.Add(name);
        }

        foreach (var type in module.GetTypeReferences().Where(IsGame))
        {
            var (isRead, definition) = Lookup(type);

            if (isRead) Note(definition != null, Name(type));
        }

        foreach (var member in module.GetMemberReferences())
        {
            if (DeclaringTypeOf(member) is not { } declaring || !IsGame(declaring)) continue;

            var (isRead, definition) = Lookup(declaring);

            if (!isRead) continue;

            // A member of a type the game no longer has is named by its type alone.
            Note(definition == null ? false : HasMember(member), definition == null ? Name(declaring) : Name(declaring) + "::" + member.Name);
        }

        foreach (var (type, name) in PatchTargets(module))
        {
            if (!IsGame(type)) continue;

            var (isRead, definition) = Lookup(type);

            if (!isRead) continue;

            Note(definition == null ? false : HasByName(definition, name), definition == null ? Name(type) : Name(type) + "::" + name);
        }

        return GameFit.Of(checkedCount, missing);
    }

    /// <summary>Of a generic type's member, the type itself; null for a member of an array or a generic parameter, which no game type declares.</summary>
    private static TypeReference? DeclaringTypeOf(MemberReference member)
    {
        var type = member.DeclaringType;

        while (type is TypeSpecification specification)
        {
            if (type is ArrayType) return null;

            type = specification.ElementType;
        }

        return type is null or GenericParameter ? null : type;
    }

    private bool IsGame(TypeReference type) => type.Scope?.Name is { Length: > 0 } scope && _game.Contains(scope.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? scope.Substring(0, scope.Length - 4) : scope);

    /// <summary>The game's definition of the type, null when the game has none; IsRead is false when the game's code could not be read for it.</summary>
    private static (bool IsRead, TypeDefinition? Definition) Lookup(TypeReference type)
    {
        try
        {
            return (true, type.Resolve());
        }
        catch (Exception)
        {
            return (false, null);
        }
    }

    private static bool? HasMember(MemberReference member)
    {
        try
        {
            return member switch
            {
                MethodReference method => method.Resolve() != null,
                FieldReference field => field.Resolve() != null,
                _ => null,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A method or a property by that name on the type or a base type, as Harmony finds a target by name.</summary>
    private static bool? HasByName(TypeDefinition type, string name)
    {
        try
        {
            TypeDefinition? definition = type;

            for (var depth = 0; depth < MaxBaseTypes && definition != null; depth++)
            {
                if (definition.Methods.Any(method => method.Name == name) || definition.Properties.Any(property => property.Name == name)) return true;
                if (definition.BaseType == null) return false;

                definition = definition.BaseType.Resolve();
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Each game method a HarmonyPatch attribute names, as Harmony puts the attributes together: a patch method's own
    /// type and name, else its class's. A patch with no method name (a constructor) or with the type given by name is left out.
    /// </summary>
    private static IEnumerable<(TypeReference Type, string Name)> PatchTargets(ModuleDefinition module)
    {
        var targets = new List<(TypeReference Type, string Name)>();

        foreach (var type in module.GetTypes())
        {
            var onClass = Target(type.CustomAttributes);

            if (onClass is { Type: { } classType, Name: { } className }) targets.Add((classType, className));

            foreach (var method in type.Methods.Where(method => method.HasCustomAttributes))
            {
                if (Target(method.CustomAttributes) is not { } onMethod) continue;

                if ((onMethod.Type ?? onClass?.Type) is { } target && (onMethod.Name ?? onClass?.Name) is { } name) targets.Add((target, name));
            }
        }

        return targets.GroupBy(target => target.Type.FullName + "::" + target.Name).Select(group => group.First());
    }

    /// <summary>The type and method name the HarmonyPatch attributes in this list give; null when there are none.</summary>
    private static (TypeReference? Type, string? Name)? Target(IEnumerable<CustomAttribute> attributes)
    {
        (TypeReference? Type, string? Name)? found = null;

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.Name != "HarmonyPatch") continue;

            TypeReference? type = null;
            string? name = null;

            try
            {
                var values = attribute.ConstructorArguments.Select(argument => argument.Value).ToList();
                var texts = values.OfType<string>().ToList();

                type = values.OfType<TypeReference>().FirstOrDefault();

                // HarmonyPatch("Namespace.Type", "Method") names its type by text, which is not followed here.
                name = type == null && texts.Count > 1 ? null : texts.FirstOrDefault();
            }
            catch (Exception)
            {
                // An argument that cannot be read (an enum from a Harmony this folder does not have) says nothing.
            }

            found = (type ?? found?.Type, name ?? found?.Name);
        }

        return found;
    }

    private static string Name(TypeReference type) => type.GetElementType().FullName.Replace('/', '.');

    public void Dispose() => _resolver.Dispose();
}
