using System.Buffers.Binary;
using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The anim mirror (GameMod/src/puppet_link/puppet_anmmirror.c): in every proc its REL has no init
/// for, a puppet plays its player's own body anims, face and hand shapes. This reads them from the
/// local Link for broadcasting, and writes other players' into the REL's anim mirror block, a
/// boot-stamped game-heap block like the names block (<see cref="BootStampedBlock"/>; layout in
/// puppet_shared.h PUPPET_ANM_*).
/// </summary>
public static class AnimMirror
{
    // One read covers the ratio packs, anm heaps and frame controls (d_a_player_main.h:2077-2082).
    private const int BodyStart = PuppetLayout.DAPY_OFF_ANM_RATIO_UNDER0;
    private const int BodyLength = PuppetLayout.DAPY_OFF_FRAME_CTRL_UPPER0 + 3 * PuppetLayout.DAPY_FRAME_CTRL_SIZE - BodyStart;
    private const int TexHeapsLength = PuppetLayout.DAPY_OFF_TEX_SCROLL_HEAP + PuppetLayout.DAPY_ANM_HEAP_SIZE - PuppetLayout.DAPY_OFF_TEX_ANM_HEAP;

    /// <summary>
    /// The local Link's body tracks, face and hands into <paramref name="anim"/>. A track whose ratio pack
    /// holds no anim is <see cref="AnimTrack.None"/>, an upper track playing its under track's anim is
    /// <see cref="AnimTrack.Same"/>, and a cutscene bck (its heap's mIdx is 0xFFFF, d_a_player_dproc.inc:319)
    /// is None too: the receiver can only load LkAnm bcks. Leaves <paramref name="anim"/> alone if Link
    /// can't be read.
    /// </summary>
    public static void ReadLocal(IDolphinService dolphin, uint link, AnimationState anim)
    {
        byte[]? body = dolphin.ReadMemory(link + BodyStart, BodyLength);
        byte[]? tex = dolphin.ReadMemory(link + PuppetLayout.DAPY_OFF_TEX_ANM_HEAP, TexHeapsLength);
        byte[]? hands = dolphin.ReadMemory(link + PuppetLayout.DAPY_OFF_HAND_IDX, 2);
        byte[]? texFrames = dolphin.ReadMemory(link + PuppetLayout.DAPY_OFF_TEX_FRAMES, 4);
        if (body is not { Length: BodyLength } || tex is not { Length: TexHeapsLength } ||
            hands is not { Length: 2 } || texFrames is not { Length: 4 })
            return;

        var tracks = new AnimTrack[AnimationState.TrackCount];
        for (int t = 0; t < tracks.Length; t++)
        {
            bool upper = t >= 2;
            int i = upper ? t - 2 : t;
            int pack = (upper ? PuppetLayout.DAPY_OFF_ANM_RATIO_UPPER0 : PuppetLayout.DAPY_OFF_ANM_RATIO_UNDER0)
                       + i * PuppetLayout.DAPY_ANM_RATIO_SIZE - BodyStart;
            int heap = (upper ? PuppetLayout.DAPY_OFF_ANM_HEAP_UPPER0 : PuppetLayout.DAPY_OFF_ANM_HEAP_UNDER0)
                       + i * PuppetLayout.DAPY_ANM_HEAP_SIZE - BodyStart;
            int fc = (upper ? PuppetLayout.DAPY_OFF_FRAME_CTRL_UPPER0 : PuppetLayout.DAPY_OFF_FRAME_CTRL_UNDER0)
                     + i * PuppetLayout.DAPY_FRAME_CTRL_SIZE - BodyStart;
            uint anm = U32(body, pack + PuppetLayout.ANM_RATIO_OFF_ANM);
            uint underAnm = U32(body, PuppetLayout.DAPY_OFF_ANM_RATIO_UNDER0 + i * PuppetLayout.DAPY_ANM_RATIO_SIZE
                                      - BodyStart + PuppetLayout.ANM_RATIO_OFF_ANM);

            ushort bck = anm == 0 ? AnimTrack.None
                : upper && anm == underAnm ? AnimTrack.Same
                : U16(body, heap + PuppetLayout.ANM_HEAP_OFF_IDX);
            tracks[t] = new AnimTrack
            {
                Bck = bck,
                Start = (short)U16(body, fc + PuppetLayout.FRAME_CTRL_OFF_START),
                End = (short)U16(body, fc + PuppetLayout.FRAME_CTRL_OFF_END),
                Loop = (short)U16(body, fc + PuppetLayout.FRAME_CTRL_OFF_LOOP),
                Attribute = body[fc + PuppetLayout.FRAME_CTRL_OFF_ATTR],
                Frame = Finite(F32(body, fc + PuppetLayout.FRAME_CTRL_OFF_FRAME)),
                Rate = Finite(F32(body, fc + PuppetLayout.FRAME_CTRL_OFF_RATE)),
                Ratio = Finite(F32(body, pack + PuppetLayout.ANM_RATIO_OFF_RATIO)),
            };
        }

        anim.Tracks = tracks;
        anim.FaceBtp = TextureAnim(tex, 0);
        anim.FaceBtk = TextureAnim(tex, PuppetLayout.DAPY_OFF_TEX_SCROLL_HEAP - PuppetLayout.DAPY_OFF_TEX_ANM_HEAP);
        anim.FaceBtpFrame = U16(texFrames, 0);
        anim.FaceBtkFrame = U16(texFrames, 2);
        anim.HandLeft = hands[0];
        anim.HandRight = hands[1];
    }

