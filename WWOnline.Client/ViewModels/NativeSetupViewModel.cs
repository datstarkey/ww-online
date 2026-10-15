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
    private static readonly ILogger Logger = Log.ForContext<NativeSetupViewModel>();

    public delegate Task<DiscCheck> VerifyDiscFunc(string path, IProgress<double>? progress, CancellationToken ct);

    private readonly GameSettingsService _settings;
    private readonly NativeBuild _build;
    private readonly VerifyDiscFunc _verifyDisc;
    private readonly Action<Action> _post;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _buildCts;
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
    private bool _isVerifyingDisc;

    /// <summary>The share of the image hashed, 0..1.</summary>
    [ObservableProperty] private double _discProgress;

    // ── Building the game ─────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild))]
    private string _sourcePath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild))]
    private string? _sourceError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameReady))]
    private string _libraryPath = "";

    /// <summary>The build tools found, or what is missing.</summary>
    [ObservableProperty] private string? _toolchainStatus;
    [ObservableProperty] private bool _toolchainOk;
    [ObservableProperty] private bool _isCheckingTools;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuild), nameof(IsGameReady))]
    private bool _isBuilding;

    /// <summary>"Step 3 of 5: Read your disc", then the step's latest output line.</summary>
    [ObservableProperty] private string _buildStep = "";
    [ObservableProperty] private string _buildProgress = "";
    [ObservableProperty] private string? _buildError;
    [ObservableProperty] private bool _buildSucceeded;

    /// <summary>The build reads no disc: only a source is needed.</summary>
    public bool CanBuild => !IsBuilding && SourceError == null && !string.IsNullOrWhiteSpace(SourcePath);

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
        DiscCheck result;
        try
        {
            var progress = new Progress<double>(p => _post(() => DiscProgress = p));
            result = await _verifyDisc(path, progress, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
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
        _buildCts?.Dispose();
        var cts = _buildCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var steps = _build.Steps(source).Count;
        var progress = new PipelineProgress();
        progress.OnProgress += e => _post(() =>
        {
            BuildStep = $"Step {e.StepNumber} of {steps}: {e.StepName}";
            BuildProgress = e.Message;
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
        GC.SuppressFinalize(this);
    }
}
