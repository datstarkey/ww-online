namespace WWOnline.Patcher.BinaryFormats.Elf;

/// <summary>
/// ELF section header types (sh_type field).
/// </summary>
public enum ElfSectionType : uint
{
    SHT_NULL = 0x0,
    SHT_PROGBITS = 0x1,
    SHT_SYMTAB = 0x2,
    SHT_STRTAB = 0x3,
    SHT_RELA = 0x4,
    SHT_HASH = 0x5,
    SHT_DYNAMIC = 0x6,
    SHT_NOTE = 0x7,
    SHT_NOBITS = 0x8,
    SHT_REL = 0x9,
    SHT_SHLIB = 0xA,
    SHT_DYNSYM = 0xB,
    SHT_INIT_ARRAY = 0xE,
    SHT_FINI_ARRAY = 0xF,
    SHT_PREINIT_ARRAY = 0x10,
    SHT_GROUP = 0x11,
    SHT_SYMTAB_SHNDX = 0x12,
    SHT_NUM = 0x13,
    Unk1 = 0x6FFFFFF5,
}

/// <summary>
/// ELF section header flags (sh_flags field). These are bitmask flags.
/// </summary>
[Flags]
public enum ElfSectionFlags : uint
{
    SHF_WRITE = 0x00000001,
    SHF_ALLOC = 0x00000002,
    SHF_EXECINSTR = 0x00000004,
    SHF_MERGE = 0x00000010,
    SHF_STRINGS = 0x00000020,
    SHF_INFO_LINK = 0x00000040,
    SHF_LINK_ORDER = 0x00000080,
    SHF_OS_NONCONFORMING = 0x00000100,
    SHF_GROUP = 0x00000200,
    SHF_TLS = 0x00000400,
    SHF_MASKOS = 0x0FF00000,
    SHF_MASKPROC = 0xF0000000,
    SHF_ORDERED = 0x04000000,
    SHF_EXCLUDE = 0x08000000,
}

/// <summary>
/// PowerPC ELF relocation types.
/// </summary>
public enum ElfRelocationType : uint
{
    R_PPC_NONE = 0x00,
    R_PPC_ADDR32 = 0x01,
    R_PPC_ADDR24 = 0x02,
    R_PPC_ADDR16 = 0x03,
    R_PPC_ADDR16_LO = 0x04,
    R_PPC_ADDR16_HI = 0x05,
    R_PPC_ADDR16_HA = 0x06,
    R_PPC_ADDR14 = 0x07,
    R_PPC_ADDR14_BRTAKEN = 0x08,
    R_PPC_ADDR14_BRNTAKEN = 0x09,
    R_PPC_REL24 = 0x0A,
    R_PPC_REL14 = 0x0B,
    R_PPC_REL14_BRTAKEN = 0x0C,
    R_PPC_REL14_BRNTAKEN = 0x0D,
    R_PPC_REL32 = 0x1A,
}

/// <summary>
/// Special ELF section indices. Some values share the same numeric value (e.g., SHN_LORESERVE and SHN_LOPROC).
/// </summary>
public enum ElfSymbolSpecialSection : ushort
{
    SHN_UNDEF = 0x0000,
    SHN_LORESERVE = 0xFF00,
    SHN_LOPROC = 0xFF00,
    SHN_HIPROC = 0xFF1F,
    SHN_LOOS = 0xFF20,
    SHN_HIOS = 0xFF3F,
    SHN_ABS = 0xFFF1,
    SHN_COMMON = 0xFFF2,
    SHN_XINDEX = 0xFFFF,
    SHN_HIRESERVE = 0xFFFF,
}

/// <summary>
/// ELF symbol types (lower 4 bits of st_info).
/// </summary>
public enum ElfSymbolType : byte
{
    STT_NOTYPE = 0,
    STT_OBJECT = 1,
    STT_FUNC = 2,
    STT_SECTION = 3,
    STT_FILE = 4,
    STT_COMMON = 5,
    STT_TLS = 6,
    STT_LOOS = 10,
    STT_HIOS = 12,
    STT_LOPROC = 13,
    STT_SPARC_REGISTER = 13,
    STT_HIPROC = 15,
}

/// <summary>
/// ELF symbol binding (upper 4 bits of st_info).
/// </summary>
public enum ElfSymbolBinding : byte
{
    STB_LOCAL = 0,
    STB_GLOBAL = 1,
    STB_WEAK = 2,
    STB_LOOS = 10,
    STB_HIOS = 12,
    STB_LOPROC = 13,
    STB_HIPROC = 15,
}
