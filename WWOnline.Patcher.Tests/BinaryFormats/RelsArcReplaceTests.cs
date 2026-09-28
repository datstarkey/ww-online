using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.BinaryFormats.Yaz0;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

/// <summary>
/// RELS.arc is loaded into ARAM, which is nearly full in vanilla. Replacing the puppet REL must
/// not decompress the other RELs (that doubled the archive and made the first play scene's
/// LkD00.arc ARAM mount fail: "Failed assertion d_s_play.cpp:3439").
/// </summary>
public class RelsArcReplaceTests
{
    private const uint PuppetRelModuleId = 0x58;

    /// <summary>
    /// Nintendo's files are not in the repo: the vanilla RELS.arc comes from the vanilla game
    /// configured in GameMod/config.json (vanilla_game_path, fallback GameMod/vanilla/).
    /// </summary>
    internal static string? FindVanillaRelsArc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var gameMod = Path.Combine(dir.FullName, "GameMod");
            if (!File.Exists(Path.Combine(gameMod, PatcherConfig.ConfigFileName))) continue;
            try
            {
                var path = PatcherConfig.LoadFromConfigJson(gameMod).VanillaRelsArcPath;
                return File.Exists(path) ? path : null;
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or KeyNotFoundException)
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>A [Fact] that is reported as skipped when no vanilla RELS.arc is configured.</summary>
    private sealed class VanillaRelsArcFactAttribute : FactAttribute
    {
        public VanillaRelsArcFactAttribute()
        {
            if (FindVanillaRelsArc() == null)
                Skip = "Vanilla RELS.arc not found: set vanilla_game_path in GameMod/config.json";
        }
    }

    [VanillaRelsArcFact]
    public void ReplaceRelById_KeepsOtherRelsCompressed()
    {
        var path = FindVanillaRelsArc()!;

        var original = File.ReadAllBytes(path);
        var arc = new RarcArchive();
        arc.Read(original);
        var compressedBefore = arc.FileEntries.Count(e => !e.IsDir && e.Data != null && Yaz0Codec.CheckIsCompressed(e.Data));

        var fakeRel = new byte[0x4000];
        fakeRel[3] = (byte)PuppetRelModuleId; // big-endian module ID in the first u32

        Assert.True(arc.ReplaceRelById(PuppetRelModuleId, fakeRel));

        var compressedAfter = arc.FileEntries.Count(e => !e.IsDir && e.Data != null && Yaz0Codec.CheckIsCompressed(e.Data));
        Assert.True(compressedAfter >= compressedBefore - 1,
            $"only the replaced REL may lose compression ({compressedBefore} -> {compressedAfter} compressed entries)");

        var saved = arc.SaveChanges();
        Assert.True(saved.Length < original.Length + 0x10000,
            $"RELS.arc grew from {original.Length} to {saved.Length} bytes — other RELs were probably decompressed");
    }

    [VanillaRelsArcFact]
    public void ReplaceRelById_UnknownId_ReturnsFalse()
    {
        var path = FindVanillaRelsArc()!;

        var arc = new RarcArchive();
        arc.Read(File.ReadAllBytes(path));
        Assert.False(arc.ReplaceRelById(0xFFFF, new byte[16]));
    }
}
