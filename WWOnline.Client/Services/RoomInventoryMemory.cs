using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Maps <see cref="RoomInventory"/> to the save data in game memory (dSv_player_c at
/// g_dComIfG_gameInfo + 0). Every address comes from <see cref="GameMemoryAddresses"/>.
/// Only synced fields are ever written — never current health/magic/arrow/bomb counts
/// (except clamping health/magic down when the room owner lowers a maximum), bottle contents,
/// bag contents, keys or rupees.
/// </summary>
public static class RoomInventoryMemory
{
    // One read covers dSv_player_status_a_c (+0x00) through dSv_player_collect_c (ends +0xC1).
    private const uint BlockStart = GameMemoryAddresses.GameInfo;
    private static readonly int BlockLength = (int)(GameMemoryAddresses.Inventory.CollectEnd - BlockStart);

    private static readonly uint ItemsAddr = GameMemoryAddresses.Inventory.ItemSlots.Address;
    private static readonly uint GetFlagsAddr = GameMemoryAddresses.Inventory.ItemOwnership.Address;

    /// <summary>
    /// The game's current synced state (bottles normalized, unknown bits dropped), or null if
    /// unreadable — including while the puppet REL has a peer's sword / shield swapped into the
    /// equip bytes (<see cref="EquipSwapGuard"/>): the caller skips that tick.
    /// </summary>
    public static RoomInventory? Read(IDolphinService dolphin)
    {
        var buf = dolphin.ReadMemory(BlockStart, BlockLength);
        if (buf == null || buf.Length < BlockLength) return null;
        if (EquipSwapGuard.ReadEquipped(dolphin) is not { } equipped) return null;
        var (sword, shield) = equipped;

        byte B(MemoryAddress<byte> a) => buf[a.Address - BlockStart];
        ushort U16(MemoryAddress<ushort> a) => (ushort)(buf[a.Address - BlockStart] << 8 | buf[a.Address - BlockStart + 1]);

        var inv = new RoomInventory
        {
            Swords = B(GameMemoryAddresses.Inventory.SwordsBitfield),
            Shields = B(GameMemoryAddresses.Inventory.ShieldsBitfield),
            PowerBracelets = B(GameMemoryAddresses.Inventory.PowerBraceletsBitfield),
            PiratesCharm = B(GameMemoryAddresses.Inventory.PiratesCharmBitfield),
            HerosCharm = B(GameMemoryAddresses.Inventory.HerosCharmBitfield),
            Songs = B(GameMemoryAddresses.Inventory.SongsBitfield),
            TriforceShards = B(GameMemoryAddresses.Inventory.TriforceShards),
            Pearls = B(GameMemoryAddresses.Inventory.PearlsBitfield),
            MaxHealth = U16(GameMemoryAddresses.Player.MaxHealth),
            MaxMagic = B(GameMemoryAddresses.Player.MaxMagicMeter),
            MaxArrows = B(GameMemoryAddresses.Inventory.MaxArrows),
            MaxBombs = B(GameMemoryAddresses.Inventory.MaxBombs),
            WalletSize = B(GameMemoryAddresses.Player.CurrentWallet),
            EquippedSword = sword,
            EquippedShield = shield,
        };
        Array.Copy(buf, ItemsAddr - BlockStart, inv.Items, 0, RoomInventory.SlotCount);
        Array.Copy(buf, GetFlagsAddr - BlockStart, inv.ItemGetFlags, 0, RoomInventory.SlotCount);
        // Unknown equip ids (e.g. mid-cutscene values) read as "none" so they never become gains.
        if (RoomInventory.SwordTier(inv.EquippedSword) == 0) inv.EquippedSword = RoomInventory.NoItem;
        if (RoomInventory.ShieldTier(inv.EquippedShield) == 0) inv.EquippedShield = RoomInventory.NoItem;
        return inv.Normalize();
    }

    /// <summary>
    /// Write <paramref name="room"/> into the game wherever it differs from <paramref name="local"/>
    /// (the game's state as just read). Equipped sword / shield are only written when the caller
    /// says the room's choice changed, and only while the REL isn't swapping them (a skipped write
    /// leaves the local value in the result; the caller re-checks next tick with
    /// <see cref="WriteEquipped"/>). Returns what the game now holds; <paramref name="changes"/>
    /// gets one entry per field written.
    /// </summary>
    public static RoomInventory Apply(IDolphinService dolphin, RoomInventory room, RoomInventory local,
        bool applySword, bool applyShield, List<string> changes)
    {
        var after = local.Clone();

        for (int i = 0; i < RoomInventory.SlotCount; i++)
        {
            byte want = room.Items[i];
            byte have = local.Items[i];
            if (RoomInventory.IsBottleSlot(i))
            {
                // Bottles: ownership only. A bottle we gain arrives empty; contents are never touched.
                bool roomOwns = want != RoomInventory.NoItem, weOwn = have != RoomInventory.NoItem;
                if (roomOwns == weOwn) continue;
                want = roomOwns ? RoomInventory.EmptyBottle : RoomInventory.NoItem;
            }
            else if (want == have) continue;

            dolphin.WriteMemory(ItemsAddr + (uint)i, [want]);
            after.Items[i] = want;
        }

        for (int i = 0; i < RoomInventory.SlotCount; i++)
        {
            if (room.ItemGetFlags[i] == local.ItemGetFlags[i]) continue;
            dolphin.WriteMemory(GetFlagsAddr + (uint)i, [room.ItemGetFlags[i]]);
            after.ItemGetFlags[i] = room.ItemGetFlags[i];
        }

        after.Swords = WriteMasked(dolphin, GameMemoryAddresses.Inventory.SwordsBitfield, RoomInventory.SwordMask, local.Swords, room.Swords);
        after.Shields = WriteMasked(dolphin, GameMemoryAddresses.Inventory.ShieldsBitfield, RoomInventory.ShieldMask, local.Shields, room.Shields);
        after.PowerBracelets = WriteMasked(dolphin, GameMemoryAddresses.Inventory.PowerBraceletsBitfield, RoomInventory.BraceletMask, local.PowerBracelets, room.PowerBracelets);
        if (after.PowerBracelets != local.PowerBracelets)
        {
            // Getting the bracelets also wears them (item_func_pwr_groove → setSelectEquip(2, ...)).
            dolphin.Write(GameMemoryAddresses.Player.PowerBracelets,
                after.PowerBracelets != 0 ? RoomInventory.PowerBraceletsItem : RoomInventory.NoItem);
        }
        after.PiratesCharm = WriteMasked(dolphin, GameMemoryAddresses.Inventory.PiratesCharmBitfield, RoomInventory.CharmMask, local.PiratesCharm, room.PiratesCharm);
        after.HerosCharm = WriteMasked(dolphin, GameMemoryAddresses.Inventory.HerosCharmBitfield, RoomInventory.CharmMask, local.HerosCharm, room.HerosCharm);
        after.Songs = WriteMasked(dolphin, GameMemoryAddresses.Inventory.SongsBitfield, RoomInventory.SongMask, local.Songs, room.Songs);
        after.TriforceShards = WriteMasked(dolphin, GameMemoryAddresses.Inventory.TriforceShards, 0xFF, local.TriforceShards, room.TriforceShards);
        after.Pearls = WriteMasked(dolphin, GameMemoryAddresses.Inventory.PearlsBitfield, RoomInventory.PearlMask, local.Pearls, room.Pearls);

        if (room.MaxHealth != local.MaxHealth)
        {
            dolphin.Write(GameMemoryAddresses.Player.MaxHealth, room.MaxHealth);
            after.MaxHealth = room.MaxHealth;
            // Host lowered the max: don't leave current health above it (current is otherwise per-player).
            if (dolphin.Read(GameMemoryAddresses.Player.CurrentHealth) is ushort hp && hp > room.MaxHealth)
                dolphin.Write(GameMemoryAddresses.Player.CurrentHealth, room.MaxHealth);
        }
        if (room.MaxMagic != local.MaxMagic)
        {
            dolphin.Write(GameMemoryAddresses.Player.MaxMagicMeter, room.MaxMagic);
            after.MaxMagic = room.MaxMagic;
            if (dolphin.Read(GameMemoryAddresses.Player.CurrentMagicMeter) is byte mp && mp > room.MaxMagic)
                dolphin.Write(GameMemoryAddresses.Player.CurrentMagicMeter, room.MaxMagic);
        }
        // Capacities only — arrow / bomb COUNTS stay per-player and are never written.
        after.MaxArrows = WriteLevel(dolphin, GameMemoryAddresses.Inventory.MaxArrows, local.MaxArrows, room.MaxArrows);
        after.MaxBombs = WriteLevel(dolphin, GameMemoryAddresses.Inventory.MaxBombs, local.MaxBombs, room.MaxBombs);
        after.WalletSize = WriteLevel(dolphin, GameMemoryAddresses.Player.CurrentWallet, local.WalletSize, room.WalletSize);

        if (applySword)
            after.EquippedSword = WriteEquipped(dolphin, GameMemoryAddresses.Player.CurrentSword, local.EquippedSword, room.EquippedSword);
        if (applyShield)
            after.EquippedShield = WriteEquipped(dolphin, GameMemoryAddresses.Player.CurrentShield, local.EquippedShield, room.EquippedShield);

        changes.AddRange(RoomInventory.Describe(local, after));
        return after;
    }

    /// <summary>Set the known bits of a byte to <paramref name="value"/>, preserving bits we don't sync
    /// (e.g. the Hero's Charm "worn" bit). Read-modify-write so a concurrent game change to other bits survives.</summary>
    private static byte WriteMasked(IDolphinService dolphin, MemoryAddress<byte> addr, byte mask, byte current, byte value)
    {
        value &= mask;
        if ((current & mask) == value) return current;
        if (dolphin.Read(addr) is not byte raw) return current;
        dolphin.Write(addr, (byte)((raw & ~mask) | value));
        return value;
    }

    /// <summary>
    /// Set an equip byte (sword / shield) to <paramref name="value"/> via <see cref="EquipSwapGuard"/>;
    /// returns what the game holds afterwards as far as we know (<paramref name="current"/> if skipped).
    /// </summary>
    public static byte WriteEquipped(IDolphinService dolphin, MemoryAddress<byte> addr, byte current, byte value)
    {
        if (current == value) return current;
        return EquipSwapGuard.TryWrite(dolphin, addr, value) ? value : current;
    }

    private static byte WriteLevel(IDolphinService dolphin, MemoryAddress<byte> addr, byte current, byte value)
    {
        if (current == value) return current;
        dolphin.Write(addr, value);
        return value;
    }
}
