using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

public class RoomInventoryTests
{
    private static RoomInventory Inv(Action<RoomInventory>? set = null)
    {
        var inv = new RoomInventory();
        set?.Invoke(inv);
        return inv;
    }

    // ── MergeGainsFrom ──────────────────────────────────────────────────────

    [Fact]
    public void Merge_OrsBitfields_AndReportsChange()
    {
        var room = Inv(r => { r.Swords = 0x01; r.Songs = 0x01; r.Pearls = 0x01; });
        Assert.True(room.MergeGainsFrom(Inv(o => { o.Swords = 0x02; o.Songs = 0x04; o.TriforceShards = 0x80; })));
        Assert.Equal(0x03, room.Swords);
        Assert.Equal(0x05, room.Songs);
        Assert.Equal(0x01, room.Pearls);
        Assert.Equal(0x80, room.TriforceShards);
    }

    [Fact]
    public void Merge_TakesMaxOfLevels_NeverLowers()
    {
        var room = Inv(r => { r.MaxHealth = 16; r.MaxMagic = 32; r.MaxArrows = 30; r.WalletSize = 1; });
        Assert.True(room.MergeGainsFrom(Inv(o => { o.MaxHealth = 20; o.MaxMagic = 16; o.MaxArrows = 60; o.MaxBombs = 30; })));
        Assert.Equal(20, room.MaxHealth);
        Assert.Equal(32, room.MaxMagic);
        Assert.Equal(60, room.MaxArrows);
        Assert.Equal(30, room.MaxBombs);
        Assert.Equal(1, room.WalletSize);
    }

    [Fact]
    public void Merge_SubsetIsNotAChange()
    {
        var room = Inv(r => { r.Swords = 0x03; r.MaxHealth = 20; r.Items[RoomInventory.BowSlot] = RoomInventory.MagicArrowItem; });
        Assert.False(room.MergeGainsFrom(Inv(o => { o.Swords = 0x01; o.MaxHealth = 12; o.Items[RoomInventory.BowSlot] = RoomInventory.BowItem; })));
        Assert.Equal(RoomInventory.MagicArrowItem, room.Items[RoomInventory.BowSlot]);
    }

    [Fact]
    public void Merge_FillsEmptySlot_KeepsOccupiedSlot()
    {
        var room = Inv(r => r.Items[0] = 0x20);
        Assert.True(room.MergeGainsFrom(Inv(o => { o.Items[0] = 0x99; o.Items[19] = 0x2F; })));
        Assert.Equal(0x20, room.Items[0]);  // occupied, not an upgrade chain: first one stays
        Assert.Equal(0x2F, room.Items[19]); // empty: filled
    }

    [Fact]
    public void Merge_UpgradesBowAndPictoBox()
    {
        var room = Inv(r => { r.Items[RoomInventory.BowSlot] = RoomInventory.BowItem; r.Items[RoomInventory.PictoBoxSlot] = RoomInventory.PictoBoxItem; });
        Assert.True(room.MergeGainsFrom(Inv(o =>
        {
            o.Items[RoomInventory.BowSlot] = RoomInventory.LightArrowItem;
            o.Items[RoomInventory.PictoBoxSlot] = RoomInventory.DeluxePictoBoxItem;
        })));
        Assert.Equal(RoomInventory.LightArrowItem, room.Items[RoomInventory.BowSlot]);
        Assert.Equal(RoomInventory.DeluxePictoBoxItem, room.Items[RoomInventory.PictoBoxSlot]);
    }

    [Fact]
    public void Merge_BottleOwnershipOnly()
    {
        var room = new RoomInventory();
        room.MergeGainsFrom(Inv(o => o.Items[RoomInventory.FirstBottleSlot] = 0x57)); // fairy in a bottle
        Assert.Equal(RoomInventory.EmptyBottle, room.Items[RoomInventory.FirstBottleSlot]);
    }

    [Fact]
    public void Merge_HigherEquippedSwordAndShieldWin()
    {
        var room = Inv(r => { r.EquippedSword = RoomInventory.MasterSword1; r.EquippedShield = RoomInventory.MirrorShield; });
        room.MergeGainsFrom(Inv(o => { o.EquippedSword = RoomInventory.MasterSword3; o.EquippedShield = RoomInventory.HerosShield; }));
        Assert.Equal(RoomInventory.MasterSword3, room.EquippedSword);
        Assert.Equal(RoomInventory.MirrorShield, room.EquippedShield);
    }

    // ── GainsOver ───────────────────────────────────────────────────────────

