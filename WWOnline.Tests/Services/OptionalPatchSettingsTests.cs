using System.Text.Json;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

public class OptionalPatchSettingsTests
{
    [Fact]
    public void StartupOptions_ParsesPatches()
    {
        Assert.Equal("skip_intro,instant_text", StartupOptions.Parse(["--patch", "--patches", "skip_intro,instant_text"]).Patches);
        Assert.Null(StartupOptions.Parse(["--patch"]).Patches);
    }

    [Fact]
    public void GameSettings_WithoutOptionalPatches_MeansDefaults()
    {
        // Settings files written before optional patches existed have no OptionalPatches key: null = use the defaults.
        var old = JsonSerializer.Deserialize<GameSettings>("""{"DolphinPath":"d","GamePath":"g"}""")!;
        Assert.Null(old.OptionalPatches);
        Assert.Null(new GameSettings().OptionalPatches);

        var saved = JsonSerializer.Deserialize<GameSettings>(JsonSerializer.Serialize(new GameSettings { OptionalPatches = [] }))!;
        Assert.NotNull(saved.OptionalPatches); // an explicit empty selection stays "none"
        Assert.Empty(saved.OptionalPatches);
    }
}
