using System.Text;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ModIndexTests
{
    private static readonly string ZipHash = new('a', 64);
    private static readonly string DllHash = new('b', 64);
    private static readonly string OldDllHash = new('c', 64);

    // The shape the wiki's index.json is written in.
    private static readonly string Sample = """
        {
          "schema": 1,
          "generated": "2026-11-08T10:00:00Z",
          "mods": [
            {
              "guid": "com.ann.map",
              "name": "Map",
              "slug": "map",
              "nexus": 12,
              "github": "Ann/SurvivalLog-Map",
              "releases": [
                { "version": "1.2.0", "date": "2026-11-01", "games": ["1.1.20719"], "file": "Map-1.2.0.zip", "size": 100, "sha256": "ZIP", "dll": "Map.dll", "dll_sha256": "DLL" },
                { "version": "1.1.0", "date": "2026-10-01", "games": ["1.1.20610"], "file": "Map-1.1.0.zip", "size": 90, "sha256": "ZIP", "dll": "Map.dll", "dll_sha256": "OLD" }
              ]
            },
            { "guid": "com.ann.pack", "name": "Pack", "github": "Ann/SurvivalLog-Pack", "releases": [] }
          ],
          "starter": {},
          "conflicts": [
            { "a": "com.ann.map", "b": "com.bob.radar", "why": "Both redraw the map.", "zh": "两者都会重绘地图。" },
            { "a": "com.bob.radar", "b": "com.ann.map", "why": "Listed twice." }
          ],
          "broken": [ { "guid": "com.ann.map", "versions": ["1.1.0"], "why": "Crashes on load." } ],
          "nexus_ids": { "com.bob.radar": 40 }
        }
        """.Replace("\"ZIP\"", "\"" + ZipHash + "\"").Replace("\"DLL\"", "\"" + DllHash + "\"").Replace("\"OLD\"", "\"" + OldDllHash + "\"");

    private static ModIndex Index(string json = "")
    {
        var (read, problem) = ModIndexReader.Parse(json.Length > 0 ? json : Sample);

        Assert.Null(problem);

        return read!.Index;
    }

    [Fact]
    public void The_wikis_index_is_read_with_its_mods_releases_and_known_problems()
    {
        var index = Index();
        var map = index.ModOf("com.ann.map");

        Assert.Equal(2, index.Mods.Count);
        Assert.Equal("map", map?.Slug);
        Assert.Equal(12, map?.NexusId);
        Assert.Equal(new[] { "1.2.0", "1.1.0" }, map!.Releases.Select(release => release.Version));
        Assert.Equal(new[] { "1.1.20719" }, map.Releases[0].Games);
        Assert.Equal("两者都会重绘地图。", index.Conflicts[0].Why.Chinese);
        Assert.Equal("Crashes on load.", index.Broken[0].Why.Chinese);
    }

    [Theory]
    [InlineData("{\"schema\":2,\"mods\":[]}", "the index is schema 2, and this Mod Manager reads schema 1")]
    [InlineData("{\"mods\":[]}", "the index does not say its schema")]
    [InlineData("[]", "the index is not a JSON object")]
    public void An_index_of_another_schema_is_not_used(string json, string problem)
    {
        Assert.Equal((null, problem), ModIndexReader.Parse(json));
    }

    [Fact]
    public void A_broken_file_is_a_problem_not_a_crash()
    {
        Assert.StartsWith("the index could not be read", ModIndexReader.Parse("{\"schema\":1,").Problem);
    }

    [Theory]
    [InlineData("\"file\": \"../Map.zip\"", "names no usable file")]
    [InlineData("\"file\": \"sub/Map.zip\"", "names no usable file")]
    [InlineData("\"file\": \"Map.exe\"", "names no usable file")]
    [InlineData("\"sha256\": \"ABC\"", "has no usable SHA-256")]
    [InlineData("\"size\": 0", "has a size of 0")]
    [InlineData("\"version\": \"latest\"", "its version cannot be read")]
    public void A_release_that_cannot_be_trusted_is_passed_over_and_the_rest_kept(string field, string problem)
    {
        var good = "{ \"version\": \"1.0.0\", \"file\": \"Map-1.0.0.zip\", \"size\": 10, \"sha256\": \"" + ZipHash + "\" }";
        var name = field.Substring(1, field.IndexOf('"', 1) - 1);
        var bad = "{ " + string.Join(", ", new[] { "\"version\": \"0.9.0\"", "\"file\": \"Map-0.9.0.zip\"", "\"size\": 10", "\"sha256\": \"" + ZipHash + "\"" }.Where(part => !part.StartsWith("\"" + name + "\"")).Append(field)) + " }";
        var json = "{\"schema\":1,\"mods\":[{\"guid\":\"com.ann.map\",\"releases\":[" + bad + "," + good + "]}]}";

        var read = ModIndexReader.Parse(json).Read!;

        Assert.Equal(new[] { "1.0.0" }, read.Index.ModOf("com.ann.map")!.Releases.Select(release => release.Version));
        Assert.Contains(problem, Assert.Single(read.Ignored));
    }

    [Fact]
    public void A_bad_page_name_or_repository_is_dropped_but_the_mod_kept()
    {
        var read = ModIndexReader.Parse("{\"schema\":1,\"mods\":[{\"guid\":\"com.ann.map\",\"slug\":\"../x\",\"github\":\"Ann/Map/extra\",\"releases\":[]}]}").Read!;
        var map = read.Index.ModOf("com.ann.map");

        Assert.Null(map?.Slug);
        Assert.Null(map?.GitHub);
        Assert.Equal(2, read.Ignored.Count);
    }

    [Fact]
    public void A_second_entry_for_a_mod_is_passed_over()
    {
        var read = ModIndexReader.Parse("{\"schema\":1,\"mods\":[{\"guid\":\"com.ann.map\",\"name\":\"First\"},{\"guid\":\"com.ann.map\",\"name\":\"Second\"}]}").Read!;

        Assert.Equal("First", read.Index.ModOf("com.ann.map")?.Name);
        Assert.Equal("a second entry for com.ann.map", Assert.Single(read.Ignored));
    }

    [Fact]
    public void The_installed_dll_is_known_by_its_hash_before_its_version()
    {
        var index = Index();

        // A build that kept the old version number in its plugin attribute.
        Assert.Equal("1.2.0", index.Identify("com.ann.map", "1.1.0", DllHash)?.Version);
        Assert.Equal("1.1.0", index.Identify("com.ann.map", "1.1.0", null)?.Version);
        Assert.Equal("1.1.0", index.Identify("com.ann.map", "v1.1.0", new string('f', 64))?.Version);
        Assert.Null(index.Identify("com.ann.map", "0.5.0", null));
        Assert.Null(index.Identify("com.bob.radar", "1.0.0", DllHash));
    }

    [Fact]
    public void When_GitHub_cannot_be_reached_the_newest_release_comes_from_the_index_with_the_wikis_page()
    {
        var latest = Index().LatestOf("ann/survivallog-map");

        Assert.Equal(new LatestRelease.Found("Ann", "SurvivalLog-Map", "v1.2.0", "https://sldb.core-directive.com/en/mods/map/"), latest);
        Assert.Null(Index().LatestOf("Ann/SurvivalLog-Pack"));
        Assert.Null(Index().LatestOf("Bob/Radar"));
    }

    [Fact]
    public void Without_a_page_on_the_wiki_the_newest_release_links_to_GitHub()
    {
        var json = "{\"schema\":1,\"mods\":[{\"guid\":\"com.ann.map\",\"github\":\"Ann/Map\",\"releases\":[{\"version\":\"2.0.0\",\"file\":\"Map.zip\",\"size\":1,\"sha256\":\"" + ZipHash + "\"}]}]}";

        Assert.Equal("https://github.com/Ann/Map/releases/tag/v2.0.0", Index(json).LatestOf("Ann/Map")?.PageUrl);
    }

    [Fact]
    public void The_wikis_copy_is_only_for_the_same_mod_from_the_same_repository_at_the_same_version()
    {
        var index = Index();

        Assert.Equal("Map-1.2.0.zip", index.MirrorFor("com.ann.map", "ANN/SurvivalLog-Map", "v1.2.0", wikiDownloads: true)?.File);
        Assert.Equal("Map-1.1.0.zip", index.MirrorFor("com.ann.map", "Ann/SurvivalLog-Map", "1.1.0", wikiDownloads: true)?.File);
        Assert.Null(index.MirrorFor("com.ann.map", "Eve/SurvivalLog-Map", "v1.2.0", wikiDownloads: true));
        Assert.Null(index.MirrorFor("com.ann.map", null, "v1.2.0", wikiDownloads: true));
        Assert.Null(index.MirrorFor("com.ann.map", "Ann/SurvivalLog-Map", "v1.3.0", wikiDownloads: true));
        Assert.Null(index.MirrorFor("com.eve.map", "Ann/SurvivalLog-Map", "v1.2.0", wikiDownloads: true));
    }

    [Fact]
    public void With_wiki_downloads_off_an_update_has_no_wiki_copy_so_it_comes_from_github()
    {
        Assert.Null(Index().MirrorFor("com.ann.map", "Ann/SurvivalLog-Map", "v1.2.0", wikiDownloads: false));
    }

    [Fact]
    public void A_known_broken_build_says_why_and_other_versions_do_not()
    {
        var index = Index();

        Assert.Equal("Crashes on load.", index.BrokenWhy("com.ann.map", "v1.1.0")?.English);
        Assert.Null(index.BrokenWhy("com.ann.map", "1.2.0"));
        Assert.Null(index.BrokenWhy("com.ann.pack", "1.1.0"));
    }

    [Fact]
    public void A_conflict_is_seen_from_both_mods_and_each_other_mod_once()
    {
        var index = Index();

        Assert.Equal("com.bob.radar", Assert.Single(index.ConflictsOf("com.ann.map")).OtherGuid);
        Assert.Equal("Both redraw the map.", Assert.Single(index.ConflictsOf("com.bob.radar")).Why.English);
        Assert.Empty(index.ConflictsOf("com.ann.pack"));
    }

    [Fact]
    public void A_conflict_without_two_mods_and_a_reason_is_passed_over()
    {
        var read = ModIndexReader.Parse("{\"schema\":1,\"conflicts\":[{\"a\":\"x\",\"b\":\"x\",\"why\":\"same\"},{\"a\":\"x\",\"b\":\"y\"}]}").Read!;

        Assert.Empty(read.Index.Conflicts);
        Assert.Equal(2, read.Ignored.Count);
    }

    [Fact]
    public void The_Nexus_page_comes_from_the_mod_or_from_the_list_of_other_mods()
    {
        var index = Index();

        Assert.Equal(12, index.NexusIdOf("com.ann.map"));
        Assert.Equal(40, index.NexusIdOf("com.bob.radar"));
        Assert.Null(index.NexusIdOf("com.ann.pack"));
    }

    [Theory]
    [InlineData("https://sldb.core-directive.com/en/mods/map/", true, "https://sldb.core-directive.com/zh/mods/map/")]
    [InlineData("https://sldb.core-directive.com/en/mods/map/", false, "https://sldb.core-directive.com/en/mods/map/")]
    [InlineData("https://github.com/Ann/Map/releases/tag/en/", true, "https://github.com/Ann/Map/releases/tag/en/")]
    public void A_Chinese_game_opens_the_wikis_Chinese_page(string url, bool isChinese, string opened)
    {
        Assert.Equal(opened, MirrorAddress.ForLanguage(url, isChinese));
    }

    [Fact]
    public void A_copy_is_used_only_when_its_size_and_hash_match_the_index()
    {
        var bytes = Encoding.UTF8.GetBytes("the zip");
        var release = new IndexRelease("1.0.0", Array.Empty<string>(), "Map.zip", bytes.Length, Sha256Text.Of(bytes), null);

        Assert.Null(Sha256Text.Mismatch(bytes, release));
        Assert.Equal("the wiki's copy does not match the SHA-256 in the index", Sha256Text.Mismatch(Encoding.UTF8.GetBytes("the zap"), release));
        Assert.StartsWith("the wiki's copy is 3 bytes", Sha256Text.Mismatch(new byte[3], release));
    }

    [Fact]
    public void The_hash_is_written_the_way_the_index_writes_it()
    {
        // SHA-256 of "abc", from FIPS 180-2.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Sha256Text.Of(Encoding.ASCII.GetBytes("abc")));
        Assert.Equal(Sha256Text.Of(Encoding.ASCII.GetBytes("abc")), Sha256Text.Of(new MemoryStream(Encoding.ASCII.GetBytes("abc"))));
    }
}

