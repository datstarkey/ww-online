using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.BinaryFormats.Yaz0;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

/// <summary>
/// All of RELS.arc is loaded into ARAM for the session. Replacing the puppet REL must not
/// decompress the other RELs (that doubled the archive and made the first play scene's
/// LkD00.arc ARAM mount fail: "Failed assertion d_s_play.cpp:3439").
/// </summary>
public class RelsArcReplaceTests
{
    private const uint PuppetRelModuleId = 0x58;

    /// <summary>
    /// Nintendo's files are not in the repo: the vanilla RELS.arc comes from the vanilla game
    /// configured in GameMod/config.json (vanilla_game_path, fallback GameMod/vanilla/).
    /// </summary>
    internal static string? FindVanillaRelsArc() =>
        TestRepo.VanillaGamePath() is { } game ? Path.Combine(game, "files", "RELS.arc") : null;

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

    /// <summary>
    /// The puppet REL is stored Yaz0 like the vanilla entry it replaces (about half the ARAM of an
    /// uncompressed copy), and reads back byte for byte.
    /// </summary>
    [VanillaRelsArcFact]
    public void ReplaceRelById_KeepsTheReplacedEntryCompressed()
    {
        var arc = new RarcArchive();
        arc.Read(File.ReadAllBytes(FindVanillaRelsArc()!));

        var fakeRel = new byte[0x4000];
        fakeRel[3] = (byte)PuppetRelModuleId;
        for (int i = 4; i < fakeRel.Length; i++) fakeRel[i] = (byte)(i * 7 % 13);
        Assert.True(arc.ReplaceRelById(PuppetRelModuleId, fakeRel));

        var reread = new RarcArchive();
        reread.Read(arc.SaveChanges());
        var entry = reread.FileEntries.Single(e => !e.IsDir && e.PeekDecompressedData() is { Length: > 4 } d && BigEndianIO.ReadU32(d, 0) == PuppetRelModuleId);
        Assert.True(Yaz0Codec.CheckIsCompressed(entry.Data!));
        Assert.True(entry.Type.HasFlag(RarcFileAttrType.COMPRESSED | RarcFileAttrType.YAZ0_COMPRESSED));
        Assert.Equal(fakeRel, entry.PeekDecompressedData());
    }

    [Fact]
    public void ReplaceContents_KeepsTheEntrysForm()
    {
        var contents = Enumerable.Range(0, 256).Select(i => (byte)(i % 16)).ToArray();

        var compressed = new RarcFileEntry { Data = new Yaz0Codec().Compress(new byte[64]) };
        compressed.ReplaceContents(contents);
        Assert.True(Yaz0Codec.CheckIsCompressed(compressed.Data!));
        Assert.Equal(contents, compressed.PeekDecompressedData());

        var plain = new RarcFileEntry { Data = new byte[64] };
        plain.ReplaceContents(contents);
        Assert.Same(contents, plain.Data);
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
