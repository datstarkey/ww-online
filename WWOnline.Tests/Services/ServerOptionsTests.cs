using WWOnline.Hubs;
using WWOnline.Server;
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
        var o = Parse(["--no-shared-wallet", "--no-shared-world", "--no-shared-items", "--no-shared-story"]).Options;
        Assert.False(o.SharedWallet || o.SharedWorld || o.SharedItems || o.SharedStory);
    }

    [Theory]
    [InlineData("WWO_PORT", "abc")]
    [InlineData("WWO_PORT", "0")]
    [InlineData("WWO_PORT", "70000")]
    [InlineData("WWO_SHARED_ITEMS", "maybe")]
    public void BadEnvironmentValues_AreErrors_NamingTheVariable(string name, string value)
    {
        var r = Parse([], new() { [name] = value });
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains(name) && e.Contains(value));
    }

    [Fact]
    public void BadCommandLinePort_IsAnError()
    {
        Assert.False(Parse(["-5"]).IsValid);
        Assert.False(Parse(["65536"]).IsValid);
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
    public void ClientHubUrl_FromHostField(string host, int port, string expected) =>
        Assert.Equal(expected, SignalRClientService.BuildHubUrl(host, port));
}
