using WWOnline.Server.Hubs;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;

namespace WWOnline.Server;

/// <summary>
/// The server's services and endpoints, shared by Program and the integration tests: the SignalR
/// hub at <see cref="HubConstants.HubPath"/> and a <see cref="HealthPath"/> endpoint for Docker's
/// HEALTHCHECK and uptime monitors.
/// </summary>
public static class ServerApp
{
    public const string HealthPath = "/health";

    /// <summary>GET /health: the server is up, and which WW-Online it is.</summary>
    public sealed record HealthInfo(string Status, string App, string Version, string? Commit, int Protocol, int Players);

    public static HealthInfo CurrentHealth() =>
        new("ok", AppInfo.DisplayName, AppInfo.Version, AppInfo.Commit, HubConstants.ProtocolVersion, GameHub.PlayerCount);

    public static void AddServices(IServiceCollection services)
    {
        services.AddSignalR();
        // NOTE: AllowAnyOrigin is intentional: the only clients are the desktop app (no browser
        // origin), and the hub checks every input itself.
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowAnyOrigin();
            });
        });
        // `docker stop` sends SIGTERM and kills after 10 s: close the connections well before that.
        services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
    }

    public static void MapEndpoints(WebApplication app)
    {
        app.UseCors();
        app.MapHub<GameHub>(HubConstants.HubPath);
        app.MapGet(HealthPath, () => Results.Ok(CurrentHealth()));
    }
}
