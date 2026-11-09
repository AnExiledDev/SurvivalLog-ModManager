using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// BROWSE lists every mod for the game on Nexus. A mod whose author set it up for the Mod Manager (the wiki keeps a
/// copy, or lists its GitHub releases) installs with one click; any other is called out as a manual download, with
/// a request the player can send its author.
/// </summary>
public sealed class BrowseTests
{
    private static readonly string Hash = new('a', 64);

    // Map is the wiki's, on Nexus as 12. Bags is listed by its author's GitHub, on Nexus as 40. Lamps is listed but
    // not on Nexus. Radar is only on Nexus. Pack is on the wiki with no release yet.
    private static readonly string IndexJson = """
        {
          "schema": 1,
          "generated": "2026-11-08T10:00:00Z",
          "mods": [
            { "guid": "com.ann.map", "name": "Map", "nexus": 12, "github": "Ann/SurvivalLog-Map",
              "releases": [ { "version": "1.2.0", "games": ["1.1.20719"], "file": "Map-1.2.0.zip", "size": 100, "sha256": "HASH", "dll": "Map.dll", "dll_sha256": "HASH" } ] },
            { "guid": "com.ann.pack", "name": "Pack", "github": "Ann/SurvivalLog-Pack", "releases": [] }
          ],
          "conflicts": [], "broken": [], "nexus_ids": {},
          "listed": [
            { "guid": "com.bob.bags", "name": "Bags", "nexus": 40, "github": "Bob/Bags", "dll": "Bags.dll" },
            { "guid": "com.bob.lamps", "name": "Lamps", "github": "Bob/Lamps", "dll": "Lamps.dll" }
          ]
        }
        """.Replace("HASH", Hash);

    private static ModIndexRead Read(string json)
    {
        var (read, problem) = ModIndexReader.Parse(json);

        Assert.Null(problem);

        return read!;
    }

    private static ModIndex Index => Read(IndexJson).Index;

    private static NexusMod OnNexus(int id, string name) => new(id, name, "1.0.0", "2026-11-0" + (id % 9 + 1), "someone") { Endorsements = id, Downloads = id * 10, Summary = name + " does things." };

    private static readonly NexusCache Nexus = new(DateTimeOffset.UtcNow, new[] { OnNexus(12, "Map"), OnNexus(40, "Bags"), OnNexus(77, "Radar"), OnNexus(77, "Radar") }, new Dictionary<int, NexusFiles>(), new Dictionary<string, IReadOnlyList<string>>());

    private static IReadOnlyList<BrowseMod> List(IEnumerable<string>? installed = null, IEnumerable<int>? installedNexus = null, Func<string, InstallState>? installOf = null, Func<string, FreshInstall?>? freshOf = null) =>
        Browse.List(Nexus, Index, (installed ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal), (installedNexus ?? Array.Empty<int>()).ToHashSet(), installOf ?? (_ => new InstallState.None()), freshOf ?? (_ => null));

    // ===== The list =====

    [Fact]
    public void A_mod_set_up_for_the_mod_manager_installs_from_here_and_any_other_is_a_manual_download()
    {
        var list = List();

        Assert.Equal(BrowseSupport.Installable, list.Single(mod => mod.NexusId == 12).Support);
        Assert.Equal(BrowseSupport.Installable, list.Single(mod => mod.NexusId == 40).Support);

        var radar = list.Single(mod => mod.NexusId == 77);

        Assert.Equal(BrowseSupport.Manual, radar.Support);
        Assert.Null(radar.Guid);
        Assert.Equal("nexus:77", radar.Key);
    }

    [Fact]
    public void A_nexus_mod_is_known_by_its_guid_through_the_index()
    {
        var list = List();

        Assert.Equal("com.ann.map", list.Single(mod => mod.NexusId == 12).Guid);
        Assert.Equal("com.bob.bags", list.Single(mod => mod.NexusId == 40).Guid);
    }

    [Fact]
    public void Installable_mods_nexus_does_not_list_still_show_and_a_mod_with_no_release_does_not()
    {
        var list = List();

        Assert.Equal(BrowseSupport.Installable, list.Single(mod => mod.Guid == "com.bob.lamps").Support);
        Assert.DoesNotContain(list, mod => mod.Guid == "com.ann.pack");
    }

    [Fact]
    public void Each_mod_shows_once()
    {
        var list = List();

        Assert.Single(list, mod => mod.NexusId == 77);
        Assert.Equal(list.Count, list.Select(mod => mod.Key).Distinct().Count());
    }

    [Fact]
    public void An_installed_mod_shows_as_installed_by_its_guid_or_by_the_nexus_page_it_was_matched_to()
    {
        var list = List(installed: new[] { "com.ann.map" }, installedNexus: new[] { 77 });

        Assert.Equal(BrowseSupport.Installed, list.Single(mod => mod.NexusId == 12).Support);
        Assert.Equal(BrowseSupport.Installed, list.Single(mod => mod.NexusId == 77).Support);
        Assert.Equal(BrowseSupport.Installable, list.Single(mod => mod.NexusId == 40).Support);
    }

