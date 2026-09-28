using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Patcher.Patches;
using WWOnline.Services;

namespace WWOnline.ViewModels;

/// <summary>One optional patch in the Settings checklist.</summary>
public partial class PatchOptionItemViewModel : ObservableObject
{
    public PatchOptionItemViewModel(OptionalPatch patch, bool isSelected)
    {
        Patch = patch;
        _isSelected = isSelected;
    }

    public OptionalPatch Patch { get; }
    public string Id => Patch.Id;
    public string Name => Patch.Name;
    public string Description => Patch.Description;
    public string Category => Patch.Category;
    public bool IsDefault => Patch.DefaultEnabled;

    /// <summary>True when every player in a room should pick the same value (show a badge).</summary>
    public bool AllPlayersShouldMatch => Patch.AllPlayersShouldMatch;

    public string MultiplayerNote => Patch.MultiplayerNote;
    public bool HasMultiplayerNote => !string.IsNullOrWhiteSpace(Patch.MultiplayerNote);

    /// <summary>A multiplayer note without the "all players should match" chip: shown as an info icon.</summary>
    public bool ShowNoteIcon => HasMultiplayerNote && !AllPlayersShouldMatch;

    /// <summary>Tooltip for the "all players should match" chip.</summary>
    public string MatchTip => HasMultiplayerNote ? MultiplayerNote : "Every player in a room should make the same choice.";

    public string Credit => Patch.Credit;

    /// <summary>Names of the patches this one can't be combined with ("" when none).</summary>
    public string ConflictsText { get; internal set; } = "";

    public bool HasConflicts => ConflictsText.Length > 0;

    [ObservableProperty] private bool _isSelected;
}

/// <summary>Patches of one category, for a grouped checklist.</summary>
public sealed record PatchOptionGroup(string Category, IReadOnlyList<PatchOptionItemViewModel> Items);

