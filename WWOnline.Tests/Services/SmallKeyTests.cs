using WWOnline.Data;
using WWOnline.Patcher.WorldData;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Shared small keys by derivation (<see cref="SmallKeyReconciler"/>): every dungeon's count is its key
/// flags taken minus its key-door switches set, written into the game only when it is idle.
/// </summary>
public class SmallKeyReconcilerTests
{
    private const int Drc = 3, Fw = 4;
    private const int DoorA = 0x36, DoorB = 0x3C; // switch word 1

    /// <summary>DRC-like: key chests (tbox 0, 1), a placed key (item bit 3), two key doors. FW: one of each.
    /// Slot 15: a test map's key without a door (never a dungeon).</summary>
    private static readonly SmallKeyTable Table = new()
    {
        StageCount = 3,
        Dungeons = new Dictionary<int, SmallKeyDungeon>
        {
            [Drc] = new()
            {
                Slot = Drc,
                Sources = [new(SmallKeySourceKind.Chest, 0, "M_NewD2", -1), new(SmallKeySourceKind.Chest, 1, "M_NewD2", -1), new(SmallKeySourceKind.Item, 3, "M_NewD2", 3)],
                Doors = [new(DoorA, "M_NewD2", -1), new(DoorB, "M_NewD2", -1)],
            },
            [Fw] = new()
            {
                Slot = Fw,
                Sources = [new(SmallKeySourceKind.Chest, 5, "kindan", -1)],
                Doors = [new(5, "kindan", -1)],
            },
            [15] = new()
            {
                Slot = 15,
                Sources = [new(SmallKeySourceKind.Chest, 0, "K_Test5", -1)],
                Doors = [],
            },
        },
    };

    private static uint[] Doors(params int[] switches)
    {
        var w = new uint[4];
        foreach (int s in switches) w[s >> 5] |= 1u << (s & 31);
        return w;
    }

    private static SlotKeyState Slot(uint tbox = 0, uint item = 0, uint[]? doors = null, int keys = 0, byte dungeonItem = 0) =>
        new(tbox, item, doors ?? new uint[4], keys, dungeonItem);

    /// <summary>The player in DRC (key HUD on); other slots as given.</summary>
    private static SmallKeyObservation InDrc(SlotKeyState drc, bool idle = true, SlotKeyState? fw = null, bool keyHud = true, SlotKeyState? test = null)
    {
        var slots = Enumerable.Repeat(SlotKeyState.Empty, 16).ToArray();
        slots[Drc] = drc;
        if (fw is { } f) slots[Fw] = f;
        if (test is { } t) slots[15] = t;
        return new SmallKeyObservation(slots, Drc, keyHud, idle, "M_NewD2", 2);
    }

    private sealed class Harness
    {
        public readonly SmallKeyReconciler Reconciler = new();
        public readonly List<string> Log = [];
        public long Tick;
        public List<KeyWrite> Step(SmallKeyObservation obs) => Reconciler.Step(Table, obs, ++Tick, Log);

        /// <summary>Step the same observation until it is idle long enough to write (or give up).</summary>
        public List<KeyWrite> Settle(SmallKeyObservation obs)
        {
            for (int i = 0; i < SmallKeyReconciler.IdleTicksRequired; i++)
            {
                var w = Step(obs);
                if (w.Count > 0) return w;
            }
            return [];
        }
    }

    [Fact]
    public void AnotherPlayersPickup_RaisesTheCount_ThroughTheHud_OnceIdle()
    {
        var h = new Harness();
        // The first idle tick waits; the second writes +1 to the HUD's pending count.
        Assert.Empty(h.Step(InDrc(Slot(tbox: 0b01, keys: 0))));
        var w = Assert.Single(h.Step(InDrc(Slot(tbox: 0b01, keys: 0))));
        Assert.Equal(new KeyWrite(Drc, KeyWriteKind.Pending, +1, 0, 1), w);
        // The HUD applied it: nothing more to do.
        Assert.Empty(h.Settle(InDrc(Slot(tbox: 0b01, keys: 1))));
    }

    [Fact]
    public void WithoutTheKeyHud_TheLiveCountIsWritten()
    {
        var h = new Harness();
        var w = h.Settle(InDrc(Slot(item: 1u << 3, keys: 0), keyHud: false));
        Assert.Equal(new KeyWrite(Drc, KeyWriteKind.Live, 1, 0, 1), Assert.Single(w));
    }

