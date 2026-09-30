using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The side quests' counters, levels and prize tiers (<see cref="StoryFlags.QuestRegisters"/>, docs/side-quests.md §3)
/// ride in the room story: Mrs. Marie's pendant count, Orca's and Koboli's levels, the Sploosh Kaboom and barrel
/// shooting prizes won and the Ghost Ship by MAX, Rose's pig pen by OR. Only each register's mask is ever read,
/// merged or written.
/// </summary>
public class QuestRegisterTests
{
    private static uint Reg(int eventByte) => EventFlagCatalog.EventBitfieldAddress + (uint)eventByte;

    private static int Index(byte eventByte) =>
        StoryFlags.QuestRegisterTable.Select((q, i) => (q, i)).Single(x => x.q.EventByte == eventByte).i;

    [Fact]
    public void TheTable_IsTheSevenQuestRegisters_EachInTheCatalogue()
    {
        Assert.Equal(StoryFlags.QuestRegisterCount, StoryFlags.QuestRegisterTable.Count);
        Assert.Equal(StoryFlags.QuestRegisterCount, StoryFlags.QuestRegisterTable.Select(q => q.EventByte).Distinct().Count());
        foreach (var q in StoryFlags.QuestRegisterTable)
        {
            var reg = EventFlagCatalog.Registers.Single(r => r.ByteIndex == q.EventByte);
            Assert.Equal(q.Merge == QuestRegisterMerge.Or ? EventRegisterPolicy.BitwiseOr : EventRegisterPolicy.Max, reg.Policy);
            // No register the room story already carries.
            Assert.False(StoryFlags.LetterRegisterBytes.Contains(q.EventByte));
            Assert.False(StoryFlags.FigurineRegisterBytes.Contains(q.EventByte));
            Assert.False(StoryFlags.WarpJarRegisterBytes.Contains(q.EventByte));
            Assert.NotEqual(StoryFlags.BeedlePointsRegisterByte, q.EventByte);
        }
    }

    [Fact]
    public void FromEventBytes_ReadsOnlyEachMask()
    {
        var raw = new byte[EventFlagCatalog.EventBitfieldSize];
        raw[0xC0] = 20;   // 20 pendants given
        raw[0xD0] = 0xF4; // Orca level 4, other bits set
        raw[0xBF] = 0xF5; // pigs 0 and 2, other bits set
        var f = StoryFlags.FromEventBytes(raw);
        Assert.Equal(20, f.QuestRegisters[Index(0xC0)]);
        Assert.Equal(4, f.QuestRegisters[Index(0xD0)]);
        Assert.Equal(0x05, f.QuestRegisters[Index(0xBF)]);
        Assert.Equal(3, f.QuestRegisterCountSet);
        Assert.False(f.IsEmpty);
    }

    [Fact]
    public void Merge_TakesTheHighest_AndOrsTheBitmask()
    {
        var room = new StoryFlags();
        room.QuestRegisters[Index(0xC0)] = 20;
        room.QuestRegisters[Index(0xFE)] = 1;
        room.QuestRegisters[Index(0xBF)] = 0x01;
        var peer = new StoryFlags();
        peer.QuestRegisters[Index(0xC0)] = 5;    // lower: kept at 20
        peer.QuestRegisters[Index(0xFE)] = 2;    // higher: raised
        peer.QuestRegisters[Index(0xBF)] = 0x04; // another pig: ORed

        Assert.True(room.MergeFrom(peer));
        Assert.Equal(20, room.QuestRegisters[Index(0xC0)]);
        Assert.Equal(2, room.QuestRegisters[Index(0xFE)]);
        Assert.Equal(0x05, room.QuestRegisters[Index(0xBF)]);
        Assert.False(room.MergeFrom(peer));

        var missing = room.Except(peer);
        Assert.Equal(20, missing.QuestRegisters[Index(0xC0)]); // a higher count is missing there
        Assert.Equal(0, missing.QuestRegisters[Index(0xFE)]);
        Assert.Equal(0x01, missing.QuestRegisters[Index(0xBF)]); // only the pig they don't have
    }

    [Fact]
    public void Normalize_DropsBitsOutsideEachMask()
    {
        var f = new StoryFlags();
        f.QuestRegisters[Index(0xC2)] = 0xFF;
        f.QuestRegisters[Index(0xBF)] = 0xFF;
        f.Normalize();
        Assert.Equal(0x03, f.QuestRegisters[Index(0xC2)]);
        Assert.Equal(0x0F, f.QuestRegisters[Index(0xBF)]);
    }

    [Fact]
    public void QuestRegistersWaitForAnIdleGame()
    {
        var room = new StoryFlags();
        room.QuestRegisters[Index(0x88)] = 3;
        room.Bits[0] = StoryFlags.SyncMask[0];
        var busy = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => false);
        Assert.Equal(0, busy.QuestRegisterCountSet);
        Assert.Equal(StoryFlags.SyncMask[0], busy.Bits[0]); // the flags don't wait
        var idle = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => true);
        Assert.Equal(3, idle.QuestRegisters[Index(0x88)]);
    }

    [Fact]
    public void AStoryWithoutQuestRegisters_IsInvalid()
    {
        Assert.False(new StoryFlags { QuestRegisters = new byte[2] }.IsValid());
        Assert.True(new StoryFlags().IsValid());
    }

    [Fact]
    public void ApplyBits_RaisesLevels_OrsThePigs_AndLeavesTheRestOfEachByte()
    {
        var d = new FakeDolphin();
        d.Set(Reg(0xC0), 0);    // no pendants given here: Mrs. Marie would run her first-time talk
        d.Set(Reg(0xC2), 0x82); // Koboli level 2, plus an unrelated flag
        d.Set(Reg(0xFE), 0x03); // three prizes won here already
        d.Set(Reg(0xBF), 0x42); // pig 1, plus an unrelated bit
        var room = new StoryFlags();
        room.QuestRegisters[Index(0xC0)] = 20;
        room.QuestRegisters[Index(0xC2)] = 3;
        room.QuestRegisters[Index(0xFE)] = 1;    // lower than ours: never written
        room.QuestRegisters[Index(0xBF)] = 0x05; // pigs 0 and 2
        StorySyncService.ApplyBits(d, room);
        Assert.Equal(20, d.Get(Reg(0xC0)));
        Assert.Equal(0x83, d.Get(Reg(0xC2)));
        Assert.Equal(0x03, d.Get(Reg(0xFE)));
        Assert.Equal(0x47, d.Get(Reg(0xBF)));
    }

    [Fact]
    public void TheStoryFlagsPage_MarksThemShared()
    {
        var pendants = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.ByteIndex == 0xC0));
        Assert.NotNull(pendants.SharedQuest);
        Assert.Contains("Side quest", pendants.PolicyText);
        Assert.Contains("Mrs. Marie", pendants.Tooltip);
        var other = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.ByteIndex == 0xA6));
        Assert.Null(other.SharedQuest);
    }
}
