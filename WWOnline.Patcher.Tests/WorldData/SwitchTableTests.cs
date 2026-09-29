using System.Text;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Yaz0;
using WWOnline.Patcher.WorldData;
using Xunit;

namespace WWOnline.Patcher.Tests.WorldData;

/// <summary>
/// The patch-time switch table: which dungeon / room switches may sync, which memory switches must not
/// merge. Synthetic stage data only (Nintendo's data is never in the repo); the one test on real files
/// is skipped without a configured vanilla game.
/// </summary>
public class SwitchTableTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-switch-table-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    // Object names of the synthetic stages -> proc (what main.dol's l_objectName would give).
    private static readonly Dictionary<string, short> Procs = new()
    {
        ["crystal"] = SwitchTableBuilder.ProcSwhit0,
        ["torch"] = SwitchTableBuilder.ProcEp,
        ["alldie"] = SwitchTableBuilder.ProcAlldie,
        ["floorsw"] = SwitchTableBuilder.ProcSwpush,
        ["ironsw"] = SwitchTableBuilder.ProcSwheavy,
        ["area"] = SwitchTableBuilder.ProcSwc00,
        ["timer"] = SwitchTableBuilder.ProcTimer,
        ["box"] = SwitchTableBuilder.ProcMovebox,
        ["chest"] = SwitchTableBuilder.ProcTbox,
        ["and0"] = SwitchTableBuilder.ProcAndsw0,
        ["and2"] = SwitchTableBuilder.ProcAndsw2,
        ["bars"] = SwitchTableBuilder.ProcDoor10,
        ["wall"] = SwitchTableBuilder.ProcWall,
        ["boulder"] = SwitchTableBuilder.ProcStone2,
        ["lightwall"] = SwitchTableBuilder.ProcMkiek,
        ["jar"] = SwitchTableBuilder.ProcWarpt,
    };

    private static PlacedActor A(string name, uint prm, int room = 0, short ax = 0, short az = 0, string stage = "Dun") =>
        new(stage, room, name, prm, ax, 0, az);

    private static SwitchTable Table(params PlacedActor[] actors) => Table(3, actors);

    private static SwitchTable Table(int slot, params PlacedActor[] actors) =>
        SwitchTableBuilder.Build([new StageData("Dun", slot, actors)], Procs);

    // Param builders, from each actor's getters (SwitchTableBuilder.SettersOf).
    private static uint Crystal(int sw, int timer = 0xFF) => (uint)(timer << 20) | (uint)sw;
    private static uint Torch(int sw, int timer = 0xFF) => (uint)(sw << 24) | 0x00FF0000u | (uint)(timer << 8);
    private static uint FloorSwitch(int sw, int type) => (uint)(type << 24) | (uint)(sw << 8);

    [Fact]
    public void LatchingSetters_MakeASwitchSyncable()
    {
        var t = Table(A("crystal", Crystal(0xE0), room: 5), A("alldie", 0xFFFF00FF | (0xE1u << 8), room: 5),
            A("torch", Torch(0x90), room: 2), A("floorsw", FloorSwitch(0xC2, 0), room: 5), A("ironsw", FloorSwitch(0xC3, 3), room: 5));
        Assert.Equal([0xC2, 0xC3, 0xE0, 0xE1], t.Zone["Dun"][5]);
        Assert.Equal([0x90], t.Dan["Dun"]);
    }

    [Fact]
    public void AnyUnsafeSetter_ExcludesTheSwitch()
    {
        var t = Table(
            A("crystal", Crystal(0xE0), room: 1), A("crystal", Crystal(0xE0, timer: 3), room: 1),   // timed crystal
            A("torch", Torch(0xE1), room: 1), A("torch", Torch(0xE1, timer: 10), room: 1),          // timed torch
            A("floorsw", FloorSwitch(0xE2, 0), room: 1), A("floorsw", FloorSwitch(0xE2, 1), room: 1), // pressure plate
            A("crystal", Crystal(0xE3), room: 1), A("area", 0x0003FF00 | 0xE3u, room: 1),            // area switch
            A("crystal", Crystal(0xE4), room: 1), A("timer", 0x00E40000u | 30, room: 1),             // timer
            A("crystal", Crystal(0xE5), room: 1), A("ironsw", FloorSwitch(0xE5, 2), room: 1),        // iron toggle
            A("crystal", Crystal(0xE6), room: 1));
        Assert.Equal([0xE6], t.Zone["Dun"][1]);
    }

    [Fact]
    public void NoKnownSetter_NotSyncable()
    {
        var t = Table(A("somethingElse", 0x000000E0, room: 1), A("floorsw", FloorSwitch(0xE0, 2), room: 1));
        Assert.Empty(t.Zone);
        Assert.Empty(t.Dan);
    }

    [Fact]
    public void ZoneSwitches_ArePerRoom_DanSwitchesPerStage()
    {
        var t = SwitchTableBuilder.Build(
        [
            new StageData("Dun", 3, [A("crystal", Crystal(0xE0), room: 1), A("crystal", Crystal(0x81), room: 1)]),
            new StageData("DunBoss", 3, [A("crystal", Crystal(0xE0, timer: 5), room: 2, stage: "DunBoss"),
                A("crystal", Crystal(0x81, timer: 5), room: 0, stage: "DunBoss")]),
        ], Procs);
        Assert.Equal([0xE0], t.Zone["Dun"][1]);     // the timed 0xE0 is another stage's room
        Assert.False(t.Zone.ContainsKey("DunBoss"));
        Assert.Equal([0x81], t.Dan["Dun"]);           // the same slot's other stage is another visit
        Assert.False(t.Dan.ContainsKey("DunBoss"));  // (where it's timed)
    }

    [Fact]
    public void WarpJars_BouldersAndLightWalls_Latch()
    {
        // WarpD-style normal jars (type 0 lidded, type 1 open at create): lid switch angle.x & 0xFF, the partner's
        // in angle.x >> 8 is only read. A three-way dungeon jar (types 2-4) only reads angle.x & 0xFF (the switch of
        // the boulder / light wall on it). Boulder: (prm >> 8) & 0xFF; light wall: prm & 0xFF.
        var t = Table(
            A("jar", 0x00001040, room: 0, ax: unchecked((short)0x8184)),
            A("jar", 0x00002011, room: 0, ax: unchecked((short)0x8281)),
            A("jar", 0x02010003, room: 0, ax: unchecked((short)0xFF88)),
            A("jar", 0x03020143, room: 2, ax: unchecked((short)0xFFE1)),
            A("boulder", 0xF47FE23F, room: 2),
            A("lightwall", 0x000000E3, room: 2));
        Assert.Equal([0x81, 0x84], t.Dan["Dun"]);    // not 0x82 (a partner) nor 0x88 (read by a three-way jar)
        Assert.Equal([0xE2, 0xE3], t.Zone["Dun"][2]); // not 0xE1 (read by a three-way jar)
    }

    [Fact]
    public void Rel_ProcNames_MatchTheTable()
    {
        // puppet_liveworld.c re-creates these; both sides must name the same processes.
        var inl = File.ReadAllText(Path.Combine(TestRepo.GameMod, "include", "ww_inlines.h"));
        short Proc(string name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(inl, @"#define\s+PROC_NAME_" + name + @"\s+0x([0-9A-Fa-f]+)");
            Assert.True(m.Success, "PROC_NAME_" + name + " not in ww_inlines.h");
            return Convert.ToInt16(m.Groups[1].Value, 16);
        }
        Assert.Equal(SwitchTableBuilder.ProcTbox, Proc("TBOX"));
        Assert.Equal(SwitchTableBuilder.ProcWall, Proc("WALL"));
        Assert.Equal(SwitchTableBuilder.ProcFloor, Proc("FLOOR"));
        Assert.Equal(SwitchTableBuilder.ProcIce, Proc("OBJ_ICE"));
        Assert.Equal(SwitchTableBuilder.ProcMjDoor, Proc("MJDOOR"));
        Assert.Equal(SwitchTableBuilder.ProcSaku, Proc("SAKU"));
        Assert.Equal(SwitchTableBuilder.ProcSwhit0, Proc("SWHIT0"));
        Assert.Equal(SwitchTableBuilder.ProcDoor10, Proc("DOOR10"));
        Assert.Equal(SwitchTableBuilder.ProcDoor12, Proc("DOOR12"));
        Assert.Equal(SwitchTableBuilder.ProcStone2, Proc("STONE2"));
        Assert.Equal(SwitchTableBuilder.ProcMkiek, Proc("MKIEK"));
        Assert.Equal(SwitchTableBuilder.ProcWarpt, Proc("OBJ_WARPT"));
    }

    [Fact]
    public void StageLevelActors_UseTheirOwnRoom_OrExcludeEverywhereWhenUnknown()
    {
        var t = Table(
            // A chest in Stage.arc keeps its room in angle.x and sets angle.z & 0xFF when opened.
            A("chest", 0xFF000000, room: -1, ax: 4, az: 0xE7),
            // Kill-all bars (door type 2) set swbit for their front room (angle.x & 0x3F).
            A("bars", 0x0FFFF200 | 0xE8u, room: -1, ax: 4),
            // An unsafe setter in Stage.arc with no known room excludes the switch in every room.
            A("crystal", Crystal(0xE9), room: 4), A("timer", 0x00E90000u | 5, room: -1));
        Assert.Equal([0xE7, 0xE8], t.Zone["Dun"][4]);
    }

    [Fact]
    public void AndSwitches_LatchingOutputs_AndTimedInputs()
    {
        // and0 behaviour 0 latches its output; behaviour 2 too, but turns its inputs off on timeout.
        var t = Table(
            A("and0", 0xE0000000u | 0x00E10000u | (0u << 8) | 2, room: 1),
            A("and0", 0xE3000000u | 0x00E40000u | (2u << 8) | 2, room: 1),
            A("crystal", Crystal(0xE4), room: 1), A("crystal", Crystal(0xE5), room: 1),
            // and2 type 1 (continuous) turns its output off again.
            A("and2", 0x00E60000u | (1u << 8) | 2, room: 1),
            A("and2", 0x00E70000u | (0u << 8) | 2, room: 1));
        Assert.Equal([0xE0, 0xE7], t.Zone["Dun"][1]); // 0xE3 follows its timed (unsafe) inputs
    }

    [Fact]
    public void AndOutputs_InheritUnsafeInputs_ThroughChains()
    {
        // An area switch (player entered) sets 0xEB; one-off AND switches chain 0xEB -> 0xEC -> 0xED; a
        // crystal also sets 0xED. Nothing in the chain may sync. 0xE0 -> 0xE1 from a latching crystal may.
        var t = Table(
            A("area", 0x0003FF00 | 0xEBu, room: 1),
            A("and2", 0xEB000000u | 0x00EC0000u | 1, room: 1),
            A("and2", 0xEC000000u | 0x00ED0000u | 1, room: 1),
            A("crystal", Crystal(0xED), room: 1),
            A("crystal", Crystal(0xE0), room: 1),
            A("and2", 0xE0000000u | 0x00E10000u | 1, room: 1));
        Assert.Equal([0xE0, 0xE1], t.Zone["Dun"][1]);
    }

    [Fact]
    public void PushBlocks_ExcludeTheirMemorySwitches()
    {
        var t = Table(A("box", 0x82000000 | (0x23u << 8), room: 0, az: 0x2400), A("wall", 0x10, room: 0));
        Assert.Equal([0x23, 0x24], t.MemoryExcluded[3]);
        Assert.Equal([0u, 0x18u, 0u, 0u], t.MemoryExcludedWords(3));
        Assert.Equal([0u, 0u, 0u, 0u], t.MemoryExcludedWords(4));
    }

    [Fact]
    public void PushBlocks_WithoutAPath_ExcludeNothing()
    {
        var t = Table(
            A("box", 0x82000000 | (0x23u << 8), room: 0, az: 0x24FF),               // pathId 0xFF: no path
            A("box", 0xC2000000 | (0x25u << 8), room: 0, az: 0x2600),               // dmy (bit 30): appears on a switch
            A("box", 0x8B000000 | (0x27u << 8), room: 0, az: 0x2800),               // MkieBB: angle.z >> 8 is its own
            A("box", 0x820000FF | (0xFFu << 8), room: 0, az: 0x2A00));              // no swSave1
        Assert.False(t.MemoryExcluded.ContainsKey(3));
    }

    [Fact]
    public void Words_MapSwitchNumbersToTheGamesWordLayouts()
    {
        var t = new SwitchTable
        {
            Dan = new() { ["Dun"] = [0x80, 0xBF] },
            Zone = new() { ["Dun"] = new() { [2] = [0xC0, 0xDF, 0xE0, 0xEF] } },
        };
        Assert.Equal([1u, 0x80000000u], t.DanWords("Dun"));
        Assert.Equal([0u, 0u], t.DanWords("Other"));
        Assert.Equal([0x80000001u, 0x8001u], t.ZoneWords("Dun", 2));
        Assert.Equal([0u, 0u], t.ZoneWords("Dun", 3));
        Assert.Equal([0u, 0u], t.ZoneWords("Other", 2));
    }

    [Fact]
    public void WriteAndLoad_RoundTrip_AndRejectOtherVersions()
    {
        Directory.CreateDirectory(_dir);
        var t = Table(A("crystal", Crystal(0xE0), room: 5), A("box", 0x82000000 | (0x23u << 8), room: 0, az: 0x2400));
        t.Write(_dir);
        var loaded = SwitchTable.Load(_dir)!;
        Assert.Equal(t.ZoneWords("Dun", 5), loaded.ZoneWords("Dun", 5));
        Assert.Equal(t.MemoryExcludedWords(3), loaded.MemoryExcludedWords(3));

        File.WriteAllText(Path.Combine(_dir, SwitchTable.FileName), "{\"Version\":999}");
        Assert.Null(SwitchTable.Load(_dir));
        File.WriteAllText(Path.Combine(_dir, SwitchTable.FileName), "not json");
        Assert.Null(SwitchTable.Load(_dir));
        Assert.Null(SwitchTable.Load(Path.Combine(_dir, "missing")));
    }

    // ---- Stage data parsing (synthetic bytes) ----

    /// <summary>A .dzr with one chunk of <paramref name="tag"/> holding the given actors, plus a STAG chunk.</summary>
    private static byte[] Dz(string tag, int entrySize, int saveTbl, params (string Name, uint Prm, short Az)[] actors)
    {
        int chunks = 2, header = 4 + chunks * 12, stagSize = 0x10;
        var dz = new byte[header + stagSize + actors.Length * entrySize];
        BigEndianIO.WriteU32(dz, 0, (uint)chunks);
        Encoding.ASCII.GetBytes("STAG").CopyTo(dz, 4);
        BigEndianIO.WriteU32(dz, 8, 1);
        BigEndianIO.WriteU32(dz, 12, (uint)header);
        dz[header + 9] = (byte)(saveTbl << 1 | 1);
        Encoding.ASCII.GetBytes(tag).CopyTo(dz, 16);
        BigEndianIO.WriteU32(dz, 20, (uint)actors.Length);
        BigEndianIO.WriteU32(dz, 24, (uint)(header + stagSize));
        for (int i = 0; i < actors.Length; i++)
        {
            int e = header + stagSize + i * entrySize;
            Encoding.ASCII.GetBytes(actors[i].Name).CopyTo(dz, e);
            BigEndianIO.WriteU32(dz, e + 8, actors[i].Prm);
            dz[e + 0x1C] = (byte)(actors[i].Az >> 8);
            dz[e + 0x1D] = (byte)actors[i].Az;
        }
        return dz;
    }

    /// <summary>A minimal RARC: one node, a "." directory entry, one file.</summary>
    private static byte[] Rarc(string fileName, byte[] file)
    {
        const int entries = 0x40, entryCount = 2;
        var strings = Encoding.ASCII.GetBytes(".\0" + fileName + "\0");
        int stringsAt = entries + entryCount * 0x14;
        int dataAt = (stringsAt + strings.Length + 0x1F) & ~0x1F;
        var arc = new byte[dataAt + file.Length];
        Encoding.ASCII.GetBytes("RARC").CopyTo(arc, 0);
        BigEndianIO.WriteU32(arc, 0x08, 0x20);
        BigEndianIO.WriteU32(arc, 0x0C, (uint)(dataAt - 0x20));
        BigEndianIO.WriteU32(arc, 0x28, entryCount);
        BigEndianIO.WriteU32(arc, 0x2C, entries - 0x20);
        BigEndianIO.WriteU32(arc, 0x34, (uint)(stringsAt - 0x20));
        BigEndianIO.WriteU32(arc, entries + 4, 0x02000000);                        // "." (directory)
        BigEndianIO.WriteU32(arc, entries + 0x14 + 4, 0x01000000 | 2);             // the file, name at +2
        BigEndianIO.WriteU32(arc, entries + 0x14 + 8, 0);
        BigEndianIO.WriteU32(arc, entries + 0x14 + 0xC, (uint)file.Length);
        strings.CopyTo(arc, stringsAt);
        file.CopyTo(arc, dataAt);
        return arc;
    }

    [Fact]
    public void ParseActors_ReadsNameParamsAndAngles_ForEveryActorChunk()
    {
        var dz = Dz("SCOB", 0x24, 7, ("crystal", 0x123, 0x2400), ("torch", 0xE0FFFFFF, 0));
        var actors = StageDataReader.ParseActors(dz, "Dun", 4).ToList();
        Assert.Equal(2, actors.Count);
        Assert.Equal(new PlacedActor("Dun", 4, "crystal", 0x123, 0, 0, 0x2400), actors[0]);
        Assert.Equal(7, StageDataReader.ParseSaveTbl(dz));
        Assert.Empty(StageDataReader.ParseActors(Dz("RTBL", 0x20, 0, ("x", 1, 0)), "Dun", 0)); // not an actor chunk
    }

    [Fact]
    public void ReadDzFiles_FindsTheDzr_InAPlainOrYaz0Archive()
    {
        var dz = Dz("ACTR", 0x20, 2, ("alldie", 0xFFFFE0FF, 0));
        var arc = Rarc("room.dzr", dz);
        foreach (var bytes in new[] { arc, new Yaz0Codec().Compress(arc) })
        {
            var files = StageDataReader.ReadDzFiles(bytes);
            var (name, data) = Assert.Single(files);
            Assert.Equal("room.dzr", name);
            Assert.Equal(dz, data);
        }
        Assert.Empty(StageDataReader.ReadDzFiles(Rarc("model.bdl", [1, 2, 3])));
        Assert.Empty(StageDataReader.ReadDzFiles([1, 2, 3, 4]));
    }

    [Fact]
    public void ReadStage_ReadsStageAndRoomArchives()
    {
        var stage = Path.Combine(_dir, "Dun");
        Directory.CreateDirectory(stage);
        File.WriteAllBytes(Path.Combine(stage, "Stage.arc"), Rarc("stage.dzs", Dz("TRES", 0x20, 5, ("chest", 0xFF000000, 0xE7))));
        File.WriteAllBytes(Path.Combine(stage, "Room3.arc"), Rarc("room.dzr", Dz("ACTR", 0x20, 0, ("alldie", 0xFFFFE0FF, 0))));
        File.WriteAllBytes(Path.Combine(stage, "Room99.arc"), Rarc("room.dzr", Dz("ACTR", 0x20, 0, ("alldie", 0xFFFFE1FF, 0))));

        var data = StageDataReader.ReadStage(stage)!;
        Assert.Equal(5, data.SaveTbl);
        Assert.Equal([(-1, "chest"), (3, "alldie")], data.Actors.Select(a => (a.Room, a.Name)).OrderBy(x => x.Room));
    }

    [Fact]
    public void ReadAll_SkipsAStageItCantRead()
    {
        var good = Path.Combine(_dir, "Good");
        var bad = Path.Combine(_dir, "Bad");
        Directory.CreateDirectory(good);
        Directory.CreateDirectory(bad);
        File.WriteAllBytes(Path.Combine(good, "Stage.arc"), Rarc("stage.dzs", Dz("TRES", 0x20, 5, ("chest", 0xFF000000, 0xE7))));
        // A truncated Yaz0 stream that claims a large size.
        var broken = new Yaz0Codec().Compress(Rarc("stage.dzs", Dz("TRES", 0x20, 5, ("chest", 0xFF000000, 0xE7))));
        File.WriteAllBytes(Path.Combine(bad, "Stage.arc"), broken[..40]);

        var skipped = new List<string>();
        var stages = StageDataReader.ReadAll(_dir, skipped.Add).ToList();
        Assert.Equal(["Good"], stages.Select(s => s.Name));
        Assert.Single(skipped);
        Assert.StartsWith("Bad", skipped[0]);
    }

    [Fact]
    public void ReadDzFiles_GarbageOffsets_ReadNothing()
    {
        var arc = Rarc("room.dzr", Dz("ACTR", 0x20, 2, ("alldie", 0xFFFFE0FF, 0)));
        var hugeOffset = (byte[])arc.Clone();
        BigEndianIO.WriteU32(hugeOffset, 0x40 + 0x14 + 8, 0xFFFFFF00);
        Assert.Empty(StageDataReader.ReadDzFiles(hugeOffset));
        var hugeHeader = (byte[])arc.Clone();
        BigEndianIO.WriteU32(hugeHeader, 0x0C, 0xFFFFFFF0);
        Assert.Empty(StageDataReader.ReadDzFiles(hugeHeader));
        var dz = Dz("ACTR", 0x20, 2, ("alldie", 0xFFFFE0FF, 0));
        BigEndianIO.WriteU32(dz, 24, 0x7FFFFFF0); // actor chunk offset past the file
        Assert.Empty(StageDataReader.ParseActors(dz, "Dun", 0));
    }

    [Fact]
    public void PartialYaz0Decompress_StopsAtTheLimit()
    {
        var data = Enumerable.Range(0, 5000).Select(i => (byte)(i % 7 == 0 ? i : 3)).ToArray();
        var comp = new Yaz0Codec().Compress(data);
        Assert.Equal(data, Yaz0Codec.Decompress(comp));
        Assert.Equal(data[..1234], Yaz0Codec.Decompress(comp, 1234));
        Assert.Equal(data, Yaz0Codec.Decompress(comp, 99999));
    }

    [VanillaGameFact]
    public void VanillaGame_BuildsATable()
    {
        var table = SwitchTableBuilder.BuildFromGame(TestRepo.VanillaGamePath()!);
        Assert.NotNull(table);
        Assert.True(table!.StageCount > 100, $"only {table.StageCount} stages read");
        Assert.NotEmpty(table.Dan);
        Assert.NotEmpty(table.Zone);
        Assert.NotEmpty(table.MemoryExcluded);
        Assert.Equal(SwitchTableBuilder.RulesVersion, table.Rules);
    }
}
