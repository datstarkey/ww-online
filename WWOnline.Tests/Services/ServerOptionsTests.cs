using WWOnline.Hubs;
using WWOnline.Server;
using WWOnline.Server.Hubs;
using WWOnline.Shared.Hubs;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>The server's settings: WWO_* environment variables (the Docker image), CLI flags on top.</summary>
public class ServerOptionsTests
{
    private static ServerOptions.Result Parse(string[] args, Dictionary<string, string>? env = null) =>
        ServerOptions.Parse(args, name => env != null && env.TryGetValue(name, out var v) ? v : null);

    [Fact]
    public void NoArgsNoEnv_GivesTodaysDefaults()
    {
        var r = Parse([]);
        Assert.True(r.IsValid);
        Assert.Equal(HubConstants.DefaultPort, r.Options.Port);
        Assert.True(r.Options.SharedWallet && r.Options.SharedWorld && r.Options.SharedItems && r.Options.SharedStory);
        Assert.True(r.Options.SharedProjectiles);
        Assert.True(r.Options.AllowWarping);
        Assert.Null(r.Options.OwnerKey);
        Assert.Null(r.Options.LogFile);
    }

    [Fact]
    public void EnvironmentVariables_AreRead()
    {
        var r = Parse([], new()
        {
            ["WWO_PORT"] = "7000",
            ["WWO_SHARED_WALLET"] = "false",
            ["WWO_SHARED_WORLD"] = "0",
            ["WWO_SHARED_ITEMS"] = "No",
            ["WWO_SHARED_STORY"] = "TRUE",
            ["WWO_OWNER_KEY"] = "  s3cret  ",
            ["WWO_LOG_FILE"] = "/data/server.log",
        });
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal(7000, r.Options.Port);
        Assert.False(r.Options.SharedWallet);
        Assert.False(r.Options.SharedWorld);
        Assert.False(r.Options.SharedItems);
        Assert.True(r.Options.SharedStory);
        Assert.Equal("s3cret", r.Options.OwnerKey);
        Assert.Equal("/data/server.log", r.Options.LogFile);
    }

    [Fact]
    public void BlankEnvironmentVariables_AreUnset()
    {
        var r = Parse([], new() { ["WWO_PORT"] = "", ["WWO_SHARED_WALLET"] = " ", ["WWO_OWNER_KEY"] = "", ["WWO_LOG_FILE"] = "" });
        Assert.True(r.IsValid);
        Assert.Equal(HubConstants.DefaultPort, r.Options.Port);
        Assert.True(r.Options.SharedWallet);
        Assert.Null(r.Options.OwnerKey);
        Assert.Null(r.Options.LogFile);
    }

    [Fact]
    public void CommandLine_WinsOverEnvironment()
    {
        var r = Parse(["7100", "--no-shared-world", "--host-token", "fromcli", "--log-file", "cli.log"], new()
        {
            ["WWO_PORT"] = "7000",
            ["WWO_SHARED_WORLD"] = "true",
            ["WWO_OWNER_KEY"] = "fromenv",
            ["WWO_LOG_FILE"] = "env.log",
        });
        Assert.True(r.IsValid);
        Assert.Equal(7100, r.Options.Port);
        Assert.False(r.Options.SharedWorld);
        Assert.Equal("fromcli", r.Options.OwnerKey);
        Assert.Equal("cli.log", r.Options.LogFile);
    }

    [Fact]
    public void CommandLine_AsTheHostButtonPassesIt()
    {
        var r = Parse(["6969", "--log-file", @"C:\logs\server.log", "--host-token", "abc123"]);
        Assert.True(r.IsValid);
        Assert.Equal(6969, r.Options.Port);
        Assert.Equal(@"C:\logs\server.log", r.Options.LogFile);
        Assert.Equal("abc123", r.Options.OwnerKey);
    }

    [Fact]
    public void OwnerKeyFlag_IsAnAliasOfHostToken()
    {
        Assert.Equal("k", Parse(["--owner-key", "k"]).Options.OwnerKey);
    }

