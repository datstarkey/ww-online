using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WWOnline.ViewModels;

/// <summary>
/// Where the embedded game shows: docked on the main window's game page, or popped out into its own window,
/// windowed or full screen. MainWindow moves the game's one view to follow <see cref="IsPoppedOut"/>, and
/// GamePopOutWindow follows <see cref="IsFullScreen"/> both ways (the macOS green button sets it too).
/// The game runs throughout; only its view moves.
/// </summary>
public partial class GameWindowViewModel : ViewModelBase
{
    private readonly Action _docked;

    /// <param name="docked">Called once the game is back in the main window (shows the game page).</param>
    /// <param name="room">The room, for the game bar's players, warp and PVP badge.</param>
    public GameWindowViewModel(Action docked, DashboardViewModel? room = null)
    {
        _docked = docked;
        Room = room;
    }

    /// <summary>The room, for the game bar (null in tests).</summary>
    public DashboardViewModel? Room { get; }

    /// <summary>The game has started (its view exists): pop out and full screen have something to move.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PopOutCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleFullScreenCommand))]
    private bool _isAvailable;

    /// <summary>The game bar's status: starting, or why the game isn't running ("" while it runs).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = "";

    /// <summary>The game is starting (the status shows progress, not a problem).</summary>
    [ObservableProperty]
    private bool _isStarting;

    /// <summary>The game couldn't start or stopped: its view is hidden and the status says what to do.</summary>
    [ObservableProperty]
    private bool _hasFailed;

    public bool HasStatus => Status.Length > 0;

    [ObservableProperty]
    private bool _isPoppedOut;

    /// <summary>The pop-out window is full screen. Only while popped out.</summary>
    [ObservableProperty]
    private bool _isFullScreen;

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private void PopOut() => IsPoppedOut = true;

    /// <summary>Back into the main window (its Bring back, the pop-out's button, or the pop-out window closing).
    /// Full screen ends with the pop-out window.</summary>
    [RelayCommand]
    private void Dock()
    {
        if (!IsPoppedOut) return;
        IsPoppedOut = false;
        IsFullScreen = false;
        _docked();
    }

    /// <summary>F11, Ctrl+Cmd+F (View menu) and the Full screen buttons. Docked, the game pops out full screen.</summary>
    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private void ToggleFullScreen()
    {
        if (!IsPoppedOut)
        {
            IsFullScreen = true;
            IsPoppedOut = true;
            return;
        }
        IsFullScreen = !IsFullScreen;
    }
}
