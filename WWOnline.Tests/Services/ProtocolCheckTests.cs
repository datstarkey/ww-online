using Microsoft.AspNetCore.SignalR;
using Moq;
using WWOnline.Server.Hubs;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

public class ProtocolCheckTests
{
    private const int Server = 5;

    private static JoinRequest Request(int protocol, string version = "0.2.0", string name = "Alice") =>
        new() { PlayerName = name, ProtocolVersion = protocol, AppVersion = version };

    [Fact]
    public void SameProtocol_IsAccepted_WithServerVersion()
    {
        var result = ProtocolCheck.Evaluate(Request(Server), Server, "0.3.0");

        Assert.True(result.Accepted);
        Assert.Equal("", result.Reason);
        Assert.Equal(Server, result.ServerProtocolVersion);
        Assert.Equal("0.3.0", result.ServerAppVersion);
    }

    [Fact]
    public void OlderClient_IsRejected_AndToldToUpdate()
    {
        var result = ProtocolCheck.Evaluate(Request(4, "0.2.0"), Server, "0.3.0");

        Assert.False(result.Accepted);
        Assert.Equal("Room runs WW-Online 0.3.0 (protocol 5); you have WW-Online 0.2.0 (protocol 4) — update the app.", result.Reason);
    }

    [Fact]
    public void NewerClient_IsRejected_AndTheHostIsToldToUpdate()
    {
        var result = ProtocolCheck.Evaluate(Request(6, "0.4.0"), Server, "0.3.0");

        Assert.False(result.Accepted);
        Assert.Contains("(protocol 6)", result.Reason);
        Assert.EndsWith("the room's host needs to update WW-Online.", result.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveProtocol_IsRejected(int protocol)
    {
        var result = ProtocolCheck.Evaluate(Request(protocol), Server, "0.3.0");

        Assert.False(result.Accepted);
        Assert.Contains("no valid protocol version", result.Reason);
    }

    [Fact]
    public void NullRequest_IsRejected()
    {
        var result = ProtocolCheck.Evaluate(null, Server, "0.3.0");
        Assert.False(result.Accepted);
        Assert.False(string.IsNullOrEmpty(result.Reason));
    }

    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("  ", "unknown")]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("1.2.3 <script>", "unknown")]
    public void SanitizeVersion_KeepsOnlySafeVersions(string? input, string expected) =>
        Assert.Equal(expected, ProtocolCheck.SanitizeVersion(input));

    [Fact]
    public void SanitizeVersion_CapsLength() =>
        Assert.Equal(ProtocolCheck.MaxAppVersionLength, ProtocolCheck.SanitizeVersion(new string('1', 500)).Length);

    [Fact]
    public void SanitizeName_TrimsAndCaps()
    {
        Assert.Equal("Bob", ProtocolCheck.SanitizeName("  Bob "));
        Assert.Equal("", ProtocolCheck.SanitizeName(null));
        Assert.Equal(ProtocolCheck.MaxPlayerNameLength, ProtocolCheck.SanitizeName(new string('x', 100)).Length);
    }

    [Fact]
    public void SanitizeName_StripsControlCharacters_AndNeverSplitsASurrogatePair()
    {
        Assert.Equal("BobFake log line", ProtocolCheck.SanitizeName("Bob\r\nFake log line\0"));
        Assert.Equal("", ProtocolCheck.SanitizeName("\n\t\u0001"));

        // 31 chars then an emoji (a surrogate pair straddling the 32-char cap): the pair is dropped whole.
        var name = ProtocolCheck.SanitizeName(new string('x', ProtocolCheck.MaxPlayerNameLength - 1) + "\U0001F600");
        Assert.Equal(ProtocolCheck.MaxPlayerNameLength - 1, name.Length);
        Assert.False(char.IsSurrogate(name[^1]));
    }
}

/// <summary>GameHub.Join against mocked SignalR plumbing. GameHub's player table is static, so these run serially.</summary>
[Collection("GameHub static state")]
public class GameHubJoinTests
{
    private sealed class HubHarness
    {
        public readonly Mock<IGameHubClient> All = new();
        public readonly Mock<HubCallerContext> Context = new();
        public readonly GameHub Hub;
        public readonly string ConnectionId = "test-" + Guid.NewGuid().ToString("N");

        public HubHarness()
        {
            Context.Setup(c => c.ConnectionId).Returns(ConnectionId);
            var clients = new Mock<IHubCallerClients<IGameHubClient>>();
            clients.Setup(c => c.All).Returns(All.Object);
            clients.Setup(c => c.Others).Returns(All.Object);
            clients.Setup(c => c.Caller).Returns(All.Object);
            Hub = new GameHub { Context = Context.Object, Clients = clients.Object };
        }
    }

    [Fact]
    public async Task Join_WithMatchingProtocol_NamesThePlayerAndAnnouncesThem()
    {
        var h = new HubHarness();
        await h.Hub.OnConnectedAsync();
        try
        {
            var result = await h.Hub.Join(new JoinRequest
            {
                PlayerName = "  Alice  ", ProtocolVersion = HubConstants.ProtocolVersion, AppVersion = "1.0.0",
            });

            Assert.True(result.Accepted);
            Assert.Equal(HubConstants.ProtocolVersion, result.ServerProtocolVersion);
            Assert.Equal(AppInfo.Version, result.ServerAppVersion);
            h.All.Verify(c => c.PlayerJoined(h.ConnectionId, "Alice"), Times.Once);
            var players = await h.Hub.GetPlayers();
            Assert.Contains(players, p => p.ConnectionId == h.ConnectionId && p.PlayerName == "Alice");
        }
        finally
        {
            await h.Hub.OnDisconnectedAsync(null);
        }
    }

    [Fact]
    public async Task Join_WithOlderProtocol_IsRefusedWithReason_AndNeverNamed()
    {
        var h = new HubHarness();
        await h.Hub.OnConnectedAsync();
        try
        {
            var result = await h.Hub.Join(new JoinRequest
            {
                PlayerName = "Bob", ProtocolVersion = HubConstants.ProtocolVersion + 1, AppVersion = "9.9.9",
            });

            Assert.False(result.Accepted);
            Assert.Contains($"protocol {HubConstants.ProtocolVersion}", result.Reason);
            Assert.Contains("you have WW-Online 9.9.9", result.Reason);
            h.All.Verify(c => c.PlayerJoined(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            var players = await h.Hub.GetPlayers();
            Assert.DoesNotContain(players, p => p.ConnectionId == h.ConnectionId);
        }
        finally
        {
            await h.Hub.OnDisconnectedAsync(null);
        }
    }

    [Fact]
    public async Task Join_WithNullRequest_IsRefused()
    {
        var h = new HubHarness();
        await h.Hub.OnConnectedAsync();
        try
        {
            var result = await h.Hub.Join(null!);
            Assert.False(result.Accepted);
        }
        finally
        {
            await h.Hub.OnDisconnectedAsync(null);
        }
    }

    [Fact]
    public async Task LegacySetPlayerName_DropsTheConnection()
    {
        var h = new HubHarness();
        await h.Hub.OnConnectedAsync();
        try
        {
            await h.Hub.SetPlayerName("OldClient");

            h.Context.Verify(c => c.Abort(), Times.Once);
            h.All.Verify(c => c.PlayerJoined(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally
        {
            await h.Hub.OnDisconnectedAsync(null);
        }
    }
}