    [Fact]
    public void DuringAnEvent_NothingIsWritten_AndTheGamesOwnPickupNeedsNoWrite()
    {
        var h = new Harness();
        // This player opens a key chest: its flag is set at once, the key lands at the end of the get-item demo.
        for (int i = 0; i < 10; i++)
            Assert.Empty(h.Step(InDrc(Slot(tbox: 0b01, keys: 0), idle: false)));
        // Demo over, the game added its own key: already the derived count.
        Assert.Empty(h.Settle(InDrc(Slot(tbox: 0b01, keys: 1))));
        Assert.Equal(0, h.Reconciler.Surplus(Drc));
        Assert.Empty(h.Log);
    }

    [Fact]
    public void AnotherPlayersDoor_SpendsTheKeyForEveryone()
    {
        var h = new Harness();
        Assert.Empty(h.Settle(InDrc(Slot(tbox: 0b11, keys: 2))));
        var w = h.Settle(InDrc(Slot(tbox: 0b11, doors: Doors(DoorB), keys: 2)));
        Assert.Equal(new KeyWrite(Drc, KeyWriteKind.Pending, -1, 2, 1), Assert.Single(w));
    }

    [Fact]
    public void AKeySpentOnADoorAnotherPlayerAlreadyOpened_IsHandedBack()
    {
        var h = new Harness();
        // 2 keys taken, door B opened by another player (its lock not re-created here yet): 1 key.
        var opened = Slot(tbox: 0b11, doors: Doors(DoorB), keys: 1);
        Assert.Empty(h.Settle(InDrc(opened)));
        // This player opens door B too: its unlock event spends a key (the switch was already set).
        var spent = opened with { KeyNum = 0 };
        for (int i = 0; i < 5; i++) Assert.Empty(h.Step(InDrc(spent, idle: false)));
        var w = h.Settle(InDrc(spent));
        Assert.Equal(new KeyWrite(Drc, KeyWriteKind.Pending, +1, 0, 1), Assert.Single(w));
    }

    [Fact]
    public void TheSameFlagsFromEveryone_NeverCountTwice_AndNeverWriteAgain()
    {
        // Reconnects, repeated snapshots and both players opening the same chest all give the same flags.
        var h = new Harness();
        var state = InDrc(Slot(tbox: 0b11, item: 1u << 3, doors: Doors(DoorA), keys: 2));
        for (int i = 0; i < 20; i++) Assert.Empty(h.Step(state));
        h.Reconciler.Reset(); // the rule went off and on / the game re-attached
        for (int i = 0; i < 20; i++) Assert.Empty(h.Step(state));
    }

    [Fact]
    public void NegativeDerivedCount_IsClampedToZero_AndLogged()
    {
        var h = new Harness();
        // One key taken, two doors open (two players used the same last key on different doors).
        var w = h.Settle(InDrc(Slot(tbox: 0b01, doors: Doors(DoorA, DoorB), keys: 1)));
        Assert.Equal(new KeyWrite(Drc, KeyWriteKind.Pending, -1, 1, 0), Assert.Single(w));
        Assert.Contains(h.Log, l => l.Contains("clamped to 0"));
    }

    [Fact]
    public void AKeyNoFlagExplains_IsKeptForThisPlayer_AndLoggedWithTheStage()
    {
        var h = new Harness();
        Assert.Empty(h.Settle(InDrc(Slot(tbox: 0b01, keys: 1))));
        // The game gains a key with no key flag behind it (a source the table doesn't know).
        Assert.Empty(h.Settle(InDrc(Slot(tbox: 0b01, keys: 2))));
        Assert.Equal(1, h.Reconciler.Surplus(Drc));
        Assert.Contains(h.Log, l => l.Contains("unknown key source") && l.Contains("M_NewD2 room 2"));
        // Spending it on a door: taken 1 - opened 1 + 1 unknown = 1, which is what the game has.
        Assert.Empty(h.Settle(InDrc(Slot(tbox: 0b01, doors: Doors(DoorA), keys: 1))));
        // Reset (rule toggled) keeps it.
        h.Reconciler.Reset();
        Assert.Equal(1, h.Reconciler.Surplus(Drc));
    }

