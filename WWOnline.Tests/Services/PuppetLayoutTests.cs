using WWOnline.Data;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Invariants of the shared memory layout generated from puppet_shared.h. The C side has the
/// same checks as compile-time typedefs; these catch the C# side (and anyone editing the
/// header without compiling the C code).
/// </summary>
public class PuppetLayoutTests
{
    private static readonly int[] U32OrF32Fields =
    [
        PuppetLayout.PUPPET_SLOT_OFF_ACTIVE, PuppetLayout.PUPPET_SLOT_OFF_POSX, PuppetLayout.PUPPET_SLOT_OFF_POSY,
        PuppetLayout.PUPPET_SLOT_OFF_POSZ, PuppetLayout.PUPPET_SLOT_OFF_ANIM_SPEED, PuppetLayout.PUPPET_SLOT_OFF_STATE_FLAGS,
        PuppetLayout.PUPPET_SLOT_OFF_VELOCITY_F, PuppetLayout.PUPPET_SLOT_OFF_SPEED_F,
        PuppetLayout.PUPPET_SLOT_OFF_MAX_NORMAL_SPEED, PuppetLayout.PUPPET_SLOT_OFF_STICK_DISTANCE,
        PuppetLayout.PUPPET_SLOT_OFF_MODE_FLG, PuppetLayout.PUPPET_SLOT_OFF_NO_RESET_FLG0,
        PuppetLayout.PUPPET_SLOT_OFF_NO_RESET_FLG1,
    ];

    [Fact]
    public void EveryFourByteField_FitsInsideSlot_AndIsAligned()
    {
        foreach (var offset in U32OrF32Fields)
        {
            Assert.True(offset + 4 <= PuppetLayout.PUPPET_SLOT_SIZE, $"field at 0x{offset:X2} overruns slot size 0x{PuppetLayout.PUPPET_SLOT_SIZE:X2}");
            Assert.Equal(0, offset % 4);
        }
    }

    [Fact]
    public void Slots_DoNotOverlapHeaderOrTrackingArrays()
    {
        Assert.True(PuppetLayout.PUPPET_SYNC_BASE + PuppetLayout.PUPPET_HDR_SIZE <= PuppetLayout.PUPPET_SLOT_0);
        Assert.True(GameMemoryAddresses.PuppetSync.SlotRegionEnd <= PuppetLayout.PUPPET_PROC_IDS_ADDR);
    }