    [Fact]
    public void GainsOver_OnlyReportsGains_NotLosses()
    {
        var baseline = Inv(b =>
        {
            b.Swords = 0x01; b.MaxHealth = 12; b.Items[3] = 0x25; b.EquippedSword = RoomInventory.HerosSword;
        });
        var now = Inv(n =>
        {
            n.Swords = 0x03; n.MaxHealth = 16; n.Items[19] = 0x2F; // gains
            n.EquippedSword = RoomInventory.NoItem;                // lost (story scene): not a gain
            // Items[3] lost: not a gain
        });
        var g = now.GainsOver(baseline);
        Assert.Equal(0x02, g.Swords);
        Assert.Equal(16, g.MaxHealth);
        Assert.Equal(0x2F, g.Items[19]);
        Assert.Equal(RoomInventory.NoItem, g.Items[3]);
        Assert.Equal(RoomInventory.NoItem, g.EquippedSword);
    }

    [Fact]
    public void GainsOver_NoChange_IsEmpty()
    {
        var a = Inv(x => { x.Songs = 0x3F; x.MaxMagic = 16; x.Items[5] = 0x2D; });
        Assert.True(a.Clone().GainsOver(a).IsEmpty);
    }

    [Fact]
    public void GainsOver_BottleContentChange_IsNotAGain()
    {
        var before = Inv(b => b.Items[RoomInventory.FirstBottleSlot] = 0x50).Normalize();
        var after = Inv(a => a.Items[RoomInventory.FirstBottleSlot] = 0x57).Normalize();
        Assert.True(after.GainsOver(before).IsEmpty);
    }

    // ── Validation / normalization ──────────────────────────────────────────

    [Fact]
    public void IsValid_RejectsBadShapesAndRanges()
    {
        Assert.True(new RoomInventory().IsValid());
        Assert.False(Inv(x => x.Items = new byte[20]).IsValid());
        Assert.False(Inv(x => x.ItemGetFlags = new byte[22]).IsValid());
        Assert.False(Inv(x => x.MaxHealth = RoomInventory.MaxHealthLimit + 1).IsValid());
        Assert.False(Inv(x => x.MaxMagic = 33).IsValid());
        Assert.False(Inv(x => x.MaxArrows = 100).IsValid());
        Assert.False(Inv(x => x.WalletSize = 3).IsValid());
        Assert.False(Inv(x => x.EquippedSword = 0x3B).IsValid());  // a shield id
        Assert.False(Inv(x => x.EquippedShield = 0x38).IsValid()); // a sword id
    }

    [Fact]
    public void Normalize_MasksUnknownBits_AndBottles()
    {
        var n = Inv(x =>
        {
            x.Swords = 0xFF; x.HerosCharm = 0x03; x.Songs = 0xFF; x.Pearls = 0xFF;
            x.Items[RoomInventory.FirstBottleSlot + 2] = 0x5A;
        }).Normalize();
        Assert.Equal(RoomInventory.SwordMask, n.Swords);
        Assert.Equal(0x01, n.HerosCharm); // "worn" bit is per-player
        Assert.Equal(RoomInventory.SongMask, n.Songs);
        Assert.Equal(RoomInventory.PearlMask, n.Pearls);
        Assert.Equal(RoomInventory.EmptyBottle, n.Items[RoomInventory.FirstBottleSlot + 2]);
    }

    [Fact]
    public void Describe_ListsEachChange_AndEqualStatesHaveNone()
    {
        var a = new RoomInventory();
        var b = Inv(x => { x.Items[12] = RoomInventory.BowItem; x.MaxHealth = 16; x.EquippedSword = RoomInventory.HerosSword; });
        var d = RoomInventory.Describe(a, b);
        Assert.Equal(3, d.Count);
        Assert.Contains(d, s => s.StartsWith("Bow"));
        Assert.True(b.ContentEquals(b.Clone()));
    }

    [Fact]
    public void Clone_IsDeep()
    {
        var a = Inv(x => x.Items[0] = 0x20);
        var c = a.Clone();
        c.Items[0] = RoomInventory.NoItem;
        c.ItemGetFlags[0] = 1;
        Assert.Equal(0x20, a.Items[0]);
        Assert.Equal(0, a.ItemGetFlags[0]);
    }
}

/// <summary>RoomInventoryMemory against a fake save-data buffer.</summary>
public class RoomInventoryMemoryTests
{
    private readonly FakeDolphin _game = new();

