using WWOnline.Patcher.BinaryFormats.Yaz0;

namespace WWOnline.Patcher.BinaryFormats.Rarc;

/// <summary>
/// Parser and writer for Nintendo RARC archive files.
/// Ported from the Python RARC class (LagoLunatic / wwrando).
/// </summary>
public class RarcArchive
{
    // ----------------------------------------------------------------
    //  Header fields (offsets 0x00 - 0x1F)
    // ----------------------------------------------------------------

    public string Magic { get; set; } = "RARC";
    public uint Size { get; set; }
    public uint DataHeaderOffset { get; set; }
    public int FileDataListOffset { get; set; }
    public uint TotalFileDataSize { get; set; }
    public uint MramFileDataSize { get; set; }
    public uint AramFileDataSize { get; set; }
    public uint Unknown1 { get; set; }

    // ----------------------------------------------------------------
    //  Data header fields (offsets 0x20 - 0x3F)
    // ----------------------------------------------------------------

    public uint NumNodes { get; set; }
    public int NodeListOffset { get; set; }
    public uint TotalNumFileEntries { get; set; }
    public int FileEntriesListOffset { get; set; }
    public uint StringListSize { get; set; }
    public int StringListOffset { get; set; }
    public ushort NextFreeFileId { get; set; }
    public byte KeepFileIdsSyncedWithIndexes { get; set; } = 1;
    public byte Unknown2 { get; set; }
    public uint Unknown3 { get; set; }

    // ----------------------------------------------------------------
    //  Parsed data
    // ----------------------------------------------------------------

    public List<RarcNode> Nodes { get; set; } = new();
    public List<RarcFileEntry> FileEntries { get; set; } = new();

    // ----------------------------------------------------------------
    //  Read
    // ----------------------------------------------------------------