    [Fact]
    public void ScratchAddresses_AreUniqueAndWordAligned()
    {
        uint[] scratch =
        [
            PuppetLayout.FRAME_COUNTER_ADDR, PuppetLayout.COUNTER_ADDR, PuppetLayout.STATUS_ADDR,
            PuppetLayout.DEBUG_PTR_ADDR, PuppetLayout.RESULT_PTR_ADDR, PuppetLayout.SPAWN_FRAME_COUNTER_ADDR,
            PuppetLayout.DBG_MAGIC_DETECTED_ADDR, PuppetLayout.DBG_CREATE_ATTEMPTS_ADDR,
            PuppetLayout.DBG_CREATE_SUCCESSES_ADDR, PuppetLayout.DBG_CREATE_FAILURES_ADDR,
            PuppetLayout.DBG_LAST_DESIRED_ADDR, PuppetLayout.DBG_LAST_PID_ADDR, PuppetLayout.DBG_DELETE_ISSUED_ADDR,
            PuppetLayout.CLIENT_DBG_SLOT0_GATES, PuppetLayout.CLIENT_HEARTBEAT_ADDR,
            PuppetLayout.CLIENT_DBG_UNSTABLE_HITS, PuppetLayout.CLIENT_DBG_STABILITY_BITS,
            PuppetLayout.CLIENT_DBG_LAST_ACTIVE, PuppetLayout.CLIENT_DBG_WRITE_COUNTER,
            PuppetLayout.PUPPET_EQUIP_SWAP_ACTIVE_ADDR, PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR,
            PuppetLayout.WORLDSYNC_ITEM_MASK_ADDR, PuppetLayout.WORLDSYNC_STAGE_TAG_ADDR,
            PuppetLayout.WORLDSYNC_DESPAWN_COUNT_ADDR, PuppetLayout.WORLDSYNC_LAST_DESPAWN_ADDR,
            PuppetLayout.WORLDSYNC_PENDING_ADDR, PuppetLayout.WORLDSYNC_SCAN_SEQ_ADDR,
            PuppetLayout.WORLDSYNC_SCAN_TAG_ADDR, PuppetLayout.WORLDSYNC_SCAN_CAND_ADDR,
            PuppetLayout.LOCAL_APPEARANCE_TAG_ADDR, PuppetLayout.LOCAL_APPEARANCE_WORD_ADDR,
            PuppetLayout.LOCAL_APPEARANCE_BLOCK_ADDR, PuppetLayout.LOCAL_APPEARANCE_IMAGE_ADDR,
            PuppetLayout.LOCAL_APPEARANCE_APPLIED_ADDR, PuppetLayout.LOCAL_APPEARANCE_STATUS_ADDR,
            PuppetLayout.PUPPET_NAMES_PTR_ADDR,
        ];
        Assert.Equal(scratch.Length, scratch.Distinct().Count());
        Assert.All(scratch, a => Assert.Equal(0u, a % 4));

        // Below SCRATCH_REGION_START is the DOL's .sbss2 — live game constants.
        Assert.All(scratch, a => Assert.InRange(a, PuppetLayout.SCRATCH_REGION_START, PuppetLayout.SCRATCH_REGION_END - 4));
        Assert.True(PuppetLayout.PUPPET_SYNC_BASE >= PuppetLayout.SCRATCH_REGION_START);
        Assert.True(PuppetLayout.PUPPET_SPAWN_COUNTER + 4 <= PuppetLayout.SCRATCH_REGION_END);

        // No scratch word may sit inside the slot block or the hook's tracking arrays.
        Assert.DoesNotContain(scratch, a => a >= PuppetLayout.PUPPET_SYNC_BASE && a < GameMemoryAddresses.PuppetSync.SlotRegionEnd);
        Assert.DoesNotContain(scratch, a => a >= PuppetLayout.PUPPET_PROC_IDS_ADDR && a < PuppetLayout.PUPPET_SPAWN_COUNTER + 4);
    }

    [Fact]
    public void BoatBlocks_FitInScratch_WithoutTouchingNeighbours()
    {
        uint boatEnd = PuppetLayout.PUPPET_BOAT_0 + (uint)(PuppetLayout.PUPPET_BOAT_SIZE * PuppetLayout.PUPPET_MAX_SLOTS);
        Assert.True(PuppetLayout.PUPPET_BOAT_0 >= PuppetLayout.LOCAL_APPEARANCE_STATUS_ADDR + 4);
        Assert.True(boatEnd <= PuppetLayout.WORLDSYNC_ITEM_MASK_ADDR);
        Assert.True(boatEnd <= PuppetLayout.SCRATCH_REGION_END);
        Assert.Equal(0u, PuppetLayout.PUPPET_BOAT_0 % 4);

        foreach (var offset in new[] { PuppetLayout.PUPPET_BOAT_OFF_FLAGS, PuppetLayout.PUPPET_BOAT_OFF_POSX,
                     PuppetLayout.PUPPET_BOAT_OFF_POSY, PuppetLayout.PUPPET_BOAT_OFF_POSZ, PuppetLayout.PUPPET_BOAT_OFF_SPEED_F })
        {
            Assert.True(offset + 4 <= PuppetLayout.PUPPET_BOAT_SIZE);
            Assert.Equal(0, offset % 4);
        }
        Assert.Equal(0, PuppetLayout.PUPPET_BOAT_SIZE % 4);

        // s16 fields: 2-aligned, inside the block, after the f32s and FLAGS.
        foreach (var offset in new[] { PuppetLayout.PUPPET_BOAT_OFF_ROTY, PuppetLayout.PUPPET_BOAT_OFF_SAIL_ANGLE,
                     PuppetLayout.PUPPET_BOAT_OFF_TILLER, PuppetLayout.PUPPET_BOAT_OFF_HEAD_X, PuppetLayout.PUPPET_BOAT_OFF_HEAD_Y })
        {
            Assert.True(offset + 2 <= PuppetLayout.PUPPET_BOAT_SIZE);
            Assert.Equal(0, offset % 2);
            Assert.True(offset >= PuppetLayout.PUPPET_BOAT_OFF_SPEED_F + 4);
        }
        foreach (var offset in new[] { PuppetLayout.PUPPET_BOAT_OFF_MAST_FRAME, PuppetLayout.PUPPET_BOAT_OFF_HEAD_FRAME })
        {
            Assert.True(offset + 1 <= PuppetLayout.PUPPET_BOAT_SIZE);
            Assert.True(offset >= PuppetLayout.PUPPET_BOAT_OFF_HEAD_Y + 2);
        }
    }

