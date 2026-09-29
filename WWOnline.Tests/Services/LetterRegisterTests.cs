using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The postbox letters' state registers (value mask 0x03: dLetter 0 not sent, 1 sent, 2 in the postbox, 3 read)
/// ride in the room story (<see cref="StoryFlags.Letters"/>), merged by MAX, so a letter one player has read is
/// read for everyone and its reward is given once. Only the 0x03 bits are ever read, merged or written.
/// </summary>
public class LetterRegisterTests
{
    private static uint Reg(int eventByte) => EventFlagCatalog.EventBitfieldAddress + (uint)eventByte;

    [Fact]
    public void TheRegisters_AreTheTwelveLetters_InThePostboxsOrder()
    {
        Assert.Equal(StoryFlags.LetterByteCount, StoryFlags.LetterRegisterBytes.Length);
        Assert.Equal(StoryFlags.LetterByteCount, StoryFlags.LetterRegisterBytes.ToArray().Distinct().Count());
        foreach (byte b in StoryFlags.LetterRegisterBytes)
        {
            var reg = EventFlagCatalog.Registers.Single(r => r.ByteIndex == b && r.Mask == StoryFlags.LetterMask);
            Assert.StartsWith("LETTER_", reg.Name);
            Assert.Equal(EventRegisterPolicy.Max, reg.Policy);
        }
        Assert.Equal(12, EventFlagCatalog.Registers.Count(r => r.Name.StartsWith("LETTER_")));
    }

    [Fact]
    public void FromEventBytes_ReadsOnlyTheStateBits()
    {
        var raw = new byte[EventFlagCatalog.EventBitfieldSize];
        raw[0x7D] = 0xFE; // bomb ad: read (2 = stocked... here 0x02) plus other flags in the high bits
        raw[0xB5] = 0x03; // Komali's father: read
        var f = StoryFlags.FromEventBytes(raw);
        Assert.Equal(0x02, f.Letters[2]);
        Assert.Equal(0x03, f.Letters[1]);
        Assert.Equal(2, f.LetterCount);
        Assert.False(f.IsEmpty);
    }

    [Fact]
    public void LettersMerge_ByMax_NeverLower()
    {
        var room = new StoryFlags();
        room.Letters[0] = 3; // read here
        var peer = new StoryFlags();
        peer.Letters[0] = 1; // only sent there
        peer.Letters[4] = 0xFE; // garbage + 2
        peer.Normalize();
        Assert.Equal(2, peer.Letters[4]);

        Assert.True(room.MergeFrom(peer));
        Assert.Equal(3, room.Letters[0]); // not lowered
        Assert.Equal(2, room.Letters[4]);
        Assert.False(room.MergeFrom(peer));

        var missing = room.Except(peer);
        Assert.Equal(3, missing.Letters[0]); // a higher state is missing there
        Assert.Equal(0, missing.Letters[4]);
    }

    [Fact]
    public void LettersWaitForAnIdleGame_LikeFigurines()
    {
        var room = new StoryFlags();
        room.Letters[3] = 3;
        room.Bits[0] = StoryFlags.SyncMask[0];
        var busy = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => false);
        Assert.Equal(0, busy.LetterCount);
        var idle = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => true);
        Assert.Equal(3, idle.Letters[3]);
    }

    [Fact]
    public void AStoryWithoutLetters_IsInvalid()
    {
        Assert.False(new StoryFlags { Letters = new byte[3] }.IsValid());
        Assert.True(new StoryFlags().IsValid());
    }

    [Fact]
    public void ApplyBits_RaisesTheState_AndLeavesTheRestOfTheByte()
    {
        var d = new FakeDolphin();
        d.Set(Reg(0xAC), 0x81); // Baito's mother: sent, plus an unrelated flag
        d.Set(Reg(0xB5), 0x03); // Komali's father: already read here
        var room = new StoryFlags();
        room.Letters[0] = 3;
        room.Letters[1] = 2; // lower than ours: never written
        StorySyncService.ApplyBits(d, room);
        Assert.Equal(0x83, d.Get(Reg(0xAC)));
        Assert.Equal(0x03, d.Get(Reg(0xB5)));
    }

    [Fact]
    public void TheStoryFlagsPage_MarksTheLettersShared()
    {
        var letter = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.ByteIndex == 0xB2 && r.Mask == 0x03));
        Assert.True(letter.IsSharedLetter);
        Assert.Contains("Letters", letter.PolicyText);
        var other = new EventRegisterRow(EventFlagCatalog.Registers.First(r => r.ByteIndex == 0x86));
        Assert.False(other.IsSharedLetter);
    }
}
