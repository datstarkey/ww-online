using Serilog;
using WWOnline.Patcher.Icons;

namespace WWOnline.Services;

/// <summary>
/// The game's item icons, decoded from the player's OWN game files into a local cache
/// (<see cref="AppPaths.IconCacheDirectory"/>; never the repo, the app folder or PatchData).
///
/// Extraction runs: at startup when the cache is missing or from an older <see cref="ItemIconCatalog.Version"/>
/// (from the vanilla game folder, else the patched one); after a successful patch when the cache is
/// still missing or out of date (<see cref="GamePatcherService.PatchFinished"/>, which covers the
/// setup's Patch step); and always from Settings' "Read icons from game". Until then
/// <see cref="GetPath"/> returns null and the UI shows its placeholders (<see cref="ViewModels.ItemIcons"/>).
/// Two clients sharing the cache (dev-test) are serialised by <see cref="ItemIconExtractor.Extract"/>.
///
/// UI-free (paths only) so it can be tested without a renderer.
/// </summary>
public class ItemIconService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ItemIconService>();

    private readonly GameSettingsService? _settings;
    private readonly GamePatcherService? _patcher;
    private readonly SemaphoreSlim _extractLock = new(1, 1);
    private volatile bool _available;
    private volatile bool _extracting;
    private string _status = "";

    /// <param name="settings">Where the game folders are (null in tests: pass folders to <see cref="ExtractAsync"/>).</param>
    /// <param name="patcher">Re-extract after a patch (null in tests).</param>
    /// <param name="cacheDirectory">The icon folder (null = <see cref="AppPaths.IconCacheDirectory"/>).</param>
    public ItemIconService(GameSettingsService? settings, GamePatcherService? patcher, string? cacheDirectory = null)
    {
        _settings = settings;
        _patcher = patcher;
        CacheDirectory = cacheDirectory ?? AppPaths.IconCacheDirectory;
        _available = ItemIconExtractor.IsCurrent(CacheDirectory);
        _status = DescribeCache();
        if (_patcher != null) _patcher.PatchFinished += OnPatchFinished;
    }

    public string CacheDirectory { get; }

    /// <summary>The cache holds a complete, current extraction.</summary>
    public bool IsAvailable => _available;

    public bool IsExtracting => _extracting;

    /// <summary>One line for Settings ("116 item icons from your game files", or why there are none).</summary>
    public string StatusText => Volatile.Read(ref _status);

    /// <summary>Raised (on any thread) when the icons appear, change, or extraction starts or stops.</summary>
    public event Action? Changed;

    /// <summary>The PNG for an item number (dItemNo), or null when icons aren't extracted or it has none.</summary>
    public string? GetPath(byte itemNo) =>
        ItemIconCatalog.TextureForItem(itemNo) is { } texture ? GetTexturePath(texture) : null;

    /// <summary>The PNG for a texture name (e.g. "compass"), or null.</summary>
    public string? GetTexturePath(string texture)
    {
        if (!_available) return null;
        var path = Path.Combine(CacheDirectory, texture + ".png");
        return File.Exists(path) ? path : null;
    }

    /// <summary>The first game folder in Settings that holds the icon archive (vanilla first), or null.</summary>
    public string? FindSourceFolder()
    {
        if (_settings == null) return null;
        var s = _settings.Load();
        foreach (var folder in new[] { s.VanillaGamePath, s.GamePath })
            if (ItemIconExtractor.FindArchive(folder) != null) return folder;
        return null;
    }

    /// <summary>Startup: extract if the cache is missing or stale and a game folder is set. Never throws.</summary>
    public async Task EnsureExtractedAsync()
    {
        try { await Task.Run(() => ItemIconExtractor.RemoveLeftovers(CacheDirectory)); }
        catch (Exception ex) { Logger.Debug(ex, "[icons] couldn't clean up old icon folders"); }
        if (_available) return;
        if (FindSourceFolder() is not { } folder)
        {
            SetStatus(DescribeCache());
            return;
        }
        await ExtractAsync(folder, force: false);
    }

    /// <summary>
    /// Decode the icons from <paramref name="gameFolder"/> (null = <see cref="FindSourceFolder"/>) into the
    /// cache. Returns null on success, else a message (also shown as <see cref="StatusText"/>). Never
    /// throws; one extraction at a time. Unless <paramref name="force"/>, a cache another process has
    /// just filled is kept as it is.
    /// </summary>
    public async Task<string?> ExtractAsync(string? gameFolder = null, bool force = true)
    {
        gameFolder ??= FindSourceFolder();
        if (gameFolder == null || ItemIconExtractor.FindArchive(gameFolder) == null)
        {
            var why = gameFolder == null
                ? "Set the vanilla game folder first: the icons are read from your own copy of the game."
                : $"No {ItemIconCatalog.ArchiveRelativePath} in {gameFolder}.";
            SetStatus(why);
            return why;
        }

        await _extractLock.WaitAsync();
        _extracting = true;
        RaiseChanged();
        try
        {
            int count = await Task.Run(() => ItemIconExtractor.Extract(gameFolder, CacheDirectory, force));
            _available = ItemIconExtractor.IsCurrent(CacheDirectory);
            Logger.Information("[icons] extracted {Count} item icons from {Folder} into {Cache}", count, gameFolder, CacheDirectory);
            SetStatus(DescribeCache());
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[icons] extracting item icons from {Folder} failed", gameFolder);
            _available = ItemIconExtractor.IsCurrent(CacheDirectory);
            var error = $"Couldn't read the item icons: {ex.Message}";
            SetStatus(error);
            return error;
        }
        finally
        {
            _extracting = false;
            _extractLock.Release();
            RaiseChanged();
        }
    }

    private void OnPatchFinished(string outputPath, string? error)
    {
        // A successful patch wrote a complete game into outputPath: take the icons from it if the cache
        // is missing or out of date (IsAvailable is false for both).
        if (error != null || _available) return;
        _ = ExtractAsync(outputPath, force: false);
    }

    private string DescribeCache() => ItemIconExtractor.ReadStamp(CacheDirectory) switch
    {
        { Version: ItemIconCatalog.Version, Count: > 0 } stamp => $"{stamp.Count} item icons, read from your game files",
        { } => "The item icons are from an older version and will be read again from your game files.",
        null => "Not read yet. They come from your own game files once the vanilla game folder is set.",
    };

    private void SetStatus(string status)
    {
        Volatile.Write(ref _status, status);
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        try { Changed?.Invoke(); }
        catch (Exception ex) { Logger.Error(ex, "[icons] a Changed handler failed"); }
    }

    public void Dispose()
    {
        if (_patcher != null) _patcher.PatchFinished -= OnPatchFinished;
        GC.SuppressFinalize(this);
    }
}
