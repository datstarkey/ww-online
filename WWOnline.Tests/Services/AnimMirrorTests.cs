using System.Buffers.Binary;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The anim mirror (puppet_anmmirror.c): reading the local Link's body tracks, face and hands, the
/// block entry the REL reads, publishing it, and the server-side validation of the new fields.
/// </summary>
public class AnimMirrorTests
{
    private const uint Link = 0x80400000;
    private const uint Block = 0x81234560;
    private const uint UnderAnm = 0x81000000;
    private const uint UpperAnm = 0x81000100;

    private static readonly byte[] BootTime = [0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC];

    private static void SetU16(FakeDolphin game, uint addr, ushort v) => game.Set(addr, (byte)(v >> 8), (byte)v);

    private static void SetF32(FakeDolphin game, uint addr, float v) =>
        game.SetU32(addr, BitConverter.SingleToUInt32Bits(v));

    private static void SetTrack(FakeDolphin game, bool upper, int i, uint anm, float ratio, ushort bck,
                                 short start, short end, short loop, byte attr, float frame, float rate)
    {
        uint pack = Link + (uint)((upper ? PuppetLayout.DAPY_OFF_ANM_RATIO_UPPER0 : PuppetLayout.DAPY_OFF_ANM_RATIO_UNDER0)
                                  + i * PuppetLayout.DAPY_ANM_RATIO_SIZE);
        uint heap = Link + (uint)((upper ? PuppetLayout.DAPY_OFF_ANM_HEAP_UPPER0 : PuppetLayout.DAPY_OFF_ANM_HEAP_UNDER0)
                                  + i * PuppetLayout.DAPY_ANM_HEAP_SIZE);
        uint fc = Link + (uint)((upper ? PuppetLayout.DAPY_OFF_FRAME_CTRL_UPPER0 : PuppetLayout.DAPY_OFF_FRAME_CTRL_UNDER0)
                                + i * PuppetLayout.DAPY_FRAME_CTRL_SIZE);
        SetF32(game, pack + PuppetLayout.ANM_RATIO_OFF_RATIO, ratio);
        game.SetU32(pack + PuppetLayout.ANM_RATIO_OFF_ANM, anm);
        SetU16(game, heap + PuppetLayout.ANM_HEAP_OFF_IDX, bck);
        game.Set(fc + PuppetLayout.FRAME_CTRL_OFF_ATTR, attr);
        SetU16(game, fc + PuppetLayout.FRAME_CTRL_OFF_START, (ushort)start);
        SetU16(game, fc + PuppetLayout.FRAME_CTRL_OFF_END, (ushort)end);
        SetU16(game, fc + PuppetLayout.FRAME_CTRL_OFF_LOOP, (ushort)loop);
        SetF32(game, fc + PuppetLayout.FRAME_CTRL_OFF_RATE, rate);
        SetF32(game, fc + PuppetLayout.FRAME_CTRL_OFF_FRAME, frame);
    }

    private static void SetTexHeap(FakeDolphin game, int offset, ushort idx, ushort pri, ushort demo)
    {
        uint heap = Link + (uint)offset;
        SetU16(game, heap + PuppetLayout.ANM_HEAP_OFF_IDX, idx);
        SetU16(game, heap + PuppetLayout.ANM_HEAP_OFF_PRI_IDX, pri);
        SetU16(game, heap + PuppetLayout.ANM_HEAP_OFF_DEMO_IDX, demo);
    }

