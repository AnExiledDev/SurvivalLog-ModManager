using System;
using System.Collections.Generic;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class PowersTests
{
    private static DllFacts Facts(string[]? types = null, string[]? members = null, string[]? nativeImports = null, bool carriesProgram = false) =>
        new(types ?? Array.Empty<string>(), members ?? Array.Empty<string>(), nativeImports ?? Array.Empty<string>(), carriesProgram);

    [Fact]
    public void A_mod_that_only_patches_the_game_has_no_powers()
    {
        var facts = Facts(new[] { "HarmonyLib.Harmony", "UnityEngine.GameObject", "System.String", "Il2CppSystem.Collections.Generic.List`1" }, new[] { "System.IO.File::ReadAllText" });

        Assert.Empty(Powers.Of(facts));
    }

    [Theory]
    [InlineData("System.Net.Http.HttpClient", Power.Network)]
    [InlineData("System.Net.Sockets.TcpClient", Power.Network)]
    [InlineData("System.Net.WebClient", Power.Network)]
    [InlineData("UnityEngine.Networking.UnityWebRequest", Power.Network)]
    [InlineData("Il2CppSystem.Net.WebClient", Power.Network)]
    [InlineData("System.Diagnostics.Process", Power.StartsPrograms)]
    [InlineData("System.Diagnostics.ProcessStartInfo", Power.StartsPrograms)]
    [InlineData("System.Runtime.InteropServices.NativeLibrary", Power.NativeCode)]
    [InlineData("Microsoft.Win32.RegistryKey", Power.Registry)]
    [InlineData("Il2CppMicrosoft.Win32.Registry", Power.Registry)]
    public void A_type_the_code_uses_gives_its_power(string type, Power power)
    {
        Assert.Equal(new[] { power }, Powers.Of(Facts(new[] { type })));
    }

    [Theory]
    [InlineData("System.Reflection.Assembly::Load", Power.LoadsOtherCode)]
    [InlineData("System.Runtime.Loader.AssemblyLoadContext::LoadFromStream", Power.LoadsOtherCode)]
    [InlineData("System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer", Power.NativeCode)]
    [InlineData("UnityEngine.Application::OpenURL", Power.OpensFiles)]
    public void A_member_the_code_calls_gives_its_power(string member, Power power)
    {
        Assert.Equal(new[] { power }, Powers.Of(Facts(members: new[] { member })));
    }

    [Fact]
    public void Names_that_only_start_alike_give_no_power()
    {
        var facts = Facts(new[] { "System.Net.HttpStatusCode", "System.Diagnostics.Stopwatch", "MyMod.ProcessQueue" }, new[] { "System.Reflection.Assembly::GetName", "System.Reflection.Assembly::LoadedThings" });

        Assert.Empty(Powers.Of(facts));
    }

    [Fact]
    public void Native_imports_and_an_embedded_program_count_too()
    {
        Assert.Equal(new[] { Power.NativeCode }, Powers.Of(Facts(nativeImports: new[] { "MessageBoxW" })));
        Assert.Equal(new[] { Power.LoadsOtherCode }, Powers.Of(Facts(carriesProgram: true)));
    }

    [Theory]
    [InlineData("CreateProcessW", Power.StartsPrograms)]
    [InlineData("ShellExecuteExW", Power.StartsPrograms)]
    [InlineData("WinExec", Power.StartsPrograms)]
    [InlineData("NtCreateUserProcess", Power.StartsPrograms)]
    [InlineData("ZwCreateUserProcess", Power.StartsPrograms)]
    [InlineData("SHCreateProcessAsUserW", Power.StartsPrograms)]
    [InlineData("_wsystem", Power.StartsPrograms)]
    [InlineData("system", Power.StartsPrograms)]
    [InlineData("_wspawnv", Power.StartsPrograms)]
    [InlineData("_popen", Power.StartsPrograms)]
    [InlineData("SHSetValueW", Power.Registry)]
    [InlineData("SHDeleteKeyA", Power.Registry)]
    [InlineData("ZwDeleteValueKey", Power.Registry)]
    [InlineData("NtRenameKey", Power.Registry)]
    [InlineData("NtOpenKeyEx", Power.Registry)]
    [InlineData("RegOpenKeyExW", Power.Registry)]
    [InlineData("RegSetValueExA", Power.Registry)]
    [InlineData("regdeletetreew", Power.Registry)]
    [InlineData("NtSetValueKey", Power.Registry)]
    public void A_windows_function_imported_directly_gives_its_power_as_well_as_native_code(string function, Power power)
    {
        Assert.Equal(new[] { power, Power.NativeCode }.OrderBy(p => p), Powers.Of(Facts(nativeImports: new[] { function })).OrderBy(p => p));
    }

    [Theory]
    [InlineData("SystemParametersInfoW")]
    [InlineData("GetAsyncKeyState")]
    [InlineData("RegisterClassExW")]
    [InlineData("NtCreateKeyedEvent")]
    [InlineData("executeQuery")]
    public void A_windows_function_that_only_starts_alike_is_native_code_alone(string function)
    {
        Assert.Equal(new[] { Power.NativeCode }, Powers.Of(Facts(nativeImports: new[] { function })));
    }

    [Fact]
    public void Only_powers_the_installed_version_lacked_are_new_in_a_fixed_order()
    {
        Assert.Equal(new[] { Power.StartsPrograms, Power.Registry }, Powers.Added(new[] { Power.Network }, new[] { Power.Registry, Power.Network, Power.StartsPrograms, Power.Registry }));
        Assert.Empty(Powers.Added(new[] { Power.Network, Power.Registry }, new[] { Power.Network }));
    }

    [Fact]
    public void An_unreadable_dll_is_always_new_even_after_an_unreadable_one()
    {
        Assert.Equal(new[] { Power.Unreadable }, Powers.Added(new[] { Power.Unreadable }, new[] { Power.Unreadable }));
    }
}

