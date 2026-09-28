using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Bmg;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.Patches;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

public class BmgFileTests
{
    private static readonly byte[] Wait = [0x1A, 0x07, 0x00, 0x00, 0x07, 0x00, 0x10];
    private static readonly byte[] Name = [0x1A, 0x05, 0x00, 0x00, 0x00];

    private static byte[] MakeBmg()
    {
        var pool = new List<byte> { 0 };
        var hi = pool.Count; pool.AddRange("Hi"u8.ToArray()); pool.Add(0);
        var second = pool.Count; pool.AddRange(Wait); pool.Add((byte)'A'); pool.AddRange(Name); pool.Add((byte)'B'); pool.Add(0);

        var inf1 = new byte[0x40];
        "INF1"u8.CopyTo(inf1);
        BigEndianIO.WriteU32(inf1, 4, (uint)inf1.Length);
        BigEndianIO.WriteU16(inf1, 8, 2);
        BigEndianIO.WriteU16(inf1, 0x0A, 0x18);
        BigEndianIO.WriteU32(inf1, 0x10, (uint)hi); BigEndianIO.WriteU16(inf1, 0x14, 100); inf1[0x10 + 0x0E] = 3;
        BigEndianIO.WriteU32(inf1, 0x28, (uint)second); BigEndianIO.WriteU16(inf1, 0x2C, 463);

        var dat1 = new byte[(8 + pool.Count + 0x1F) & ~0x1F];
        "DAT1"u8.CopyTo(dat1);
        BigEndianIO.WriteU32(dat1, 4, (uint)dat1.Length);
        pool.ToArray().CopyTo(dat1, 8);

        var file = new byte[0x20 + inf1.Length + dat1.Length];
        "MESGbmg1"u8.CopyTo(file);
        BigEndianIO.WriteU32(file, 8, (uint)(file.Length / 0x20));
        BigEndianIO.WriteU32(file, 0x0C, 2);
        inf1.CopyTo(file, 0x20);
        dat1.CopyTo(file, 0x20 + inf1.Length);
        return file;
    }

    [Fact]
    public void Read_ParsesMessagesWithControlCodes()
    {
        var bmg = new BmgFile(MakeBmg());
        Assert.Equal(2, bmg.Messages.Count);
        Assert.Equal("Hi"u8.ToArray(), bmg.FindById(100)!.Text);
        Assert.Equal([.. Wait, (byte)'A', .. Name, (byte)'B'], bmg.FindById(463)!.Text);
    }

    [Fact]
    public void InstantText_RemovesOnlyWaitCodes_AndRoundTrips()
    {
        var bmg = new BmgFile(MakeBmg());
        foreach (var m in bmg.Messages)
        {
            m.InitialDrawType = 1;
            m.RemoveControlCodes([0x1A, 0x07, 0x00, 0x00, 0x07]);
        }
        var saved = bmg.Save();

        var reread = new BmgFile(saved);
        Assert.Equal([(byte)'A', .. Name, (byte)'B'], reread.FindById(463)!.Text);
        Assert.All(reread.Messages, m => Assert.Equal(1, m.InitialDrawType));
        Assert.Equal((uint)(saved.Length / 0x20), BigEndianIO.ReadU32(saved, 8));
        Assert.Equal(0, saved.Length % 0x20);
    }

    [Fact]
    public void Rename_GrowsTheStringPool()
    {
        var bmg = new BmgFile(MakeBmg());
        bmg.FindById(100)!.Text = "Swift Sail"u8.ToArray();
        var reread = new BmgFile(bmg.Save());
        Assert.Equal("Swift Sail"u8.ToArray(), reread.FindById(100)!.Text);
        Assert.Equal(2, reread.Messages.Count);
    }

    [VanillaGameFact]
    public void VanillaBmg_RoundTripsUnchangedWhenNothingIsEdited()
    {
        var arc = RarcArchive.FromFile(Path.Combine(TestRepo.VanillaGamePath()!, "files", "res", "Msg", "bmgres.arc"));
        var original = arc.GetFileEntry(PatchEdit.BmgEntry)!.Data!;
        var bmg = new BmgFile(original);
        var reread = new BmgFile(bmg.Save());
        Assert.Equal(bmg.Messages.Count, reread.Messages.Count);
        for (int i = 0; i < bmg.Messages.Count; i++)
            Assert.Equal(bmg.Messages[i].Text, reread.Messages[i].Text);
        Assert.Equal("Sail"u8.ToArray(), bmg.FindById(463)!.Text);
    }
}