    [Fact]
    public void BoatFields_DoNotOverlap()
    {
        var fields = new (int Offset, int Size)[]
        {
            (PuppetLayout.PUPPET_BOAT_OFF_FLAGS, 4), (PuppetLayout.PUPPET_BOAT_OFF_POSX, 4),
            (PuppetLayout.PUPPET_BOAT_OFF_POSY, 4), (PuppetLayout.PUPPET_BOAT_OFF_POSZ, 4),
            (PuppetLayout.PUPPET_BOAT_OFF_SPEED_F, 4), (PuppetLayout.PUPPET_BOAT_OFF_ROTY, 2),
            (PuppetLayout.PUPPET_BOAT_OFF_SAIL_ANGLE, 2), (PuppetLayout.PUPPET_BOAT_OFF_TILLER, 2),
            (PuppetLayout.PUPPET_BOAT_OFF_HEAD_X, 2), (PuppetLayout.PUPPET_BOAT_OFF_HEAD_Y, 2),
            (PuppetLayout.PUPPET_BOAT_OFF_MAST_FRAME, 1), (PuppetLayout.PUPPET_BOAT_OFF_HEAD_FRAME, 1),
        };
        var used = new bool[PuppetLayout.PUPPET_BOAT_SIZE];
        foreach (var (offset, size) in fields)
        {
            for (int i = offset; i < offset + size; i++)
            {
                Assert.False(used[i], $"boat byte 0x{i:X2} is used twice");
                used[i] = true;
            }
        }
    }

    [Fact]
    public void BoatFlags_AreDistinctBits()
    {
        int[] flags = { PuppetLayout.PUPPET_BOAT_FLAG_ACTIVE, PuppetLayout.PUPPET_BOAT_FLAG_FLY,
            PuppetLayout.PUPPET_BOAT_FLAG_MAST_ON, PuppetLayout.PUPPET_BOAT_FLAG_MAST_HIDE };
        int all = 0;
        foreach (int flag in flags)
        {
            Assert.Equal(1, System.Numerics.BitOperations.PopCount((uint)flag));
            Assert.Equal(0, all & flag);
            all |= flag;
        }
        // The head bck rides in FLAGS above the flag bits and fits every Ship.arc bck index.
        Assert.Equal(0, all & (PuppetLayout.PUPPET_BOAT_HEAD_BCK_MASK << PuppetLayout.PUPPET_BOAT_HEAD_BCK_SHIFT));
        Assert.True(PuppetLayout.SHIP_BCK_LAST <= PuppetLayout.PUPPET_BOAT_HEAD_BCK_MASK);
    }

    [Fact]
    public void BoatPart_RidesInFlags_BetweenTheFlagBitsAndTheHeadBck()
    {
        int part = PuppetLayout.PUPPET_BOAT_PART_MASK << PuppetLayout.PUPPET_BOAT_PART_SHIFT;
        int flagBits = PuppetLayout.PUPPET_BOAT_FLAG_ACTIVE | PuppetLayout.PUPPET_BOAT_FLAG_FLY |
                       PuppetLayout.PUPPET_BOAT_FLAG_MAST_ON | PuppetLayout.PUPPET_BOAT_FLAG_MAST_HIDE;
        Assert.Equal(0, part & flagBits);
        Assert.Equal(0, part & (PuppetLayout.PUPPET_BOAT_HEAD_BCK_MASK << PuppetLayout.PUPPET_BOAT_HEAD_BCK_SHIFT));
        Assert.True(PuppetLayout.SHIP_PART_CRANE <= PuppetLayout.PUPPET_BOAT_PART_MASK);
    }

    [Fact]
    public void ShipParts_MatchBoatState()
    {
        // daShip_c::Part_e (d_a_ship.h): the REL (puppet_shared.h) and BoatState agree.
        Assert.Equal(PuppetLayout.SHIP_PART_WAIT, (int)BoatState.PartWait);
        Assert.Equal(PuppetLayout.SHIP_PART_STEER, (int)BoatState.PartSteer);
        Assert.Equal(PuppetLayout.SHIP_PART_CANNON, (int)BoatState.PartCannon);
        Assert.Equal(PuppetLayout.SHIP_PART_CRANE, (int)BoatState.PartCrane);
        Assert.Equal(PuppetLayout.SHIP_ROPE_MAX, (int)BoatState.MaxRopeLength);
    }

