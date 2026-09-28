namespace WWOnline.Patcher.BinaryFormats.Rarc;

/// <summary>
/// File attribute type flags used in RARC archive file entries.
/// Ported from the Python RARCFileAttrType IntFlag enum.
/// </summary>
[Flags]
public enum RarcFileAttrType : byte
{
    FILE = 0x01,
    DIRECTORY = 0x02,
    COMPRESSED = 0x04,
    PRELOAD_TO_MRAM = 0x10,
    PRELOAD_TO_ARAM = 0x20,
    LOAD_FROM_DVD = 0x40,
    YAZ0_COMPRESSED = 0x80,
}
