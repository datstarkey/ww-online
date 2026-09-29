using System.Diagnostics;
using WWOnline.Patcher.WorldData;
using Xunit;
using Xunit.Abstractions;

namespace WWOnline.Patcher.Tests.WorldData;

/// <summary>
/// The small-key table: every key source (chest / placed item and its flag) and key door (its switch) per
/// dungeon save slot, and the derived key count. Synthetic stage data only (Nintendo's data is never in
/// the repo); the vanilla check is skipped without a configured vanilla game.
/// </summary>
public class SmallKeyTableTests(ITestOutputHelper output)
{
    private static readonly Dictionary<string, short> Procs = new()
    {
        ["takara2"] = SmallKeyTableBuilder.ProcTbox,
        ["item"] = SmallKeyTableBuilder.ProcItem,
        ["keyshut"] = SmallKeyTableBuilder.ProcDoor10,
        ["keyS12"] = SmallKeyTableBuilder.ProcDoor12,
        ["other"] = 0x0BE,
    };

    private const int Key = SmallKeyTableBuilder.SmallKeyItem;

    // Param builders, from each actor's getters (SmallKeyTableBuilder.Classify).
    private static PlacedActor Chest(int tboxNo, int item = Key, int func = 0, string stage = "Dun", int room = -1) =>
        new(stage, room, "takara2", 0xFF000000u | (uint)(tboxNo << 7) | (uint)func, 0, 0, (short)((item << 8) | 0xFF));
    private static PlacedActor Item(int bit, int item = Key, string stage = "Dun", int room = 1) =>
        new(stage, room, "item", 0x03000000u | (uint)(bit << 8) | (uint)item, 0, 0, 0);
    private static PlacedActor Door10(int sw, int type = 4, string stage = "Dun") =>
        new(stage, -1, "keyshut", 0x0FFFF000u | (uint)(type << 8) | (uint)sw, 0, 0, 0);
    private static PlacedActor Door12(int sw, int type = 1, string stage = "Dun") =>
        new(stage, -1, "keyS12", 0x0FFFF000u | (uint)(type << 8) | (uint)sw, 0, 0, 0);

    private static SmallKeyTable Table(int slot, params PlacedActor[] actors) =>
        SmallKeyTableBuilder.Build([new StageData("Dun", slot, actors)], Procs);

    [Fact]
    public void KeyChestsItemsAndDoors_AreFound()
    {
        var t = Table(3, Chest(0), Chest(3), Item(14), Door10(0x3C), Door10(0x3D, type: 5), Door12(0x0F));
        var d = t.For(3)!;
        Assert.Equal([(SmallKeySourceKind.Chest, 0), (SmallKeySourceKind.Chest, 3), (SmallKeySourceKind.Item, 14)],
            d.Sources.Select(s => (s.Kind, s.Bit)));
        Assert.Equal([0x0F, 0x3C, 0x3D], d.Doors.Select(x => x.Switch));
        Assert.Equal(0b1001u, d.TboxMask);
        Assert.Equal(1u << 14, d.ItemMask);
        Assert.Equal([1u << 0x0F, 1u << (0x3C - 32) | 1u << (0x3D - 32), 0u, 0u], d.SwitchMask);
    }

    [Fact]
    public void OtherChestsItemsDoorsAndActors_AreIgnored()
    {
        var t = Table(3,
            Chest(1, item: 0x16),           // map chest
            Item(2, item: 0x01),            // rupee
            Door10(0x20, type: 1),          // boss door (chkMakeKey 2): no key spent
            Door10(0x21, type: 0),          // switch bars
            Door12(0x22, type: 3),          // door12 boss door
            new PlacedActor("Dun", 0, "other", Key, 0, 0, Key << 8),
            new PlacedActor("Dun", 0, "unknownName", Key, 0, 0, Key << 8));
        Assert.Empty(t.Dungeons);
    }

    [Fact]
    public void SeaTwoChestsAndZoneItems_AreUntracked_NotCounted()
    {
        var t = Table(5, Chest(2, func: 7), Item(0x45), Door10(0x90));
        var d = t.For(5)!;
        Assert.Empty(d.Sources);
        Assert.Empty(d.Doors);
        Assert.Equal(3, d.Untracked.Count);
    }

