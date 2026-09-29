using System.Numerics;

namespace WWOnline.Shared.Models;

/// <summary>
/// The room's shared item ownership ("room inventory"), owned by the server while the
/// SharedItems room rule is on. Everything here is a grow-only merge between players
/// (<see cref="MergeGainsFrom"/>: bitfields OR, levels MAX, empty slots filled / upgraded);
/// only the room owner can remove or downgrade, by replacing the whole state.
///
/// Per-player and NEVER in here: current health / magic / arrow / bomb counts, bottle
/// CONTENTS (a bottle slot only records "owned"), bag contents (spoils, bait, delivery bag —
/// the trading sequence lives there), small-key counts, rupees (shared wallet).
///
/// Field layout mirrors dSv_player_c (tww-decomp include/d/d_save.h); the client maps it to
/// memory in RoomInventoryMemory.
/// </summary>
public class RoomInventory
{
    public const int SlotCount = 21;
    public const byte NoItem = 0xFF;

    public const int BowSlot = 12;
    public const int PictoBoxSlot = 8;
    public const int FirstBottleSlot = 14;
    public const int BottleSlotCount = 4;
    /// <summary>dItemNo_EMPTY_BOTTLE_e — how an owned bottle is recorded, whatever it holds.</summary>
    public const byte EmptyBottle = 0x50;

    // Item ids (tww-decomp d_item_data.h)
    public const byte HerosSword = 0x38, MasterSword1 = 0x39, MasterSword2 = 0x3A, MasterSword3 = 0x3E;
    public const byte HerosShield = 0x3B, MirrorShield = 0x3C;
    public const byte PowerBraceletsItem = 0x28;
    public const byte BowItem = 0x27, MagicArrowItem = 0x35, LightArrowItem = 0x36;
    public const byte PictoBoxItem = 0x23, DeluxePictoBoxItem = 0x26;

    // Known bits of each ownership byte. Unknown bits are never synced (and never written).
    public const byte SwordMask = 0x0F;     // dSv_player_collect_c mCollect[0]: Hero's, Master x3
    public const byte ShieldMask = 0x03;    // mCollect[1]: Hero's, Mirror
    public const byte BraceletMask = 0x01;  // mCollect[2]
    /// <summary>mCollect[3]/[4] bit 0 = owned. Hero's Charm bit 1 is the per-player "wearing it" toggle.</summary>
    public const byte CharmMask = 0x01;
    public const byte SongMask = 0x3F;      // mTact: 6 songs
    public const byte PearlMask = 0x07;     // mSymbol: Nayru, Din, Farore

    /// <summary>The heart catalogue's source IDs (WWOnline.Patcher HeartCatalog.SourceIds: 44 Pieces of Heart and 6
    /// Heart Containers, docs/hearts.md). Part of the wire format: IDs are never reordered, only appended.</summary>
    public const int HeartSourceCount = 50;
    public const ulong HeartSourceMask = (1UL << HeartSourceCount) - 1;

    // Level limits (validation)
    public const ushort MaxHealthLimit = 80; // 20 hearts, in quarter hearts
    public const byte MaxMagicLimit = 32;
    public const byte MaxAmmoLimit = 99;
    public const byte MaxWalletSize = 2;

    public static readonly string[] SlotNames =
    [
        "Telescope", "Sail", "Wind Waker", "Grappling Hook", "Spoils Bag", "Boomerang", "Deku Leaf",
        "Tingle Tuner", "Picto Box", "Iron Boots", "Magic Armor", "Bait Bag", "Bow", "Bombs",
        "Bottle 1", "Bottle 2", "Bottle 3", "Bottle 4", "Delivery Bag", "Hookshot", "Skull Hammer",
    ];

    /// <summary>Server-assigned, bumped on every change. Clients ignore pushes older than what they have.</summary>
    public long Revision { get; set; }

    /// <summary>dSv_player_item_c::mItems — item id per slot (0xFF = empty). Bottles are 0x50 or 0xFF.</summary>
    public byte[] Items { get; set; } = NewEmptySlots();

    /// <summary>dSv_player_get_item_c::mItemFlags — per-slot "ever obtained" bits.</summary>
    public byte[] ItemGetFlags { get; set; } = new byte[SlotCount];

    public byte Swords { get; set; }
    public byte Shields { get; set; }
    public byte PowerBracelets { get; set; }
    public byte PiratesCharm { get; set; }
    public byte HerosCharm { get; set; }
    public byte Songs { get; set; }
    public byte TriforceShards { get; set; }
    public byte Pearls { get; set; }

    /// <summary>Heart containers + pieces, in quarter hearts. Not merged by clients that derive it from
    /// <see cref="HeartSources"/> (docs/hearts.md); kept for the room summary.</summary>
    public ushort MaxHealth { get; set; }

