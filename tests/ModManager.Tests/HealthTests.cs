using Jint;
using ModManager.Adapters;
using ModManager.Core;
using Mono.Cecil;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// The HEALTH tab: what is wrong with how BepInEx and the mods are installed, so a player whose mod does not start
/// can see why without opening the game folder. Each check is here because it is a way players really break an install.
/// </summary>
public class HealthTests
{
    private const string Toolkit = "com.dev.survivallog.exiledtoolkit";

    private static InstallScan Scan(
        string bepInEx = HealthCheck.TestedBepInEx,
        FoundDll[]? dlls = null,
        string[]? folders = null,
        string[]? settings = null,
        string[]? inUse = null,
        string[]? otherGuids = null) =>
        new(bepInEx, dlls ?? Array.Empty<FoundDll>(), folders ?? Array.Empty<string>(), settings ?? Array.Empty<string>(), inUse ?? Array.Empty<string>(), otherGuids ?? Array.Empty<string>());

    private static FoundDll Dll(string path, string? sha, params string[] guids) => new(path, sha, guids);

    private static IReadOnlyList<HealthFinding> Find(InstallScan scan, params ModEntry[] mods) => HealthCheck.Find(scan, mods, Toolkit);

    // ===== What is found =====

    [Fact]
    public void A_clean_install_has_nothing_to_show()
    {
        var found = Find(Scan(dlls: new[] { Dll("plugins/Map.dll", "a", "com.ann.map"), Dll("plugins/Radar/Radar.dll", "b", "com.ann.radar") }, folders: new[] { "plugins/Radar" }, settings: new[] { "BepInEx.cfg", "com.ann.map.cfg" }), Loaded("com.ann.map"));

        Assert.Empty(found);
    }

    [Fact]
    public void Another_BepInEx_build_is_named_without_its_build_hash_and_the_tested_one_with_a_hash_is_fine()
    {
        Assert.Equal(new HealthFinding.OtherBepInEx("6.0.0-be.735"), Assert.Single(Find(Scan(bepInEx: "6.0.0-be.735+5fef357"))));
        Assert.Empty(Find(Scan(bepInEx: HealthCheck.TestedBepInEx + "+0123abc")));
    }

    [Fact]
    public void A_BepInEx_version_that_could_not_be_read_is_not_called_wrong()
    {
        Assert.Empty(Find(Scan(bepInEx: "")));
    }

    [Fact]
    public void A_mod_that_needs_a_newer_Toolkit_names_the_range_and_the_installed_version()
    {
        var radar = Loaded("com.ann.radar") with { Relations = new[] { NeedsToolkit(RelationStatus.WrongVersion, ">=0.9.0") } };

        var found = Find(Scan(), ToolkitMod("0.8.1"), radar);

        Assert.Equal(new HealthFinding.OldToolkit("com.ann.radar", ">=0.9.0", "0.8.1"), Assert.Single(found));
    }

    [Fact]
    public void A_Toolkit_that_fits_or_a_mod_turned_off_is_not_a_Toolkit_problem()
    {
        var fits = Loaded("com.ann.radar") with { Relations = new[] { NeedsToolkit(RelationStatus.On, ">=0.8.0") } };
        var off = Off("com.ann.map", "Map.dll") with { Relations = new[] { NeedsToolkit(RelationStatus.WrongVersion, ">=0.9.0") } };

        Assert.Empty(Find(Scan(), ToolkitMod("0.8.1"), fits, off));
    }

    [Fact]
    public void Two_DLLs_with_one_GUID_are_named_with_both_files()
    {
        var found = Find(Scan(dlls: new[] { Dll("plugins/Radar/Radar.dll", "a", "com.ann.radar"), Dll("plugins/RadarOld.dll", "b", "com.ann.radar") }));

        var same = Assert.IsType<HealthFinding.SameGuid>(Assert.Single(found));

        Assert.Equal("com.ann.radar", same.Guid);
        Assert.Equal(new[] { "plugins/Radar/Radar.dll", "plugins/RadarOld.dll" }, same.Files);
    }

