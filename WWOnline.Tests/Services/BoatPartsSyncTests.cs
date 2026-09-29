using System.Buffers.Binary;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The peer boat's cannon / crane: read from the local daShip_c (PuppetSyncService.ParseShipPose)
/// and written to the slot's FLAGS word and PUPPET_BOAT_CANNON_* / PUPPET_BOAT_CRANE_* words for the REL.
/// </summary>
public class BoatPartsSyncTests
{
    /// <summary>A daShip_c block as ReadLocalBoatPose reads it (from mPart to m03E8), at rest.</summary>
    private static byte[] ShipBlock(byte part = 0, short yaw = 0, short pitch = 0, short crane = 0, short swing = 0, short rope = 0)
    {
        var block = new byte[PuppetSyncService.ShipPoseLength];
        void S16(uint offset, short value) =>
            BinaryPrimitives.WriteInt16BigEndian(block.AsSpan((int)(offset - PuppetSyncService.ShipPoseStart)), value);

        block[GameMemoryAddresses.Sea.ShipOffsetPart - PuppetSyncService.ShipPoseStart] = part;
        S16(GameMemoryAddresses.Sea.ShipOffsetCannonYaw, yaw);
        S16(GameMemoryAddresses.Sea.ShipOffsetCannonPitch, pitch);
        S16(GameMemoryAddresses.Sea.ShipOffsetCraneAngle, crane);
        S16(GameMemoryAddresses.Sea.ShipOffsetCraneSwing, swing);
        S16(GameMemoryAddresses.Sea.ShipOffsetRopeCnt, rope);
        S16(GameMemoryAddresses.Sea.ShipOffsetMastBck, PuppetLayout.SHIP_BCK_MAST_OFF2);
        BinaryPrimitives.WriteInt32BigEndian(
            block.AsSpan((int)(GameMemoryAddresses.Sea.ShipOffsetMastScale - PuppetSyncService.ShipPoseStart)),
            BitConverter.SingleToInt32Bits(1.0f));
        return block;
    }

    private static BoatState Parse(byte[] block)
    {
        var boat = new BoatState();
        PuppetSyncService.ParseShipPose(block, boat);
        return boat;
    }

    [Fact]
    public void PoseBlock_StartsAtMPart_AndCoversTheMastScale()
    {
        Assert.Equal(GameMemoryAddresses.Sea.ShipOffsetPart, PuppetSyncService.ShipPoseStart);
        Assert.Equal((int)(0x3E8 + 4 - 0x34E), PuppetSyncService.ShipPoseLength);
    }

    [Fact]
    public void Cannon_ReadsItsAim()
    {
        var boat = Parse(ShipBlock(part: BoatState.PartCannon, yaw: -0x2345, pitch: 0x1800, crane: 0x3000, rope: 20));
        Assert.Equal(BoatState.PartCannon, boat.Part);
        Assert.Equal(-0x2345, boat.CannonYaw);
        Assert.Equal(0x1800, boat.CannonPitch);
        // Not the crane's: those stay at rest.
        Assert.Equal(0, boat.CraneAngle);
        Assert.Equal(0, boat.RopeLength);
        Assert.True(boat.IsValid());
    }

    [Fact]
    public void Crane_AddsTheSwingToTheArm_AndReadsTheRope()
    {
        var boat = Parse(ShipBlock(part: BoatState.PartCrane, yaw: 0x1000, crane: -0x3000, swing: -0x800, rope: 250));
        Assert.Equal(BoatState.PartCrane, boat.Part);
        Assert.Equal(-0x3800, boat.CraneAngle);
        Assert.Equal(250, boat.RopeLength);
        Assert.Equal(0, boat.CannonYaw);
        Assert.True(boat.IsValid());
    }

    [Fact]
    public void SurpriseValues_AreClamped_NotRejected()
    {
        var cannon = Parse(ShipBlock(part: BoatState.PartCannon, pitch: -5));
        Assert.Equal(0, cannon.CannonPitch);
        Assert.True(cannon.IsValid());

        var crane = Parse(ShipBlock(part: BoatState.PartCrane, crane: 0x3000, swing: 0x1000, rope: 900));
        Assert.Equal(BoatState.MaxCraneAngle, crane.CraneAngle);
        Assert.Equal(BoatState.MaxRopeLength, crane.RopeLength);
        Assert.True(crane.IsValid());

        var garbage = Parse(ShipBlock(part: 7, yaw: 0x100));
        Assert.Equal(BoatState.PartWait, garbage.Part);
        Assert.Equal(0, garbage.CannonYaw);
        Assert.True(garbage.IsValid());
    }

