using System.Numerics;
using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// The room's shared story progress, owned by the server while the SharedStory room rule is on:
/// <list type="bullet">
/// <item>the single-bit event flags (bytes 0x00-0x41 of dSv_event_c, see <see cref="EventFlagCatalog"/>),
/// only the bits in <see cref="SyncMask"/> (the catalog's Full-sync mask: everything except LocalOnly and
/// risky Unknown);</item>
/// <item>the Nintendo Gallery figurines Carlov has made (<see cref="Figurines"/>): the 17 event registers
/// the game uses as one bitfield (docs/figurines.md).</item>
/// </list>
/// Grow-only: players' bits are OR-merged, and only syncable bits are ever stored, sent or applied.
/// Every other event register (0x79-0xFF) and mTmp are never part of this.
/// </summary>
public class StoryFlags
{
    public const int ByteCount = EventFlagCatalog.BitFlagBytes;

    /// <summary>TOTAL_FIGURE_COUNT: figurines 0x00-0x85 (d_a_npc_mt.cpp:14, d_a_obj_figure.cpp:23).</summary>
    public const int TotalFigurines = 0x86;

    /// <summary>One byte per 8 figurines (l_figure_comp has 17 entries).</summary>
    public const int FigurineByteCount = 17;

    /// <summary>
    /// The event byte holding figurines 8i..8i+7, for i = 0..16: l_figure_comp (d_a_npc_mt.cpp:304,
    /// d_a_npc_mn.cpp:222, d_a_obj_figure.cpp:43; the same list is dSv_info_c::reinit's l_holdEventReg,
    /// d_save.cpp:1402, main.dol 0x80375DC8, kept on New Game+). Figurine n is bit (n % 8) of register
    /// FigurineRegisterBytes[n / 8], read with getEventReg(0xXXFF) and set by daNpcMt_c::setFigure.
    /// </summary>
    public static ReadOnlySpan<byte> FigurineRegisterBytes =>
        [0x95, 0x94, 0x93, 0x92, 0x91, 0x90, 0x8F, 0x8E, 0x8D, 0x8C, 0xB1, 0x9C, 0x84, 0x83, 0x82, 0x81, 0x80];

    private static readonly byte[] Mask = EventFlagCatalog.GetFullSyncMask()[..ByteCount];

    private static readonly byte[] FigMask = BuildFigurineMask();

    /// <summary>Per-byte mask of the bits story sync may touch.</summary>
    public static ReadOnlySpan<byte> SyncMask => Mask;

    /// <summary>Per-byte mask of <see cref="Figurines"/>: every real figurine, never bits 0x86-0x87.</summary>
    public static ReadOnlySpan<byte> FigurineMask => FigMask;

    /// <summary>mEvent bytes 0x00-0x41, masked to <see cref="SyncMask"/>.</summary>
    public byte[] Bits { get; set; } = new byte[ByteCount];

    /// <summary>
    /// The figurines made, in figurine order: figurine n is bit (n % 8) of Figurines[n / 8], whose event
    /// byte is <see cref="FigurineRegisterBytes"/>[n / 8]. Masked to <see cref="FigurineMask"/>.
    /// </summary>
    public byte[] Figurines { get; set; } = new byte[FigurineByteCount];

    private static byte[] BuildFigurineMask()
    {
        var m = new byte[FigurineByteCount];
        for (int n = 0; n < TotalFigurines; n++) m[n / 8] |= (byte)(1 << (n % 8));
        return m;
    }

    /// <summary>The syncable bits of the game's whole event array (<see cref="EventFlagCatalog.EventBitfieldSize"/> bytes).</summary>
    public static StoryFlags FromEventBytes(ReadOnlySpan<byte> eventBytes)
    {
        if (eventBytes.Length < EventFlagCatalog.EventBitfieldSize)
            throw new ArgumentException($"need the whole {EventFlagCatalog.EventBitfieldSize}-byte event array", nameof(eventBytes));
        var f = new StoryFlags();
        for (int i = 0; i < ByteCount; i++) f.Bits[i] = (byte)(eventBytes[i] & Mask[i]);
        for (int i = 0; i < FigurineByteCount; i++) f.Figurines[i] = (byte)(eventBytes[FigurineRegisterBytes[i]] & FigMask[i]);
        return f;
    }

    /// <summary>Structure check for anything from the network (call <see cref="Normalize"/> after).</summary>
    public bool IsValid() => Bits is { Length: ByteCount } && Figurines is { Length: FigurineByteCount };

    /// <summary>Drop every bit outside <see cref="SyncMask"/> / <see cref="FigurineMask"/>. Returns this.</summary>
    public StoryFlags Normalize()
    {
        for (int i = 0; i < ByteCount; i++) Bits[i] &= Mask[i];
        for (int i = 0; i < FigurineByteCount; i++) Figurines[i] &= FigMask[i];
        return this;
    }

