using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// What the delivery bag holds, for the Shared delivery bag rule (<see cref="RoomSettings.SharedDelivery"/>):
/// how many of each quest item (<see cref="Types"/>, item numbers 0x8C-0x9E in tww-decomp d_item_data.h), and
/// which of them were ever obtained.
///   - The bag is 8 slots of item numbers (dSv_player_bag_item_c::mReserve[8], 0xFF = empty) with no counts:
///     an item that arrives takes the first empty slot (setReserveItem), one that is handed over, posted,
///     traded, set on Zunari's stand or thrown away leaves its slot (setReserveItemEmpty /
///     setReserveItemChange). The same item can sit in more than one slot (Zunari sells trade goods until
///     three are in the bag), so a type's count is the number of slots holding it; a total fits the 8 slots.
///   - <see cref="Obtained"/> is dSv_player_get_bag_item_c::mReserveFlags: bit (item - 0x8C) is set by the
///     item's item_func_* when it is first obtained and never cleared (only the demo build's Zunari debug menu
///     clears one). NPCs read it: Zunari counts his stock by it, and "ever obtained and no longer in the bag" is
///     how the game itself tells that Father's Letter and Moblin's Letter were handed over (dNpc_chkLetterPassed,
///     d_kankyo_dayproc.inc). It only grows (OR-merged).
/// As a room total every count is 0-8 and they fit the bag; as a delta (SendDeliveryDelta) counts are signed
/// and <see cref="Obtained"/> holds the newly obtained bits.
/// </summary>
public sealed class DeliveryCounts : IBagCounts<DeliveryCounts>
{
    public const int SlotCount = 8;
    public const byte FirstItemNo = 0x8C;

    /// <summary>The delivery bag's items in item-number order (index = item - 0x8C = the mReserveFlags bit),
    /// from d_item_data.h and item_func_flower_1 .. item_func_kaisen_present2 (d_item.cpp). The unused
    /// salvage items 0x9F-0xA2 are left out: nothing in the retail game gives them.</summary>
    public static readonly (string Name, byte ItemNo)[] Types =
    [
        ("Town Flower", 0x8C),
        ("Sea Flower", 0x8D),
        ("Exotic Flower", 0x8E),
        ("Hero's Flag", 0x8F),
        ("Big Catch Flag", 0x90),
        ("Big Sale Flag", 0x91),
        ("Pinwheel", 0x92),
        ("Sickle Moon Flag", 0x93),
        ("Skull Tower Idol", 0x94),
        ("Fountain Idol", 0x95),
        ("Postman Statue", 0x96),
        ("Shop Guru Statue", 0x97),
        ("Father's Letter", 0x98),
        ("Note to Mom", 0x99),
        ("Maggie's Letter", 0x9A),
        ("Moblin's Letter", 0x9B),
        ("Cabana Deed", 0x9C),
        ("Complimentary ID", 0x9D),
        ("Fill-Up Coupon", 0x9E),
    ];

    public static readonly int TypeCount = Types.Length;

    /// <summary>The first one-of-a-kind item: Father's Letter onwards (letters, Cabana Deed, Complimentary ID,
    /// Fill-Up Coupon) are each given once. The trade goods before it can be held more than once.</summary>
    public const int FirstUniqueType = 12;

    /// <summary>Whether the game only ever gives one of this item (the room's bag never holds two).</summary>
    public static bool IsUnique(int type) => type >= FirstUniqueType;

    /// <summary>The <see cref="Obtained"/> bits this rule knows (one per type).</summary>
    public static readonly uint KnownObtainedMask = (1u << Types.Length) - 1;

    /// <summary>The type index for an item number, or -1 (not a delivery item this rule knows).</summary>
    public static int TypeOf(byte itemNo) =>
        itemNo >= FirstItemNo && itemNo - FirstItemNo < Types.Length ? itemNo - FirstItemNo : -1;

    /// <summary>Count per type, <see cref="TypeCount"/> long, in item-number order.</summary>
    public int[] Counts { get; set; }

    /// <summary>mReserveFlags: bit i = <see cref="Types"/>[i] was ever obtained.</summary>
    public uint Obtained { get; set; }

    public DeliveryCounts()
    {
        Counts = new int[TypeCount];
    }

    public DeliveryCounts(int[] counts, uint obtained = 0)
    {
        Counts = new int[TypeCount];
        Array.Copy(counts, Counts, Math.Min(counts.Length, TypeCount));
        Obtained = obtained;
    }

    public int this[int type] => Counts[type];

    private bool WellFormed => Counts != null && Counts.Length == TypeCount;

    private bool KnownBitsOnly => (Obtained & ~KnownObtainedMask) == 0;

    /// <summary>Slots the counts take (each item one slot).</summary>
    public int SlotsUsed() => Counts.Sum();

    public bool IsValidTotal() =>
        WellFormed && KnownBitsOnly && Counts.All(c => c >= 0 && c <= SlotCount) && SlotsUsed() <= SlotCount;

    public bool IsValidDelta() =>
        WellFormed && KnownBitsOnly && (Obtained != 0 || Counts.Any(c => c != 0)) &&
        Counts.All(c => c >= -SlotCount && c <= SlotCount);

    [JsonIgnore]
    public bool IsEmpty => WellFormed && Obtained == 0 && Counts.All(c => c == 0);

    public DeliveryCounts Clone() => new(Counts, Obtained);

    /// <summary>Counts: this - other per type. Obtained: the bits this has and other hasn't (they only grow).</summary>
    public DeliveryCounts Minus(DeliveryCounts other)
    {
        var d = new int[TypeCount];
        for (int i = 0; i < TypeCount; i++) d[i] = Counts[i] - other.Counts[i];
        return new DeliveryCounts(d, Obtained & ~other.Obtained);
    }

    public bool Equals(DeliveryCounts? other) =>
        other is not null && WellFormed && other.WellFormed && Obtained == other.Obtained &&
        Counts.AsSpan().SequenceEqual(other.Counts);

    public override bool Equals(object? obj) => Equals(obj as DeliveryCounts);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Obtained);
        foreach (var c in Counts ?? []) h.Add(c);
        return h.ToHashCode();
    }

    private static string Names(uint bits) =>
        string.Join(", ", Enumerable.Range(0, TypeCount).Where(i => (bits >> i & 1) != 0).Select(i => Types[i].Name));

    public override string ToString()
    {
        if (!WellFormed) return "(malformed)";
        var parts = Enumerable.Range(0, TypeCount).Where(i => Counts[i] != 0)
            .Select(i => Counts[i] == 1 ? Types[i].Name : $"{Counts[i]} {Types[i].Name}").ToList();
        return parts.Count == 0 ? "empty" : string.Join(", ", parts);
    }

    public string DeltaText()
    {
        if (!WellFormed) return "(malformed)";
        var parts = Enumerable.Range(0, TypeCount).Where(i => Counts[i] != 0)
            .Select(i => $"{Counts[i]:+#;-#} {Types[i].Name}").ToList();
        if (Obtained != 0) parts.Add($"first obtained: {Names(Obtained)}");
        return parts.Count == 0 ? "no change" : string.Join(", ", parts);
    }
}