    [Fact]
    public void Sail_LeavesThePartsAtRest()
    {
        var boat = Parse(ShipBlock(part: BoatState.PartSteer, yaw: 0x100, crane: 0x200, rope: 20));
        Assert.Equal(BoatState.PartSteer, boat.Part);
        Assert.Equal(0, boat.CannonYaw);
        Assert.Equal(0, boat.CraneAngle);
        Assert.Equal(0, boat.RopeLength);
    }

    [Fact]
    public void ShortBlock_LeavesTheBoatUntouched()
    {
        var boat = new BoatState();
        PuppetSyncService.ParseShipPose(new byte[4], boat);
        Assert.Equal(0, boat.Part);
    }

    [Theory]
    [InlineData(BoatState.PartWait)]
    [InlineData(BoatState.PartSteer)]
    [InlineData(BoatState.PartCannon)]
    [InlineData(BoatState.PartCrane)]
    public void Flags_CarryThePart(byte part)
    {
        uint flags = PuppetSyncService.BoatFlags(new BoatState { Part = part, HeadBck = 0x0E, MastHidden = true });
        Assert.Equal(part, (int)((flags >> PuppetLayout.PUPPET_BOAT_PART_SHIFT) & PuppetLayout.PUPPET_BOAT_PART_MASK));
        Assert.Equal(0x0E, (int)((flags >> PuppetLayout.PUPPET_BOAT_HEAD_BCK_SHIFT) & PuppetLayout.PUPPET_BOAT_HEAD_BCK_MASK));
        Assert.NotEqual(0u, flags & PuppetLayout.PUPPET_BOAT_FLAG_ACTIVE);
        Assert.NotEqual(0u, flags & PuppetLayout.PUPPET_BOAT_FLAG_MAST_HIDE);
    }

    [Fact]
    public void Flags_MarkAParkedBoat()
    {
        Assert.Equal(0u, PuppetSyncService.BoatFlags(new BoatState()) & PuppetLayout.PUPPET_BOAT_FLAG_PARKED);
        uint parked = PuppetSyncService.BoatFlags(new BoatState { Parked = true });
        Assert.NotEqual(0u, parked & PuppetLayout.PUPPET_BOAT_FLAG_PARKED);
        Assert.NotEqual(0u, parked & PuppetLayout.PUPPET_BOAT_FLAG_ACTIVE); // still drawn
    }

    [Theory]
    [InlineData(0, 0, 0, 0x0000)]       // classic red
    [InlineData(255, 255, 255, 0xFFFF)]
    [InlineData(30, 60, 180, 0x19F6)]
    [InlineData(4, 2, 4, 0x0001)]       // encodes to 0: sent as 1, not "classic"
    public void BoatColor565_IsTheHullPaletteFormat(byte r, byte g, byte b, int expected) =>
        Assert.Equal(expected, PuppetSyncService.BoatColor565(new AppearanceState { BoatR = r, BoatG = g, BoatB = b }));

    [Fact]
    public void CannonAndCraneWords_AreBigEndianAtTheSharedOffsets()
    {
        var boat = new BoatState { CannonYaw = -0x1234, CannonPitch = 0x3FFF, CraneAngle = -0x3800, RopeLength = 123 };

        var cannon = Enumerable.Repeat((byte)0xAA, PuppetLayout.PUPPET_BOAT_CANNON_SIZE).ToArray();
        PuppetSyncService.EncodeBoatCannon(boat, cannon);
        Assert.Equal(-0x1234, BinaryPrimitives.ReadInt16BigEndian(cannon.AsSpan(PuppetLayout.PUPPET_BOAT_CANNON_OFF_YAW)));
        Assert.Equal(0x3FFF, BinaryPrimitives.ReadInt16BigEndian(cannon.AsSpan(PuppetLayout.PUPPET_BOAT_CANNON_OFF_PITCH)));

        var crane = Enumerable.Repeat((byte)0xAA, PuppetLayout.PUPPET_BOAT_CRANE_SIZE).ToArray();
        PuppetSyncService.EncodeBoatCrane(boat, crane);
        Assert.Equal(-0x3800, BinaryPrimitives.ReadInt16BigEndian(crane.AsSpan(PuppetLayout.PUPPET_BOAT_CRANE_OFF_ANGLE)));
        Assert.Equal(123, crane[PuppetLayout.PUPPET_BOAT_CRANE_OFF_ROPE]);
        Assert.Equal(0, crane[PuppetLayout.PUPPET_BOAT_CRANE_SIZE - 1]); // the spare byte is cleared
    }
}
