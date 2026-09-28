using Serilog;
using WWOnline.Server.Hubs;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;

// CLI: [port] [--log-file <path>] [--no-shared-wallet] [--no-shared-world] [--no-shared-items] [--no-shared-story]
//      [--host-token <t>]
//      [--version]
//      (port defaults to 6969; --host-token is passed by a client that hosts this server)
// Room rules start ON; the room owner can toggle them at runtime from the client dashboard.
if (args.Contains("--version"))
{
    Console.WriteLine($"{AppInfo.DisplayVersion} server (protocol {HubConstants.ProtocolVersion})");
    return;
}

int port = HubConstants.DefaultPort;
string? logFile = null;
bool sharedWallet = true, sharedWorld = true, sharedItems = true, sharedStory = true;
string? hostToken = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--log-file" && i + 1 < args.Length)
        logFile = args[++i];
    else if (args[i] == "--no-shared-wallet")
        sharedWallet = false;
    else if (args[i] == "--no-shared-world")
        sharedWorld = false;
    else if (args[i] == "--no-shared-items")
        sharedItems = false;
    else if (args[i] == "--no-shared-story")
        sharedStory = false;
    else if (args[i] == "--host-token" && i + 1 < args.Length)
        hostToken = args[++i];
    else if (int.TryParse(args[i], out var parsedPort))
        port = parsedPort;
}

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
if (!string.IsNullOrWhiteSpace(logFile))
    logConfig = logConfig.WriteTo.File(logFile, shared: true, flushToDiskInterval: TimeSpan.FromSeconds(1),
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}");
Log.Logger = logConfig.CreateLogger();

// Only touch GameHub AFTER the logger exists: its static Logger field binds to whatever
// Log.Logger is on first use (before this line that's Serilog's silent default, and every
// [room]/[world]/[wallet]/[items]/[story] line from the hub would be lost for the whole session).
GameHub.ConfigureRoomDefaults(sharedWallet, sharedWorld, sharedItems, sharedStory);
GameHub.ConfigureHostToken(hostToken);

try
{
    Log.Information("Starting {AppName} {Version} server (protocol {Protocol}) on port {Port} (shared wallet {Wallet}, shared world {World}, shared items {Items}, shared story {Story})",
        AppInfo.DisplayName, AppInfo.Version, HubConstants.ProtocolVersion, port, sharedWallet ? "ON" : "OFF", sharedWorld ? "ON" : "OFF", sharedItems ? "ON" : "OFF", sharedStory ? "ON" : "OFF");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddSignalR();
    // NOTE: AllowAnyOrigin is intentional for dev/LAN use.
    // For a public-facing deployment, restrict to specific origins.
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowAnyOrigin();
        });
    });

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    var app = builder.Build();

    app.UseCors();
    app.MapHub<GameHub>(HubConstants.HubPath);

    Log.Information("SignalR hub mapped at {Path}", HubConstants.HubPath);

    // Periodic relay-rate line so the server log shows whether puppet data is actually flowing.
    var statsInterval = TimeSpan.FromSeconds(10);
    using var statsTimer = new Timer(_ =>
    {
        try { GameHub.LogRelayStats(statsInterval); }
        catch (Exception ex) { Log.Warning(ex, "Relay stats failed"); }
    }, null, statsInterval, statsInterval);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
