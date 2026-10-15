using Avalonia.Controls;
using WWOnline.ViewModels;

namespace WWOnline.Views;

public partial class NativeBuildView : UserControl
{
    public NativeBuildView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not NativeSetupViewModel vm) return;
            vm.BrowseForSourceFolderAsync = () => Pickers.PickFolderAsync(this, "Select Seaborne's source folder");
            vm.BrowseForLibraryAsync = () => Pickers.PickLibraryAsync(this);
        };
    }
}
