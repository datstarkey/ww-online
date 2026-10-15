using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>Choosing the backend, finding libtww and the disc, and saying which platform joins.</summary>
public sealed class GameBackendTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-backend-" + Guid.NewGuid().ToString("N"));

    public GameBackendTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void DefaultBackend_IsNativeOnMacOS_DolphinElsewhere()
    {
        Assert.Equal(GameBackend.Native, NativePaths.DefaultBackend(macOS: true));
        Assert.Equal(GameBackend.Dolphin, NativePaths.DefaultBackend(macOS: false));
        Assert.Equal(NativePaths.DefaultBackend(OperatingSystem.IsMacOS()), new GameSettings().Backend);
    }

    [Fact]
    public void SettingsFromBeforeTheBackend_GetThisPlatformsDefault()
    {
        File.WriteAllText(Path.Combine(_dir, "game-settings.json"), """{ "DolphinPath": "C:\\Dolphin\\Dolphin.exe" }""");
        Assert.Equal(NativePaths.DefaultBackend(OperatingSystem.IsMacOS()), new GameSettingsService(_dir).Load().Backend);
    }

    [Theory]
    [InlineData(GameBackend.Dolphin)]
    [InlineData(GameBackend.Native)]
    public void Backend_IsSavedByName_AndLoadsBack(GameBackend backend)
    {
        var settings = new GameSettingsService(_dir);
        settings.Save(new GameSettings { Backend = backend });

        Assert.Contains($"\"{backend}\"", File.ReadAllText(Path.Combine(_dir, "game-settings.json")));
        Assert.Equal(backend, settings.Load().Backend);
    }

    [Fact]
    public void NativeBackend_ServesIDolphinServiceFromTheEmbeddedGame()
    {
        var services = new ServiceCollection();
        App.AddGameBackend(services, GameBackend.Native);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<NativeGameService>(provider.GetRequiredService<IDolphinService>());
        Assert.Same(provider.GetRequiredService<GameHost>(), provider.GetRequiredService<INativeGame>());
    }

    [Fact]
    public void DolphinBackend_HasNoEmbeddedGame()
    {
        var services = new ServiceCollection();
        App.AddGameBackend(services, GameBackend.Dolphin);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DolphinService>(provider.GetRequiredService<IDolphinService>());
        Assert.Null(provider.GetService<INativeGame>());
        Assert.Null(provider.GetService<GameHost>());
    }

    [Theory]
    [InlineData("/env/libtww.dylib", "/set/libtww.dylib", "/env/libtww.dylib")]
    [InlineData("  ", "/set/libtww.dylib", "/set/libtww.dylib")]
    [InlineData(null, "", null)]
    public void Library_EnvironmentVariableWinsOverTheSetting(string? variable, string setting, string? expected)
    {
        Assert.Equal(expected, NativePaths.Library(new GameSettings { NativeLibraryPath = setting }, variable));
    }

    [Fact]
    public void Disc_EnvironmentVariableWinsOverTheSetting()
    {
        Assert.Equal("/env.iso", NativePaths.Disc(new GameSettings { DiscImagePath = "/set.iso" }, "/env.iso"));
        Assert.Equal("/set.iso", NativePaths.Disc(new GameSettings { DiscImagePath = "/set.iso" }, null));
    }

    [Fact]
    public void EvaluateNative_NeedsAnExistingLibraryAndDisc()
    {
        var lib = Path.Combine(_dir, "libtww.dylib");
        var disc = Path.Combine(_dir, "GZLE01.iso");

        Assert.Contains(NativePaths.LibraryVariable, LaunchReadiness.EvaluateNative(null, disc).Problem);
        Assert.Contains("not found", LaunchReadiness.EvaluateNative(lib, disc).Problem);
        File.WriteAllText(lib, "");
        Assert.Contains(NativePaths.DiscVariable, LaunchReadiness.EvaluateNative(lib, null).Problem);
        Assert.Contains("not found", LaunchReadiness.EvaluateNative(lib, disc).Problem);
        File.WriteAllText(disc, "");
        Assert.True(LaunchReadiness.EvaluateNative(lib, disc).CanLaunch);
    }

    [Fact]
    public void PlatformName_NamesTheBackendOsAndArchitecture()
    {
        Assert.Equal("dolphin-win", NativePaths.PlatformName(GameBackend.Dolphin));
        var native = NativePaths.PlatformName(GameBackend.Native);
        Assert.StartsWith(OperatingSystem.IsMacOS() ? "native-osx-" : "native-", native);
        Assert.Equal(native, Shared.Hubs.ProtocolCheck.SanitizeVersion(native)); // logged as sent
    }

    [Fact]
    public void JoinRequest_Platform_IsAdditive()
    {
        var old = JsonSerializer.Deserialize<JoinRequest>("""{"PlayerName":"a","ProtocolVersion":12,"AppVersion":"0.7.0"}""")!;
        Assert.Equal("", old.Platform);
        var json = JsonSerializer.Serialize(new JoinRequest { Platform = "native-osx-arm64" });
        Assert.Equal("native-osx-arm64", JsonSerializer.Deserialize<JoinRequest>(json)!.Platform);
    }
}
