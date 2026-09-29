using WWOnline.Patcher.Pipeline;

namespace WWOnline.Services;

/// <summary>What <see cref="GameFolderCheck.CheckVanilla"/> found.</summary>
/// <param name="Error">Why the folder can't be used (null = usable).</param>
/// <param name="Note">Worth telling the player, but not a problem (e.g. the game ID couldn't be read).</param>
/// <param name="GameId">The disc's game ID from sys/boot.bin ("GZLE01"), or null when unknown.</param>
/// <param name="GameFolder">A folder inside the chosen one that holds the game (the player picked its
/// parent), else null.</param>
public sealed record VanillaFolderCheck(string? Error, string? Note, string? GameId, string? GameFolder)
{
    public bool IsValid => Error == null;
}

/// <summary>
/// Checks the two game folders the player chooses: the extracted original game (read only) and the
/// folder the patched copy goes into. One implementation for the setup wizard and the patcher.
///
/// The app patches an extracted game (a folder with sys/ and files/, from Dolphin's "Extract Entire
/// Disc"). It can't read disc images, so an ISO/RVZ/... gets an explanation instead of "not found".
/// </summary>
public static class GameFolderCheck
{
    /// <summary>The only version WW-Online supports: The Wind Waker, NTSC-U GameCube.</summary>
    public const string SupportedGameId = "GZLE01";

    /// <summary>How to get a folder from a disc image, for error messages and the setup wizard.</summary>
    public const string ExtractHint =
        "In Dolphin, right-click the game → Properties → Filesystem, right-click the disc at the top → Extract Entire Disc…, and choose an empty folder.";

    private static readonly string[] DiscImageExtensions =
        [".iso", ".gcm", ".rvz", ".ciso", ".gcz", ".wia", ".nkit", ".wbfs", ".tgc", ".dol"];

    /// <summary>Is <paramref name="path"/> the extracted, unmodified US Wind Waker?</summary>
    public static VanillaFolderCheck CheckVanilla(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Fail("Choose the folder you extracted the game into.");
        path = path.Trim();

        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return DiscImageExtensions.Contains(ext)
                ? Fail($"That's a {(ext == ".dol" ? "game file" : "disc image")}, not a folder. WW-Online needs the whole game extracted to a folder. {ExtractHint}")
                : Fail("That's a file. Choose the folder the game was extracted into (it contains sys and files).");
        }

        if (!Directory.Exists(path))
            return Fail($"Folder not found: {path}");

        if (!File.Exists(Path.Combine(path, "sys", "main.dol")))
        {
            var inner = FindGameFolderInside(path);
            return inner != null
                ? new VanillaFolderCheck($"The game is in a folder inside this one: {inner}", null, null, inner)
                : Fail($"No game found here: expected sys{Path.DirectorySeparatorChar}main.dol. {ExtractHint}");
        }

        if (File.Exists(BuildStamp.GetStampPath(path)))
            return Fail("This is a copy WW-Online already patched. Choose the original extracted game instead.");

        if (!File.Exists(Path.Combine(path, "files", "RELS.arc")))
            return Fail($"RELS.arc not found in {Path.Combine(path, "files")}. Extract the entire disc, not just some files.");
        if (!File.Exists(Path.Combine(path, "sys", "bi2.bin")))
            return Fail($"bi2.bin not found in {Path.Combine(path, "sys")}. Extract the entire disc, not just some files.");

        var id = ReadGameId(path);
        if (id == null)
            return new VanillaFolderCheck(null,
                $"Couldn't read the game ID (sys{Path.DirectorySeparatorChar}boot.bin is missing). Make sure this is the US version, {SupportedGameId}.",
                null, null);
        if (!string.Equals(id, SupportedGameId, StringComparison.Ordinal))
            return new VanillaFolderCheck(
                $"This is {DescribeGameId(id)} (game ID {id}). WW-Online only works with the US GameCube version, {SupportedGameId}.",
                null, id, null);

        return new VanillaFolderCheck(null, null, id, null);

        static VanillaFolderCheck Fail(string error) => new(error, null, null, null);
    }

    /// <summary>
    /// Can the patched game go in <paramref name="patchedPath"/>? It must be a folder of its own: the
    /// patch writes sys/ and files/ there, so it may not be (or be inside, or contain) the original.
    /// </summary>
    /// <returns>An error, or null when it's fine.</returns>
    public static string? CheckPatchedFolder(string? vanillaPath, string? patchedPath)
    {
        if (string.IsNullOrWhiteSpace(patchedPath))
            return "Choose a folder for the patched game.";
        if (File.Exists(patchedPath.Trim()))
            return "That's a file. Choose a folder for the patched game.";

        string patched;
        try { patched = Normalize(patchedPath); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "That isn't a valid folder path.";
        }

        if (!string.IsNullOrWhiteSpace(vanillaPath))
        {
            string vanilla;
            try { vanilla = Normalize(vanillaPath); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }

            if (IsSameOrInside(patched, vanilla) || IsSameOrInside(vanilla, patched) || FileIdentity.SameFile(
                    Path.Combine(vanilla, "sys", "main.dol"), Path.Combine(patched, "sys", "main.dol")))
                return "Use a different folder from the original game: patching writes into this folder, and the original must stay untouched.";
        }
        return null;
    }

    /// <summary>A folder next to the original game for the patched copy ("…\WindWaker" → "…\WindWaker-WWOnline").</summary>
    public static string? SuggestPatchedFolder(string? vanillaPath)
    {
        if (string.IsNullOrWhiteSpace(vanillaPath)) return null;
        try
        {
            var full = Normalize(vanillaPath);
            var parent = Path.GetDirectoryName(full);
            var name = Path.GetFileName(full);
            return parent == null || name.Length == 0 ? null : Path.Combine(parent, name + "-WWOnline");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>The game ID in sys/boot.bin (its first 6 bytes), or null when it can't be read.</summary>
    public static string? ReadGameId(string gameFolder)
    {
        try
        {
            var bootBin = Path.Combine(gameFolder, "sys", "boot.bin");
            if (!File.Exists(bootBin)) return null;
            using var stream = File.OpenRead(bootBin);
            Span<byte> id = stackalloc byte[6];
            if (stream.Read(id) != id.Length) return null;
            foreach (var b in id)
                if (b < 0x20 || b > 0x7E) return null;
            return System.Text.Encoding.ASCII.GetString(id);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static string DescribeGameId(string id) => id switch
    {
        "GZLP01" => "the European (PAL) version of The Wind Waker",
        "GZLJ01" => "the Japanese version of The Wind Waker",
        _ when id.StartsWith("GZL", StringComparison.Ordinal) => "another version of The Wind Waker",
        _ => "a different game",
    };

    /// <summary>The one subfolder (one level down) that holds sys/main.dol, or null.</summary>
    private static string? FindGameFolderInside(string path)
    {
        try
        {
            var hits = Directory.EnumerateDirectories(path)
                .Where(d => File.Exists(Path.Combine(d, "sys", "main.dol")))
                .Take(2)
                .ToList();
            return hits.Count == 1 ? hits[0] : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

    /// <summary>
    /// Is <paramref name="path"/> <paramref name="folder"/> or inside it? Both are normalised full paths. A root
    /// ("E:\\") keeps its separator after trimming, so it is used as the prefix as is: everything on E: is inside it.
    /// </summary>
    public static bool IsSameOrInside(string path, string folder)
    {
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(path, folder, cmp)) return true;
        var prefix = Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, cmp);
    }
}
