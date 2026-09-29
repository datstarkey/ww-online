using System.Reflection;
using WWOnline.Patcher.Icons;
using Xunit;

namespace WWOnline.Patcher.Tests.Icons;

public class ItemIconCatalogTests
{
    [Fact]
    public void EveryNamedItemNumber_HasATexture()
    {
        var fields = typeof(ItemIconCatalog.ItemNo).GetFields(BindingFlags.Public | BindingFlags.Static);
        Assert.NotEmpty(fields);
        foreach (var f in fields)
            Assert.True(ItemIconCatalog.TextureForItem((byte)f.GetValue(null)!) != null, $"ItemNo.{f.Name} has no texture");
    }

    [Theory]
    [InlineData(0x20, "telescope")]
    [InlineData(0x22, "baton")]          // Wind Waker
    [InlineData(0x25, "rope")]           // Grappling Hook
    [InlineData(0x28, "gloves_00")]      // Power Bracelets
    [InlineData(0x29, "boots_00")]       // Iron Boots
    [InlineData(0x3E, "sword_03")]       // Master Sword (full power)
    [InlineData(0x4C, "dungeon_map")]
    [InlineData(0x4D, "compass")]
    [InlineData(0x4E, "boss_key")]
    [InlineData(0x15, "get_key")]        // small key
    [InlineData(0x69, "god_symbol_02")]  // Nayru's Pearl
    [InlineData(0x6A, "god_symbol_00")]  // Din's Pearl
    [InlineData(0x78, "sail_00")]
    [InlineData(0xAC, "max_purse")]
    public void Table_MatchesTheDecomp(byte itemNo, string texture) =>
        Assert.Equal(texture, ItemIconCatalog.TextureForItem(itemNo));

    [Theory]
    [InlineData(0x00)] // heart drop: the table's bottle_00 filler, not a real icon
    [InlineData(0x09)] // small magic jar
    [InlineData(0xFF)] // no item
    public void FillerAndEmpty_HaveNoTexture(byte itemNo) =>
        Assert.Null(ItemIconCatalog.TextureForItem(itemNo));

    [Fact]
    public void TextureNames_AreBareFileNames() =>
        Assert.All(ItemIconCatalog.AllTextures, t =>
        {
            Assert.DoesNotContain('.', t);
            Assert.DoesNotContain('/', t);
            Assert.Equal(t.ToLowerInvariant(), t);
        });
}
