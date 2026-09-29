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
}
