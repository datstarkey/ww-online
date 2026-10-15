using Avalonia.Controls;

namespace WWOnline.Views;

/// <summary>The main window's game page: the docked game under its bar, or a placeholder while it is popped out.</summary>
public partial class GameDockView : UserControl
{
    public GameDockView()
    {
        InitializeComponent();
    }

    /// <summary>Where the game's view goes while docked.</summary>
    public Panel GameSlot => Slot;
}
