using System.Text;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Dol;

namespace WWOnline.Patcher.WorldData;

/// <summary>
/// Builds the <see cref="SmallKeyTable"/> from the player's own game: every actor placed in every stage
/// (<see cref="StageDataReader"/>) and main.dol's object-name → process table. Per save slot, over all
/// layers and every stage that uses the slot (Wind Temple's data is in both <c>kaze</c> and <c>Cave08</c>),
/// deduplicated by flag. The sources and doors, from tww-decomp:
/// <list type="bullet">
/// <item>Chests (<c>daTbox_c</c>, proc 0x126): item <c>home.angle.z &gt;&gt; 8</c>, tbox bit <c>prm &gt;&gt; 7 &amp; 0x1F</c>
/// (d_a_tbox.h:35-37). Function types 7/8 save to STAGE_SEA2 instead (d_a_tbox.cpp:854-858) and are skipped.</item>
/// <item>Placed items (<c>daItem_c</c>, proc 0x101): item <c>prm &amp; 0xFF</c>, item bit <c>prm &gt;&gt; 8 &amp; 0xFF</c>
/// (d_a_item.h:141-142); bits below 0x20 are the stage's memBit mItem (dSv_info_c::onItem, d_save.cpp:1629).</item>
/// <item>Key doors: <c>daDoor10_c</c> (proc 0x12E) type 4/5, <c>daDoor12_c</c> (proc 0x12F) type 1: chkMakeKey() == 1
/// (d_a_door10.cpp:13-25, d_a_door12.cpp:39-49). Type = <c>prm &gt;&gt; 8 &amp; 0xF</c>, switch = <c>prm &amp; 0xFF</c>
/// (d_door.cpp:16-28); keyInit sets that memory switch when the key is used (d_door.cpp:434-448).</item>
/// </list>
/// Nothing else in the vanilla dungeons gives a small key (every other actor was checked for item 0x15
/// in its parameters); each dungeon has exactly as many keys as key doors (docs/small-keys.md).
/// </summary>
public static class SmallKeyTableBuilder
{
    /// <summary>dItemNo_SMALL_KEY_e (d_item_data.h).</summary>
    public const int SmallKeyItem = 0x15;

    // Proc names (f_pc_name.h; the same values the switch table reads from each REL's profile).
    public const short ProcItem = 0x101, ProcTbox = 0x126, ProcDoor10 = 0x12E, ProcDoor12 = 0x12F;

    /// <summary>daTbox_c function types that save to STAGE_SEA2 (FUNC_TYPE_EXTRA_SAVE_INFO[_SPAWN], d_a_tbox.cpp:30-31).</summary>
    private const int TboxExtraSaveInfo = 7, TboxExtraSaveInfoSpawn = 8;

    /// <summary>dSv_memBit_c::mItem holds 32 bits (d_save.h:687); higher memory item bits alias mVisitedRoom.</summary>
    private const int MemoryItemBits = 32;

    /// <summary>l_objectName (d_stage.cpp:437, symbols.txt .data 0x80372818 size 0x26AC): dStage_objectNameInf
    /// {char name[8]; s16 procname; s8 argument; s8 gbaName;}, 0xC bytes each.</summary>
    public const uint ObjectNameTableAddress = 0x80372818;
    public const int ObjectNameTableSize = 0x26AC;
    public const int ObjectNameEntrySize = 0xC;

    /// <summary>Object name → process name, from main.dol's l_objectName.</summary>
    public static Dictionary<string, short> ReadObjectNames(DolPatcher dol)
    {
        var names = new Dictionary<string, short>(StringComparer.Ordinal);
        for (int i = 0; i < ObjectNameTableSize / ObjectNameEntrySize; i++)
        {
            uint entry = ObjectNameTableAddress + (uint)(i * ObjectNameEntrySize);
            var bytes = new byte[ObjectNameEntrySize];
            for (int w = 0; w < ObjectNameEntrySize; w += 4)
                BigEndianIO.WriteU32(bytes, w, dol.ReadU32(entry + (uint)w));
            int len = 0;
            while (len < 8 && bytes[len] != 0) len++;
            names.TryAdd(Encoding.ASCII.GetString(bytes, 0, len), BigEndianIO.ReadS16(bytes, 8));
        }
        return names;
    }

    /// <summary>
    /// The table for an extracted game folder (sys/main.dol + files/res/Stage), or null when it has none.
    /// Stages that can't be read are reported to <paramref name="skipped"/> and left out.
    /// </summary>
    public static SmallKeyTable? BuildFromGame(string gamePath, Action<string>? skipped = null)
    {
        var dolPath = Path.Combine(gamePath, "sys", "main.dol");
        var stages = StageDataReader.StagesPath(gamePath);
        if (stages == null || !File.Exists(dolPath))
            return null;
        var procs = ReadObjectNames(new DolPatcher(dolPath));
        return Build(StageDataReader.ReadAll(stages, skipped ?? (_ => { })).ToList(), procs);
    }