    [Fact]
    public void BoatCannonAndCraneWords_FillTwoScratchGaps_WithoutTouchingNeighbours()
    {
        uint cannonEnd = PuppetLayout.PUPPET_BOAT_CANNON_0 + (uint)(PuppetLayout.PUPPET_BOAT_CANNON_SIZE * PuppetLayout.PUPPET_MAX_SLOTS);
        uint craneEnd = PuppetLayout.PUPPET_BOAT_CRANE_0 + (uint)(PuppetLayout.PUPPET_BOAT_CRANE_SIZE * PuppetLayout.PUPPET_MAX_SLOTS);

        // Cannon words: between the hook's settle counter and its debug counters.
        Assert.True(PuppetLayout.PUPPET_BOAT_CANNON_0 >= PuppetLayout.SPAWN_FRAME_COUNTER_ADDR + 4);
        Assert.True(cannonEnd <= PuppetLayout.DBG_MAGIC_DETECTED_ADDR);
        // Crane words: between the client's slot-0 gate bitmap and its heartbeat.
        Assert.True(PuppetLayout.PUPPET_BOAT_CRANE_0 >= PuppetLayout.CLIENT_DBG_SLOT0_GATES + 4);
        Assert.True(craneEnd <= PuppetLayout.CLIENT_HEARTBEAT_ADDR);

        // Inside the scratch region (above .sbss2), clear of the small-keys reserve 0x803FD1D0..DF.
        foreach (var (start, end) in new[] { (PuppetLayout.PUPPET_BOAT_CANNON_0, cannonEnd), (PuppetLayout.PUPPET_BOAT_CRANE_0, craneEnd) })
        {
            Assert.True(start >= PuppetLayout.SCRATCH_REGION_START);
            Assert.True(end <= PuppetLayout.SCRATCH_REGION_END);
            Assert.True(end <= 0x803FD1D0u || start >= 0x803FD1E0u);
            Assert.Equal(0u, start % 4);
        }

        // No listed scratch word inside either block.
        uint[] words =
        [
            PuppetLayout.SPAWN_FRAME_COUNTER_ADDR, PuppetLayout.DBG_MAGIC_DETECTED_ADDR, PuppetLayout.DBG_CREATE_ATTEMPTS_ADDR,
            PuppetLayout.DBG_CREATE_SUCCESSES_ADDR, PuppetLayout.DBG_CREATE_FAILURES_ADDR, PuppetLayout.DBG_LAST_DESIRED_ADDR,
            PuppetLayout.DBG_LAST_PID_ADDR, PuppetLayout.DBG_DELETE_ISSUED_ADDR, PuppetLayout.CLIENT_DBG_SLOT0_GATES,
            PuppetLayout.CLIENT_HEARTBEAT_ADDR, PuppetLayout.CLIENT_DBG_UNSTABLE_HITS, PuppetLayout.CLIENT_DBG_STABILITY_BITS,
            PuppetLayout.CLIENT_DBG_LAST_ACTIVE, PuppetLayout.CLIENT_DBG_WRITE_COUNTER, PuppetLayout.PUPPET_SYNC_BASE,
        ];
        Assert.DoesNotContain(words, a => a + 4 > PuppetLayout.PUPPET_BOAT_CANNON_0 && a < cannonEnd);
        Assert.DoesNotContain(words, a => a + 4 > PuppetLayout.PUPPET_BOAT_CRANE_0 && a < craneEnd);

        Assert.Equal(PuppetLayout.PUPPET_BOAT_CANNON_0 + (uint)PuppetLayout.PUPPET_BOAT_CANNON_SIZE,
            GameMemoryAddresses.PuppetSync.GetBoatCannonBase(1));
        Assert.Equal(PuppetLayout.PUPPET_BOAT_CRANE_0 + (uint)PuppetLayout.PUPPET_BOAT_CRANE_SIZE,
            GameMemoryAddresses.PuppetSync.GetBoatCraneBase(1));

        // Fields fit their word, aligned, and don't overlap.
        Assert.Equal(0, PuppetLayout.PUPPET_BOAT_CANNON_OFF_YAW % 2);
        Assert.True(PuppetLayout.PUPPET_BOAT_CANNON_OFF_YAW + 2 <= PuppetLayout.PUPPET_BOAT_CANNON_OFF_PITCH);
        Assert.True(PuppetLayout.PUPPET_BOAT_CANNON_OFF_PITCH + 2 <= PuppetLayout.PUPPET_BOAT_CANNON_SIZE);
        Assert.Equal(0, PuppetLayout.PUPPET_BOAT_CANNON_OFF_PITCH % 2);
        Assert.Equal(0, PuppetLayout.PUPPET_BOAT_CRANE_OFF_ANGLE % 2);
        Assert.True(PuppetLayout.PUPPET_BOAT_CRANE_OFF_ANGLE + 2 <= PuppetLayout.PUPPET_BOAT_CRANE_OFF_ROPE);
        Assert.True(PuppetLayout.PUPPET_BOAT_CRANE_OFF_ROPE + 1 <= PuppetLayout.PUPPET_BOAT_CRANE_SIZE);
    }

