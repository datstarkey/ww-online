using System.Text;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Dol;

namespace WWOnline.Patcher.WorldData;

/// <summary>How an actor writes a switch.</summary>
public enum SwitchUse
{
    /// <summary>Only ever turns it on, for good (a crystal without a timer, a floor switch that stays down,
    /// "all enemies dead", a broken wall...): safe to sync on-edges.</summary>
    Latching,
    /// <summary>Turns it off again, or toggles it (timers, timed torches and crystals, pressure plates, area
    /// switches, continuous AND switches), or it means "this player is here" (one-shot area switches):
    /// syncing its on-edge would latch it on for the receiver, or fire it for a player elsewhere.</summary>
    Unsafe,
    /// <summary>Encodes a position in it (a push block's path index): must never be OR-merged.</summary>
    PathEncoded,
}

/// <summary>An actor's write to a switch.</summary>
/// <param name="Room">The room whose zone a zone switch (0xC0+) belongs to, or -1 when unknown.</param>
public readonly record struct SwitchSetter(int Switch, int Room, SwitchUse Use);

/// <summary>
/// Builds the <see cref="SwitchTable"/> from the player's own game: every actor placed in every stage
/// (<see cref="StageDataReader"/>), the object-name → process table in main.dol, and what each actor
/// kind does to its switch (<see cref="SettersOf"/>, from the tww-decomp sources, proc names read from
/// each actor REL's profile). A dan or zone switch may sync when at least one latching setter uses it
/// and no unsafe one does; a switch nobody is known to set is left alone. Everything is per save slot
/// (memory, dan) or per stage room (zone), over all layers.
/// </summary>
public static class SwitchTableBuilder
{
    /// <summary>
    /// BUMP whenever <see cref="SettersOf"/> or <see cref="Build"/> classify differently: it is part of the
    /// build stamp's hash (BuildStamp), so games patched with older rules read as stale and get patched again.
    /// </summary>
    public const int RulesVersion = 3; // 3: boulders (Stone2), light walls (MkieK), normal warp jars

    /// <summary>l_objectName (d_stage.cpp:437, symbols.txt .data 0x80372818 size 0x26AC): dStage_objectNameInf
    /// {char name[8]; s16 procname; s8 argument; s8 gbaName;}, 0xC bytes each.</summary>
    public const uint ObjectNameTableAddress = 0x80372818;
    public const int ObjectNameTableSize = 0x26AC;
    public const int ObjectNameEntrySize = 0xC;

