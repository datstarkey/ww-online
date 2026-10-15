using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

public sealed class StartupOptionsTests
{
    [Fact]
    public void HiddenTestMode_ParsesItsFlags()
    {
        var options = StartupOptions.Parse(["--player", "Player2", "--auto-connect", "--hidden", "--test-dir", "/tmp/shots",
            "--test-shots", "900,1500", "--test-rupees", "600:50", "--test-warp", "1200:Player1"]);

        Assert.True(options.Hidden);
        Assert.Equal("/tmp/shots", options.TestDir);
        Assert.Equal("900,1500", options.TestShots);
        Assert.Equal("600:50", options.TestRupees);
        Assert.Equal("1200:Player1", options.TestWarp);
        Assert.False(options.IsHeadless);
    }

    [Fact]
    public void WithoutTheFlags_NotHidden()
    {
        var options = StartupOptions.Parse(["--player", "Player1", "--host-server"]);

        Assert.False(options.Hidden);
        Assert.Null(options.TestShots);
    }
}