    [Fact]
    public void ALateKeyRightAfterAKnownFlag_IsNotAnUnknownSource()
    {
        var h = new Harness();
        Assert.Empty(h.Settle(InDrc(Slot(keys: 0))));
        // Another player's chest flag arrives and is written (+1) ...
        Assert.Single(h.Settle(InDrc(Slot(tbox: 0b01, keys: 0))));
        // ... and this game's own key for the same chest lands just after (both opened it).
        var w = h.Settle(InDrc(Slot(tbox: 0b01, keys: 2)));
        Assert.Equal(0, h.Reconciler.Surplus(Drc));
        Assert.Equal(new KeyWrite(Drc, KeyWriteKind.Pending, -1, 2, 1), Assert.Single(w));
    }

    [Fact]
    public void AWriteDownThatDoesntLand_IsWrittenAgain_NotTakenAsAnUnknownKey()
    {
        var h = new Harness();
        Assert.Empty(h.Settle(InDrc(Slot(tbox: 0b11, keys: 2))));
        var doorOpened = InDrc(Slot(tbox: 0b11, doors: Doors(DoorB), keys: 2));
        Assert.Single(h.Settle(doorOpened)); // -1 queued ... and lost (the game overwrote it)
        for (int i = 0; i < SmallKeyReconciler.WriteLandTicks + 4; i++)
            h.Step(doorOpened);
        Assert.Equal(0, h.Reconciler.Surplus(Drc));
        Assert.DoesNotContain(h.Log, l => l.Contains("unknown key source"));
    }

    [Fact]
    public void OtherDungeons_AreWrittenThroughTheirSavedCopy_AtOnce()
    {
        var h = new Harness();
        var w = h.Step(InDrc(Slot(), idle: false, fw: Slot(tbox: 1u << 5, keys: 0)));
        Assert.Equal(new KeyWrite(Fw, KeyWriteKind.Saved, 1, 0, 1), Assert.Single(w));
    }

    [Fact]
    public void ASlotWithoutKeyDoors_IsNeverTouched()
    {
        var h = new Harness();
        for (int i = 0; i < 5; i++)
            Assert.Empty(h.Step(InDrc(Slot(), test: Slot(tbox: 1, keys: 7))));
    }
}

/// <summary>Reading the game's small-key state and making the reconciler's writes (<see cref="SmallKeyMemory"/>).</summary>
public class SmallKeyMemoryTests
{
    private const uint StagInfo = 0x80500000;

