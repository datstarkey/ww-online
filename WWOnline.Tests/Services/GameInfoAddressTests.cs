using WWOnline.Data;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Pins derived game-info addresses to absolute values confirmed independently (vanilla DOL
/// disassembly / LagoLunatic's WW hacking docs), so an offset typo can't silently point a
/// memory write at the wrong field again.
/// </summary>
public class GameInfoAddressTests
{
    [Fact]
    public void Play_IsGameInfoPlus0x12A0() => Assert.Equal(0x803C5EA8u, GameMemoryAddresses.Play);

    [Fact]
    public void StageName_MatchesPlayCurStage() =>
        Assert.Equal(GameMemoryAddresses.Play + 0x3E94, GameMemoryAddresses.Stage.CurrentStageName.Address);

    [Fact]
    public void PendingRupeeDelta_IsPlayItemRupeeCount() =>
        Assert.Equal(0x803CA768u, GameMemoryAddresses.Player.PendingRupeeDelta.Address);

    [Fact]
    public void SmallKeyAddresses_MatchTheVanillaDol()
    {
        // dMeter_keyMove (DOL 0x801FCF28): lha 0x5B74(gameInfo) = play.mItemKeyNumCount, lbz/stb 0x798(gameInfo) = the
        // live mKeyNum, and lbz 9(stagInfo) bit 0 = ChkKeyDisp. item_func_small_key (0x800C31B0) and
        // dDoor_key2_c::keyInit (0x8006C52C) change the same 0x5B74.
        Assert.Equal(0x803CA77Cu, GameMemoryAddresses.WorldFlags.PendingKeyDelta.Address);
        Assert.Equal(GameMemoryAddresses.GameInfo + 0x5B74, GameMemoryAddresses.WorldFlags.PendingKeyDelta.Address);
        Assert.Equal(0x803C53A0u, GameMemoryAddresses.WorldFlags.LiveKeyNum);
        Assert.Equal(GameMemoryAddresses.GameInfo + 0x798, GameMemoryAddresses.WorldFlags.LiveKeyNum);
        Assert.Equal(0x09, GameMemoryAddresses.WorldFlags.StagSaveTblOffset);
        Assert.Equal(0x01, GameMemoryAddresses.WorldFlags.StagKeyDispMask);
        // Saved mKeyNum per dungeon slot (docs/small-keys.md §1): DRC 3, Wind Temple 7.
        Assert.Equal(0x803C5014u, GameMemoryAddresses.WorldFlags.SavedSlot(3) + GameMemoryAddresses.WorldFlags.OffKeyNum);
        Assert.Equal(0x803C50A4u, GameMemoryAddresses.WorldFlags.SavedSlot(7) + GameMemoryAddresses.WorldFlags.OffKeyNum);
    }

    [Fact]
    public void ClothesInputs_MatchPlayerInit()
    {
        // daPy_lk_c::playerInit (DOL 0x80125AC8-0x80125AEC): isEventBit(gameInfo + 0x624, 0x2A80) and
        // lbz clearCount, 0x1A0(gameInfo).
        Assert.Equal(0x803C522Cu, GameMemoryAddresses.Events.EventBits);
        Assert.Equal(GameMemoryAddresses.Events.EventBits, GameMemoryAddresses.Events.EventBitfield.Address);
        Assert.Equal(0x803C5256u, GameMemoryAddresses.Events.HeroClothesEventByte.Address);
        Assert.Equal(0x803C4DA8u, GameMemoryAddresses.Player.ClearCount.Address);
    }

    [Fact]
    public void WorldFlagAddresses_MatchDolVerifiedValues()
    {
        Assert.Equal(0x803C4F88u, GameMemoryAddresses.WorldFlags.SavedMemoryBase);
        Assert.Equal(0x803C5380u, GameMemoryAddresses.WorldFlags.LiveMemory);
        Assert.Equal(0x803C9DA0u, GameMemoryAddresses.WorldFlags.StagInfoPtr);
        Assert.Equal(0x803C9D54u, GameMemoryAddresses.WorldFlags.NextStageEnable);
        Assert.Equal(0x803C9EA2u, GameMemoryAddresses.Events.EventMode); // the REL's GAMEINFO_EVT_MODE (gameInfo + 0x529A)
    }

    [Fact]
    public void ShipAddresses_MatchDecomp()
    {
        // d_com_inf_game.h play.mpPlayerPtr[3] at 0x48AC, [2] = the ship.
        Assert.Equal(0x803CA75Cu, GameMemoryAddresses.Sea.ShipActorPtr);
        // fopAc_ac_c base fields (f_op_actor.h): current.pos, shape_angle.y, speedF.
        Assert.Equal(0x1F8u, GameMemoryAddresses.Sea.ShipOffsetPosX);
        Assert.Equal(0x20Eu, GameMemoryAddresses.Sea.ShipOffsetRotY);
        Assert.Equal(0x254u, GameMemoryAddresses.Sea.ShipOffsetSpeedF);
    }

    [Fact]
    public void ShipPoseOffsets_MatchDecomp()
    {
        // daShip_c (d_a_ship.h) field offsets.
        Assert.Equal(0x298u, GameMemoryAddresses.Sea.ShipOffsetBodyAnm);   // mpBodyAnm
        Assert.Equal(0x29Cu, GameMemoryAddresses.Sea.ShipOffsetHeadAnm);   // mpHeadAnm
        Assert.Equal(0x358u, GameMemoryAddresses.Sea.ShipOffsetStateFlag); // mStateFlag
        Assert.Equal(0x364u, GameMemoryAddresses.Sea.ShipOffsetSailAngle); // mSailAngle
        Assert.Equal(0x366u, GameMemoryAddresses.Sea.ShipOffsetTiller);    // m0366
        Assert.Equal(0x392u, GameMemoryAddresses.Sea.ShipOffsetMastBck);   // m0392
        Assert.Equal(0x3A0u, GameMemoryAddresses.Sea.ShipOffsetHeadX);     // m03A0
        Assert.Equal(0x3A2u, GameMemoryAddresses.Sea.ShipOffsetHeadY);     // m03A2
        Assert.Equal(0x3B4u, GameMemoryAddresses.Sea.ShipOffsetHeadBck);   // m03B4
        Assert.Equal(0x3E8u, GameMemoryAddresses.Sea.ShipOffsetMastScale); // m03E8
        Assert.Equal(0x34Eu, GameMemoryAddresses.Sea.ShipOffsetPart);        // mPart
        Assert.Equal(0x394u, GameMemoryAddresses.Sea.ShipOffsetCannonYaw);   // m0394
        Assert.Equal(0x396u, GameMemoryAddresses.Sea.ShipOffsetCannonPitch); // m0396
        Assert.Equal(0x398u, GameMemoryAddresses.Sea.ShipOffsetCraneAngle);  // m0398
        Assert.Equal(0x39Cu, GameMemoryAddresses.Sea.ShipOffsetCraneSwing);  // m039C
        Assert.Equal(0x39Eu, GameMemoryAddresses.Sea.ShipOffsetRopeCnt);     // mRopeCnt
        // mDoExt_McaMorf::mFrameCtrl at 0x58 (the vanilla ctor at 0x80012650 builds its
        // J3DFrameCtrl at this+0x58) + J3DFrameCtrl::mFrame at 0x10.
        Assert.Equal(0x68u, GameMemoryAddresses.Sea.McaMorfOffsetFrame);
    }
}
