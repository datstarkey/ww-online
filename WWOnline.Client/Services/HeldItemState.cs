using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// The local Link's held-item state as the puppet REL wants it (GameMod/src/puppet_link/puppet_held.c):
/// the item in hand, what Link carries, and the aim angles. Offsets and values come from puppet_shared.h.
/// </summary>
public static class HeldItemState
{
    /// <summary>
    /// The draw / put-away upper anims (REST, TAKE, TAKEBOTH, TAKEL, TAKER; daPy_lk_c::checkEquipAnime,
    /// d_a_player_main.cpp:3571). While one plays, m3562 holds the item Link will hold after its swap frame.
    /// </summary>
    public static bool IsEquipAnim(ushort upperAnm) =>
        upperAnm == PuppetLayout.DAPY_UPPER_ANM_REST ||
        (upperAnm >= PuppetLayout.DAPY_UPPER_ANM_TAKE && upperAnm <= PuppetLayout.DAPY_UPPER_ANM_TAKER);

    /// <summary>
    /// The item to report: during a draw / put-away anim the item Link WILL hold (m3562), so the
    /// puppet starts the same anim at the same time instead of a whole anim later; else mEquipItem.
    /// </summary>
    public static ushort ReportedItem(ushort equipItem, ushort nextEquipItem, ushort upperAnm) =>
        IsEquipAnim(upperAnm) ? nextEquipItem : equipItem;

    /// <summary>
    /// What Link carries (mActorKeepGrab's actor, d_a_player_main.h:2088): a bomb, or nothing the puppet
    /// draws. The actor may be deleted under us between reads, so the pointer must be in MEM1 and the
    /// worst a stale read does is show or hide a bomb for one tick.
    /// </summary>
    public static byte ReadGrabKind(IDolphinService dolphin, uint linkPtr)
    {
        byte[]? p = dolphin.ReadMemory(linkPtr + PuppetLayout.DAPY_OFF_GRAB_ACTOR, 4);
        if (p is not { Length: 4 })
            return PuppetLayout.PUPPET_GRAB_KIND_NONE;
        uint actor = (uint)(p[0] << 24 | p[1] << 16 | p[2] << 8 | p[3]);
        if (actor < 0x80000000 || actor >= PuppetNameTags.Mem1End || actor % 4 != 0)
            return PuppetLayout.PUPPET_GRAB_KIND_NONE;
        byte[]? name = dolphin.ReadMemory(actor + PuppetLayout.FPC_OFF_PROC_NAME, 2);
        return name is { Length: 2 } && (name[0] << 8 | name[1]) == PuppetLayout.FPC_NAME_BOMB
            ? (byte)PuppetLayout.PUPPET_GRAB_KIND_BOMB
            : (byte)PuppetLayout.PUPPET_GRAB_KIND_NONE;
    }

    /// <summary>
    /// The fuse of the bomb Link carries (daBomb_c::mRestTime of mActorKeepGrab's actor): frames left,
    /// capped at 255 for the slot's byte; 0 when he carries no bomb (or it just exploded). The receiver's
    /// REL plays the carried bomb's flash and swell from it, as the real bomb's draw_norm does.
    /// </summary>
    public static byte ReadGrabFuse(IDolphinService dolphin, uint linkPtr)
    {
        if (ReadGrabKind(dolphin, linkPtr) != PuppetLayout.PUPPET_GRAB_KIND_BOMB)
            return 0;
        byte[]? p = dolphin.ReadMemory(linkPtr + PuppetLayout.DAPY_OFF_GRAB_ACTOR, 4);
        if (p is not { Length: 4 })
            return 0;
        uint actor = (uint)(p[0] << 24 | p[1] << 16 | p[2] << 8 | p[3]);
        byte[]? rest = dolphin.ReadMemory(actor + PuppetLayout.DABOMB_OFF_REST_TIME, 2);
        if (rest is not { Length: 2 })
            return 0;
        return FuseByte((short)(rest[0] << 8 | rest[1]));
    }

    /// <summary>A fuse (mRestTime) as the slot byte: 1..255 frames, 0 for none.</summary>
    public static byte FuseByte(short restTime) => (byte)Math.Clamp((int)restTime, 0, byte.MaxValue);

    /// <summary>Only grab kinds the REL knows reach the slot (the server clamps others to 0 too).</summary>
    public static byte SanitizeGrabKind(byte kind) =>
        kind <= PuppetLayout.PUPPET_GRAB_KIND_MAX ? kind : (byte)PuppetLayout.PUPPET_GRAB_KIND_NONE;
}
