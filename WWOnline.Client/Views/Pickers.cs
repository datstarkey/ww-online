using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace WWOnline.Views;

/// <summary>The folder and Dolphin.exe pickers the Settings page and the setup share.</summary>
internal static class Pickers
{
    /// <summary>A folder, or null when cancelled.</summary>
    public static async Task<string?> PickFolderAsync(Control owner, string title)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    /// <summary>A GameCube disc image (DolphinTool.DiscImageExtensions), or null when cancelled.</summary>
    public static async Task<string?> PickDiscImageAsync(Control owner)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select your Wind Waker disc image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Disc images") { Patterns = Services.DolphinTool.DiscImageExtensions.Select(e => "*" + e).ToArray() },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });
        return files.FirstOrDefault()?.Path.LocalPath;
    }

    /// <summary>Dolphin.exe, or null when cancelled.</summary>
    public static async Task<string?> PickDolphinAsync(Control owner)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Dolphin.exe",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Dolphin") { Patterns = ["Dolphin.exe"] },
                new FilePickerFileType("Programs") { Patterns = ["*.exe"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });
        return files.FirstOrDefault()?.Path.LocalPath;
    }
}
