using System.Text.RegularExpressions;
using WWOnline.Patcher.Patches;
using Xunit;

namespace WWOnline.Patcher.Tests.Patches;

/// <summary>
/// seachart_players.asm can't include puppet_shared.h, so the address it loads the REL's draw function from is
/// written out by hand: it must be SEACHART_FN_ADDR, or the chart would call through some other word.
/// </summary>
public class SeaChartPatchTests
{
    private static string Asm => File.ReadAllText(Path.Combine(TestRepo.GameMod, "src", "patches", "seachart_players.asm"));

    [Fact]
    public void TheHookLoads_SeachartFnAddr()
    {
        var lis = Regex.Match(Asm, @"lis\s+r12,\s*0x([0-9A-Fa-f]+)");
        var lwz = Regex.Match(Asm, @"lwz\s+r12,\s*(-?)0x([0-9A-Fa-f]+)\s*\(r12\)");
        Assert.True(lis.Success && lwz.Success);
        long addr = (Convert.ToInt64(lis.Groups[1].Value, 16) << 16) +
                    (lwz.Groups[1].Value == "-" ? -1 : 1) * Convert.ToInt64(lwz.Groups[2].Value, 16);
        uint want = PuppetSharedHeader.ReadDefine(
            Path.Combine(TestRepo.GameMod, "src", "puppet_link", "puppet_shared.h"), "SEACHART_FN_ADDR");
        Assert.Equal(want, (uint)addr);
    }

    [Fact]
    public void TheHook_ReplacesTheInstructionAfterTheScreenDraw()
    {
        // dDlst_FMAP_c::draw (0x801BB024): bl J2DScreen::draw at 0x801BB06C, then lwz r31, 0xC(r1) at 0x801BB070.
        Assert.Matches(@"\.org 0x801BB070", Asm);
        Assert.Matches(@"lwz r31, 0xC \(r1\)", Asm);
        Assert.Matches(@"b 0x801BB074", Asm);
    }
}
