using System;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests
{
    /// <summary>
    /// Before updates are installed, Save Backups is asked to back up the saves, found by name with no reference either
    /// way. A Save Backups without the call is skipped, and installs close together ask once.
    /// </summary>
    public sealed class SaveBackupAskTests
    {
        private static readonly DateTime Start = new(2026, 11, 8, 20, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void The_call_is_found_by_name_and_returns_what_Save_Backups_returns()
        {
            SaveBackups.UpdateBackup.Saves = 3;

            var call = SaveBackupAsk.Find(typeof(SaveBackupAskTests).Assembly);

            Assert.NotNull(call);
            Assert.Equal(3, call!());
        }

        [Fact]
        public void An_assembly_without_the_call_gives_none()
        {
            Assert.Null(SaveBackupAsk.Find(typeof(object).Assembly));
            Assert.Null(SaveBackupAsk.Find(null));
        }

        [Fact]
        public void Installs_close_together_ask_once()
        {
            var ask = new SaveBackupAsk();

            Assert.True(ask.TakeTurn(Start));
            Assert.False(ask.TakeTurn(Start.AddMinutes(1)));
            Assert.False(ask.TakeTurn(Start.AddMinutes(9)));
        }

        [Fact]
        public void An_install_after_the_quiet_time_asks_again()
        {
            var ask = new SaveBackupAsk();

            ask.TakeTurn(Start);

            Assert.True(ask.TakeTurn(Start + SaveBackupAsk.Quiet));
            Assert.False(ask.TakeTurn(Start + SaveBackupAsk.Quiet + TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public void A_clock_set_back_asks_again()
        {
            var ask = new SaveBackupAsk();

            ask.TakeTurn(Start);

            Assert.True(ask.TakeTurn(Start.AddMinutes(-5)));
        }
    }
}

namespace SaveBackups
{
    /// <summary>Stands in for Save Backups' own class of this name.</summary>
    public static class UpdateBackup
    {
        public static int Saves;

        public static int BeforeModUpdates() => Saves;
    }
}
