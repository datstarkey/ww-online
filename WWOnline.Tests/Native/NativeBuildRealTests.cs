using WWOnline.Native;
using WWOnline.Patcher.Pipeline;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>
/// The setup's build for real, opt-in because it fetches sources and compiles for minutes:
/// <c>WWO_REAL_NATIVE_BUILD=&lt;cache dir&gt; dotnet test --filter NativeBuildRealTests</c> builds libtww from the
/// bundled seaborne/ through <see cref="NativeBuild"/>'s own steps on a real process runner, headless. Each step's
/// output goes to <c>&lt;cache dir&gt;/build.log</c>.
/// </summary>
public sealed class NativeBuildRealTests
{
    [Fact]
    public async Task TheBundledSource_BuildsLibtww_IntoTheCacheOnly()
    {
        var cache = Environment.GetEnvironmentVariable("WWO_REAL_NATIVE_BUILD");
        if (string.IsNullOrWhiteSpace(cache) || !OperatingSystem.IsMacOS()) return;
        var source = NativePaths.BundledSource;
        Assert.NotNull(source);
        var log = Path.Combine(Directory.CreateDirectory(cache).FullName, "build.log");
        var progress = new PipelineProgress();
        progress.OnProgress += e => File.AppendAllText(log, $"[{e.StepNumber} {e.StepName}] {e.Status} {e.Message}\n");

        var library = await new NativeBuild(new ProcessRunner(), cache).BuildAsync(source, progress);

        Assert.StartsWith(Path.GetFullPath(cache), library);
        Assert.True(new FileInfo(library).Length > 0);
    }
}
