using System.Diagnostics;
using WWOnline.Patcher.WorldData;
using Xunit;
using Xunit.Abstractions;

namespace WWOnline.Patcher.Tests.WorldData;

/// <summary>
/// The heart table: every Piece of Heart and Heart Container with the flag that records it (chests, placed and
/// dug-up items, salvage points and boss containers from the stage files, plus the catalogue's NPC rewards) and the
/// max health it derives. Synthetic stage data only (Nintendo's data is never in the repo); the vanilla check is
/// skipped without a configured vanilla game.
/// </summary>
public class HeartTableTests(ITestOutputHelper output)
{
    private static readonly Dictionary<string, short> Procs = new()
    {
        ["takara3"] = HeartTableBuilder.ProcTbox,
        ["item"] = HeartTableBuilder.ProcItem,
        ["TagKb"] = HeartTableBuilder.ProcTagKbItem,
        ["Salvage"] = HeartTableBuilder.ProcSalvage,
        ["Bitem"] = HeartTableBuilder.ProcBossItem,
        ["other"] = 0x0BE,
    };

    private const int Piece = HeartTableBuilder.HeartPiece, Container = HeartTableBuilder.HeartContainer;

    // Param builders, from each actor's getters (HeartTableBuilder.Classify).
    private static PlacedActor Chest(int tbox, int item = Piece, int func = 0, string stage = "sea", int room = 11) =>
        new(stage, room, "takara3", 0xFF200000u | (uint)(tbox << 7) | (uint)func, 0, 0, (short)((item << 8) | 0xFF));
    private static PlacedActor Item(int bit, int item = Piece, string name = "item", string stage = "sea", int room = 45) =>
        new(stage, room, name, 0x01FF0000u | (uint)(bit << 8) | (uint)item, 0, 0, 0xFF);
    private static PlacedActor Salvage(int kind, int save, int item = Piece, int room = 5, int copy = 0) =>
        new("sea", room, "Salvage", (uint)(kind << 28) | (uint)(save << 20) | 0xFF000u | (uint)(item << 4) | 1, 0, 0, (short)copy);
    private static PlacedActor Boss(int slot, string stage = "M_DragB") => new(stage, 0, "Bitem", (uint)slot, 0, 0, 0);

    private static HeartTable Table(int slot, params PlacedActor[] actors) =>
        HeartTableBuilder.Build([new StageData(actors.FirstOrDefault().Stage ?? "sea", slot, actors)], Procs, rewards: []);

    [Fact]
    public void ChestsItemsSalvageAndBosses_AreFoundWithTheirFlags()
    {
        var t = Table(0,
            Chest(10), Chest(3, func: 7), Chest(4, item: 0x3F),
            Item(8), Item(2, name: "TagKb"),
            Salvage(0, 9), Salvage(2, 0, room: 17), Salvage(3, 2, item: Container, room: 6),
            Boss(3));
        Assert.Equal(
            [HeartFlag.Chest(0, 4), HeartFlag.Chest(0, 10), HeartFlag.Chest(1, 3), HeartFlag.Item(0, 2), HeartFlag.Item(0, 8),
             HeartFlag.StageLife(3), HeartFlag.Chart(9), HeartFlag.Ocean(6, 2), HeartFlag.Ocean(17, 0)],
            t.Sources.Select(s => s.Flag).OrderBy(f => f.Kind).ThenBy(f => f.A).ThenBy(f => f.B));
        Assert.Equal(7, t.PiecesTotal);
        Assert.Equal(2, t.ContainersTotal); // the salvaged container and the boss
        Assert.Empty(t.Untracked);
    }

    [Fact]
    public void OtherItemsActorsAndDuplicates_AreIgnored()
    {
        var t = Table(0,
            Chest(1, item: 0x16), Item(2, item: 0x01), Salvage(0, 4, item: 0x04),
            new PlacedActor("sea", 0, "other", Piece, 0, 0, Piece << 8),
            new PlacedActor("sea", 0, "unknownName", Piece, 0, 0, Piece << 8),
            // One source on three layers / four salvage copies: counted once.
            Chest(10), Chest(10), Chest(10), Salvage(0, 9, copy: 0), Salvage(0, 9, copy: 1), Salvage(0, 9, copy: 2));
        Assert.Equal([HeartFlag.Chest(0, 10), HeartFlag.Chart(9)], t.Sources.Select(s => s.Flag));
    }

