namespace WWOnline.Patcher.BinaryFormats.Rel;

/// <summary>
/// PowerPC and Dolphin-specific relocation types used in REL module files.
/// </summary>
public enum RelRelocationType : byte
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

    R_DOLPHIN_NOP = 0xC9,
    R_DOLPHIN_SECTION = 0xCA,
    R_DOLPHIN_END = 0xCB,
    R_DOLPHIN_MRKREF = 0xCC,
}
