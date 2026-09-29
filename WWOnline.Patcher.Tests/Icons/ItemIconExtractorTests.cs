using WWOnline.Patcher.BinaryFormats.Bti;
using WWOnline.Patcher.BinaryFormats.Png;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.Icons;
using Xunit;

namespace WWOnline.Patcher.Tests.Icons;

public sealed class ItemIconExtractorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-icons-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static readonly (string, byte[])[] TwoIcons =
    [
        ("telescope.bti", SyntheticIcons.C8(0xFC00)), // opaque red
        ("baton.bti", SyntheticIcons.Ia4White()),      // tinted
    ];

    [Fact]
    public void DecodeArchive_DecodesEveryBti_AndTintsTheIa4Ones()
    {
        var icons = ItemIconExtractor.DecodeArchive(SyntheticIcons.Archive([.. TwoIcons, ("readme.txt", new byte[] { 1, 2, 3 })]));

        Assert.Equal(["baton", "telescope"], icons.Keys.Order());
        Assert.All(icons.Values, png => Assert.True(PngWriter.HasGameAssetMarker(png)));

        var (w, h, rgba) = SyntheticIcons.ReadPng(icons["telescope"]);
        Assert.Equal((8, 4), (w, h));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rgba[..4]);

        uint tint = ItemIconCatalog.Tints["baton"];
        (_, _, rgba) = SyntheticIcons.ReadPng(icons["baton"]);
        Assert.Equal(new[] { (byte)(tint >> 16), (byte)(tint >> 8), (byte)tint, (byte)255 }, rgba[..4]);
    }

    [Fact]
    public void Extract_WritesPngsAndAStamp()
    {
        var game = SyntheticIcons.GameFolder(Path.Combine(_dir, "game"), TwoIcons);
        var cache = Path.Combine(_dir, "GameIcons");

        Assert.False(ItemIconExtractor.IsCurrent(cache));
        Assert.Equal(2, ItemIconExtractor.Extract(game, cache));

        Assert.True(File.Exists(Path.Combine(cache, "telescope.png")));
        Assert.True(File.Exists(Path.Combine(cache, "baton.png")));
        Assert.True(ItemIconExtractor.IsCurrent(cache));
        var stamp = ItemIconExtractor.ReadStamp(cache)!;
        Assert.Equal(ItemIconCatalog.Version, stamp.Version);
        Assert.Equal(2, stamp.Count);
        // No staging folders left beside the cache.
        Assert.Equal([cache], Directory.GetDirectories(_dir).Where(d => !d.EndsWith("game")));
    }

    [Fact]
    public void Extract_ReplacesTheOldSet()
    {
        var cache = Path.Combine(_dir, "GameIcons");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "stale.png"), "old");

        ItemIconExtractor.Extract(SyntheticIcons.GameFolder(Path.Combine(_dir, "game"), TwoIcons), cache);

        Assert.False(File.Exists(Path.Combine(cache, "stale.png")));
    }

    [Fact]
    public void Extract_WithoutTheArchive_ThrowsAndKeepsTheOldIcons()
    {
        var cache = Path.Combine(_dir, "GameIcons");
        ItemIconExtractor.Extract(SyntheticIcons.GameFolder(Path.Combine(_dir, "game"), TwoIcons), cache);

        Assert.Throws<FileNotFoundException>(() => ItemIconExtractor.Extract(Path.Combine(_dir, "nothing"), cache));
        Assert.True(ItemIconExtractor.IsCurrent(cache));
    }

    [Fact]
    public void Extract_BadArchive_ThrowsAndKeepsTheOldIcons()
    {
        var cache = Path.Combine(_dir, "GameIcons");
        ItemIconExtractor.Extract(SyntheticIcons.GameFolder(Path.Combine(_dir, "game"), TwoIcons), cache);
        var bad = Path.Combine(_dir, "bad");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(bad, ItemIconCatalog.ArchiveRelativePath))!);
        File.WriteAllBytes(Path.Combine(bad, ItemIconCatalog.ArchiveRelativePath), new byte[64]);

        Assert.ThrowsAny<Exception>(() => ItemIconExtractor.Extract(bad, cache));
        Assert.True(File.Exists(Path.Combine(cache, "telescope.png")));
    }

    [Fact]
    public void Extract_NotForced_KeepsACurrentCache_WithoutReadingTheGame()
    {
        var cache = Path.Combine(_dir, "GameIcons");
        ItemIconExtractor.Extract(SyntheticIcons.GameFolder(Path.Combine(_dir, "game"), TwoIcons), cache);

        // Another client already filled it: no archive needed, the cache stays as it is.
        Assert.Equal(2, ItemIconExtractor.Extract(Path.Combine(_dir, "nothing"), cache, force: false));
        Assert.True(ItemIconExtractor.IsCurrent(cache));
    }

    [Fact]
    public void Extract_FromManyThreadsAtOnce_EndsWithOneCompleteSet()
    {
        var game = SyntheticIcons.GameFolder(Path.Combine(_dir, "game"), TwoIcons);
        var cache = Path.Combine(_dir, "GameIcons");

        Parallel.For(0, 6, i => ItemIconExtractor.Extract(game, cache, force: i % 2 == 0));

        Assert.True(ItemIconExtractor.IsCurrent(cache));
        Assert.Equal(3, Directory.GetFiles(cache).Length); // 2 icons + the stamp
        Assert.Equal([cache], Directory.GetDirectories(_dir).Where(d => !d.EndsWith("game")));
    }

    [Fact]
    public void RemoveLeftovers_DeletesStagingAndOldFolders_Only()
    {
        var cache = Path.Combine(_dir, "GameIcons");
        ItemIconExtractor.Extract(SyntheticIcons.GameFolder(Path.Combine(_dir, "game"), TwoIcons), cache);
        var staging = Directory.CreateDirectory(cache + ItemIconExtractor.StagingInfix + "deadbeef").FullName;
        var trash = Directory.CreateDirectory(cache + ItemIconExtractor.TrashInfix + "cafef00d").FullName;
        File.WriteAllText(Path.Combine(staging, "half.png"), "x");
        var unrelated = Directory.CreateDirectory(Path.Combine(_dir, "GameIconsOther")).FullName;

        ItemIconExtractor.RemoveLeftovers(cache);

        Assert.False(Directory.Exists(staging));
        Assert.False(Directory.Exists(trash));
        Assert.True(Directory.Exists(unrelated));
        Assert.True(ItemIconExtractor.IsCurrent(cache));
    }

    [Fact]
    public void Extract_OversizedArchive_IsRefused()
    {
        var game = Path.Combine(_dir, "huge");
        var arc = Path.Combine(game, ItemIconCatalog.ArchiveRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(arc)!);
        using (var f = File.Create(arc)) f.SetLength(ItemIconExtractor.MaxArchiveSize + 1);

        Assert.Throws<InvalidDataException>(() => ItemIconExtractor.Extract(game, Path.Combine(_dir, "GameIcons")));
    }

    [Fact]
    public void FindArchive_NullForMissingOrBlankFolders()
    {
        Assert.Null(ItemIconExtractor.FindArchive(null));
        Assert.Null(ItemIconExtractor.FindArchive(""));
        Assert.Null(ItemIconExtractor.FindArchive(Path.Combine(_dir, "none")));
    }

    [Fact]
    public void ReadStamp_NullForGarbage()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, ItemIconExtractor.StampFileName), "{not json");
        Assert.Null(ItemIconExtractor.ReadStamp(_dir));
        Assert.False(ItemIconExtractor.IsCurrent(_dir));
    }

    // ── The real game (skipped unless GameMod/config.json points at a vanilla game) ──

    [VanillaGameFact]
    public void RealArchive_HasEveryTextureTheCatalogueNames_AndAllDecode()
    {
        var archive = ItemIconExtractor.FindArchive(TestRepo.VanillaGamePath());
        Assert.NotNull(archive);
        var arc = RarcArchive.FromFile(archive);
        var names = arc.FileEntries.Where(e => !e.IsDir).Select(e => Path.GetFileNameWithoutExtension(e.Name)).ToHashSet();

        var missing = ItemIconCatalog.AllTextures.Concat(ItemIconCatalog.Tints.Keys).Where(t => !names.Contains(t)).ToList();
        Assert.Empty(missing);

        foreach (var entry in arc.FileEntries.Where(e => !e.IsDir && e.Name.EndsWith(".bti")))
        {
            var tex = new BtiTexture(entry.PeekDecompressedData()!);
            Assert.Equal(tex.Width * tex.Height * 4, tex.DecodeRgba().Length);
        }
        // The tints are for grey textures only.
        foreach (var t in ItemIconCatalog.Tints.Keys)
            Assert.Equal(GxTextureFormat.IA4, new BtiTexture(arc.GetFileEntry(t + ".bti")!.PeekDecompressedData()!).Format);
    }
}
