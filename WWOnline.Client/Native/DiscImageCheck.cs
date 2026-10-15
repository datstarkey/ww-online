using System.Security.Cryptography;
using System.Text;

namespace WWOnline.Native;

/// <summary>The outcome of <see cref="DiscImageCheck.VerifyAsync"/>: OK, or what is wrong with the file.</summary>
public sealed record DiscCheck(bool Ok, string Message);

/// <summary>
/// Is this the disc image the native game runs from: The Wind Waker, USA (GZLE01), revision 0, as a plain
/// .iso? Checked by the image's SHA-1 against the hashes tww-native accepts (native/tools/disc_manifest.py,
/// SUPPORTED and ALT_ISO_SHA1): the only disc-derived values here, and they identify the disc, not its data.
/// </summary>
public static class DiscImageCheck
{
    public const string GameId = "GZLE01";

    /// <summary>The plain GZLE01 revision 0 .iso, and the No-Intro/Redump-set dump of the same disc.</summary>
    public static readonly IReadOnlySet<string> AcceptedSha1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "0289e70f470dc758c73be9b8bcd8c865fb82d8ae",
        "6b5f06c10d50ebb4099cded88217eb71e5bfbb4a",
    };

    /// <param name="progress">The share of the image hashed so far, 0..1.</param>
    /// <param name="accepted">The image hashes to accept (<see cref="AcceptedSha1"/>; tests pass their own).</param>
    public static async Task<DiscCheck> VerifyAsync(string path, IProgress<double>? progress = null,
        CancellationToken ct = default, IReadOnlySet<string>? accepted = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new(false, "That file doesn't exist.");
        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
            var header = new byte[8];
            if (await file.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct) < header.Length)
                return new(false, "That file is too small to be a disc image.");
            string id = Encoding.ASCII.GetString(header, 0, 6);
            if (id != GameId)
                return new(false, $"That isn't The Wind Waker (USA): its disc ID is {Printable(id)}, not {GameId}. " +
                                  "RVZ, GCZ, CISO and NKit images must be converted to a plain .iso first (Dolphin: Convert File).");
            if (header[7] != 0)
                return new(false, $"That is revision {header[7]} of the disc; the native game needs revision 0.");

            file.Position = 0;
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            var buffer = new byte[4 << 20];
            long done = 0, length = file.Length;
            int read;
            while ((read = await file.ReadAsync(buffer, ct)) > 0)
            {
                sha1.AppendData(buffer, 0, read);
                done += read;
                progress?.Report(length > 0 ? (double)done / length : 1);
            }
            string hash = Convert.ToHexStringLower(sha1.GetHashAndReset());
            return (accepted ?? AcceptedSha1).Contains(hash)
                ? new(true, $"{GameId} revision 0, verified.")
                : new(false, $"This {GameId} image doesn't match the known good dump (SHA-1 {hash}). " +
                             "Use a plain, unmodified .iso of the disc.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(false, $"Couldn't read the file: {ex.Message}");
        }
    }

    private static string Printable(string id) =>
        id.All(c => c is >= ' ' and <= '~') ? $"\"{id}\"" : "unreadable";
}