    // Proc names, each read from its REL's g_profile (+0x08) in the vanilla RELS.arc / files/rels.
    public const short ProcAlldie = 0x016, ProcSwpush = 0x01D, ProcSwheavy = 0x01E, ProcMovebox = 0x02D,
        ProcWarpt = 0x043, ProcMjDoor = 0x047, ProcMkiek = 0x04E, ProcFloor = 0x062, ProcEp = 0x0BA, ProcTbox = 0x126, ProcSwc00 = 0x12C,
        ProcDoor10 = 0x12E, ProcDoor12 = 0x12F, ProcAndsw0 = 0x135, ProcAndsw2 = 0x136, ProcSaku = 0x191,
        ProcWall = 0x1B1, ProcSwhit0 = 0x1C9, ProcStone2 = 0x1CD, ProcIce = 0x1D2, ProcTimer = 0x1E1;

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
    /// What an actor of process <paramref name="proc"/> does to switches (0xFF = none is left out). Every
    /// field below is the actor's own getter in tww-decomp: (prm >> shift) &amp; mask, or home.angle.
    /// </summary>
    public static IEnumerable<SwitchSetter> SettersOf(short proc, PlacedActor a)
    {
        uint p = a.Params;
        int room = a.Room;
        switch (proc)
        {
            case ProcTbox:
            {
                // Chests keep their room in angle.x (d_a_tbox.cpp:1241). OpenInit_com sets angle.z & 0xFF
                // (:861-864); a func-2 chest sets its swNo when it appears (:1044).
                int chestRoom = a.AngleX & 0x3F;
                yield return new(a.AngleZ & 0xFF, chestRoom, SwitchUse.Latching);
                if ((p & 0x7F) == 2)
                    yield return new((int)(p >> 12) & 0xFF, chestRoom, SwitchUse.Latching);
                break;
            }
            // Broken for good: d_a_wall.cpp:303, d_a_floor.cpp:86, d_a_obj_ice.cpp:388 (param_on_swSave),
            // d_a_obj_majyuu_door.cpp:219, d_a_saku.cpp:371-392 (two halves).
            // Light-dissolved wall MkieK (d_a_obj_mkiek.cpp:174, sw = prm & 0xFF) too.
            case ProcWall:
            case ProcFloor:
            case ProcIce:
            case ProcMjDoor:
            case ProcMkiek:
                yield return new((int)p & 0xFF, room, SwitchUse.Latching);
                break;
            // Black boulder / Ekao (daStone2, sw = (prm >> 8) & 0xFF): set when it breaks or is lifted
            // (d_a_stone2.cpp:273, :513). The ones on DRC's and ET's warp jars uncover them.
            case ProcStone2:
                yield return new((int)(p >> 8) & 0xFF, room, SwitchUse.Latching);
                break;
            case ProcWarpt:
                // Warp jar (d_a_obj_warpt.cpp:705-764): type = prm & 0xF. A normal jar (not 2-4) sets its lid switch
                // angle.x & 0xFF when the lid breaks (openHuta, :396), type 1 at create (:718); its partner reads
                // it live when you warp (normalWarp). The three-way dungeon jars (types 2-4) keep their lids in an
                // event register and only read angle.x & 0xFF (the switch of whatever covers them).
                if ((p & 0xF) is not (2 or 3 or 4))
                    yield return new(a.AngleX & 0xFF, room, SwitchUse.Latching);
                break;
            case ProcSaku:
                yield return new((int)(p >> 8) & 0xFF, room, SwitchUse.Latching);
                yield return new((int)(p >> 16) & 0xFF, room, SwitchUse.Latching);
                break;
            case ProcSwhit0:
            {
                // Crystal: sw = prm & 0xFF, timer = u8(prm >> 20) (0 or 0xFF = none); a timed one turns
                // its switch off again (actionOnTimer, d_a_swhit0.cpp:348-365).
                uint timer = (p >> 20) & 0xFF;
                yield return new((int)p & 0xFF, room, timer is 0 or 0xFF ? SwitchUse.Latching : SwitchUse.Unsafe);
                break;
            }
            case ProcEp:
            {
                // Torch: sw = prm >> 24, timer = (prm >> 8) & 0xFF (0xFF = none); a timed one goes out and
                // clears the switch (d_a_ep.cpp:222-227, 286-288, 650-652).
                uint timer = (p >> 8) & 0xFF;
                yield return new((int)(p >> 24), room, timer == 0xFF ? SwitchUse.Latching : SwitchUse.Unsafe);
                break;
            }
            case ProcSwpush:
            {
                // Floor switch: sw = (prm >> 8) & 0xFF, type = (prm >> 24) & 3 (d_a_obj_swpush.h:63-88). Types 0
                // and 3 stay pressed and obey the save; 1 is a pressure plate (clears on release), 2 is "on
                // is up" (pressing clears it) (M_attr, d_a_obj_swpush.cpp:17-92).
                int type = (int)(p >> 24) & 3;
                yield return new((int)(p >> 8) & 0xFF, room, type is 0 or 3 ? SwitchUse.Latching : SwitchUse.Unsafe);
                break;
            }
            case ProcSwheavy:
            {
                // Iron-boots switch: same fields; only type 3 obeys the save and stays pressed, 2 toggles,
                // 0/1 pop back up and clear it (M_attr, d_a_obj_swheavy.cpp:13-54, :364-392).
                int type = (int)(p >> 24) & 3;
                yield return new((int)(p >> 8) & 0xFF, room, type == 3 ? SwitchUse.Latching : SwitchUse.Unsafe);
                break;
            }
            case ProcAlldie:
                // "Every enemy in the room is dead": sw = prm >> 8, only ever set (d_a_alldie.cpp:19, :45).
                yield return new((int)(p >> 8) & 0xFF, room, SwitchUse.Latching);
                break;
            case ProcDoor10:
            case ProcDoor12:
                // Kill-all bars (door type 2, d_door.cpp getType) set swbit once the front room is clear
                // (d_a_door10.cpp:139-148, d_a_door12.cpp:159-170). The front room is angle.x & 0x3F.
                if (((p >> 8) & 0xF) == 2)
                    yield return new((int)p & 0xFF, a.AngleX & 0x3F, SwitchUse.Latching);
                break;
            case ProcAndsw0:
            {
                // AND switch (d_a_andsw0.cpp:327-341): output = prm >> 24, inputs = count (prm & 0xFF) switches
                // from (prm >> 16) & 0xFF (0xFF: output + 1), behaviour = (prm >> 8) & 0xFF. 0 and 3 latch the
                // output; 2 latches it too but turns the INPUTS off when its timer runs out (ACT_TIMER2, :96-104);
                // anything else turns the output off again (ACT_OFF_ALL, :69-78). count 0xFF is a special case.
                int output = (int)(p >> 24), count = (int)p & 0xFF, behaviour = (int)(p >> 8) & 0xFF;
                if (count == 0xFF)
                {
                    yield return new(output, room, SwitchUse.Unsafe);
                    break;
                }
                yield return new(output, room, behaviour is 0 or 2 or 3 ? SwitchUse.Latching : SwitchUse.Unsafe);
                if (behaviour == 2)
                {
                    int first = (int)(p >> 16) & 0xFF;
                    if (first == 0xFF) first = output + 1;
                    for (int i = 0; i < count && first + i < 0xFF; i++)
                        yield return new(first + i, room, SwitchUse.Unsafe);
                }
                break;
            }
            case ProcAndsw2:
                // AND switch 2 (d_a_andsw2.cpp:20-55): output = (prm >> 16) & 0xFF; type (prm >> 8) & 0xFF 0 is one-off,
                // 1 (continuous) turns the output off again (actionOff, :152-158).
                yield return new((int)(p >> 16) & 0xFF, room, ((p >> 8) & 0xFF) == 0 ? SwitchUse.Latching : SwitchUse.Unsafe);
                break;
            case ProcTimer:
                yield return new((int)(p >> 16) & 0xFF, room, SwitchUse.Unsafe); // d_a_obj_timer.h:21-25, .cpp:72-73
                break;
            case ProcSwc00:
                // Area switch: sw1 = prm & 0xFF. Type 0 clears it when your Link leaves the area; the others fire
                // once when your Link enters (d_a_swc00.cpp:13-30), which is a player position, not world state
                // (it closes arena bars behind a player): never synced.
                yield return new((int)p & 0xFF, room, SwitchUse.Unsafe);
                break;
            case ProcMovebox:
            {
                // Push block (d_a_obj_movebox.h:120-200, .cpp:1021-1040, 1125-1210). Its path point is saved in
                // swSave1 = (prm >> 8) & 0xFF and swSave2 = angle.z >> 8 only when it follows a path: pathId =
                // angle.z & 0xFF != 0xFF and swSave1 != 0xFF. A "dmy" box (prm bit 30: appears on a switch) has no
                // pathId / swSave2 (prmZ_init zeroes them), and MkieBB (type 0xB) uses angle.z >> 8 for its own
                // switch. Otherwise swSave1 is just a switch the box reads.
                bool dmy = ((p >> 30) & 1) != 0;
                int type = (int)(p >> 24) & 0xF;
                int sw1 = (int)(p >> 8) & 0xFF, pathId = a.AngleZ & 0xFF, sw2 = (a.AngleZ >> 8) & 0xFF;
                if (dmy || type == MoveboxTypeMkieBB || pathId == 0xFF || sw1 == 0xFF)
                    break;
                yield return new(sw1, room, SwitchUse.PathEncoded);
                yield return new(sw2, room, SwitchUse.PathEncoded);
                break;
            }
        }
    }