    [Fact]
    public void AllNoSharedFlags_TurnTheRulesOff()
    {
        var o = Parse(["--no-shared-wallet", "--no-shared-world", "--no-shared-items", "--no-shared-story",
                       "--no-shared-projectiles", "--no-shared-bait", "--no-shared-spoils", "--no-warping"]).Options;
        Assert.False(o.SharedWallet || o.SharedWorld || o.SharedItems || o.SharedStory || o.SharedProjectiles || o.SharedBait ||
                     o.SharedSpoils || o.AllowWarping);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    public void AllowWarpingVariable_IsRead(string value, bool expected)
    {
        var r = Parse([], new() { [ServerOptions.AllowWarpingVar] = value });
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal(expected, r.Options.AllowWarping);
        Assert.True(r.Options.SharedWallet && r.Options.SharedProjectiles); // the other rules keep their defaults
    }

    [Fact]
    public void NoWarpingFlag_WinsOverTheVariable()
    {
        var r = Parse(["--no-warping"], new() { ["WWO_ALLOW_WARPING"] = "true" });
        Assert.True(r.IsValid);
        Assert.False(r.Options.AllowWarping);
        Assert.True(r.Options.SharedStory);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("true", true)]
    [InlineData("Yes", true)]
    public void SharedProjectilesVariable_IsRead(string value, bool expected)
    {
        var r = Parse([], new() { [ServerOptions.SharedProjectilesVar] = value });
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal(expected, r.Options.SharedProjectiles);
        Assert.True(r.Options.SharedWallet && r.Options.SharedStory); // the other rules keep their defaults
    }

    [Fact]
    public void SharedBait_DefaultsOn_ReadsItsVariable_AndTheFlagWins()
    {
        Assert.True(Parse([]).Options.SharedBait);
        var env = Parse([], new() { [ServerOptions.SharedBaitVar] = "off" });
        Assert.True(env.IsValid);
        Assert.False(env.Options.SharedBait);
        Assert.True(env.Options.SharedWallet);
        Assert.False(Parse(["--no-shared-bait"], new() { ["WWO_SHARED_BAIT"] = "true" }).Options.SharedBait);
        Assert.False(Parse([], new() { [ServerOptions.SharedBaitVar] = "maybe" }).IsValid);
    }

    [Fact]
    public void SharedSpoils_DefaultsOn_ReadsItsVariable_AndTheFlagWins()
    {
        Assert.True(Parse([]).Options.SharedSpoils);
        var env = Parse([], new() { [ServerOptions.SharedSpoilsVar] = "0" });
        Assert.True(env.IsValid);
        Assert.False(env.Options.SharedSpoils);
        Assert.True(env.Options.SharedBait);
        Assert.False(Parse(["--no-shared-spoils"], new() { ["WWO_SHARED_SPOILS"] = "yes" }).Options.SharedSpoils);
        Assert.False(Parse([], new() { [ServerOptions.SharedSpoilsVar] = "sometimes" }).IsValid);
    }

    [Fact]
    public void NoSharedProjectilesFlag_WinsOverTheVariable()
    {
        var r = Parse(["--no-shared-projectiles"], new() { ["WWO_SHARED_PROJECTILES"] = "true" });
        Assert.True(r.IsValid);
        Assert.False(r.Options.SharedProjectiles);
        Assert.True(r.Options.SharedWorld);
    }

    [Theory]
    [InlineData("WWO_PORT", "abc")]
    [InlineData("WWO_PORT", "0")]
    [InlineData("WWO_PORT", "70000")]
    [InlineData("WWO_SHARED_ITEMS", "maybe")]
    [InlineData("WWO_SHARED_PROJECTILES", "sometimes")]
    public void BadEnvironmentValues_AreErrors_NamingTheVariable(string name, string value)
    {
        var r = Parse([], new() { [name] = value });
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains(name) && e.Contains(value));
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("99999999999")] // overflows int: still a port, still an error
    public void BadCommandLinePort_IsAnError(string port)
    {
        var r = Parse([port]);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains(port));
    }

    [Theory]
    [InlineData("--owner-key")]
    [InlineData("--host-token")]
    [InlineData("--log-file")]
    public void FlagWithoutAValue_IsAnError(string flag)
    {
        var last = Parse(["6969", flag]);
        Assert.False(last.IsValid);
        Assert.Contains(last.Errors, e => e.Contains(flag) && e.Contains("needs a value"));

        var beforeAnotherFlag = Parse([flag, "--no-shared-world"]);
        Assert.False(beforeAnotherFlag.IsValid);
        Assert.False(beforeAnotherFlag.Options.SharedWorld); // the next flag still counts
    }

    [Fact]
    public void DevTestDedicatedServerArgs_StillWork()
    {
        var r = Parse(["6969", "--log-file", @"C:\repo\logs\latest\server.log"]);
        Assert.True(r.IsValid);
        Assert.Equal(@"C:\repo\logs\latest\server.log", r.Options.LogFile);
    }

    [Fact]
    public void OverlongOwnerKey_IsAnError()
    {
        Assert.True(Parse(["--owner-key", new string('k', OwnerKeyCheck.MaxKeyLength)]).IsValid);
        Assert.False(Parse(["--owner-key", new string('k', OwnerKeyCheck.MaxKeyLength + 1)]).IsValid);
        Assert.False(Parse([], new() { ["WWO_OWNER_KEY"] = new string('k', OwnerKeyCheck.MaxKeyLength + 1) }).IsValid);
    }

    [Fact]
    public void UnknownArgs_AreLeftForAspNet()
    {
        var r = Parse(["--urls", "http://*:1", "--environment", "Production"]);
        Assert.True(r.IsValid);
        Assert.Equal(HubConstants.DefaultPort, r.Options.Port);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData(" On ", true)]
    [InlineData("yes", true)]
    [InlineData("1", true)]
    [InlineData("FALSE", false)]
    [InlineData("off", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    [InlineData("2", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseBool(string? text, bool? expected) => Assert.Equal(expected, ServerOptions.ParseBool(text));

    [Theory]
    [InlineData("localhost", 6969, "http://localhost:6969/gamehub")]
    [InlineData(" 100.64.0.7 ", 7000, "http://100.64.0.7:7000/gamehub")]
    [InlineData("https://wwo.example.com", 6969, "https://wwo.example.com/gamehub")]
    [InlineData("https://wwo.example.com/", 6969, "https://wwo.example.com/gamehub")]
    [InlineData("https://example.com:8443/wwo/gamehub", 6969, "https://example.com:8443/wwo/gamehub")]
    [InlineData("http://192.168.1.20:7000", 6969, "http://192.168.1.20:7000/gamehub")]
    [InlineData("https://wwo.example.com", 0, "https://wwo.example.com/gamehub")] // URL: no Port field needed
    public void ClientHubUrl_FromHostField(string host, int port, string expected)
    {
        Assert.True(SignalRClientService.TryBuildHubUrl(host, port, out var url, out var error), error);
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("ws://wwo.example.com")]
    [InlineData("wss://wwo.example.com")]
    [InlineData("ftp://wwo.example.com")]
    [InlineData("https://")]
    [InlineData("")]
    public void ClientHubUrl_RefusesWhatItCantConnectTo(string host)
    {
        Assert.False(SignalRClientService.TryBuildHubUrl(host, 6969, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.DoesNotContain("http://ws", error);
    }

    [Fact]
    public void ClientHubUrl_PlainHostNeedsAValidPort() =>
        Assert.False(SignalRClientService.TryBuildHubUrl("localhost", 0, out _, out _));

    [Fact]
    public async Task Client_UnsupportedScheme_FailsWithTheReason_WithoutConnecting()
    {
        await using var client = new SignalRClientService();
        var (success, message) = await client.ConnectAsync("ws://wwo.example.com", 6969, "Alice");
        Assert.False(success);
        Assert.Contains("http:// or https://", message);
        Assert.Null(client.Connection);
    }
}