    /// <summary>
    /// Reads and parses a RARC archive from raw bytes.
    /// Handles Yaz0 decompression automatically if the data is compressed.
    /// </summary>
    public void Read(byte[] inputData)
    {
        byte[] data = inputData;

        // Decompress if Yaz0
        if (Yaz0Codec.CheckIsCompressed(data))
            data = Yaz0Codec.Decompress(data);

        // --- Read header (0x00 - 0x1F) ---
        Magic = BigEndianIO.ReadStr(data, 0x00, 4);
        if (Magic != "RARC")
            throw new InvalidDataException($"Invalid RARC magic: \"{Magic}\"");

        Size = BigEndianIO.ReadU32(data, 0x04);
        DataHeaderOffset = BigEndianIO.ReadU32(data, 0x08);
        if (DataHeaderOffset != 0x20)
            throw new InvalidDataException($"Unexpected data header offset: 0x{DataHeaderOffset:X}");

        FileDataListOffset = (int)(BigEndianIO.ReadU32(data, 0x0C) + DataHeaderOffset);
        TotalFileDataSize = BigEndianIO.ReadU32(data, 0x10);
        MramFileDataSize = BigEndianIO.ReadU32(data, 0x14);
        AramFileDataSize = BigEndianIO.ReadU32(data, 0x18);
        Unknown1 = BigEndianIO.ReadU32(data, 0x1C);

        // --- Read data header (0x20 - 0x3F) ---
        int dh = (int)DataHeaderOffset;
        NumNodes = BigEndianIO.ReadU32(data, dh + 0x00);
        NodeListOffset = (int)(BigEndianIO.ReadU32(data, dh + 0x04) + DataHeaderOffset);
        TotalNumFileEntries = BigEndianIO.ReadU32(data, dh + 0x08);
        FileEntriesListOffset = (int)(BigEndianIO.ReadU32(data, dh + 0x0C) + DataHeaderOffset);
        StringListSize = BigEndianIO.ReadU32(data, dh + 0x10);
        StringListOffset = (int)(BigEndianIO.ReadU32(data, dh + 0x14) + DataHeaderOffset);
        NextFreeFileId = BigEndianIO.ReadU16(data, dh + 0x18);
        KeepFileIdsSyncedWithIndexes = BigEndianIO.ReadU8(data, dh + 0x1A);
        Unknown2 = BigEndianIO.ReadU8(data, dh + 0x1B);
        Unknown3 = BigEndianIO.ReadU32(data, dh + 0x1C);

        // Counts come from the file: bound them by the data before sizing anything from them, so a
        // corrupt archive fails fast instead of allocating gigabytes.
        if (NumNodes > (uint)data.Length / RarcNode.ENTRY_SIZE
            || NodeListOffset < 0 || NodeListOffset > data.Length - (long)NumNodes * RarcNode.ENTRY_SIZE)
            throw new InvalidDataException($"RARC: {NumNodes} nodes don't fit in {data.Length} bytes");
        if (TotalNumFileEntries > (uint)data.Length / RarcFileEntry.ENTRY_SIZE
            || FileEntriesListOffset < 0 || FileEntriesListOffset > data.Length - (long)TotalNumFileEntries * RarcFileEntry.ENTRY_SIZE)
            throw new InvalidDataException($"RARC: {TotalNumFileEntries} file entries don't fit in {data.Length} bytes");
        if (StringListOffset < 0 || StringListOffset > data.Length || FileDataListOffset < 0 || FileDataListOffset > data.Length)
            throw new InvalidDataException("RARC: string table or file data offset is outside the file");

        // --- Read nodes ---
        Nodes = new List<RarcNode>((int)NumNodes);
        for (int i = 0; i < (int)NumNodes; i++)
        {
            int offset = NodeListOffset + i * RarcNode.ENTRY_SIZE;
            var node = new RarcNode();
            node.Read(data, offset, StringListOffset);
            Nodes.Add(node);
        }

        // --- Read file entries ---
        FileEntries = new List<RarcFileEntry>((int)TotalNumFileEntries);
        for (int i = 0; i < (int)TotalNumFileEntries; i++)
        {
            int offset = FileEntriesListOffset + i * RarcFileEntry.ENTRY_SIZE;
            var entry = new RarcFileEntry();
            entry.Read(data, offset, StringListOffset, FileDataListOffset);
            FileEntries.Add(entry);

            // Link directory entries to their target nodes
            if (entry.IsDir && entry.NodeIndex != 0xFFFFFFFF)
            {
                entry.Node = Nodes[(int)entry.NodeIndex];
                if (entry.Name != "." && entry.Name != "..")
                {
                    entry.Node.DirEntry = entry;
                }
            }
        }

        // --- Populate each node's file list ---
        foreach (var node in Nodes)
        {
            for (int i = (int)node.FirstFileIndex; i < (int)node.FirstFileIndex + node.NumFiles; i++)
            {
                var entry = FileEntries[i];
                entry.ParentNode = node;
                node.Files.Add(entry);
            }
        }
    }

    // ----------------------------------------------------------------
    //  Add / Get file entries
    // ----------------------------------------------------------------

    /// <summary>
    /// Adds a new file entry to the specified node.
    /// Sets the preload type based on file extension (.rel -> ARAM, otherwise MRAM).
    /// </summary>
    public RarcFileEntry AddNewFile(string fileName, byte[] fileData, RarcNode node)
    {
        var entry = new RarcFileEntry();

        if (KeepFileIdsSyncedWithIndexes == 0)
        {
            if (NextFreeFileId == 0xFFFF)
                throw new InvalidOperationException(
                    "Next free file ID in RARC is 0xFFFF. Cannot add new file.");
            entry.Id = NextFreeFileId;
            NextFreeFileId++;
        }

        entry.Type = RarcFileAttrType.FILE;
        if (fileName.EndsWith(".rel", StringComparison.OrdinalIgnoreCase))
            entry.Type |= RarcFileAttrType.PRELOAD_TO_ARAM;
        else
            entry.Type |= RarcFileAttrType.PRELOAD_TO_MRAM;

        entry.Name = fileName;
        entry.Data = fileData;
        entry.DataSize = (uint)fileData.Length;

        entry.ParentNode = node;
        node.Files.Add(entry);

        RegenerateAllFileEntriesList();

        return entry;
    }

