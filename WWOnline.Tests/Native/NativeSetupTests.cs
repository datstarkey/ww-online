using System.Security.Cryptography;
using System.Text;
using WWOnline.Native;
using WWOnline.Patcher.Pipeline;
using WWOnline.Services;
using WWOnline.Tests.Services;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>
/// The native backend's setup: verifying the disc image, building libtww through the pipeline steps (on a fake
/// process runner: nothing is ever built here), the setup's native steps, Settings' validation and the Tools page.
/// </summary>
public sealed class NativeSetupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-native-setup-" + Guid.NewGuid().ToString("N"));
    private readonly GameSettingsService _settings;
    private readonly FakeProcessRunner _runner = new();
    private readonly NativeBuild _build;

    public NativeSetupTests()
    {
        _settings = new GameSettingsService(Path.Combine(_dir, "settings"));
        var s = _settings.Load();
        s.Backend = GameBackend.Native;
        _settings.Save(s);
        _build = new NativeBuild(_runner, Path.Combine(_dir, "cache"));
        // A build that works: every step succeeds, ninja leaves a libtww in the build folder (the last argument).
        _runner.Handler = (_, args) =>
        {
            if (args[1].Contains("ninja -C"))
                File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(args[^1]).FullName, "libtww.dylib"), [0]);
            return new ProcessResult(0, "ok\n");
        };
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>A file starting with a disc header (<paramref name="id"/>, revision at byte 7).</summary>
    private string Image(string id = "GZLE01", byte revision = 0, int size = 4096)
    {
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes(id, bytes);
        bytes[7] = revision;
        var path = Path.Combine(Directory.CreateDirectory(_dir).FullName, $"{Guid.NewGuid():N}.iso");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static HashSet<string> HashOf(string path) => [Convert.ToHexStringLower(SHA1.HashData(File.ReadAllBytes(path)))];

    /// <summary>A folder that looks like Seaborne's source.</summary>
    private string Source(string name = "seaborne")
    {
        var root = Path.Combine(_dir, name);
        foreach (var f in new[] { "native/CMakeLists.txt", "native/include/pc/tww.h", "native/tools/fetch_recompcore.sh" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, f))!);
            File.WriteAllText(Path.Combine(root, f), "");
        }
        return root;
    }

    private NativeSetupViewModel Vm(NativeSetupViewModel.VerifyDiscFunc? verify = null) =>
        new(_settings, _build, a => a(), verify ?? ((_, _, _) => Task.FromResult(new DiscCheck(true, "verified"))));

    // ── The disc check ───────────────────────────────────────────────

    [Fact]
    public async Task Disc_TheKnownImage_IsAccepted_AndProgressReachesTheEnd()
    {
        var path = Image(size: 10 << 20);
        double last = 0;
        var result = await DiscImageCheck.VerifyAsync(path, new SyncProgress(p => last = p), accepted: HashOf(path));

        Assert.True(result.Ok, result.Message);
        Assert.Equal(1.0, last);
    }

    [Fact]
    public async Task Disc_AnotherGZLE01Image_IsRefusedByItsHash()
    {
        var result = await DiscImageCheck.VerifyAsync(Image());

        Assert.False(result.Ok);
        Assert.Contains("SHA-1", result.Message);
    }

    [Theory]
    [InlineData("GZLP01", 0, "GZLE01")]   // the European disc
    [InlineData("GZLE01", 1, "revision 1")]
    [InlineData("RVZ\u0001\0\0", 0, "convert")] // not a plain .iso
    public async Task Disc_TheWrongDisc_IsRefusedFromItsHeader(string id, byte revision, string reason)
    {
        var path = Image(id, revision);
        var result = await DiscImageCheck.VerifyAsync(path, accepted: HashOf(path));

        Assert.False(result.Ok);
        Assert.Contains(reason, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disc_AMissingOrTinyFile_IsRefused()
    {
        Assert.False((await DiscImageCheck.VerifyAsync(Path.Combine(_dir, "nope.iso"))).Ok);
        var tiny = Path.Combine(Directory.CreateDirectory(_dir).FullName, "tiny.iso");
        File.WriteAllBytes(tiny, "GZLE"u8.ToArray());
        Assert.False((await DiscImageCheck.VerifyAsync(tiny)).Ok);
    }

    // ── The build ────────────────────────────────────────────────────

    [Fact]
    public void Build_Steps_AreTheReadmesInOrder_WithoutTheDisc()
    {
        var names = _build.Steps("/src").Select(s => (s.StepNumber, s.Name));
        Assert.Equal([(1, "Check the build tools"), (2, "Fetch the audio sources"), (3, "Configure"), (4, "Build the game")], names);
    }

    [Fact]
    public void Build_GoesInTheCache_OneFolderPerSource()
    {
        var cache = Path.Combine(_dir, "cache");
        Assert.StartsWith(Path.Combine(cache, "build") + Path.DirectorySeparatorChar, _build.BuildDirectory("/a/seaborne"));
        Assert.Equal(_build.BuildDirectory("/a/seaborne"), _build.BuildDirectory("/a/seaborne"));
        Assert.NotEqual(_build.BuildDirectory("/a/seaborne"), _build.BuildDirectory("/b/seaborne"));
        Assert.Equal(Path.Combine(_build.BuildDirectory("/a/seaborne"), "libtww.dylib"), _build.LibraryPath("/a/seaborne"));
        Assert.Equal(Path.Combine(cache, "recompcore"), _build.RecompCoreDirectory);
        Assert.Equal(Path.Combine(AppPaths.GameCacheDirectory, "recompcore"), new NativeBuild(_runner).RecompCoreDirectory);
    }

    [Fact]
    public async Task Build_RunsEachStepInALoginShell_AndReturnsTheLibrary()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var source = Source();
        var events = new List<PipelineProgressEvent>();
        var progress = new PipelineProgress();
        progress.OnProgress += events.Add;

        var library = await _build.BuildAsync(source, progress);

        Assert.Equal(_build.LibraryPath(source), library);
        Assert.Equal(4, _runner.Calls.Count);
        Assert.All(_runner.Calls, c =>
        {
            Assert.Equal(NativeBuild.Shell, c.Exe);
            Assert.Equal("-lc", c.Args[0]);
            Assert.Equal([source, _build.RecompCoreDirectory, _build.BuildDirectory(source)], c.Args[^3..]);
            Assert.DoesNotContain("gen_assets", c.Args[1]);
        });
        Assert.Contains("native/tools/fetch_recompcore.sh \"$2\"", _runner.Calls[1].Args[1]);
        Assert.Contains("cmake -S native -B \"$3\"", _runner.Calls[2].Args[1]);
        Assert.Contains("-DCOS_RECOMPCORE_DIR=\"$2\"", _runner.Calls[2].Args[1]);
        Assert.Contains("ninja -C \"$3\" tww", _runner.Calls[3].Args[1]);
        Assert.Contains(events, e => e.StepNumber == 4 && e.Status == PipelineStatus.Completed);
    }

    [Fact]
    public async Task Build_AFailingStep_StopsWithItsLastLine()
    {
        if (!OperatingSystem.IsMacOS()) return;
        _runner.Handler = (_, args) => args[1].Contains("cmake -S")
            ? new ProcessResult(1, "-- Configuring\nCMake Error: no compiler\n")
            : new ProcessResult(0, "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _build.BuildAsync(Source(), new PipelineProgress()));

        Assert.Equal("CMake Error: no compiler", ex.Message);
        Assert.Equal(3, _runner.Calls.Count); // nothing after Configure
    }

    [Fact]
    public async Task Toolchain_ReportsWhatIsMissing()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Equal(NativeBuild.NotSupported, (await new NativeBuild(_runner).CheckToolchainAsync()).Error);
            return;
        }
        _runner.Handler = (_, _) => new ProcessResult(1, "ninja is missing (Homebrew: brew install cmake ninja)\n");
        var (error, _) = await new NativeBuild(_runner).CheckToolchainAsync();
        Assert.StartsWith("ninja is missing", error);

        _runner.Handler = (_, _) => new ProcessResult(0, "Xcode command line tools, cmake, ninja and git found\n");
        Assert.Null((await new NativeBuild(_runner).CheckToolchainAsync()).Error);
    }

    [Fact]
    public void Source_MustBeSeaborne()
    {
        Assert.Null(NativeBuild.CheckSource(Source()));
        Assert.NotNull(NativeBuild.CheckSource(Directory.CreateDirectory(Path.Combine(_dir, "empty")).FullName));
        Assert.NotNull(NativeBuild.CheckSource(""));
    }

    // ── The view-model ───────────────────────────────────────────────

    [Fact]
    public async Task Vm_AVerifiedDisc_IsSaved_AndARefusedOneIsNot()
    {
        var good = Image();
        using var vm = Vm((path, _, _) => Task.FromResult(new DiscCheck(path == good, path == good ? "verified" : "wrong disc")));

        vm.DiscImagePath = Image();
        await vm.VerifyDiscCommand.ExecuteAsync(null);
        Assert.False(vm.DiscOk);
        Assert.Equal("wrong disc", vm.DiscStatus);
        Assert.Equal("", _settings.Load().DiscImagePath);

        vm.DiscImagePath = good;
        await vm.VerifyDiscCommand.ExecuteAsync(null);
        Assert.True(vm.DiscOk);
        Assert.Equal(good, _settings.Load().DiscImagePath);

        vm.DiscImagePath = good + "x"; // an edit needs a new check
        Assert.False(vm.DiscOk);
    }

    [Fact]
    public async Task Vm_Build_NeedsASourceButNoDisc_ThenSavesTheLibrary()
    {
        if (!OperatingSystem.IsMacOS()) return;
        using var vm = Vm();
        vm.SourcePath = Source("my-seaborne");
        Assert.True(vm.CanBuild); // the disc is read when the game starts, not by the build

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.True(vm.BuildSucceeded);
        Assert.True(vm.IsGameReady);
        var saved = _settings.Load();
        Assert.Equal(vm.SourcePath, saved.NativeSourcePath);
        Assert.Equal(_build.LibraryPath(vm.SourcePath), saved.NativeLibraryPath);
    }

    [Fact]
    public async Task Vm_TheBundledSource_IsTheDefault_AndIsNotSaved()
    {
        if (!OperatingSystem.IsMacOS()) return;
        Assert.NotNull(NativePaths.BundledSource); // the repository's seaborne/, above the test's bin folder
        using var vm = Vm();
        Assert.Equal(NativePaths.BundledSource, vm.SourcePath);
        Assert.Null(vm.SourceError);

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.True(vm.BuildSucceeded);
        Assert.Equal("", _settings.Load().NativeSourcePath);
    }

    [Fact]
    public void BundledSource_IsTheFirstSeaborneFolderAtOrAboveTheApp()
    {
        var app = Directory.CreateDirectory(Path.Combine(_dir, "app", "bin")).FullName;
        Directory.CreateDirectory(Path.Combine(_dir, "app", "seaborne")); // not a source: no native/CMakeLists.txt
        Assert.Null(NativePaths.FindBundledSource(app));

        var source = Source();
        Assert.Equal(source, NativePaths.FindBundledSource(app));
        Assert.Equal(source, NativePaths.FindBundledSource(_dir));
    }

    [Fact]
    public async Task Vm_AFailedBuild_SaysWhichStep()
    {
        if (!OperatingSystem.IsMacOS()) return;
        _runner.Handler = (_, args) => args[1].Contains("fetch_recompcore") ? new ProcessResult(128, "fatal: unable to access github.com\n") : new ProcessResult(0, "");
        using var vm = Vm();
        vm.SourcePath = Source();

        await vm.BuildCommand.ExecuteAsync(null);

        Assert.False(vm.IsGameReady);
        Assert.Equal("Step 2 of 4: Fetch the audio sources failed: fatal: unable to access github.com", vm.BuildError);
        Assert.Equal("", _settings.Load().NativeLibraryPath);
    }

    [Fact]
    public void Vm_ABadSource_IsFlagged()
    {
        using var vm = Vm();
        vm.SourcePath = Directory.CreateDirectory(Path.Combine(_dir, "not-it")).FullName;
        Assert.NotNull(vm.SourceError);
        Assert.False(vm.CanBuild);
    }

    // ── The setup's native steps ─────────────────────────────────────

    private SetupWizardViewModel Wizard(NativeSetupViewModel native)
    {
        var catalog = new OptionalPatchCatalogService(_settings, new WWOnline.Patcher.Patches.OptionalPatchCatalog([]));
        var patchOptions = new PatchOptionsViewModel(catalog, _settings, null, a => a(), TimeSpan.Zero);
        return new SetupWizardViewModel(_settings, (_, _, _) => throw new InvalidOperationException("never patches natively"),
            patchOptions, new AppearanceViewModel(_settings, _ => { }), () => [], new DolphinTool(_runner), a => a(), native);
    }

    [Fact]
    public async Task Setup_Native_IsDiscYouBuildPlay_WithoutDolphin()
    {
        using var native = Vm();
        using var vm = Wizard(native);
        _runner.Handler = (_, _) => new ProcessResult(0, "tools found\n");

        Assert.True(vm.IsNative);
        Assert.Equal([SetupStep.Welcome, SetupStep.Game, SetupStep.Player, SetupStep.Patch, SetupStep.Done], vm.Steps.Select(s => s.Step));
        Assert.Equal([1, 2, 3, 4, 5], vm.Steps.Select(s => s.Number));

        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Game, vm.Step);
        Assert.True(vm.ShowNativeGame);
        Assert.False(vm.ShowDolphinGame);
        Assert.Equal("Step 2 of 5", vm.StepCaption);
        Assert.False(vm.CanGoNext); // the disc first

        native.DiscImagePath = Image();
        await native.VerifyDiscCommand.ExecuteAsync(null);
        Assert.True(vm.CanGoNext);
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Player, vm.Step);

        vm.PlayerName = "Tetra";
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Patch, vm.Step);
        Assert.True(vm.ShowNativeBuild);
        await vm.ToolDetection;
        Assert.Equal(OperatingSystem.IsMacOS() ? "tools found" : NativeBuild.NotSupported, native.ToolchainStatus);
        Assert.False(vm.CanGoNext); // not built
        Assert.True(vm.CanSkipStep);
        Assert.Equal("Build later", vm.SkipStepText);

        vm.SkipStepCommand.Execute(null);
        Assert.Equal(SetupStep.Done, vm.Step);
        Assert.True(vm.ShowNotBuiltWarning);
        Assert.False(vm.ShowNotPatchedWarning);

        vm.NextCommand.Execute(null); // Finish
        Assert.True(_settings.Load().SetupCompleted);
    }

    [Fact]
    public void Setup_Native_BackGoesThroughTheNativeSteps()
    {
        using var native = Vm();
        using var vm = Wizard(native);
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Game, vm.Step);
        vm.BackCommand.Execute(null);
        Assert.Equal(SetupStep.Welcome, vm.Step);
    }

    // ── Settings, first run, Tools ───────────────────────────────────

    [Fact]
    public void Settings_Native_NeedTheDiscAndTheBuiltGame_NotDolphin()
    {
        var s = _settings.Load();
        Assert.False(_settings.IsConfigured(s));

        s.DiscImagePath = Image();
        Assert.Equal("The game isn't built yet", _settings.ValidatePaths(s));
        s.NativeLibraryPath = Path.Combine(_dir, "libtww.dylib");
        File.WriteAllBytes(s.NativeLibraryPath, [0]);
        Assert.True(_settings.IsConfigured(s));
    }

    [Fact]
    public void FirstRun_Native_ADiscAndABuiltGame_AreAWorkingSetup()
    {
        var s = _settings.Load();
        s.DiscImagePath = Image();
        s.NativeLibraryPath = Path.Combine(_dir, "libtww.dylib");
        File.WriteAllBytes(s.NativeLibraryPath, [0]);
        _settings.Save(s);

        Assert.Equal(FirstRun.Decision.MigratedAsCompleted, FirstRun.Resolve(_settings, new StartupOptions(null, null, null, false, false, null)));
    }

    [Fact]
    public void Tools_MemoryPeekAndPoke_AreOffOnTheNativeGame()
    {
        var native = new DebugToolsViewModel(new NativeGameService(new FakeNativeGame()), _settings);
        Assert.False(native.IsMemoryAvailable);
        Assert.False(native.ReadMemoryCommand.CanExecute(null));
        Assert.False(native.WriteMemoryCommand.CanExecute(null));

        var dolphin = new DebugToolsViewModel(new FakeDolphin(), _settings);
        Assert.True(dolphin.IsMemoryAvailable);
        Assert.True(dolphin.ReadMemoryCommand.CanExecute(null));
    }

    /// <summary>Reports on the caller's thread (Progress&lt;T&gt; posts to the thread pool).</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
