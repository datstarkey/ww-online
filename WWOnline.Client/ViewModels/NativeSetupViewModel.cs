using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Native;
using WWOnline.Patcher.Pipeline;
using WWOnline.Services;

namespace WWOnline.ViewModels;

/// <summary>
/// The native backend's setup, on the first-run setup's steps and in Settings: the player's disc image
/// (verified by <see cref="DiscImageCheck"/> before it is saved as <see cref="GameSettings.DiscImagePath"/>), Seaborne's
/// source (the bundled one unless another is chosen) and building libtww from it (<see cref="NativeBuild"/>, saved as
/// <see cref="GameSettings.NativeLibraryPath"/>), or a libtww chosen by hand.
/// </summary>
public partial class NativeSetupViewModel : ViewModelBase, IDisposable
{
    // ninja's "[done/total] ..." progress lines.
    [GeneratedRegex(@"^\[(\d+)/(\d+)\]")]
    private static partial Regex NinjaProgress();

    private static readonly ILogger Logger = Log.ForContext<NativeSetupViewModel>();

    public delegate Task<DiscCheck> VerifyDiscFunc(string path, IProgress<double>? progress, CancellationToken ct);

    private readonly GameSettingsService _settings;
    private readonly NativeBuild _build;
    private readonly VerifyDiscFunc _verifyDisc;
    private readonly Action<Action> _post;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _buildCts;
    private CancellationTokenSource? _discCts;
    private bool _loading;

    /// <summary>Set by the view: pickers (null result = cancelled).</summary>
    public Func<Task<string?>>? BrowseForDiscImageAsync { get; set; }
    public Func<Task<string?>>? BrowseForSourceFolderAsync { get; set; }
    public Func<Task<string?>>? BrowseForLibraryAsync { get; set; }

    // ── Disc ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyDiscCommand))]
    private string _discImagePath = "";

    /// <summary>The disc check's verdict ("GZLE01 revision 0, verified." or what is wrong).</summary>
    [ObservableProperty] private string? _discStatus;

