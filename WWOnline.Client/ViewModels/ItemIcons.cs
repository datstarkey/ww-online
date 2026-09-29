using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using WWOnline.Patcher.Icons;
using WWOnline.Services;

namespace WWOnline.ViewModels;

/// <summary>
/// The UI side of <see cref="ItemIconService"/>: loaded bitmaps, and a few named icons the views bind
/// directly (<c>{Binding Icons.Compass}</c>). Every getter returns null until the icons are extracted,
/// and the views then show their placeholder (code square, vector shape or text) instead, so a fresh
/// install with no game folder still looks right. <see cref="Refreshed"/> (UI thread) tells the view
/// models holding per-item icons to fetch them again.
/// </summary>
public sealed partial class ItemIcons : ObservableObject, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ItemIcons>();

    private readonly ItemIconService _service;
    private readonly Action<Action> _post;
    private readonly Dictionary<string, IImage?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, IImage?> _load;

    public ItemIcons(ItemIconService service) : this(service, a => Dispatcher.UIThread.Post(a), LoadBitmap) { }

    /// <param name="post">Runs an action on the UI thread (<c>a => a()</c> in tests).</param>
    /// <param name="load">Loads a PNG (tests pass a fake: a Bitmap needs a renderer).</param>
    public ItemIcons(ItemIconService service, Action<Action> post, Func<string, IImage?> load)
    {
        _service = service;
        _post = post;
        _load = load;
        _service.Changed += OnServiceChanged;
    }

    /// <summary>Raised on the UI thread after the icon set changed (extracted or replaced).</summary>
    public event Action? Refreshed;

    public bool IsAvailable => _service.IsAvailable;

    /// <summary>The icon for an item number (dItemNo), or null (not extracted, or no icon).</summary>
    public IImage? Get(byte itemNo) =>
        ItemIconCatalog.TextureForItem(itemNo) is { } texture ? GetTexture(texture) : null;

    public IImage? GetTexture(string texture)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(texture, out var cached)) return cached;
            IImage? image = null;
            if (_service.GetTexturePath(texture) is { } path)
            {
                try { image = _load(path); }
                catch (Exception ex) { Logger.Debug(ex, "[icons] couldn't load {Path}", path); }
            }
            // Only hits are remembered: a miss (not extracted yet, or another client swapping the
            // folder that moment) is looked up again next time.
            if (image != null) _cache[texture] = image;
            return image;
        }
    }

    // Named icons the views bind directly (the upcoming Dungeons card uses the dungeon ones).
    public IImage? Rupee => Get(ItemIconCatalog.ItemNo.GreenRupee);
    public IImage? HeartContainer => Get(ItemIconCatalog.ItemNo.HeartContainer);
    public IImage? HeartPiece => Get(ItemIconCatalog.ItemNo.HeartPiece);
    public IImage? Song => Get(ItemIconCatalog.ItemNo.WindsRequiem);
    public IImage? SmallKey => Get(ItemIconCatalog.ItemNo.SmallKey);
    public IImage? DungeonMap => Get(ItemIconCatalog.ItemNo.DungeonMap);
    public IImage? Compass => Get(ItemIconCatalog.ItemNo.Compass);
    public IImage? BossKey => Get(ItemIconCatalog.ItemNo.BossKey);

    private void OnServiceChanged()
    {
        // Mid-extraction the old files are about to be swapped out: rebuild once it has finished.
        if (_service.IsExtracting) return;
        _post(() =>
        {
            lock (_cache) _cache.Clear();
            OnPropertyChanged(string.Empty); // every named icon
            Refreshed?.Invoke();
        });
    }

    private static IImage? LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        return new Bitmap(stream);
    }

    public void Dispose() => _service.Changed -= OnServiceChanged;
}