public class UpdateReviewTests
{
    private const string Guid = "com.ann.map";

    private static DllScan Dll(params string[] types) => Plugins(new[] { Guid }, types);

    private static DllScan Plugins(string[] guids, params string[] types) => new DllScan.Read(guids, new DllFacts(types, Array.Empty<string>(), Array.Empty<string>(), false));

    private static IncomingDll Incoming(DllScan scan, bool isNew = false) => new(scan, isNew);

    private static UpdateVerdict Judge(DllScan newDll, IEnumerable<DllScan> installed, params IncomingDll[] extra)
    {
        var incoming = new List<IncomingDll> { Incoming(newDll) };

        incoming.AddRange(extra);

        return UpdateReview.Judge(Guid, newDll, installed, incoming);
    }

    [Fact]
    public void An_update_that_can_do_nothing_new_is_clear()
    {
        Assert.IsType<UpdateVerdict.Clear>(Judge(Dll("System.Net.Http.HttpClient"), new[] { Dll("System.Net.WebClient") }));
    }

    [Fact]
    public void An_update_that_gains_a_power_waits_for_the_player()
    {
        var verdict = Judge(Dll("System.Net.Http.HttpClient", "System.Runtime.InteropServices.NativeLibrary"), new[] { Dll("System.Net.WebClient") });

        Assert.Equal(new[] { Power.NativeCode }, Assert.IsType<UpdateVerdict.NeedsApproval>(verdict).NewPowers);
    }

    [Fact]
    public void An_update_that_adds_a_dll_waits_for_the_player_even_when_it_is_harmless()
    {
        var verdict = Judge(Dll(), new[] { Dll() }, Incoming(Plugins(Array.Empty<string>()), isNew: true));

        Assert.Equal(new[] { Power.AddsDll }, Assert.IsType<UpdateVerdict.NeedsApproval>(verdict).NewPowers);
    }

    [Fact]
    public void A_power_in_another_dll_of_the_update_counts()
    {
        var verdict = Judge(Dll(), new[] { Dll() }, Incoming(Plugins(Array.Empty<string>(), "System.Net.WebClient")));

        Assert.Equal(new[] { Power.Network }, Assert.IsType<UpdateVerdict.NeedsApproval>(verdict).NewPowers);
    }

    [Theory]
    [InlineData("System.Diagnostics.Process", "it newly starts programs, which no update is allowed to start doing")]
    [InlineData("Microsoft.Win32.RegistryKey", "it newly uses the Windows registry, which no update is allowed to start doing")]
    public void An_update_that_gains_a_dangerous_power_is_refused(string type, string reason)
    {
        Assert.Equal(reason, Assert.IsType<UpdateVerdict.Rejected>(Judge(Dll(type), new[] { Dll() })).Reason);
    }

    [Fact]
    public void Starting_a_program_through_a_direct_windows_call_is_refused_too()
    {
        var update = new DllScan.Read(new[] { Guid }, new DllFacts(Array.Empty<string>(), Array.Empty<string>(), new[] { "CreateProcessW" }, false));

        Assert.Equal("it newly starts programs, which no update is allowed to start doing", Assert.IsType<UpdateVerdict.Rejected>(Judge(update, new[] { Dll() })).Reason);
    }

    [Fact]
    public void An_update_that_starts_opening_links_waits_for_the_player()
    {
        var update = new DllScan.Read(new[] { Guid }, new DllFacts(Array.Empty<string>(), new[] { "UnityEngine.Application::OpenURL" }, Array.Empty<string>(), false));

        Assert.Equal(new[] { Power.OpensFiles }, Assert.IsType<UpdateVerdict.NeedsApproval>(Judge(update, new[] { Dll() })).NewPowers);
    }

