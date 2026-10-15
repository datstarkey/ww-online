using System.Runtime.CompilerServices;
using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>The shared inventory's play side on the native game: tww.h's inv calls behind <see cref="IPlayInventory"/>.</summary>
public class NativeInventoryTests
{
    private readonly FakeNativeGame _game = new();
    private readonly NativeGameService _sut;

    public NativeInventoryTests() => _sut = new NativeGameService(_game);

    private static int Offset<TStruct, TField>(ref TStruct s, ref TField field) =>
        (int)Unsafe.ByteOffset(ref Unsafe.As<TStruct, byte>(ref s), ref Unsafe.As<TField, byte>(ref field));

    private void SaveU16(uint offset, int v) => (_game.Save[offset], _game.Save[offset + 1]) = ((byte)(v >> 8), (byte)v);

    private static uint Off(uint address) => address - GameMemoryAddresses.GameInfo;

    [Fact]
    public void TwwInvPlay_Is64Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwInvPlay();
        Assert.Equal(64, Unsafe.SizeOf<TwwInvPlay>());
        Assert.Equal(14, TwwInvPlay.CounterCount);
        Assert.Equal(0, Offset(ref s, ref s.Pending));
        Assert.Equal(56, Offset(ref s, ref s.SelectItem));
        Assert.Equal(60, Offset(ref s, ref s.SaveTbl));
        Assert.Equal(61, Offset(ref s, ref s.KeyDisp));
    }

    [Fact]
    public void Pending_IsReadAndAddedTo_ThroughTheInvCalls()
    {
        _game.Inv.Pending[(int)TwwInvCounter.Key] = 1;
        Assert.True(_sut.Play.Add(TwwInvCounter.Rupee, 300));
        Assert.True(_sut.Play.Add(TwwInvCounter.Beast + 7, -2));

        var p = _sut.Play.ReadPending()!;
        Assert.Equal(TwwInvPlay.CounterCount, p.Length);
        Assert.Equal(300, p[(int)TwwInvCounter.Rupee]);
        Assert.Equal(1, p[(int)TwwInvCounter.Key]);
        Assert.Equal(-2, p[(int)TwwInvCounter.Beast + 7]);
    }

    [Fact]
    public void SelectItems_AreTheThreeButtons()
    {
        _game.Inv.SelectItem[3] = 0x77;
        Assert.True(_sut.Play.WriteSelectItems([0x45, 0xFF, 0x1F]));

        Assert.Equal(new byte[] { 0x45, 0xFF, 0x1F }, _sut.Play.ReadSelectItems());
        Assert.Equal(0x77, _game.Inv.SelectItem[3]);
    }

    [Fact]
    public void Stage_IsTheSaveSlotAndKeyHud_OrNoneOutsideAStage()
    {
        _game.Inv.SaveTbl = -1;
        Assert.Equal((null, false), _sut.Play.ReadStage());

        _game.Inv.SaveTbl = 3;
        _game.Inv.KeyDisp = 1;
        Assert.Equal((3, true), _sut.Play.ReadStage());
    }

    [Fact]
    public void AStoppedGame_ReadsNothing_AndWritesFail()
    {
        _game.IsRunning = false;

        Assert.Null(_sut.Play.ReadPending());
        Assert.False(_sut.Play.Add(TwwInvCounter.Rupee, 1));
    }

    [Fact]
    public void GameActions_QueueRupeesAndAmmo_FromTheSaveCount()
    {
        SaveU16(Off(GameMemoryAddresses.Player.RupeeCount.Address), 50);
        _game.Save[Off(GameMemoryAddresses.Player.CurrentArrowCount.Address)] = 5;

        Assert.True(GameActions.SetRupees(_sut, 100));
        Assert.True(GameActions.AddRupees(_sut, 5));
        Assert.True(GameActions.SetArrows(_sut, 20));

        Assert.Equal(55, _game.Inv.Pending[(int)TwwInvCounter.Rupee]);
        Assert.Equal(15, _game.Inv.Pending[(int)TwwInvCounter.Arrow]);
    }

    [Fact]
    public void KeysAndMaxLife_AreQueued_OnlyWhileNothingIsPending()
    {
        Assert.True(SmallKeyMemory.Apply(_sut, new KeyWrite(0, KeyWriteKind.Pending, 1, 0, 1)));
        Assert.False(SmallKeyMemory.Apply(_sut, new KeyWrite(0, KeyWriteKind.Pending, 1, 0, 1)));
        Assert.True(HeartMemory.ApplyDelta(_sut, 4));
        Assert.False(HeartMemory.ApplyDelta(_sut, 4));

        Assert.Equal(1, _game.Inv.Pending[(int)TwwInvCounter.Key]);
        Assert.Equal(4, _game.Inv.Pending[(int)TwwInvCounter.MaxLife]);
    }

    [Fact]
    public void HeartAndKeyObservations_ReadTheLiveSlot_AndIdle()
    {
        _game.Gate = FakeNativeGame.InPlay("M_NewD2", 0);
        _game.Inv.SaveTbl = 3;
        _game.Inv.KeyDisp = 1;
        SaveU16(0, 20); // max life
        _game.Save[Off(GameMemoryAddresses.WorldFlags.LiveMemory) + GameMemoryAddresses.WorldFlags.OffKeyNum] = 2;

        var keys = SmallKeyMemory.Read(_sut)!;
        Assert.True(keys.Idle);
        Assert.True(keys.KeyHud);
        Assert.Equal(3, keys.CurrentSlot);
        Assert.Equal(2, keys.Slots[3].KeyNum);
        Assert.Equal("M_NewD2", keys.Stage);

        Assert.True(HeartMemory.Read(_sut)!.Idle);
        _game.Gate.MiniGame = 8;
        var hearts = HeartMemory.Read(_sut)!;
        Assert.Equal(20, hearts.MaxLife);
        Assert.False(hearts.Idle);
        _game.Gate.MiniGame = 0;
        _game.Gate.MenuPaused = 1;
        Assert.False(SmallKeyMemory.Read(_sut)!.Idle);
    }

    [Fact]
    public void Spoils_GoToTheSaveSlots_AndThePendingCounts()
    {
        _game.Save[Off(GameMemoryAddresses.Inventory.SpoilsBag.Address)] = ItemIDs.MainItems.SpoilsBag;
        Array.Fill(_game.Save, SpoilsBagSlots.Empty, (int)Off(GameMemoryAddresses.Inventory.SpoilsItems), SpoilsCounts.TypeCount);
        var bag = SpoilsBagMemory.Read(_sut)!;
        var target = new int[SpoilsCounts.TypeCount];
        target[7] = 5;
        var plan = SpoilsBag.PlanFor(bag, new SpoilsCounts(target));

        Assert.Equal(BagWriteResult.Written, SpoilsBagMemory.Write(_sut, bag, plan));

        Assert.Equal(SpoilsCounts.Types[7].ItemNo, _game.Save[Off(GameMemoryAddresses.Inventory.SpoilsItems)]);
        Assert.Equal(5, _game.Inv.Pending[(int)TwwInvCounter.Beast + 7]);
        Assert.Equal(BagWriteResult.Raced, SpoilsBagMemory.Write(_sut, bag, plan)); // the bag and the pending count changed
    }

    [Fact]
    public void EquippedSwordAndShield_ReadFromTheSave_NoSwapNatively()
    {
        _game.Save[Off(GameMemoryAddresses.Player.CurrentSword.Address)] = ItemIDs.Swords.MasterSwordFull;
        _game.Save[Off(GameMemoryAddresses.Player.CurrentShield.Address)] = ItemIDs.Shields.MirrorShield;

        Assert.Equal((ItemIDs.Swords.MasterSwordFull, ItemIDs.Shields.MirrorShield), EquipSwapGuard.ReadEquipped(_sut));
        Assert.True(EquipSwapGuard.TryWrite(_sut, GameMemoryAddresses.Player.CurrentSword, 0x38));
    }
}
