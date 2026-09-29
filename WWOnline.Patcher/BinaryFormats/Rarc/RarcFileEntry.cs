using WWOnline.Patcher.BinaryFormats.Yaz0;

namespace WWOnline.Patcher.BinaryFormats.Rarc;

/// <summary>
/// Represents a file or directory entry within a RARC archive node.
/// Ported from the Python FileEntry class.
/// </summary>
public class RarcFileEntry
{
    public const int ENTRY_SIZE = 0x14;

    /// <summary>File ID. 0xFFFF for directory entries.</summary>
    public ushort Id { get; set; } = 0xFFFF;

    /// <summary>Hash of the entry name.</summary>
    public ushort NameHash { get; set; }

    /// <summary>Attribute type flags (file, directory, compression, preload target).</summary>
    public RarcFileAttrType Type { get; set; }

    /// <summary>Offset of the name within the string table.</summary>
    public uint NameOffset { get; set; }

    /// <summary>Entry name (file or directory name).</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// For files: offset of the file data relative to the file data list start.
    /// Not used for directories.
    /// </summary>
    public uint DataOffset { get; set; }

    /// <summary>Size of the file data, or 0x10 for directory entries.</summary>
    public uint DataSize { get; set; }

    /// <summary>File data bytes. Null for directory entries.</summary>
    public byte[]? Data { get; set; }

    /// <summary>The node that this entry belongs to (i.e., the parent directory).</summary>
    public RarcNode? ParentNode { get; set; }

    /// <summary>For directory entries: the node that this entry points to.</summary>
    public RarcNode? Node { get; set; }

    /// <summary>For directory entries: the index of the node this entry points to.</summary>
    public uint NodeIndex { get; set; }

    /// <summary>Byte offset of this entry's data within the archive.</summary>
    public int EntryOffset { get; set; }

    /// <summary>Whether this entry represents a directory.</summary>
    public bool IsDir => (Type & RarcFileAttrType.DIRECTORY) != 0;

    /// <summary>
    /// Reads the file entry from archive data at the specified offset.
    /// </summary>
    /// <param name="data">The full archive byte array.</param>
    /// <param name="entryOffset">Byte offset where this entry's 0x14 bytes begin.</param>
    /// <param name="stringListOffset">Byte offset of the string table in the archive.</param>
    /// <param name="fileDataListOffset">Byte offset of the file data section in the archive.</param>
    public void Read(byte[] data, int entryOffset, int stringListOffset, int fileDataListOffset)
    {
        EntryOffset = entryOffset;

        Id = BigEndianIO.ReadU16(data, entryOffset);
        NameHash = BigEndianIO.ReadU16(data, entryOffset + 2);
        uint typeAndNameOffset = BigEndianIO.ReadU32(data, entryOffset + 4);
        uint dataOffsetOrNodeIndex = BigEndianIO.ReadU32(data, entryOffset + 8);
        DataSize = BigEndianIO.ReadU32(data, entryOffset + 0xC);

        Type = (RarcFileAttrType)((typeAndNameOffset & 0xFF000000) >> 24);
        NameOffset = typeAndNameOffset & 0x00FFFFFF;

        Name = BigEndianIO.ReadStrUntilNull(data, stringListOffset + (int)NameOffset);

        if (IsDir)
        {
            // Directory entry: data_offset_or_node_index is the index of the target node.
            // data_size is always 0x10 for directories.
            NodeIndex = dataOffsetOrNodeIndex;
            Node = null;
            Data = null;
        }
        else
        {
            // File entry: read the actual file data.
            DataOffset = dataOffsetOrNodeIndex;
            long absoluteDataOffset = fileDataListOffset + (long)DataOffset;
            if (absoluteDataOffset + DataSize > data.Length)
                throw new InvalidDataException($"RARC: \"{Name}\" runs past the end of the archive");
            Data = new byte[DataSize];
            Array.Copy(data, absoluteDataOffset, Data, 0, DataSize);
        }
    }

