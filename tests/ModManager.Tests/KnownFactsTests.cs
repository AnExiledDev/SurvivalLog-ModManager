using System.Text.Json;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// What the wiki's index and Nexus Mods know about an installed mod reaches the player: a version known to be broken,
/// an update held back because it is known not to work, a mod it is known to clash with, and a newer version on Nexus
/// for a mod that is not on the wiki. The page shows each and offers no install of a held update.
/// </summary>
public class KnownFactsTests
{
    private static readonly string NewDll = new('d', 64);
    private static readonly string OldDll = new('e', 64);
    private static readonly string RadarDll = new('f', 64);

    private static readonly BilingualText Crashes = new("Crashes on load.", "加载时崩溃。");
    private static readonly BilingualText Redraw = new("Both redraw the map.", "两者都会重绘地图。");

    private static readonly ModIndex Index = new(
        "2026-11-08",
        new[]
        {
            new IndexMod("com.ann.map", "Map", "map", 12, "Ann/SurvivalLog-Map", new[]
            {
                new IndexRelease("1.2.0", new[] { "1.1.20719" }, "Map-1.2.0.zip", 100, new string('a', 64), NewDll),
                new IndexRelease("1.1.0", new[] { "1.1.20610" }, "Map-1.1.0.zip", 90, new string('a', 64), OldDll),
            }),
        },
        new[] { new IndexConflict("com.ann.map", "com.bob.radar", Redraw) },
        new[] { new IndexBroken("com.ann.map", new[] { "1.1.0" }, Crashes) },
        new Dictionary<string, int> { ["com.bob.radar"] = 40 });

    private const string Manifest = "aa/bb/cc/00000001-0000-0000-0000-000000000000";

    // Radar 1.3 is on Nexus; the installed DLL is in its old 1.2 file. Map's own DLL is listed there too, by a reupload.
    private static readonly NexusCache Nexus = new(
        DateTimeOffset.Parse("2026-11-08T10:00:00Z"),
        new[] { new NexusMod(40, "Radar", "1.3", "2026-11-01", "Bob") },
        new Dictionary<int, NexusFiles>
        {
            [40] = new("2026-11-01", new[] { new NexusFile(1, "1.2", "OLD_VERSION", Manifest), new NexusFile(2, "1.3", "MAIN", "aa/bb/cc/00000002-0000-0000-0000-000000000000") }),
        },
        new Dictionary<string, IReadOnlyList<string>> { [Manifest] = new[] { RadarDll, NewDll } });

    private static readonly HashSet<string> BothInstalled = new(StringComparer.Ordinal) { "com.ann.map", "com.bob.radar" };

    private static ModEntry Mod(string guid, string version, UpdateState? update = null) =>
        new(guid, guid, version, guid + ".dll", new ModState.Loaded(Array.Empty<Setting>()), ModInfo.None, new ModSwitch.Allowed(true, Array.Empty<string>()), update ?? new UpdateState.NotChecked());

    [Fact]
    public void A_dll_that_is_a_broken_release_is_called_broken_whatever_version_the_plugin_claims()
    {
        var known = KnownFactsOf.For(Mod("com.ann.map", "1.2.0"), Index, NexusCache.Empty, BothInstalled, OldDll);

        Assert.Equal(Crashes, known.Broken);
        Assert.Null(KnownFactsOf.For(Mod("com.ann.map", "1.2.0"), Index, NexusCache.Empty, BothInstalled, NewDll).Broken);
    }

    [Fact]
    public void An_update_to_a_broken_release_is_held_with_the_reason()
    {
        var offered = Mod("com.ann.map", "1.0.0", new UpdateState.Available("v1.1.0", new NotesState.NotAsked()));
        var good = Mod("com.ann.map", "1.0.0", new UpdateState.Available("v1.2.0", new NotesState.NotAsked()));

        Assert.Equal(Crashes, KnownFactsOf.For(offered, Index, NexusCache.Empty, BothInstalled, null).UpdateHeld);
        Assert.Null(KnownFactsOf.For(good, Index, NexusCache.Empty, BothInstalled, null).UpdateHeld);
    }

    [Fact]
    public void A_known_conflict_shows_only_when_the_other_mod_is_installed()
    {
        var map = Mod("com.ann.map", "1.2.0");

        Assert.Equal(new[] { new KnownConflict("com.bob.radar", Redraw) }, KnownFactsOf.For(map, Index, NexusCache.Empty, BothInstalled, NewDll).Conflicts);
        Assert.Equal(new[] { new KnownConflict("com.ann.map", Redraw) }, KnownFactsOf.For(Mod("com.bob.radar", "1.2"), Index, NexusCache.Empty, BothInstalled, RadarDll).Conflicts);
        Assert.Empty(KnownFactsOf.For(map, Index, NexusCache.Empty, new HashSet<string> { "com.ann.map" }, NewDll).Conflicts);
    }

    [Fact]
    public void A_mod_from_Nexus_shows_its_newer_version_there_and_the_wikis_own_mods_never_do()
    {
        var radar = KnownFactsOf.For(Mod("com.bob.radar", "1.2"), Index, Nexus, BothInstalled, RadarDll);
        var map = KnownFactsOf.For(Mod("com.ann.map", "1.2.0"), Index, Nexus, BothInstalled, NewDll);

        Assert.Equal(new NexusNewest(40, "Radar", "1.3", true), radar.Nexus);
        Assert.Null(map.Nexus);
    }

