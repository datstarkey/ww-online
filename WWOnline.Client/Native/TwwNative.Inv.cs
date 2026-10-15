using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WWOnline.Native;

// tww.h "Shared inventory (fan-out: inv)": play's pending HUD counters and the facts the inventory sync reads.
public static unsafe partial class TwwNative
{
    [LibraryImport(LibraryName)] public static partial void tww_inv_get(TwwInvPlay* play);
    [LibraryImport(LibraryName)] public static partial void tww_inv_add(TwwInvCounter counter, int delta);
    [LibraryImport(LibraryName)] public static partial void tww_inv_set_select_item(uint button, byte itemNo);
}

/// <summary>The shared-inventory calls (tww.h, fan-out: inv). Game thread only, like <see cref="ITwwApi"/>.</summary>
public interface ITwwInvApi
{
    TwwInvPlay InvGet();
    /// <summary>+= <paramref name="delta"/>, as dComIfGp_setItem*Count does: d_meter applies it next frame.</summary>
    void InvAdd(TwwInvCounter counter, int delta);
    /// <summary>play mSelectItem[<paramref name="button"/>] (button &lt; 4).</summary>
    void InvSetSelectItem(int button, byte itemNo);
}

/// <summary>TWW_INV_*: play's changes queued for the HUD.</summary>
public enum TwwInvCounter : uint
{
    Rupee = 0,
    Key = 1,
    MaxLife = 2,
    MaxMagic = 3,
    Arrow = 4,
    Bomb = 5,
    /// <summary>mItemBeastNumCounts[8], dBeastIndex_e order: Beast + i.</summary>
    Beast = 6,
}

[InlineArray(TwwInvPlay.CounterCount)]
public struct TwwInvCounters
{
    private int _element;
}

[InlineArray(4)]
public struct TwwByte4
{
    private byte _element;
}

/// <summary>TwwInvPlay (64 bytes).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwInvPlay
{
    /// <summary>TWW_INV_COUNTERS.</summary>
    public const int CounterCount = (int)TwwInvCounter.Beast + 8;

    /// <summary>Indexed by <see cref="TwwInvCounter"/>: changes queued, not yet applied.</summary>
    public TwwInvCounters Pending;
    /// <summary>play mSelectItem: the item number X, Y, Z (and the 4th) show.</summary>
    public TwwByte4 SelectItem;
    /// <summary>The current stage's save slot, -1 if not in a stage.</summary>
    public sbyte SaveTbl;
    /// <summary>The current stage shows the small-key HUD.</summary>
    public byte KeyDisp;
    public short Pad0;
}