    /// <summary>A Link mid side hop: one bck on under[0], upper[0] on it, a separate upper[1], nothing on under[1].</summary>
    private static FakeDolphin LinkInASideHop()
    {
        var game = new FakeDolphin();
        SetTrack(game, false, 0, UnderAnm, 1.0f, 0x0100, 0, 30, 0, 0, 12.25f, 1.5f);
        SetTrack(game, false, 1, 0, 0.0f, 0x0055, 0, 0, 0, 2, 0f, 0f);         // stale mIdx, no anm
        SetTrack(game, true, 0, UnderAnm, 1.0f, 0x0077, 0, 30, 0, 0, 12.25f, 1.5f); // stale mIdx, plays under[0]
        SetTrack(game, true, 1, UpperAnm, 0.25f, 0x0120, 2, 20, 4, 2, 7.5f, -1.0f);
        SetTexHeap(game, PuppetLayout.DAPY_OFF_TEX_ANM_HEAP, 0x1E0, 0xFFFF, 0xFFFF);
        SetTexHeap(game, PuppetLayout.DAPY_OFF_TEX_SCROLL_HEAP, 0x160, 0x165, 0xFFFF);
        game.Set(Link + PuppetLayout.DAPY_OFF_HAND_IDX, 3, 7);
        SetU16(game, Link + PuppetLayout.DAPY_OFF_TEX_FRAMES, 5);
        SetU16(game, Link + PuppetLayout.DAPY_OFF_TEX_FRAMES + 2, 9);
        return game;
    }

    [Fact]
    public void ReadLocal_ReportsEachTrack_AndWhichUpperTracksPlayTheirUnderTrack()
    {
        var anim = new AnimationState();
        AnimMirror.ReadLocal(LinkInASideHop(), Link, anim);

        Assert.NotNull(anim.Tracks);
        Assert.Equal(AnimationState.TrackCount, anim.Tracks!.Length);

        var under0 = anim.Tracks[0];
        Assert.Equal((ushort)0x0100, under0.Bck);
        Assert.Equal(((short)0, (short)30, (short)0, (byte)0), (under0.Start, under0.End, under0.Loop, under0.Attribute));
        Assert.Equal((12.25f, 1.5f, 1.0f), (under0.Frame, under0.Rate, under0.Ratio));

        Assert.Equal(AnimTrack.None, anim.Tracks[1].Bck); // no anm in the pack: its mIdx is stale
        Assert.Equal(AnimTrack.Same, anim.Tracks[2].Bck); // the pack holds under[0]'s anm

        var upper1 = anim.Tracks[3];
        Assert.Equal((ushort)0x0120, upper1.Bck);
        Assert.Equal(((short)2, (short)20, (short)4, (byte)2), (upper1.Start, upper1.End, upper1.Loop, upper1.Attribute));
        Assert.Equal((7.5f, -1.0f, 0.25f), (upper1.Frame, upper1.Rate, upper1.Ratio));
    }

    [Fact]
    public void ReadLocal_ReportsFaceAndHands_PriorityTextureFirst()
    {
        var anim = new AnimationState();
        AnimMirror.ReadLocal(LinkInASideHop(), Link, anim);

        Assert.Equal((ushort)0x1E0, anim.FaceBtp); // mIdx: no priority texture
        Assert.Equal((ushort)0x165, anim.FaceBtk); // field_0x2 wins over mIdx
        Assert.Equal((ushort)5, anim.FaceBtpFrame);
        Assert.Equal((ushort)9, anim.FaceBtkFrame);
        Assert.Equal(((byte)3, (byte)7), (anim.HandLeft, anim.HandRight));
    }

    [Fact]
    public void ReadLocal_CutsceneAnims_AreNotMirrored()
    {
        var game = LinkInASideHop();
        // A cutscene bck (dProcTool_init + setDemoData load it with mIdx 0xFFFF) and a demo face.
        SetU16(game, Link + PuppetLayout.DAPY_OFF_ANM_HEAP_UNDER0 + PuppetLayout.ANM_HEAP_OFF_IDX, 0xFFFF);
        SetTexHeap(game, PuppetLayout.DAPY_OFF_TEX_ANM_HEAP, 0x1E0, 0xFFFF, 0x0003);

        var anim = new AnimationState();
        AnimMirror.ReadLocal(game, Link, anim);

        Assert.Equal(AnimTrack.None, anim.Tracks![0].Bck);
        Assert.Equal(AnimTrack.None, anim.FaceBtp);
    }