    /// <summary>Grow-only set of heart sources anyone in the room has taken: bit N = catalogue source N
    /// (<see cref="HeartSourceCount"/> bits). Max health is derived from it, so a piece two players both take
    /// counts once.</summary>
    public ulong HeartSources { get; set; }
    public byte MaxMagic { get; set; }
    /// <summary>Quiver capacity (dSv_player_item_max_c) — NOT the arrow count.</summary>
    public byte MaxArrows { get; set; }
    /// <summary>Bomb bag capacity — NOT the bomb count.</summary>
    public byte MaxBombs { get; set; }
    public byte WalletSize { get; set; }

    /// <summary>The room's equipped sword / shield item id (0xFF = none).</summary>
    public byte EquippedSword { get; set; } = NoItem;
    public byte EquippedShield { get; set; } = NoItem;

    public static byte[] NewEmptySlots() => Enumerable.Repeat(NoItem, SlotCount).ToArray();

    public static bool IsBottleSlot(int slot) => slot >= FirstBottleSlot && slot < FirstBottleSlot + BottleSlotCount;

    /// <summary>
    /// Upgrade order within a slot: the Bow slot becomes Fire &amp; Ice then Light Arrows, the Picto
    /// Box becomes the Deluxe Picto Box. -1 = empty, 0 = any other item.
    /// </summary>
    public static int UpgradeRank(int slot, byte item)
    {
        if (item == NoItem) return -1;
        return (slot, item) switch
        {
            (BowSlot, BowItem) => 1,
            (BowSlot, MagicArrowItem) => 2,
            (BowSlot, LightArrowItem) => 3,
            (PictoBoxSlot, PictoBoxItem) => 1,
            (PictoBoxSlot, DeluxePictoBoxItem) => 2,
            _ => 0,
        };
    }

    public static int SwordTier(byte id) => id switch
    {
        HerosSword => 1, MasterSword1 => 2, MasterSword2 => 3, MasterSword3 => 4, _ => 0,
    };

    public static int ShieldTier(byte id) => id switch
    {
        HerosShield => 1, MirrorShield => 2, _ => 0,
    };

    /// <summary>mCollect[0] bit for a sword item id (0 if none).</summary>
    public static byte SwordBit(byte id) => id switch
    {
        HerosSword => 0x01, MasterSword1 => 0x02, MasterSword2 => 0x04, MasterSword3 => 0x08, _ => 0,
    };

    /// <summary>mCollect[1] bit for a shield item id (0 if none).</summary>
    public static byte ShieldBit(byte id) => id switch
    {
        HerosShield => 0x01, MirrorShield => 0x02, _ => 0,
    };

    /// <summary>Structure and range check — call before <see cref="Normalize"/> on anything from the network.</summary>
    public bool IsValid() =>
        Items is { Length: SlotCount } &&
        ItemGetFlags is { Length: SlotCount } &&
        MaxHealth <= MaxHealthLimit &&
        (HeartSources & ~HeartSourceMask) == 0 &&
        MaxMagic <= MaxMagicLimit &&
        MaxArrows <= MaxAmmoLimit &&
        MaxBombs <= MaxAmmoLimit &&
        WalletSize <= MaxWalletSize &&
        (EquippedSword == NoItem || SwordTier(EquippedSword) > 0) &&
        (EquippedShield == NoItem || ShieldTier(EquippedShield) > 0);

    /// <summary>Drop unknown bits and record any non-empty bottle as an empty bottle. Returns this.</summary>
    public RoomInventory Normalize()
    {
        // You can't legitimately have a sword/shield equipped that you don't own; debug tools and
        // cutscenes can set the equip byte without the mCollect bit, which made the room think
        // the owner had no sword. Equipped ⇒ owned.
        Swords |= SwordBit(EquippedSword);
        Shields |= ShieldBit(EquippedShield);
        Swords &= SwordMask;
        Shields &= ShieldMask;
        PowerBracelets &= BraceletMask;
        PiratesCharm &= CharmMask;
        HerosCharm &= CharmMask;
        Songs &= SongMask;
        Pearls &= PearlMask;
        HeartSources &= HeartSourceMask;
        for (int i = FirstBottleSlot; i < FirstBottleSlot + BottleSlotCount; i++)
            if (Items[i] != NoItem) Items[i] = EmptyBottle;
        return this;
    }