    [Fact]
    public void HeartsTheSaveCantRecord_AreUntracked_NotCounted()
    {
        var t = Table(0, Item(0x45), Item(0x1F, name: "TagKb"), Salvage(6, 1), Salvage(2, 31, room: 16));
        Assert.Empty(t.Sources);
        Assert.Equal(4, t.Untracked.Count);
    }

    [Fact]
    public void AHeartFlagAnotherChestAlsoSets_IsReported()
    {
        var t = Table(0, Chest(10), Chest(10, item: 0x16, room: 30));
        Assert.Single(t.Sources);
        Assert.Contains(t.Untracked, u => u.Contains("slot 0 tbox 10") && u.Contains("room 30"));
    }

    [Fact]
    public void TestAndUnreachableStages_AreLeftOut()
    {
        var t = HeartTableBuilder.Build(
        [
            new StageData("I_TestM", HeartCatalog.TestSlot, [Chest(0, stage: "I_TestM")]),
            new StageData("Cave06", 12, [Chest(18, stage: "Cave06")]),
            new StageData("I_SubAN", 12, [Chest(7, stage: "I_SubAN")]),
            new StageData("Cave01", 12, [Chest(5, stage: "Cave01")]),
        ], Procs, rewards: []);
        Assert.Equal([HeartFlag.Chest(12, 5)], t.Sources.Select(s => s.Flag));
    }

    [Fact]
    public void Tally_CountsTakenSourcesAndDerivesMaxLife()
    {
        var t = Table(0, Chest(10), Item(8), Salvage(0, 9), Boss(3), Boss(2, "M2tower"));
        var flags = new HeartFlagState();
        Assert.Equal(new HeartTally(0, 0), t.Tally(flags));
        Assert.Equal(12, t.Tally(flags).MaxLife);

        flags.Tbox[0] = 1u << 10 | 1u << 11; // another chest's bit doesn't count
        flags.Set(HeartFlag.Chart(9));
        flags.DungeonItem[3] = 0b1000;       // Gohma beaten but the container not taken: not counted
        Assert.Equal(new HeartTally(0, 2), t.Tally(flags));
        flags.Set(HeartFlag.StageLife(3));
        Assert.Equal(new HeartTally(1, 2), t.Tally(flags));
        Assert.Equal(12 + 4 + 2, t.Tally(flags).MaxLife);
    }

    [Fact]
    public void RoomSourceSet_CountsEachSourceOnce_WithOrWithoutTheLocalFlag()
    {
        var t = Table(0, Chest(10), Item(8), Boss(3));
        var flags = new HeartFlagState();
        flags.Set(HeartFlag.Chest(0, 10));
        ulong chest = 1UL << HeartCatalog.IdOf(HeartFlag.Chest(0, 10))!.Value;
        ulong gohma = 1UL << HeartCatalog.IdOf(HeartFlag.StageLife(3))!.Value;
        Assert.Equal(chest, t.SourceBits(flags));
        Assert.Equal(new HeartTally(0, 1), t.Tally(flags, chest));          // the same source locally and in the room: once
        Assert.Equal(new HeartTally(1, 1), t.Tally(flags, chest | gohma));  // a container only the room has
        Assert.Equal(new HeartTally(1, 0), t.Tally(new HeartFlagState(), gohma));
    }

    [Fact]
    public void FlagState_ReadsEveryKind()
    {
        var s = new HeartFlagState();
        s.Events[0x13] = 0x40;
        Assert.True(s.IsSet(HeartFlag.Event(0x1340)));
        Assert.False(s.IsSet(HeartFlag.Event(0x1320)));

        // Registers: getEventReg = byte & mask; the letter counts only once read (3), the prize counts from 1.
        s.Events[0xAC] = 0x02 | 0xF0; // STOCK, other bits in the byte don't matter
        Assert.False(s.IsSet(HeartFlag.Register(0xAC03, 3)));
        s.Events[0xAC] = 0x03;
        Assert.True(s.IsSet(HeartFlag.Register(0xAC03, 3)));
        s.Events[0xFE] = 0x02;
        Assert.True(s.IsSet(HeartFlag.Register(0xFE07, 1)));

        s.CompleteMaps[0] = 1u << 8; // chart 9 = isCompleteMap(8)
        Assert.True(s.IsSet(HeartFlag.Chart(9)));
        Assert.False(s.IsSet(HeartFlag.Chart(8)));
        s.Ocean[17] = 1;
        Assert.True(s.IsSet(HeartFlag.Ocean(17, 0)));

        // Maggie: Moblin's Letter ever obtained and no longer in the bag.
        Assert.False(s.IsSet(HeartFlag.MoblinsLetter));
        s.GetBagReserve = 1u << HeartFlagState.MoblinsLetterReserveBit;
        s.DeliveryBag[3] = HeartFlagState.MoblinsLetterItem;
        Assert.False(s.IsSet(HeartFlag.MoblinsLetter));
        s.DeliveryBag[3] = 0xFF;
        Assert.True(s.IsSet(HeartFlag.MoblinsLetter));
    }