    public RoomInventoryMemoryTests()
    {
        // A fresh save: empty item slots, nothing equipped.
        _game.Set(GameMemoryAddresses.Inventory.ItemSlots.Address, RoomInventory.NewEmptySlots());
        _game.Set(GameMemoryAddresses.Player.CurrentSword.Address, RoomInventory.NoItem, RoomInventory.NoItem);
    }

    [Fact]
    public void Addresses_MatchDecompLayout()
    {
        // dSv_player_c at g_dComIfG_gameInfo (0x803C4C08): mCollect +0xB4, mItemMax +0x6E, mGetItem +0x51.
        Assert.Equal(0x803C4CBCu, GameMemoryAddresses.Inventory.SwordsBitfield.Address);
        Assert.Equal(0x803C4CBDu, GameMemoryAddresses.Inventory.ShieldsBitfield.Address);
        Assert.Equal(0x803C4CBEu, GameMemoryAddresses.Inventory.PowerBraceletsBitfield.Address);
        Assert.Equal(0x803C4CC5u, GameMemoryAddresses.Inventory.SongsBitfield.Address);
        Assert.Equal(0x803C4CC6u, GameMemoryAddresses.Inventory.TriforceShards.Address);
        Assert.Equal(0x803C4CC7u, GameMemoryAddresses.Inventory.PearlsBitfield.Address);
        Assert.Equal(0x803C4C44u, GameMemoryAddresses.Inventory.ItemSlots.Address);
        Assert.Equal(0x803C4C59u, GameMemoryAddresses.Inventory.ItemOwnership.Address);
        Assert.Equal(0x803C4C77u, GameMemoryAddresses.Inventory.MaxArrows.Address);
        Assert.Equal(0x803C4C78u, GameMemoryAddresses.Inventory.MaxBombs.Address);
        // Counts live elsewhere (mItemRecord) and must never be the capacity fields.
        Assert.NotEqual(GameMemoryAddresses.Player.CurrentArrowCount.Address, GameMemoryAddresses.Inventory.MaxArrows.Address);
        Assert.NotEqual(GameMemoryAddresses.Player.CurrentBombCount.Address, GameMemoryAddresses.Inventory.MaxBombs.Address);
    }

    [Fact]
    public void Read_ParsesSaveData()
    {
        _game.Set(GameMemoryAddresses.Inventory.ItemSlots.Address + 12, RoomInventory.BowItem);
        _game.Set(GameMemoryAddresses.Inventory.ItemSlots.Address + 14, 0x57); // fairy bottle
        _game.Set(GameMemoryAddresses.Inventory.SwordsBitfield.Address, 0x01);
        _game.Set(GameMemoryAddresses.Inventory.HerosCharmBitfield.Address, 0x03);
        _game.Set(GameMemoryAddresses.Player.MaxHealth.Address, 0x00, 0x10);
        _game.Set(GameMemoryAddresses.Player.CurrentSword.Address, RoomInventory.HerosSword);

        var inv = RoomInventoryMemory.Read(_game)!;
        Assert.Equal(RoomInventory.BowItem, inv.Items[12]);
        Assert.Equal(RoomInventory.EmptyBottle, inv.Items[14]);
        Assert.Equal(0x01, inv.Swords);
        Assert.Equal(0x01, inv.HerosCharm);
        Assert.Equal(16, inv.MaxHealth);
        Assert.Equal(RoomInventory.HerosSword, inv.EquippedSword);
    }

