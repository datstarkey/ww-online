using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Services;
using WWOnline.Shared.Hubs;

namespace WWOnline.ViewModels;

/// <summary>The first-run setup's steps, in order.</summary>
public enum SetupStep
{
    Welcome,
    /// <summary>Before Game: the player needs Dolphin (or its DolphinTool) to extract their disc.</summary>
    Dolphin,
    Game,
    Patches,
    Player,
    Patch,
    Done,
}

/// <summary>One entry in the setup's step list.</summary>
public partial class SetupStepItem : ObservableObject
{
    public SetupStepItem(SetupStep step, int number, string title)
    {
        Step = step;
        Number = number;
        Title = title;
    }

    public SetupStep Step { get; }
    public int Number { get; }
    public string Title { get; }

    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isDone;
}

/// <summary>
/// The first-run setup (also Settings → Run setup): welcome, Dolphin, the game (extracting the disc
/// image with Dolphin's DolphinTool when it can, see <see cref="DolphinTool"/>), the optional
/// game patches (the Settings checklist's own view-model, <see cref="PatchOptions"/>), name and tunic
/// colour (<see cref="Appearance"/>), patching, and how to play.
///
/// Each step is saved when the player moves on from it, so closing the app half way keeps what they
/// entered. Finishing or skipping the setup sets <see cref="GameSettings.SetupCompleted"/>
/// (<see cref="FirstRun"/>). Patching goes through the same GamePatcherService.PatchGameAsync call as
/// the Settings page's Patch game button.
/// </summary>
public partial class SetupWizardViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<SetupWizardViewModel>();

    /// <summary>Patches the game: (vanilla folder, patched folder, progress) → error or null.</summary>
    public delegate Task<string?> PatchGameFunc(string vanillaPath, string gamePath, IProgress<string>? progress);

    private readonly GameSettingsService _settings;
    private readonly PatchGameFunc _patchGame;
    private readonly Func<IReadOnlyList<string>> _findDolphin;
    private readonly DolphinTool _dolphinTool;
    private readonly Action<Action> _post;
    private CancellationTokenSource? _extractCts;
    private readonly CancellationTokenSource _lifetime = new(); // cancels tool checks and extraction on Dispose
    private bool _disposed;

    /// <summary>The optional patch checklist (shared with Settings → Game patches).</summary>
    public PatchOptionsViewModel PatchOptions { get; }

    /// <summary>The Appearance page's view-model: its tunic swatches, picked here too.</summary>
    public AppearanceViewModel Appearance { get; }

    public ObservableCollection<SetupStepItem> Steps { get; } =
    [
        new(SetupStep.Welcome, 1, "Welcome"),
        new(SetupStep.Dolphin, 2, "Dolphin"),
        new(SetupStep.Game, 3, "Your game"),
        new(SetupStep.Patches, 4, "Game patches"),
        new(SetupStep.Player, 5, "You"),
        new(SetupStep.Patch, 6, "Patch"),
        new(SetupStep.Done, 7, "Play"),
    ];

    /// <summary>Raised when the setup closes (finished or skipped).</summary>
    public event Action? Closed;

    /// <summary>Set by the view: folder and file pickers (null result = cancelled).</summary>
    public Func<string, Task<string?>>? BrowseForFolderAsync { get; set; }
    public Func<Task<string?>>? BrowseForDolphinAsync { get; set; }
    public Func<Task<string?>>? BrowseForDiscImageAsync { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsGame), nameof(IsDolphin), nameof(IsPatches), nameof(IsPlayer),
        nameof(IsPatch), nameof(IsDone), nameof(CanGoBack), nameof(CanGoNext), nameof(NextText), nameof(CanSkipStep),
        nameof(SkipStepText), nameof(StepCaption))]
    private SetupStep _step = SetupStep.Welcome;

    public bool IsWelcome => Step == SetupStep.Welcome;
    public bool IsGame => Step == SetupStep.Game;
    public bool IsDolphin => Step == SetupStep.Dolphin;
    public bool IsPatches => Step == SetupStep.Patches;
    public bool IsPlayer => Step == SetupStep.Player;
    public bool IsPatch => Step == SetupStep.Patch;
    public bool IsDone => Step == SetupStep.Done;

    /// <summary>"Step 2 of 7".</summary>
    public string StepCaption => $"Step {(int)Step + 1} of {Steps.Count}";

    // ── Game folders ──────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(VanillaOk), nameof(PatchSummary))]
    private string _vanillaGamePath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(GamePathOk), nameof(PatchSummary))]
    private string _gamePath = "";

    [ObservableProperty] private string? _vanillaError;
    [ObservableProperty] private string? _vanillaNote;
    [ObservableProperty] private string? _gamePathError;

    /// <summary>Set when the chosen folder has the game one level down: offered as a one-click fix.</summary>
    [ObservableProperty] private string? _suggestedVanillaPath;

    /// <summary>"GZLE01 · The Wind Waker (USA)" once the original game checks out.</summary>
    [ObservableProperty] private string? _detectedGameText;

    public bool VanillaOk => VanillaError == null && !string.IsNullOrWhiteSpace(VanillaGamePath);
    public bool GamePathOk => GamePathError == null && !string.IsNullOrWhiteSpace(GamePath);

    // ── Extracting the disc image (DolphinTool) ───────────────────

    /// <summary>The chosen Dolphin has a DolphinTool that can extract: offer "extract my ISO" on the Game step.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowManualExtractSteps))]
    private bool _canExtract;

    /// <summary>Still checking DolphinTool (on entering the Game step).</summary>
    [ObservableProperty] private bool _isCheckingTool;

    /// <summary>The DolphinTool.exe being used (when <see cref="CanExtract"/>).</summary>
    [ObservableProperty] private string? _dolphinToolPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExtractNow))]
    private string _discImagePath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExtractNow))]
    private string _extractFolder = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(CanGoNext), nameof(CanExtractNow))]
    private bool _isExtracting;

    [ObservableProperty] private string _extractProgress = "";
    [ObservableProperty] private string? _extractError;
    [ObservableProperty] private bool _extractSucceeded;

    public bool CanExtractNow => CanExtract && !IsExtracting &&
                                 !string.IsNullOrWhiteSpace(DiscImagePath) && !string.IsNullOrWhiteSpace(ExtractFolder);

    /// <summary>No usable DolphinTool: show how to extract in Dolphin's own window instead.</summary>
    public bool ShowManualExtractSteps => !CanExtract;

    /// <summary>The latest DolphinTool check (tests await it).</summary>
    public Task ToolDetection { get; private set; } = Task.CompletedTask;

    // ── Dolphin ───────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(DolphinOk))]
    private string _dolphinPath = "";

    [ObservableProperty] private string? _dolphinError;
    [ObservableProperty] private bool _autoLaunchDolphin = true;

    /// <summary>Dolphin.exe files found on this PC (click one to use it).</summary>
    public ObservableCollection<string> DolphinCandidates { get; } = [];

    [ObservableProperty] private bool _hasSearchedForDolphin;

    public bool DolphinOk => DolphinError == null && !string.IsNullOrWhiteSpace(DolphinPath);
    public bool HasDolphinCandidates => DolphinCandidates.Count > 0;
    public bool NoDolphinFound => HasSearchedForDolphin && DolphinCandidates.Count == 0;

    // ── Player ────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(PlayerInitials))]
    private string _playerName = "";

    [ObservableProperty] private string? _playerNameError;

    public int MaxPlayerNameLength => ProtocolCheck.MaxPlayerNameLength;
    public string PlayerInitials => TunicColors.InitialsOf(PlayerName);

    // ── Patch ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack), nameof(CanGoNext), nameof(CanSkipStep), nameof(CanPatch), nameof(ShowPatchButton))]
    private bool _isPatching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(ShowPatchButton), nameof(PatchButtonText))]
    private bool _patchSucceeded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchButtonText))]
    private string? _patchError;

    [ObservableProperty] private string _patchProgress = "";

    public bool CanPatch => !IsPatching && VanillaOk && GamePathOk;

    /// <summary>The patched game is ready: patched just now, or already up to date.</summary>
    public bool IsGameReady => PatchSucceeded || (!IsPatching && PatchOptions.IsUpToDate);

    /// <summary>On the Patch step: the game was already up to date before patching here.</summary>
    public bool ShowAlreadyUpToDate => !PatchSucceeded && !IsPatching && PatchError == null && PatchOptions.IsUpToDate;

    /// <summary>The Play step's line about Dolphin.</summary>
    public string DolphinHint => AutoLaunchDolphin && DolphinOk
        ? "When you host or join, WW-Online starts Dolphin with your patched game and attaches to it."
        : $"Start your patched game in Dolphin ({Path.Combine(GamePath, "sys", "main.dol")}), then attach on the Dolphin page.";
    public bool ShowPatchButton => !IsPatching && !PatchSucceeded;
    public string PatchButtonText => PatchError != null ? "Try again" : "Patch game";

    /// <summary>"From …  →  …", on the Patch step.</summary>
    public string PatchSummary => $"{VanillaGamePath}  →  {GamePath}";

    // ── Navigation ────────────────────────────────────────────────

    public bool CanGoBack => Step is not (SetupStep.Welcome or SetupStep.Done) && !IsPatching && !IsExtracting;

    public bool CanGoNext => Step switch
    {
        SetupStep.Game => VanillaOk && GamePathOk && !IsExtracting,
        SetupStep.Dolphin => DolphinOk,
        SetupStep.Player => !string.IsNullOrWhiteSpace(ProtocolCheck.SanitizeName(PlayerName)),
        SetupStep.Patch => PatchSucceeded || (!IsPatching && PatchOptions.IsUpToDate),
        _ => true,
    };

    public string NextText => Step switch
    {
        SetupStep.Welcome => "Get started",
        SetupStep.Done => "Finish",
        _ => "Next",
    };

    /// <summary>Dolphin and Patch can be left for later (the app then says what's missing).</summary>
    public bool CanSkipStep => Step switch
    {
        SetupStep.Dolphin => true,
        SetupStep.Patch => !IsPatching && !PatchSucceeded,
        _ => false,
    };

    public string SkipStepText => Step == SetupStep.Patch ? "Patch later" : "Skip for now";

    public SetupWizardViewModel(GameSettingsService settings, GamePatcherService patcher, PatchOptionsViewModel patchOptions,
        AppearanceViewModel appearance)
        : this(settings, patcher.PatchGameAsync, patchOptions, appearance, DolphinLocator.FindCandidates,
            new DolphinTool(new ProcessRunner()), a => Avalonia.Threading.Dispatcher.UIThread.Post(a))
    {
    }

    /// <param name="settings">Where each step is saved.</param>
    /// <param name="patchGame">Patches the game (GamePatcherService.PatchGameAsync in the app; a fake in tests).</param>
    /// <param name="patchOptions">The optional patch checklist.</param>
    /// <param name="appearance">The tunic colour picker.</param>
    /// <param name="findDolphin">Finds Dolphin.exe files (DolphinLocator.FindCandidates).</param>
    /// <param name="dolphinTool">Extracts disc images (a fake process runner in tests).</param>
    /// <param name="post">Runs an action on the UI thread (tests pass <c>a =&gt; a()</c>).</param>
    public SetupWizardViewModel(GameSettingsService settings, PatchGameFunc patchGame, PatchOptionsViewModel patchOptions,
        AppearanceViewModel appearance, Func<IReadOnlyList<string>> findDolphin, DolphinTool dolphinTool, Action<Action> post)
    {
        _dolphinTool = dolphinTool;
        _settings = settings;
        _patchGame = patchGame;
        PatchOptions = patchOptions;
        Appearance = appearance;
        _findDolphin = findDolphin;
        _post = post;
        PatchOptions.PropertyChanged += OnPatchOptionsPropertyChanged;
        Load();
    }

    /// <summary>Start over at Welcome with what's saved (first run, or Settings → Run setup).</summary>
    public void Open()
    {
        Load();
        Step = SetupStep.Welcome;
        UpdateStepList();
    }

    private void Load()
    {
        var s = _settings.Load();
        VanillaGamePath = s.VanillaGamePath ?? "";
        GamePath = s.GamePath ?? "";
        DolphinPath = s.DolphinPath ?? "";
        AutoLaunchDolphin = s.AutoLaunchDolphin;
        PlayerName = s.PlayerName ?? "";
        IsPatching = false;
        PatchSucceeded = false;
        PatchError = null;
        PatchProgress = "";
        Validate();
        UpdateStepList();
    }

    private void Validate()
    {
        ValidateVanilla();
        ValidateGamePath();
        ValidateDolphin();
        ValidatePlayerName();
    }

    partial void OnVanillaGamePathChanged(string value)
    {
        ValidateVanilla();
        ValidateGamePath();
    }

    partial void OnGamePathChanged(string value) => ValidateGamePath();
    partial void OnDolphinPathChanged(string value) => ValidateDolphin();
    partial void OnPlayerNameChanged(string value) => ValidatePlayerName();

    private void ValidateVanilla()
    {
        // Blank on a fresh start: no red text before the player has chosen anything.
        if (string.IsNullOrWhiteSpace(VanillaGamePath))
        {
            VanillaError = null;
            VanillaNote = null;
            SuggestedVanillaPath = null;
            DetectedGameText = null;
            Refresh();
            return;
        }
        var check = GameFolderCheck.CheckVanilla(VanillaGamePath);
        VanillaError = check.Error;
        VanillaNote = check.Note;
        SuggestedVanillaPath = check.GameFolder;
        DetectedGameText = check.IsValid
            ? check.GameId != null ? $"{check.GameId} · The Wind Waker (USA)" : "The Wind Waker (game ID not checked)"
            : null;
        Refresh();
    }

    private void ValidateGamePath()
    {
        GamePathError = string.IsNullOrWhiteSpace(GamePath) ? null : GameFolderCheck.CheckPatchedFolder(VanillaGamePath, GamePath);
        Refresh();
    }

    private void ValidateDolphin()
    {
        DolphinError = string.IsNullOrWhiteSpace(DolphinPath) ? null : DolphinLocator.Validate(DolphinPath);
        Refresh();
    }

    private void ValidatePlayerName()
    {
        PlayerNameError = PlayerName.Length > 0 && string.IsNullOrWhiteSpace(ProtocolCheck.SanitizeName(PlayerName))
            ? "Use at least one letter or number."
            : null;
        Refresh();
    }

    /// <summary>The computed flags that depend on the validation results.</summary>
    private void Refresh()
    {
        OnPropertyChanged(nameof(VanillaOk));
        OnPropertyChanged(nameof(GamePathOk));
        OnPropertyChanged(nameof(DolphinOk));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanPatch));
        OnPropertyChanged(nameof(IsGameReady));
        OnPropertyChanged(nameof(ShowAlreadyUpToDate));
        OnPropertyChanged(nameof(DolphinHint));
    }

    partial void OnIsPatchingChanged(bool value) => Refresh();
    partial void OnPatchSucceededChanged(bool value) => Refresh();
    partial void OnPatchErrorChanged(string? value) => Refresh();
    partial void OnAutoLaunchDolphinChanged(bool value) => Refresh();

    private void OnPatchOptionsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PatchOptionsViewModel.IsUpToDate) ||
            e.PropertyName == nameof(PatchOptionsViewModel.NeedsRepatch) ||
            e.PropertyName == nameof(PatchOptionsViewModel.IsCheckingBuild))
            Refresh();
    }

    // ── Commands ──────────────────────────────────────────────────

    [RelayCommand]
    private void Next()
    {
        if (!CanGoNext) return;
        switch (Step)
        {
            case SetupStep.Game:
                SaveGameFolders();
                break;
            case SetupStep.Dolphin:
                SaveDolphin(DolphinPath);
                break;
            case SetupStep.Player:
                SavePlayerName();
                break;
            case SetupStep.Done:
                Finish();
                return;
        }
        GoTo(Step + 1);
    }

    [RelayCommand]
    private void Back()
    {
        if (CanGoBack) GoTo(Step - 1);
    }

    /// <summary>Leave this step for later: Dolphin (keeps whatever was saved) or Patch.</summary>
    [RelayCommand]
    private void SkipStep()
    {
        if (!CanSkipStep) return;
        if (Step == SetupStep.Dolphin) SaveDolphin(null);
        GoTo(Step + 1);
    }

    /// <summary>Close the setup without finishing it. Counts as done: it won't open by itself again.</summary>
    [RelayCommand]
    private void SkipSetup()
    {
        if (IsPatching || IsExtracting) return;
        Logger.Information("[setup] skipped at {Step}", Step);
        Close();
    }

    [RelayCommand]
    private void Finish()
    {
        Logger.Information("[setup] finished");
        Close();
    }

    private void Close()
    {
        FirstRun.MarkCompleted(_settings);
        Closed?.Invoke();
    }

    [RelayCommand]
    private void UseSuggestedVanillaPath()
    {
        if (SuggestedVanillaPath is { } inner) UseVanillaPath(inner);
    }

    [RelayCommand]
    private async Task BrowseVanilla()
    {
        if (BrowseForFolderAsync == null) return;
        var picked = await BrowseForFolderAsync("Choose the extracted Wind Waker folder");
        if (picked == null) return;
        // Picked the folder above the game? Use the game folder inside it.
        var check = GameFolderCheck.CheckVanilla(picked);
        UseVanillaPath(check.GameFolder ?? picked);
    }

    private void UseVanillaPath(string path)
    {
        VanillaGamePath = path;
        if (VanillaOk && string.IsNullOrWhiteSpace(GamePath))
            GamePath = GameFolderCheck.SuggestPatchedFolder(path) ?? "";
    }

    [RelayCommand]
    private async Task BrowseGamePath()
    {
        if (BrowseForFolderAsync == null) return;
        var picked = await BrowseForFolderAsync("Choose a folder for the patched game");
        if (picked != null) GamePath = picked;
    }

    [RelayCommand]
    private async Task BrowseDolphin()
    {
        if (BrowseForDolphinAsync == null) return;
        var picked = await BrowseForDolphinAsync();
        if (picked != null) DolphinPath = picked;
    }

    [RelayCommand]
    private void UseDolphinCandidate(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path)) DolphinPath = path;
    }

    /// <summary>Look for Dolphin.exe (on entering the Dolphin step, and on demand).</summary>
    [RelayCommand]
    private void FindDolphin()
    {
        IReadOnlyList<string> found;
        try { found = _findDolphin(); }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[setup] looking for Dolphin failed");
            found = [];
        }
        DolphinCandidates.Clear();
        foreach (var path in found) DolphinCandidates.Add(path);
        HasSearchedForDolphin = true;
        OnPropertyChanged(nameof(HasDolphinCandidates));
        OnPropertyChanged(nameof(NoDolphinFound));
        if (string.IsNullOrWhiteSpace(DolphinPath) && DolphinCandidates.Count > 0)
            DolphinPath = DolphinCandidates[0];
    }

    [RelayCommand]
    private async Task PatchGame()
    {
        if (!CanPatch) return;
        IsPatching = true;
        PatchSucceeded = false;
        PatchError = null;
        PatchProgress = "Starting…";
        SaveGameFolders(); // the patch uses these; Settings shows the same folders

        var progress = new Progress<string>(msg => _post(() => PatchProgress = msg));
        string? error;
        try
        {
            error = await _patchGame(VanillaGamePath.Trim(), GamePath.Trim(), progress);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[setup] patching failed");
            error = ex.Message;
        }

        IsPatching = false;
        if (error == null)
        {
            PatchSucceeded = true;
            PatchProgress = "";
        }
        else
        {
            PatchError = error;
        }
    }

    // ── Extracting the disc image ─────────────────────────────────

    /// <summary>Is there a DolphinTool next to the chosen Dolphin that can extract? (On entering the Game step.)</summary>
    private async Task DetectDolphinToolAsync()
    {
        var tool = DolphinTool.FindNextTo(DolphinPath);
        if (tool == null)
        {
            CanExtract = false;
            DolphinToolPath = null;
            return;
        }
        if (CanExtract && string.Equals(DolphinToolPath, tool, StringComparison.OrdinalIgnoreCase)) return;

        IsCheckingTool = true;
        bool supported = false;
        try
        {
            supported = await _dolphinTool.SupportsExtractAsync(tool, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            // The setup closed.
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[setup] checking DolphinTool failed");
        }
        finally
        {
            _post(() =>
            {
                IsCheckingTool = false;
                DolphinToolPath = supported ? tool : null;
                CanExtract = supported;
                OnPropertyChanged(nameof(CanExtractNow));
            });
        }
    }

    partial void OnDiscImagePathChanged(string value)
    {
        ExtractError = null;
        ExtractSucceeded = false;
        if (string.IsNullOrWhiteSpace(ExtractFolder) && DolphinTool.SuggestExtractFolder(value) is { } folder)
            ExtractFolder = folder;
    }

    [RelayCommand]
    private async Task BrowseDiscImage()
    {
        if (BrowseForDiscImageAsync == null) return;
        var picked = await BrowseForDiscImageAsync();
        if (picked == null) return;
        ExtractFolder = ""; // re-suggest beside the new image
        DiscImagePath = picked;
    }

    [RelayCommand]
    private async Task BrowseExtractFolder()
    {
        if (BrowseForFolderAsync == null) return;
        var picked = await BrowseForFolderAsync("Choose an empty folder to extract the game into");
        if (picked != null) ExtractFolder = picked;
    }

    /// <summary>
    /// Extract the disc image with DolphinTool, then use the extracted folder as the original game (its
    /// GZLE01 check runs as for a folder picked by hand). A folder that already holds the game is used as is.
    /// </summary>
    [RelayCommand]
    private async Task ExtractDisc()
    {
        if (!CanExtractNow || DolphinToolPath is not { } tool) return;
        var image = DiscImagePath.Trim();
        var folder = ExtractFolder.Trim();
        ExtractError = DolphinTool.CheckExtractInputs(image, folder);
        ExtractSucceeded = false;
        if (ExtractError != null) return;

        if (!File.Exists(Path.Combine(folder, "sys", "main.dol")))
        {
            // A folder this extraction creates is ours to remove if it fails; one that was there isn't.
            bool createdHere = !Directory.Exists(folder);
            IsExtracting = true;
            ExtractProgress = "Starting DolphinTool…";
            _extractCts?.Dispose();
            var cts = _extractCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            string? error;
            try
            {
                // Lines that arrive after Cancel (DolphinTool's last words) are dropped.
                var progress = new Progress<string>(line => _post(() =>
                {
                    if (IsExtracting && !cts.IsCancellationRequested) ExtractProgress = line;
                }));
                error = await _dolphinTool.ExtractAsync(tool, image, folder, progress, cts.Token);
            }
            catch (OperationCanceledException)
            {
                error = "Extraction cancelled.";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[setup] extraction failed");
                error = ex.Message;
            }
            IsExtracting = false;
            ExtractProgress = "";
            if (error != null)
            {
                if (createdHere) RemovePartialExtraction(folder);
                ExtractError = error;
                return;
            }
        }

        ExtractSucceeded = true;
        // DolphinTool puts a GameCube disc in folder\sys + folder\files; take a nested game folder too.
        UseVanillaPath(GameFolderCheck.CheckVanilla(folder).GameFolder ?? folder);
    }

    /// <summary>Delete what a failed or cancelled extraction left in the folder it created, so a retry can use it.</summary>
    private static void RemovePartialExtraction(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warning(ex, "[setup] couldn't remove the partial extraction in {Folder}", folder);
        }
    }

    [RelayCommand]
    private void CancelExtract() => _extractCts?.Cancel();

    // ── Steps ─────────────────────────────────────────────────────

    private void GoTo(SetupStep step)
    {
        if (step < SetupStep.Welcome || step > SetupStep.Done) return;
        Step = step;
        if (step == SetupStep.Dolphin && !HasSearchedForDolphin) FindDolphin();
        if (step == SetupStep.Game) ToolDetection = DetectDolphinToolAsync();
        if (step == SetupStep.Patch && !PatchSucceeded)
        {
            PatchOptions.SetGamePath(GamePath.Trim());
            _ = PatchOptions.RefreshBuildStatusAsync();
        }
        UpdateStepList();
    }

    private void UpdateStepList()
    {
        foreach (var item in Steps)
        {
            item.IsCurrent = item.Step == Step;
            item.IsDone = item.Step < Step;
        }
    }

    private void SaveGameFolders()
    {
        var s = _settings.Load();
        s.VanillaGamePath = VanillaGamePath.Trim();
        s.GamePath = GamePath.Trim();
        _settings.Save(s);
        PatchOptions.SetGamePath(s.GamePath);
    }

    /// <param name="dolphinPath">The Dolphin.exe to save, or null to keep the saved one (skipped).</param>
    private void SaveDolphin(string? dolphinPath)
    {
        var s = _settings.Load();
        if (dolphinPath != null) s.DolphinPath = dolphinPath.Trim();
        s.AutoLaunchDolphin = AutoLaunchDolphin;
        _settings.Save(s);
    }

    private void SavePlayerName()
    {
        var s = _settings.Load();
        s.PlayerName = ProtocolCheck.SanitizeName(PlayerName);
        _settings.Save(s);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PatchOptions.PropertyChanged -= OnPatchOptionsPropertyChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _extractCts?.Dispose();
        _extractCts = null;
        GC.SuppressFinalize(this);
    }
}
