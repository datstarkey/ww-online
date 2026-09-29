using System.Numerics;

namespace WWOnline.Shared.Models;

/// <summary>
/// The dungeon-visit and room switches of one place in the game, for the shared world's live puzzle
/// state (docs/live-world.md §5): <see cref="Dan"/> = dSv_danBit_c switches 0x80-0xBF of save slot
/// <see cref="Slot"/> (they live for the whole visit of that slot's stages), <see cref="Zone"/> =
/// dSv_zoneBit_c switches 0xC0-0xEF of room <see cref="Room"/> of <see cref="Stage"/> (they live while
/// the room is loaded). Grow-only and on-edges only: the server ORs what players set and nobody ever
/// sends a clear, so a switch the game turns off again locally is never pushed back on (clients only
/// sync switches their patch-time switch table says are latching).
/// </summary>
public class RoomSwitches
{
    public const int DanWords = 2;
    public const int ZoneWords = 2;
    public const int SlotCount = StageFlags.SlotCount;
    public const int MaxRoom = 63;
    public const int MaxStageNameLength = 8; // dStage_startStage_c::mName is char[8]

    /// <summary>Zone word 1 holds 0xE0-0xEF only (48 zone switches).</summary>
    public const uint ZoneWord1Mask = 0xFFFF;

    /// <summary>Stage name (1..8 printable ASCII characters).</summary>
    public string Stage { get; set; } = "";

    /// <summary>The stage's save slot (dStage_stagInfo_GetSaveTbl), 0..15.</summary>
    public int Slot { get; set; }

    /// <summary>The room, 0..63.</summary>
    public int Room { get; set; }

    /// <summary>Switches 0x80-0xBF: bit n - 0x80 of word (n - 0x80) >> 5 (dSv_danBit_c::mSwitch[2]).</summary>
    public uint[] Dan { get; set; } = new uint[DanWords];

    /// <summary>Switches 0xC0-0xEF: word 0 = 0xC0-0xDF, word 1 = 0xE0-0xEF (bit n - 0xC0 - 32 * word).</summary>
    public uint[] Zone { get; set; } = new uint[ZoneWords];

    /// <summary>Structure and range check for anything from the network.</summary>
    public bool IsValid() =>
        IsValidStageName(Stage) && Slot is >= 0 and < SlotCount && Room is >= 0 and <= MaxRoom &&
        Dan is { Length: DanWords } && Zone is { Length: ZoneWords } && (Zone[1] & ~ZoneWord1Mask) == 0;

    public static bool IsValidStageName(string? stage) =>
        stage is { Length: > 0 and <= MaxStageNameLength } && stage.All(c => c is > ' ' and <= '~');

    /// <summary>Same stage, slot and room.</summary>
    public bool SamePlace(RoomSwitches other) => Stage == other.Stage && Slot == other.Slot && Room == other.Room;

    public bool IsEmpty => Dan.All(w => w == 0) && Zone.All(w => w == 0);

    public int BitCount => Dan.Sum(w => BitOperations.PopCount(w)) + Zone.Sum(w => BitOperations.PopCount(w));

    /// <summary>An empty set for the same place.</summary>
    public RoomSwitches EmptyCopy() => new() { Stage = Stage, Slot = Slot, Room = Room };

    public RoomSwitches Clone()
    {
        var c = EmptyCopy();
        Array.Copy(Dan, c.Dan, DanWords);
        Array.Copy(Zone, c.Zone, ZoneWords);
        return c;
    }

    /// <summary>OR <paramref name="other"/>'s bits into this. Returns true if any bit was added.</summary>
    public bool MergeFrom(RoomSwitches other)
    {
        bool changed = false;
        for (int i = 0; i < DanWords; i++) { changed |= (other.Dan[i] & ~Dan[i]) != 0; Dan[i] |= other.Dan[i]; }
        for (int i = 0; i < ZoneWords; i++) { changed |= (other.Zone[i] & ~Zone[i]) != 0; Zone[i] |= other.Zone[i]; }
        return changed;
    }

    /// <summary>Bits set in this but not in <paramref name="other"/> (same place as this).</summary>
    public RoomSwitches Except(RoomSwitches other)
    {
        var d = EmptyCopy();
        for (int i = 0; i < DanWords; i++) d.Dan[i] = Dan[i] & ~other.Dan[i];
        for (int i = 0; i < ZoneWords; i++) d.Zone[i] = Zone[i] & ~other.Zone[i];
        return d;
    }

    /// <summary>Only the bits also set in <paramref name="dan"/> / <paramref name="zone"/> (same place as this).</summary>
    public RoomSwitches Masked(IReadOnlyList<uint> dan, IReadOnlyList<uint> zone)
    {
        var d = EmptyCopy();
        for (int i = 0; i < DanWords; i++) d.Dan[i] = Dan[i] & dan[i];
        for (int i = 0; i < ZoneWords; i++) d.Zone[i] = Zone[i] & zone[i];
        return d;
    }

    /// <summary>The same bits without the room ones (for a player elsewhere in the slot's stages).</summary>
    public RoomSwitches DanOnly()
    {
        var d = EmptyCopy();
        Array.Copy(Dan, d.Dan, DanWords);
        return d;
    }

    public override string ToString() =>
        $"{Stage} slot {Slot} room {Room}: dan={Dan[0]:X8},{Dan[1]:X8} zone={Zone[0]:X8},{Zone[1]:X4}";
}
