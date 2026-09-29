namespace WWOnline.Tests.Services;

/// <summary>
/// A stand-in for an extracted game in a temp folder: the file names the app checks for, with a few
/// dummy bytes each (never game data), and a boot.bin that starts with the given game ID.
/// </summary>
internal static class FakeGameFolder
{
    public static string Create(string dir, string? gameId = "GZLE01", bool withRelsArc = true)
    {
        Directory.CreateDirectory(Path.Combine(dir, "sys"));
        Directory.CreateDirectory(Path.Combine(dir, "files", "Stage"));
        File.WriteAllBytes(Path.Combine(dir, "sys", "main.dol"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(dir, "sys", "bi2.bin"), new byte[16]);
        File.WriteAllBytes(Path.Combine(dir, "files", "Stage", "sea.arc"), [4, 5]);
        if (withRelsArc) File.WriteAllBytes(Path.Combine(dir, "files", "RELS.arc"), [6]);
        if (gameId != null)
            File.WriteAllBytes(Path.Combine(dir, "sys", "boot.bin"), [.. System.Text.Encoding.ASCII.GetBytes(gameId), 0, 0]);
        return dir;
    }
}
