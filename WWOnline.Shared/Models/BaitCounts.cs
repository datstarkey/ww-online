namespace WWOnline.Shared.Models;

/// <summary>
/// What the bait bag holds, by type, for the Shared bait bag rule (<see cref="RoomSettings.SharedBait"/>).
/// The bag has <see cref="SlotCount"/> slots (tww-decomp d_save.h dSv_player_bag_item_c::mBait[8], with the
/// counts in dSv_player_bag_item_record_c::mBaitNum[8]). Every purchase or pickup fills one empty slot and
/// sets its count to 3 (dSv_player_bag_item_c::setBaitItem):
///   - All-Purpose Bait: a slot holds up to <see cref="UsesPerSlot"/> uses; each use takes one off, and the slot
///     empties at 0 (setBaitItemEmpty). <see cref="Bait"/> counts uses across all slots.
///   - Hyoi Pear: a slot is one pear (the count isn't shown or used); a use empties the slot. <see cref="Pears"/>
///     counts slots.
/// As a room total both are &gt;= 0 and fit the bag together (<see cref="FitsTheBag"/>); as a delta
/// (SendBaitDelta) they are signed.
/// </summary>
public sealed class BaitCounts : IBagCounts<BaitCounts>
{
    public const int SlotCount = 8;
    public const int UsesPerSlot = 3;
    public const int MaxBait = SlotCount * UsesPerSlot;
    public const int MaxPears = SlotCount;

    /// <summary>All-Purpose Bait uses (0-24 in a total).</summary>
    public int Bait { get; set; }

    /// <summary>Hyoi Pears (0-8 in a total).</summary>
    public int Pears { get; set; }

    public BaitCounts() { }

    public BaitCounts(int bait, int pears)
    {
        Bait = bait;
        Pears = pears;
    }

    /// <summary>Bag slots these counts need: partial bait slots packed into full ones, one per pear.</summary>
    public static int SlotsNeeded(int bait, int pears) => (bait + UsesPerSlot - 1) / UsesPerSlot + pears;

    public bool FitsTheBag => SlotsNeeded(Bait, Pears) <= SlotCount;

    /// <summary>A room total (or a game's bag): in range and fits the 8 slots.</summary>
    public bool IsValidTotal() =>
        Bait >= 0 && Bait <= MaxBait && Pears >= 0 && Pears <= MaxPears && FitsTheBag;

    /// <summary>A change a client may send: something changed, and no type by more than a whole bag.</summary>
    public bool IsValidDelta() =>
        (Bait != 0 || Pears != 0) &&
        Bait >= -MaxBait && Bait <= MaxBait && Pears >= -MaxPears && Pears <= MaxPears;

    public bool IsEmpty => Bait == 0 && Pears == 0;

    public BaitCounts Clone() => new(Bait, Pears);

    public static BaitCounts operator -(BaitCounts a, BaitCounts b) => new(a.Bait - b.Bait, a.Pears - b.Pears);

    public BaitCounts Minus(BaitCounts other) => this - other;

    public bool Equals(BaitCounts? other) => other is not null && Bait == other.Bait && Pears == other.Pears;
    public override bool Equals(object? obj) => Equals(obj as BaitCounts);
    public override int GetHashCode() => HashCode.Combine(Bait, Pears);

    public override string ToString() => $"{Bait} bait, {Pears} pear{(Pears == 1 ? "" : "s")}";

    /// <summary>A signed change for the log, e.g. "-1 bait, +1 pear".</summary>
    public string DeltaText()
    {
        var parts = new List<string>(2);
        if (Bait != 0) parts.Add($"{Bait:+#;-#} bait");
        if (Pears != 0) parts.Add($"{Pears:+#;-#} pear{(Pears is 1 or -1 ? "" : "s")}");
        return parts.Count == 0 ? "no change" : string.Join(", ", parts);
    }
}
