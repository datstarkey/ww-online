using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The local player's outfit / tunic colour, as the game side sees it.
///
/// Every Link on screen shares one model (one linktexS3TC texture header), so appearance is applied
/// per Link at draw time by the puppet REL (GameMod/src/puppet_link/puppet_appearance.c): it keeps
/// the local Link in the published outfit/colour and swaps each puppet's own look in around its
/// draw. The client never writes model data — it only publishes the choice
/// (<see cref="Publish"/>). The REL only runs while a puppet actor exists, so
/// <see cref="PuppetSyncService"/> keeps a parked puppet alive while <see cref="NeedsRel"/> says the
/// local Link doesn't show the choice yet (e.g. a hero's-clothes story flag just arrived, or the
/// colour changed with no peer around). The recoloured tunic outlives the REL, so this is brief.
/// </summary>
public static class LocalAppearance
{
    /// <summary>The Default preset / an old or unset slot (0,0,0): vanilla texture, no recolour.</summary>
    public static bool IsDefaultColor(byte r, byte g, byte b) =>
        (r | g | b) == 0 ||
        (r == PuppetLayout.TUNIC_COLOR_DEFAULT_R && g == PuppetLayout.TUNIC_COLOR_DEFAULT_G && b == PuppetLayout.TUNIC_COLOR_DEFAULT_B);

    /// <summary>LOCAL_APPEARANCE_WORD: (clothes &lt;&lt; 24) | 0xRRGGBB.</summary>
    public static uint PackWord(AppearanceState a) =>
        ((uint)a.ClothesType << 24) | ((uint)a.ColorR << 16) | ((uint)a.ColorG << 8) | a.ColorB;

    /// <summary>
    /// Outfit the local Link should wear. APPEARANCE_CLOTHES_DEFAULT (and anything unknown) follows the
    /// save exactly like playerInit: casual unless the hero's clothes are owned, and always casual in
    /// the second quest (d_a_player_main.cpp:12362).
    /// </summary>
    public static bool WantsCasual(byte clothesType, bool heroClothesOwned, byte clearCount) => clothesType switch
    {
        PuppetLayout.APPEARANCE_CLOTHES_HERO => false,
        PuppetLayout.APPEARANCE_CLOTHES_CASUAL => true,
        _ => !heroClothesOwned || clearCount != 0,
    };

    /// <summary>What the game currently shows for the local Link (all read-only).</summary>
    /// <param name="LinkCasual">daPyFlg1_CASUAL_CLOTHES on the real Link.</param>
    /// <param name="HeroClothesOwned">EVENT_BIT_HERO_CLOTHES in the save.</param>
    /// <param name="ClearCount">dSv_player_info_c::mClearCount.</param>
    /// <param name="HeaderImage">Image the shared linktexS3TC header points at (header + imageOffset).</param>
    /// <param name="PublishedImage">LOCAL_APPEARANCE_IMAGE: the REL's recoloured image for the local Link, 0 = none.</param>
    /// <param name="AppliedRgb">LOCAL_APPEARANCE_APPLIED: the colour in that image.</param>
    public readonly record struct Snapshot(bool LinkCasual, bool HeroClothesOwned, byte ClearCount,
        uint HeaderImage, uint PublishedImage, uint AppliedRgb);

    /// <summary>
    /// Does the REL have to run to make the local Link match <paramref name="want"/>? Yes when the
    /// outfit differs, or (hero clothes only — casual clothes are never recoloured) the live header
    /// doesn't show the wanted colour: a custom colour not (yet) applied, or a stale recolour when
    /// the Default colour is wanted.
    /// </summary>
    public static bool NeedsRel(AppearanceState want, in Snapshot s)
    {
        if (WantsCasual(want.ClothesType, s.HeroClothesOwned, s.ClearCount) != s.LinkCasual)
            return true;
        if (s.LinkCasual)
            return false;
        bool recoloured = s.PublishedImage != 0 && s.HeaderImage == s.PublishedImage;
        if (IsDefaultColor(want.ColorR, want.ColorG, want.ColorB))
            return recoloured;
        uint rgb = ((uint)want.ColorR << 16) | ((uint)want.ColorG << 8) | want.ColorB;
        return !recoloured || s.AppliedRgb != rgb;
    }

    /// <summary>Write the choice for the REL: the word, then the tag that validates it.</summary>
    public static void Publish(IDolphinService dolphin, AppearanceState a)
    {
        dolphin.WriteMemory(PuppetLayout.LOCAL_APPEARANCE_WORD_ADDR, BigEndian(PackWord(a)));
        dolphin.WriteMemory(PuppetLayout.LOCAL_APPEARANCE_TAG_ADDR, BigEndian(PuppetLayout.LOCAL_APPEARANCE_MAGIC));
    }

    /// <summary>Read the local Link's appearance state, or null if Link / its header isn't readable.</summary>
    public static Snapshot? Read(IDolphinService dolphin, uint linkPtr)
    {
        if (ReadU32(dolphin, linkPtr + PuppetLayout.DAPY_OFF_NO_RESET_FLG1) is not uint flg1 ||
            ReadU32(dolphin, linkPtr + PuppetLayout.DAPY_OFF_CURR_LINKTEX) is not uint header ||
            header < 0x80000000 || header >= 0x84000000 ||
            ReadU32(dolphin, header + PuppetLayout.RESTIMG_OFF_IMAGE_OFFSET) is not uint imageOffset ||
            ReadU32(dolphin, PuppetLayout.LOCAL_APPEARANCE_IMAGE_ADDR) is not uint published ||
            ReadU32(dolphin, PuppetLayout.LOCAL_APPEARANCE_APPLIED_ADDR) is not uint applied)
            return null;
        byte eventByte = dolphin.Read(GameMemoryAddresses.Events.HeroClothesEventByte) ?? 0;
        byte clearCount = dolphin.Read(GameMemoryAddresses.Player.ClearCount) ?? 0;
        return new Snapshot(
            LinkCasual: (flg1 & PuppetLayout.DAPY_FLG1_CASUAL_CLOTHES) != 0,
            HeroClothesOwned: (eventByte & (PuppetLayout.EVENT_BIT_HERO_CLOTHES & 0xFF)) != 0,
            ClearCount: clearCount,
            HeaderImage: unchecked(header + imageOffset),
            PublishedImage: published,
            AppliedRgb: applied);
    }

    private static byte[] BigEndian(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    private static uint? ReadU32(IDolphinService dolphin, uint addr)
    {
        var b = dolphin.ReadMemory(addr, 4);
        if (b == null || b.Length < 4) return null;
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }
}
