using Serilog;
using WWOnline.Server;
using WWOnline.Server.Hubs;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;

// Settings come from the command line and WWO_* environment variables (ServerOptions.Usage,
// docs/self-hosting.md). The port defaults to 6969; --host-token is passed by a client that hosts
// this server, and WWO_OWNER_KEY / --owner-key is the same thing for a dedicated server.
// Room rules start ON unless turned off here; the room owner can toggle them at runtime.
if (args.Contains("--version"))
{
    Console.WriteLine($"{AppInfo.DisplayVersion} server (protocol {HubConstants.ProtocolVersion})");
    return;
}
if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine(ServerOptions.Usage);
    return;
}

var parsed = ServerOptions.Parse(args, Environment.GetEnvironmentVariable);
if (!parsed.IsValid)
{
    foreach (var error in parsed.Errors)
        Console.Error.WriteLine($"error: {error}");
    Console.Error.WriteLine();
    Console.Error.WriteLine(ServerOptions.Usage);
    Environment.ExitCode = 2;
    return;
}
var options = parsed.Options;

// Console (stdout) always: that's what `docker logs` shows. A file too if asked for.
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
// Serilog's file sink fails silently, so check the file can be written first and say so if not
// (e.g. a Docker bind mount the container's non-root user can't write).
string? logFileError = null;
if (!string.IsNullOrWhiteSpace(options.LogFile))
{
    try
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(options.LogFile));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using (new FileStream(options.LogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { }
        logConfig = logConfig.WriteTo.File(options.LogFile, shared: true, flushToDiskInterval: TimeSpan.FromSeconds(1),
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
        logFileError = ex.Message;
    }
}
Log.Logger = logConfig.CreateLogger();
if (logFileError != null)
    Log.Error("Can't write the log file {LogFile} ({Error}): logging to the console only", options.LogFile, logFileError);

// Only touch GameHub AFTER the logger exists: its static Logger field binds to whatever
// Log.Logger is on first use (before this line that's Serilog's silent default, and every
// [room]/[world]/[wallet]/[bait]/[spoils]/[items]/[story] line from the hub would be lost for the whole session).
GameHub.ConfigureRoomDefaults(options.SharedWallet, options.SharedWorld, options.SharedItems, options.SharedStory,
                              options.SharedProjectiles, options.SharedBait, options.SharedSpoils);
GameHub.ConfigureHostToken(options.OwnerKey);

try
{
    Log.Information("Starting {AppName} {Version} server (protocol {Protocol}) on port {Port} (shared wallet {Wallet}, shared world {World}, shared items {Items}, shared story {Story}, shared bait bag {Bait}, shared spoils bag {Spoils}, other players' projectiles {Projectiles})",
        AppInfo.DisplayName, AppInfo.Version, HubConstants.ProtocolVersion, options.Port,
        options.SharedWallet ? "ON" : "OFF", options.SharedWorld ? "ON" : "OFF", options.SharedItems ? "ON" : "OFF", options.SharedStory ? "ON" : "OFF",
        options.SharedBait ? "ON" : "OFF", options.SharedSpoils ? "ON" : "OFF", options.SharedProjectiles ? "ON" : "OFF");
    Log.Information(options.OwnerKey != null
        ? "[room] An owner key is set: the player who enters it becomes the room owner"
        : "[room] No owner key: the earliest-joined player is the room owner");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    ServerApp.AddServices(builder.Services);
    builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}");

    var app = builder.Build();
    ServerApp.MapEndpoints(app);

    Log.Information("SignalR hub mapped at {Path}, health check at {Health}", HubConstants.HubPath, ServerApp.HealthPath);
    // SIGTERM (docker stop) and Ctrl+C stop the host gracefully: SignalR closes every connection
    // and clients start reconnecting.
    app.Lifetime.ApplicationStopping.Register(() => Log.Information("Shutting down ({Count} player(s) connected)", GameHub.PlayerCount));

    // Periodic relay-rate line so the server log shows whether puppet data is actually flowing.
    var statsInterval = TimeSpan.FromSeconds(10);
    using var statsTimer = new Timer(_ =>
    {
        try { GameHub.LogRelayStats(statsInterval); }
        catch (Exception ex) { Log.Warning(ex, "Relay stats failed"); }
    }, null, statsInterval, statsInterval);

    await app.RunAsync();
    Log.Information("Server stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly");
    Environment.ExitCode = 1; // e.g. the port is taken: let a restart policy / supervisor see the failure
}
finally
{
    Log.CloseAndFlush();
}
