using Avalonia.Controls;
using WWOnline.ViewModels;

namespace WWOnline.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.BrowseForFileAsync = () => Pickers.PickDolphinAsync(this);
            vm.BrowseForFolderAsync = () => Pickers.PickFolderAsync(this, "Select Patched Game Output Folder");
            vm.BrowseForVanillaFolderAsync = () => Pickers.PickFolderAsync(this, "Select Vanilla Game Folder");
        }
    }
}
