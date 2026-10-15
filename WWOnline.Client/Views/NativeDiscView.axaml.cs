using Avalonia.Controls;
using WWOnline.ViewModels;

namespace WWOnline.Views;

public partial class NativeDiscView : UserControl
{
    public NativeDiscView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is NativeSetupViewModel vm)
                vm.BrowseForDiscImageAsync = () => Pickers.PickDiscImageAsync(this);
        };
    }
}