    [Fact]
    public void One_DLL_in_two_places_is_found_by_name_or_by_bytes_and_not_again_as_a_GUID_clash()
    {
        var found = Find(Scan(dlls: new[]
        {
            Dll("plugins/Map.dll", "a", "com.ann.map"),
            Dll("plugins/Mods/Map.dll", "b", "com.ann.map"),
            Dll("plugins/Map (1).dll", "a", "com.ann.map"),
            Dll("plugins/Lib/Shared.dll", "c"),
            Dll("plugins/Other/shared.DLL", "d"),
        }));

        Assert.Equal(2, found.Count);
        Assert.Equal(new[] { "plugins/Lib/Shared.dll", "plugins/Other/shared.DLL" }, Assert.IsType<HealthFinding.SameDll>(found[0]).Files);
        Assert.Equal(new[] { "plugins/Map (1).dll", "plugins/Map.dll", "plugins/Mods/Map.dll" }, Assert.IsType<HealthFinding.SameDll>(found[1]).Files);
    }

    [Fact]
    public void A_GUID_clash_reaching_past_one_copied_DLL_is_still_shown()
    {
        var found = Find(Scan(dlls: new[] { Dll("plugins/Map.dll", "a", "com.ann.map"), Dll("plugins/Old/Map.dll", "a", "com.ann.map"), Dll("plugins/MapFork.dll", "z", "com.ann.map") }));

        Assert.Equal(new[] { "plugins/Map.dll", "plugins/MapFork.dll", "plugins/Old/Map.dll" }, Assert.IsType<HealthFinding.SameGuid>(found[0]).Files);
        Assert.IsType<HealthFinding.SameDll>(found[1]);
    }

    [Fact]
    public void A_mod_unpacked_one_folder_too_deep_is_named_once_at_its_outermost_folder()
    {
        var found = Find(Scan(folders: new[] { "plugins/Radar", "plugins/Radar/BepInEx", "plugins/Radar/BepInEx/plugins", "plugins/Map/Plugins", "plugins/Pack/Configs" }));

        Assert.Equal(new HealthFinding[] { new HealthFinding.NestedFolder("plugins/Map/Plugins"), new HealthFinding.NestedFolder("plugins/Radar/BepInEx") }, found);
    }

    [Fact]
    public void A_settings_file_no_known_plugin_owns_is_shown_as_left_over()
    {
        var found = Find(Scan(
            dlls: new[] { Dll("plugins/Radar.dll", "a", "com.ann.radar") },
            settings: new[] { "BepInEx.cfg", "com.ann.map.cfg", "com.ann.map.keys.cfg", "com.ann.radar.cfg", "Shared.cfg", "com.bob.gone.cfg", "com.ann.patcher.cfg", "com.ann.trashed.cfg", "old.cfg" },
            inUse: new[] { "Shared.cfg" },
            otherGuids: new[] { "com.ann.patcher", "com.ann.trashed" }), Off("com.ann.map", "Map.dll"));

        Assert.Equal(new[] { "com.bob.gone.cfg", "old.cfg" }, Assert.IsType<HealthFinding.UnusedSettings>(Assert.Single(found)).Files);
    }

    [Fact]
    public void What_keeps_mods_from_starting_comes_first()
    {
        var radar = Loaded("com.ann.radar") with { Relations = new[] { NeedsToolkit(RelationStatus.WrongVersion, ">=0.9.0") } };

        var found = Find(Scan(bepInEx: "6.0.0-be.700", dlls: new[] { Dll("plugins/A.dll", "a", "g"), Dll("plugins/B.dll", "b", "g") }, folders: new[] { "plugins/BepInEx" }, settings: new[] { "gone.cfg" }), ToolkitMod("0.8.0"), radar);

        Assert.Equal(new[] { "OtherBepInEx", "OldToolkit", "SameGuid", "NestedFolder", "UnusedSettings" }, found.Select(f => f.GetType().Name));
    }

    // ===== Looking at a real folder =====