    /// <summary>
    /// The file's contents, decompressed if Yaz0 — WITHOUT modifying the entry. Use this to
    /// inspect entries you don't intend to replace: DecompressDataIfNecessary would make them
    /// save back uncompressed and bloat the archive (all of RELS.arc stays in ARAM for the session).
    /// </summary>
    public byte[]? PeekDecompressedData()
    {
        if (Data == null || Data.Length < 4) return Data;
        return Yaz0Codec.CheckIsCompressed(Data) ? Yaz0Codec.Decompress(Data) : Data;
    }

    /// <summary>
    /// Replaces the file's contents with <paramref name="contents"/> (uncompressed), Yaz0-compressing
    /// it when the entry was stored compressed, so the entry keeps its form. Every vanilla REL in
    /// RELS.arc is Yaz0 (the game expands it when it loads the module), and the archive is ARAM
    /// resident, so storing a replaced REL uncompressed costs about twice the ARAM.
    /// </summary>
    public void ReplaceContents(byte[] contents)
    {
        var wasCompressed = Data != null && Yaz0Codec.CheckIsCompressed(Data);
        Data = wasCompressed && !Yaz0Codec.CheckIsCompressed(contents)
            ? new Yaz0Codec().Compress(contents)
            : contents;
    }

    /// <summary>
    /// Decompresses the file data if it is Yaz0-compressed.
    /// Clears the COMPRESSED and YAZ0_COMPRESSED flags after decompression.
    /// </summary>
    public void DecompressDataIfNecessary()
    {
        if (Data == null || Data.Length < 4)
            return;

        if (Yaz0Codec.CheckIsCompressed(Data))
        {
            Data = Yaz0Codec.Decompress(Data);
            Type &= ~RarcFileAttrType.COMPRESSED;
            Type &= ~RarcFileAttrType.YAZ0_COMPRESSED;
        }
    }

    /// <summary>
    /// Writes this file entry's metadata back into the archive stream.
    /// Recalculates name hash and compression flags before writing.
    /// </summary>
    /// <param name="stream">The archive output stream.</param>
    public void SaveChanges(Stream stream)
    {
        // Calculate name hash
        NameHash = RarcNode.ComputeNameHash(Name);

        // Update compression flags for non-directory entries
        if (!IsDir && Data != null && Yaz0Codec.CheckIsCompressed(Data))
        {
            Type |= RarcFileAttrType.COMPRESSED;
            Type |= RarcFileAttrType.YAZ0_COMPRESSED;
        }
        else if (!IsDir)
        {
            Type &= ~RarcFileAttrType.COMPRESSED;
            Type &= ~RarcFileAttrType.YAZ0_COMPRESSED;
        }

        // Compose type_and_name_offset: type byte in upper 8 bits, name offset in lower 24 bits
        uint typeAndNameOffset = ((uint)Type << 24) | (NameOffset & 0x00FFFFFF);

        uint dataOffsetOrNodeIndex;
        if (IsDir)
        {
            dataOffsetOrNodeIndex = NodeIndex;
            DataSize = 0x10;
        }
        else
        {
            dataOffsetOrNodeIndex = DataOffset;
            DataSize = Data != null ? (uint)Data.Length : 0;
        }

        BigEndianIO.WriteU16(stream, EntryOffset + 0x00, Id);
        BigEndianIO.WriteU16(stream, EntryOffset + 0x02, NameHash);
        BigEndianIO.WriteU32(stream, EntryOffset + 0x04, typeAndNameOffset);
        BigEndianIO.WriteU32(stream, EntryOffset + 0x08, dataOffsetOrNodeIndex);
        BigEndianIO.WriteU32(stream, EntryOffset + 0x0C, DataSize);
        BigEndianIO.WriteU32(stream, EntryOffset + 0x10, 0);
    }
}
