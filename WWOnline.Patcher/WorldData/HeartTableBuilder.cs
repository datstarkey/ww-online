using WWOnline.Patcher.BinaryFormats.Dol;

namespace WWOnline.Patcher.WorldData;

/// <summary>
/// Builds the <see cref="HeartTable"/> from the player's own game (the same stage reader and object-name table as
/// the small-key table; never written anywhere) plus <see cref="HeartCatalog.Rewards"/>. From the stage files,
/// over every layer, deduplicated by flag (the Windfall chest is on three layers, the jail chest in three FF stages,
/// each sunken treasure has four salvage points of which the game picks one):
/// <list type="bullet">
/// <item>Chests (<c>daTbox_c</c>, proc 0x126): item <c>home.angle.z &gt;&gt; 8</c>, tbox bit <c>prm &gt;&gt; 7 &amp; 0x1F</c>
/// (d_a_tbox.h:35-37) of the stage's slot, or of STAGE_SEA2 for function types 7/8 (d_a_tbox.cpp:854-858).</item>
/// <item>Placed items (<c>daItem_c</c>, 0x101) and dug-up items (<c>daTagKbItem_c</c>, 0x1A4, the Outset pig's black
/// soil): item <c>prm &amp; 0xFF</c>, item bit <c>prm &gt;&gt; 8 &amp; 0xFF</c> (d_a_item.h:141-142, d_a_tag_kb_item.cpp:9-13);
/// memory item bits are below 0x20 (d_save.cpp:1637).</item>
/// <item>Salvage points (<c>daSalvage_c</c>, 0x194): item <c>prm &gt;&gt; 4</c>, kind <c>prm &gt;&gt; 28</c>, save number
/// <c>prm &gt;&gt; 20</c> (d_salvage.cpp:12-19). Kind 0 (a treasure chart's sunken treasure) saves the chart's
/// complete-map bit, kinds 2-4 the ocean bit (room, save number) unless the save number is 31 (end_salvage,
/// d_a_salvage.cpp:502-520).</item>
/// <item>Boss containers (<c>bossitem_class</c>, 0x102): its parameter is the boss's save slot; the container sets
/// that slot's STAGE_LIFE (d_a_boss_item.cpp:25-40, d_item.cpp:587-603).</item>
/// </list>
/// Heart items: dItemNo_HEART_PIECE_e 0x07, dItemNo_HEART_PIECE_ALT_e 0x3F (1 quarter), dItemNo_HEART_CONTAINER_e 0x08 (4).
/// </summary>
public static class HeartTableBuilder
{
    public const int HeartPiece = 0x07, HeartContainer = 0x08, HeartPieceAlt = 0x3F;
    public const short ProcItem = SmallKeyTableBuilder.ProcItem, ProcTbox = SmallKeyTableBuilder.ProcTbox;
    public const short ProcBossItem = 0x102, ProcSalvage = 0x194, ProcTagKbItem = 0x1A4;

    private const int TboxExtraSaveInfo = 7, TboxExtraSaveInfoSpawn = 8, SeaTwoSlot = 1;
    private const int MemoryItemBits = 32, NoSalvageSave = 31, OceanGrids = HeartFlagState.OceanGrids;

    /// <summary>Quarter hearts an item gives, or 0 when it isn't a heart.</summary>
    public static int HeartQuarters(int item) => item switch
    {
        HeartPiece or HeartPieceAlt => 1,
        HeartContainer => HeartTable.ContainerQuarters,
        _ => 0,
    };

    /// <summary>The table for an extracted game folder (sys/main.dol + files/res/Stage), or null when it has none.</summary>
    public static HeartTable? BuildFromGame(string gamePath, Action<string>? skipped = null)
    {
        var dolPath = Path.Combine(gamePath, "sys", "main.dol");
        var stages = StageDataReader.StagesPath(gamePath);
        if (stages == null || !File.Exists(dolPath))
            return null;
        var procs = SmallKeyTableBuilder.ReadObjectNames(new DolPatcher(dolPath));
        return Build(StageDataReader.ReadAll(stages, skipped ?? (_ => { })).ToList(), procs);
    }

    /// <summary>The table for these stages (<paramref name="procs"/>: object name → process) plus the catalogue's rewards.</summary>
    public static HeartTable Build(IReadOnlyCollection<StageData> stages, IReadOnlyDictionary<string, short> procs,
        IReadOnlyList<HeartSource>? rewards = null)
    {
        var found = new Dictionary<HeartFlag, HeartSource>();
        var untracked = new List<string>();
        // Every chest / item flag any actor uses, to catch a heart flag another actor shares.
        var users = new Dictionary<HeartFlag, List<string>>();

        foreach (var stage in stages)
        {
            if (stage.SaveTbl == HeartCatalog.TestSlot || HeartCatalog.UnreachableStages.Contains(stage.Name)) continue;
            foreach (var a in stage.Actors)
            {
                if (!procs.TryGetValue(a.Name, out short proc)) continue;
                if (FlagOf(proc, a, stage.SaveTbl) is { } used)
                {
                    if (!users.TryGetValue(used, out var list)) users[used] = list = [];
                    list.Add($"{Where(a)} (item 0x{ItemOf(proc, a):X2})");
                }
                switch (Classify(proc, a, stage.SaveTbl))
                {
                    case (HeartSource source, null):
                        found.TryAdd(source.Flag, source);
                        break;
                    case (null, string what):
                        untracked.Add(what);
                        break;
                }
            }
        }

        // A heart flag another (non-heart) chest or item also sets would count that one as a heart.
        foreach (var flag in found.Keys)
        {
            if (!users.TryGetValue(flag, out var list)) continue;
            var others = list.Where(u => !IsHeartItemText(u)).Distinct().ToList();
            if (others.Count > 0)
                untracked.Add($"{flag} is also set by {string.Join("; ", others)}");
        }

        var sources = found.Values
            .Select(s => HeartCatalog.StageSourceNames.TryGetValue(s.Flag, out var name) ? s with { Name = name } : s)
            .OrderBy(s => s.Type).ThenBy(s => s.Flag.Kind).ThenBy(s => s.Flag.A).ThenBy(s => s.Flag.B)
            .Concat(rewards ?? HeartCatalog.Rewards)
            .ToList();
        return new HeartTable { StageCount = stages.Count, Sources = sources, Untracked = untracked.Distinct().ToList() };
    }