    private const int MoveboxTypeMkieBB = 0xB; // TYPE_BLACK_BOX_WITH_MKIE

    /// <summary>An AND switch: it sets <paramref name="Output"/> once its inputs are all on.</summary>
    public readonly record struct AndSwitch(int Output, int Room, int[] Inputs);

    /// <summary>
    /// The AND switch an actor is, if any: its output follows its inputs, so an output fed by a switch that
    /// must not sync must not sync either (it would replay what the input did, e.g. a "player entered" area
    /// switch starting an ambush for a player elsewhere).
    /// </summary>
    public static AndSwitch? AndSwitchOf(short proc, PlacedActor a)
    {
        uint p = a.Params;
        int count = (int)p & 0xFF;
        if (count is 0 or 0xFF) return null;
        int output, first;
        if (proc == ProcAndsw0)
        {
            // d_a_andsw0.cpp:31-37, :327-341: inputs from (prm >> 16) & 0xFF, or output + 1 when that is 0 / 0xFF.
            output = (int)(p >> 24);
            first = (int)(p >> 16) & 0xFF;
            if (first is 0 or 0xFF) first = output + 1;
        }
        else if (proc == ProcAndsw2)
        {
            // getTopSw (d_a_andsw2.cpp:58-69): prm >> 24, else output + 1.
            output = (int)(p >> 16) & 0xFF;
            first = (int)(p >> 24);
            if (first == 0xFF) first = output == 0xFF ? 0xFF : output + 1;
        }
        else return null;
        if (output == 0xFF || first == 0xFF) return null;
        return new AndSwitch(output, a.Room, Enumerable.Range(first, count).Where(n => n < 0xF0).ToArray());
    }

