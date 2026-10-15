using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using WWOnline.Shared;
using WWOnline.ViewModels;

namespace WWOnline.Views;

/// <summary>
/// The game popped out of the main window (<see cref="GameWindowViewModel"/>). Its window state follows
/// IsFullScreen, and native full screen (the green button) sets it back. MainWindow moves the game's view into
/// <see cref="GameSlot"/> and docks it again when this window closes.
/// </summary>
public partial class GamePopOutWindow : Window
{
    private GameWindowViewModel? _game;

    public GamePopOutWindow()
    {
        InitializeComponent();
        Title = $"{AppInfo.DisplayName} · Game";
    }

    public Panel GameSlot => Slot;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Unsubscribe();
        _game = DataContext as GameWindowViewModel;
        if (_game == null) return;
        _game.PropertyChanged += OnGamePropertyChanged;
        FollowFullScreen(_game);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && _game != null)
            _game.IsFullScreen = WindowState == WindowState.FullScreen;
    }

    protected override void OnClosed(EventArgs e)
    {
        Unsubscribe();
        base.OnClosed(e);
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameWindowViewModel.IsFullScreen) && sender is GameWindowViewModel game)
            FollowFullScreen(game);
    }

    private void FollowFullScreen(GameWindowViewModel game) =>
        WindowState = game.IsFullScreen ? WindowState.FullScreen : WindowState.Normal;

    private void Unsubscribe()
    {
        if (_game != null) _game.PropertyChanged -= OnGamePropertyChanged;
        _game = null;
    }
}
