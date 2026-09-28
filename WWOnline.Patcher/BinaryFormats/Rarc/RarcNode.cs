using System.Text;

namespace WWOnline.Patcher.BinaryFormats.Rarc;

/// <summary>
/// Represents a directory node in a RARC archive.
/// Each node contains a list of file entries and metadata about the directory.
/// Ported from the Python Node class.
/// </summary>
public class RarcNode
{
    public const int ENTRY_SIZE = 0x10;

    /// <summary>Four-character type identifier (e.g. "ROOT").</summary>
    public string Type { get; set; } = "";

    /// <summary>Directory name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Offset of the name within the string table.</summary>
    public uint NameOffset { get; set; }

    /// <summary>Hash of the name, computed as: hash = (hash * 3 + charCode) &amp; 0xFFFF.</summary>
    public ushort NameHash { get; set; }

    /// <summary>Number of file entries in this node (including . and .. entries).</summary>
    public ushort NumFiles { get; set; }

    /// <summary>Index of the first file entry belonging to this node in the global file entries list.</summary>
    public uint FirstFileIndex { get; set; }

    /// <summary>The file entries belonging to this node.</summary>
    public List<RarcFileEntry> Files { get; set; } = new();

    /// <summary>The directory file entry that points to this node (the entry in the parent's file list).</summary>
    public RarcFileEntry? DirEntry { get; set; }

    /// <summary>Byte offset of this node's data within the archive.</summary>
    public int NodeOffset { get; set; }

    /// <summary>
    /// Reads the node from archive data at the specified offset.
    /// </summary>
    /// <param name="data">The full archive byte array.</param>
    /// <param name="nodeOffset">Byte offset where this node's 0x10 bytes begin.</param>
    /// <param name="stringListOffset">Byte offset of the string table in the archive.</param>
    public void Read(byte[] data, int nodeOffset, int stringListOffset)
    {
        NodeOffset = nodeOffset;

        Type = BigEndianIO.ReadStr(data, nodeOffset + 0x00, 4);
        NameOffset = BigEndianIO.ReadU32(data, nodeOffset + 0x04);
        NameHash = BigEndianIO.ReadU16(data, nodeOffset + 0x08);
        NumFiles = BigEndianIO.ReadU16(data, nodeOffset + 0x0A);
        FirstFileIndex = BigEndianIO.ReadU32(data, nodeOffset + 0x0C);

        Name = BigEndianIO.ReadStrUntilNull(data, stringListOffset + (int)NameOffset);
    }

    /// <summary>
    /// Writes this node's data back into the archive stream.
    /// Recalculates name hash and num_files before writing.
    /// </summary>
    /// <param name="stream">The archive output stream.</param>
    public void SaveChanges(Stream stream)
    {
        // Calculate name hash
        NameHash = ComputeNameHash(Name);

        // Update num_files from the actual file list
        NumFiles = (ushort)Files.Count;

        BigEndianIO.WriteMagicStr(stream, NodeOffset + 0x00, Type, 4);
        BigEndianIO.WriteU32(stream, NodeOffset + 0x04, NameOffset);
        BigEndianIO.WriteU16(stream, NodeOffset + 0x08, NameHash);
        BigEndianIO.WriteU16(stream, NodeOffset + 0x0A, NumFiles);
        BigEndianIO.WriteU32(stream, NodeOffset + 0x0C, FirstFileIndex);
    }

    /// <summary>
    /// Computes the RARC name hash for a given string.
    /// Algorithm: hash = 0; for each char: hash = hash * 3 + charCode; hash &amp;= 0xFFFF.
    /// </summary>
    public static ushort ComputeNameHash(string name)
    {
        int hash = 0;
        foreach (char c in name)
        {
            hash *= 3;
            hash += c;
            hash &= 0xFFFF;
        }
        return (ushort)hash;
    }
}
