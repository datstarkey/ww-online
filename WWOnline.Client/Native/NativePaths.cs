using System.Runtime.InteropServices;
using WWOnline.Services;

namespace WWOnline.Native;

/// <summary>Which game the client runs: Dolphin (Windows, memory polling) or the embedded native game.</summary>
public enum GameBackend
{
    Dolphin,
    Native,
}

/// <summary>
/// Where the native backend's files are: libtww, the disc image, Seaborne's source and the game's own user
/// folder. An environment variable wins over the saved setting. The setup's local build (NativeBuild) fills
/// <see cref="GameSettings.NativeLibraryPath"/> with the library it built; it can also be set by hand or through
/// <see cref="LibraryVariable"/>.
/// </summary>
public static class NativePaths
{
    public const string LibraryVariable = "WWO_LIBTWW";
    public const string DiscVariable = "WWO_DISC";
    public const string SourceVariable = "WWO_TWW_SOURCE";

    /// <summary>The default backend: native on macOS (Dolphin clients exist on Windows only), else Dolphin.</summary>
    public static GameBackend DefaultBackend(bool macOS) => macOS ? GameBackend.Native : GameBackend.Dolphin;

    public static string? Library(GameSettings settings) =>
        Library(settings, Environment.GetEnvironmentVariable(LibraryVariable));

    public static string? Library(GameSettings settings, string? variable) => Pick(variable, settings.NativeLibraryPath);

    public static string? Disc(GameSettings settings) =>
        Disc(settings, Environment.GetEnvironmentVariable(DiscVariable));

    public static string? Disc(GameSettings settings, string? variable) => Pick(variable, settings.DiscImagePath);

    /// <summary>The Seaborne source the setup builds libtww from: the variable, the saved choice, else <see cref="BundledSource"/>.</summary>
    public static string? Source(GameSettings settings) =>
        Source(settings, Environment.GetEnvironmentVariable(SourceVariable));

    public static string? Source(GameSettings settings, string? variable) => Pick(variable, settings.NativeSourcePath) ?? BundledSource;

    /// <summary>The Seaborne source that comes with this client (<see cref="FindBundledSource"/>), or null.</summary>
    public static string? BundledSource { get; } = FindBundledSource(AppContext.BaseDirectory);

    /// <summary>
    /// The first seaborne/ folder holding Seaborne's source at or above <paramref name="from"/>: the copy published
    /// next to the macOS app, or the repository's own above a dev build's bin folder.
    /// </summary>
    public static string? FindBundledSource(string from)
    {
        for (var dir = new DirectoryInfo(from); dir != null; dir = dir.Parent)
        {
            var source = Path.Combine(dir.FullName, "seaborne");
            if (File.Exists(Path.Combine(source, "native", "CMakeLists.txt")))
                return source;
        }
        return null;
    }

    /// <summary>The client's <see cref="Shared.Models.JoinRequest.Platform"/>: "dolphin-win", "native-osx-arm64", ...</summary>
    public static string PlatformName(GameBackend backend)
    {
        if (backend == GameBackend.Dolphin) return "dolphin-win";
        string os = OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win" : "linux";
        return $"native-{os}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";
    }

    /// <summary>The game's saves, settings and caches (COS_USER_DIR).</summary>
    public static string UserDirectory => Path.Combine(AppPaths.AppDataDirectory, "native");

    private static string? Pick(string? variable, string setting) =>
        !string.IsNullOrWhiteSpace(variable) ? variable.Trim()
        : !string.IsNullOrWhiteSpace(setting) ? setting.Trim()
        : null;
}
