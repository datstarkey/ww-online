using System.Runtime.CompilerServices;
using WWOnline.Patcher.Config;
using Xunit;

namespace WWOnline.Patcher.Tests;

/// <summary>
/// Locates the repository checkout from this source file's compile-time path, so tests that read
/// GameMod/ sources work whatever --artifacts-path the tests were built with.
/// </summary>
internal static class TestRepo
{
    public static string Root { get; } = FindRoot();

    public static string GameMod => Path.Combine(Root, "GameMod");

    /// <summary>A config over the real GameMod sources. GamePath is a throwaway (never written by these tests).</summary>
    public static PatcherConfig Config() => PatcherConfig.Create(Path.Combine(Path.GetTempPath(), "wwo-no-game"), "", GameMod);

    private static string FindRoot([CallerFilePath] string thisFile = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WW-Online.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (WW-Online.sln) not found above " + thisFile);
    }

    /// <summary>The configured extracted vanilla game (GameMod/config.json vanilla_game_path), or null.</summary>
    public static string? VanillaGamePath()
    {
        try
        {
            var path = PatcherConfig.LoadFromConfigJson(GameMod).ResolvedVanillaGamePath;
            return File.Exists(Path.Combine(path, "files", "RELS.arc")) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>A [Fact] reported as skipped when no vanilla game is configured (Nintendo files aren't in the repo).</summary>
internal sealed class VanillaGameFactAttribute : FactAttribute
{
    public VanillaGameFactAttribute()
    {
        if (TestRepo.VanillaGamePath() == null)
            Skip = "Vanilla game not found: set vanilla_game_path in GameMod/config.json";
    }
}
