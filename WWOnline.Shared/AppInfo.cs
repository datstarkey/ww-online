using System.Reflection;

namespace WWOnline.Shared;

/// <summary>
/// The running app's identity, read from assembly attributes generated from Directory.Build.props
/// (Version, WwoRepositoryUrl). Release builds stamp the version from the git tag, so this is the
/// only place code should get "which WW-Online is this" from. Client and server both reference
/// this assembly, which is built with the same props.
/// </summary>
public static class AppInfo
{
    public const string DisplayName = "WW-Online";

    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    /// <summary>SemVer app version without build metadata, e.g. "1.2.3" or "1.3.0-beta.1".</summary>
    public static string Version { get; } = StripBuildMetadata(InformationalVersionRaw());

    /// <summary>The git commit the build came from (SourceLink build metadata), if known.</summary>
    public static string? Commit { get; } = BuildMetadata(InformationalVersionRaw());

    /// <summary>True for a pre-release version ("1.3.0-beta.1").</summary>
    public static bool IsPrerelease => Version.Contains('-');

    /// <summary>
    /// The GitHub repo that releases (and updates) come from: WwoRepositoryUrl in
    /// Directory.Build.props (the only place it is written), overridden by the release workflow.
    /// </summary>
    public static string RepositoryUrl { get; } =
        Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "WwoRepositoryUrl")?.Value
        ?? "";

    /// <summary>"WW-Online 1.2.3" for logs and titles.</summary>
    public static string DisplayVersion => $"{DisplayName} {Version}";

    private static string InformationalVersionRaw() =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    internal static string StripBuildMetadata(string version)
    {
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    internal static string? BuildMetadata(string version)
    {
        var plus = version.IndexOf('+');
        return plus >= 0 && plus + 1 < version.Length ? version[(plus + 1)..] : null;
    }
}
