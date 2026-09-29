using System.Text;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Yaz0;

namespace WWOnline.Patcher.WorldData;

/// <summary>One actor placed in a stage's data (a dzs / dzr actor chunk entry).</summary>
/// <param name="Room">The Room&lt;N&gt;.arc it is placed in, or -1 for Stage.arc.</param>
/// <param name="Name">The object name (dStage_objectNameInf::name, up to 8 characters).</param>
/// <param name="Params">base_process_class::mParameters.</param>
/// <param name="AngleX">home.angle.x (several actors keep parameters here).</param>
public readonly record struct PlacedActor(string Stage, int Room, string Name, uint Params, short AngleX, short AngleY, short AngleZ);

/// <summary>A stage's save slot (dStage_stagInfo_GetSaveTbl) and every actor placed in it, on every layer.</summary>
public sealed record StageData(string Name, int SaveTbl, IReadOnlyList<PlacedActor> Actors);

/// <summary>
/// Reads the actors placed in the game's stages (files/res/Stage/&lt;stage&gt;/Stage.arc and Room*.arc)
/// from the player's own game files, at patch time. Only the archive header and the .dzs / .dzr file
/// are inflated, not the models and textures.
/// </summary>
public static class StageDataReader
{
    /// <summary>The stages folder of an extracted game, or null when it has none.</summary>
    public static string? StagesPath(string gamePath)
    {
        var path = Path.Combine(gamePath, "files", "res", "Stage");
        return Directory.Exists(path) ? path : null;
    }

    /// <summary>Largest archive prefix we inflate: stage data archives are a few MB at most.</summary>
    public const int MaxInflate = 64 << 20;

    /// <summary>
    /// Every stage under <paramref name="stagesPath"/> that has a Stage.arc with stage info. A stage whose
    /// archives can't be read (damaged or modded files) is skipped and reported to <paramref name="skipped"/>
    /// rather than failing the whole patch.
    /// </summary>
    public static IEnumerable<StageData> ReadAll(string stagesPath, Action<string> skipped)
    {
        foreach (var dir in Directory.GetDirectories(stagesPath).OrderBy(d => d, StringComparer.Ordinal))
        {
            StageData? stage;
            try { stage = ReadStage(dir); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or IndexOutOfRangeException
                                           or ArgumentException or OverflowException or UnauthorizedAccessException)
            {
                skipped($"{Path.GetFileName(dir)} ({ex.GetType().Name}: {ex.Message})");
                continue;
            }
            if (stage != null)
                yield return stage;
        }
    }