    [Fact]
    public void Catalogue_HasSixteenRewardsWithDistinctFlags()
    {
        Assert.Equal(16, HeartCatalog.Rewards.Count);
        Assert.All(HeartCatalog.Rewards, r => Assert.Equal(1, r.Quarters));
        Assert.Equal(HeartCatalog.Rewards.Count, HeartCatalog.Rewards.Select(r => r.Flag).Distinct().Count());
        Assert.Equal(34, HeartCatalog.StageSourceNames.Count);
        Assert.Empty(HeartCatalog.Rewards.Select(r => r.Flag).Intersect(HeartCatalog.StageSourceNames.Keys));
        Assert.Equal(50, HeartCatalog.Rewards.Select(r => r.Name).Concat(HeartCatalog.StageSourceNames.Values).Distinct().Count());
    }

    /// <summary>
    /// The vanilla game: exactly 44 Pieces of Heart and 6 Heart Containers, each on its own flag, the stage-file part
    /// exactly the catalogue's 34 named sources, and every flag set derives 20 hearts (12 + 44 + 24 = 80).
    /// </summary>
    [VanillaGameFact]
    public void VanillaGame_Has44PiecesAnd6Containers_AndEveryFlagDerives20Hearts()
    {
        var watch = Stopwatch.StartNew();
        var skipped = new List<string>();
        var table = HeartTableBuilder.BuildFromGame(TestRepo.VanillaGamePath()!, skipped.Add);
        output.WriteLine($"built in {watch.ElapsedMilliseconds} ms from {table?.StageCount} stages; {table?.Summary()}");
        Assert.NotNull(table);
        Assert.Empty(skipped);
        foreach (var s in table!.Sources) output.WriteLine($"{s.Quarters} | {s.Type,-10} | {s.Flag,-28} | {s.Name} | {s.Where}");
        foreach (var u in table.Untracked) output.WriteLine($"untracked: {u}");

        Assert.Equal(HeartTable.VanillaPieces, table.PiecesTotal);
        Assert.Equal(HeartTable.VanillaContainers, table.ContainersTotal);
        Assert.Equal(table.Sources.Count, table.Sources.Select(s => s.Flag).Distinct().Count());
        Assert.Empty(table.Untracked);

        // The stage files hold exactly the catalogue's named sources (every one found, nothing unnamed).
        var fromStages = table.Sources.Where(s => s.Type != HeartSourceType.Reward).ToList();
        Assert.Equal(HeartCatalog.StageSourceNames.Keys.OrderBy(f => f.ToString()), fromStages.Select(s => s.Flag).OrderBy(f => f.ToString()));
        Assert.All(fromStages, s => Assert.Equal(HeartCatalog.StageSourceNames[s.Flag], s.Name));
        Assert.Equal(6, fromStages.Count(s => s.Type == HeartSourceType.Boss));

        var none = new HeartFlagState();
        Assert.Equal(HeartTable.StartingQuarters, table.Tally(none).MaxLife);
        var all = new HeartFlagState();
        foreach (var s in table.Sources) all.Set(s.Flag);
        var tally = table.Tally(all);
        Assert.Equal(new HeartTally(6, 44), tally);
        Assert.Equal(HeartTable.MaxQuarters, HeartTable.StartingQuarters + tally.Quarters);
        Assert.Equal(HeartTable.MaxQuarters, tally.MaxLife);

        // Every source has its catalogue ID: the room's heart-source set covers all 50, and the set alone derives 80.
        Assert.All(table.Sources, src => Assert.NotNull(src.Id));
        Assert.Equal((1UL << 50) - 1, table.SourceBits(all));
        Assert.Equal(HeartTable.MaxQuarters, table.Tally(none, table.SourceBits(all)).MaxLife);

        // Each source alone adds exactly its own quarters (no flag stands for two sources).
        foreach (var s in table.Sources)
        {
            var one = new HeartFlagState();
            one.Set(s.Flag);
            Assert.Equal(s.Quarters, table.Tally(one).Quarters);
        }
    }
}
