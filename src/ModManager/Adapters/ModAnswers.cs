using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Asks a mod about its item lists through a static class named ModManagerAnswers in its own DLL, found by name, so the
/// mod needs no reference to the Mod Manager. Each method takes the setting's section and key and returns a string,
/// and each is optional: Problem says why the mod cannot use the value as it stands ("" when it can), Blank gives the
/// list a blank value stands for now, and BlankNote a line about that list. Problem and BlankNote are "English\nChinese".
/// A method that throws is logged once and counts as no answer.
/// </summary>
internal sealed class ModAnswers
{
    public const string TypeName = "ModManagerAnswers";

    private static readonly Type[] SectionAndKey = { typeof(string), typeof(string) };

    private readonly Action<string> _log;
    private readonly HashSet<string> _reported = new();
    private readonly Dictionary<Assembly, Methods?> _byAssembly = new();

    public ModAnswers(Action<string> log)
    {
        _log = log;
    }

    private sealed record Methods(MethodInfo? Problem, MethodInfo? Blank, MethodInfo? BlankNote);

    public ModAnswer For(string guid, Assembly assembly, string section, string key)
    {
        var methods = MethodsOf(assembly);

        if (methods == null) return ModAnswer.None;

        var (problemAsked, problem) = Ask(guid, methods.Problem, section, key);
        var (_, blank) = Ask(guid, methods.Blank, section, key);
        var (_, note) = Ask(guid, methods.BlankNote, section, key);

        return new ModAnswer(problemAsked, Words(problem), blank?.Trim(), Words(note));
    }

    private static Description? Words(string? text) => string.IsNullOrWhiteSpace(text) ? null : Wording.SplitDescription(text);

    /// <summary>Whether the method answered, and what it said.</summary>
    private (bool Answered, string? Text) Ask(string guid, MethodInfo? method, string section, string key)
    {
        if (method == null) return (false, null);

        try
        {
            return (true, method.Invoke(null, new object[] { section, key }) as string);
        }
        catch (Exception ex)
        {
            var reason = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

            if (_reported.Add(guid + "/" + method.Name + "/" + section + "/" + key)) _log(guid + " " + TypeName + "." + method.Name + " failed for " + section + "." + key + ", so it is not used: " + reason);

            return (false, null);
        }
    }

    private Methods? MethodsOf(Assembly assembly)
    {
        if (_byAssembly.TryGetValue(assembly, out var known)) return known;

        var type = TypesOf(assembly).FirstOrDefault(type => type.Name == TypeName && type.IsAbstract && type.IsSealed);
        var methods = type == null ? null : new Methods(Method(type, "Problem"), Method(type, "Blank"), Method(type, "BlankNote"));

        _byAssembly[assembly] = methods;

        return methods;
    }

    private static MethodInfo? Method(Type type, string name)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, SectionAndKey, null);

        return method != null && method.ReturnType == typeof(string) ? method : null;
    }

    /// <summary>A DLL that refers to something missing still gives the types it could load.</summary>
    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null).Select(type => type!);
        }
    }
}
