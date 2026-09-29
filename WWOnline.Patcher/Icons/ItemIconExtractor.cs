using System.Text.Json;
using System.Text.Json.Serialization;
using WWOnline.Patcher.BinaryFormats.Bti;
using WWOnline.Patcher.BinaryFormats.Png;
using WWOnline.Patcher.BinaryFormats.Rarc;

namespace WWOnline.Patcher.Icons;

/// <summary>What <see cref="ItemIconExtractor.ReadStamp"/> finds in an icon folder.</summary>
public sealed record ItemIconStamp(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("extractedUtc")] DateTime ExtractedUtc);

/// <summary>
/// Decodes the item icons from the player's own copy of the game (<see cref="ItemIconCatalog.ArchiveRelativePath"/>)
/// into PNGs in a local cache folder. Nothing it writes may be committed or shipped: every PNG carries
/// <see cref="PngWriter.GameAssetKeyword"/>, which scripts/check-no-nintendo-files.ps1 rejects.
/// </summary>
public static class ItemIconExtractor
{
    /// <summary>Written last, so a folder with a current stamp is complete.</summary>
    public const string StampFileName = "icons.json";

    /// <summary>The icon archive under an extracted game folder, or null if it isn't there.</summary>
    public static string? FindArchive(string? gameFolder)
    {
        if (string.IsNullOrWhiteSpace(gameFolder)) return null;
        try
        {
            var path = Path.Combine(gameFolder, ItemIconCatalog.ArchiveRelativePath);
            return File.Exists(path) ? path : null;
        }
        catch (ArgumentException)
        {
            return null; // a malformed path from settings
        }
    }

