using WWOnline.Shared.Hubs;

namespace WWOnline.Server;

/// <summary>
/// The server's settings, from the command line and WWO_* environment variables (the Docker image
/// is configured through the environment; see docs/self-hosting.md). A command-line flag wins over
/// its environment variable, which wins over the default.
/// </summary>
public sealed record ServerOptions
{
    public const string PortVar = "WWO_PORT";
    public const string SharedWalletVar = "WWO_SHARED_WALLET";
    public const string SharedWorldVar = "WWO_SHARED_WORLD";
    public const string SharedItemsVar = "WWO_SHARED_ITEMS";
    public const string SharedStoryVar = "WWO_SHARED_STORY";
    public const string OwnerKeyVar = "WWO_OWNER_KEY";
    public const string LogFileVar = "WWO_LOG_FILE";

    public const string Usage =
        """
        Usage: WWOnline.Server [port] [--log-file <path>] [--owner-key <key>]
                               [--no-shared-wallet] [--no-shared-world] [--no-shared-items] [--no-shared-story]
                               [--version] [--help]

        Environment variables (a command-line flag wins over its variable):
          WWO_PORT            port to listen on (default 6969)
          WWO_SHARED_WALLET   true/false: the room's starting rules (default true);
          WWO_SHARED_WORLD      the room owner can change them from the client
          WWO_SHARED_ITEMS
          WWO_SHARED_STORY
          WWO_OWNER_KEY       a player who enters this key in the client becomes the room owner
          WWO_LOG_FILE        also log to this file (the console log is always on)
        """;

    public int Port { get; init; } = HubConstants.DefaultPort;
    public string? LogFile { get; init; }
    public bool SharedWallet { get; init; } = true;
    public bool SharedWorld { get; init; } = true;
    public bool SharedItems { get; init; } = true;
    public bool SharedStory { get; init; } = true;

    /// <summary>
    /// The secret that claims room ownership (GameHub.ClaimRoomOwner). A client that hosts the
    /// server passes a random one (--host-token); a dedicated server's admin sets it
    /// (WWO_OWNER_KEY / --owner-key) and enters it in the client's Owner key field.
    /// </summary>
    public string? OwnerKey { get; init; }

    public sealed record Result(ServerOptions Options, IReadOnlyList<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    /// <param name="getEnv">Environment lookup (Environment.GetEnvironmentVariable; a fake in tests).</param>
    public static Result Parse(IReadOnlyList<string> args, Func<string, string?> getEnv)
    {
        var errors = new List<string>();

        // Environment first, then the command line on top.
        var o = new ServerOptions();
        if (Env(getEnv, PortVar) is { } portText)
            o = o with { Port = ParsePort(portText, PortVar, errors) ?? o.Port };
        o = o with
        {
            SharedWallet = EnvBool(getEnv, SharedWalletVar, o.SharedWallet, errors),
            SharedWorld = EnvBool(getEnv, SharedWorldVar, o.SharedWorld, errors),
            SharedItems = EnvBool(getEnv, SharedItemsVar, o.SharedItems, errors),
            SharedStory = EnvBool(getEnv, SharedStoryVar, o.SharedStory, errors),
            OwnerKey = Env(getEnv, OwnerKeyVar),
            LogFile = Env(getEnv, LogFileVar),
        };

        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--log-file" when i + 1 < args.Count:
                    o = o with { LogFile = args[++i] };
                    break;
                // --host-token: what a client passes to the server it hosts. --owner-key: the same,
                // for someone running a dedicated server by hand.
                case "--host-token" or "--owner-key" when i + 1 < args.Count:
                    o = o with { OwnerKey = NullIfBlank(args[++i]) };
                    break;
                case "--no-shared-wallet": o = o with { SharedWallet = false }; break;
                case "--no-shared-world": o = o with { SharedWorld = false }; break;
                case "--no-shared-items": o = o with { SharedItems = false }; break;
                case "--no-shared-story": o = o with { SharedStory = false }; break;
                default:
                    // A bare number is the port. Anything else is left for ASP.NET's own
                    // command-line configuration (WebApplication.CreateBuilder(args)).
                    if (int.TryParse(arg, out _))
                        o = o with { Port = ParsePort(arg, "port", errors) ?? o.Port };
                    break;
            }
        }

        return new Result(o, errors);
    }

    /// <summary>Accepts true/false, 1/0, yes/no and on/off, in any case.</summary>
    public static bool? ParseBool(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "on" => true,
        "false" or "0" or "no" or "off" => false,
        _ => null,
    };

    private static string? Env(Func<string, string?> getEnv, string name) => NullIfBlank(getEnv(name));

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool EnvBool(Func<string, string?> getEnv, string name, bool fallback, List<string> errors)
    {
        if (Env(getEnv, name) is not { } text) return fallback;
        if (ParseBool(text) is { } value) return value;
        errors.Add($"{name}='{text}' is not true or false");
        return fallback;
    }

    private static int? ParsePort(string text, string source, List<string> errors)
    {
        if (int.TryParse(text.Trim(), out var port) && port is >= 1 and <= 65535) return port;
        errors.Add($"{source}='{text}' is not a port (1-65535)");
        return null;
    }
}
