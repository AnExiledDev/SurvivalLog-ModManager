using System.IO;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ReleaseAssetsTests
{
    private const string Download = "https://github.com/Ann/Map/releases/download/v1.2.0/";

    private static string Release(params (string Name, string Url, long Size)[] assets) =>
        "{\"tag_name\":\"v1.2.0\",\"assets\":[" + string.Join(",", assets.Select(asset => "{\"name\":\"" + asset.Name + "\",\"browser_download_url\":\"" + asset.Url + "\",\"size\":" + asset.Size + "}")) + "]}";

    [Fact]
    public void The_only_zip_is_chosen_whatever_its_name()
    {
        var (asset, problem) = ReleaseAssets.Choose(Release(("release.zip", Download + "release.zip", 10), ("Map.dll", Download + "Map.dll", 10)), "Map.dll");

        Assert.Null(problem);
        Assert.Equal("release.zip", asset?.Name);
    }

    [Fact]
    public void With_several_zips_the_one_named_after_the_dll_is_chosen()
    {
        var (asset, _) = ReleaseAssets.Choose(Release(("Other.zip", Download + "Other.zip", 10), ("Map-1.2.0.zip", Download + "Map-1.2.0.zip", 10)), "Map.dll");

        Assert.Equal("Map-1.2.0.zip", asset?.Name);
    }

    [Theory]
    [InlineData("{\"assets\":[]}")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void A_release_without_a_zip_cannot_be_installed(string json)
    {
        Assert.Equal("the release has no .zip file to install", ReleaseAssets.Choose(json, "Map.dll").Problem);
    }

    [Fact]
    public void Several_zips_none_named_after_the_dll_are_not_guessed_between()
    {
        var (asset, problem) = ReleaseAssets.Choose(Release(("a.zip", Download + "a.zip", 1), ("b.zip", Download + "b.zip", 1)), "Map.dll");

        Assert.Null(asset);
        Assert.Equal("the release has several .zip files and none is named after Map", problem);
    }

    [Fact]
    public void A_zip_over_the_size_limit_or_off_GitHub_is_refused()
    {
        Assert.Null(ReleaseAssets.Choose(Release(("Map.zip", Download + "Map.zip", ReleaseAssets.MaxDownloadBytes + 1)), "Map.dll").Asset);
        Assert.StartsWith("the release's file is not on GitHub", ReleaseAssets.Choose(Release(("Map.zip", "https://evil.example/Map.zip", 1)), "Map.dll").Problem);
    }

    [Fact]
    public void A_broken_answer_is_a_problem_not_a_crash()
    {
        Assert.StartsWith("GitHub sent a release that could not be read", ReleaseAssets.Choose("{not json", "Map.dll").Problem);
    }
}

public class DownloadAddressTests
{
    [Theory]
    [InlineData("https://github.com/Ann/Map/releases/download/v1/Map.zip")]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1")]
    [InlineData("https://GitHub.com/Ann/Map.zip")]
    public void GitHub_and_its_file_storage_over_https_are_allowed(string url)
    {
        Assert.True(DownloadAddress.IsAllowed(new System.Uri(url)));
    }

    [Theory]
    [InlineData("http://github.com/Ann/Map.zip")]
    [InlineData("https://github.com:8443/Ann/Map.zip")]
    [InlineData("https://user@github.com/Ann/Map.zip")]
    [InlineData("https://github.com.evil.example/Map.zip")]
    [InlineData("https://evilgithubusercontent.com/Map.zip")]
    [InlineData("https://githubusercontent.com.evil.example/Map.zip")]
    [InlineData("file:///C:/Windows/system32/x.zip")]
    public void Anything_else_is_refused(string url)
    {
        Assert.False(DownloadAddress.IsAllowed(new System.Uri(url)));
    }

    [Fact]
    public void A_redirect_may_be_relative_but_never_leave_GitHub()
    {
        var request = new System.Uri("https://github.com/Ann/Map/releases/download/v1/Map.zip");

        Assert.Equal("https://github.com/elsewhere.zip", DownloadAddress.Next(request, "/elsewhere.zip")?.AbsoluteUri);
        Assert.Equal("https://objects.githubusercontent.com/x", DownloadAddress.Next(request, "https://objects.githubusercontent.com/x")?.AbsoluteUri);
        Assert.Null(DownloadAddress.Next(request, "https://evil.example/x"));
        Assert.Null(DownloadAddress.Next(request, null));
        Assert.Null(DownloadAddress.Next(request, " "));
    }
}

public class ZipPlanTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "plugins", "Map");

    private static ZipItem Item(string name, long length = 10) => new(name, length);

    private static string In(params string[] parts) => Path.GetFullPath(Path.Combine(new[] { Folder }.Concat(parts).ToArray()));

    [Fact]
    public void The_folder_holding_the_dll_in_the_zip_maps_onto_the_mods_folder()
    {
        var items = new[] { Item("Map/"), Item("Map/Map.dll"), Item("Map/lang/zh.json"), Item("README.md") };

        var (files, problem) = ZipPlan.Place(items, "Map.dll", Folder, false);

        Assert.Null(problem);
        Assert.Equal(new[] { new PlacedFile("Map/Map.dll", In("Map.dll")), new PlacedFile("Map/lang/zh.json", In("lang", "zh.json")) }, files);
    }

    [Fact]
    public void The_shallowest_copy_of_the_dll_sets_the_root()
    {
        var (files, _) = ZipPlan.Place(new[] { Item("deep/x/Map.dll"), Item("Map.dll") }, "Map.dll", Folder, false);

        Assert.Equal(new[] { In("deep", "x", "Map.dll"), In("Map.dll") }, files!.Select(file => file.Target));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("lang/../../evil.dll")]
    [InlineData("..\\evil.dll")]
    [InlineData("/Windows/evil.dll")]
    [InlineData("C:/Windows/evil.dll")]
    public void A_file_that_would_land_outside_the_mods_folder_stops_the_install(string name)
    {
        var (files, problem) = ZipPlan.Place(new[] { Item("Map.dll"), Item(name) }, "Map.dll", Folder, false);

        Assert.Null(files);
        Assert.StartsWith("the zip has a file that would land outside the mod's folder", problem);
    }

    [Fact]
    public void A_zip_without_the_dll_is_refused()
    {
        Assert.Equal("the zip does not hold Map.dll", ZipPlan.Place(new[] { Item("Other.dll"), Item("Map.dll/") }, "Map.dll", Folder, false).Problem);
    }

    [Fact]
    public void In_the_shared_plugins_folder_only_files_named_after_the_dll_are_written()
    {
        var items = new[] { Item("Map.dll"), Item("map.PDB"), Item("Map.xml"), Item("Map.json"), Item("Other.dll"), Item("sub/Map.dll.config") };

        var (files, _) = ZipPlan.Place(items, "Map.dll", Folder, true);

        Assert.Equal(new[] { "Map.dll", "map.PDB", "Map.xml" }, files!.Select(file => file.EntryName));
    }

    [Fact]
    public void Too_many_files_or_too_many_bytes_are_refused_before_anything_else()
    {
        var many = Enumerable.Range(0, ZipPlan.MaxEntries + 1).Select(i => Item("f" + i)).Append(Item("Map.dll")).ToList();

        Assert.Equal("the zip has more than 100 files", ZipPlan.Place(many, "Map.dll", Folder, false).Problem);
        Assert.Equal("the zip unpacks to more than 200 MB", ZipPlan.Place(new[] { Item("Map.dll", ZipPlan.MaxUnpackedBytes), Item("x", 1) }, "Map.dll", Folder, false).Problem);
        Assert.Null(ZipPlan.Place(new[] { Item("Map.dll", ZipPlan.MaxUnpackedBytes) }, "Map.dll", Folder, false).Problem);
    }
}

public class UpdateToastTests
{
    [Fact]
    public void One_update_and_several_read_differently()
    {
        Assert.Equal("1 mod has an update. Click to see it.", UpdateToast.Text(1).English);
        Assert.Equal("3 mods have updates. Click to see them.", UpdateToast.Text(3).English);
        Assert.StartsWith("3 ", UpdateToast.Text(3).Chinese);
    }
}
