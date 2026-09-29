using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WWOnline.Hubs;
using WWOnline.Server;
using WWOnline.Server.Hubs;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The real server endpoints (ServerApp, as Program maps them) on a free local port: the /health
/// endpoint the Docker HEALTHCHECK polls, and claiming the room with a dedicated server's owner key.
/// </summary>
[Collection("GameHub static state")]
public class ServerAppIntegrationTests
{
    private static async Task<(WebApplication App, int Port)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        ServerApp.AddServices(builder.Services);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        ServerApp.MapEndpoints(app);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return (app, new Uri(address).Port);
    }

    // GameHub's player list is static and a previous test's connections may still be closing, so a
    // name reused across tests ("Guesser") could belong to a lingering connection that is the room's
    // earliest joiner. Every test names its players uniquely.
    private static string Name(string role) => $"{role}-{Guid.NewGuid().ToString("N")[..8]}";

    [Fact]
    public async Task Health_Returns200_WithVersionAndProtocol()
    {
        var (app, port) = await StartAsync();
        await using var _ = app;
        using var http = new HttpClient();

        var response = await http.GetAsync($"http://127.0.0.1:{port}{ServerApp.HealthPath}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal(AppInfo.DisplayName, root.GetProperty("app").GetString());
        Assert.Equal(AppInfo.Version, root.GetProperty("version").GetString());
        Assert.Equal(HubConstants.ProtocolVersion, root.GetProperty("protocol").GetInt32());
        Assert.True(root.GetProperty("players").GetInt32() >= 0);
    }

    [Fact]
    public async Task OwnerKey_MakesTheSecondJoinerTheRoomOwner()
    {
        var earlyName = Name("Early");
        var adminName = Name("Admin");
        GameHub.ConfigureHostToken("owner-key-123");
        try
        {
            var (app, port) = await StartAsync();
            await using var _ = app;

            // First joiner, no key: owner only until the key holder arrives.
            await using var first = new SignalRClientService();
            Assert.True((await first.ConnectAsync("127.0.0.1", port, earlyName)).success);

            await using var keyHolder = new SignalRClientService();
            Assert.True((await keyHolder.ConnectAsync("127.0.0.1", port, adminName, "owner-key-123")).success);

            var connection = keyHolder.Connection!;
            var settings = await connection.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            Assert.Equal(connection.ConnectionId, settings.OwnerConnectionId);
            Assert.Equal(adminName, settings.OwnerName);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
        }
    }

    [Fact]
    public async Task WrongOwnerKey_DoesNotClaimTheRoom()
    {
        var earlyName = Name("Early");
        var guesserName = Name("Guesser");
        GameHub.ConfigureHostToken("right-key");
        try
        {
            var (app, port) = await StartAsync();
            await using var _ = app;

            await using var first = new SignalRClientService();
            Assert.True((await first.ConnectAsync("127.0.0.1", port, earlyName)).success);
            await using var guesser = new SignalRClientService();
            Assert.True((await guesser.ConnectAsync("127.0.0.1", port, guesserName, "wrong-key")).success);

            // (Not asserting WHO owns it: GameHub's player list is static, and a previous test's
            // connections may still be closing. The guesser, who joined last, must not.)
            var connection = guesser.Connection!;
            var settings = await connection.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            Assert.NotEqual(connection.ConnectionId, settings.OwnerConnectionId);
            Assert.NotEqual(guesserName, settings.OwnerName);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
        }
    }

    [Fact]
    public async Task AfterTooManyWrongKeys_EvenTheRightKeyIsIgnored_OnThatConnection()
    {
        var earlyName = Name("Early");
        var guesserName = Name("Guesser");
        var adminName = Name("Admin");
        GameHub.ConfigureHostToken("right-key");
        try
        {
            var (app, port) = await StartAsync();
            await using var _ = app;

            // Someone joins first, so the guesser (joined later) could only own the room by claiming it.
            await using var first = new SignalRClientService();
            Assert.True((await first.ConnectAsync("127.0.0.1", port, earlyName)).success);

            await using var guesser = new SignalRClientService();
            Assert.True((await guesser.ConnectAsync("127.0.0.1", port, guesserName)).success);
            var connection = guesser.Connection!;
            for (int i = 0; i < OwnerKeyCheck.MaxFailedAttempts; i++)
                await connection.InvokeAsync(HubConstants.ClaimRoomOwner, $"guess-{i}");
            await connection.InvokeAsync(HubConstants.ClaimRoomOwner, "right-key"); // too late: ignored

            var settings = await connection.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            Assert.NotEqual(connection.ConnectionId, settings.OwnerConnectionId);
            Assert.NotEqual(guesserName, settings.OwnerName);

            // Only that connection is locked out: a fresh one with the right key still claims the room.
            await using var owner = new SignalRClientService();
            Assert.True((await owner.ConnectAsync("127.0.0.1", port, adminName, "right-key")).success);
            var after = await owner.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            Assert.Equal(adminName, after.OwnerName);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
        }
    }

    [Fact]
    public async Task SharedProjectilesRule_OnlyTheRoomOwnerCanChangeIt()
    {
        var adminName = Name("Admin");
        var playerName = Name("Player");
        GameHub.ConfigureHostToken("projectiles-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true);
        try
        {
            var (app, port) = await StartAsync();
            await using var _ = app;

            await using var owner = new SignalRClientService();
            Assert.True((await owner.ConnectAsync("127.0.0.1", port, adminName, "projectiles-key")).success);
            await using var player = new SignalRClientService();
            Assert.True((await player.ConnectAsync("127.0.0.1", port, playerName)).success);

            // A player who isn't the owner can't turn it off.
            var rejected = await player.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            rejected.SharedProjectiles = false;
            await player.Connection!.InvokeAsync(HubConstants.SetRoomSettings, rejected);
            var afterPlayer = await player.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            Assert.True(afterPlayer.SharedProjectiles);

            // The owner can; everyone sees it.
            var requested = await owner.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            Assert.Equal(adminName, requested.OwnerName);
            requested.SharedProjectiles = false;
            await owner.Connection!.InvokeAsync(HubConstants.SetRoomSettings, requested);
            var afterOwner = await player.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            Assert.False(afterOwner.SharedProjectiles);
            Assert.True(afterOwner.SharedWallet && afterOwner.SharedWorld && afterOwner.SharedItems && afterOwner.SharedStory);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true);
        }
    }

    private static PuppetData At(string stage, byte room, float x = 0, float z = 0) => new()
    {
        StageName = stage,
        RoomNumber = room,
        Position = new Vector3(x, 0, z),
    };

    private static PlayerEvent Bomb(string stage, byte room, uint seq) => new()
    {
        Origin = "integration",
        Seq = seq,
        Kind = (byte)PlayerEventKind.BombThrow,
        Id = 0x1234,
        StageName = stage,
        RoomNumber = room,
        Position = new Vector3(100, 50, 100),
        SpeedF = 20,
        SpeedY = 15,
        Gravity = -2.9f,
        Timer = 120,
    };

    /// <summary>The next event <paramref name="client"/> receives within <paramref name="ms"/>, else null.</summary>
    private static async Task<PlayerEvent?> NextEvent(SignalRClientService client, Func<Task> send, int ms = 1500)
    {
        var got = new TaskCompletionSource<PlayerEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(PlayerEvent e) => got.TrySetResult(e);
        client.PlayerEventReceived += Handler;
        try
        {
            await send();
            var done = await Task.WhenAny(got.Task, Task.Delay(ms));
            return done == got.Task ? got.Task.Result : null;
        }
        finally
        {
            client.PlayerEventReceived -= Handler;
        }
    }

    [Fact]
    public async Task PlayerEvent_GoesToTheRoomWhereItHappened_AndNotWhileTheRuleIsOff()
    {
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true);
        try
        {
            var (app, port) = await StartAsync();
            await using var _ = app;

            await using var sender = new SignalRClientService();
            Assert.True((await sender.ConnectAsync("127.0.0.1", port, Name("Bomber"))).success);
            await using var near = new SignalRClientService();
            Assert.True((await near.ConnectAsync("127.0.0.1", port, Name("Near"))).success);
            await using var away = new SignalRClientService();
            Assert.True((await away.ConnectAsync("127.0.0.1", port, Name("Away"))).success);

            // Where everyone is: the server learns it from their puppet data.
            await sender.Connection!.InvokeAsync(HubConstants.SendPuppetData, At("M_Dai", 3));
            await near.Connection!.InvokeAsync(HubConstants.SendPuppetData, At("M_Dai", 3));
            await away.Connection!.InvokeAsync(HubConstants.SendPuppetData, At("M_Dai", 4));

            var awayGot = NextEvent(away, () => Task.CompletedTask, 1000);
            var nearGot = await NextEvent(near, () => sender.SendPlayerEventAsync(Bomb("M_Dai", 3, 1)));
            Assert.NotNull(nearGot);
            Assert.Equal(sender.Connection!.ConnectionId, nearGot!.PlayerId);
            Assert.Equal((byte)PlayerEventKind.BombThrow, nearGot.Kind);
            Assert.Equal(1u, nearGot.Seq);
            Assert.Null(await awayGot); // another room of the dungeon: not relayed

            // The sender walks into room 4; their bomb from room 3 still explodes for room 3, not room 4.
            await sender.Connection!.InvokeAsync(HubConstants.SendPuppetData, At("M_Dai", 4));
            var explode = Bomb("M_Dai", 3, 2);
            explode.Kind = (byte)PlayerEventKind.Explode;
            var awayGot2 = NextEvent(away, () => Task.CompletedTask, 1000);
            var nearGot2 = await NextEvent(near, () => sender.SendPlayerEventAsync(explode));
            Assert.NotNull(nearGot2);
            Assert.Equal((byte)PlayerEventKind.Explode, nearGot2!.Kind);
            Assert.Null(await awayGot2);

            // Claiming another stage than the sender is in: dropped.
            Assert.Null(await NextEvent(near, () => sender.SendPlayerEventAsync(Bomb("sea", 3, 3)), 500));

            // Rule off: dropped.
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: false);
            Assert.Null(await NextEvent(near, () => sender.SendPlayerEventAsync(Bomb("M_Dai", 3, 4)), 500));
        }
        finally
        {
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true);
        }
    }
}