    /// <summary>
    /// The texture anim a face heap shows: a cutscene one (field_0x4, from the LkD00 archive) can't be
    /// mirrored; else the priority one (field_0x2, setPriTextureAnime), else mIdx (setTextureAnime,
    /// d_a_player_main.cpp:760, 863).
    /// </summary>
    private static ushort TextureAnim(byte[] tex, int heap)
    {
        if (U16(tex, heap + PuppetLayout.ANM_HEAP_OFF_DEMO_IDX) != AnimTrack.None)
            return AnimTrack.None;
        ushort pri = U16(tex, heap + PuppetLayout.ANM_HEAP_OFF_PRI_IDX);
        return pri != AnimTrack.None ? pri : U16(tex, heap + PuppetLayout.ANM_HEAP_OFF_IDX);
    }

    /// <summary>
    /// A slot's PUPPET_ANM_SLOT_SIZE entry for <paramref name="puppet"/> sampled with <paramref name="proc"/>
    /// (the mCurProc the slot carries), or all zeroes (SEQ 0 = no data) when it has no tracks.
    /// <paramref name="seq"/> must change exactly when the sample does: the REL snaps frames on new samples only.
    /// </summary>
    public static byte[] Encode(AnimationState? anim, byte proc, uint seq)
    {
        var e = new byte[PuppetLayout.PUPPET_ANM_SLOT_SIZE];
        if (anim?.Tracks is not { Length: > 0 } tracks || seq == 0)
            return e;

        BinaryPrimitives.WriteUInt32BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_SEQ), seq);
        e[PuppetLayout.PUPPET_ANM_OFF_PROC] = proc;
        e[PuppetLayout.PUPPET_ANM_OFF_HAND_L] = anim.HandLeft;
        e[PuppetLayout.PUPPET_ANM_OFF_HAND_R] = anim.HandRight;
        BinaryPrimitives.WriteUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTP), anim.FaceBtp);
        BinaryPrimitives.WriteUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTK), anim.FaceBtk);
        BinaryPrimitives.WriteUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTP_FRAME), anim.FaceBtpFrame);
        BinaryPrimitives.WriteUInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TEX_BTK_FRAME), anim.FaceBtkFrame);
        for (int t = 0; t < PuppetLayout.PUPPET_ANM_TRACKS; t++)
        {
            var span = e.AsSpan(PuppetLayout.PUPPET_ANM_OFF_TRACK0 + t * PuppetLayout.PUPPET_ANM_TRACK_SIZE,
                                PuppetLayout.PUPPET_ANM_TRACK_SIZE);
            AnimTrack? tr = t < tracks.Length ? tracks[t] : null;
            if (tr == null)
            {
                BinaryPrimitives.WriteUInt16BigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_BCK..],
                    t < 2 ? AnimTrack.None : AnimTrack.Same);
                continue;
            }
            BinaryPrimitives.WriteUInt16BigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_BCK..], tr.Bck);
            BinaryPrimitives.WriteInt16BigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_START..], tr.Start);
            BinaryPrimitives.WriteInt16BigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_END..], tr.End);
            BinaryPrimitives.WriteInt16BigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_LOOP..], tr.Loop);
            span[PuppetLayout.PUPPET_ANM_TR_OFF_ATTR] = tr.Attribute;
            BinaryPrimitives.WriteSingleBigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_FRAME..], Finite(tr.Frame));
            BinaryPrimitives.WriteSingleBigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_RATE..], Finite(tr.Rate));
            BinaryPrimitives.WriteSingleBigEndian(span[PuppetLayout.PUPPET_ANM_TR_OFF_RATIO..], Finite(tr.Ratio));
        }
        return e;
    }

    /// <summary>
    /// Write each slot's entry (null = no data) into the anim mirror block, only where it differs. Returns
    /// the block's address, or null when there is no usable block (no puppet created since boot, a bad
    /// pointer / magic, or a block from before a reboot): then nothing is written.
    /// </summary>
    public static uint? Publish(IDolphinService dolphin, IReadOnlyList<byte[]?> entries)
    {
        if (BootStampedBlock.Read(dolphin, PuppetLayout.PUPPET_ANM_PTR_ADDR, PuppetLayout.PUPPET_ANM_MAGIC,
                PuppetLayout.PUPPET_ANM_BLOCK_SIZE) is not { } found)
            return null;
        var (block, current) = found;

        var empty = new byte[PuppetLayout.PUPPET_ANM_SLOT_SIZE];
        for (int i = 0; i < PuppetLayout.PUPPET_MAX_SLOTS; i++)
        {
            byte[] want = i < entries.Count && entries[i] is { Length: PuppetLayout.PUPPET_ANM_SLOT_SIZE } e ? e : empty;
            int offset = PuppetLayout.PUPPET_ANM_OFF_SLOT0 + i * PuppetLayout.PUPPET_ANM_SLOT_SIZE;
            if (current.AsSpan(offset, PuppetLayout.PUPPET_ANM_SLOT_SIZE).SequenceEqual(want))
                continue;
            dolphin.WriteMemory(block + (uint)offset, want);
        }
        return block;
    }

    private static float Finite(float f) => float.IsFinite(f) ? f : 0f;
    private static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));
    private static ushort U16(byte[] b, int o) => BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o));
    private static float F32(byte[] b, int o) => BinaryPrimitives.ReadSingleBigEndian(b.AsSpan(o));
}
