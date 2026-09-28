using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>The one-time copy of %LocalAppData%\WindwakerOnline into the renamed WWOnline folder.</summary>
public sealed class LegacyAppDataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-migrate-" + Guid.NewGuid().ToString("N"));
    private string OldDir => Path.Combine(_root, LegacyAppDataMigration.LegacyFolderName);
    private string NewDir => Path.Combine(_root, AppPaths.AppDataFolderName);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void WriteOld(string relative, string content)
    {
        var path = Path.Combine(OldDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void NoLegacyFolder_DoesNothing()
    {
        var result = LegacyAppDataMigration.Run(OldDir, NewDir);

        Assert.False(result.Ran);
        Assert.False(Directory.Exists(NewDir));
    }

    [Fact]
    public void CopiesJsonSettingsAndLogs_AndKeepsTheOldFolder()
    {
        WriteOld("game-settings.json", """{"GamePath":"g"}""");
        WriteOld("window-settings.json", """{"Width":1}""");
        WriteOld("notes.txt", "not a settings file");
        WriteOld(Path.Combine("logs", "windwaker-online-20260101.txt"), "log line");

        var result = LegacyAppDataMigration.Run(OldDir, NewDir);

        Assert.True(result.Ran);
        Assert.Empty(result.FailedFiles);
        Assert.Equal("""{"GamePath":"g"}""", File.ReadAllText(Path.Combine(NewDir, "game-settings.json")));
        Assert.Equal("""{"Width":1}""", File.ReadAllText(Path.Combine(NewDir, "window-settings.json")));
        Assert.Equal("log line", File.ReadAllText(Path.Combine(NewDir, "logs", "windwaker-online-20260101.txt")));
        Assert.False(File.Exists(Path.Combine(NewDir, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(NewDir, LegacyAppDataMigration.MarkerFileName)));

        // Copy, not move: the old folder is untouched.
        Assert.True(File.Exists(Path.Combine(OldDir, "game-settings.json")));
        Assert.True(File.Exists(Path.Combine(OldDir, "logs", "windwaker-online-20260101.txt")));
    }

    [Fact]
    public void NewFolderAlreadyExists_StillCopies_ButNeverOverwrites()
    {
        // An installed app's WWOnline folder always exists (Velopack installs into it).
        WriteOld("game-settings.json", "old");
        WriteOld("window-settings.json", "old window");
        Directory.CreateDirectory(Path.Combine(NewDir, "current"));
        File.WriteAllText(Path.Combine(NewDir, "game-settings.json"), "new");

        var result = LegacyAppDataMigration.Run(OldDir, NewDir);

        Assert.True(result.Ran);
        Assert.Equal(["window-settings.json"], result.CopiedFiles);
        Assert.Equal("new", File.ReadAllText(Path.Combine(NewDir, "game-settings.json")));
        Assert.Equal("old window", File.ReadAllText(Path.Combine(NewDir, "window-settings.json")));
    }

    [Fact]
    public void RunsOnlyOnce()
    {
        WriteOld("game-settings.json", "old");
        Assert.True(LegacyAppDataMigration.Run(OldDir, NewDir).Ran);

        // The player deletes (resets) a settings file: it must not come back on the next start.
        File.Delete(Path.Combine(NewDir, "game-settings.json"));
        var second = LegacyAppDataMigration.Run(OldDir, NewDir);

        Assert.False(second.Ran);
        Assert.False(File.Exists(Path.Combine(NewDir, "game-settings.json")));
    }

    [Fact]
    public void JsonSettingsStore_ReadsTheMigratedFile()
    {
        WriteOld("game-settings.json", """{"GamePath":"C:\\games\\ww"}""");
        LegacyAppDataMigration.Run(OldDir, NewDir);

        var settings = new JsonSettingsStore<GameSettings>("game-settings.json", NewDir).Load();

        Assert.Equal(@"C:\games\ww", settings.GamePath);
    }
}
