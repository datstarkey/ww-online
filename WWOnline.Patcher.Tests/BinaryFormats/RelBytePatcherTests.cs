using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.BinaryFormats.Rel;
using WWOnline.Patcher.BinaryFormats.Yaz0;
using WWOnline.Patcher.Patches;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

public class RelBytePatcherTests
{
    // Section 1 (code) is at 0x64..0xA4. Module-0 relocations (positions are file offsets):
    //   0x74 R_PPC_REL24      -> 0x80001000
    //   0x86 R_PPC_ADDR16_HA  -> 0x80002000
    //   (R_DOLPHIN_NOP advancing to 0x8A)
    //   0x90 R_PPC_REL24      -> 0x80003000
    private const int RelocTable = 0xAC;

    private static byte[] MakeRel()
    {
        var d = new byte[0x100];
        BigEndianIO.WriteU32(d, 0x00, 5);
        BigEndianIO.WriteU32(d, 0x0C, 3);
        BigEndianIO.WriteU32(d, 0x10, 0x4C);
        BigEndianIO.WriteU32(d, 0x4C + 8, 0x64 | 1); BigEndianIO.WriteU32(d, 0x4C + 12, 0x40); // .text
        BigEndianIO.WriteU32(d, 0x4C + 20, 0x10);                                              // .bss
        for (int i = 0x64; i < 0xA4; i += 4) BigEndianIO.WriteU32(d, i, 0x48000001);          // bl placeholders
        BigEndianIO.WriteU32(d, 0x28, 0xA4);
        BigEndianIO.WriteU32(d, 0x2C, 8);
        BigEndianIO.WriteU32(d, 0xA4, 0);
        BigEndianIO.WriteU32(d, 0xA8, RelocTable);
        int e = RelocTable;
        void Entry(ushort delta, RelRelocationType type, byte section, uint addend)
        {
            BigEndianIO.WriteU16(d, e, delta); d[e + 2] = (byte)type; d[e + 3] = section; BigEndianIO.WriteU32(d, e + 4, addend); e += 8;
        }
        Entry(0, RelRelocationType.R_DOLPHIN_SECTION, 1, 0);
        Entry(0x10, RelRelocationType.R_PPC_REL24, 0, 0x80001000);
        Entry(0x12, RelRelocationType.R_PPC_ADDR16_HA, 0, 0x80002000);
        Entry(0x04, RelRelocationType.R_DOLPHIN_NOP, 0, 0);
        Entry(0x06, RelRelocationType.R_PPC_REL24, 0, 0x80003000);
        Entry(0, RelRelocationType.R_DOLPHIN_END, 0, 0);
        return d;
    }

    private static RelRelocationType TypeOf(byte[] rel, int entryIndex) => (RelRelocationType)rel[RelocTable + entryIndex * 8 + 2];
    private static uint AddendOf(byte[] rel, int entryIndex) => BigEndianIO.ReadU32(rel, RelocTable + entryIndex * 8 + 4);

    private static PatchChunk Nop(uint at) => new(at, [0x60, 0, 0, 0], []);

    [Fact]
    public void NopOverARelocatedCall_DisablesThatRelocationOnly()
    {
        var rel = MakeRel();
        var (disabled, retargeted) = RelBytePatcher.ApplyUncompressed(rel, [Nop(0x74)]);

        Assert.Equal((1, 0), (disabled, retargeted));
        Assert.Equal(0x60000000u, BigEndianIO.ReadU32(rel, 0x74));
        Assert.Equal(RelRelocationType.R_DOLPHIN_NOP, TypeOf(rel, 1));
        Assert.Equal(RelRelocationType.R_PPC_ADDR16_HA, TypeOf(rel, 2));
        Assert.Equal(RelRelocationType.R_PPC_REL24, TypeOf(rel, 4));
    }

    [Fact]
    public void Addr16Relocation_OnlyCoversTwoBytes()
    {
        var rel = MakeRel();
        Assert.Equal((0, 0), RelBytePatcher.ApplyUncompressed(rel, [Nop(0x88)]));
        Assert.Equal((1, 0), RelBytePatcher.ApplyUncompressed(rel, [Nop(0x84)]));
        Assert.Equal(RelRelocationType.R_DOLPHIN_NOP, TypeOf(rel, 2));
    }

    [Fact]
    public void CallIntoMainDol_RetargetsTheExistingRelocation()
    {
        var rel = MakeRel();
        var chunk = new PatchChunk(0x90, [0x48, 0, 0, 1], [new PatchRelocation(0, "R_PPC_REL24", "helper", 0x803FD200)]);
        Assert.Equal((0, 1), RelBytePatcher.ApplyUncompressed(rel, [chunk]));
        Assert.Equal(RelRelocationType.R_PPC_REL24, TypeOf(rel, 4));
        Assert.Equal(0x803FD200u, AddendOf(rel, 4));
    }

    [Fact]
    public void CallWithoutAnExistingRelocation_Throws()
    {
        var chunk = new PatchChunk(0x98, [0x48, 0, 0, 1], [new PatchRelocation(0, "R_PPC_REL24", "helper", 0x803FD200)]);
        Assert.Throws<InvalidOperationException>(() => RelBytePatcher.ApplyUncompressed(MakeRel(), [chunk]));
    }

    [Fact]
    public void ChunkOutsideAnInitialisedSection_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RelBytePatcher.ApplyUncompressed(MakeRel(), [Nop(0x40)]));
        Assert.Throws<InvalidOperationException>(() => RelBytePatcher.ApplyUncompressed(MakeRel(), [Nop(0xA2)]));
    }

    [Fact]
    public void CompressedRel_StaysCompressed()
    {
        var compressed = new Yaz0Codec().Compress(MakeRel());
        var result = RelBytePatcher.Apply(compressed, [Nop(0x74)]);
        Assert.True(Yaz0Codec.CheckIsCompressed(result.Data));
        Assert.Equal(0x60000000u, BigEndianIO.ReadU32(Yaz0Codec.Decompress(result.Data), 0x74));
    }

    [VanillaGameFact]
    public void VanillaShipRel_BalladNop_DisablesTheMusicCallRelocation()
    {
        var arc = RarcArchive.FromFile(Path.Combine(TestRepo.VanillaGamePath()!, "files", "RELS.arc"));
        var entry = arc.GetFileEntry("d_a_ship.rel")!;
        var before = Yaz0Codec.Decompress(entry.Data!);

        var result = RelBytePatcher.Apply(entry.Data!, [Nop(0x7680), Nop(0x7A10)]);
        var after = Yaz0Codec.Decompress(result.Data);

        Assert.Equal(1, result.RelocationsDisabled); // the bl at 0x7680 (warp music) had a main.dol REL24
        Assert.Equal(before.Length, after.Length);
        var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
        // Only the two instructions and the one relocation entry's type byte change.
        // d_a_ship.rel's last section ends at 0xE3A8; the relocation table follows.
        Assert.All(changed, i => Assert.True(i is >= 0x7680 and < 0x7684 or >= 0x7A10 and < 0x7A14 or >= 0xE3A8, $"unexpected change at 0x{i:X}"));
        Assert.Single(changed, i => i >= 0xE3A8);
        Assert.True(result.Data.Length <= entry.Data!.Length + 0x100, "re-compressed REL should stay about the same size");
    }
}
