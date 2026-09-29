using Avalonia;
using Avalonia.Media;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Bti;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.Icons;
using WWOnline.Services;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Services;

public sealed class ItemIconServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-iconsvc-" + Guid.NewGuid().ToString("N"));
    private string Cache => Path.Combine(_dir, AppPaths.IconCacheFolderName);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>An 8x4 C8 texture (made up, not game data).</summary>
    private static byte[] Texture()
    {
        var data = new byte[0x20 + 32 + 4];
        data[0x00] = (byte)GxTextureFormat.C8;
        BigEndianIO.WriteU16(data, 0x02, 8);
        BigEndianIO.WriteU16(data, 0x04, 4);
        data[0x08] = 1;
        data[0x09] = (byte)GxPaletteFormat.RGB5A3;
        BigEndianIO.WriteU16(data, 0x0A, 2);
        BigEndianIO.WriteU32(data, 0x0C, 0x40);
        BigEndianIO.WriteU32(data, 0x1C, 0x20);
        BigEndianIO.WriteU16(data, 0x42, 0xFFFF);
        return data;
    }

    /// <summary>A fake extracted game with an itemicon.arc holding "telescope" and "compass".</summary>
    private string Game(string name = "game")
    {
        var folder = Path.Combine(_dir, name);
        var arcPath = Path.Combine(folder, ItemIconCatalog.ArchiveRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(arcPath)!);
        var arc = new RarcArchive();
        var root = new RarcNode { Type = "ROOT", Name = "itemicon" };
        arc.Nodes.Add(root);
        arc.AddNewFile("telescope.bti", Texture(), root);
        arc.AddNewFile("compass.bti", Texture(), root);
        File.WriteAllBytes(arcPath, arc.SaveChanges());
        return folder;
    }

    private sealed class FakeImage : IImage
    {
        public Size Size => new(8, 4);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    [Fact]
    public void NothingExtracted_EverythingIsNull_AndStatusSaysWhy()
    {
        var service = new ItemIconService(null, null, Cache);
        Assert.False(service.IsAvailable);
        Assert.Null(service.GetPath(0x20));
        Assert.Null(service.GetTexturePath("compass"));
        Assert.Contains("Not read yet", service.StatusText);
    }

    [Fact]
    public async Task Extract_WithNoFolder_ReturnsAMessage_AndNeverThrows()
    {
        var service = new ItemIconService(null, null, Cache);
        var error = await service.ExtractAsync();
        Assert.NotNull(error);
        Assert.False(service.IsAvailable);

        error = await service.ExtractAsync(Path.Combine(_dir, "no-game-here"));
        Assert.Contains("itemicon.arc", error);
    }

    [Fact]
    public async Task Extract_MakesThePathsAvailable()
    {
        var service = new ItemIconService(null, null, Cache);
        int changes = 0;
        service.Changed += () => Interlocked.Increment(ref changes);

        Assert.Null(await service.ExtractAsync(Game()));

        Assert.True(service.IsAvailable);
        Assert.False(service.IsExtracting);
        Assert.Equal(Path.Combine(Cache, "telescope.png"), service.GetPath(0x20));
        Assert.Null(service.GetPath(0x2F)); // hookshot: not in this made-up archive
        Assert.Null(service.GetPath(0xFF)); // no item
        Assert.Contains("2 item icons", service.StatusText);
        Assert.True(changes > 0);

        // A new service over the same cache finds it (the next app start).
        Assert.True(new ItemIconService(null, null, Cache).IsAvailable);
    }

    [Fact]
    public async Task Extract_Failing_ShowsWhy_EvenWithIconsPresent()
    {
        var service = new ItemIconService(null, null, Cache);
        await service.ExtractAsync(Game());

        var error = await service.ExtractAsync(Path.Combine(_dir, "no-game-here"));

        Assert.NotNull(error);
        Assert.Equal(error, service.StatusText);
        Assert.True(service.IsAvailable); // the icons already there stay
    }

    [Fact]
    public async Task EnsureExtracted_RemovesLeftoverFolders()
    {
        var leftover = Directory.CreateDirectory(Cache + ItemIconExtractor.StagingInfix + "0badf00d").FullName;
        var service = new ItemIconService(new GameSettingsService(Path.Combine(_dir, "settings")), null, Cache);
        await service.EnsureExtractedAsync();
        Assert.False(Directory.Exists(leftover));
    }

    [Fact]
    public async Task EnsureExtracted_UsesTheVanillaFolderFromSettings()
    {
        var settings = new GameSettingsService(Path.Combine(_dir, "settings"));
        var s = settings.Load();
        s.VanillaGamePath = Game("vanilla");
        s.GamePath = Path.Combine(_dir, "patched-missing");
        settings.Save(s);

        var service = new ItemIconService(settings, null, Cache);
        Assert.Equal(s.VanillaGamePath, service.FindSourceFolder());
        await service.EnsureExtractedAsync();
        Assert.True(service.IsAvailable);
    }

    [Fact]
    public async Task EnsureExtracted_FallsBackToThePatchedFolder()
    {
        var settings = new GameSettingsService(Path.Combine(_dir, "settings"));
        var s = settings.Load();
        s.VanillaGamePath = Path.Combine(_dir, "vanilla-missing");
        s.GamePath = Game("patched");
        settings.Save(s);

        var service = new ItemIconService(settings, null, Cache);
        await service.EnsureExtractedAsync();
        Assert.True(service.IsAvailable);
    }

    [Fact]
    public async Task EnsureExtracted_WithNoGameSet_StaysUnavailable()
    {
        var service = new ItemIconService(new GameSettingsService(Path.Combine(_dir, "settings")), null, Cache);
        await service.EnsureExtractedAsync();
        Assert.False(service.IsAvailable);
        Assert.False(Directory.Exists(Cache));
    }

    [Fact]
    public async Task ItemIcons_FallBackToNull_ThenRefreshOnceExtracted()
    {
        var service = new ItemIconService(null, null, Cache);
        var loaded = new List<string>();
        var icons = new ItemIcons(service, a => a(), path => { loaded.Add(path); return new FakeImage(); });
        int refreshed = 0;
        icons.Refreshed += () => refreshed++;

        // Before extraction: every icon is null, so the views show their placeholders.
        Assert.Null(icons.Get(0x20));
        Assert.Null(icons.Compass);
        Assert.Empty(loaded);

        await service.ExtractAsync(Game());

        Assert.True(refreshed > 0);
        Assert.IsType<FakeImage>(icons.Get(0x20));
        Assert.NotNull(icons.Compass);
        Assert.Null(icons.BossKey); // not in this archive
        Assert.Same(icons.Get(0x20), icons.Get(0x20)); // loaded once
        Assert.Single(loaded, p => p.EndsWith("telescope.png"));
        icons.Dispose();
    }

    [Fact]
    public async Task ItemIcons_AMiss_IsLookedUpAgain()
    {
        // Another client swapping the shared cache can make a file vanish for a moment.
        var service = new ItemIconService(null, null, Cache);
        await service.ExtractAsync(Game());
        var icons = new ItemIcons(service, a => a(), _ => new FakeImage());
        var png = Path.Combine(Cache, "compass.png");
        var bytes = File.ReadAllBytes(png);
        File.Delete(png);

        Assert.Null(icons.Compass);
        File.WriteAllBytes(png, bytes);
        Assert.NotNull(icons.Compass);
    }

    [Fact]
    public void ItemIcons_ALoadFailure_IsAPlaceholderNotACrash()
    {
        Directory.CreateDirectory(Cache);
        ItemIconExtractor.Extract(Game(), Cache);
        var service = new ItemIconService(null, null, Cache);
        var icons = new ItemIcons(service, a => a(), _ => throw new IOException("locked"));
        Assert.Null(icons.Get(0x20));
    }

    [Fact]
    public void IconCache_IsNeverInTheRepoOrTheAppFolder()
    {
        var cache = Path.GetFullPath(AppPaths.IconCacheDirectory);
        Assert.EndsWith(AppPaths.IconCacheFolderName, cache);
        Assert.False(cache.StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every icon the Room items page and the Room page ask for exists in the catalogue.</summary>
    [Fact]
    public void EveryItemTheUiShows_HasAnIcon()
    {
        var missing = RoomItemsViewModel.ItemsShownAsIcons()
            .Concat(RoomItemsViewModel.DefaultSlots.Select(s => s.ItemId))
            .Where(id => ItemIconCatalog.TextureForItem(id) == null)
            .Select(id => $"0x{id:X2}")
            .ToList();
        Assert.Empty(missing);
        Assert.Equal(21, RoomItemsViewModel.DefaultSlots.Count);
    }
}
