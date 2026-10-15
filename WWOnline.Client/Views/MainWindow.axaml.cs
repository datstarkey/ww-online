using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WWOnline.ViewModels;

namespace WWOnline.Views;

public partial class MainWindow : Window
{
    private GameView? _gameView;
    private GamePopOutWindow? _popOut;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                vm.GameWindow.PropertyChanged += OnGameWindowPropertyChanged;
                AddGameView(vm);
            }
        };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.NativeGame) && sender is MainViewModel vm)
            AddGameView(vm);
    }

    // Only once the native game has started: creating the view waits for the game thread to load libtww.
    private void AddGameView(MainViewModel vm)
    {
        if (vm.NativeGame is not { } host || _gameView != null) return;
        _gameView = new GameView(host);
        GameDock.GameSlot.Children.Add(_gameView);
    }

    // F11 on the game page: the game pops out full screen (in its window, F11 toggles full screen).
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F11 || DataContext is not MainViewModel { IsGameViewActive: true } vm) return;
        vm.GameWindow.ToggleFullScreenCommand.Execute(null);
        e.Handled = true;
    }

    private void OnGameWindowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GameWindowViewModel.IsPoppedOut) || sender is not GameWindowViewModel game ||
            _gameView == null)
            return;
        if (game.IsPoppedOut)
            PopOut(game);
        else if (_popOut is { } window)
        {
            TakeBackGame(window);
            window.Close();
        }
    }

    // The pop-out window takes the game page's size, so the game keeps its size.
    private void PopOut(GameWindowViewModel game)
    {
        if (_popOut != null) return;
        _popOut = new GamePopOutWindow { DataContext = game, Width = GameDock.Bounds.Width, Height = GameDock.Bounds.Height };
        _popOut.Closing += OnPopOutClosing;
        MoveGameView(_popOut.GameSlot);
        _popOut.Show();
    }

    // Closed by the player (its close button, Cmd+W): the game docks before its window goes.
    private void OnPopOutClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not GamePopOutWindow window || window != _popOut) return;
        TakeBackGame(window);
        if (DataContext is MainViewModel vm) vm.GameWindow.DockCommand.Execute(null);
    }

    private void TakeBackGame(GamePopOutWindow window)
    {
        window.Closing -= OnPopOutClosing;
        _popOut = null;
        MoveGameView(GameDock.GameSlot);
    }

    // One GameView for the process, moved within one UI turn: Avalonia then re-parents its NSView into the other
    // window instead of destroying it (NativeControlHost queues destruction and cancels it on re-attachment), and
    // the game keeps running. The keys follow the view once the new window has shown.
    private void MoveGameView(Panel slot)
    {
        if (_gameView is not { } view) return;
        (view.Parent as Panel)?.Children.Remove(view);
        slot.Children.Add(view);
        Dispatcher.UIThread.Post(view.TakeKeyboard, DispatcherPriority.Background);
    }
}