    [Fact]
    public void Apply_WritesRoomValues_NeverCounts_KeepsBottleContents()
    {
        _game.Set(GameMemoryAddresses.Inventory.ItemSlots.Address + 14, 0x57); // our fairy
        _game.Set(GameMemoryAddresses.Inventory.ItemSlots.Address + 5, 0x2D);  // boomerang the room removed
        _game.Set(GameMemoryAddresses.Inventory.HerosCharmBitfield.Address, 0x02); // wearing bit, no charm
        _game.Set(GameMemoryAddresses.Player.CurrentArrowCount.Address, 7);
        _game.Set(GameMemoryAddresses.Player.CurrentBombCount.Address, 3);
        _game.Set(GameMemoryAddresses.Player.MaxHealth.Address, 0x00, 0x20);
        _game.Set(GameMemoryAddresses.Player.CurrentHealth.Address, 0x00, 0x20);
        var local = RoomInventoryMemory.Read(_game)!;

        var room = new RoomInventory
        {
            HerosCharm = 0x01, MaxArrows = 60, MaxBombs = 60, MaxHealth = 12, Songs = 0x3F,
            EquippedSword = RoomInventory.MasterSword1, PowerBracelets = 0x01,
        };
        room.Items[14] = RoomInventory.EmptyBottle;
        room.Items[15] = RoomInventory.EmptyBottle; // a bottle we don't have yet
        room.Items[19] = 0x2F;
        room.Normalize(); // as every room state from the server is (equipped ⇒ owned)

        var changes = new List<string>();
        var after = RoomInventoryMemory.Apply(_game, room, local, applySword: true, applyShield: true, changes);

        Assert.Equal(0x57, _game.Get(GameMemoryAddresses.Inventory.ItemSlots.Address + 14)); // contents kept
        Assert.Equal(RoomInventory.EmptyBottle, _game.Get(GameMemoryAddresses.Inventory.ItemSlots.Address + 15));
        Assert.Equal(RoomInventory.NoItem, _game.Get(GameMemoryAddresses.Inventory.ItemSlots.Address + 5));
        Assert.Equal(0x2F, _game.Get(GameMemoryAddresses.Inventory.ItemSlots.Address + 19));
        Assert.Equal(0x03, _game.Get(GameMemoryAddresses.Inventory.HerosCharmBitfield.Address)); // worn bit preserved
        Assert.Equal(60, _game.Get(GameMemoryAddresses.Inventory.MaxArrows.Address));
        Assert.Equal(7, _game.Get(GameMemoryAddresses.Player.CurrentArrowCount.Address)); // count untouched
        Assert.Equal(3, _game.Get(GameMemoryAddresses.Player.CurrentBombCount.Address));
        Assert.Equal(12, _game.Get(GameMemoryAddresses.Player.MaxHealth.Address + 1));
        Assert.Equal(12, _game.Get(GameMemoryAddresses.Player.CurrentHealth.Address + 1)); // clamped to new max
        Assert.Equal(RoomInventory.MasterSword1, _game.Get(GameMemoryAddresses.Player.CurrentSword.Address));
        Assert.Equal(RoomInventory.PowerBraceletsItem, _game.Get(GameMemoryAddresses.Player.PowerBracelets.Address));
        Assert.NotEmpty(changes);

        // Re-reading the game gives what Apply said it holds, so the next tick sees no gains.
        var reread = RoomInventoryMemory.Read(_game)!;
        Assert.True(reread.ContentEquals(after));
        Assert.True(reread.GainsOver(after).IsEmpty);
    }

    [Fact]
    public void Apply_EquippedSwordNotForced_WhenCallerSaysUnchanged()
    {
        var local = RoomInventoryMemory.Read(_game)!; // no sword equipped (e.g. taken by a story scene)
        var room = new RoomInventory { Swords = 0x01, EquippedSword = RoomInventory.HerosSword };
        RoomInventoryMemory.Apply(_game, room, local, applySword: false, applyShield: false, []);
        Assert.Equal(RoomInventory.NoItem, _game.Get(GameMemoryAddresses.Player.CurrentSword.Address));
        Assert.Equal(0x01, _game.Get(GameMemoryAddresses.Inventory.SwordsBitfield.Address));
    }

    [Fact]
    public void Read_SkipsTheTick_WhileThePuppetRelHasAPeersEquipmentSwappedIn()
    {
        _game.Set(GameMemoryAddresses.Player.CurrentSword.Address, RoomInventory.MasterSword1); // the peer's
        _game.SetU32(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 3); // odd: swap in progress
        Assert.Null(RoomInventoryMemory.Read(_game));

        _game.Set(GameMemoryAddresses.Player.CurrentSword.Address, RoomInventory.HerosSword);   // restored
        _game.SetU32(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 4);
        Assert.Equal(RoomInventory.HerosSword, RoomInventoryMemory.Read(_game)!.EquippedSword);
    }

    [Fact]
    public void Apply_DoesNotWriteEquipment_DuringASwap()
    {
        var local = RoomInventoryMemory.Read(_game)!;
        _game.SetU32(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 1);
        var room = new RoomInventory { Swords = 0x01, EquippedSword = RoomInventory.HerosSword }.Normalize();

        var after = RoomInventoryMemory.Apply(_game, room, local, applySword: true, applyShield: false, []);

        Assert.Equal(RoomInventory.NoItem, _game.Get(GameMemoryAddresses.Player.CurrentSword.Address));
        Assert.Equal(RoomInventory.NoItem, after.EquippedSword); // skipped: the caller re-checks next tick
        Assert.Equal(0x01, _game.Get(GameMemoryAddresses.Inventory.SwordsBitfield.Address)); // the rest still applied
    }

