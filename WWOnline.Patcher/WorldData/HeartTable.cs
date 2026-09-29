namespace WWOnline.Patcher.WorldData;

/// <summary>What kind of save flag records that a Piece of Heart or Heart Container was obtained (docs/hearts.md).</summary>
public enum HeartFlagKind
{
    /// <summary>dSv_memBit_c::mTbox bit <see cref="HeartFlag.B"/> of save slot <see cref="HeartFlag.A"/> (daTbox_c::OpenInit_com).</summary>
    Chest,
    /// <summary>dSv_memBit_c::mItem bit B of save slot A: a placed item (daItem_c) or a dug-up one (daTagKbItem_c).</summary>
    Item,
    /// <summary>dSv_memBit_c::mDungeonItem bit 4 (STAGE_LIFE) of save slot A: set only by item_func_utuwa_heart, i.e.
    /// when the boss's Heart Container is picked up (d_item.cpp:587-603).</summary>
    StageLife,
    /// <summary>dSv_player_map_c complete-map bit of treasure chart A (1-based): the chart's sunken treasure was
    /// salvaged (daSalvage_c::end_salvage, kind 0).</summary>
    ChartSalvaged,
    /// <summary>dSv_ocean_c grid A bit B: a switch-gated / fixed salvage point was salvaged (end_salvage, kinds 2-4).</summary>
    OceanSalvage,
    /// <summary>dSv_event_c bit A (0xBBMM: byte BB, mask MM): an NPC reward.</summary>
    EventBit,
    /// <summary>dSv_event_c register A (0xBBMM) holds at least B: an NPC reward counted in a register.</summary>
    EventRegAtLeast,
    /// <summary>Maggie's reward for Moblin's Letter: the letter was ever obtained (dSv_player_get_bag_item_c reserve
    /// bit 15) and is no longer in the delivery bag (she takes it before the heart; nobody else takes it).</summary>
    MoblinsLetterDelivered,
}

/// <summary>A save flag that says one heart source was taken. <see cref="A"/> / <see cref="B"/> per <see cref="HeartFlagKind"/>.</summary>
public readonly record struct HeartFlag(HeartFlagKind Kind, int A, int B = 0)
{
    public static HeartFlag Chest(int slot, int tbox) => new(HeartFlagKind.Chest, slot, tbox);
    public static HeartFlag Item(int slot, int bit) => new(HeartFlagKind.Item, slot, bit);
    public static HeartFlag StageLife(int slot) => new(HeartFlagKind.StageLife, slot);
    public static HeartFlag Chart(int chart) => new(HeartFlagKind.ChartSalvaged, chart);
    public static HeartFlag Ocean(int grid, int bit) => new(HeartFlagKind.OceanSalvage, grid, bit);
    public static HeartFlag Event(int id) => new(HeartFlagKind.EventBit, id);
    public static HeartFlag Register(int reg, int atLeast) => new(HeartFlagKind.EventRegAtLeast, reg, atLeast);
    public static HeartFlag MoblinsLetter { get; } = new(HeartFlagKind.MoblinsLetterDelivered, 0);

    /// <summary>Whether a room rule syncs the flag itself (the heart count doesn't need it: the room's heart-source set
    /// carries every source, docs/hearts.md): world flags (Shared world), event bits (Shared story,
    /// when the story sync mask has them) or neither (registers, charts, ocean bits and the delivery bag stay per player).</summary>
    public HeartFlagScope Scope => Kind switch
    {
        HeartFlagKind.Chest or HeartFlagKind.Item or HeartFlagKind.StageLife => HeartFlagScope.World,
        HeartFlagKind.EventBit => HeartFlagScope.Story,
        _ => HeartFlagScope.Local,
    };

    public override string ToString() => Kind switch
    {
        HeartFlagKind.Chest => $"slot {A} tbox {B}",
        HeartFlagKind.Item => $"slot {A} item bit {B}",
        HeartFlagKind.StageLife => $"slot {A} STAGE_LIFE",
        HeartFlagKind.ChartSalvaged => $"chart {A} salvaged",
        HeartFlagKind.OceanSalvage => $"ocean grid {A} bit {B}",
        HeartFlagKind.EventBit => $"event 0x{A:X4}",
        HeartFlagKind.EventRegAtLeast => $"register 0x{A:X4} >= {B}",
        HeartFlagKind.MoblinsLetterDelivered => "Moblin's Letter delivered",
        _ => $"{Kind} {A} {B}",
    };
}

/// <summary>Which room rule shares a heart flag between players.</summary>
public enum HeartFlagScope
{
    /// <summary>A world flag (dSv_memBit_c): OR-merged by Shared world.</summary>
    World,
    /// <summary>An event bit: OR-merged by Shared story (unless the story sync mask leaves it out).</summary>
    Story,
    /// <summary>Never synced: each player's own.</summary>
    Local,
}