    [JsonIgnore]
    public bool IsEmpty => Bits.All(b => b == 0) && Figurines.All(b => b == 0);

    /// <summary>Set event flags (figurines not included).</summary>
    [JsonIgnore]
    public int BitCount => Bits.Sum(b => BitOperations.PopCount(b));

    /// <summary>Figurines made.</summary>
    [JsonIgnore]
    public int FigurineCount => Figurines.Sum(b => BitOperations.PopCount(b));

    /// <summary>OR the syncable bits of <paramref name="other"/> into this. Returns true if any bit was added.</summary>
    public bool MergeFrom(StoryFlags other)
    {
        bool changed = false;
        for (int i = 0; i < ByteCount; i++)
            Bits[i] = Or(Bits[i], (byte)(other.Bits[i] & Mask[i]), ref changed);
        for (int i = 0; i < FigurineByteCount; i++)
            Figurines[i] = Or(Figurines[i], (byte)(other.Figurines[i] & FigMask[i]), ref changed);
        return changed;
    }

    private static byte Or(byte mine, byte theirs, ref bool changed)
    {
        byte r = (byte)(mine | theirs);
        if (r != mine) changed = true;
        return r;
    }

    /// <summary>Bits set in this but not in <paramref name="other"/>.</summary>
    public StoryFlags Except(StoryFlags other)
    {
        var d = new StoryFlags();
        for (int i = 0; i < ByteCount; i++) d.Bits[i] = (byte)(Bits[i] & ~other.Bits[i]);
        for (int i = 0; i < FigurineByteCount; i++) d.Figurines[i] = (byte)(Figurines[i] & ~other.Figurines[i]);
        return d;
    }

    /// <summary>A copy holding only the event flags (no figurines).</summary>
    public StoryFlags FlagsOnly() => new() { Bits = (byte[])Bits.Clone() };

    /// <summary>A copy holding only the figurines (no event flags).</summary>
    public StoryFlags FigurinesOnly() => new() { Figurines = (byte[])Figurines.Clone() };

    public StoryFlags Clone() => new() { Bits = (byte[])Bits.Clone(), Figurines = (byte[])Figurines.Clone() };

    public bool Has(ushort id) => (id >> 8) < ByteCount && (Bits[id >> 8] & (id & 0xFF)) != 0;

    /// <summary>True if figurine <paramref name="figure"/> (0x00-0x85, daNpcMt_c::isFigureGet's number) is made.</summary>
    public bool HasFigurine(int figure) =>
        figure is >= 0 and < TotalFigurines && (Figurines[figure / 8] & (1 << (figure % 8))) != 0;

    /// <summary>Mark figurine <paramref name="figure"/> made (what daNpcMt_c::setFigure does to the save). Returns this.</summary>
    public StoryFlags WithFigurine(int figure)
    {
        if (figure is < 0 or >= TotalFigurines) throw new ArgumentOutOfRangeException(nameof(figure));
        Figurines[figure / 8] |= (byte)(1 << (figure % 8));
        return this;
    }

    /// <summary>The numbers of every figurine made, ascending.</summary>
    public IEnumerable<int> MadeFigurines() => Enumerable.Range(0, TotalFigurines).Where(HasFigurine);

    /// <summary>The catalog entries of every set bit, in array order.</summary>
    public IEnumerable<EventFlagInfo> SetFlags() => EventFlagCatalog.Flags.Where(f => Has(f.Id));

    /// <summary>The set flags whose side effects are known to be risky (see <see cref="EventFlagCatalog.RiskEffects"/>).</summary>
    public IEnumerable<EventFlagInfo> RiskyFlags() => SetFlags().Where(f => f.Risky);

    /// <summary>"3 flag(s)", or "3 flag(s) + 2 figurine(s)" when figurines are set, for log lines.</summary>
    public string CountText() =>
        FigurineCount == 0 ? $"{BitCount} flag(s)" : $"{BitCount} flag(s) + {FigurineCount} figurine(s)";

    /// <summary>
    /// Names of the set flags for log lines, e.g. "MET_KORL, UNK_0F40", then the figurines made as
    /// "figurines 0x12, 0x40". Long lists (a joiner bringing a whole save) are cut off after
    /// <paramref name="max"/> entries each with "+N more".
    /// </summary>
    public string Describe(int max = 40)
    {
        var parts = new List<string>();
        var names = SetFlags().Select(f => f.Name).ToList();
        if (names.Count > 0) parts.Add(Truncate(names, max));
        var figs = MadeFigurines().Select(n => $"0x{n:X2}").ToList();
        if (figs.Count > 0) parts.Add("figurines " + Truncate(figs, max));
        return parts.Count == 0 ? "(none)" : string.Join(", ", parts);
    }

    private static string Truncate(List<string> items, int max) =>
        items.Count <= max
            ? string.Join(", ", items)
            : string.Join(", ", items.Take(max)) + $", +{items.Count - max} more";

    public override string ToString() => $"{CountText()}: {Describe()}";
}
