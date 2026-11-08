using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// Nexus Mods is asked, without an account, which of its mods an installed DLL is and whether a newer version is up
/// there, so a mod from Nexus can say so. The answers are read defensively, saved for a day, and asked again only
/// where something changed.
/// </summary>
public class NexusTests
{
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);

    private static string Uri(int n) => "aa/bb/cc/" + n.ToString("D8") + "-0000-0000-0000-000000000000";

    private static NexusMod Mod(int id, string version, string updated = "2026-11-01", string uploader = "Bob") => new(id, "Mod " + id, version, updated, uploader);

    private static NexusFile File(int id, string version, string category) => new(id, version, category, Uri(id));

    private static NexusCache Cache(IReadOnlyList<NexusMod> mods, Dictionary<int, NexusFiles> files, Dictionary<string, IReadOnlyList<string>> manifests) =>
        new(DateTimeOffset.Parse("2026-11-08T10:00:00Z"), mods, files, manifests);

    // ===== Reading Nexus's answers =====

    [Fact]
    public void A_page_of_the_list_gives_each_mod_and_the_total()
    {
        var (mods, total, problem) = NexusApi.ReadMods("""
            {"data":{"mods":{"totalCount":161,"nodes":[
              {"modId":40,"name":"Radar","version":"1.2","updatedAt":"2026-11-01T00:00:00Z","uploader":{"name":"Bob"}},
              {"modId":"41","name":"Lamp","version":"0.3"},
              {"name":"No id"}
            ]}}}
            """);

        Assert.Null(problem);
        Assert.Equal(161, total);
        Assert.Equal(new[] { 40, 41 }, mods!.Select(mod => mod.ModId));
        Assert.Equal(new NexusMod(40, "Radar", "1.2", "2026-11-01T00:00:00Z", "Bob"), mods[0]);
        Assert.Equal("", mods[1].Uploader);
    }

    [Theory]
    [InlineData("""{"errors":[{"message":"Rate limited"}]}""", "Rate limited")]
    [InlineData("""{"data":{}}""", "no list of mods")]
    [InlineData("""not json""", "could not be read")]
    public void A_list_Nexus_could_not_give_is_a_problem(string body, string problem)
    {
        var (mods, _, said) = NexusApi.ReadMods(body);

        Assert.Null(mods);
        Assert.Contains(problem, said);
    }

    [Fact]
    public void Several_mods_files_come_back_each_under_its_own_name_and_a_mod_sent_nothing_for_is_left_out()
    {
        var body = """
            {"data":{
              "m40":[{"fileId":7,"version":"1.2","category":"MAIN","uri":"URI7"},{"fileId":8,"version":"1.3","category":"MAIN","uri":"../../etc"}],
              "m41":[]
            }}
            """.Replace("URI7", Uri(7));

        var (files, problem) = NexusApi.ReadFiles(body, new[] { 40, 41, 42 });

        Assert.Null(problem);
        Assert.Equal(new[] { 40, 41 }, files!.Keys.OrderBy(id => id));
        Assert.Equal(new[] { File(7, "1.2", "MAIN") }, files[40]);
        Assert.Empty(files[41]);
    }

    [Fact]
    public void A_manifest_gives_the_hash_of_each_dll_in_the_file_and_nothing_else()
    {
        var body = """
            {"files":[
              {"file_path":"BepInEx/plugins/Radar/Radar.DLL","file_hashes":{"SHA256":"UPPER"}},
              {"file_path":"BepInEx/plugins/Radar/readme.txt","file_hashes":{"SHA256":"B"}},
              {"file_path":"BepInEx/plugins/Radar/Bad.dll","file_hashes":{"SHA256":"not a hash"}},
              {"file_path":"BepInEx/plugins/Radar/Copy.dll","file_hashes":{"SHA256":"UPPER"}}
            ]}
            """.Replace("UPPER", HashA.ToUpperInvariant()).Replace("\"B\"", "\"" + HashB + "\"");

        var (hashes, problem) = NexusApi.ReadManifest(body);

        Assert.Null(problem);
        Assert.Equal(new[] { HashA }, hashes);
    }

    private static string QueryOf(string request)
    {
        using var json = System.Text.Json.JsonDocument.Parse(request);

        return json.RootElement.GetProperty("query").GetString()!;
    }

    [Fact]
    public void Every_request_asks_only_for_this_game()
    {
        var list = QueryOf(NexusApi.ModsRequest(80));
        var files = QueryOf(NexusApi.FilesRequest(new[] { 40, 41 }));

        Assert.Contains("gameDomainName: { value: \"survivallog\", op: EQUALS }", list);
        Assert.Contains("offset: 80", list);
        Assert.Contains("m40: modFiles(modId: 40, gameId: 9894)", files);
        Assert.Contains("m41: modFiles(modId: 41, gameId: 9894)", files);
    }

    // ===== The saved copy =====

    [Fact]
    public void The_saved_copy_reads_back_as_it_was_written()
    {
        var cache = Cache(
            new[] { Mod(40, "1.2", uploader: "Bob") },
            new Dictionary<int, NexusFiles> { [40] = new("2026-11-01", new[] { File(7, "1.2", "MAIN") }) },
            new Dictionary<string, IReadOnlyList<string>> { [Uri(7)] = new[] { HashA } });

        var read = NexusCache.FromJson(cache.ToJson());

        Assert.NotNull(read);
        Assert.Equal(cache.CheckedAt, read!.CheckedAt);
        Assert.Equal(cache.Mods, read.Mods);
        Assert.Equal(cache.Files[40].Files, read.Files[40].Files);
        Assert.Equal("2026-11-01", read.Files[40].UpdatedAt);
        Assert.Equal(new[] { HashA }, read.Manifests[Uri(7)]);
    }

    [Theory]
    [InlineData("""{"schema":2,"mods":[]}""")]
    [InlineData("""{"mods":[]}""")]
    [InlineData("""[1,2]""")]
    [InlineData("""{"schema":1,""")]
    public void A_saved_copy_of_another_kind_is_not_used(string text)
    {
        Assert.Null(NexusCache.FromJson(text));
    }

    [Fact]
    public void A_saved_manifest_under_a_name_that_is_not_a_manifest_or_a_hash_that_is_not_one_is_dropped()
    {
        var text = """{"schema":1,"checked":"","mods":[],"files":{},"manifests":{"../x":["HASH"],"GOOD":["HASH","nope"]}}""".Replace("HASH", HashA).Replace("GOOD", Uri(7));

        var read = NexusCache.FromJson(text)!;

        Assert.Null(read.CheckedAt);
        Assert.Equal(new[] { Uri(7) }, read.Manifests.Keys);
        Assert.Equal(new[] { HashA }, read.Manifests[Uri(7)]);
    }

    [Fact]
    public void Nexus_is_asked_once_a_day_at_most_and_a_clock_that_went_back_does_not_stop_it()
    {
        var now = DateTimeOffset.Parse("2026-11-08T10:00:00Z");

        Assert.True(NexusApi.IsDue(null, now));
        Assert.False(NexusApi.IsDue(now.AddHours(-23), now));
        Assert.True(NexusApi.IsDue(now.AddHours(-24), now));
        Assert.True(NexusApi.IsDue(now.AddHours(2), now));
    }

    // ===== What is asked for =====

    [Fact]
    public void Nothing_is_asked_about_when_every_installed_mod_is_on_the_wiki()
    {
        Assert.Empty(NexusPlan.Candidates(new[] { Mod(40, "1") }, new HashSet<int>(), Array.Empty<int?>()));
    }

    [Fact]
    public void When_the_wiki_lists_a_page_for_every_other_mod_only_those_pages_are_asked_about()
    {
        var mods = new[] { Mod(40, "1"), Mod(41, "1"), Mod(42, "1") };

        Assert.Equal(new[] { 41 }, NexusPlan.Candidates(mods, new HashSet<int>(), new int?[] { 41 }));
    }

    [Fact]
    public void A_mod_the_wiki_lists_no_page_for_means_every_mod_that_is_not_the_wikis_is_asked_about()
    {
        var mods = new[] { Mod(40, "1"), Mod(41, "1"), Mod(42, "1", uploader: "anexileddev"), Mod(43, "1") };

        Assert.Equal(new[] { 41, 43 }, NexusPlan.Candidates(mods, new HashSet<int> { 40 }, new int?[] { 41, null }));
    }

    [Fact]
    public void Files_are_read_again_only_for_mods_that_changed_since_or_were_never_read()
    {
        var mods = new[] { Mod(40, "1", "2026-11-02"), Mod(41, "1", "2026-11-01"), Mod(42, "1") };
        var saved = Cache(mods, new Dictionary<int, NexusFiles>
        {
            [40] = new("2026-11-01", Array.Empty<NexusFile>()),
            [41] = new("2026-11-01", Array.Empty<NexusFile>()),
        }, new Dictionary<string, IReadOnlyList<string>>());

        Assert.Equal(new[] { 40, 42 }, NexusPlan.NeedFiles(new[] { 40, 41, 42 }, mods, saved));
    }

    [Fact]
    public void Manifests_not_read_yet_are_asked_for_current_files_first_and_newest_first_up_to_the_days_limit()
    {
        var files = new Dictionary<int, NexusFiles>
        {
            [40] = new("", new[] { File(1, "1.0", "OLD_VERSION"), File(2, "1.1", "MAIN"), File(3, "1.2", "MAIN"), File(4, "x", "ARCHIVED"), File(5, "o", "OPTIONAL") }),
            [41] = new("", new[] { File(6, "2.0", "MAIN") }),
        };
        var read = new Dictionary<string, IReadOnlyList<string>> { [Uri(6)] = Array.Empty<string>() };

        Assert.Equal(new[] { Uri(3), Uri(2), Uri(5) }, NexusPlan.NeedManifests(new[] { 40, 41 }, files, read, 3));
        Assert.Equal(Uri(4), NexusPlan.NeedManifests(new[] { 40, 41 }, files, read, 10).Last());
    }

    [Fact]
    public void The_days_reads_go_first_to_the_mods_named_like_an_installed_mod_not_matched_yet()
    {
        // With one read left, the reads must not go to an unrelated mod that happens to have the newest upload.
        var mods = new[] { new NexusMod(40, "Fast Travel", "1", "", "Ann"), new NexusMod(41, "Better Radar", "1", "", "Bob"), new NexusMod(42, "Quick Stack", "1", "", "Cy") };
        var files = new Dictionary<int, NexusFiles>
        {
            [40] = new("", new[] { File(10, "1", "MAIN") }),
            [41] = new("", new[] { File(1, "1", "MAIN"), File(2, "0.9", "OLD_VERSION") }),
            [42] = new("", new[] { File(11, "1", "MAIN") }),
        };
        var read = new Dictionary<string, IReadOnlyList<string>>();
        var wanted = new[] { "BetterRadar", "BetterRadar", "com.bob.betterradar" };

        Assert.Equal(new[] { Uri(1) }, NexusPlan.NeedManifests(new[] { 40, 41, 42 }, files, read, 1, mods, wanted));
        // A likely mod's old upload still waits for every current one, since a player most often has a current file.
        Assert.Equal(new[] { Uri(1), Uri(11), Uri(10), Uri(2) }, NexusPlan.NeedManifests(new[] { 40, 41, 42 }, files, read, 10, mods, wanted));
        // With nothing wanted, as when every installed mod is matched, the order is newest first again.
        Assert.Equal(new[] { Uri(11) }, NexusPlan.NeedManifests(new[] { 40, 41, 42 }, files, read, 1, mods, Array.Empty<string>()));
    }

    [Theory]
    [InlineData("BetterRadarMod", "better radar")]
    [InlineData("com.bob.better-radar", "bob better radar")]
    [InlineData("SL_HTTPServer2", "http server 2")]
    [InlineData("The Survival Log Mod", "")]
    public void A_name_is_known_by_its_words_without_the_filler_every_mod_name_has(string name, string words) =>
        Assert.Equal(words, string.Join(" ", NameWords.Of(name)));

    [Theory]
    [InlineData("Auto Loot Plus", "AutoLootPlus", 3)]
    [InlineData("Autoloot", "Auto Loot", 1)]
    [InlineData("Smooth Maps", "Map", 0)]
    [InlineData("Fast Travel", "Better Radar", 0)]
    public void Names_are_alike_by_the_words_they_share_or_one_held_in_the_other(string nexusName, string installed, int likeness) =>
        Assert.Equal(likeness, NameWords.Likeness(NameWords.Of(nexusName), new[] { NameWords.Of(installed) }));

    [Fact]
    public void A_mod_no_longer_on_Nexus_loses_its_files_and_their_manifests()
    {
        var saved = Cache(
            new[] { Mod(40, "1"), Mod(41, "1") },
            new Dictionary<int, NexusFiles>
            {
                [40] = new("2026-11-01", new[] { File(1, "1", "MAIN") }),
                [41] = new("2026-11-01", new[] { File(2, "1", "MAIN") }),
            },
            new Dictionary<string, IReadOnlyList<string>> { [Uri(1)] = new[] { HashA }, [Uri(2)] = new[] { HashB } });
        var now = DateTimeOffset.Parse("2026-11-09T10:00:00Z");
        var fresh = new Dictionary<int, NexusFiles> { [40] = new("2026-11-09", new[] { File(1, "1", "MAIN"), File(3, "2", "MAIN") }) };

        var merged = NexusPlan.Merge(now, new[] { Mod(40, "2", "2026-11-09") }, saved, fresh, new Dictionary<string, IReadOnlyList<string>> { [Uri(3)] = new[] { HashB } });

        Assert.Equal(now, merged.CheckedAt);
        Assert.Equal(new[] { 40 }, merged.Files.Keys);
        Assert.Equal(new[] { Uri(1), Uri(3) }, merged.Manifests.Keys.OrderBy(uri => uri));
        Assert.Equal(new[] { HashA }, merged.Manifests[Uri(1)]);
    }

    // ===== Which mod a DLL is, and whether Nexus has a newer one =====

    private static NexusCache Radar(params NexusFile[] files) => Cache(
        new[] { Mod(40, "1.3") },
        new Dictionary<int, NexusFiles> { [40] = new("", files) },
        files.ToDictionary(file => file.Uri, file => (IReadOnlyList<string>)new[] { file.FileId == 1 ? HashA : HashB }));

    [Fact]
    public void A_dll_in_the_newest_main_file_is_up_to_date_even_when_the_file_carries_another_version()
    {
        var found = NexusMatch.Find(Radar(File(1, "1.0", "MAIN")), null, "1.0", HashA);

        Assert.Equal(new NexusNewest(40, "Mod 40", "1.3", false), found);
    }

    [Fact]
    public void A_dll_in_an_old_file_shows_the_newer_version_whatever_the_plugin_says()
    {
        var found = NexusMatch.Find(Radar(File(1, "1.2", "OLD_VERSION"), File(2, "1.3", "MAIN")), null, "9.9", HashA);

        Assert.Equal(new NexusNewest(40, "Mod 40", "1.3", true), found);
    }

    [Fact]
    public void A_dll_found_in_two_mods_names_neither_unless_the_wiki_lists_one_of_them()
    {
        var cache = Cache(
            new[] { Mod(40, "1.3"), Mod(41, "2.0") },
            new Dictionary<int, NexusFiles>
            {
                [40] = new("", new[] { File(1, "1.3", "MAIN") }),
                [41] = new("", new[] { File(2, "2.0", "MAIN") }),
            },
            new Dictionary<string, IReadOnlyList<string>> { [Uri(1)] = new[] { HashA }, [Uri(2)] = new[] { HashA } });

        Assert.Null(NexusMatch.Find(cache, null, "1.0", HashA));
        Assert.Equal(41, NexusMatch.Find(cache, 41, "1.0", HashA)?.ModId);
    }

    [Theory]
    [InlineData("1.2.0", true)]
    [InlineData("1.3", false)]
    [InlineData("1.4", false)]
    public void Without_a_hash_the_page_the_wiki_lists_is_compared_by_version(string installed, bool isNewer)
    {
        var found = NexusMatch.Find(Radar(File(2, "1.3", "MAIN")), 40, installed, HashA.Replace('a', 'f'));

        Assert.Equal(new NexusNewest(40, "Mod 40", "1.3", isNewer), found);
    }

    [Fact]
    public void A_version_that_cannot_be_read_is_never_called_older()
    {
        Assert.False(NexusMatch.Find(Radar(), 40, "beta", null)!.IsNewer);
    }

    [Fact]
    public void A_mod_Nexus_has_no_match_for_shows_nothing()
    {
        Assert.Null(NexusMatch.Find(Radar(File(1, "1.3", "MAIN")), null, "1.0", HashB.Replace('b', 'e')));
        Assert.Null(NexusMatch.Find(Radar(), 99, "1.0", null));
    }
}