    [Fact]
    public void Encode_PutsEveryFieldAtItsLayoutOffset_BigEndian()
    {
        var anim = new AnimationState();
        AnimMirror.ReadLocal(LinkInASideHop(), Link, anim);

        byte[] e = AnimMirror.Encode(anim, 0x20, 7);

        Assert.Equal(PuppetLayout.PUPPET_ANM_SLOT_SIZE, e.Length);
        Assert.Equal(7u, BinaryPrimitives.ReadUInt32BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_SEQ)));
        Assert.Equal(0x20, e[PuppetLayout.PUPPET_ANM_OFF_PROC]);
        Assert.Equal(((byte)3, (byte)7), (e[PuppetLayout.PUPPET_ANM_OFF_HAND_L], e[PuppetLayout.PUPPET_ANM_OFF_HAND_R]));
        Assert.Equal(0x1E0, BinaryPrimitives.ReadUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTP)));
        Assert.Equal(0x165, BinaryPrimitives.ReadUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTK)));
        Assert.Equal(5, BinaryPrimitives.ReadUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTP_FRAME)));
        Assert.Equal(9, BinaryPrimitives.ReadUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTK_FRAME)));

        Span<byte> Track(int t) => e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TRACK0 + t * PuppetLayout.PUPPET_ANM_TRACK_SIZE,
                                            PuppetLayout.PUPPET_ANM_TRACK_SIZE);
        Assert.Equal(0x0100, BinaryPrimitives.ReadUInt16BigEndian(Track(0)[PuppetLayout.PUPPET_ANM_TR_OFF_BCK..]));
        Assert.Equal(12.25f, BinaryPrimitives.ReadSingleBigEndian(Track(0)[PuppetLayout.PUPPET_ANM_TR_OFF_FRAME..]));
        Assert.Equal(1.5f, BinaryPrimitives.ReadSingleBigEndian(Track(0)[PuppetLayout.PUPPET_ANM_TR_OFF_RATE..]));
        Assert.Equal(AnimTrack.None, BinaryPrimitives.ReadUInt16BigEndian(Track(1)[PuppetLayout.PUPPET_ANM_TR_OFF_BCK..]));
        Assert.Equal(AnimTrack.Same, BinaryPrimitives.ReadUInt16BigEndian(Track(2)[PuppetLayout.PUPPET_ANM_TR_OFF_BCK..]));

        var upper1 = Track(3);
        Assert.Equal(0x0120, BinaryPrimitives.ReadUInt16BigEndian(upper1[PuppetLayout.PUPPET_ANM_TR_OFF_BCK..]));
        Assert.Equal(2, BinaryPrimitives.ReadInt16BigEndian(upper1[PuppetLayout.PUPPET_ANM_TR_OFF_START..]));
        Assert.Equal(20, BinaryPrimitives.ReadInt16BigEndian(upper1[PuppetLayout.PUPPET_ANM_TR_OFF_END..]));
        Assert.Equal(4, BinaryPrimitives.ReadInt16BigEndian(upper1[PuppetLayout.PUPPET_ANM_TR_OFF_LOOP..]));
        Assert.Equal(2, upper1[PuppetLayout.PUPPET_ANM_TR_OFF_ATTR]);
        Assert.Equal(-1.0f, BinaryPrimitives.ReadSingleBigEndian(upper1[PuppetLayout.PUPPET_ANM_TR_OFF_RATE..]));
        Assert.Equal(0.25f, BinaryPrimitives.ReadSingleBigEndian(upper1[PuppetLayout.PUPPET_ANM_TR_OFF_RATIO..]));
    }

    [Fact]
    public void Encode_WithoutTracks_IsNoData()
    {
        Assert.All(AnimMirror.Encode(new AnimationState(), 0x20, 7), b => Assert.Equal(0, b));
        Assert.All(AnimMirror.Encode(null, 0x20, 7), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Encode_MissingTracks_AreNoneBelowAndSameAbove()
    {
        var anim = new AnimationState { Tracks = [new AnimTrack { Bck = 0x0100 }] };
        byte[] e = AnimMirror.Encode(anim, 0x20, 1);

        ushort Bck(int t) => BinaryPrimitives.ReadUInt16BigEndian(
            e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TRACK0 + t * PuppetLayout.PUPPET_ANM_TRACK_SIZE + PuppetLayout.PUPPET_ANM_TR_OFF_BCK));
        Assert.Equal(new ushort[] { 0x0100, AnimTrack.None, AnimTrack.Same, AnimTrack.Same }, new[] { Bck(0), Bck(1), Bck(2), Bck(3) });
    }

    private static FakeDolphin GameWithBlock()
    {
        var game = new FakeDolphin();
        game.Set(GameMemoryAddresses.System.OSStartTime.Address, BootTime);
        game.SetU32(PuppetLayout.PUPPET_ANM_PTR_ADDR, Block);
        game.SetU32(Block + PuppetLayout.PUPPET_ANM_OFF_MAGIC, PuppetLayout.PUPPET_ANM_MAGIC);
        game.Set(Block + PuppetLayout.PUPPET_ANM_OFF_BOOT, BootTime);
        return game;
    }

    private static uint EntryAddress(int slot) =>
        Block + (uint)(PuppetLayout.PUPPET_ANM_OFF_SLOT0 + slot * PuppetLayout.PUPPET_ANM_SLOT_SIZE);

    [Fact]
    public void Publish_WritesEachSlotsEntry_AndOnlyWhatChanged()
    {
        var game = GameWithBlock();
        var entry = AnimMirror.Encode(new AnimationState { Tracks = [new AnimTrack { Bck = 0x0100, Frame = 3f }] }, 0x20, 1);

        Assert.Equal(Block, AnimMirror.Publish(game, [null, entry, null]));
        Assert.Equal(entry, game.ReadMemory(EntryAddress(1), PuppetLayout.PUPPET_ANM_SLOT_SIZE));
        Assert.Equal(0u, game.GetU32(EntryAddress(0)));

        int writes = 0;
        game.BeforeWrite = _ => writes++;
        AnimMirror.Publish(game, [null, entry, null]);
        Assert.Equal(0, writes);

        // A slot that goes empty is cleared.
        AnimMirror.Publish(game, [null, null, null]);
        Assert.Equal(1, writes);
        Assert.All(game.ReadMemory(EntryAddress(1), PuppetLayout.PUPPET_ANM_SLOT_SIZE)!, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Publish_WithoutThisBootsBlock_WritesNothing()
    {
        var game = GameWithBlock();
        game.Set(GameMemoryAddresses.System.OSStartTime.Address, [0x00, 0x00, 0x12, 0x35, 0x00, 0x00, 0x00, 0x01]);
        int writes = 0;
        game.BeforeWrite = _ => writes++;

        Assert.Null(AnimMirror.Publish(game, [AnimMirror.Encode(new AnimationState { Tracks = [new AnimTrack()] }, 0x20, 1)]));
        Assert.Equal(0, writes);
    }

    [Fact]
    public void Validation_RejectsNonFiniteOrTooManyTracks()
    {
        Assert.True(new AnimationState().IsValid());
        Assert.True(new AnimationState { Tracks = [new AnimTrack(), new AnimTrack()] }.IsValid());
        Assert.False(new AnimationState { Tracks = [new AnimTrack { Frame = float.NaN }] }.IsValid());
        Assert.False(new AnimationState { Tracks = [new AnimTrack { Rate = float.PositiveInfinity }] }.IsValid());
        Assert.False(new AnimationState { Tracks = [new AnimTrack { Ratio = float.NaN }] }.IsValid());
        Assert.False(new AnimationState { Tracks = [new AnimTrack { Rate = AnimTrack.MaxRate * 2 }] }.IsValid());
        Assert.False(new AnimationState { Tracks = [null!] }.IsValid());
        Assert.False(new AnimationState { Tracks = new AnimTrack[AnimationState.TrackCount + 1].Select(_ => new AnimTrack()).ToArray() }.IsValid());

        var puppet = new PuppetData { Animation = new AnimationState { Tracks = [new AnimTrack { Frame = float.NaN }] } };
        Assert.False(puppet.IsValid());
    }
}