    // ===== Where an install comes from =====

    [Fact]
    public void The_wikis_mod_installs_from_its_hash_checked_copy_and_a_listed_one_from_its_github()
    {
        var map = Browse.TargetOf(Index, "com.ann.map");
        var bags = Browse.TargetOf(Index, "com.bob.bags");

        Assert.Equal("Map.dll", map?.Dll);
        Assert.Equal("Map-1.2.0.zip", map?.WikiCopy?.File);
        Assert.Null(map?.GitHub);

        Assert.Equal("Bags.dll", bags?.Dll);
        Assert.Null(bags?.WikiCopy);
        Assert.Equal("Bob/Bags", bags?.GitHub);

        Assert.Null(Browse.TargetOf(Index, "com.ann.pack"));
        Assert.Null(Browse.TargetOf(Index, "com.nobody.radar"));
    }

    [Fact]
    public void A_listed_mod_with_an_unsafe_dll_name_or_one_the_wiki_already_has_is_ignored()
    {
        var json = IndexJson.Replace("\"listed\": [", """
            "listed": [
                { "guid": "com.eve.bad", "name": "Bad", "github": "Eve/Bad", "dll": "../Bad.dll" },
                { "guid": "com.ann.map", "name": "Map again", "github": "Eve/Map", "dll": "Map.dll" },
            """);
        var read = Read(json);

        Assert.Null(read.Index.ListedOf("com.eve.bad"));
        Assert.Null(read.Index.ListedOf("com.ann.map"));
        Assert.NotNull(read.Index.ListedOf("com.bob.bags"));
        Assert.Equal(2, read.Ignored.Count(problem => problem.Contains("listed mod") || problem.Contains("second entry")));
    }

    [Fact]
    public void A_listed_mod_gives_its_nexus_page_to_lookups()
    {
        Assert.Equal(40, Index.NexusIdOf("com.bob.bags"));
    }

    // ===== Checks on the DLL =====

    private static DllScan Scan(string guid, params string[] types) => new DllScan.Read(new[] { guid }, new DllFacts(types, Array.Empty<string>(), Array.Empty<string>(), false));

    [Fact]
    public void A_dll_that_is_another_mod_or_cannot_be_read_is_never_installed()
    {
        Assert.IsType<UpdateVerdict.Rejected>(Browse.Judge("com.bob.bags", Scan("com.eve.other"), isWikiCopy: true));
        Assert.IsType<UpdateVerdict.Rejected>(Browse.Judge("com.bob.bags", new DllScan.Unreadable("bad header"), isWikiCopy: true));
    }

    [Fact]
    public void A_mod_from_its_authors_github_that_can_reach_the_internet_waits_for_the_player()
    {
        var verdict = Browse.Judge("com.bob.bags", Scan("com.bob.bags", "System.Net.WebClient"), isWikiCopy: false);

        Assert.Equal(new[] { Power.Network }, Assert.IsType<UpdateVerdict.NeedsApproval>(verdict).NewPowers);
        Assert.IsType<UpdateVerdict.Clear>(Browse.Judge("com.bob.bags", Scan("com.bob.bags"), isWikiCopy: false));
    }

    [Fact]
    public void The_wikis_own_copy_goes_in_at_once_as_its_hash_was_checked()
    {
        Assert.IsType<UpdateVerdict.Clear>(Browse.Judge("com.ann.map", Scan("com.ann.map", "System.Net.WebClient"), isWikiCopy: true));
    }

    // ===== What an installed mod needs =====

    [Fact]
    public void A_need_that_cannot_be_installed_from_here_shows_on_the_card_and_an_installable_one_does_not()
    {
        var fresh = new FreshInstall(new[] { "Bags.dll" }, new[] { "com.bob.lamps", "com.nobody.radar", "com.ann.toolkit" });
        var bags = List(installed: new[] { "com.ann.toolkit" }, installOf: guid => guid == "com.bob.bags" ? new InstallState.Installed("v1.0.0") : new InstallState.None(), freshOf: guid => guid == "com.bob.bags" ? fresh : null)
            .Single(mod => mod.Guid == "com.bob.bags");

        Assert.True(bags.IsFresh);
        Assert.Equal(new[] { new BrowseNeed("com.nobody.radar", null) }, bags.Needs);
    }

    [Fact]
    public void A_need_being_installed_is_not_missing()
    {
        var missing = Browse.MissingNeeds(new[] { "a", "b", "c", "a" }, new HashSet<string> { "a" }, Index, guid => guid == "b" ? new InstallState.Installing() : new InstallState.None());

        Assert.Equal(new[] { "c" }, missing);
    }

    // ===== Asking the author =====

    [Fact]
    public void The_request_names_the_mod_and_where_the_steps_are_in_both_languages()
    {
        var message = AskAuthor.Message("Radar");

        Assert.Contains("Mod Manager support to Radar", message);
        Assert.Contains("「Radar」", message);
        Assert.Equal(2, message.Split(AskAuthor.KitUrl).Length - 1);
    }

