using WWOnline.Patcher.Config;
using Xunit;

namespace WWOnline.Patcher.Tests.Config;

public class PatcherConfigTests : IDisposable
{
    private readonly string _gameMod = Path.Combine(Path.GetTempPath(), "wwo-cfg-" + Guid.NewGuid().ToString("N"));

    public PatcherConfigTests() => Directory.CreateDirectory(_gameMod);

    public void Dispose()
    {
        try { Directory.Delete(_gameMod, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Load_ReadsVanillaGamePath_AndResolvesExtractedLayout()
    {
        File.WriteAllText(Path.Combine(_gameMod, PatcherConfig.ConfigFileName),
            """{ "game_path": "C:/out", "vanilla_game_path": "C:/vanilla", "devkitppc_path": "C:/dkp" }""");

        var config = PatcherConfig.LoadFromConfigJson(_gameMod);

        Assert.Equal("C:/vanilla", config.VanillaGamePath);
        Assert.Equal(Path.Combine("C:/vanilla", "sys", "main.dol"), config.VanillaMainDolPath);
        Assert.Equal(Path.Combine("C:/vanilla", "files", "RELS.arc"), config.VanillaRelsArcPath);
        Assert.Equal(Path.Combine("C:/vanilla", "sys", "bi2.bin"), config.VanillaBi2Path);
        Assert.Equal(Path.Combine(_gameMod, "include"), config.IncludePath);
    }

    [Fact]
    public void Load_WithoutVanillaGamePath_FallsBackToGameModVanilla()
    {
        File.WriteAllText(Path.Combine(_gameMod, PatcherConfig.ConfigFileName), """{ "game_path": "C:/out" }""");

        var config = PatcherConfig.LoadFromConfigJson(_gameMod);

        Assert.Equal(Path.Combine(_gameMod, "vanilla", "sys", "main.dol"), config.VanillaMainDolPath);
    }

    [Fact]
    public void Load_MissingConfig_PointsAtExample()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => PatcherConfig.LoadFromConfigJson(_gameMod));
        Assert.Contains(PatcherConfig.ExampleConfigFileName, ex.Message);
    }
}