    /// <summary>One stage folder, or null without a readable Stage.arc / STAG chunk.</summary>
    public static StageData? ReadStage(string stageDir)
    {
        string name = Path.GetFileName(stageDir);
        var stageArc = Path.Combine(stageDir, "Stage.arc");
        if (!File.Exists(stageArc))
            return null;

        int? saveTbl = null;
        var actors = new List<PlacedActor>();
        foreach (var (_, dz) in ReadDzFiles(File.ReadAllBytes(stageArc)))
        {
            saveTbl ??= ParseSaveTbl(dz);
            actors.AddRange(ParseActors(dz, name, -1));
        }
        if (saveTbl == null)
            return null;

        foreach (var file in Directory.GetFiles(stageDir, "Room*.arc"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            if (!int.TryParse(stem.AsSpan(4), out int room) || room is < 0 or > 63)
                continue;
            foreach (var (_, dz) in ReadDzFiles(File.ReadAllBytes(file)))
                actors.AddRange(ParseActors(dz, name, room));
        }
        return new StageData(name, saveTbl.Value, actors);
    }

    /// <summary>The .dzs / .dzr files in a (Yaz0-compressed) RARC archive.</summary>
    public static List<(string Name, byte[] Data)> ReadDzFiles(byte[] archive)
    {
        var result = new List<(string, byte[])>();
        // The header first: file data starts at 0x20 + the u32 at 0x0C, the tables are before it.
        var head = Yaz0Codec.Decompress(archive, 0x40);
        if (head.Length < 0x40 || Encoding.ASCII.GetString(head, 0, 4) != "RARC")
            return result;
        uint headerSize = BigEndianIO.ReadU32(head, 0x0C);
        if (headerSize > MaxInflate)
            return result;
        int dataStart = (int)headerSize + 0x20;
        head = Yaz0Codec.Decompress(archive, dataStart);
        if (head.Length < dataStart)
            return result;

        uint fileCount = BigEndianIO.ReadU32(head, 0x28);
        long entries = BigEndianIO.ReadU32(head, 0x2C) + 0x20L;
        long strings = BigEndianIO.ReadU32(head, 0x34) + 0x20L;
        var wanted = new List<(string Name, int Offset, int Size)>();
        for (long i = 0; i < fileCount; i++)
        {
            long e = entries + i * 0x14;
            if (e + 0x14 > head.Length) break;
            uint typeAndName = BigEndianIO.ReadU32(head, (int)e + 4);
            if (((typeAndName >> 24) & 0x02) != 0) continue; // directory
            long nameAt = strings + (typeAndName & 0xFFFFFF);
            if (nameAt >= head.Length) continue;
            string fileName = BigEndianIO.ReadStrUntilNull(head, (int)nameAt);
            if (!fileName.EndsWith(".dzs", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".dzr", StringComparison.OrdinalIgnoreCase))
                continue;
            long offset = dataStart + (long)BigEndianIO.ReadU32(head, (int)e + 8);
            long size = BigEndianIO.ReadU32(head, (int)e + 0xC);
            if (offset + size > MaxInflate) continue;
            wanted.Add((fileName, (int)offset, (int)size));
        }
        if (wanted.Count == 0)
            return result;

        var data = Yaz0Codec.Decompress(archive, wanted.Max(w => w.Offset + w.Size));
        foreach (var (fileName, offset, size) in wanted)
        {
            if (offset + size <= data.Length)
                result.Add((fileName, data.AsSpan(offset, size).ToArray()));
        }
        return result;
    }

    /// <summary>Actor chunk tags (first three characters; the fourth is the layer) and their entry size.</summary>
    private static int ActorEntrySize(string tag) => tag[..3] switch
    {
        "ACT" or "TRE" or "TGO" => 0x20,
        "SCO" or "TGS" or "TGD" or "DOO" or "Doo" => 0x24,
        _ => 0,
    };

    /// <summary>Every actor in a .dzs / .dzr: name at +0, params +8, position +0xC, angle +0x18 in every actor chunk.</summary>
    public static IEnumerable<PlacedActor> ParseActors(byte[] dz, string stage, int room)
    {
        foreach (var (tag, count, offset) in Chunks(dz))
        {
            int size = ActorEntrySize(tag);
            if (size == 0) continue;
            for (int i = 0; i < count; i++)
            {
                long e = offset + (long)i * size;
                if (offset < 0 || e + 0x1E > dz.Length) yield break;
                int len = 0;
                int at = (int)e;
                while (len < 8 && dz[at + len] != 0) len++;
                yield return new PlacedActor(stage, room, Encoding.ASCII.GetString(dz, at, len), BigEndianIO.ReadU32(dz, at + 8),
                    BigEndianIO.ReadS16(dz, at + 0x18), BigEndianIO.ReadS16(dz, at + 0x1A), BigEndianIO.ReadS16(dz, at + 0x1C));
            }
        }
    }

    /// <summary>
    /// The stage's save slot: stage_stag_info_class::mProp (+0x09) >> 1 &amp; 0x7F, the STAG chunk of the
    /// .dzs (d_stage.h:70, :1027; the same read as puppet_worldsync.c's STAGINFO_SAVE_TBL).
    /// </summary>
    public static int? ParseSaveTbl(byte[] dz)
    {
        foreach (var (tag, count, offset) in Chunks(dz))
        {
            if (tag == "STAG" && count > 0 && offset >= 0 && offset + 0x0A <= dz.Length)
                return (dz[offset + 0x09] >> 1) & 0x7F;
        }
        return null;
    }

    private static IEnumerable<(string Tag, int Count, int Offset)> Chunks(byte[] dz)
    {
        if (dz.Length < 4) yield break;
        uint n = BigEndianIO.ReadU32(dz, 0);
        for (int i = 0; i < n; i++)
        {
            int c = 4 + i * 12;
            if (c + 12 > dz.Length) yield break;
            uint count = BigEndianIO.ReadU32(dz, c + 4), offset = BigEndianIO.ReadU32(dz, c + 8);
            if (offset > dz.Length || count > dz.Length) continue; // garbage: skip the chunk
            yield return (Encoding.ASCII.GetString(dz, c, 4), (int)count, (int)offset);
        }
    }
}
