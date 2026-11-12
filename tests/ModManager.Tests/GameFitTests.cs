using System.Text.Json;
using ModManager.Adapters;
using ModManager.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// A mod built for an older game build can call a game method the game no longer has, and fails only when that code
/// runs. Each DLL is checked against the game's code in BepInEx's interop folder: what it names that the game lacks is
/// listed, what cannot be read is never called missing, and the checks are kept until the game's code changes.
/// </summary>
public sealed class GameFitTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "slmm-fit-" + Guid.NewGuid().ToString("N"));
    private readonly string _interop;
    private readonly string _core;
    private readonly List<string> _log = new();

    public GameFitTests()
    {
        _interop = Path.Combine(_root, "interop");
        _core = Path.Combine(_root, "core");
        Directory.CreateDirectory(_interop);
        Directory.CreateDirectory(_core);
        WriteGame(Path.Combine(_interop, "Assembly-CSharp.dll"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    // ===== The check of one DLL =====

    [Fact]
    public void What_the_game_no_longer_has_is_listed_and_what_it_has_is_not()
    {
        var fit = Check(WriteMod("DoorMod.dll"));

        Assert.Equal(new[] { "Game.Door::Shut", "Game.Door::Slam", "Game.Window" }, fit.Missing);
        Assert.Equal(3, fit.MissingCount);
    }

    [Fact]
    public void Every_game_type_member_and_patch_target_is_counted_and_nothing_from_other_assemblies()
    {
        // Door and Window; Open, Shut and Close; the patches of Gate.Open, Door.Slam and Door.Locked. Not Other.Thing.Do.
        Assert.Equal(8, Check(WriteMod("DoorMod.dll")).Checked);
    }

    [Fact]
    public void A_dll_that_uses_nothing_from_the_game_has_nothing_missing()
    {
        var fit = Check(WriteEmpty("Plain.dll"));

        Assert.Equal(0, fit.Checked);
        Assert.False(fit.IsMissingAny);
    }

    [Fact]
    public void A_file_that_is_not_a_dll_is_not_checked_rather_than_called_broken()
    {
        var path = Path.Combine(_root, "notes.dll");

        File.WriteAllText(path, "not a program");

        using var game = new GameReferences(_interop, _core);

        Assert.Null(game.Check(path));
    }

    // ===== Kept between starts =====

    [Fact]
    public void A_check_runs_in_the_background_and_a_later_start_uses_the_saved_one_without_reading_the_dll()
    {
        var mod = WriteMod("DoorMod.dll");
        var sha = Sha(mod);
        var first = Checks();

        first.Start(new[] { (mod, sha) });

        Assert.Null(first.FitOf(sha));
        WaitFinished(first);
        Assert.Equal(3, first.FitOf(sha)?.MissingCount);

        File.Delete(mod);

        var second = Checks();

        second.Start(new[] { (mod, sha) });

        Assert.Equal(3, second.FitOf(sha)?.MissingCount);
        Assert.False(second.TakeFinished());
    }

    [Fact]
    public void A_new_game_build_checks_every_dll_again()
    {
        var mod = WriteMod("DoorMod.dll");
        var sha = Sha(mod);
        var first = Checks();

        first.Start(new[] { (mod, sha) });
        WaitFinished(first);

        // BepInEx rebuilds the interop folder after a game update, and the rebuilt game has Door.Shut.
        WriteGame(Path.Combine(_interop, "Assembly-CSharp.dll"), withShut: true);

        var second = Checks();

        second.Start(new[] { (mod, sha) });

        Assert.Null(second.FitOf(sha));
        WaitFinished(second);
        Assert.Equal(new[] { "Game.Door::Slam", "Game.Window" }, second.FitOf(sha)?.Missing);
    }

    [Fact]
    public void Without_an_interop_folder_nothing_is_checked_and_it_says_why()
    {
        var checks = new GameFitChecks(Path.Combine(_root, "none"), _core, new SavedFile(Path.Combine(_root, "saved.json"), "checks", _log.Add), _log.Add);

        checks.Start(new[] { (WriteMod("DoorMod.dll"), new string('a', 64)) });

        Assert.Null(checks.FitOf(new string('a', 64)));
        Assert.Contains(_log, line => line.Contains("there is no") && line.Contains("none"));
    }

    // ===== The saved copy =====

    private static readonly GameFit Outdated = new(10, 2, new[] { "Game.Door::Shut", "Game.Window" });

    [Fact]
    public void The_saved_copy_reads_back_as_it_was()
    {
        var cache = new GameFitCache("stamp", new Dictionary<string, GameFit> { [new string('a', 64)] = Outdated });
        var read = GameFitCache.FromJson(cache.ToJson());

        Assert.NotNull(read);
        Assert.Equal("stamp", read!.Stamp);
        Assert.Equal(Outdated.Missing, read.ByDll[new string('a', 64)].Missing);
        Assert.Equal(2, read.ByDll[new string('a', 64)].MissingCount);
        Assert.Equal(10, read.ByDll[new string('a', 64)].Checked);
    }

    [Fact]
    public void A_saved_copy_of_another_schema_or_that_cannot_be_read_is_not_used()
    {
        Assert.Null(GameFitCache.FromJson("{\"schema\":2,\"interop\":\"s\",\"dlls\":{}}"));
        Assert.Null(GameFitCache.FromJson("{\"schema\":1,"));
    }

    [Fact]
    public void Only_the_installed_dlls_are_kept_and_checks_of_another_game_build_are_dropped()
    {
        var a = new string('a', 64);
        var b = new string('b', 64);
        var saved = new GameFitCache("old", new Dictionary<string, GameFit> { [a] = Outdated, [b] = Outdated });

        Assert.Equal(new[] { a }, saved.With("old", new Dictionary<string, GameFit>(), new[] { a }).ByDll.Keys);
        Assert.Empty(saved.With("new", new Dictionary<string, GameFit>(), new[] { a, b }).ByDll);
        Assert.Equal(new[] { a, b }, saved.ToCheck("new", new[] { a, b }));
        Assert.Equal(new[] { b }, new GameFitCache("old", new Dictionary<string, GameFit> { [a] = Outdated }).ToCheck("old", new[] { a, b }));
    }

    [Fact]
    public void The_stamp_changes_with_any_interop_file_and_not_with_the_order_they_are_listed_in()
    {
        var files = new[] { ("Assembly-CSharp.dll", 100L, 5L), ("UnityEngine.dll", 200L, 6L) };

        Assert.Equal(GameFitCache.StampOf(files), GameFitCache.StampOf(files.AsEnumerable().Reverse()));
        Assert.NotEqual(GameFitCache.StampOf(files), GameFitCache.StampOf(new[] { ("Assembly-CSharp.dll", 101L, 5L), ("UnityEngine.dll", 200L, 6L) }));
        Assert.NotEqual(GameFitCache.StampOf(files), GameFitCache.StampOf(new[] { ("Assembly-CSharp.dll", 100L, 7L), ("UnityEngine.dll", 200L, 6L) }));
    }

    [Fact]
    public void Only_the_first_names_are_kept_and_the_count_is_of_all_of_them()
    {
        var fit = GameFit.Of(200, Enumerable.Range(0, 80).Select(i => "Game.Type" + i.ToString("D2")));

        Assert.Equal(80, fit.MissingCount);
        Assert.Equal(GameFit.MaxNames, fit.Missing.Count);
        Assert.Equal("Game.Type00", fit.Missing[0]);
    }

    // ===== On the page =====

    private static ModEntry Mod(string guid) =>
        new(guid, guid, "1.0.0", guid + ".dll", new ModState.Loaded(Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());

    [Fact]
    public void The_page_is_sent_the_check()
    {
        using var json = JsonDocument.Parse(PageScript.ModJson(Mod("g") with { Fit = Outdated }));
        var fit = json.RootElement.GetProperty("fit");

        Assert.Equal(10, fit.GetProperty("checked").GetInt32());
        Assert.Equal(2, fit.GetProperty("missingCount").GetInt32());
        Assert.Equal("Game.Window", fit.GetProperty("missing")[1].GetString());
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(PageScript.ModJson(Mod("g"))).RootElement.GetProperty("fit").ValueKind);
    }

    [Fact]
    public void A_mod_missing_game_code_is_marked_and_says_what_is_missing()
    {
        var drawn = KnownFactsTests.Drawn(KnownFactsTests.Open(Mod("g") with { Fit = Outdated }));

        Assert.Contains(">OUTDATED</span>", drawn);
        Assert.Contains("<b>May not work with this game version.</b> This game version is missing 2 of the 10 parts of the game code the mod uses.", drawn);
        Assert.Contains("Game.Door::Shut<br>Game.Window", drawn);
    }

    [Fact]
    public void A_long_list_of_missing_names_is_cut_with_how_many_more()
    {
        var fit = GameFit.Of(40, Enumerable.Range(0, 12).Select(i => "Game.Type" + i.ToString("D2")));
        var drawn = KnownFactsTests.Drawn(KnownFactsTests.Open(Mod("g") with { Fit = fit }));

        Assert.Contains("Game.Type07<br>and 4 more", drawn);
        Assert.DoesNotContain("Game.Type08", drawn);
    }

    [Fact]
    public void A_mod_that_has_everything_it_uses_shows_no_warning()
    {
        var drawn = KnownFactsTests.Drawn(KnownFactsTests.Open(Mod("g") with { Fit = new GameFit(10, 0, Array.Empty<string>()) }));

        Assert.DoesNotContain(">OUTDATED</span>", drawn);
        Assert.DoesNotContain("May not work with this game version.", drawn);
    }

    // ===== Helpers =====

    private GameFit Check(string path)
    {
        using var game = new GameReferences(_interop, _core);

        return Assert.IsType<GameFit>(game.Check(path));
    }

    private GameFitChecks Checks() =>
        new(_interop, _core, new SavedFile(Path.Combine(_root, "mod-manager", "game-code-checks.json"), "checks", _log.Add), _log.Add);

    private static void WaitFinished(GameFitChecks checks)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (!checks.TakeFinished())
        {
            Assert.True(DateTime.UtcNow < deadline, "the check did not finish within 20 seconds");
            Thread.Sleep(20);
        }
    }

    private static string Sha(string path)
    {
        using var file = File.OpenRead(path);

        return Sha256Text.Of(file);
    }

    /// <summary>
    /// The game's code: Door with Open and the property Locked, and Gate, a Door. A later build adds Door.Shut.
    /// Door has no base type, so the check never needs the runtime's own assemblies.
    /// </summary>
    private static void WriteGame(string path, bool withShut = false)
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Assembly-CSharp", new Version(0, 0, 0, 0)), "Assembly-CSharp", ModuleKind.Dll);
        var module = assembly.MainModule;
        var door = new TypeDefinition("Game", "Door", TypeAttributes.Public | TypeAttributes.Class);
        var getter = Method(module, "get_Locked", module.TypeSystem.Boolean);

        door.Methods.Add(Method(module, "Open", module.TypeSystem.Void));

        if (withShut) door.Methods.Add(Method(module, "Shut", module.TypeSystem.Void));

        door.Methods.Add(getter);
        door.Properties.Add(new PropertyDefinition("Locked", PropertyAttributes.None, module.TypeSystem.Boolean) { GetMethod = getter });
        module.Types.Add(door);
        module.Types.Add(new TypeDefinition("Game", "Gate", TypeAttributes.Public | TypeAttributes.Class, door));

        // Written to bytes first, so a rebuild in the same second still lands as a new file with a new time.
        using var stream = new MemoryStream();

        assembly.Write(stream);
        File.WriteAllBytes(path, stream.ToArray());
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(withShut ? 1 : 0));
    }

    private static MethodDefinition Method(ModuleDefinition module, string name, TypeReference returns)
    {
        var method = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static, returns);
        var il = method.Body.GetILProcessor();

        if (returns.Name == "Boolean") il.Emit(OpCodes.Ldc_I4_0);

        il.Emit(OpCodes.Ret);

        return method;
    }

    /// <summary>
    /// A mod that calls Door.Open (there), Door.Shut (gone) and Window.Close (its type is gone), and Other.Thing.Do
    /// from a library that is not the game's; and patches Gate.Open (on Gate's base type), Door.Slam (gone, named on
    /// the patch method under a class that names Door) and the property Door.Locked.
    /// </summary>
    private string WriteMod(string fileName)
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("DoorMod", new Version(1, 0)), "DoorMod", ModuleKind.Dll);
        var module = assembly.MainModule;
        var game = new AssemblyNameReference("Assembly-CSharp", new Version(0, 0, 0, 0));
        var harmony = new AssemblyNameReference("0Harmony", new Version(2, 10, 0, 0));
        var other = new AssemblyNameReference("OtherLib", new Version(1, 0, 0, 0));

        module.AssemblyReferences.Add(game);
        module.AssemblyReferences.Add(harmony);
        module.AssemblyReferences.Add(other);

        TypeReference Game(string name) => new("Game", name, module, game);
        MethodReference Call(TypeReference type, string name) => new(name, module.TypeSystem.Void, type);

        var run = new MethodDefinition("Run", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        var il = run.Body.GetILProcessor();

        il.Emit(OpCodes.Call, Call(Game("Door"), "Open"));
        il.Emit(OpCodes.Call, Call(Game("Door"), "Shut"));
        il.Emit(OpCodes.Call, Call(Game("Window"), "Close"));
        il.Emit(OpCodes.Call, Call(new TypeReference("Other", "Thing", module, other), "Do"));
        il.Emit(OpCodes.Ret);

        var main = new TypeDefinition("DoorMod", "Main", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);

        main.Methods.Add(run);
        module.Types.Add(main);
        module.Types.Add(PatchClass(module, "PatchOpen", Patch(module, harmony, Game("Gate"), "Open"), null));
        module.Types.Add(PatchClass(module, "PatchSlam", Patch(module, harmony, Game("Door"), null), Patch(module, harmony, null, "Slam")));
        module.Types.Add(PatchClass(module, "PatchLocked", Patch(module, harmony, Game("Door"), "Locked"), null));

        var path = Path.Combine(_root, fileName);

        assembly.Write(path);

        return path;
    }

    private string WriteEmpty(string fileName)
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Plain", new Version(1, 0)), "Plain", ModuleKind.Dll);

        assembly.MainModule.Types.Add(new TypeDefinition("Plain", "Thing", TypeAttributes.Public | TypeAttributes.Class, assembly.MainModule.TypeSystem.Object));

        var path = Path.Combine(_root, fileName);

        assembly.Write(path);

        return path;
    }

    private static TypeDefinition PatchClass(ModuleDefinition module, string name, CustomAttribute onClass, CustomAttribute? onPrefix)
    {
        var type = new TypeDefinition("DoorMod", name, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
        var prefix = Method(module, "Prefix", module.TypeSystem.Void);

        type.CustomAttributes.Add(onClass);

        if (onPrefix != null) prefix.CustomAttributes.Add(onPrefix);

        type.Methods.Add(prefix);

        return type;
    }

    /// <summary>[HarmonyPatch(typeof(type), name)], [HarmonyPatch(typeof(type))] or [HarmonyPatch(name)].</summary>
    private static CustomAttribute Patch(ModuleDefinition module, AssemblyNameReference harmony, TypeReference? type, string? name)
    {
        var ctor = new MethodReference(".ctor", module.TypeSystem.Void, new TypeReference("HarmonyLib", "HarmonyPatch", module, harmony)) { HasThis = true };
        var attribute = new CustomAttribute(ctor);
        var systemType = new TypeReference("System", "Type", module, module.TypeSystem.CoreLibrary);

        if (type != null)
        {
            ctor.Parameters.Add(new ParameterDefinition(systemType));
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(systemType, type));
        }

        if (name != null)
        {
            ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, name));
        }

        return attribute;
    }
}