    [Fact]
    public void BoatBlocks_LeaveTheReservedWordsFree()
    {
        // docs/small-keys.md reserves 0x803FD1D0..0x803FD1DF (between the boats and world sync).
        Assert.True(PuppetLayout.PUPPET_BOAT_0 + (uint)(PuppetLayout.PUPPET_BOAT_SIZE * PuppetLayout.PUPPET_MAX_SLOTS) <= 0x803FD1D0u);
    }

    [Fact]
    public void ShipBckIndices_MatchBoatState()
    {
        // Ship.h dRes_INDEX_SHIP_BCK_*: the REL (puppet_shared.h) and BoatState's validation agree.
        Assert.Equal(PuppetLayout.SHIP_BCK_FIRST, (int)BoatState.FirstShipBck);
        Assert.Equal(PuppetLayout.SHIP_BCK_LAST, (int)BoatState.LastShipBck);
        Assert.Equal(PuppetLayout.SHIP_BCK_MAST_OFF2, (int)BoatState.MastOffBck);
        Assert.Equal(PuppetLayout.SHIP_BCK_MAST_ON2, (int)BoatState.MastOnBck);
        Assert.True(BoatState.IsHeadBck(PuppetLayout.SHIP_BCK_FN_LOOK_L));
        Assert.True(BoatState.IsHeadBck(PuppetLayout.SHIP_BCK_DAMAGE1));
        Assert.False(BoatState.IsHeadBck(PuppetLayout.SHIP_BCK_MAST_ON2));
    }

    [Fact]
    public void NamesPointer_SitsInTheFreeWordsBeforeTheBoats()
    {
        // 0x803FD168..0x803FD16F was the only free scratch; the names block itself lives on the game heap.
        Assert.True(PuppetLayout.PUPPET_NAMES_PTR_ADDR >= PuppetLayout.LOCAL_APPEARANCE_STATUS_ADDR + 4);
        Assert.True(PuppetLayout.PUPPET_NAMES_PTR_ADDR + 4 <= PuppetLayout.PUPPET_BOAT_0);
        Assert.Equal(0u, PuppetLayout.PUPPET_NAMES_PTR_ADDR % 4);
    }

    [Fact]
    public void NamesBlock_HoldsEverySlotsName_WithRoomForTheNul()
    {
        Assert.True(PuppetLayout.PUPPET_NAMES_OFF_MAGIC + 4 <= PuppetLayout.PUPPET_NAMES_OFF_FLAGS);
        Assert.True(PuppetLayout.PUPPET_NAMES_OFF_FLAGS + 4 <= PuppetLayout.PUPPET_NAMES_OFF_BOOT);
        Assert.True(PuppetLayout.PUPPET_NAMES_OFF_BOOT + GameMemoryAddresses.System.OSStartTime.Length
                    <= PuppetLayout.PUPPET_NAMES_OFF_NAME0);
        Assert.Equal(0, PuppetLayout.PUPPET_NAMES_OFF_BOOT % 4);
        Assert.True(PuppetLayout.PUPPET_NAMES_OFF_NAME0 + PuppetLayout.PUPPET_MAX_SLOTS * PuppetLayout.PUPPET_NAME_BYTES
                    <= PuppetLayout.PUPPET_NAMES_BLOCK_SIZE);
        Assert.Equal(0, PuppetLayout.PUPPET_NAMES_BLOCK_SIZE % 4);
        Assert.Equal(0, PuppetLayout.PUPPET_NAMES_OFF_FLAGS % 4);
        Assert.InRange(PuppetLayout.PUPPET_NAME_MAX_CHARS, 1, PuppetLayout.PUPPET_NAME_BYTES - 1);
        Assert.Equal(1, System.Numerics.BitOperations.PopCount((uint)PuppetLayout.PUPPET_NAMES_FLAG_SHOW));
        Assert.Equal(0x4E414D45, PuppetLayout.PUPPET_NAMES_MAGIC); // "NAME"
    }

