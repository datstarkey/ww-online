using System.IO.Compression;
using WWOnline.Server;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>A hosted room starts with the rules the host last left one on (<see cref="HostedRoomRules"/>).</summary>
public class HostedRoomRulesTests
{
    [Fact]
    public void NeverHosted_PassesNoFlags_EveryRuleOn()
    {
        Assert.Equal("", HostedRoomRules.ServerArgs(null));
        Assert.Equal("", HostedRoomRules.ServerArgs(new RoomSettings()));
    }

    [Fact]
    public void TheSavedRules_BecomeTheServersFlags_AndParseBackToTheSameRules()
    {
        var coop = new RoomSettings().ApplyPreset(RoomPreset.Coop);
        var args = HostedRoomRules.ServerArgs(coop);
        Assert.Contains(" --no-shared-wallet", args);
        Assert.Contains(" --no-warping", args);
        Assert.DoesNotContain("projectiles", args); // on in Co-op

        var parsed = ServerOptions.Parse(("6969" + args).Split(' ', StringSplitOptions.RemoveEmptyEntries), _ => null);
        Assert.True(parsed.IsValid);
        var o = parsed.Options;
        var back = new RoomSettings
        {
            SharedWallet = o.SharedWallet, SharedWorld = o.SharedWorld, SharedItems = o.SharedItems, SharedStory = o.SharedStory,
            SharedBait = o.SharedBait, SharedSpoils = o.SharedSpoils, SharedDelivery = o.SharedDelivery,
            SharedProjectiles = o.SharedProjectiles, AllowWarping = o.AllowWarping,
        };
        Assert.True(HostedRoomRules.SameRules(coop, back));
    }

    [Fact]
    public void OnlyTheRulesAreSaved_NotTheOwner()
    {
        var s = new RoomSettings { SharedWallet = false, OwnerConnectionId = "abc", OwnerName = "Link" };
        var saved = HostedRoomRules.RulesOnly(s);
        Assert.Equal("", saved.OwnerConnectionId);
        Assert.Equal("", saved.OwnerName);
        Assert.False(saved.SharedWallet);
        Assert.Equal("abc", s.OwnerConnectionId); // a copy
        Assert.True(HostedRoomRules.SameRules(s, saved));
        Assert.False(HostedRoomRules.SameRules(s, new RoomSettings()));
        Assert.False(HostedRoomRules.SameRules(null, s));
    }

    [Fact]
    public void TheRules_RoundTripThroughTheSettingsFile()
    {
        var dir = Directory.CreateTempSubdirectory("wwo-hosted-rules").FullName;
        try
        {
            var service = new GameSettingsService(dir);
            var settings = service.Load();
            Assert.Null(settings.HostedRoomRules);
            settings.HostedRoomRules = new RoomSettings { SharedStory = false, AllowWarping = false };
            service.Save(settings);
            var loaded = new GameSettingsService(dir).Load().HostedRoomRules;
            Assert.NotNull(loaded);
            Assert.False(loaded!.SharedStory);
            Assert.False(loaded.AllowWarping);
            Assert.True(loaded.SharedWallet);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

/// <summary>Settings → Bug report (<see cref="BugReport"/>): the recent logs and a summary in one zip, the user
/// folder redacted.</summary>
public class BugReportTests
{
    private static string Touch(string path, string text, DateTime when)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        File.SetLastWriteTime(path, when);
        return path;
    }

    [Fact]
    public void CollectFiles_TakesTheNewestRecentClientLogs_AndTheServerLog()
    {
        var dir = Directory.CreateTempSubdirectory("wwo-bugreport").FullName;
        try
        {
            var now = new DateTime(2026, 9, 30, 12, 0, 0);
            for (int d = 0; d < 5; d++)
                Touch(Path.Combine(dir, $"ww-online-2026092{9 - d}.txt"), "x", now.AddHours(-d * 10));
            Touch(Path.Combine(dir, "server.log"), "s", now.AddMinutes(-5));
            Touch(Path.Combine(dir, "old-other.txt"), "o", now);

            var files = BugReport.CollectFiles(dir, null, dolphinExe: Path.Combine(dir, "nope", "Dolphin.exe"), now);
            var clients = files.Where(f => f.Name.StartsWith("client/")).ToList();
            Assert.Equal(BugReport.MaxClientLogs, clients.Count);
            Assert.Equal("client/ww-online-20260929.txt", clients[0].Name); // newest first
            Assert.Contains(files, f => f.Name == "server/server.log");
            Assert.DoesNotContain(files, f => f.Name.Contains("old-other"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void CollectFiles_LeavesOutStaleLogs_AndFindsAPortableDolphinsLog()
    {
        var dir = Directory.CreateTempSubdirectory("wwo-bugreport").FullName;
        try
        {
            var now = DateTime.Now;
            Touch(Path.Combine(dir, "ww-online-20200101.txt"), "old", now.AddDays(-30));
            var dolphinExe = Path.Combine(dir, "Dolphin", "Dolphin.exe");
            Touch(Path.Combine(dir, "Dolphin", "User", "Logs", "dolphin.log"), "d", now);

            var files = BugReport.CollectFiles(dir, null, dolphinExe, now);
            Assert.DoesNotContain(files, f => f.Name.StartsWith("client/"));
            Assert.Contains(files, f => f.Name == "dolphin/dolphin.log");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Redact_ReplacesTheUserFolder_AnyCase_EitherSlash()
    {
        const string profile = @"C:\Users\Tetra";
        Assert.Equal(@"game at %USERPROFILE%\Games and %USERPROFILE%/x",
            BugReport.Redact(@"game at c:\users\tetra\Games and C:/Users/Tetra/x", profile));
        Assert.Equal("nothing here", BugReport.Redact("nothing here", ""));
    }

    [Fact]
    public void Write_ZipsTheLogsRedacted_WithAnAboutFile()
    {
        var dir = Directory.CreateTempSubdirectory("wwo-bugreport").FullName;
        try
        {
            var now = new DateTime(2026, 9, 30, 12, 34, 56);
            var log = Touch(Path.Combine(dir, "logs", "ww-online-20260930.txt"), @"[launch] C:\Users\Tetra\Dolphin", now);
            var about = BugReport.About(new GameSettings { OptionalPatches = ["skip_intro"] }, "up to date", now);
            Assert.Contains("Optional patches: skip_intro", about);
            Assert.Contains("never hosted", about);

            var zipPath = BugReport.Write(Path.Combine(dir, "out"),
                [new("client/ww-online-20260930.txt", log), new("server/server.log", Path.Combine(dir, "missing.log"))],
                about, @"C:\Users\Tetra", now);
            Assert.EndsWith("WW-Online-bug-report-20260930-123456.zip", zipPath);

            using var zip = ZipFile.OpenRead(zipPath);
            Assert.Equal(["about.txt", "client/ww-online-20260930.txt"], zip.Entries.Select(e => e.FullName).OrderBy(n => n));
            using (var r = new StreamReader(zip.GetEntry("client/ww-online-20260930.txt")!.Open()))
                Assert.Equal(@"[launch] %USERPROFILE%\Dolphin", r.ReadToEnd());
            using (var r = new StreamReader(zip.GetEntry("about.txt")!.Open()))
                Assert.Contains("Couldn't include:", r.ReadToEnd()); // the missing server log is noted
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