    [Fact]
    public void The_scanner_finds_the_DLLs_folders_settings_and_patchers_in_a_real_BepInEx_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "slmm-health-" + Guid.NewGuid().ToString("N"));
        var plugins = Path.Combine(root, "plugins");
        var patchers = Path.Combine(root, "patchers");
        var config = Path.Combine(root, "config");

        try
        {
            Directory.CreateDirectory(Path.Combine(plugins, "Radar", "BepInEx", "plugins"));
            Directory.CreateDirectory(Path.Combine(plugins, "Old"));
            Directory.CreateDirectory(patchers);
            Directory.CreateDirectory(config);

            var radar = PluginDll("Radar", "com.ann.radar");

            File.WriteAllBytes(Path.Combine(plugins, "Radar", "Radar.dll"), radar);
            File.WriteAllBytes(Path.Combine(plugins, "Old", "RadarCopy.dll"), radar);
            File.WriteAllText(Path.Combine(plugins, "NotADll.dll"), "plain text");
            File.WriteAllBytes(Path.Combine(patchers, "Shell.dll"), PatcherDll("com.ann.shell"));
            File.WriteAllText(Path.Combine(config, "BepInEx.cfg"), "");
            File.WriteAllText(Path.Combine(config, "com.bob.gone.cfg"), "");

            var logged = new List<string>();
            var scanner = new InstallScanner(root, plugins, patchers, config, logged.Add);

            var scan = scanner.Scan("6.0.0-be.788+abc", new[] { Path.Combine(config, "com.ann.radar.cfg") }, new[] { "com.ann.trashed" });

            Assert.Equal(new[] { "plugins/NotADll.dll", "plugins/Old/RadarCopy.dll", "plugins/Radar/Radar.dll" }, scan.Dlls.Select(dll => dll.Path).OrderBy(path => path, StringComparer.Ordinal));
            Assert.Equal(new[] { "com.ann.radar" }, scan.Dlls.Single(dll => dll.Path == "plugins/Radar/Radar.dll").Guids);
            Assert.Equal(scan.Dlls.Single(dll => dll.Path == "plugins/Radar/Radar.dll").Sha256, scan.Dlls.Single(dll => dll.Path == "plugins/Old/RadarCopy.dll").Sha256);
            Assert.Empty(scan.Dlls.Single(dll => dll.Path == "plugins/NotADll.dll").Guids);
            Assert.Contains("plugins/Radar/BepInEx/plugins", scan.Folders);
            Assert.Equal(new[] { "BepInEx.cfg", "com.bob.gone.cfg" }, scan.SettingsFiles.OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal(new[] { "com.ann.radar.cfg" }, scan.SettingsInUse);
            Assert.Equal(new[] { "com.ann.shell", "com.ann.trashed" }, scan.OtherGuids);

            var found = HealthCheck.Find(scan, Array.Empty<ModEntry>(), Toolkit);

            Assert.Contains(found, f => f is HealthFinding.SameDll same && same.Files.SequenceEqual(new[] { "plugins/Old/RadarCopy.dll", "plugins/Radar/Radar.dll" }));
            Assert.Contains(new HealthFinding.NestedFolder("plugins/Radar/BepInEx"), found);
            Assert.Equal(new[] { "com.bob.gone.cfg" }, found.OfType<HealthFinding.UnusedSettings>().Single().Files);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Missing_BepInEx_folders_are_no_error()
    {
        var root = Path.Combine(Path.GetTempPath(), "slmm-health-" + Guid.NewGuid().ToString("N"));
        var logged = new List<string>();

        var scan = new InstallScanner(root, Path.Combine(root, "plugins"), Path.Combine(root, "patchers"), Path.Combine(root, "config"), logged.Add).Scan("", Array.Empty<string>(), Array.Empty<string>());

        Assert.Empty(scan.Dlls);
        Assert.Empty(logged);
    }

    private static byte[] PluginDll(string name, string guid) => Dll(name, "BepInEx", "BepInPlugin", guid);

    private static byte[] PatcherDll(string guid) => Dll("Shell", "BepInEx.Preloader.Core.Patching", "PatcherPluginInfoAttribute", guid);

    private static byte[] Dll(string name, string attributeNamespace, string attributeName, string guid)
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, new Version(1, 0)), name, ModuleKind.Dll);
        var module = assembly.MainModule;
        var core = new AssemblyNameReference("BepInEx.Core", new Version(6, 0, 0, 0));
        var type = new TypeDefinition(name, "Plugin", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, module.TypeSystem.Object);
        var ctor = new MethodReference(".ctor", module.TypeSystem.Void, new TypeReference(attributeNamespace, attributeName, module, core)) { HasThis = true };

        for (var i = 0; i < 3; i++) ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));

        var attribute = new CustomAttribute(ctor);

        foreach (var text in new[] { guid, name, "1.0.0" }) attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, text));

        module.AssemblyReferences.Add(core);
        type.CustomAttributes.Add(attribute);
        module.Types.Add(type);

        using var stream = new MemoryStream();

        assembly.Write(stream);

        return stream.ToArray();
    }

    // ===== The page =====

    [Fact]
    public void With_nothing_wrong_the_health_page_falls_back_to_the_mod_list()
    {
        var engine = Page(View(new[] { Loaded("g") }));

        var page = OpenHealth(engine);

        Assert.DoesNotContain("Install check", page);
    }

    [Fact]
    public void The_HEALTH_tab_lists_each_finding()
    {
        var engine = Page(View(new[] { Loaded("g") },
            new HealthFinding.OldToolkit("Radar", ">=0.9.0", "0.8.1"),
            new HealthFinding.NestedFolder("plugins/Radar/BepInEx"),
            new HealthFinding.UnusedSettings(new[] { "com.bob.gone.cfg" })));

        var page = OpenHealth(engine);

        Assert.Contains("Install check &middot; 3", page);
        Assert.Contains("data-health=\"toolkit\"", page);
        Assert.Contains("0.8.1", page);
        Assert.Contains("<div>plugins/Radar/BepInEx</div>", page);
        Assert.Contains("<div>com.bob.gone.cfg</div>", page);
    }

    [Fact]
    public void Errors_no_mod_could_be_matched_to_are_on_the_health_page_counted_with_the_findings()
    {
        var crash = new Problem(ProblemLevel.Error, "Unity", "game crash", Array.Empty<string>(), Array.Empty<string>(), 2, 1);
        var hint = crash with { Level = ProblemLevel.Warning, Message = "texture missing" };
        var view = View(new[] { Loaded("g") }, new HealthFinding.NestedFolder("plugins/Radar/BepInEx")) with
        {
            OtherErrors = new[] { new ModProblem(hint, Array.Empty<ClashHint>()), new ModProblem(crash, Array.Empty<ClashHint>()) },
        };

        var page = OpenHealth(Page(view));

        Assert.Contains("Install check &middot; 1", page);
        Assert.Contains("BepInEx and game errors &middot; 2", page);
        Assert.Contains("1 problem", page);
        Assert.Contains("2 warnings", page);
        Assert.True(page.IndexOf("game crash", StringComparison.Ordinal) < page.IndexOf("texture missing", StringComparison.Ordinal), "an error comes before a warning");
        Assert.True(page.IndexOf("BepInEx and game errors", StringComparison.Ordinal) < page.IndexOf("Install check", StringComparison.Ordinal), "the part with the worst finding comes first");
    }

    [Fact]
    public void With_only_errors_the_health_page_still_opens_and_says_the_install_check_is_clean()
    {
        var crash = new Problem(ProblemLevel.Error, "Unity", "game crash", Array.Empty<string>(), Array.Empty<string>(), 1, 1);
        var view = View(new[] { Loaded("g") }) with { OtherErrors = new[] { new ModProblem(crash, Array.Empty<ClashHint>()) } };

        var page = OpenHealth(Page(view));

        Assert.Contains("game crash", page);
        Assert.Contains("Nothing wrong was found.", page);
    }

    private static ModEntry Loaded(string guid) =>
        new(guid, guid, "1.0", guid + ".dll", new ModState.Loaded(Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), new UpdateState.NotChecked());

    private static ModEntry Off(string guid, string file) =>
        new(guid, guid, "1.0", file, new ModState.Off(), ModInfo.None, new ModSwitch.Allowed(false, Array.Empty<string>()), new UpdateState.NotChecked());

    private static ModEntry ToolkitMod(string version) => Loaded(Toolkit) with { Version = version };

    private static Relation NeedsToolkit(RelationStatus status, string range) => new(RelationKind.Needs, Toolkit, "Exiled's Toolkit", status, false, range);

    private static ModsView View(IReadOnlyList<ModEntry> mods, params HealthFinding[] health) => new(mods, Array.Empty<string>(), false, "F10") { Health = health };

    private static Engine Page(ModsView view)
    {
        var engine = new Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(ItemFieldPageTests.StandIns);
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(view));

        return engine;
    }

    private static string OpenHealth(Engine engine) =>
        DrawnAfter(engine, "fire('click', { target: el('DIV', ['hd-title'], { page: 'health' }, { '[data-page]': { dataset: { page: 'health' } } }) })");

    private static string DrawnAfter(Engine engine, string script)
    {
        engine.Execute("drawn.length = 0");
        engine.Execute(script);

        return KnownFactsTests.Drawn(engine);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(HealthTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