    /// <summary>Decode every BTI in <paramref name="archiveBytes"/> (a RARC, Yaz0 or not): name (no .bti) → PNG bytes.</summary>
    public static Dictionary<string, byte[]> DecodeArchive(byte[] archiveBytes)
    {
        var arc = new RarcArchive();
        arc.Read(archiveBytes);
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in arc.FileEntries)
        {
            if (entry.IsDir || !entry.Name.EndsWith(".bti", StringComparison.OrdinalIgnoreCase)) continue;
            var data = entry.PeekDecompressedData();
            if (data == null) continue;
            var name = Path.GetFileNameWithoutExtension(entry.Name);
            var tex = new BtiTexture(data);
            var rgba = tex.DecodeRgba();
            if (ItemIconCatalog.Tints.TryGetValue(name, out var tint)) Tint(rgba, tint);
            result[name] = PngWriter.Encode(rgba, tex.Width, tex.Height);
        }
        return result;
    }

    /// <summary>Largest itemicon.arc we read (the real one is about 300 KB).</summary>
    public const long MaxArchiveSize = 16 * 1024 * 1024;

    /// <summary>How long <see cref="Extract"/> waits for another process extracting into the same folder.</summary>
    public static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Extract the icons from <paramref name="gameFolder"/> (an extracted game: vanilla or patched,
    /// itemicon.arc is the same in both) into <paramref name="outputDir"/>, replacing what was there.
    /// Returns the number of icons in the folder afterwards. Throws if the archive is missing or unreadable
    /// (the old icons are then kept).
    ///
    /// Safe across processes (dev-test runs two clients on one cache): a named mutex per folder covers
    /// check → extract → swap. Unless <paramref name="force"/>, a current stamp found once the lock is
    /// held (another process just extracted) counts as done. The swap renames the old folder aside
    /// before moving the new one in, so the folder is never half-written, and a reader that loses the
    /// race briefly sees no icons rather than a mix.
    /// </summary>
    public static int Extract(string gameFolder, string outputDir, bool force = true)
    {
        var full = Path.GetFullPath(outputDir);
        using var gate = FolderLock.Acquire(full, LockTimeout)
                         ?? throw new TimeoutException($"Another WW-Online is still writing {full}");

        if (!force && ReadStamp(full) is { Version: ItemIconCatalog.Version, Count: > 0 } current)
            return current.Count;

        var archive = FindArchive(gameFolder)
                      ?? throw new FileNotFoundException($"No {ItemIconCatalog.ArchiveRelativePath} in {gameFolder}");
        if (new FileInfo(archive).Length > MaxArchiveSize)
            throw new InvalidDataException($"{archive} is larger than {MaxArchiveSize} bytes: not the game's icon archive");
        var icons = DecodeArchive(File.ReadAllBytes(archive));
        if (icons.Count == 0) throw new InvalidDataException($"{archive} holds no textures");

        var parent = Path.GetDirectoryName(full) ?? throw new ArgumentException("Bad icon folder", nameof(outputDir));
        Directory.CreateDirectory(parent);
        RemoveLeftoversLocked(full);
        var staging = full + StagingInfix + Guid.NewGuid().ToString("N")[..8];
        var trash = full + TrashInfix + Guid.NewGuid().ToString("N")[..8];
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var (name, png) in icons)
                File.WriteAllBytes(Path.Combine(staging, name + ".png"), png);
            var stamp = new ItemIconStamp(ItemIconCatalog.Version, icons.Count, archive, DateTime.UtcNow);
            File.WriteAllText(Path.Combine(staging, StampFileName), JsonSerializer.Serialize(stamp));

            if (Directory.Exists(full)) Directory.Move(full, trash);
            Directory.Move(staging, full);
        }
        finally
        {
            TryDelete(staging);
            TryDelete(trash);
        }
        return icons.Count;
    }

    /// <summary>
    /// Delete staging / old folders a crashed or killed extraction left beside <paramref name="outputDir"/>
    /// (startup). Skipped while another process holds the folder's lock (it may be using them).
    /// </summary>
    public static void RemoveLeftovers(string outputDir)
    {
        var full = Path.GetFullPath(outputDir);
        using var gate = FolderLock.Acquire(full, TimeSpan.Zero);
        if (gate != null) RemoveLeftoversLocked(full);
    }

    /// <summary>Beside the cache folder: "&lt;name&gt;.new-xxxxxxxx" (being written), "&lt;name&gt;.old-xxxxxxxx" (being deleted).</summary>
    public const string StagingInfix = ".new-";
    public const string TrashInfix = ".old-";

    private static void RemoveLeftoversLocked(string full)
    {
        var parent = Path.GetDirectoryName(full);
        if (parent == null || !Directory.Exists(parent)) return;
        var name = Path.GetFileName(full);
        foreach (var pattern in new[] { name + StagingInfix + "*", name + TrashInfix + "*" })
            foreach (var dir in Directory.EnumerateDirectories(parent, pattern))
                TryDelete(dir);
    }

    private static void TryDelete(string dir)
    {
        if (!Directory.Exists(dir)) return;
        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>A machine-wide named mutex for one cache folder (its full path, case-folded and hashed).</summary>
    private sealed class FolderLock : IDisposable
    {
        private readonly Mutex _mutex;

        private FolderLock(Mutex mutex) => _mutex = mutex;

        /// <summary>The lock, or null if another process still holds it after <paramref name="timeout"/>.</summary>
        public static FolderLock? Acquire(string fullPath, TimeSpan timeout)
        {
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())))[..32];
            var mutex = new Mutex(false, "WWOnline-GameIcons-" + hash);
            try
            {
                if (!mutex.WaitOne(timeout))
                {
                    mutex.Dispose();
                    return null;
                }
            }
            catch (AbandonedMutexException)
            {
                // Its owner died mid-extraction: the lock is ours now, and the leftovers get cleaned.
            }
            return new FolderLock(mutex);
        }

        public void Dispose()
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    /// <summary>The stamp in <paramref name="outputDir"/>, or null if there is none (or it is unreadable).</summary>
    public static ItemIconStamp? ReadStamp(string outputDir)
    {
        try
        {
            var path = Path.Combine(outputDir, StampFileName);
            return File.Exists(path) ? JsonSerializer.Deserialize<ItemIconStamp>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>True when <paramref name="outputDir"/> holds a complete extraction of this <see cref="ItemIconCatalog.Version"/>.</summary>
    public static bool IsCurrent(string outputDir) => ReadStamp(outputDir) is { Version: ItemIconCatalog.Version, Count: > 0 };

    /// <summary>Multiply RGB by <paramref name="rgb"/> (0xRRGGBB); alpha is kept.</summary>
    public static void Tint(byte[] rgba, uint rgb)
    {
        int tr = (int)(rgb >> 16) & 0xFF, tg = (int)(rgb >> 8) & 0xFF, tb = (int)rgb & 0xFF;
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = (byte)(rgba[i] * tr / 255);
            rgba[i + 1] = (byte)(rgba[i + 1] * tg / 255);
            rgba[i + 2] = (byte)(rgba[i + 2] * tb / 255);
        }
    }
}