    [Fact]
    public void Code_loading_hidden_in_another_dll_of_the_update_is_refused_alongside_a_warned_power()
    {
        var loader = new DllScan.Read(Array.Empty<string>(), new DllFacts(Array.Empty<string>(), new[] { "System.Reflection.Assembly::Load" }, Array.Empty<string>(), false));
        var verdict = Judge(Dll("System.Net.Http.HttpClient"), new[] { Dll() }, Incoming(loader, isNew: true));

        Assert.Equal("it newly loads other code, which no update is allowed to start doing", Assert.IsType<UpdateVerdict.Rejected>(verdict).Reason);
    }

    [Fact]
    public void A_mod_that_already_started_programs_keeps_updating()
    {
        Assert.IsType<UpdateVerdict.Clear>(Judge(Dll("System.Diagnostics.Process", "Microsoft.Win32.Registry"), new[] { Dll("System.Diagnostics.ProcessStartInfo", "Microsoft.Win32.RegistryKey") }));
    }

    [Fact]
    public void An_unreadable_extra_dll_waits_for_the_player()
    {
        var verdict = Judge(Dll(), new[] { Dll() }, Incoming(new DllScan.Unreadable("bad header")));

        Assert.Equal(new[] { Power.Unreadable }, Assert.IsType<UpdateVerdict.NeedsApproval>(verdict).NewPowers);
    }

    [Fact]
    public void An_update_whose_dll_is_another_mod_is_refused()
    {
        var verdict = Judge(Plugins(new[] { "com.evil.thing" }), new[] { Dll() });

        Assert.Equal("its DLL is a different mod (com.evil.thing)", Assert.IsType<UpdateVerdict.Rejected>(verdict).Reason);
    }

    [Fact]
    public void An_update_whose_dll_holds_no_plugin_is_refused()
    {
        Assert.Equal("its DLL holds no BepInEx plugin", Assert.IsType<UpdateVerdict.Rejected>(Judge(Plugins(Array.Empty<string>()), new[] { Dll() })).Reason);
    }

    [Fact]
    public void An_update_whose_own_dll_cannot_be_read_is_refused()
    {
        var verdict = Judge(new DllScan.Unreadable("not a .NET file"), new[] { Dll() });

        Assert.Equal("its DLL could not be read (not a .NET file)", Assert.IsType<UpdateVerdict.Rejected>(verdict).Reason);
    }

    [Fact]
    public void Guids_match_exactly()
    {
        Assert.IsType<UpdateVerdict.Rejected>(Judge(Plugins(new[] { "COM.ANN.MAP" }), new[] { Dll() }));
    }
}

public class RepositoryPinTests
{
    private static readonly LatestRelease.Found Release = new("Ann", "Mod", "v1.0 beta", "https://github.com/Ann/Mod/releases/tag/v1.0%20beta");

    [Fact]
    public void The_release_is_fetched_by_the_pinned_number_with_its_tag_escaped()
    {
        Assert.Equal("https://api.github.com/repositories/1425067219/releases/tags/v1.0%20beta", RepositoryPin.ReleaseUri(1425067219, Release.Tag).AbsoluteUri);
        Assert.Equal("https://api.github.com/repos/Ann/Mod", RepositoryPin.RepositoryUri(Release).AbsoluteUri);
    }

    [Fact]
    public void A_pin_is_kept_under_the_name_the_mod_gives_not_where_GitHub_sends_it()
    {
        var moved = Release with { Owner = "Someone", Repository = "Else" };

        Assert.Equal("Ann/Mod", RepositoryPin.KeyOf("Ann/Mod", moved));
        Assert.Equal("Someone/Else", RepositoryPin.KeyOf(null, moved));
    }

    [Fact]
    public void The_number_comes_from_GitHubs_answer_about_the_repository()
    {
        Assert.Equal(1425067219L, RepositoryPin.IdFrom("{\"id\":1425067219,\"name\":\"Mod\"}"));
    }

    [Theory]
    [InlineData("{\"name\":\"Mod\"}")]
    [InlineData("{\"id\":\"1425067219\"}")]
    [InlineData("{\"id\":0}")]
    [InlineData("{\"id\":-5}")]
    [InlineData("[1]")]
    [InlineData("<html>")]
    public void An_answer_without_a_real_number_gives_none(string json)
    {
        Assert.Null(RepositoryPin.IdFrom(json));
    }

    [Fact]
    public void A_saved_pin_reads_back_and_anything_else_is_no_pin()
    {
        Assert.Equal(42L, RepositoryPin.Parse(RepositoryPin.Format(42) + "\n"));
        Assert.Null(RepositoryPin.Parse(""));
        Assert.Null(RepositoryPin.Parse("-42"));
        Assert.Null(RepositoryPin.Parse("42 43"));
        Assert.Null(RepositoryPin.Parse("0"));
    }
}