public class UpdateBookIndexTests
{
    private static readonly string Hash = new('a', 64);

    private static ModIndex Index() =>
        ModIndexReader.Parse("{\"schema\":1,\"mods\":[{\"guid\":\"com.ann.map\",\"slug\":\"map\",\"github\":\"Ann/Map\",\"releases\":[{\"version\":\"1.2.0\",\"games\":[\"1.1.20719\"],\"file\":\"Map-1.2.0.zip\",\"size\":1,\"sha256\":\"" + Hash + "\"}]}]}").Read!.Index;

    [Fact]
    public void A_failed_GitHub_check_falls_back_to_the_index()
    {
        var book = new UpdateBook();

        book.Record(Index());
        book.Record("Ann/Map", new LatestRelease.Failed("offline"));

        Assert.Equal("v1.2.0", Assert.IsType<UpdateState.Available>(book.StateOf("Ann/Map", "1.1.0")).Tag);
        Assert.Equal("https://sldb.core-directive.com/en/mods/map/", book.UpdateFor("Ann/Map", "1.1.0")?.PageUrl);
    }

    [Fact]
    public void GitHubs_answer_wins_when_it_has_one()
    {
        var book = new UpdateBook();

        book.Record(Index());
        book.Record("Ann/Map", new LatestRelease.Found("Ann", "Map", "v1.3.0", "https://github.com/Ann/Map/releases/tag/v1.3.0"));

        Assert.Equal("v1.3.0", Assert.IsType<UpdateState.Available>(book.StateOf("Ann/Map", "1.1.0")).Tag);
    }

    [Fact]
    public void A_failed_check_of_a_mod_the_index_does_not_know_stays_failed()
    {
        var book = new UpdateBook();

        book.Record(Index());
        book.Record("Ann/Other", new LatestRelease.Failed("offline"));

        Assert.IsType<UpdateState.Failed>(book.StateOf("Ann/Other", "1.0"));
    }

    [Fact]
    public void The_game_versions_come_from_the_index_only_when_the_mod_lists_none_of_its_own()
    {
        var book = new UpdateBook();

        book.Record(Index());

        Assert.Equal(new[] { "1.1.20719" }, book.GameVersionsListed(null, "com.ann.map", "1.2.0", null));
        Assert.Empty(book.GameVersionsListed(null, "com.ann.map", "1.0.0", null));

        book.Record("Ann/Map", GameVersionLists.Parse("1.2.0 = 1.1.20610").List);

        Assert.Equal(new[] { "1.1.20610" }, book.GameVersionsListed("Ann/Map", "com.ann.map", "1.2.0", null));
    }
}