    /// <summary>A game in DRC (slot 3, key HUD), idle.</summary>
    private static FakeDolphin InDrc(bool keyHud = true)
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.WorldFlags.StagInfoPtr, StagInfo);
        d.Set(StagInfo + (uint)GameMemoryAddresses.WorldFlags.StagSaveTblOffset, (byte)(3 << 1 | (keyHud ? 1 : 0)));
        d.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, (byte)'M', (byte)'_', (byte)'N', 0);
        return d;
    }

    [Fact]
    public void Read_TakesTheCurrentSlotFromTheLiveCopy_AndTheOthersFromTheSave()
    {
        var d = InDrc();
        d.SetU32(GameMemoryAddresses.WorldFlags.LiveMemory + GameMemoryAddresses.WorldFlags.OffTbox, 0b11);
        d.SetU32(GameMemoryAddresses.WorldFlags.LiveMemory + GameMemoryAddresses.WorldFlags.OffSwitch + 4, 1u << 22);
        d.Set(GameMemoryAddresses.WorldFlags.LiveKeyNum, 2);
        d.Set(GameMemoryAddresses.WorldFlags.LiveMemory + GameMemoryAddresses.WorldFlags.OffDungeonItem, 0b101);
        d.Set(GameMemoryAddresses.WorldFlags.SavedSlot(3) + GameMemoryAddresses.WorldFlags.OffKeyNum, 9); // stale saved copy
        d.SetU32(GameMemoryAddresses.WorldFlags.SavedSlot(4) + GameMemoryAddresses.WorldFlags.OffTbox, 1u << 5);
        d.Set(GameMemoryAddresses.WorldFlags.SavedSlot(4) + GameMemoryAddresses.WorldFlags.OffKeyNum, 1);

        var obs = SmallKeyMemory.Read(d)!;
        Assert.Equal(3, obs.CurrentSlot);
        Assert.True(obs.KeyHud);
        Assert.True(obs.Idle);
        Assert.Equal("M_N", obs.Stage);
        Assert.Equal((0b11u, 2, (byte)0b101), (obs.Slots[3].Tbox, obs.Slots[3].KeyNum, obs.Slots[3].DungeonItem));
        Assert.Equal(1u << 22, obs.Slots[3].Switch[1]);
        Assert.Equal((1u << 5, 1), (obs.Slots[4].Tbox, obs.Slots[4].KeyNum));
    }

    [Fact]
    public void Read_IsNotIdle_WhileAKeyChangeIsQueued_OrAnEventRuns_OrThePauseMenuIsOpen()
    {
        var d = InDrc();
        d.Write(GameMemoryAddresses.WorldFlags.PendingKeyDelta, (short)-1);
        Assert.False(SmallKeyMemory.Read(d)!.Idle);
        d.Write(GameMemoryAddresses.WorldFlags.PendingKeyDelta, (short)0);
        d.Set(GameMemoryAddresses.Events.EventMode, 1);
        Assert.False(SmallKeyMemory.Read(d)!.Idle);
        d.Set(GameMemoryAddresses.Events.EventMode, 0);
        d.Set(GameMemoryAddresses.Events.MenuPause, 1);
        Assert.False(SmallKeyMemory.Read(d)!.Idle);
        d.Set(GameMemoryAddresses.Events.MenuPause, 0);
        Assert.True(SmallKeyMemory.Read(d)!.Idle);
    }

    [Fact]
    public void ReadStage_KeyHudFlag()
    {
        Assert.Equal((3, true), SmallKeyMemory.ReadStage(InDrc()));
        Assert.Equal((3, false), SmallKeyMemory.ReadStage(InDrc(keyHud: false)));
        Assert.Equal((null, false), SmallKeyMemory.ReadStage(new FakeDolphin()));
    }

    [Fact]
    public void Apply_WritesThePendingDelta_OnlyWhenNoneIsQueued()
    {
        var d = InDrc();
        Assert.True(SmallKeyMemory.Apply(d, new KeyWrite(3, KeyWriteKind.Pending, -1, 2, 1)));
        Assert.Equal((short)-1, d.Read(GameMemoryAddresses.WorldFlags.PendingKeyDelta));
        Assert.Equal(0xFF, d.Get(0x803CA77C));
        Assert.False(SmallKeyMemory.Apply(d, new KeyWrite(3, KeyWriteKind.Pending, +1, 1, 2))); // the game's -1 is still queued
        Assert.Equal((short)-1, d.Read(GameMemoryAddresses.WorldFlags.PendingKeyDelta));
    }

    [Fact]
    public void Apply_WritesTheLiveOrSavedCount()
    {
        var d = InDrc();
        SmallKeyMemory.Apply(d, new KeyWrite(3, KeyWriteKind.Live, 2, 0, 2));
        Assert.Equal(2, d.Get(0x803C53A0));
        SmallKeyMemory.Apply(d, new KeyWrite(4, KeyWriteKind.Saved, 1, 0, 1));
        Assert.Equal(1, d.Get(0x803C5038)); // FW's saved mKeyNum (docs/small-keys.md §1)
        SmallKeyMemory.Apply(d, new KeyWrite(4, KeyWriteKind.Saved, 500, 0, 500));
        Assert.Equal(99, d.Get(0x803C5038));
    }
}

/// <summary>Where the small-key table comes from (<see cref="SmallKeyTableProvider"/>).</summary>
public class SmallKeyTableProviderTests
{
    private static SmallKeyTable OneDungeon() => new()
    {
        StageCount = 1,
        Dungeons = new Dictionary<int, SmallKeyDungeon>
        {
            [4] = new() { Slot = 4, Sources = [new(SmallKeySourceKind.Chest, 5, "kindan", -1)], Doors = [new(5, "kindan", -1)] },
        },
    };

    [Fact]
    public void Build_TriesEachGameFolderInTurn()
    {
        var tried = new List<string>();
        var table = OneDungeon();
        var provider = new SmallKeyTableProvider(() => ["patched", "vanilla", "PATCHED"], path =>
        {
            tried.Add(path);
            if (path == "patched") return null;           // no stage data here
            return table;
        });
        Assert.Same(table, provider.Build());
        Assert.Equal(["patched", "vanilla"], tried);
    }

    [Fact]
    public void Build_SkipsAFolderThatThrows_AndReturnsNullWithoutStageData()
    {
        var provider = new SmallKeyTableProvider(() => ["broken", "empty"], path =>
            path == "broken" ? throw new InvalidDataException("bad archive") : new SmallKeyTable());
        Assert.Null(provider.Build());
    }
}