    /// <summary>
    /// Finds a file entry by name. Returns null if not found.
    /// </summary>
    public RarcFileEntry? GetFileEntry(string fileName)
    {
        foreach (var entry in FileEntries)
        {
            if (entry.Name == fileName)
                return entry;
        }
        return null;
    }

    /// <summary>
    /// Replace the REL whose module ID (first u32) is <paramref name="relId"/> with
    /// <paramref name="relData"/> (uncompressed). The replaced entry keeps its Yaz0 compression
    /// (<see cref="RarcFileEntry.ReplaceContents"/>), and other entries are only peeked at, never
    /// decompressed in place — RELS.arc is loaded into ARAM, and saving every REL uncompressed
    /// doubled it (943KB → 1.97MB) and made later ARAM mounts (LkD00.arc) fail.
    /// Returns false if no entry has that module ID.
    /// </summary>
    public bool ReplaceRelById(uint relId, byte[] relData)
    {
        foreach (var entry in FileEntries)
        {
            if (entry.IsDir) continue;
            var contents = entry.PeekDecompressedData();
            if (contents == null || contents.Length < 4) continue;
            if (BigEndianIO.ReadU32(contents, 0) != relId) continue;

            entry.ReplaceContents(relData);
            return true;
        }
        return false;
    }

    // ----------------------------------------------------------------
    //  Regenerate file entries list
    // ----------------------------------------------------------------

    /// <summary>
    /// Rebuilds the flat FileEntries list by walking the node tree starting from nodes[0].
    /// If keep_file_ids_synced_with_indexes is set, also reassigns file IDs.
    /// </summary>
    public void RegenerateAllFileEntriesList()
    {
        FileEntries = new List<RarcFileEntry>();
        RegenerateFilesListForNode(Nodes[0]);

        if (KeepFileIdsSyncedWithIndexes != 0)
        {
            NextFreeFileId = (ushort)FileEntries.Count;

            for (int i = 0; i < FileEntries.Count; i++)
            {
                if (!FileEntries[i].IsDir)
                    FileEntries[i].Id = (ushort)i;
            }
        }
    }

    private void RegenerateFilesListForNode(RarcNode node)
    {
        // Sort the . and .. directory entries to be at the end of the node's file list.
        var relDirEntries = new List<RarcFileEntry>();
        foreach (var entry in node.Files)
        {
            if (entry.IsDir && (entry.Name == "." || entry.Name == ".."))
                relDirEntries.Add(entry);
        }
        foreach (var relEntry in relDirEntries)
        {
            node.Files.Remove(relEntry);
            node.Files.Add(relEntry);
        }

        node.FirstFileIndex = (uint)FileEntries.Count;
        FileEntries.AddRange(node.Files);

        // Recursively add subdirectory nodes.
        foreach (var entry in node.Files)
        {
            if (entry.IsDir && entry.Name != "." && entry.Name != ".." && entry.Node != null)
                RegenerateFilesListForNode(entry.Node);
        }
    }

    // ----------------------------------------------------------------
    //  Save / Repack
    // ----------------------------------------------------------------