/// <summary>
/// The optional game patch checklist for the Settings page. Toggling an item saves the selection
/// immediately (GameSettings.OptionalPatches via OptionalPatchCatalogService); the next Patch Game
/// applies it. Selecting a patch unticks the ones it conflicts with. <see cref="NeedsRepatch"/>
/// says whether the patched game still matches the selection.
///
/// The build check hashes the GameMod sources in dev, so it runs on the thread pool, debounced:
/// a burst of toggles (or typing a game path) costs one check. It re-runs when a patch finishes.
/// </summary>
public partial class PatchOptionsViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<PatchOptionsViewModel>();
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(300);

    private readonly OptionalPatchCatalogService _catalogService;
    private readonly GameSettingsService _settingsService;
    private readonly GamePatcherService? _patcher;
    private readonly Action<Action> _post;
    private readonly TimeSpan _debounce;
    private CancellationTokenSource? _refreshCts;
    private int _refreshGeneration;
    private string? _gamePath;
    private bool _suppressSave;
    private bool _disposed;

    /// <summary>Every optional patch (category, then name order).</summary>
    public ObservableCollection<PatchOptionItemViewModel> Options { get; } = [];

    /// <summary>The same items grouped by category.</summary>
    public IReadOnlyList<PatchOptionGroup> Groups { get; private set; } = [];

    /// <summary>False when no catalogue was found (nothing to choose).</summary>
    public bool HasOptions => Options.Count > 0;

    /// <summary>Ids of the ticked patches (what Patch Game will apply).</summary>
    public IReadOnlyList<string> SelectedIds =>
        Options.Where(o => o.IsSelected).Select(o => o.Id).OrderBy(i => i, StringComparer.Ordinal).ToList();

    public string SelectionSummary => $"{Options.Count(o => o.IsSelected)} of {Options.Count} optional patches selected";

    /// <summary>True when the patched game doesn't match the selection (or sources) — patch again.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    private bool _needsRepatch;

    /// <summary>One line describing the build status (from the build stamp).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    private string _buildStatusText = "";

    /// <summary>A build check is pending or running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    private bool _isCheckingBuild;

    /// <summary>The last build check threw (status unknown).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    private bool _buildCheckFailed;

    /// <summary>The last check found the patched game matching the selection.</summary>
    public bool IsUpToDate => !NeedsRepatch && !IsCheckingBuild && !BuildCheckFailed && BuildStatusText.Length > 0;

    /// <summary>The latest scheduled build check (tests await it).</summary>
    public Task BuildStatusRefresh { get; private set; } = Task.CompletedTask;

    public PatchOptionsViewModel(OptionalPatchCatalogService catalogService, GameSettingsService settingsService,
        GamePatcherService patcher)
        : this(catalogService, settingsService, patcher, a => Dispatcher.UIThread.Post(a), DefaultDebounce)
    {
    }

    /// <param name="catalogService">The catalogue and saved selection.</param>
    /// <param name="settingsService">Where the saved game path comes from.</param>
    /// <param name="patcher">Its PatchFinished re-checks the build (null in tests).</param>
    /// <param name="post">Runs an action on the UI thread (tests pass <c>a =&gt; a()</c>).</param>
    /// <param name="debounce">How long toggles settle before the build is re-checked.</param>
    public PatchOptionsViewModel(OptionalPatchCatalogService catalogService, GameSettingsService settingsService,
        GamePatcherService? patcher, Action<Action> post, TimeSpan debounce)
    {
        _catalogService = catalogService;
        _settingsService = settingsService;
        _patcher = patcher;
        _post = post;
        _debounce = debounce;
        if (_patcher != null) _patcher.PatchFinished += OnPatchFinished;
        Reload();
    }

    /// <summary>
    /// The patched game folder to check (the Settings page's field, saved or not). Null = the saved
    /// GamePath setting.
    /// </summary>
    public void SetGamePath(string? gamePath)
    {
        if (string.Equals(_gamePath, gamePath, StringComparison.Ordinal)) return;
        _gamePath = gamePath;
        ScheduleBuildStatusRefresh(_debounce);
    }

    /// <summary>Rebuild the checklist from the catalogue and the saved selection.</summary>
    public void Reload()
    {
        foreach (var item in Options) item.PropertyChanged -= OnItemPropertyChanged;
        Options.Clear();

        // GetSelectedIds already dropped unknown ids and settled conflicts (newest pick wins).
        var selected = new HashSet<string>(_catalogService.GetSelectedIds(), StringComparer.Ordinal);
        var catalog = _catalogService.Catalog;
        foreach (var patch in _catalogService.Patches)
        {
            var item = new PatchOptionItemViewModel(patch, selected.Contains(patch.Id))
            {
                ConflictsText = string.Join(", ", catalog.ConflictsOf(patch.Id).Select(id => catalog.Find(id)?.Name ?? id)),
            };
            item.PropertyChanged += OnItemPropertyChanged;
            Options.Add(item);
        }
        Groups = Options.GroupBy(o => o.Category).Select(g => new PatchOptionGroup(g.Key, g.ToList())).ToList();
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(HasOptions));
        OnSelectionChanged(save: false);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        _catalogService.ResetToDefaults();
        Reload();
    }

    [RelayCommand]
    private void SelectNone()
    {
        // With no catalogue there is nothing to untick, and saving [] would turn "never chosen"
        // (defaults) into "none" for a catalogue that shows up later.
        if (Options.Count == 0) return;
        SetAll(_ => false);
    }

    /// <summary>Re-check the build status now (no debounce). The result lands on the UI thread.</summary>
    [RelayCommand]
    public Task RefreshBuildStatusAsync()
    {
        ScheduleBuildStatusRefresh(TimeSpan.Zero);
        return BuildStatusRefresh;
    }

    private void OnPatchFinished(string outputPath, string? error) =>
        _post(() => ScheduleBuildStatusRefresh(TimeSpan.Zero));

    /// <summary>
    /// Check the build in the background after <paramref name="delay"/>, superseding any pending
    /// check. The selection and path are snapshotted here, on the UI thread.
    /// </summary>
    private void ScheduleBuildStatusRefresh(TimeSpan delay)
    {
        if (_disposed) return;
        var desired = SelectedIds;
        var gamePath = _gamePath;
        CancelPendingRefresh();
        var cts = _refreshCts = new CancellationTokenSource();
        int generation = Interlocked.Increment(ref _refreshGeneration);
        IsCheckingBuild = true;
        BuildStatusRefresh = RunBuildCheckAsync(generation, gamePath, desired, delay, cts.Token);
    }

    /// <summary>
    /// Cancel and dispose the pending check's token source. Safe while that check is still running:
    /// it is cancelled first, and a cancelled token stays readable after its source is disposed.
    /// </summary>
    private void CancelPendingRefresh()
    {
        var old = _refreshCts;
        _refreshCts = null;
        if (old == null) return;
        old.Cancel();
        old.Dispose();
    }

    private async Task RunBuildCheckAsync(int generation, string? gamePath, IReadOnlyList<string> desired,
        TimeSpan delay, CancellationToken ct)
    {
        try
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, ct).ConfigureAwait(false);
            var (status, failed) = await Task.Run(() => CheckBuild(gamePath, desired), ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;
            _post(() =>
            {
                if (_disposed || generation != Volatile.Read(ref _refreshGeneration)) return;
                NeedsRepatch = status.NeedsRepatch;
                BuildStatusText = status.Message;
                BuildCheckFailed = failed;
                IsCheckingBuild = false;
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer check, or disposed.
        }
    }

    private (OptionalPatchBuildStatus Status, bool Failed) CheckBuild(string? gamePath, IReadOnlyList<string> desired)
    {
        try
        {
            return (_catalogService.CheckGameBuild(gamePath ?? _settingsService.Load().GamePath, desired), false);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[patches] build status check failed");
            return (new OptionalPatchBuildStatus(false, "Couldn't check the patched game: " + ex.Message, []), true);
        }
    }

    private void SetAll(Func<PatchOptionItemViewModel, bool> selected)
    {
        _suppressSave = true;
        try
        {
            foreach (var item in Options) item.IsSelected = selected(item);
        }
        finally
        {
            _suppressSave = false;
        }
        OnSelectionChanged(save: true);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PatchOptionItemViewModel.IsSelected) || _suppressSave) return;
        if (sender is PatchOptionItemViewModel { IsSelected: true } item)
        {
            // Untick whatever the newly ticked patch conflicts with (the newest pick wins).
            var conflicts = _catalogService.Catalog.ConflictsOf(item.Id);
            _suppressSave = true;
            try
            {
                foreach (var other in Options.Where(o => o.IsSelected && conflicts.Contains(o.Id)))
                    other.IsSelected = false;
            }
            finally
            {
                _suppressSave = false;
            }
        }
        OnSelectionChanged(save: true);
    }

    private void OnSelectionChanged(bool save)
    {
        if (save)
        {
            try
            {
                _catalogService.SaveSelectedIds(SelectedIds);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[patches] could not save the optional patch selection");
            }
        }
        OnPropertyChanged(nameof(SelectedIds));
        OnPropertyChanged(nameof(SelectionSummary));
        ScheduleBuildStatusRefresh(_debounce);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_patcher != null) _patcher.PatchFinished -= OnPatchFinished;
        CancelPendingRefresh();
        foreach (var item in Options) item.PropertyChanged -= OnItemPropertyChanged;
        GC.SuppressFinalize(this);
    }
}