    [Fact]
    public void Normalize_EquippedSwordAndShieldCountAsOwned()
    {
        // Debug tools / cutscenes can set the equip byte without the mCollect ownership bit.
        var inv = new RoomInventory { EquippedSword = RoomInventory.HerosSword, EquippedShield = RoomInventory.MirrorShield }.Normalize();
        Assert.Equal(RoomInventory.SwordBit(RoomInventory.HerosSword), inv.Swords);
        Assert.Equal(RoomInventory.ShieldBit(RoomInventory.MirrorShield), inv.Shields);
    }
}

/// <summary>The sea chart menu's save data (charts owned / opened / completed, sea squares, Triforce charts) up-merges.</summary>
public class RoomInventorySeaMapTests
{
    [Fact]
    public void SeaMap_Addresses_AreDSvPlayerMap()
    {
        // dSv_player_c /* 0x0C4 */ mMap at g_dComIfG_gameInfo 0x803C4C08: field_0x0[1] at +0x10, field_0x81 at +0x81.
        Assert.Equal(0x803C4CDCu, GameMemoryAddresses.Inventory.SeaMapCharts);
        Assert.Equal(0x803C4D4Du, GameMemoryAddresses.Inventory.SeaMapTriforce);
        Assert.Equal(RoomInventory.SeaMapLength - 1, GameMemoryAddresses.Inventory.SeaMapChartsAndSquaresLength);
    }

    [Fact]
    public void SeaMap_MergesUp_AndGainsAreOnlyNewBits()
    {
        var room = new RoomInventory();
        var peer = new RoomInventory();
        peer.SeaMap[0] = 0x05;                                        // two charts owned
        peer.SeaMap[RoomInventory.SeaMapChartBytes + 10] = 0x01;       // a square visited
        peer.SeaMap[RoomInventory.SeaMapLength - 1] = 0x80;            // a Triforce chart deciphered
        Assert.True(room.MergeGainsFrom(peer));
        Assert.False(room.MergeGainsFrom(peer));
        Assert.Equal(2, room.ChartsOwned);
        Assert.Equal(1, room.SquaresVisited);

        var mine = room.Clone();
        mine.SeaMap[0] |= 0x02;
        var gains = mine.GainsOver(room);
        Assert.Equal(0x02, gains.SeaMap[0]);
        Assert.Equal(0, gains.SeaMap[RoomInventory.SeaMapLength - 1]);
        Assert.False(gains.IsEmpty);
    }

    [Fact]
    public void SeaMap_WrongLength_IsInvalid()
    {
        Assert.False(new RoomInventory { SeaMap = new byte[3] }.IsValid());
        Assert.True(new RoomInventory().IsValid());
    }

    [Fact]
    public void SeaMap_ReadAndApply_OrIntoTheSave()
    {
        var game = new FakeDolphin();
        game.Set(GameMemoryAddresses.Inventory.ItemSlots.Address, RoomInventory.NewEmptySlots());
        game.Set(GameMemoryAddresses.Player.CurrentSword.Address, RoomInventory.NoItem, RoomInventory.NoItem);
        game.Set(GameMemoryAddresses.Inventory.SeaMapCharts, 0x01);          // we own chart 0
        game.Set(GameMemoryAddresses.Inventory.SeaMapCharts + 0x30, 0x03);   // Forsaken Fortress square seen

        var local = RoomInventoryMemory.Read(game)!;
        Assert.Equal(1, local.ChartsOwned);

        var room = local.Clone();
        room.SeaMap[0] = 0x02;                                               // the room has chart 1 (not 0)
        room.SeaMap[RoomInventory.SeaMapChartBytes + 20] = 0x01;
        room.SeaMap[RoomInventory.SeaMapLength - 1] = 0x01;
        var after = RoomInventoryMemory.Apply(game, room, local, applySword: false, applyShield: false, []);

        Assert.Equal(0x03, game.Get(GameMemoryAddresses.Inventory.SeaMapCharts));          // OR, ours kept
        Assert.Equal(0x01, game.Get(GameMemoryAddresses.Inventory.SeaMapCharts + 0x30 + 20 - 0));
        Assert.Equal(0x01, game.Get(GameMemoryAddresses.Inventory.SeaMapTriforce));
        Assert.Equal(0x03, game.Get(GameMemoryAddresses.Inventory.SeaMapCharts + 0x30));   // untouched square bits
        Assert.Equal(2, after.ChartsOwned);
    }
}
