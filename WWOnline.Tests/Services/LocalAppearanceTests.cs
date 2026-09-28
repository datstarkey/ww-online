using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The client side of per-Link appearance: what it publishes for the puppet REL and when it keeps a
/// (parked) puppet alive so the REL can bring the local Link in line.
/// </summary>
public class LocalAppearanceTests
{
    private const byte Hero = PuppetLayout.APPEARANCE_CLOTHES_HERO;
    private const byte Casual = PuppetLayout.APPEARANCE_CLOTHES_CASUAL;
    private const byte GameDefault = PuppetLayout.APPEARANCE_CLOTHES_DEFAULT;

    private const uint TexImage = 0x80A00000;     // the pristine hero image in TEX1
    private const uint RecolorImage = 0x81234560;  // a REL look-block variant

    private static AppearanceState Want(byte clothes, byte r = PuppetLayout.TUNIC_COLOR_DEFAULT_R,
        byte g = PuppetLayout.TUNIC_COLOR_DEFAULT_G, byte b = PuppetLayout.TUNIC_COLOR_DEFAULT_B) =>
        new() { ClothesType = clothes, ColorR = r, ColorG = g, ColorB = b };

    private static LocalAppearance.Snapshot Game(bool casual, bool heroClothes = true, byte clear = 0,
        uint header = TexImage, uint published = 0, uint applied = 0) =>
        new(casual, heroClothes, clear, header, published, applied);

    [Theory]
    [InlineData(Hero, false, (byte)0, false)]
    [InlineData(Casual, true, (byte)0, true)]
    [InlineData(GameDefault, true, (byte)0, false)]  // hero's clothes owned -> hero
    [InlineData(GameDefault, false, (byte)0, true)]  // not yet -> pajamas
    [InlineData(GameDefault, true, (byte)1, true)]   // second quest -> always pajamas (playerInit)
    [InlineData((byte)7, true, (byte)0, false)]      // unknown = follow the save
    public void WantsCasual_FollowsSettingOrPlayerInitRule(byte clothes, bool heroOwned, byte clear, bool casual) =>
        Assert.Equal(casual, LocalAppearance.WantsCasual(clothes, heroOwned, clear));

    [Fact]
    public void DefaultColour_IsTheSharedMarkerOrBlack()
    {
        Assert.True(LocalAppearance.IsDefaultColor(PuppetLayout.TUNIC_COLOR_DEFAULT_R, PuppetLayout.TUNIC_COLOR_DEFAULT_G,
            PuppetLayout.TUNIC_COLOR_DEFAULT_B));
        Assert.True(LocalAppearance.IsDefaultColor(0, 0, 0));
        Assert.False(LocalAppearance.IsDefaultColor(20, 20, 20)); // the Black preset is a real colour
    }

    [Fact]
    public void Matching_NeedsNoRel()
    {
        Assert.False(LocalAppearance.NeedsRel(Want(Hero), Game(casual: false)));
        Assert.False(LocalAppearance.NeedsRel(Want(Casual), Game(casual: true)));
        Assert.False(LocalAppearance.NeedsRel(Want(GameDefault), Game(casual: false, heroClothes: true)));
    }

    [Fact]
    public void HeroClothesFlagArriving_UnderGameDefault_NeedsRel()
    {
        // Shared story just set the hero's-clothes bit while this Link still wears pajamas.
        Assert.True(LocalAppearance.NeedsRel(Want(GameDefault), Game(casual: true, heroClothes: true)));
    }

    [Fact]
    public void CustomColour_NeedsRelUntilTheHeaderShowsIt()
    {
        var black = Want(Hero, 20, 20, 20);
        Assert.True(LocalAppearance.NeedsRel(black, Game(casual: false)));                                  // never applied
        Assert.True(LocalAppearance.NeedsRel(black, Game(false, published: RecolorImage, applied: 0x141414))); // header reset (e.g. game reboot)
        Assert.True(LocalAppearance.NeedsRel(black, Game(false, header: RecolorImage, published: RecolorImage, applied: 0x781EA0))); // old colour
        Assert.False(LocalAppearance.NeedsRel(black, Game(false, header: RecolorImage, published: RecolorImage, applied: 0x141414)));
    }

    [Fact]
    public void DefaultColour_NeedsRelOnlyToUndoARecolour()
    {
        Assert.True(LocalAppearance.NeedsRel(Want(Hero), Game(false, header: RecolorImage, published: RecolorImage, applied: 0x141414)));
        Assert.False(LocalAppearance.NeedsRel(Want(Hero), Game(false, header: TexImage, published: RecolorImage, applied: 0x141414)));
    }

    [Fact]
    public void Pajamas_AreNeverRecoloured()
    {
        Assert.False(LocalAppearance.NeedsRel(Want(Casual, 20, 20, 20), Game(casual: true)));
    }

    [Fact]
    public void Publish_WritesWordThenTag()
    {
        var game = new FakeDolphin();
        var order = new List<uint>();
        game.BeforeWrite = order.Add;
        LocalAppearance.Publish(game, Want(Casual, 0x78, 0x1E, 0xA0));
        Assert.Equal(0x01781EA0u, game.GetU32(PuppetLayout.LOCAL_APPEARANCE_WORD_ADDR));
        Assert.Equal((uint)PuppetLayout.LOCAL_APPEARANCE_MAGIC, game.GetU32(PuppetLayout.LOCAL_APPEARANCE_TAG_ADDR));
        Assert.Equal([PuppetLayout.LOCAL_APPEARANCE_WORD_ADDR, PuppetLayout.LOCAL_APPEARANCE_TAG_ADDR], order);
    }

    [Fact]
    public void Read_FollowsLinkHeaderAndSave()
    {
        var game = new FakeDolphin();
        const uint link = 0x80400000, header = 0x80B00000;
        game.SetU32(link + PuppetLayout.DAPY_OFF_NO_RESET_FLG1, 0x1000 | PuppetLayout.DAPY_FLG1_CASUAL_CLOTHES);
        game.SetU32(link + PuppetLayout.DAPY_OFF_CURR_LINKTEX, header);
        game.SetU32(header + PuppetLayout.RESTIMG_OFF_IMAGE_OFFSET, 0xFFFFFF00);                // image just below the header
        game.Set(GameMemoryAddresses.Events.HeroClothesEventByte.Address, PuppetLayout.EVENT_BIT_HERO_CLOTHES & 0xFF);
        game.Set(GameMemoryAddresses.Player.ClearCount.Address, 1);
        game.SetU32(PuppetLayout.LOCAL_APPEARANCE_IMAGE_ADDR, RecolorImage);
        game.SetU32(PuppetLayout.LOCAL_APPEARANCE_APPLIED_ADDR, 0x141414);

        var s = LocalAppearance.Read(game, link);

        Assert.NotNull(s);
        Assert.True(s.Value.LinkCasual);
        Assert.True(s.Value.HeroClothesOwned);
        Assert.Equal(1, s.Value.ClearCount);
        Assert.Equal(header - 0x100, s.Value.HeaderImage);
        Assert.Equal(RecolorImage, s.Value.PublishedImage);
        Assert.Equal(0x141414u, s.Value.AppliedRgb);
    }

    [Fact]
    public void Read_NullWhenTheHeaderPointerIsBad()
    {
        var game = new FakeDolphin();
        game.SetU32(0x80400000 + PuppetLayout.DAPY_OFF_CURR_LINKTEX, 0);
        Assert.Null(LocalAppearance.Read(game, 0x80400000));
    }
}
