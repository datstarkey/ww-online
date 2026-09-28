using Avalonia;
using Avalonia.Controls;
using WWOnline.ViewModels;

namespace WWOnline.Views;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _vm;

    public DashboardView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.CopyRequested -= OnCopyRequested;
        _vm = DataContext as DashboardViewModel;
        if (_vm != null) _vm.CopyRequested += OnCopyRequested;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_vm != null) _vm.CopyRequested -= OnCopyRequested;
        _vm = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_vm == null && DataContext is DashboardViewModel vm)
        {
            _vm = vm;
            _vm.CopyRequested += OnCopyRequested;
        }
    }

    /// <summary>Copy invite: the clipboard lives on the TopLevel, so the view does it.</summary>
    private void OnCopyRequested(string text) => _ = CopyAsync(text);

    private async Task CopyAsync(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(text);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[room] couldn't copy the invite address");
        }
    }
}