    /// <summary>
    /// Where a switch lives: memory switches per save slot, dan switches per stage (the game keeps them per
    /// slot, but every stage of a slot is a separate visit: you always leave through another slot, the sea),
    /// zone switches per stage room (-1 = a Stage.arc actor whose room is unknown).
    /// </summary>
    private readonly record struct Scope(char Space, int Slot, string Stage, int Room);

    private static Scope ScopeOf(StageData stage, int sw, int room) =>
        sw < 0x80 ? new('M', stage.SaveTbl, "", 0) :
        sw < 0xC0 ? new('D', 0, stage.Name, 0) :
        new('Z', 0, stage.Name, room is >= 0 and < 64 ? room : -1);

    /// <summary>Build the table from parsed stages (the pure part: tests feed it synthetic stages).</summary>
    public static SwitchTable Build(IEnumerable<StageData> stages, IReadOnlyDictionary<string, short> procByName)
    {
        var latch = new HashSet<(Scope, int)>();
        var unsafeUse = new HashSet<(Scope, int)>();
        var memoryExcluded = new Dictionary<int, SortedSet<int>>();
        var ands = new List<((Scope, int) Output, (Scope, int)[] Inputs)>();
        int stageCount = 0;

        foreach (var stage in stages)
        {
            stageCount++;
            foreach (var actor in stage.Actors)
            {
                if (!procByName.TryGetValue(actor.Name, out short proc)) continue;
                foreach (var s in SettersOf(proc, actor))
                {
                    if (s.Switch is < 0 or >= 0xF0) continue; // 0xFF = none
                    var key = (ScopeOf(stage, s.Switch, s.Room), s.Switch);
                    if (s.Use == SwitchUse.PathEncoded && s.Switch < 0x80)
                        Get(memoryExcluded, stage.SaveTbl).Add(s.Switch);
                    (s.Use == SwitchUse.Latching ? latch : unsafeUse).Add(key);
                }
                if (AndSwitchOf(proc, actor) is { } and && and.Output < 0xF0)
                    ands.Add(((ScopeOf(stage, and.Output, and.Room), and.Output),
                        and.Inputs.Select(i => (ScopeOf(stage, i, and.Room), i)).ToArray()));
            }
        }

        // A zone switch with an unsafe setter in an unknown room is unsafe in every room of the stage.
        bool IsUnsafe((Scope Scope, int Switch) k) =>
            unsafeUse.Contains(k) || (k.Scope.Space == 'Z' && unsafeUse.Contains((k.Scope with { Room = -1 }, k.Switch)));

        // AND outputs inherit "unsafe" from any input, through chains of AND switches.
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (var (output, inputs) in ands)
                if (!IsUnsafe(output) && inputs.Any(IsUnsafe))
                    changed |= unsafeUse.Add(output);
        }