    /// <summary>
    /// What a placed actor is to the hearts: a heart source with its flag, a heart whose flag the save can't show
    /// (its description), or nothing.
    /// </summary>
    public static (HeartSource? Source, string? Untracked) Classify(short proc, PlacedActor a, int slot)
    {
        uint p = a.Params;
        int item = ItemOf(proc, a);
        int quarters = HeartQuarters(item);
        switch (proc)
        {
            case ProcTbox when quarters > 0:
            {
                int func = (int)(p & 0x7F);
                int tboxSlot = func is TboxExtraSaveInfo or TboxExtraSaveInfoSpawn ? SeaTwoSlot : slot;
                return (Source(HeartFlag.Chest(tboxSlot, (int)(p >> 7) & 0x1F), quarters, HeartSourceType.Chest, a), null);
            }
            case ProcItem or ProcTagKbItem when quarters > 0:
            {
                int bit = (int)(p >> 8) & 0xFF;
                if (bit < MemoryItemBits && !(proc == ProcTagKbItem && bit == 0x1F)) // 0x1F: a dug-up item with no flag
                    return (Source(HeartFlag.Item(slot, bit), quarters, HeartSourceType.PlacedItem, a), null);
                return (null, $"{Where(a)}: heart item with item bit 0x{bit:X2} (not a memory item bit)");
            }
            case ProcSalvage when quarters > 0:
            {
                int kind = (int)(p >> 28), save = (int)(p >> 20) & 0xFF;
                // cmapNo: the actor's room, or the parameter's room for an actor in room 0 (dSalvage_control_c::entry).
                int grid = a.Room != 0 ? a.Room : (int)(p >> 12) & 0xFF;
                if (kind == 0 && save is >= 1 and <= 128)
                    return (Source(HeartFlag.Chart(save), quarters, HeartSourceType.Salvage, a), null);
                if (kind is 2 or 3 or 4 && save <= 15 && grid is > 0 and < OceanGrids)
                    return (Source(HeartFlag.Ocean(grid, save), quarters, HeartSourceType.Salvage, a), null);
                return (null, $"{Where(a)}: heart salvage kind {kind} save {save} (no save flag the table reads)");
            }
            case ProcBossItem:
            {
                int bossSlot = (int)(p & 0xFF);
                if (bossSlot is >= 0 and < HeartFlagState.SlotCount)
                    return (Source(HeartFlag.StageLife(bossSlot), HeartTable.ContainerQuarters, HeartSourceType.Boss, a), null);
                return (null, $"{Where(a)}: boss item for slot {bossSlot}");
            }
        }
        return (null, null);
    }

    /// <summary>The item an actor holds (or -1): chests keep it in angle.z, salvage in the parameter's bits 4-11.</summary>
    private static int ItemOf(short proc, PlacedActor a) => proc switch
    {
        ProcTbox => (a.AngleZ >> 8) & 0xFF,
        ProcItem or ProcTagKbItem => (int)(a.Params & 0xFF),
        ProcSalvage => (int)(a.Params >> 4) & 0xFF,
        _ => -1,
    };

    /// <summary>The chest / item flag an actor sets when taken, heart or not (for the shared-flag check).</summary>
    private static HeartFlag? FlagOf(short proc, PlacedActor a, int slot)
    {
        uint p = a.Params;
        switch (proc)
        {
            case ProcTbox:
            {
                int func = (int)(p & 0x7F);
                return HeartFlag.Chest(func is TboxExtraSaveInfo or TboxExtraSaveInfoSpawn ? SeaTwoSlot : slot, (int)(p >> 7) & 0x1F);
            }
            case ProcItem or ProcTagKbItem:
            {
                int bit = (int)(p >> 8) & 0xFF;
                return bit < MemoryItemBits ? HeartFlag.Item(slot, bit) : null;
            }
            default:
                return null;
        }
    }

    private static bool IsHeartItemText(string u) =>
        u.EndsWith($"(item 0x{HeartPiece:X2})", StringComparison.Ordinal) ||
        u.EndsWith($"(item 0x{HeartPieceAlt:X2})", StringComparison.Ordinal) ||
        u.EndsWith($"(item 0x{HeartContainer:X2})", StringComparison.Ordinal);

    private static HeartSource Source(HeartFlag flag, int quarters, HeartSourceType type, PlacedActor a) =>
        new(flag, quarters, type, Where(a), Where(a));

    private static string Where(PlacedActor a) => a.Room >= 0 ? $"{a.Stage} room {a.Room} {a.Name}" : $"{a.Stage} {a.Name}";
}