/// <summary>Where a heart source is, for the docs and the logs.</summary>
public enum HeartSourceType
{
    Chest,
    PlacedItem,
    Salvage,
    Boss,
    Reward,
}

/// <summary>One Piece of Heart (1 quarter heart) or Heart Container (4) and the flag that says it was taken.</summary>
/// <param name="Where">Stage / room / actor it was found in (or the actor that gives it), for the logs.</param>
public readonly record struct HeartSource(HeartFlag Flag, int Quarters, HeartSourceType Type, string Name, string Where)
{
    public bool IsContainer => Quarters == HeartTable.ContainerQuarters;

    /// <summary>The catalogue's source ID (bit of the room's heart-source set), or null for a source it doesn't know.</summary>
    public int? Id => HeartCatalog.IdOf(Flag);
}

/// <summary>How many sources a save's flags show taken.</summary>
public readonly record struct HeartTally(int Containers, int Pieces, int OtherQuarters = 0)
{
    public int Quarters => Containers * HeartTable.ContainerQuarters + Pieces + OtherQuarters;

    /// <summary>The max health these flags give (dSv_player_status_a_c::mMaxLife, quarter hearts), 12..80.</summary>
    public int MaxLife => Math.Clamp(HeartTable.StartingQuarters + Quarters, 0, HeartTable.MaxQuarters);
}

/// <summary>
/// A save's heart flags: everything <see cref="HeartFlag"/> can point at. The current stage's memBits come
/// from the live copy (dSv_info_c::mMemory), the others from the save (dSv_save_c::mMemory[slot]).
/// </summary>
public sealed class HeartFlagState
{
    public const int SlotCount = 16, EventBytes = 256, MapWords = 4, OceanGrids = 50, BagSlots = 8;
    /// <summary>dItemNo_MOBLINS_LETTER_e (d_item_data.h) and its reserve index (item - dItemNo_TOWN_FLOWER_e 0x8C).</summary>
    public const byte MoblinsLetterItem = 0x9B;
    public const int MoblinsLetterReserveBit = MoblinsLetterItem - 0x8C;
    /// <summary>dSv_memBit_c::STAGE_LIFE (d_save.h:644).</summary>
    public const byte StageLifeMask = 1 << 4;

    public uint[] Tbox { get; } = new uint[SlotCount];
    public uint[] Item { get; } = new uint[SlotCount];
    public byte[] DungeonItem { get; } = new byte[SlotCount];
    /// <summary>dSv_save_c::mEvent mFlags[256].</summary>
    public byte[] Events { get; } = new byte[EventBytes];
    /// <summary>dSv_player_map_c field_0x0[3][4]: the complete-map bits (isCompleteMap).</summary>
    public uint[] CompleteMaps { get; } = new uint[MapWords];
    /// <summary>dSv_ocean_c field_0x0[50].</summary>
    public ushort[] Ocean { get; } = new ushort[OceanGrids];
    /// <summary>dSv_player_get_bag_item_c::mReserveFlags (delivery bag items ever obtained).</summary>
    public uint GetBagReserve { get; set; }
    /// <summary>dSv_player_bag_item_c::mReserve[8] (the delivery bag's items, 0xFF = empty).</summary>
    public byte[] DeliveryBag { get; } = Enumerable.Repeat((byte)0xFF, BagSlots).ToArray();

    public bool IsSet(HeartFlag f)
    {
        switch (f.Kind)
        {
            case HeartFlagKind.Chest: return InSlot(f.A) && Bit(Tbox[f.A], f.B);
            case HeartFlagKind.Item: return InSlot(f.A) && Bit(Item[f.A], f.B);
            case HeartFlagKind.StageLife: return InSlot(f.A) && (DungeonItem[f.A] & StageLifeMask) != 0;
            case HeartFlagKind.ChartSalvaged:
            {
                int i = f.A - 1; // dComIfGs_isCompleteCollectMap(no) = isCompleteMap(no - 1)
                return i is >= 0 and < MapWords * 32 && Bit(CompleteMaps[i >> 5], i & 31);
            }
            case HeartFlagKind.OceanSalvage: return f.A is >= 0 and < OceanGrids && f.B is >= 0 and < 16 && (Ocean[f.A] & (1 << f.B)) != 0;
            case HeartFlagKind.EventBit: return (Events[(f.A >> 8) & 0xFF] & f.A & 0xFF) != 0;
            case HeartFlagKind.EventRegAtLeast: return (Events[(f.A >> 8) & 0xFF] & f.A & 0xFF) >= f.B; // getEventReg: byte & mask
            case HeartFlagKind.MoblinsLetterDelivered:
                return Bit(GetBagReserve, MoblinsLetterReserveBit) && Array.IndexOf(DeliveryBag, MoblinsLetterItem) < 0;
            default: return false;
        }
    }