        var dan = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        var zone = new Dictionary<string, Dictionary<int, SortedSet<int>>>(StringComparer.Ordinal);
        foreach (var k in latch.Where(k => !IsUnsafe(k)))
        {
            if (k.Item1.Space == 'D')
                Get(dan, k.Item1.Stage).Add(k.Item2);
            else if (k.Item1.Space == 'Z' && k.Item1.Room >= 0)
            {
                if (!zone.TryGetValue(k.Item1.Stage, out var rooms)) zone[k.Item1.Stage] = rooms = new();
                Get(rooms, k.Item1.Room).Add(k.Item2);
            }
        }

        return new SwitchTable
        {
            Rules = RulesVersion,
            StageCount = stageCount,
            MemoryExcluded = memoryExcluded.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            Dan = dan.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray(), StringComparer.Ordinal),
            Zone = zone.ToDictionary(kv => kv.Key, kv => kv.Value.ToDictionary(r => r.Key, r => r.Value.ToArray()), StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// Build the table from an extracted game: its sys/main.dol and files/res/Stage. Null when that game has
    /// no stage folder (e.g. a vanilla folder with only the three files the patcher copies).
    /// </summary>
    public static SwitchTable? BuildFromGame(string gamePath)
    {
        var dolPath = Path.Combine(gamePath, "sys", "main.dol");
        var stagesPath = StageDataReader.StagesPath(gamePath);
        if (stagesPath == null || !File.Exists(dolPath))
            return null;
        return Build(StageDataReader.ReadAll(stagesPath, _ => { }), ReadObjectNames(new DolPatcher(dolPath)));
    }

    /// <summary>
    /// Build the table for a patch: the stage data from <paramref name="vanillaGamePath"/> (else the output
    /// game's, whose stage archives patches don't change) and the object names from the VANILLA main.dol,
    /// then write it into <paramref name="gamePath"/>. Returns a line for the patch log. Throws when there
    /// is no stage data: the patch fails rather than stamping a game without its table.
    /// </summary>
    public static string BuildAndWrite(string vanillaGamePath, string gamePath)
    {
        var dol = Path.Combine(vanillaGamePath, "sys", "main.dol");
        var stages = StageDataReader.StagesPath(vanillaGamePath) ?? StageDataReader.StagesPath(gamePath);
        File.Delete(Path.Combine(gamePath, SwitchTable.FileName));
        if (!File.Exists(dol))
            throw new FileNotFoundException($"Can't build the switch table: no vanilla main.dol at {dol}", dol);
        if (stages == null)
            throw new DirectoryNotFoundException(
                $"Can't build the switch table: neither {vanillaGamePath} nor {gamePath} has files/res/Stage (use a full extracted game).");
        var skipped = new List<string>();
        var table = Build(StageDataReader.ReadAll(stages, skipped.Add), ReadObjectNames(new DolPatcher(dol)));
        table.Write(gamePath);
        return (skipped.Count == 0 ? "" : $"Skipped unreadable stage data: {string.Join("; ", skipped)}. ") +
               $"Switch table: {table.StageCount} stages, {table.Dan.Sum(d => d.Value.Length)} dan and " +
               $"{table.Zone.Sum(z => z.Value.Sum(r => r.Value.Length))} room switch(es) syncable, " +
               $"{table.MemoryExcluded.Sum(m => m.Value.Length)} push-block switch(es) excluded.";
    }

    private static TSet Get<TKey, TSet>(Dictionary<TKey, TSet> d, TKey key) where TKey : notnull where TSet : new()
    {
        if (!d.TryGetValue(key, out var set)) d[key] = set = new TSet();
        return set;
    }
}
