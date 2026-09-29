using WWOnline.Patcher.Pipeline;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

public sealed class GameFolderCheckTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-folders-" + Guid.NewGuid().ToString("N"));

    public GameFolderCheckTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string Sub(string name) => Path.Combine(_dir, name);

    [Fact]
    public void UsGame_IsValid_WithItsId()
    {
        var check = GameFolderCheck.CheckVanilla(FakeGameFolder.Create(Sub("ww")));
        Assert.True(check.IsValid);
        Assert.Equal("GZLE01", check.GameId);
        Assert.Null(check.Note);
    }

    [Theory]
    [InlineData("GZLP01", "European")]
    [InlineData("GZLJ01", "Japanese")]
    [InlineData("GALE01", "different game")]
    public void OtherVersions_AreRefused_Clearly(string id, string expected)
    {
        var check = GameFolderCheck.CheckVanilla(FakeGameFolder.Create(Sub("other"), id));
        Assert.False(check.IsValid);
        Assert.Contains(expected, check.Error);
        Assert.Contains("GZLE01", check.Error);
    }

    [Fact]
    public void NoBootBin_IsAllowed_WithANote()
    {
        var check = GameFolderCheck.CheckVanilla(FakeGameFolder.Create(Sub("noboot"), gameId: null));
        Assert.True(check.IsValid);
        Assert.Contains("GZLE01", check.Note);
    }

    [Theory]
    [InlineData("game.iso")]
    [InlineData("game.rvz")]
    [InlineData("game.GCM")]
    public void DiscImage_ExplainsHowToExtract(string file)
    {
        var path = Sub(file);
        File.WriteAllBytes(path, [0]);
        var check = GameFolderCheck.CheckVanilla(path);
        Assert.False(check.IsValid);
        Assert.Contains("disc image", check.Error);
        Assert.Contains("Extract Entire Disc", check.Error);
    }

    [Fact]
    public void ParentFolder_PointsAtTheGameInside()
    {
        var inner = FakeGameFolder.Create(Sub(Path.Combine("dump", "WindWaker")));
        var check = GameFolderCheck.CheckVanilla(Sub("dump"));
        Assert.False(check.IsValid);
        Assert.Equal(inner, check.GameFolder);
    }

    [Fact]
    public void MissingFiles_AndPatchedCopies_AreRefused()
    {
        Assert.False(GameFolderCheck.CheckVanilla("").IsValid);
        Assert.Contains("not found", GameFolderCheck.CheckVanilla(Sub("nope")).Error);
        Assert.Contains("RELS.arc", GameFolderCheck.CheckVanilla(FakeGameFolder.Create(Sub("norels"), withRelsArc: false)).Error);

        var patched = FakeGameFolder.Create(Sub("patched"));
        File.WriteAllText(BuildStamp.GetStampPath(patched), "{}");
        Assert.Contains("already patched", GameFolderCheck.CheckVanilla(patched).Error);
    }

    [Fact]
    public void PatchedFolder_MustBeSeparateFromTheOriginal()
    {
        var vanilla = FakeGameFolder.Create(Sub("ww"));
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(vanilla, vanilla));
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(vanilla, vanilla + Path.DirectorySeparatorChar));
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(vanilla, Path.Combine(vanilla, "out")));
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(vanilla, _dir)); // contains the original
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(vanilla, ""));
        Assert.Null(GameFolderCheck.CheckPatchedFolder(vanilla, Sub("ww-WWOnline")));
        Assert.Equal(Sub("ww-WWOnline"), GameFolderCheck.SuggestPatchedFolder(vanilla));
    }

    [Fact]
    public void CopyMissingGameFiles_CopiesTheRestOfTheGame_Once()
    {
        var vanilla = FakeGameFolder.Create(Sub("ww"));
        File.WriteAllBytes(Path.Combine(vanilla, "The Wind Waker.iso"), [1, 2]); // lying next to sys/files: not game data
        File.WriteAllText(Path.Combine(vanilla, "notes.txt"), "hi");
        var output = Sub("out");

        // main.dol, bi2.bin and RELS.arc are the patch steps' job; sys/boot.bin and files/Stage/sea.arc are ours.
        Assert.Equal(2, GamePatcherService.CopyMissingGameFiles(vanilla, output));
        Assert.True(File.Exists(Path.Combine(output, "files", "Stage", "sea.arc")));
        Assert.True(File.Exists(Path.Combine(output, "sys", "boot.bin")));
        Assert.False(File.Exists(Path.Combine(output, "sys", "main.dol")));
        Assert.False(File.Exists(Path.Combine(output, "The Wind Waker.iso")));
        Assert.False(File.Exists(Path.Combine(output, "notes.txt")));
        Assert.Empty(Directory.EnumerateFiles(output, "*.partial", SearchOption.AllDirectories));

        Assert.Equal(0, GamePatcherService.CopyMissingGameFiles(vanilla, output));
    }

    [Fact]
    public void CopyMissingGameFiles_RepairsAFileCutShort()
    {
        var vanilla = FakeGameFolder.Create(Sub("ww"));
        var output = Sub("out");
        GamePatcherService.CopyMissingGameFiles(vanilla, output);
        var sea = Path.Combine(output, "files", "Stage", "sea.arc");
        File.WriteAllBytes(sea, [4]); // an interrupted copy left it short

        Assert.Equal(1, GamePatcherService.CopyMissingGameFiles(vanilla, output));
        Assert.Equal(File.ReadAllBytes(Path.Combine(vanilla, "files", "Stage", "sea.arc")), File.ReadAllBytes(sea));
    }

    [Fact]
    public void DriveRootOriginal_KeepsThePatchedFolderOffThatDrive()
    {
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(@"E:\", @"E:\WW-Patched"));
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(@"E:\", @"E:\"));
        Assert.NotNull(GameFolderCheck.CheckPatchedFolder(@"E:\Games\WW", @"E:\"));
        Assert.Null(GameFolderCheck.CheckPatchedFolder(@"E:\", @"F:\WW-Patched"));
        Assert.True(GameFolderCheck.IsSameOrInside(@"E:\WW-Patched", @"E:\"));
        Assert.False(GameFolderCheck.IsSameOrInside(@"E:\Games2", @"E:\Games"));
    }

    [Fact]
    public void PatchedFolder_ThatIsTheOriginalUnderAnotherName_IsRefused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vanilla = FakeGameFolder.Create(Sub("ww"));
        var copy = FakeGameFolder.Create(Sub("copy"));
        Assert.True(FileIdentity.SameFile(Path.Combine(vanilla, "sys", "main.dol"), Path.Combine(vanilla, "sys", "main.dol")));
        Assert.False(FileIdentity.SameFile(Path.Combine(vanilla, "sys", "main.dol"), Path.Combine(copy, "sys", "main.dol")));

        // A junction to the original (no admin rights needed) spells the same folder differently.
        var junction = Sub("alias");
        using (var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{vanilla}\"")
               { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
        {
            mklink!.WaitForExit(10_000);
        }
        if (!Directory.Exists(junction)) return; // couldn't make one here: nothing to test
        try
        {
            Assert.Contains("different folder", GameFolderCheck.CheckPatchedFolder(vanilla, junction));
            Assert.Null(GameFolderCheck.CheckPatchedFolder(vanilla, copy));
        }
        finally
        {
            Directory.Delete(junction); // removes the link only
        }
    }

    [Fact]
    public async Task OnlyOnePatchRunsAtATime()
    {
        var settings = new GameSettingsService(Sub("settings"));
        var patcher = new GamePatcherService(new OptionalPatchCatalogService(settings, WWOnline.Patcher.Patches.OptionalPatchCatalog.Empty),
            Sub("PatchData"));
        var vanilla = FakeGameFolder.Create(Sub("ww"));
        var progress = new BlockingProgress();

        // The first run stops in its first progress report (copying the game), then fails there: it never
        // reaches the compile or patch steps.
        var first = Task.Run(() => patcher.PatchGameAsync(vanilla, Sub("out"), [], progress));
        Assert.True(progress.Entered.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(patcher.IsPatching);

        var second = await patcher.PatchGameAsync(vanilla, Sub("out2"), [], null);
        Assert.Contains("already running", second);
        Assert.False(Directory.Exists(Sub("out2")));

        progress.Release.Set();
        Assert.Contains("Couldn't copy the game", await first);
        Assert.False(patcher.IsPatching);
    }

    /// <summary>Holds the first report until released, then fails it like a disk error.</summary>
    private sealed class BlockingProgress : IProgress<string>
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();

        public void Report(string value)
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            throw new IOException("disk full");
        }
    }

    [Fact]
    public void DolphinValidation_WantsDolphinExe()
    {
        Assert.NotNull(DolphinLocator.Validate(""));
        Assert.NotNull(DolphinLocator.Validate(Sub("missing.exe")));

        var folder = Directory.CreateDirectory(Sub("Dolphin-x64")).FullName;
        var exe = Path.Combine(folder, "Dolphin.exe");
        File.WriteAllBytes(exe, [0]);
        var tool = Path.Combine(folder, "DolphinTool.exe");
        File.WriteAllBytes(tool, [0]);

        Assert.Null(DolphinLocator.Validate(exe));
        Assert.Contains("folder", DolphinLocator.Validate(folder));
        Assert.Contains("helper", DolphinLocator.Validate(tool));

        var found = DolphinLocator.FindCandidates([exe], [exe, Sub("nope.exe"), exe.ToUpperInvariant()]);
        Assert.Equal([exe], found);
    }
}