    [Fact]
    public void StagesSharingASlot_AreMergedAndDeduplicated()
    {
        // Wind Temple's data is in both kaze and Cave08 (save slot 7).
        var a = new StageData("kaze", 7, [Chest(4, stage: "kaze"), Chest(9, stage: "kaze"), Door12(0x13, stage: "kaze"), Door12(0x1B, stage: "kaze")]);
        var b = new StageData("Cave08", 7, [Chest(4, stage: "Cave08"), Chest(9, stage: "Cave08"), Door12(0x13, stage: "Cave08"), Door12(0x1B, stage: "Cave08")]);
        var t = SmallKeyTableBuilder.Build([a, b], Procs);
        Assert.Equal(2, t.For(7)!.Sources.Count);
        Assert.Equal(2, t.For(7)!.Doors.Count);
        Assert.Equal(2, t.StageCount);
    }

    [Fact]
    public void Derive_IsKeysTakenMinusDoorsOpened()
    {
        var d = Table(3, Chest(0), Chest(3), Item(14), Door10(0x3C), Door10(0x36)).For(3)!;
        var noDoors = new uint[4];
        Assert.Equal(0, d.Derive(0, 0, noDoors));
        Assert.Equal(1, d.Derive(1u << 3, 0, noDoors));
        Assert.Equal(3, d.Derive(0b1001u | 0xF0u /* other chests */, 1u << 14, noDoors));
        var oneDoor = new uint[] { 0, 1u << (0x36 - 32), 0, 0 };
        Assert.Equal(2, d.Derive(0b1001u, 1u << 14, oneDoor));
        // Another save's door with no key of ours taken yet (two players, the same last key): negative.
        var bothDoors = new uint[] { 0xFFFF, 1u << (0x36 - 32) | 1u << (0x3C - 32), 0, 0 };
        Assert.Equal(-1, d.Derive(1u, 0, bothDoors));
    }

    [Fact]
    public void Summary_ListsEveryDungeon()
    {
        var t = Table(4, Chest(5), Door10(5));
        Assert.Equal("slot 4: 1 key(s) / 1 door(s)", t.Summary());
    }

    /// <summary>The vanilla dungeons have exactly as many small keys as key doors (docs/small-keys.md).</summary>
    [VanillaGameFact]
    public void VanillaGame_EveryDungeonHasAsManyKeysAsKeyDoors()
    {
        var watch = Stopwatch.StartNew();
        var skipped = new List<string>();
        var table = SmallKeyTableBuilder.BuildFromGame(TestRepo.VanillaGamePath()!, skipped.Add);
        output.WriteLine($"built in {watch.ElapsedMilliseconds} ms from {table?.StageCount} stages; {table?.Summary()}");
        Assert.NotNull(table);
        Assert.Empty(skipped);
        Assert.True(table!.StageCount > 100, $"only {table.StageCount} stages read");

        // Slot → (keys, doors): DRC, Forbidden Woods, Tower of the Gods, Earth Temple, Wind Temple.
        var expected = new Dictionary<int, int> { [3] = 4, [4] = 1, [5] = 2, [6] = 3, [7] = 2 };
        // Only key dungeons have key doors. STAGE_TEST (slot 15) holds a test map's key: never a dungeon.
        Assert.Equal(expected.Keys.Order(), table.Dungeons.Values.Where(d => d.Doors.Count > 0).Select(d => d.Slot).Order());
        foreach (var d in table.Dungeons.Values.Where(d => !expected.ContainsKey(d.Slot)))
            output.WriteLine($"not a dungeon: slot {d.Slot}: {string.Join(", ", d.Sources)} {string.Join(", ", d.Untracked)}");
        foreach (var (slot, n) in expected)
        {
            var d = table.For(slot)!;
            foreach (var s in d.Sources) output.WriteLine($"slot {slot}: {s}");
            foreach (var door in d.Doors) output.WriteLine($"slot {slot}: {door}");
            Assert.Equal(n, d.Sources.Count);
            Assert.Equal(n, d.Doors.Count);
            Assert.Empty(d.Untracked);
        }
    }
}
