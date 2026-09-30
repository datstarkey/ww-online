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
/// the game uses as one bitfield (docs/figurines.md);</item>
/// <item>which dungeon warp jars are open (<see cref="WarpJars"/>): the 6 registers the three-way jars keep
/// their "open" bits in (docs/live-world.md §0.2);</item>
/// <item>Beedle's membership points (<see cref="BeedlePoints"/>): a counter, merged by MAX;</item>
/// <item>the postbox letters (<see cref="Letters"/>): each letter's state register, merged by MAX;</item>
/// <item>the side quests' counters, levels and prize tiers (<see cref="QuestRegisters"/>, <see cref="QuestRegisterTable"/>):
/// merged by MAX, or OR for a bitmask (docs/side-quests.md §3).</item>
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

    /// <summary>
    /// The event byte of each warp jar group, in daObj_Warpt_c::m_event_reg order (d_a_obj_warpt.cpp:37-44:
    /// UNK_A207, A107, A007, 9F07, A307, A407). onWarpBit ORs a jar's bit (1, 2 or 4) into its group's byte
    /// when the jar opens; the other jars read it live when Link warps.
    /// </summary>
    public static ReadOnlySpan<byte> WarpJarRegisterBytes => [0xA2, 0xA1, 0xA0, 0x9F, 0xA3, 0xA4];

    public const int WarpJarByteCount = 6;

    /// <summary>The bits of each warp jar register (the registers' 0x07 value mask; the rest of the byte is other flags).</summary>
    public const byte WarpJarMask = 0x07;

    /// <summary>
    /// The event byte of Beedle's point card: UNK_86FF, +1 per purchase up to 0xFF and never lowered
    /// (d_a_npc_bs1.cpp:939-941; 30 and 60 points send the silver / gold membership letters).
    /// </summary>
    public const int BeedlePointsRegisterByte = 0x86;

    /// <summary>
    /// The event byte of each postbox letter's state (dLetter_*, d_letter.cpp: 0 not sent, 1 sent, 2 in the
    /// postbox, 3 read; value mask 0x03, the rest of the byte is other flags), in daObjTpost_c::m_letter order
    /// (d_a_obj_toripost.cpp:39-52): Baito's mother, Komali's father, the bomb ad, Orca, Grandma, Rock Spire shop
    /// ad, Tingle, Aryll, silver membership, Hoskit's girlfriend, Baito, gold membership. Reading one at a
    /// postbox gives its reward; a letter one player has read reads as read for everyone, so the reward is
    /// given once, and reaches the others through the other rules (the chart through the sea map, a heart piece
    /// through the derived hearts, the ID / coupon through the delivery bag, rupees through the wallet).
    /// </summary>
    public static ReadOnlySpan<byte> LetterRegisterBytes =>
        [0xAC, 0xB5, 0x7D, 0x7B, 0x9D, 0x7A, 0xB2, 0x8B, 0xB0, 0xAE, 0x7C, 0xAF];

    public const int LetterByteCount = 12;

    /// <summary>The bits of each letter register (dLetterStts_e 0..3).</summary>
    public const byte LetterMask = 0x03;

    /// <summary>
    /// The side-quest registers the room shares, in wire order (<see cref="QuestRegisters"/>[i] is entry i: only ever
    /// append). Each keeps progress one player makes that the others' games would otherwise redo or pay again
    /// (docs/side-quests.md §3): read under <see cref="QuestRegister.Mask"/>, merged by
    /// <see cref="QuestRegister.Merge"/>, and written back leaving the byte's other bits alone.
    /// </summary>
    public static IReadOnlyList<QuestRegister> QuestRegisterTable { get; } =
    [
        // d_a_npc_ho receivePendant (:70-82): pendants handed to Mrs. Marie, capped at 99. At 0 she runs her
        // first-time talk and takes 20 more for a Cabana Deed; 40 given gives the Hero's Charm (1C04).
        new(0xC0, 0xFF, QuestRegisterMerge.Max, "Joy Pendants given to Mrs. Marie"),
        // d_a_npc_ji1 setClearRecord (:409-422): Orca's lesson level, 0-4 (4 = the 1000-hit record, 0F20). The
        // catalogue's value mask is 0x03, but the game writes 4 unmasked, so the byte's 0x07 bits are carried.
        new(0xD0, 0x07, QuestRegisterMerge.Max, "Orca's lesson level"),
        // d_a_npc_bmsw (:427-445): Koboli's mail-sorting level, 1-3.
        new(0xC2, 0x03, QuestRegisterMerge.Max, "Koboli's mail-sorting level"),
        // d_a_npc_kg1: Sploosh Kaboom prizes won (cap 3), the index into {heart, chart, rupees} (docs/hearts.md §3.3).
        new(0xFE, 0x07, QuestRegisterMerge.Max, "Sploosh Kaboom prizes won"),
        // d_a_npc_kg2: barrel shooting prizes won (cap 3), the index into {heart, chart, rupees}.
        new(0xB7, 0x03, QuestRegisterMerge.Max, "Barrel shooting prizes won"),
        // d_a_tag_ghostship (:31-39): 3 = cleared; the ship's create fails (d_a_ghostship.cpp:275) and the sea
        // chart drops its icon (d_menu_fmap.cpp:2537).
        new(0x88, 0x03, QuestRegisterMerge.Max, "Ghost Ship cleared"),
        // d_a_kb (:2170-2175): one bit per pig in Rose's pen, 1 << (shape & 3).
        new(0xBF, 0x0F, QuestRegisterMerge.Or, "Rose's pigs in the pen"),
    ];

    public const int QuestRegisterCount = 7;

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

    /// <summary>
    /// The warp jars opened, per group: WarpJars[i] is the <see cref="WarpJarMask"/> bits of event byte
    /// <see cref="WarpJarRegisterBytes"/>[i].
    /// </summary>
    public byte[] WarpJars { get; set; } = new byte[WarpJarByteCount];

    /// <summary>Beedle's membership points. Merged by MAX: the room keeps the highest card, and everyone's is
    /// raised to it (two players buying in the same moment can lose a point between them).</summary>
    public byte BeedlePoints { get; set; }

    /// <summary>Each postbox letter's state (0-3), in <see cref="LetterRegisterBytes"/> order. Merged by MAX.</summary>
    public byte[] Letters { get; set; } = new byte[LetterByteCount];

    /// <summary>The side-quest registers' values under their masks, in <see cref="QuestRegisterTable"/> order.</summary>
    public byte[] QuestRegisters { get; set; } = new byte[QuestRegisterCount];

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
        for (int i = 0; i < WarpJarByteCount; i++) f.WarpJars[i] = (byte)(eventBytes[WarpJarRegisterBytes[i]] & WarpJarMask);
        f.BeedlePoints = eventBytes[BeedlePointsRegisterByte];
        for (int i = 0; i < LetterByteCount; i++) f.Letters[i] = (byte)(eventBytes[LetterRegisterBytes[i]] & LetterMask);
        for (int i = 0; i < QuestRegisterCount; i++)
            f.QuestRegisters[i] = (byte)(eventBytes[QuestRegisterTable[i].EventByte] & QuestRegisterTable[i].Mask);
        return f;
    }

    /// <summary>Structure check for anything from the network (call <see cref="Normalize"/> after).</summary>
    public bool IsValid() =>
        Bits is { Length: ByteCount } && Figurines is { Length: FigurineByteCount } && WarpJars is { Length: WarpJarByteCount } &&
        Letters is { Length: LetterByteCount } && QuestRegisters is { Length: QuestRegisterCount };

    /// <summary>Drop every bit outside <see cref="SyncMask"/> / <see cref="FigurineMask"/>. Returns this.</summary>
    public StoryFlags Normalize()
    {
        for (int i = 0; i < ByteCount; i++) Bits[i] &= Mask[i];
        for (int i = 0; i < FigurineByteCount; i++) Figurines[i] &= FigMask[i];
        for (int i = 0; i < WarpJarByteCount; i++) WarpJars[i] &= WarpJarMask;
        for (int i = 0; i < LetterByteCount; i++) Letters[i] &= LetterMask;
        for (int i = 0; i < QuestRegisterCount; i++) QuestRegisters[i] &= QuestRegisterTable[i].Mask;
        return this;
    }

    [JsonIgnore]
    public bool IsEmpty => Bits.All(b => b == 0) && Figurines.All(b => b == 0) && WarpJars.All(b => b == 0) && BeedlePoints == 0 &&
                           Letters.All(b => b == 0) && QuestRegisters.All(b => b == 0);

    /// <summary>Set event flags (figurines not included).</summary>
    [JsonIgnore]
    public int BitCount => Bits.Sum(b => BitOperations.PopCount(b));

    /// <summary>Figurines made.</summary>
    [JsonIgnore]
    public int FigurineCount => Figurines.Sum(b => BitOperations.PopCount(b));

    /// <summary>Warp jars open.</summary>
    [JsonIgnore]
    public int WarpJarCount => WarpJars.Sum(b => BitOperations.PopCount(b));

    /// <summary>Letters with any state (sent, in the postbox or read).</summary>
    [JsonIgnore]
    public int LetterCount => Letters.Count(b => b != 0);

    /// <summary>Side-quest registers with any value.</summary>
    [JsonIgnore]
    public int QuestRegisterCountSet => QuestRegisters.Count(b => b != 0);

    /// <summary>OR the syncable bits of <paramref name="other"/> into this. Returns true if any bit was added.</summary>
    public bool MergeFrom(StoryFlags other)
    {
        bool changed = false;
        for (int i = 0; i < ByteCount; i++)
            Bits[i] = Or(Bits[i], (byte)(other.Bits[i] & Mask[i]), ref changed);
        for (int i = 0; i < FigurineByteCount; i++)
            Figurines[i] = Or(Figurines[i], (byte)(other.Figurines[i] & FigMask[i]), ref changed);
        for (int i = 0; i < WarpJarByteCount; i++)
            WarpJars[i] = Or(WarpJars[i], (byte)(other.WarpJars[i] & WarpJarMask), ref changed);
        if (other.BeedlePoints > BeedlePoints) { BeedlePoints = other.BeedlePoints; changed = true; }
        for (int i = 0; i < LetterByteCount; i++)
        {
            byte theirs = (byte)(other.Letters[i] & LetterMask);
            if (theirs > Letters[i]) { Letters[i] = theirs; changed = true; }
        }
        for (int i = 0; i < QuestRegisterCount; i++)
        {
            var reg = QuestRegisterTable[i];
            byte theirs = (byte)(other.QuestRegisters[i] & reg.Mask);
            byte merged = reg.Merge == QuestRegisterMerge.Or ? (byte)(QuestRegisters[i] | theirs) : Math.Max(QuestRegisters[i], theirs);
            if (merged != QuestRegisters[i]) { QuestRegisters[i] = merged; changed = true; }
        }
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
        for (int i = 0; i < WarpJarByteCount; i++) d.WarpJars[i] = (byte)(WarpJars[i] & ~other.WarpJars[i]);
        d.BeedlePoints = BeedlePoints > other.BeedlePoints ? BeedlePoints : (byte)0; // a MAX field: only a higher count is "missing"
        for (int i = 0; i < LetterByteCount; i++) d.Letters[i] = Letters[i] > other.Letters[i] ? Letters[i] : (byte)0; // MAX too
        for (int i = 0; i < QuestRegisterCount; i++)
            d.QuestRegisters[i] = QuestRegisterTable[i].Merge == QuestRegisterMerge.Or
                ? (byte)(QuestRegisters[i] & ~other.QuestRegisters[i])
                : QuestRegisters[i] > other.QuestRegisters[i] ? QuestRegisters[i] : (byte)0;
        return d;
    }

    /// <summary>A copy holding the event flags, warp jars and Beedle's points, without the figurines, letters and
    /// side-quest registers (which wait for an idle game).</summary>
    public StoryFlags FlagsOnly() =>
        new() { Bits = (byte[])Bits.Clone(), WarpJars = (byte[])WarpJars.Clone(), BeedlePoints = BeedlePoints };

    /// <summary>A copy holding only the figurines (no event flags).</summary>
    public StoryFlags FigurinesOnly() => new() { Figurines = (byte[])Figurines.Clone() };

    public StoryFlags Clone() =>
        new()
        {
            Bits = (byte[])Bits.Clone(), Figurines = (byte[])Figurines.Clone(), WarpJars = (byte[])WarpJars.Clone(),
            BeedlePoints = BeedlePoints, Letters = (byte[])Letters.Clone(), QuestRegisters = (byte[])QuestRegisters.Clone(),
        };

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
        $"{BitCount} flag(s)" + (FigurineCount == 0 ? "" : $" + {FigurineCount} figurine(s)") +
        (WarpJarCount == 0 ? "" : $" + {WarpJarCount} warp jar(s)") +
        (BeedlePoints == 0 ? "" : $" + Beedle {BeedlePoints} pt(s)") +
        (LetterCount == 0 ? "" : $" + {LetterCount} letter(s)") +
        (QuestRegisterCountSet == 0 ? "" : $" + {QuestRegisterCountSet} side-quest register(s)");

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
        var jars = Enumerable.Range(0, WarpJarByteCount).Where(i => WarpJars[i] != 0)
            .Select(i => $"0x{WarpJarRegisterBytes[i]:X2}={WarpJars[i]}").ToList();
        if (jars.Count > 0) parts.Add("warp jars " + string.Join(", ", jars));
        if (BeedlePoints != 0) parts.Add($"Beedle points {BeedlePoints}");
        var letters = Enumerable.Range(0, LetterByteCount).Where(i => Letters[i] != 0)
            .Select(i => $"0x{LetterRegisterBytes[i]:X2}={Letters[i]}").ToList();
        if (letters.Count > 0) parts.Add("letters " + string.Join(", ", letters));
        var quests = Enumerable.Range(0, QuestRegisterCount).Where(i => QuestRegisters[i] != 0)
            .Select(i => $"{QuestRegisterTable[i].What} {QuestRegisters[i]}").ToList();
        if (quests.Count > 0) parts.Add(string.Join(", ", quests));
        return parts.Count == 0 ? "(none)" : string.Join(", ", parts);
    }

    private static string Truncate(List<string> items, int max) =>
        items.Count <= max
            ? string.Join(", ", items)
            : string.Join(", ", items.Take(max)) + $", +{items.Count - max} more";

    public override string ToString() => $"{CountText()}: {Describe()}";
}

/// <summary>How a side-quest register merges between players.</summary>
public enum QuestRegisterMerge
{
    /// <summary>A counter, level or state that only goes up: the room keeps the highest.</summary>
    Max,

    /// <summary>A bitmask whose bits are only ever set: the room keeps every bit anyone has.</summary>
    Or,
}

/// <summary>One side-quest register the room story shares (<see cref="StoryFlags.QuestRegisterTable"/>).</summary>
/// <param name="EventByte">Its byte in dSv_event_c (0x79-0xFF).</param>
/// <param name="Mask">The bits of that byte it owns; the byte's other bits are never read or written.</param>
/// <param name="Merge">How players' values combine.</param>
/// <param name="What">What it counts, for logs and the Story flags page.</param>
public readonly record struct QuestRegister(byte EventByte, byte Mask, QuestRegisterMerge Merge, string What);