    /// <summary>
    /// Repacks the RARC archive and returns the complete archive as a byte array.
    /// </summary>
    public byte[] SaveChanges()
    {
        using var stream = new MemoryStream();

        // --- 1. Write node placeholders ---
        NodeListOffset = 0x40;
        stream.SetLength(NodeListOffset);
        stream.Position = NodeListOffset;

        int nextNodeOffset = NodeListOffset;
        foreach (var node in Nodes)
        {
            node.NodeOffset = nextNodeOffset;
            stream.Position = node.NodeOffset;
            stream.Write(new byte[RarcNode.ENTRY_SIZE]);
            nextNodeOffset += RarcNode.ENTRY_SIZE;
        }

        // --- 2. Regenerate file entries list and write placeholders ---
        RegenerateAllFileEntriesList();

        // Align to 0x20 with zero padding for file entries
        BigEndianIO.AlignDataToNearest(stream, 0x20, paddingBytes: new byte[] { 0 });
        FileEntriesListOffset = (int)stream.Length;

        int nextEntryOffset = FileEntriesListOffset;
        foreach (var entry in FileEntries)
        {
            entry.EntryOffset = nextEntryOffset;
            stream.Position = entry.EntryOffset;
            stream.Write(new byte[RarcFileEntry.ENTRY_SIZE]);
            nextEntryOffset += RarcFileEntry.ENTRY_SIZE;
        }

        // --- 3. Write string table ---
        BigEndianIO.AlignDataToNearest(stream, 0x20);
        StringListOffset = (int)stream.Length;

        var writtenStringOffsets = new Dictionary<string, uint>();

        // "." at offset 0
        BigEndianIO.WriteStrWithNullByte(stream, StringListOffset + 0, ".");
        writtenStringOffsets["."] = 0;

        // ".." at offset 2
        BigEndianIO.WriteStrWithNullByte(stream, StringListOffset + 2, "..");
        writtenStringOffsets[".."] = 2;

        int nextStringOffset = 5; // After ".\0..\0"

        // Write strings for all nodes and file entries, deduplicating
        var allEntries = new List<object>();
        foreach (var n in Nodes) allEntries.Add(n);
        foreach (var e in FileEntries) allEntries.Add(e);

        foreach (var item in allEntries)
        {
            string name;
            if (item is RarcNode nodeItem)
                name = nodeItem.Name;
            else
                name = ((RarcFileEntry)item).Name;

            if (writtenStringOffsets.TryGetValue(name, out uint existingOffset))
            {
                // Reuse existing string offset
                if (item is RarcNode n) n.NameOffset = existingOffset;
                else ((RarcFileEntry)item).NameOffset = existingOffset;
            }
            else
            {
                uint offset = (uint)nextStringOffset;
                BigEndianIO.WriteStrWithNullByte(stream, StringListOffset + nextStringOffset, name);
                nextStringOffset += name.Length + 1;
                writtenStringOffsets[name] = offset;

                if (item is RarcNode n) n.NameOffset = offset;
                else ((RarcFileEntry)item).NameOffset = offset;
            }
        }

        // --- 4. Save nodes (write actual data over placeholders) ---
        foreach (var node in Nodes)
        {
            node.SaveChanges(stream);
        }

        // --- 5. Write file data ---
        BigEndianIO.AlignDataToNearest(stream, 0x20);
        FileDataListOffset = (int)stream.Length;

        // Build node index lookup to avoid O(N²) IndexOf calls
        var nodeIndexMap = new Dictionary<RarcNode, int>(Nodes.Count);
        for (int i = 0; i < Nodes.Count; i++)
            nodeIndexMap[Nodes[i]] = i;

        // Build file entry index lookup
        var fileEntryIndexMap = new Dictionary<RarcFileEntry, int>(FileEntries.Count);
        for (int i = 0; i < FileEntries.Count; i++)
            fileEntryIndexMap[FileEntries[i]] = i;

        // Group file entries by preload type: MRAM -> ARAM -> DVD
        var mramEntries = new List<RarcFileEntry>();
        var aramEntries = new List<RarcFileEntry>();
        var dvdEntries = new List<RarcFileEntry>();

        foreach (var entry in FileEntries)
        {
            if (entry.IsDir)
            {
                entry.NodeIndex = entry.Node == null ? 0xFFFFFFFF : (uint)nodeIndexMap[entry.Node];
                entry.SaveChanges(stream);
            }
            else
            {
                if ((entry.Type & RarcFileAttrType.PRELOAD_TO_MRAM) != 0)
                    mramEntries.Add(entry);
                else if ((entry.Type & RarcFileAttrType.PRELOAD_TO_ARAM) != 0)
                    aramEntries.Add(entry);
                else if ((entry.Type & RarcFileAttrType.LOAD_FROM_DVD) != 0)
                    dvdEntries.Add(entry);
                else
                    throw new InvalidOperationException(
                        $"File entry \"{entry.Name}\" is not set as being loaded into any type of RAM.");
            }
        }

        int nextFileDataOffset = 0;

        void WriteFileEntryData(RarcFileEntry entry)
        {
            if (KeepFileIdsSyncedWithIndexes != 0)
                entry.Id = (ushort)fileEntryIndexMap[entry];

            int dataSize = entry.Data?.Length ?? 0;
            entry.DataOffset = (uint)nextFileDataOffset;
            entry.DataSize = (uint)dataSize;
            entry.SaveChanges(stream);

            if (entry.Data != null && entry.Data.Length > 0)
            {
                stream.Position = FileDataListOffset + entry.DataOffset;
                stream.Write(entry.Data);
            }

            nextFileDataOffset += dataSize;

            // Pad start of the next file to 0x20 alignment
            BigEndianIO.AlignDataToNearest(stream, 0x20);
            nextFileDataOffset = (int)stream.Length - FileDataListOffset;
        }

        foreach (var entry in mramEntries)
            WriteFileEntryData(entry);
        MramFileDataSize = (uint)nextFileDataOffset;

        foreach (var entry in aramEntries)
            WriteFileEntryData(entry);
        AramFileDataSize = (uint)nextFileDataOffset - MramFileDataSize;

        foreach (var entry in dvdEntries)
            WriteFileEntryData(entry);
        TotalFileDataSize = (uint)nextFileDataOffset;

        // --- 6. Update headers ---

        // Main header (0x00 - 0x1F)
        BigEndianIO.WriteMagicStr(stream, 0x00, Magic, 4);
        Size = (uint)(FileDataListOffset + TotalFileDataSize);
        BigEndianIO.WriteU32(stream, 0x04, Size);
        DataHeaderOffset = 0x20;
        BigEndianIO.WriteU32(stream, 0x08, DataHeaderOffset);
        BigEndianIO.WriteU32(stream, 0x0C, (uint)(FileDataListOffset - 0x20));
        BigEndianIO.WriteU32(stream, 0x10, TotalFileDataSize);
        BigEndianIO.WriteU32(stream, 0x14, MramFileDataSize);
        BigEndianIO.WriteU32(stream, 0x18, AramFileDataSize);
        BigEndianIO.WriteU32(stream, 0x1C, 0);

        // Data header (0x20 - 0x3F)
        int dh = (int)DataHeaderOffset;
        NumNodes = (uint)Nodes.Count;
        BigEndianIO.WriteU32(stream, dh + 0x00, NumNodes);
        TotalNumFileEntries = (uint)FileEntries.Count;
        BigEndianIO.WriteU32(stream, dh + 0x04, (uint)(NodeListOffset - (int)DataHeaderOffset));
        BigEndianIO.WriteU32(stream, dh + 0x08, TotalNumFileEntries);
        BigEndianIO.WriteU32(stream, dh + 0x0C, (uint)(FileEntriesListOffset - (int)DataHeaderOffset));
        StringListSize = (uint)(FileDataListOffset - StringListOffset);
        BigEndianIO.WriteU32(stream, dh + 0x10, StringListSize);
        BigEndianIO.WriteU32(stream, dh + 0x14, (uint)(StringListOffset - (int)DataHeaderOffset));
        BigEndianIO.WriteU16(stream, dh + 0x18, NextFreeFileId);
        BigEndianIO.WriteU8(stream, dh + 0x1A, KeepFileIdsSyncedWithIndexes);
        BigEndianIO.WriteU8(stream, dh + 0x1B, 0);
        BigEndianIO.WriteU32(stream, dh + 0x1C, 0);

        return stream.ToArray();
    }

    /// <summary>
    /// Convenience method: reads a RARC archive from a file path.
    /// </summary>
    public static RarcArchive FromFile(string path)
    {
        var archive = new RarcArchive();
        archive.Read(File.ReadAllBytes(path));
        return archive;
    }
}
