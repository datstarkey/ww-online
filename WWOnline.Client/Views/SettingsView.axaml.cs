using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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
            vm.BrowseForFileAsync = BrowseForFileAsync;
            vm.BrowseForFolderAsync = BrowseForFolderAsync;
            vm.BrowseForVanillaFolderAsync = BrowseForVanillaFolderAsync;
        }
    }

    private async System.Threading.Tasks.Task<string?> BrowseForFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Dolphin Executable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executable") { Patterns = new[] { "*.exe" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        });

        return files.FirstOrDefault()?.Path.LocalPath;
    }

    private async System.Threading.Tasks.Task<string?> BrowseForFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Patched Game Output Folder",
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    private async System.Threading.Tasks.Task<string?> BrowseForVanillaFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Vanilla Game Folder",
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }
}