    /// <summary>
    /// Merge another player's gains into this: bitfields OR, levels MAX, an empty slot is filled
    /// (or a Bow / Picto Box upgraded), a higher-tier equipped sword / shield wins.
    /// Never removes or downgrades anything. Returns true if anything changed.
    /// </summary>
    public bool MergeGainsFrom(RoomInventory other)
    {
        bool changed = false;
        for (int i = 0; i < SlotCount; i++)
        {
            byte theirs = other.Items[i];
            if (theirs != NoItem && UpgradeRank(i, theirs) > UpgradeRank(i, Items[i]))
            {
                Items[i] = IsBottleSlot(i) ? EmptyBottle : theirs;
                changed = true;
            }
            ItemGetFlags[i] = Or(ItemGetFlags[i], other.ItemGetFlags[i], ref changed);
        }

        Swords = Or(Swords, other.Swords, ref changed);
        Shields = Or(Shields, other.Shields, ref changed);
        PowerBracelets = Or(PowerBracelets, other.PowerBracelets, ref changed);
        PiratesCharm = Or(PiratesCharm, other.PiratesCharm, ref changed);
        HerosCharm = Or(HerosCharm, other.HerosCharm, ref changed);
        Songs = Or(Songs, other.Songs, ref changed);
        TriforceShards = Or(TriforceShards, other.TriforceShards, ref changed);
        Pearls = Or(Pearls, other.Pearls, ref changed);
        ulong hearts = HeartSources | (other.HeartSources & HeartSourceMask);
        if (hearts != HeartSources) { HeartSources = hearts; changed = true; }

        if (other.MaxHealth > MaxHealth) { MaxHealth = other.MaxHealth; changed = true; }
        MaxMagic = Max(MaxMagic, other.MaxMagic, ref changed);
        MaxArrows = Max(MaxArrows, other.MaxArrows, ref changed);
        MaxBombs = Max(MaxBombs, other.MaxBombs, ref changed);
        WalletSize = Max(WalletSize, other.WalletSize, ref changed);

        if (SwordTier(other.EquippedSword) > SwordTier(EquippedSword)) { EquippedSword = other.EquippedSword; changed = true; }
        if (ShieldTier(other.EquippedShield) > ShieldTier(EquippedShield)) { EquippedShield = other.EquippedShield; changed = true; }
        return changed;
    }

    /// <summary>
    /// Only what this (a player's current state) gained over <paramref name="baseline"/>: new bits,
    /// higher levels, newly filled or upgraded slots, a better equipped sword / shield. Losses are
    /// ignored — they never propagate to the room.
    /// </summary>
    public RoomInventory GainsOver(RoomInventory baseline)
    {
        var g = new RoomInventory();
        for (int i = 0; i < SlotCount; i++)
        {
            if (Items[i] != NoItem && UpgradeRank(i, Items[i]) > UpgradeRank(i, baseline.Items[i]))
                g.Items[i] = Items[i];
            g.ItemGetFlags[i] = (byte)(ItemGetFlags[i] & ~baseline.ItemGetFlags[i]);
        }
        g.Swords = (byte)(Swords & ~baseline.Swords);
        g.Shields = (byte)(Shields & ~baseline.Shields);
        g.PowerBracelets = (byte)(PowerBracelets & ~baseline.PowerBracelets);
        g.PiratesCharm = (byte)(PiratesCharm & ~baseline.PiratesCharm);
        g.HerosCharm = (byte)(HerosCharm & ~baseline.HerosCharm);
        g.Songs = (byte)(Songs & ~baseline.Songs);
        g.TriforceShards = (byte)(TriforceShards & ~baseline.TriforceShards);
        g.Pearls = (byte)(Pearls & ~baseline.Pearls);
        g.HeartSources = HeartSources & ~baseline.HeartSources;
        g.MaxHealth = MaxHealth > baseline.MaxHealth ? MaxHealth : (ushort)0;
        g.MaxMagic = MaxMagic > baseline.MaxMagic ? MaxMagic : (byte)0;
        g.MaxArrows = MaxArrows > baseline.MaxArrows ? MaxArrows : (byte)0;
        g.MaxBombs = MaxBombs > baseline.MaxBombs ? MaxBombs : (byte)0;
        g.WalletSize = WalletSize > baseline.WalletSize ? WalletSize : (byte)0;
        if (SwordTier(EquippedSword) > SwordTier(baseline.EquippedSword)) g.EquippedSword = EquippedSword;
        if (ShieldTier(EquippedShield) > ShieldTier(baseline.EquippedShield)) g.EquippedShield = EquippedShield;
        return g;
    }

    /// <summary>True when this holds nothing at all (e.g. <see cref="GainsOver"/> found no gains).</summary>
    public bool IsEmpty =>
        Items.All(b => b == NoItem) && ItemGetFlags.All(b => b == 0) &&
        Swords == 0 && Shields == 0 && PowerBracelets == 0 && PiratesCharm == 0 && HerosCharm == 0 &&
        Songs == 0 && TriforceShards == 0 && Pearls == 0 && HeartSources == 0 &&
        MaxHealth == 0 && MaxMagic == 0 && MaxArrows == 0 && MaxBombs == 0 && WalletSize == 0 &&
        EquippedSword == NoItem && EquippedShield == NoItem;