    [Fact]
    public void HeldItemFields_FitAfterTheV2Fields_WithoutOverlap()
    {
        // s16 aim angles: 2-aligned, after the last u32 of the v2 block; the u8 grab kind last.
        Assert.True(PuppetLayout.PUPPET_SLOT_OFF_NO_RESET_FLG1 + 4 <= PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_X);
        Assert.Equal(0, PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_X % 2);
        Assert.Equal(0, PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_Y % 2);
        Assert.True(PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_X + 2 <= PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_Y);
        Assert.True(PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_Y + 2 <= PuppetLayout.PUPPET_SLOT_OFF_GRAB_KIND);
        Assert.True(PuppetLayout.PUPPET_SLOT_OFF_GRAB_KIND + 1 <= PuppetLayout.PUPPET_SLOT_SIZE);
        Assert.Equal(0, PuppetLayout.PUPPET_SLOT_SIZE % 4);

        Assert.Equal(PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_X, GameMemoryAddresses.PuppetSync.SlotOffset_BodyAngleX);
        Assert.Equal(PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_Y, GameMemoryAddresses.PuppetSync.SlotOffset_BodyAngleY);
        Assert.Equal(PuppetLayout.PUPPET_SLOT_OFF_GRAB_KIND, GameMemoryAddresses.PuppetSync.SlotOffset_GrabKind);
    }

    [Fact]
    public void GrabKinds_MatchTheSharedModel()
    {
        // The server clamps PuppetData grab kinds above EquipmentState.MaxGrabKind to 0 (WWOnline.Shared can't see PuppetLayout).
        Assert.Equal(PuppetLayout.PUPPET_GRAB_KIND_MAX, (int)EquipmentState.MaxGrabKind);
        Assert.Equal(0, PuppetLayout.PUPPET_GRAB_KIND_NONE);
        Assert.InRange(PuppetLayout.PUPPET_GRAB_KIND_BOMB, 1, PuppetLayout.PUPPET_GRAB_KIND_MAX);
    }

    [Fact]
    public void LinkFieldOffsets_MatchTheDecomp()
    {
        // tww-decomp d_a_player.h:491 mBodyAngle (csXyz), d_a_player_main.h:2088 mActorKeepGrab (+4 = mActor),
        // f_pc_base.h:16 mProcName, f_pc_name.h:308 fpcNm_BOMB_e, LkAnm.h TAKE..TAKER.
        Assert.Equal(0x2B4, PuppetLayout.DAPY_OFF_BODY_ANGLE_X);
        Assert.Equal(PuppetLayout.DAPY_OFF_BODY_ANGLE_X + 2, PuppetLayout.DAPY_OFF_BODY_ANGLE_Y);
        Assert.Equal(0x318C + 4, PuppetLayout.DAPY_OFF_GRAB_ACTOR);
        Assert.Equal(0x08, PuppetLayout.FPC_OFF_PROC_NAME);
        Assert.Equal(0x128, PuppetLayout.FPC_NAME_BOMB);
        Assert.Equal(PuppetLayout.DAPY_UPPER_ANM_TAKE + 3, PuppetLayout.DAPY_UPPER_ANM_TAKER);
    }

    [Fact]
    public void SlotBuffer_MatchesLayoutSize()
    {
        Assert.Equal(PuppetLayout.PUPPET_SLOT_SIZE, GameMemoryAddresses.PuppetSync.SlotSize);
        Assert.Equal(PuppetLayout.PUPPET_SLOT_0 + 2u * PuppetLayout.PUPPET_SLOT_SIZE, GameMemoryAddresses.PuppetSync.GetSlotBase(2));
    }
}
