using System.Reflection;
using WWOnline.Shared;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>AppInfo reads the version and repo URL that Directory.Build.props stamps into every assembly.</summary>
public class AppInfoTests
{
    [Fact]
    public void Version_IsSemVer_WithoutBuildMetadata()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", AppInfo.Version);
        Assert.Equal(AppInfo.Version.Contains('-'), AppInfo.IsPrerelease);
    }

    [Fact]
    public void Version_MatchesTheAssemblyVersion()
    {
        var assemblyVersion = typeof(AppInfo).Assembly.GetName().Version!;
        Assert.StartsWith($"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}", AppInfo.Version);
    }

    [Fact]
    public void RepositoryUrl_ComesFromDirectoryBuildProps()
    {
        var stamped = typeof(AppInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "WwoRepositoryUrl").Value;
        Assert.Equal(stamped, AppInfo.RepositoryUrl);
        Assert.StartsWith("https://github.com/", AppInfo.RepositoryUrl);
    }

    [Fact]
    public void ClientAndServer_ShareTheVersion()
    {
        string Info(Type t) => t.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.Equal(Info(typeof(AppInfo)), Info(typeof(WWOnline.Server.Hubs.GameHub)));
        Assert.Equal(Info(typeof(AppInfo)), Info(typeof(WWOnline.Services.UpdateService)));
    }
}