    /// <summary>Make <paramref name="f"/> read as set (tests, and the catalogue's all-flags check).</summary>
    public void Set(HeartFlag f)
    {
        switch (f.Kind)
        {
            case HeartFlagKind.Chest: Tbox[f.A] |= 1u << f.B; break;
            case HeartFlagKind.Item: Item[f.A] |= 1u << f.B; break;
            case HeartFlagKind.StageLife: DungeonItem[f.A] |= StageLifeMask; break;
            case HeartFlagKind.ChartSalvaged: CompleteMaps[(f.A - 1) >> 5] |= 1u << ((f.A - 1) & 31); break;
            case HeartFlagKind.OceanSalvage: Ocean[f.A] |= (ushort)(1 << f.B); break;
            case HeartFlagKind.EventBit: Events[(f.A >> 8) & 0xFF] |= (byte)(f.A & 0xFF); break;
            case HeartFlagKind.EventRegAtLeast:
            {
                int b = (f.A >> 8) & 0xFF, mask = f.A & 0xFF;
                if ((Events[b] & mask) < f.B) Events[b] = (byte)((Events[b] & ~mask) | (f.B & mask));
                break;
            }
            case HeartFlagKind.MoblinsLetterDelivered:
                GetBagReserve |= 1u << MoblinsLetterReserveBit;
                for (int i = 0; i < DeliveryBag.Length; i++)
                    if (DeliveryBag[i] == MoblinsLetterItem) DeliveryBag[i] = 0xFF;
                break;
        }
    }

    private static bool InSlot(int slot) => slot is >= 0 and < SlotCount;
    private static bool Bit(uint word, int bit) => bit is >= 0 and < 32 && (word & (1u << bit)) != 0;
}

/// <summary>
/// Every Piece of Heart and Heart Container and the flag that records it: chests, placed / dug-up items, salvage
/// points and boss containers from the player's own stage files (<see cref="HeartTableBuilder"/>, never in the repo),
/// plus the NPC rewards of <see cref="HeartCatalog"/>. A save's max health is
/// <c>12 + 4 × containers taken + pieces taken</c> (<see cref="Tally"/>), a source taken when its flag
/// is set here or its catalogue ID is in the room's grow-only source set: every player derives the same value and
/// nothing is counted twice (docs/hearts.md).
/// </summary>
public sealed class HeartTable
{
    /// <summary>The 3 starting hearts (dSv_player_status_a_c::init: mMaxLife = 12).</summary>
    public const int StartingQuarters = 12;
    /// <summary>20 hearts: d_meter's clamp (dMeter_LifeMove, maxHP &gt; 0x50) and RoomInventory.MaxHealthLimit.</summary>
    public const int MaxQuarters = 80;
    public const int ContainerQuarters = 4;
    /// <summary>The vanilla game (GZLE01): 44 Pieces of Heart and 6 Heart Containers, 12 + 44 + 24 = 80.</summary>
    public const int VanillaPieces = 44, VanillaContainers = 6;

    /// <summary>How many stages the stage-file part was built from (a sanity figure for the logs).</summary>
    public int StageCount { get; init; }

    public IReadOnlyList<HeartSource> Sources { get; init; } = [];

    /// <summary>Heart items the save can't record (a zone item bit, a salvage point that doesn't save): a heart from
    /// one shows up as a heart no flag explains (logged at runtime).</summary>
    public IReadOnlyList<string> Untracked { get; init; } = [];

    public int PiecesTotal => Sources.Count(s => !s.IsContainer);
    public int ContainersTotal => Sources.Count(s => s.IsContainer);

    /// <summary>The source IDs (bit N = catalogue source N) whose flag <paramref name="flags"/> shows taken: what this
    /// game adds to the room's heart-source set.</summary>
    public ulong SourceBits(HeartFlagState flags)
    {
        ulong bits = 0;
        foreach (var s in Sources)
            if (s.Id is int id && id < 64 && flags.IsSet(s.Flag)) bits |= 1UL << id;
        return bits;
    }

    /// <summary>The sources taken: their flag set in <paramref name="flags"/>, or their ID in <paramref name="roomSources"/>
    /// (the room's grow-only heart-source set). Each source counts once, however many players took it.</summary>
    public HeartTally Tally(HeartFlagState flags, ulong roomSources = 0)
    {
        int containers = 0, pieces = 0, other = 0;
        foreach (var s in Sources)
        {
            bool inRoom = s.Id is int id && id < 64 && (roomSources & (1UL << id)) != 0;
            if (!inRoom && !flags.IsSet(s.Flag)) continue;
            if (s.IsContainer) containers++;
            else if (s.Quarters == 1) pieces++;
            else other += s.Quarters;
        }
        return new HeartTally(containers, pieces, other);
    }

    /// <summary>"44 piece(s), 6 container(s): 12 chest, 2 placed item, ..." for the logs.</summary>
    public string Summary() =>
        $"{PiecesTotal} piece(s), {ContainersTotal} container(s): " +
        string.Join(", ", Sources.GroupBy(s => s.Type).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}")) +
        (Untracked.Count > 0 ? $" (+{Untracked.Count} untracked)" : "");
}
