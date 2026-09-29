using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The dungeon warp jars' "open" bits (event registers 0x9F-0xA4, value mask 0x07: daObj_Warpt_c::m_event_reg)
/// ride in the room story (<see cref="StoryFlags.WarpJars"/>), so a jar whose own lid one player breaks opens
/// for everyone. Only the 0x07 bits are ever read, merged or written; the rest of each byte is other flags.
/// </summary>
public class WarpJarRegisterTests
{
    private static uint Reg(int eventByte) => EventFlagCatalog.EventBitfieldAddress + (uint)eventByte;

    [Fact]
    public void TheRegisters_AreTheJarsOwn_InMEventRegOrder()
    {
        Assert.Equal(new byte[] { 0xA2, 0xA1, 0xA0, 0x9F, 0xA3, 0xA4 }, StoryFlags.WarpJarRegisterBytes.ToArray());
        foreach (byte b in StoryFlags.WarpJarRegisterBytes)
        {
            var reg = EventFlagCatalog.Registers.Single(r => r.ByteIndex == b && r.Mask == StoryFlags.WarpJarMask);
            Assert.Equal(EventRegisterPolicy.BitwiseOr, reg.Policy);
        }
    }

    [Fact]
    public void FromEventBytes_ReadsOnlyTheJarBits()
    {
        var raw = new byte[EventFlagCatalog.EventBitfieldSize];
        raw[0x9F] = 0xF5; // WT: jars 1 and 4, plus other flags in the high bits
        raw[0xA2] = 0x02; // DRC: jar 2
        var f = StoryFlags.FromEventBytes(raw);
        Assert.Equal(0x05, f.WarpJars[3]);
        Assert.Equal(0x02, f.WarpJars[0]);
        Assert.Equal(3, f.WarpJarCount);
        Assert.False(f.IsEmpty);
    }

    [Fact]
    public void JarsMerge_GrowOnly_AndNormalizeDropsOtherBits()
    {
        var room = new StoryFlags();
        var peer = new StoryFlags();
        peer.WarpJars[3] = 0xFC; // WT jar 4 + garbage
        Assert.True(peer.IsValid());
        peer.Normalize();
        Assert.Equal(0x04, peer.WarpJars[3]);

        Assert.True(room.MergeFrom(peer));
        Assert.False(room.MergeFrom(peer)); // nothing new
        Assert.Equal(0x04, room.Except(new StoryFlags()).WarpJars[3]);
        Assert.Equal(0, room.Except(peer).WarpJarCount);
    }

    [Fact]
    public void JarsGoWithTheFlags_NotHeldBackLikeFigurines()
    {
        var f = new StoryFlags();
        f.WarpJars[1] = 0x01;
        f.Figurines[0] = 0x01;
        var flagsOnly = f.FlagsOnly();
        Assert.Equal(0x01, flagsOnly.WarpJars[1]);
        Assert.Equal(0, flagsOnly.FigurineCount);
    }

    [Fact]
    public void AStoryWithoutJars_IsInvalid()
    {
        var f = new StoryFlags { WarpJars = new byte[2] };
        Assert.False(f.IsValid());
    }

    [Fact]
    public void ApplyBits_OrsTheJarBits_AndLeavesTheRestOfTheByte()
    {
        var d = new FakeDolphin();
        d.Set(Reg(0xA3), 0x80); // ET: an unrelated flag in the same byte
        var room = new StoryFlags();
        room.WarpJars[4] = 0x02; // ET jar 2
        room.WarpJars[5] = 0xFF; // garbage never reaches the game
        StorySyncService.ApplyBits(d, room);
        Assert.Equal(0x82, d.Get(Reg(0xA3)));
        Assert.Equal(0x07, d.Get(Reg(0xA4)));
    }

    [Fact]
    public void TheStoryFlagsPage_MarksTheJarRegistersShared()
    {
        var jar = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.ByteIndex == 0x9F && r.Mask == 0x07));
        Assert.True(jar.IsSharedWarpJars);
        Assert.Contains("Warp jars", jar.PolicyText);
        var other = new EventRegisterRow(EventFlagCatalog.Registers.First(r => r.ByteIndex == 0x9D));
        Assert.False(other.IsSharedWarpJars);
    }
}