    /// <summary>Same synced content (ignores <see cref="Revision"/>).</summary>
    public bool ContentEquals(RoomInventory other) => Describe(this, other).Count == 0;

    public RoomInventory Clone()
    {
        var c = (RoomInventory)MemberwiseClone();
        c.Items = (byte[])Items.Clone();
        c.ItemGetFlags = (byte[])ItemGetFlags.Clone();
        return c;
    }

    /// <summary>
    /// Human-readable differences from <paramref name="before"/> to <paramref name="after"/> for the
    /// [items] log lines, e.g. "Bow —→27", "swords 01→03", "max health 12→16".
    /// </summary>
    public static List<string> Describe(RoomInventory before, RoomInventory after)
    {
        var d = new List<string>();
        for (int i = 0; i < SlotCount; i++)
        {
            if (before.Items[i] != after.Items[i])
                d.Add($"{SlotNames[i]} {Hex(before.Items[i])}→{Hex(after.Items[i])}");
            if (before.ItemGetFlags[i] != after.ItemGetFlags[i])
                d.Add($"{SlotNames[i]} get-flags {before.ItemGetFlags[i]:X2}→{after.ItemGetFlags[i]:X2}");
        }
        Bits(d, "swords", before.Swords, after.Swords);
        Bits(d, "shields", before.Shields, after.Shields);
        Bits(d, "power bracelets", before.PowerBracelets, after.PowerBracelets);
        Bits(d, "pirate's charm", before.PiratesCharm, after.PiratesCharm);
        Bits(d, "hero's charm", before.HerosCharm, after.HerosCharm);
        Bits(d, "songs", before.Songs, after.Songs);
        Bits(d, "triforce", before.TriforceShards, after.TriforceShards);
        Bits(d, "pearls", before.Pearls, after.Pearls);
        Level(d, "max health", before.MaxHealth, after.MaxHealth);
        if (before.HeartSources != after.HeartSources)
            d.Add($"heart sources {BitOperations.PopCount(before.HeartSources)}→{BitOperations.PopCount(after.HeartSources)} (+{string.Join(",", Bits64(after.HeartSources & ~before.HeartSources))})");
        Level(d, "max magic", before.MaxMagic, after.MaxMagic);
        Level(d, "quiver", before.MaxArrows, after.MaxArrows);
        Level(d, "bomb bag", before.MaxBombs, after.MaxBombs);
        Level(d, "wallet size", before.WalletSize, after.WalletSize);
        if (before.EquippedSword != after.EquippedSword)
            d.Add($"equipped sword {Hex(before.EquippedSword)}→{Hex(after.EquippedSword)}");
        if (before.EquippedShield != after.EquippedShield)
            d.Add($"equipped shield {Hex(before.EquippedShield)}→{Hex(after.EquippedShield)}");
        return d;
    }

    /// <summary>One-line overview for logs.</summary>
    public string Summary() =>
        $"rev {Revision}: {Items.Count(b => b != NoItem)} item(s), swords {Swords:X2}, shields {Shields:X2}, " +
        $"bracelets {PowerBracelets:X2}, charms {PiratesCharm:X2}/{HerosCharm:X2}, songs {Songs:X2}, " +
        $"triforce {BitOperations.PopCount(TriforceShards)}/8, pearls {Pearls:X2}, max health {MaxHealth}, heart sources {BitOperations.PopCount(HeartSources)}, " +
        $"max magic {MaxMagic}, quiver {MaxArrows}, bomb bag {MaxBombs}, wallet size {WalletSize}, " +
        $"equipped {Hex(EquippedSword)}/{Hex(EquippedShield)}";

    public override string ToString() => Summary();

    private static string Hex(byte b) => b == NoItem ? "—" : b.ToString("X2");

    private static void Bits(List<string> d, string name, byte a, byte b)
    {
        if (a != b) d.Add($"{name} {a:X2}→{b:X2}");
    }

    private static IEnumerable<int> Bits64(ulong v)
    {
        for (int i = 0; i < 64; i++)
            if ((v & (1UL << i)) != 0) yield return i;
    }

    private static void Level(List<string> d, string name, int a, int b)
    {
        if (a != b) d.Add($"{name} {a}→{b}");
    }

    private static byte Or(byte a, byte b, ref bool changed)
    {
        byte r = (byte)(a | b);
        if (r != a) changed = true;
        return r;
    }

    private static byte Max(byte a, byte b, ref bool changed)
    {
        if (b <= a) return a;
        changed = true;
        return b;
    }
}