    [Fact]
    public void The_page_is_sent_what_is_known()
    {
        var mod = Mod("com.ann.map", "1.1.0") with
        {
            Known = new KnownFacts { Broken = Crashes, Conflicts = new[] { new KnownConflict("com.bob.radar", Redraw) }, Nexus = new NexusNewest(40, "Radar", "1.3", true) },
        };

        using var json = JsonDocument.Parse(PageScript.ModJson(mod));
        var known = json.RootElement.GetProperty("known");

        Assert.Equal("Crashes on load.", known.GetProperty("broken").GetProperty("en").GetString());
        Assert.Equal("加载时崩溃。", known.GetProperty("broken").GetProperty("zh").GetString());
        Assert.Equal(JsonValueKind.Null, known.GetProperty("updateHeld").ValueKind);
        Assert.Equal("com.bob.radar", known.GetProperty("conflicts")[0].GetProperty("guid").GetString());
        Assert.Equal("Both redraw the map.", known.GetProperty("conflicts")[0].GetProperty("en").GetString());
        Assert.Equal(40, known.GetProperty("nexus").GetProperty("id").GetInt32());
        Assert.True(known.GetProperty("nexus").GetProperty("newer").GetBoolean());
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(PageScript.ModJson(Mod("g", "1"))).RootElement.GetProperty("known").GetProperty("nexus").ValueKind);
    }

    // ===== On the page =====

    [Fact]
    public void A_held_update_says_why_and_offers_no_install()
    {
        var mod = Mod("g", "1.0.0", new UpdateState.Available("v1.1.0", new NotesState.NotAsked())) with { Known = new KnownFacts { UpdateHeld = Crashes } };
        var engine = Open(mod);
        var drawn = Drawn(engine);

        Assert.Contains(">HELD</span>", drawn);
        Assert.Contains("Version 1.1.0 is known not to work, so the Mod Manager will not install it: Crashes on load.", drawn);
        Assert.DoesNotContain("data-act=\"install\"", drawn);
        Assert.Contains("data-act=\"update\"", drawn);

        // The Updates list says why there is nothing to install.
        engine.Execute("fire('click', { target: el('DIV', [], { filter: 'updates' }, { '[data-filter]': { dataset: { filter: 'updates' } } }) })");

        var updates = Drawn(engine);

        Assert.Contains("1 update is known not to work (marked HELD)", updates);
        Assert.DoesNotContain("data-act=\"installAll\"", updates);
    }

    [Fact]
    public void An_update_that_is_not_held_can_still_be_installed()
    {
        var drawn = Drawn(Open(Mod("g", "1.0.0", new UpdateState.Available("v1.2.0", new NotesState.NotAsked()))));

        Assert.Contains("data-act=\"install\"", drawn);
        Assert.DoesNotContain(">HELD</span>", drawn);
    }

    [Fact]
    public void A_broken_version_is_marked_and_says_why()
    {
        var drawn = Drawn(Open(Mod("g", "1.1.0") with { Known = new KnownFacts { Broken = Crashes } }));

        Assert.Contains(">BROKEN</span>", drawn);
        Assert.Contains("<b>This version is known not to work.</b> Crashes on load.", drawn);
    }

    [Fact]
    public void A_newer_version_on_Nexus_is_shown_with_a_button_to_its_page_that_names_the_mod()
    {
        var engine = Open(Mod("g", "1.2") with { Known = new KnownFacts { Nexus = new NexusNewest(40, "Radar", "1.3", true) } });
        var drawn = Drawn(engine);

        Assert.Contains(">NEXUS</span>", drawn);
        Assert.Contains("<b>Version 1.3 is on Nexus Mods.</b> You have 1.2.", drawn);
        Assert.Contains("data-act=\"nexus\"", drawn);

        engine.Execute("fire('click', { target: act('nexus') })");

        Assert.EndsWith("|nexus|g", engine.Evaluate("posted.filter(function (text) { return text.indexOf('|nexus|') >= 0; }).join('\\n')").AsString());
    }

    [Fact]
    public void A_mod_up_to_date_on_Nexus_says_so_and_offers_its_page()
    {
        var drawn = Drawn(Open(Mod("g", "1.3") with { Known = new KnownFacts { Nexus = new NexusNewest(40, "Radar", "1.3", false) } }));

        Assert.Contains("up to date on Nexus", drawn);
        Assert.Contains("data-act=\"nexus\"", drawn);
        Assert.DoesNotContain("is on Nexus Mods.", drawn);
    }

    [Fact]
    public void A_known_conflict_opens_a_Conflicts_tab_with_the_reason()
    {
        var map = Mod("com.ann.map", "1.2.0") with { Known = new KnownFacts { Conflicts = new[] { new KnownConflict("com.bob.radar", Redraw) } } };
        var engine = Open(map, Mod("com.bob.radar", "1.2"));

        Assert.Contains("data-side=\"conflicts\"", Drawn(engine));

        engine.Execute("fire('click', { target: el('DIV', ['tab'], { side: 'conflicts' }, { '[data-side]': { dataset: { side: 'conflicts' } } }) })");

        var drawn = Drawn(engine);

        Assert.Contains("Known not to work together", drawn);
        Assert.Contains("<div class=\"cf-why\">Both redraw the map.</div>", drawn);
    }

    internal static string Drawn(Engine engine) => engine.Evaluate("drawn.join('\\n')").AsString();

    internal static Engine Open(params ModEntry[] mods)
    {
        var engine = new Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(ItemFieldPageTests.StandIns);
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(new ModsView(mods, Array.Empty<string>(), false, "F10")));

        return engine;
    }

    private static string Resource(string name)
    {
        using var stream = typeof(KnownFactsTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