    /// <summary>The table for these stages, with <paramref name="procs"/> mapping object names to processes.</summary>
    public static SmallKeyTable Build(IReadOnlyCollection<StageData> stages, IReadOnlyDictionary<string, short> procs)
    {
        var sources = new Dictionary<int, Dictionary<(SmallKeySourceKind, int), SmallKeySource>>();
        var doors = new Dictionary<int, Dictionary<int, SmallKeyDoor>>();
        var untracked = new Dictionary<int, List<string>>();

        foreach (var stage in stages)
        {
            int slot = stage.SaveTbl;
            foreach (var a in stage.Actors)
            {
                if (!procs.TryGetValue(a.Name, out short proc)) continue;
                switch (Classify(proc, a))
                {
                    case (SmallKeySource source, null, null):
                        Add(sources, slot, (source.Kind, source.Bit), source);
                        break;
                    case (null, SmallKeyDoor door, null):
                        Add(doors, slot, door.Switch, door);
                        break;
                    case (null, null, string what):
                        if (!untracked.TryGetValue(slot, out var list)) untracked[slot] = list = [];
                        list.Add(what);
                        break;
                }
            }
        }

        var dungeons = new Dictionary<int, SmallKeyDungeon>();
        foreach (int slot in sources.Keys.Concat(doors.Keys).Concat(untracked.Keys).Distinct().Order())
        {
            dungeons[slot] = new SmallKeyDungeon
            {
                Slot = slot,
                Sources = sources.GetValueOrDefault(slot)?.Values.OrderBy(s => s.Kind).ThenBy(s => s.Bit).ToList() ?? [],
                Doors = doors.GetValueOrDefault(slot)?.Values.OrderBy(d => d.Switch).ToList() ?? [],
                Untracked = untracked.GetValueOrDefault(slot)?.Distinct().ToList() ?? [],
            };
        }
        return new SmallKeyTable { StageCount = stages.Count, Dungeons = dungeons };
    }

    /// <summary>
    /// What a placed actor is to the small keys: a key source with its flag, a key door with its switch, a
    /// key source whose flag the save can't show (its description), or nothing.
    /// </summary>
    public static (SmallKeySource? Source, SmallKeyDoor? Door, string? Untracked) Classify(short proc, PlacedActor a)
    {
        uint p = a.Params;
        switch (proc)
        {
            case ProcTbox:
            {
                if (((a.AngleZ >> 8) & 0xFF) != SmallKeyItem) break;
                int func = (int)(p & 0x7F);
                if (func is TboxExtraSaveInfo or TboxExtraSaveInfoSpawn)
                    return (null, null, $"{a.Stage} room {a.Room} chest {a.Name} (saves to STAGE_SEA2)");
                return (new SmallKeySource(SmallKeySourceKind.Chest, (int)(p >> 7) & 0x1F, a.Stage, a.Room), null, null);
            }
            case ProcItem:
            {
                if ((p & 0xFF) != SmallKeyItem) break;
                int bit = (int)(p >> 8) & 0xFF;
                if (bit < MemoryItemBits)
                    return (new SmallKeySource(SmallKeySourceKind.Item, bit, a.Stage, a.Room), null, null);
                return (null, null, $"{a.Stage} room {a.Room} item {a.Name} (item bit 0x{bit:X2})");
            }
            case ProcDoor10:
            case ProcDoor12:
            {
                int type = (int)(p >> 8) & 0xF;
                bool keyDoor = proc == ProcDoor10 ? type is 4 or 5 : type == 1;
                int sw = (int)(p & 0xFF);
                if (!keyDoor) break;
                // keyInit saves only a memory switch (< 0x80); a key door on another one never stays open.
                if (sw >= 0x80)
                    return (null, null, $"{a.Stage} key door {a.Name} (switch 0x{sw:X2} is not a memory switch)");
                return (null, new SmallKeyDoor(sw, a.Stage, a.Room), null);
            }
        }
        return (null, null, null);
    }

    private static void Add<TKey, TValue>(Dictionary<int, Dictionary<TKey, TValue>> map, int slot, TKey key, TValue value)
        where TKey : notnull
    {
        if (!map.TryGetValue(slot, out var inner)) map[slot] = inner = new Dictionary<TKey, TValue>();
        inner.TryAdd(key, value);
    }
}
