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
    public GameWindowViewModel(Action docked) => _docked = docked;

    [ObservableProperty]
    private bool _isPoppedOut;

    /// <summary>The pop-out window is full screen. Only while popped out.</summary>
    [ObservableProperty]
    private bool _isFullScreen;

    [RelayCommand]
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

    /// <summary>F11 and the Full screen buttons. Docked, the game pops out full screen.</summary>
    [RelayCommand]
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