    [ObservableProperty] private bool _discOk;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyDiscCommand))]
    [NotifyPropertyChangedFor(nameof(ShowDiscProblem))]
    private bool _isVerifyingDisc;

    /// <summary>The share of the image hashed, 0..1.</summary>
    [ObservableProperty] private double _discProgress;

    /// <summary>A real problem with the disc (not the check in progress).</summary>
    public bool ShowDiscProblem => !DiscOk && !IsVerifyingDisc && !string.IsNullOrEmpty(DiscStatus);

    partial void OnDiscOkChanged(bool value) => OnPropertyChanged(nameof(ShowDiscProblem));
    partial void OnDiscStatusChanged(string? value) => OnPropertyChanged(nameof(ShowDiscProblem));

    // ── Building the game ─────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild))]
    private string _sourcePath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild))]
    private string? _sourceError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameReady), nameof(BuildButtonText))]
    private string _libraryPath = "";

    /// <summary>The build tools found, or what is missing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild), nameof(ToolchainMissing), nameof(NeedsCommandLineTools))]
    private string? _toolchainStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild), nameof(ToolchainMissing), nameof(NeedsCommandLineTools))]
    private bool _toolchainOk;

    [ObservableProperty] private bool _isCheckingTools;

    /// <summary>The tool check ran and found something missing: Build stays off until it passes.</summary>
    public bool ToolchainMissing => ToolchainStatus != null && !ToolchainOk;

    /// <summary>Xcode's command line tools are what is missing: offer their installer.</summary>
    public bool NeedsCommandLineTools => ToolchainMissing && ToolchainStatus!.Contains("xcode-select", StringComparison.Ordinal);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild), nameof(IsGameReady), nameof(BuildButtonText), nameof(HasBuildLog))]
    private bool _isBuilding;

    /// <summary>"Step 3 of 5: Read your disc", then the step's latest output line.</summary>
    [ObservableProperty] private string _buildStep = "";
    [ObservableProperty] private string _buildProgress = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBuildLog))]
    private string? _buildError;
    [ObservableProperty] private bool _buildSucceeded;

    /// <summary>ninja's share done, 0..1, or -1 before it starts compiling (the bar is indeterminate).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuildPercentKnown))]
    private double _buildPercent = -1;

    public bool BuildPercentKnown => BuildPercent >= 0;

    /// <summary>"3:12 elapsed · usually 5-10 minutes the first time".</summary>
    [ObservableProperty] private string _buildElapsed = "";
    private readonly Stopwatch _buildClock = new();

    /// <summary>The build reads no disc: only a source is needed (and the tools, once a check has found them missing).</summary>
    public bool CanBuild => !IsBuilding && SourceError == null && !string.IsNullOrWhiteSpace(SourcePath) && !ToolchainMissing;

    /// <summary>"Build the game", or "Rebuild" once there is one.</summary>
    public string BuildButtonText => IsGameReady ? "Rebuild" : "Build the game";

    /// <summary>A log from the last build exists (Show log).</summary>
    public bool HasBuildLog => !IsBuilding && File.Exists(_build.LogPath);

    /// <summary>A built (or chosen) libtww is there: the game can start.</summary>
    public bool IsGameReady => !IsBuilding && !string.IsNullOrWhiteSpace(LibraryPath) && File.Exists(LibraryPath);

    public NativeSetupViewModel(GameSettingsService settings)
        : this(settings, new NativeBuild(new ProcessRunner()), a => Avalonia.Threading.Dispatcher.UIThread.Post(a))
    {
    }

    /// <param name="build">Builds libtww (a fake process runner in tests).</param>
    /// <param name="post">Runs an action on the UI thread (tests pass <c>a =&gt; a()</c>).</param>
    /// <param name="verifyDisc">Checks a disc image (<see cref="DiscImageCheck.VerifyAsync"/> by default).</param>
    public NativeSetupViewModel(GameSettingsService settings, NativeBuild build, Action<Action> post, VerifyDiscFunc? verifyDisc = null)
    {
        _settings = settings;
        _build = build;
        _post = post;
        _verifyDisc = verifyDisc ?? ((path, progress, ct) => DiscImageCheck.VerifyAsync(path, progress, ct));
        Load();
    }

    /// <summary>Show what is saved (a disc is only ever saved once verified).</summary>
    public void Load()
    {
        var s = _settings.Load();
        _loading = true;
        DiscImagePath = s.DiscImagePath;
        DiscOk = !string.IsNullOrWhiteSpace(s.DiscImagePath) && File.Exists(s.DiscImagePath);
        DiscStatus = DiscOk ? "Verified." : null;
        SourcePath = NativePaths.Source(s) ?? "";
        LibraryPath = s.NativeLibraryPath;
        _loading = false;
        SourceError = string.IsNullOrWhiteSpace(SourcePath) ? null : NativeBuild.CheckSource(SourcePath);
        BuildError = null;
        BuildSucceeded = false;
    }

    partial void OnDiscImagePathChanged(string value)
    {
        if (_loading) return;
        DiscOk = false;
        DiscStatus = null;
    }

    partial void OnSourcePathChanged(string value)
    {
        if (!_loading) SourceError = string.IsNullOrWhiteSpace(value) ? null : NativeBuild.CheckSource(value);
    }

    [RelayCommand]
    private async Task BrowseDisc()
    {
        if (BrowseForDiscImageAsync == null) return;
        if (await BrowseForDiscImageAsync() is not { } picked) return;
        DiscImagePath = picked;
        await VerifyDisc();
    }

    private bool CanVerifyDisc() => !IsVerifyingDisc && !string.IsNullOrWhiteSpace(DiscImagePath);

    /// <summary>Check the disc image; saved only once it checks out.</summary>
    [RelayCommand(CanExecute = nameof(CanVerifyDisc))]
    private async Task VerifyDisc()
    {
        var path = DiscImagePath.Trim();
        IsVerifyingDisc = true;
        DiscOk = false;
        DiscStatus = "Checking your disc image…";
        DiscProgress = 0;
        _discCts?.Dispose();
        var cts = _discCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        DiscCheck result;
        try
        {
            var progress = new Progress<double>(p => _post(() => DiscProgress = p));
            result = await _verifyDisc(path, progress, cts.Token);
        }
        catch (OperationCanceledException)
        {
            IsVerifyingDisc = false;
            DiscStatus = _lifetime.IsCancellationRequested ? null : "Check cancelled. Verify the disc to use it.";
            return;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[native-setup] checking the disc failed");
            result = new DiscCheck(false, ex.Message);
        }
        finally
        {
            IsVerifyingDisc = false;
        }
        Logger.Information("[native-setup] disc {Path}: {Ok} {Message}", path, result.Ok, result.Message);
        DiscOk = result.Ok;
        DiscStatus = result.Message;
        if (result.Ok)
            Save(s => s.DiscImagePath = path);
    }

    [RelayCommand]
    private void CancelVerifyDisc() => _discCts?.Cancel();

    /// <summary>Run Xcode's command line tools installer (macOS asks the player), then check again.</summary>
    [RelayCommand]
    private async Task InstallCommandLineTools()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("xcode-select", "--install") { UseShellExecute = false });
            if (p != null) await p.WaitForExitAsync(_lifetime.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Warning(ex, "[native-setup] xcode-select --install failed");
        }
        ToolchainStatus = "Finish the installer macOS opened, then check the build tools again.";
    }

    /// <summary>Open the last build's full log (TextEdit / Console).</summary>
    [RelayCommand]
    private void ShowBuildLog()
    {
        if (!File.Exists(_build.LogPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_build.LogPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[native-setup] opening the build log failed");
        }
    }

    [RelayCommand]
    private async Task BrowseSource()
    {
        if (BrowseForSourceFolderAsync == null) return;
        if (await BrowseForSourceFolderAsync() is { } picked) SourcePath = picked;
    }

    [RelayCommand]
    private async Task BrowseLibrary()
    {
        if (BrowseForLibraryAsync == null) return;
        if (await BrowseForLibraryAsync() is not { } picked || !File.Exists(picked)) return;
        LibraryPath = picked;
        Save(s => s.NativeLibraryPath = picked);
    }

    /// <summary>Look for the build tools (on entering the build step, and on demand).</summary>
    [RelayCommand]
    public async Task CheckTools()
    {
        IsCheckingTools = true;
        try
        {
            var (error, found) = await _build.CheckToolchainAsync(_lifetime.Token);
            ToolchainOk = error == null;
            ToolchainStatus = error ?? found;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[native-setup] checking the build tools failed");
            ToolchainOk = false;
            ToolchainStatus = ex.Message;
        }
        finally
        {
            IsCheckingTools = false;
        }
    }

    /// <summary>Build libtww from the source, then save where it is (and the source, unless it is the bundled one).</summary>
    [RelayCommand]
    private async Task Build()
    {
        if (!CanBuild) return;
        var source = SourcePath.Trim();
        Save(s => s.NativeSourcePath = source == NativePaths.BundledSource ? "" : source);
        IsBuilding = true;
        BuildSucceeded = false;
        BuildError = null;
        BuildStep = "Starting…";
        BuildProgress = "";
        BuildPercent = -1;
        BuildElapsed = "";
        _buildClock.Restart();
        _buildCts?.Dispose();
        var cts = _buildCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var steps = _build.Steps(source).Count;
        var progress = new PipelineProgress();
        progress.OnProgress += e => _post(() =>
        {
            BuildStep = $"Step {e.StepNumber} of {steps}: {e.StepName}";
            BuildProgress = e.Message;
            if (NinjaProgress().Match(e.Message) is { Success: true } m &&
                double.TryParse(m.Groups[2].Value, out var total) && total > 0)
                BuildPercent = double.Parse(m.Groups[1].Value) / total;
            BuildElapsed = $"{_buildClock.Elapsed:m\\:ss} elapsed · usually 5-15 minutes the first time, less after";
        });
        try
        {
            var library = await _build.BuildAsync(source, progress, cts.Token);
            LibraryPath = library;
            Save(s => s.NativeLibraryPath = library);
            BuildSucceeded = true;
            Logger.Information("[native-setup] built {Library}", library);
        }
        catch (OperationCanceledException)
        {
            BuildError = "Build cancelled.";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[native-setup] the build failed at {Step}", BuildStep);
            BuildError = $"{BuildStep} failed: {ex.Message}";
        }
        finally
        {
            _buildClock.Stop();
            IsBuilding = false;
        }
    }

    [RelayCommand]
    private void CancelBuild() => _buildCts?.Cancel();

    private void Save(Action<GameSettings> change)
    {
        var s = _settings.Load();
        change(s);
        _settings.Save(s);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _buildCts?.Dispose();
        _discCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
