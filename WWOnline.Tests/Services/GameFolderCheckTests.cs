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
        var output = Sub("out");
        Directory.CreateDirectory(Path.Combine(output, "sys"));
        File.WriteAllBytes(Path.Combine(output, "sys", "main.dol"), [9]); // already there: left alone

        Assert.Equal(4, GamePatcherService.CopyMissingGameFiles(vanilla, output));
        Assert.True(File.Exists(Path.Combine(output, "files", "Stage", "sea.arc")));
        Assert.True(File.Exists(Path.Combine(output, "sys", "boot.bin")));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(Path.Combine(output, "sys", "main.dol")));

        Assert.Equal(0, GamePatcherService.CopyMissingGameFiles(vanilla, output));
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
