using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// What the spoils bag holds, for the Shared spoils bag rule (<see cref="RoomSettings.SharedSpoils"/>): one
/// count per spoil type, indexed like the game's dBeastIndex_e (tww-decomp d_com_inf_game.h), which is also
/// how it stores them (dSv_player_bag_item_record_c::mBeastNum[8], 0-99 each). The bag's 8 slots
/// (dSv_player_bag_item_c::mBeast[8]) only say where each type shows in the menu: a type takes the first
/// free slot when first obtained (setBeastItem) and leaves it when its count reaches 0 (setBeastItemEmpty),
/// so 8 types always fit. As a room total every count is 0-<see cref="MaxCount"/>; as a delta
/// (SendSpoilsDelta) they are signed.
/// </summary>
public sealed class SpoilsCounts : IBagCounts<SpoilsCounts>
{
    public const int TypeCount = 8;
    /// <summary>d_meter.cpp clamps each spoil count to 99.</summary>
    public const int MaxCount = 99;

    /// <summary>dBeastIndex_e order, with the item number each index stands for (fopMsgM_itemNumIdx(24 + i)).</summary>
    public static readonly (string Name, byte ItemNo)[] Types =
    [
        ("Skull Necklace", 0x45),
        ("Boko Baba Seed", 0x46),
        ("Golden Feather", 0x47),
        ("Knight's Crest", 0x48),
        ("Red Chu Jelly", 0x49),
        ("Green Chu Jelly", 0x4A),
        ("Blue Chu Jelly", 0x4B),
        ("Joy Pendant", 0x1F),
    ];

    /// <summary>Count per type, <see cref="TypeCount"/> long, in dBeastIndex_e order.</summary>
    public int[] Counts { get; set; } = new int[TypeCount];

    public SpoilsCounts() { }

    public SpoilsCounts(params int[] counts)
    {
        Counts = new int[TypeCount];
        Array.Copy(counts, Counts, Math.Min(counts.Length, TypeCount));
    }

    public int this[int type] => Counts[type];

    private bool WellFormed => Counts is { Length: TypeCount };

    public bool IsValidTotal() => WellFormed && Counts.All(c => c >= 0 && c <= MaxCount);

    public bool IsValidDelta() =>
        WellFormed && Counts.Any(c => c != 0) && Counts.All(c => c >= -MaxCount && c <= MaxCount);

    [JsonIgnore]
    public bool IsEmpty => WellFormed && Counts.All(c => c == 0);

    public SpoilsCounts Clone() => new(Counts);

    public SpoilsCounts Minus(SpoilsCounts other)
    {
        var d = new int[TypeCount];
        for (int i = 0; i < TypeCount; i++) d[i] = Counts[i] - other.Counts[i];
        return new SpoilsCounts(d);
    }

    public bool Equals(SpoilsCounts? other) =>
        other is not null && WellFormed && other.WellFormed && Counts.AsSpan().SequenceEqual(other.Counts);

    public override bool Equals(object? obj) => Equals(obj as SpoilsCounts);

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var c in Counts ?? []) h.Add(c);
        return h.ToHashCode();
    }

    public override string ToString()
    {
        if (!WellFormed) return "(malformed)";
        var parts = Enumerable.Range(0, TypeCount).Where(i => Counts[i] != 0).Select(i => $"{Counts[i]} {Types[i].Name}").ToList();
        return parts.Count == 0 ? "empty" : string.Join(", ", parts);
    }

    public string DeltaText()
    {
        if (!WellFormed) return "(malformed)";
        var parts = Enumerable.Range(0, TypeCount).Where(i => Counts[i] != 0).Select(i => $"{Counts[i]:+#;-#} {Types[i].Name}").ToList();
        return parts.Count == 0 ? "no change" : string.Join(", ", parts);
    }
}
