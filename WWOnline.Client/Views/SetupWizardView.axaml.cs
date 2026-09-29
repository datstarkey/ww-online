using Avalonia.Controls;
using WWOnline.ViewModels;

namespace WWOnline.Views;

/// <summary>The first-run setup (SetupWizardViewModel), shown over the whole window.</summary>
public partial class SetupWizardView : UserControl
{
    public SetupWizardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is SetupWizardViewModel vm)
        {
            vm.BrowseForFolderAsync = title => Pickers.PickFolderAsync(this, title);
            vm.BrowseForDolphinAsync = () => Pickers.PickDolphinAsync(this);
            vm.BrowseForDiscImageAsync = () => Pickers.PickDiscImageAsync(this);
        }
    }
}
