using WWOnline.Data;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The names block the puppet REL draws player names from (puppet_nametag.c): name sanitising for
/// the game font, the byte encoding, and write-only-what-changed publishing.
/// </summary>
public class PuppetNameTagsTests
{
    private const uint Block = 0x81234560;

    [Theory]
    [InlineData("Link", "Link")]
    [InlineData("  Tetra  ", "Tetra")]
    [InlineData("Jösé Ñandú", "Jose Nandu")]
    [InlineData("Straße", "Strasse")]
    [InlineData("Big   \t Sword", "Big Sword")]
    [InlineData("Line\nBreak\u0007", "Line Break")]
    [InlineData("a%s{|}~", "a%s{|}~")]
    [InlineData("Medli 🎵", "Medli ?")]
    [InlineData("ゼルダ Zelda", "??? Zelda")]
    public void Sanitize_KeepsPrintableAscii(string input, string expected)
    {
        Assert.Equal(expected, PuppetNameTags.Sanitize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0001\u0002")]
    [InlineData("ゼルダ")]
    [InlineData("!!!")]
    public void Sanitize_NothingReadable_IsEmpty(string? input)
    {
        Assert.Equal("", PuppetNameTags.Sanitize(input));
    }

    [Fact]
    public void Sanitize_TruncatesToMaxChars_WithoutTrailingSpace()
    {
        string name = PuppetNameTags.Sanitize("The Hero of Winds Is Here");
        Assert.Equal("The Hero of Wind", name);

        // A cut right after a word leaves no trailing space.
        Assert.Equal("Aaaaaaaaaaaaaaa", PuppetNameTags.Sanitize("Aaaaaaaaaaaaaaa Bbb"));

        // A fold that expands (ß -> ss) can't overrun either.
        Assert.Equal(PuppetLayout.PUPPET_NAME_MAX_CHARS, PuppetNameTags.Sanitize(new string('ß', 20)).Length);
    }

    [Theory]
    [InlineData("Jösé Ñandú")]
    [InlineData("The Hero of Winds Is Here")]
    [InlineData("Medli 🎵")]
    public void Sanitize_IsIdempotent(string input)
    {
        string once = PuppetNameTags.Sanitize(input);
        Assert.Equal(once, PuppetNameTags.Sanitize(once));
    }

    [Fact]
    public void DisplayName_FallsBackToPlayerNumber()
    {
        Assert.Equal("Aryll", PuppetNameTags.DisplayName("Aryll", 0));
        Assert.Equal("Player 1", PuppetNameTags.DisplayName(null, 0));
        Assert.Equal("Player 3", PuppetNameTags.DisplayName("ゼルダ", 2));
    }

    [Fact]
    public void Encode_IsNulPaddedAscii()
    {
        byte[] bytes = PuppetNameTags.Encode("Link");
        Assert.Equal(PuppetLayout.PUPPET_NAME_BYTES, bytes.Length);
        Assert.Equal("Link"u8.ToArray(), bytes[..4]);
        Assert.All(bytes[4..], b => Assert.Equal(0, b));

        Assert.All(PuppetNameTags.Encode(""), b => Assert.Equal(0, b));
        Assert.All(PuppetNameTags.Encode(null), b => Assert.Equal(0, b));

        // Even a too-long string keeps its NUL.
        byte[] full = PuppetNameTags.Encode(new string('x', 40));
        Assert.Equal(0, full[^1]);
        Assert.All(full[..^1], b => Assert.Equal((byte)'x', b));
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(0x7FFFFFF0u, false)]
    [InlineData(0x80000000u, true)]
    [InlineData(Block, true)]
    [InlineData(Block + 2u, false)]
    [InlineData(PuppetNameTags.Mem1End - (uint)PuppetLayout.PUPPET_NAMES_BLOCK_SIZE, true)]
    [InlineData(PuppetNameTags.Mem1End - (uint)PuppetLayout.PUPPET_NAMES_BLOCK_SIZE + 4u, false)]
    [InlineData(0xC0000000u, false)]
    public void IsValidBlockAddress_WholeBlockInMem1(uint address, bool valid)
    {
        Assert.Equal(valid, PuppetNameTags.IsValidBlockAddress(address));
    }

    private static FakeDolphin GameWithBlock(uint block = Block)
    {
        var game = new FakeDolphin();
        game.SetU32(PuppetLayout.PUPPET_NAMES_PTR_ADDR, block);
        game.SetU32(block + PuppetLayout.PUPPET_NAMES_OFF_MAGIC, PuppetLayout.PUPPET_NAMES_MAGIC);
        return game;
    }

    private static string NameAt(FakeDolphin game, int slot)
    {
        var bytes = game.ReadMemory(Block + (uint)(PuppetLayout.PUPPET_NAMES_OFF_NAME0 + slot * PuppetLayout.PUPPET_NAME_BYTES),
                                    PuppetLayout.PUPPET_NAME_BYTES)!;
        int len = Array.IndexOf(bytes, (byte)0);
        return System.Text.Encoding.ASCII.GetString(bytes, 0, len);
    }

    [Fact]
    public void Publish_NoBlockYet_WritesNothing()
    {
        var game = new FakeDolphin();
        int writes = 0;
        game.BeforeWrite = _ => writes++;

        Assert.Null(PuppetNameTags.Publish(game, true, ["Link", "", ""]));
        Assert.Equal(0, writes);
    }

    [Fact]
    public void Publish_BadMagicOrPointer_WritesNothing()
    {
        var badMagic = GameWithBlock();
        badMagic.SetU32(Block, 0x12345678);
        var badPtr = GameWithBlock(0x8000_0002);
        int writes = 0;
        badMagic.BeforeWrite = _ => writes++;
        badPtr.BeforeWrite = _ => writes++;

        Assert.Null(PuppetNameTags.Publish(badMagic, true, ["Link", "", ""]));
        Assert.Null(PuppetNameTags.Publish(badPtr, true, ["Link", "", ""]));
        Assert.Equal(0, writes);
    }

    [Fact]
    public void Publish_WritesFlagsAndNames_ThenNothingWhileUnchanged()
    {
        var game = GameWithBlock();

        var first = PuppetNameTags.Publish(game, true, ["Link", "", "Tetra"]);
        Assert.Equal(new PuppetNameTags.PublishResult(Block, true, 0b101), first);
        Assert.Equal((uint)PuppetLayout.PUPPET_NAMES_FLAG_SHOW, game.GetU32(Block + PuppetLayout.PUPPET_NAMES_OFF_FLAGS));
        Assert.Equal("Link", NameAt(game, 0));
        Assert.Equal("", NameAt(game, 1));
        Assert.Equal("Tetra", NameAt(game, 2));
        Assert.Equal((uint)PuppetLayout.PUPPET_NAMES_MAGIC, game.GetU32(Block)); // never touched

        int writes = 0;
        game.BeforeWrite = _ => writes++;
        var again = PuppetNameTags.Publish(game, true, ["Link", "", "Tetra"]);
        Assert.Equal(new PuppetNameTags.PublishResult(Block, false, 0), again);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void Publish_OnlyTheChangedSlot_OrOnlyTheFlag()
    {
        var game = GameWithBlock();
        PuppetNameTags.Publish(game, true, ["Link", "Medli", "Tetra"]);

        var written = new List<uint>();
        game.BeforeWrite = written.Add;
        var rename = PuppetNameTags.Publish(game, true, ["Link", "Makar", "Tetra"]);
        Assert.Equal(0b010, rename!.Value.SlotsWritten);
        Assert.False(rename.Value.FlagsWritten);
        Assert.Equal(new[] { Block + (uint)(PuppetLayout.PUPPET_NAMES_OFF_NAME0 + PuppetLayout.PUPPET_NAME_BYTES) }, written);
        Assert.Equal("Makar", NameAt(game, 1));

        written.Clear();
        var hide = PuppetNameTags.Publish(game, false, ["Link", "Makar", "Tetra"]);
        Assert.True(hide!.Value.FlagsWritten);
        Assert.Equal(0, hide.Value.SlotsWritten);
        Assert.Equal(new[] { Block + (uint)PuppetLayout.PUPPET_NAMES_OFF_FLAGS }, written);
        Assert.Equal(0u, game.GetU32(Block + PuppetLayout.PUPPET_NAMES_OFF_FLAGS));
    }

    [Fact]
    public void Publish_ShorterName_ClearsTheOldTail()
    {
        var game = GameWithBlock();
        PuppetNameTags.Publish(game, true, ["Quill the Postman", "", ""]);
        PuppetNameTags.Publish(game, true, ["Link", "", ""]);

        Assert.Equal("Link", NameAt(game, 0));
        var slot = game.ReadMemory(Block + PuppetLayout.PUPPET_NAMES_OFF_NAME0, PuppetLayout.PUPPET_NAME_BYTES)!;
        Assert.All(slot[4..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Publish_RewritesAFreshBlock_AtTheSameAddress()
    {
        // e.g. the game rebooted and the new REL allocated its block where the old one was:
        // the client compares with memory, not with what it wrote last time.
        var game = GameWithBlock();
        PuppetNameTags.Publish(game, true, ["Link", "", ""]);
        game.Set(Block + PuppetLayout.PUPPET_NAMES_OFF_FLAGS, new byte[PuppetLayout.PUPPET_NAMES_BLOCK_SIZE - 4]);

        var result = PuppetNameTags.Publish(game, true, ["Link", "", ""]);
        Assert.Equal(new PuppetNameTags.PublishResult(Block, true, 0b001), result);
        Assert.Equal("Link", NameAt(game, 0));
    }
}
