using System.Text.Json;

namespace WWOnline.Patcher.WorldData;

/// <summary>
/// Which switches the shared world may sync, per stage slot and room, built at patch time from the
/// player's own game files (<see cref="SwitchTableBuilder"/>) and written next to the patched game
/// (<see cref="FileName"/>). Never in the repo: it is derived from Nintendo's stage data.
/// <list type="bullet">
/// <item><see cref="Dan"/>: dungeon-visit switches (0x80-0xBF, dSv_danBit_c) of a stage that only latching
/// actors set (safe to sync on-edges only). Per stage, not per save slot: the sea caves share slots but
/// each is its own visit.</item>
/// <item><see cref="Zone"/>: room switches (0xC0-0xEF, dSv_zoneBit_c) of a stage's room, same rule.</item>
/// <item><see cref="MemoryExcluded"/>: memory switches (0x00-0x7F) of a save slot that must NOT be
/// OR-merged: a push block's path position is encoded in two of them (d_a_obj_movebox.cpp:1163-1206).</item>
/// </list>
/// </summary>
public sealed class SwitchTable
{
    public const string FileName = "wwo-switch-table.json";
    public const int FormatVersion = 1;

    public int Version { get; init; } = FormatVersion;

    /// <summary>The <see cref="SwitchTableBuilder.RulesVersion"/> the table was built with.</summary>
    public int Rules { get; init; }

    /// <summary>How many stages the table was built from (a sanity figure for the logs).</summary>
    public int StageCount { get; init; }

    /// <summary>Save slot → memory switch numbers never to merge.</summary>
    public Dictionary<int, int[]> MemoryExcluded { get; init; } = new();

    /// <summary>Stage name → dan switch numbers (0x80-0xBF) that may sync.</summary>
    public Dictionary<string, int[]> Dan { get; init; } = new();

    /// <summary>Stage name → room → zone switch numbers (0xC0-0xEF) that may sync.</summary>
    public Dictionary<string, Dictionary<int, int[]>> Zone { get; init; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>The memory switch words (dSv_memBit_c::mSwitch[4] layout) that must not be merged in a slot.</summary>
    public uint[] MemoryExcludedWords(int slot) => Words(MemoryExcluded.GetValueOrDefault(slot), 0x00, 4);

    /// <summary>The dan switches that may sync in a stage, as dSv_danBit_c::mSwitch[2] words (bit n - 0x80).</summary>
    public uint[] DanWords(string stage) => Words(Dan.GetValueOrDefault(stage), 0x80, 2);

    /// <summary>The zone switches that may sync in a room: word 0 = 0xC0-0xDF, word 1 = 0xE0-0xEF (bit n - 0xC0).</summary>
    public uint[] ZoneWords(string stage, int room) =>
        Words(Zone.GetValueOrDefault(stage)?.GetValueOrDefault(room), 0xC0, 2);

    private static uint[] Words(int[]? switches, int first, int count)
    {
        var w = new uint[count];
        if (switches == null) return w;
        foreach (int s in switches)
        {
            int i = s - first;
            if (i >= 0 && i < count * 32)
                w[i >> 5] |= 1u << (i & 31);
        }
        return w;
    }

    public void Write(string gamePath) =>
        File.WriteAllText(Path.Combine(gamePath, FileName), JsonSerializer.Serialize(this, JsonOptions));

    /// <summary>The table next to a patched game, or null when there is none (patched by an older app) or it is unreadable.</summary>
    public static SwitchTable? Load(string gamePath)
    {
        try
        {
            var path = Path.Combine(gamePath, FileName);
            if (!File.Exists(path)) return null;
            var table = JsonSerializer.Deserialize<SwitchTable>(File.ReadAllText(path), JsonOptions);
            return table?.Version == FormatVersion ? table : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
