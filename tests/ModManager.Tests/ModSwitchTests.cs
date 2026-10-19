using System;
using System.IO;
using System.Linq;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ModSwitchTests
{
    private const string Self = "mm";

    private static PluginNode Node(string guid, string file, params string[] needs) => new(guid, guid.ToUpperInvariant(), file, needs);

    private static readonly PluginNode Toolkit = Node("tk", "Toolkit.dll");
    private static readonly PluginNode Manager = Node(Self, "ModManager.dll", "tk");
    private static readonly PluginNode Base = Node("base", "Base.dll");
    private static readonly PluginNode Middle = Node("mid", "Mid.dll", "base");
    private static readonly PluginNode Top = Node("top", "Top.dll", "mid");
    private static readonly PluginNode Twin = Node("twin", "base.DLL");
    private static readonly PluginNode Loner = Node("loner", "Loner.dll");
    private static readonly PluginNode[] All = { Toolkit, Manager, Base, Middle, Top, Twin, Loner };

    [Fact]
    public void Turning_a_mod_off_stops_everything_that_needs_it_through_any_chain_and_anything_in_its_DLL()
    {
        var stopped = SwitchPlanner.StopsWith(Base, All).Select(plugin => plugin.Guid).OrderBy(guid => guid);

        Assert.Equal(new[] { "mid", "top", "twin" }, stopped);
    }

    [Fact]
    public void A_mod_nothing_needs_stops_alone()
    {
        var plan = Assert.IsType<ModSwitch.Allowed>(SwitchPlanner.ForLoaded(Loner, All, Self, true));

        Assert.Empty(plan.AlsoStops);
    }

    [Fact]
    public void An_allowed_switch_names_what_stops_with_it_and_where_it_points_now()
    {
        var plan = Assert.IsType<ModSwitch.Allowed>(SwitchPlanner.ForLoaded(Base, All, Self, false));

        Assert.False(plan.IsOnNextStart);
        Assert.Equal(new[] { "MID", "TOP", "TWIN" }, plan.AlsoStops);
    }

    [Fact]
    public void The_Mod_Manager_cannot_turn_itself_off()
    {
        var plan = Assert.IsType<ModSwitch.Locked>(SwitchPlanner.ForLoaded(Manager, All, Self, true));

        Assert.True(plan.IsModManager);
    }

    [Fact]
    public void A_mod_the_Mod_Manager_needs_is_locked_so_the_way_back_on_never_goes_away()
    {
        var plan = Assert.IsType<ModSwitch.Locked>(SwitchPlanner.ForLoaded(Toolkit, All, Self, true));

        Assert.False(plan.IsModManager);
        Assert.Equal(new[] { "MM" }, plan.NeededBy);
    }

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "slmm-folders");
    private static readonly string Plugins = Path.Combine(Root, "plugins");
    private static readonly string Off = Path.Combine(Root, "disabled-plugins");
    private static readonly string OldOff = Path.Combine(Root, "plugins-disabled");
    private static readonly PluginFolders Folders = new(Plugins, new[] { Off, OldOff });

    [Fact]
    public void Off_keeps_the_DLLs_place_below_the_folder_and_on_brings_it_back()
    {
        var on = Path.Combine(Plugins, "Pack", "Mod.dll");

        var off = Folders.OffPath(on);

        Assert.Equal(Path.Combine(Off, "Pack", "Mod.dll"), off);
        Assert.Equal(on, Folders.OnPath(off!));
        Assert.True(Folders.IsOn(on));
        Assert.False(Folders.IsOn(off!));
    }

    [Fact]
    public void A_DLL_in_the_older_off_folder_comes_back_on_too()
    {
        Assert.Equal(Path.Combine(Plugins, "Mod.dll"), Folders.OnPath(Path.Combine(OldOff, "Mod.dll")));
    }

    [Theory]
    [InlineData("core", "BepInEx.Core.dll")]
    [InlineData("plugins-extra", "Mod.dll")]
    [InlineData("..foo", "Mod.dll")]
    public void A_DLL_outside_the_folders_has_nowhere_to_move(string folder, string file)
    {
        var path = Path.Combine(Root, folder, file);

        Assert.Null(Folders.OffPath(path));
        Assert.Null(Folders.OnPath(path));
        Assert.False(Folders.IsOn(path));
    }

    [Fact]
    public void A_mod_whose_update_is_downloading_or_waiting_for_a_restart_cannot_be_switched()
    {
        var allowed = new ModSwitch.Allowed(true, Array.Empty<string>());

        var whileInstalling = SwitchPlanner.HoldWhileFilesChange(allowed, new InstallState.Installing(), new RollbackState.None());
        var afterInstall = SwitchPlanner.HoldWhileFilesChange(allowed, new InstallState.Installed("v1.1.0"), new RollbackState.Available("1.0.0"));
        var afterRollback = SwitchPlanner.HoldWhileFilesChange(allowed, new InstallState.None(), new RollbackState.Done("1.0.0"));

        Assert.Equal(new ModSwitch.Held(true), whileInstalling);
        Assert.Equal(new ModSwitch.Held(true), afterInstall);
        Assert.Equal(new ModSwitch.Held(true), afterRollback);
    }

    [Fact]
    public void A_failed_install_or_rollback_changed_no_files_so_the_switch_stays_free()
    {
        var allowed = new ModSwitch.Allowed(false, new[] { "Child" });

        Assert.Same(allowed, SwitchPlanner.HoldWhileFilesChange(allowed, new InstallState.Failed("no zip"), new RollbackState.Failed("1.0.0", "disk full")));
        Assert.Same(allowed, SwitchPlanner.HoldWhileFilesChange(allowed, new InstallState.None(), new RollbackState.None()));
    }

    [Fact]
    public void A_locked_or_immovable_mod_keeps_its_reason_during_an_update()
    {
        var locked = new ModSwitch.Locked(true, Array.Empty<string>());
        var unavailable = new ModSwitch.Unavailable();

        Assert.Same(locked, SwitchPlanner.HoldWhileFilesChange(locked, new InstallState.Installing(), new RollbackState.None()));
        Assert.Same(unavailable, SwitchPlanner.HoldWhileFilesChange(unavailable, new InstallState.Installed("v2"), new RollbackState.None()));
    }

    [Fact]
    public void The_plugins_folder_itself_is_not_a_DLL_to_move()
    {
        Assert.Null(Folders.OffPath(Plugins));
    }
}