    // ===== Page commands =====

    [Fact]
    public void Browse_commands_carry_a_guid_or_a_nexus_page_number()
    {
        Assert.Equal(new PageCommand.BrowseInstall("com.bob.bags"), PageCommand.Parse(PageCommand.Prefix + "browseInstall|com.bob.bags"));
        Assert.Equal(new PageCommand.BrowseUndo("com.bob.bags"), PageCommand.Parse(PageCommand.Prefix + "browseUndo|com.bob.bags"));
        Assert.Equal(new PageCommand.AskAuthor(77), PageCommand.Parse(PageCommand.Prefix + "askAuthor|77"));
        Assert.Equal(new PageCommand.OpenNexusPage(77), PageCommand.Parse(PageCommand.Prefix + "nexusPage|77"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(PageCommand.Prefix + "askAuthor|-3"));
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(PageCommand.Prefix + "nexusPage|77/../x"));
    }

    // ===== The page =====

    private static ModsView View(BrowseFeed feed, params BrowseMod[] mods) => new(Array.Empty<ModEntry>(), Array.Empty<string>(), false, "F10") { Browse = mods, BrowseFeed = feed };

    private static BrowseMod Radar => new("nexus:77", "Radar", "Shows things.", "someone", "1.0.0", "2026-11-01", 5, 50, null, 77, null, BrowseSupport.Manual);

    private static BrowseMod Bags => new("com.bob.bags", "Bags", "Bigger bags.", "Bob", "1.0.0", "2026-11-01", 9, 90, null, 40, "com.bob.bags", BrowseSupport.Installable);

    private static Jint.Engine OnBrowse(ModsView view)
    {
        var engine = new Jint.Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(ItemFieldPageTests.StandIns);
        engine.Execute("function card(name, key) { var up = { '[data-bw]': { dataset: { bw: key } } }; up['[data-act]'] = el('DIV', [], { act: name }, up); return up['[data-act]']; }");
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Open(view));
        engine.Execute("drawn.length = 0; fire('click', { target: el('DIV', [], {}, { '[data-page]': { dataset: { page: 'browse' } } }) })");

        return engine;
    }

    private static string Posted(Jint.Engine engine, string verb) =>
        engine.Evaluate("posted.filter(function (text) { return text.indexOf('|" + verb + "|') >= 0; }).join('\\n')").AsString();

    [Fact]
    public void A_manual_download_is_called_out_and_ask_the_author_sends_its_nexus_page()
    {
        var engine = OnBrowse(View(BrowseFeed.Ready, Radar));
        var drawn = KnownFactsTests.Drawn(engine);

        Assert.Contains("No Mod Manager support: manual download.", drawn);
        Assert.Contains("ASK THE AUTHOR", drawn);

        engine.Execute("fire('click', { target: card('bwAsk', 'nexus:77') })");

        Assert.EndsWith("|askAuthor|77", Posted(engine, "askAuthor"));
    }

    [Fact]
    public void Install_sends_the_mods_guid()
    {
        var engine = OnBrowse(View(BrowseFeed.Ready, Bags));

        Assert.DoesNotContain("No Mod Manager support", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: card('bwInstall', 'com.bob.bags') })");

        Assert.EndsWith("|browseInstall|com.bob.bags", Posted(engine, "browseInstall"));
    }

    [Fact]
    public void A_mod_installed_from_here_this_session_waits_in_the_bar_with_undo()
    {
        var engine = OnBrowse(View(BrowseFeed.Ready, Bags with { Install = new InstallState.Installed("v1.0.0"), IsFresh = true }));

        Assert.Contains("Installed, starts from the next start", KnownFactsTests.Drawn(engine));

        engine.Execute("fire('click', { target: el('DIV', [], { act: 'undo', undo: '0' }, { '[data-act]': { dataset: { act: 'undo', undo: '0' } } }) })");

        Assert.EndsWith("|browseUndo|com.bob.bags", Posted(engine, "browseUndo"));
    }

    [Fact]
    public void With_update_checks_off_browse_says_how_to_turn_them_on()
    {
        var drawn = KnownFactsTests.Drawn(OnBrowse(View(BrowseFeed.Off)));

        Assert.Contains("「Check For Updates」", drawn);
    }

    [Fact]
    public void The_list_and_its_state_reach_the_page()
    {
        using var json = System.Text.Json.JsonDocument.Parse(PageScript.ViewJson(View(BrowseFeed.Asking, Radar, Bags)));
        var root = json.RootElement;

        Assert.Equal("asking", root.GetProperty("browseFeed").GetString());
        Assert.Equal("manual", root.GetProperty("browse")[0].GetProperty("support").GetString());
        Assert.Equal(77, root.GetProperty("browse")[0].GetProperty("nexus").GetInt32());
        Assert.Equal("com.bob.bags", root.GetProperty("browse")[1].GetProperty("guid").GetString());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(BrowseTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
