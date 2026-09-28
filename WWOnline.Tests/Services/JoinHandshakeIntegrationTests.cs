using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WWOnline.Hubs;
using WWOnline.Server.Hubs;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The version handshake over a real SignalR connection (Kestrel on a free local port):
/// SignalRClientService.ConnectAsync against the real GameHub, a room that refuses, and a
/// server from before the handshake.
/// </summary>
[Collection("GameHub static state")]
public class JoinHandshakeIntegrationTests
{
    /// <summary>A room on a newer protocol.</summary>
    public class RefusingHub : Hub
    {
        public const string Reason = "Room runs WW-Online 9.0.0 (protocol 99); you have WW-Online 0.1.0 (protocol 1) — update the app.";
        public JoinResult Join(JoinRequest request) => JoinResult.Reject(Reason, 99, "9.0.0");
    }

    /// <summary>A server from before the version check: no Join method.</summary>
    public class LegacyHub : Hub
    {
        public Task SetPlayerName(string playerName) => Task.CompletedTask;
        public List<PlayerInfo> GetPlayers() => [];
    }

    private static async Task<(WebApplication App, int Port)> StartAsync<THub>() where THub : Hub
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapHub<THub>(HubConstants.HubPath);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return (app, new Uri(address).Port);
    }

    [Fact]
    public async Task SameProtocol_Connects()
    {
        var (app, port) = await StartAsync<GameHub>();
        await using var _ = app;
        await using var client = new SignalRClientService();
        int connected = 0;
        client.Connected += () => Interlocked.Increment(ref connected);

        var (success, message) = await client.ConnectAsync("127.0.0.1", port, "Alice");

        Assert.True(success, message);
        Assert.True(client.IsConnected);
        Assert.Equal(1, connected);
        Assert.Null(client.LastJoinRejection);
        Assert.True(client.LastJoinResult?.Accepted);
        Assert.Equal(HubConstants.ProtocolVersion, client.LastJoinResult?.ServerProtocolVersion);
    }

    [Fact]
    public async Task RefusedJoin_FailsConnect_WithTheRoomsReason_AndDisconnects()
    {
        var (app, port) = await StartAsync<RefusingHub>();
        await using var _ = app;
        await using var client = new SignalRClientService();
        string? rejected = null;
        int connected = 0;
        client.JoinRejected += reason => rejected = reason;
        client.Connected += () => Interlocked.Increment(ref connected);

        var (success, message) = await client.ConnectAsync("127.0.0.1", port, "Alice");

        Assert.False(success);
        Assert.Equal(RefusingHub.Reason, message);
        Assert.Equal(RefusingHub.Reason, rejected);
        Assert.Equal(RefusingHub.Reason, client.LastJoinRejection);
        Assert.False(client.IsConnected);
        Assert.Equal(0, connected);
    }

    [Fact]
    public async Task ServerWithoutJoin_IsTreatedAsTooOld()
    {
        var (app, port) = await StartAsync<LegacyHub>();
        await using var _ = app;
        await using var client = new SignalRClientService();

        var (success, message) = await client.ConnectAsync("127.0.0.1", port, "Alice");

        Assert.False(success);
        Assert.Contains("the room's host needs to update", message);
        Assert.False(client.IsConnected);
    }
}
