using System;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

public class ModReportTests
{
    private static ModEntry Mod(string guid, string name, ModState state, ModSwitch modSwitch) =>
        new(guid, name, "1.2.0", name + ".dll", state, ModInfo.None, modSwitch, new UpdateState.NotChecked());

    private static readonly ModState Running = new ModState.Loaded(Array.Empty<Setting>());

    [Fact]
    public void The_report_names_the_game_each_mod_and_the_load_problems()
    {
        var view = new ModsView(
            new[]
            {
                Mod("com.a.storage", "Storage Hub", Running, new ModSwitch.Allowed(true, Array.Empty<string>())),
                Mod("com.a.broken", "Broken", new ModState.NotLoaded(), new ModSwitch.Unavailable()),
                Mod("com.a.sleepy", "Sleepy", new ModState.Off(), new ModSwitch.Allowed(false, Array.Empty<string>())),
            },
            new[] { "Broken needs\nsomething" },
            IsChinese: true,
            OpenKey: "F10") { GameVersion = "1.1.18902" };

        var text = ModReport.Text(view);

        Assert.Equal(
            "Survival Log 1.1.18902\n" +
            "Mods (3):\n" +
            "- Storage Hub 1.2.0 (com.a.storage, Storage Hub.dll): running\n" +
            "- Broken 1.2.0 (com.a.broken, Broken.dll): failed to load\n" +
            "- Sleepy 1.2.0 (com.a.sleepy, Sleepy.dll): off\n" +
            "Load problems:\n" +
            "- Broken needs something\n",
            text);
    }

    [Fact]
    public void A_mod_switched_this_session_says_what_happens_at_the_next_start()
    {
        var view = new ModsView(
            new[]
            {
                Mod("com.a.one", "One", Running, new ModSwitch.Allowed(false, Array.Empty<string>())),
                Mod("com.a.two", "Two", new ModState.Off(), new ModSwitch.Allowed(true, Array.Empty<string>())),
            },
            Array.Empty<string>(),
            false,
            "F10");

        var text = ModReport.Text(view);

        Assert.Contains("One 1.2.0 (com.a.one, One.dll): running, off from the next start", text);
        Assert.Contains("Two 1.2.0 (com.a.two, Two.dll): off, on from the next start", text);
        Assert.StartsWith("Survival Log (version unknown)\n", text);
        Assert.DoesNotContain("Load problems", text);
    }
}
