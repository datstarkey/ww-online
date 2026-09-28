using System.Numerics;

namespace WWOnline.Shared.Models;

/// <summary>
/// The room's shared story progress: the single-bit event flags (bytes 0x00-0x41 of dSv_event_c,
/// see <see cref="EventFlagCatalog"/>), owned by the server while the SharedStory room rule is on.
/// Grow-only: players' flags are OR-merged, and only bits in <see cref="SyncMask"/> (the catalog's
/// Full-sync mask: everything except LocalOnly and risky Unknown) are ever stored, sent or applied.
/// Event registers (0x79-0xFF) and mTmp are never part of this.
/// </summary>
public class StoryFlags
{
    public const int ByteCount = EventFlagCatalog.BitFlagBytes;

    private static readonly byte[] Mask = EventFlagCatalog.GetFullSyncMask()[..ByteCount];

    /// <summary>Per-byte mask of the bits story sync may touch.</summary>
    public static ReadOnlySpan<byte> SyncMask => Mask;

    /// <summary>mEvent bytes 0x00-0x41, masked to <see cref="SyncMask"/>.</summary>
    public byte[] Bits { get; set; } = new byte[ByteCount];

    /// <summary>The syncable bits of a raw event array read from the game (at least <see cref="ByteCount"/> bytes).</summary>
    public static StoryFlags FromEventBytes(ReadOnlySpan<byte> eventBytes)
    {
        var f = new StoryFlags();
        for (int i = 0; i < ByteCount; i++) f.Bits[i] = (byte)(eventBytes[i] & Mask[i]);
        return f;
    }

    /// <summary>Structure check for anything from the network (call <see cref="Normalize"/> after).</summary>
    public bool IsValid() => Bits is { Length: ByteCount };

    /// <summary>Drop every bit outside <see cref="SyncMask"/>. Returns this.</summary>
    public StoryFlags Normalize()
    {
        for (int i = 0; i < ByteCount; i++) Bits[i] &= Mask[i];
        return this;
    }

    public bool IsEmpty => Bits.All(b => b == 0);

    public int BitCount => Bits.Sum(b => BitOperations.PopCount(b));

    /// <summary>OR the syncable bits of <paramref name="other"/> into this. Returns true if any bit was added.</summary>
    public bool MergeFrom(StoryFlags other)
    {
        bool changed = false;
        for (int i = 0; i < ByteCount; i++)
        {
            byte r = (byte)(Bits[i] | (other.Bits[i] & Mask[i]));
            if (r != Bits[i]) changed = true;
            Bits[i] = r;
        }
        return changed;
    }

    /// <summary>Bits set in this but not in <paramref name="other"/>.</summary>
    public StoryFlags Except(StoryFlags other)
    {
        var d = new StoryFlags();
        for (int i = 0; i < ByteCount; i++) d.Bits[i] = (byte)(Bits[i] & ~other.Bits[i]);
        return d;
    }

    public StoryFlags Clone() => new() { Bits = (byte[])Bits.Clone() };

    public bool Has(ushort id) => (id >> 8) < ByteCount && (Bits[id >> 8] & (id & 0xFF)) != 0;

    /// <summary>The catalog entries of every set bit, in array order.</summary>
    public IEnumerable<EventFlagInfo> SetFlags() => EventFlagCatalog.Flags.Where(f => Has(f.Id));

    /// <summary>The set flags whose side effects are known to be risky (see <see cref="EventFlagCatalog.RiskEffects"/>).</summary>
    public IEnumerable<EventFlagInfo> RiskyFlags() => SetFlags().Where(f => f.Risky);

    /// <summary>
    /// Names of the set flags for log lines, e.g. "MET_KORL, UNK_0F40". Long lists (a joiner
    /// bringing a whole save) are cut off after <paramref name="max"/> names with "+N more".
    /// </summary>
    public string Describe(int max = 40)
    {
        var names = SetFlags().Select(f => f.Name).ToList();
        if (names.Count == 0) return "(none)";
        return names.Count <= max
            ? string.Join(", ", names)
            : string.Join(", ", names.Take(max)) + $", +{names.Count - max} more";
    }

    public override string ToString() => $"{BitCount} flag(s): {Describe()}";
}
