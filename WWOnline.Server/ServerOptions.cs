using WWOnline.Server.Hubs;
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
    public const string SharedProjectilesVar = "WWO_SHARED_PROJECTILES";
    public const string SharedBaitVar = "WWO_SHARED_BAIT";
    public const string SharedSpoilsVar = "WWO_SHARED_SPOILS";
    public const string SharedDeliveryVar = "WWO_SHARED_DELIVERY";
    public const string AllowWarpingVar = "WWO_ALLOW_WARPING";
    public const string OwnerKeyVar = "WWO_OWNER_KEY";
    public const string LogFileVar = "WWO_LOG_FILE";

    public const string Usage =
        """
        Usage: WWOnline.Server [port] [--log-file <path>] [--owner-key <key>]
                               [--no-shared-wallet] [--no-shared-world] [--no-shared-items] [--no-shared-story]
                               [--no-shared-bait] [--no-shared-spoils] [--no-shared-delivery]
                               [--no-shared-projectiles]
                               [--no-warping]
                               [--version] [--help]

        Environment variables (a command-line flag wins over its variable):
          WWO_PORT            port to listen on (default 6969)
          WWO_SHARED_WALLET   true/false: the room's starting rules (default true);
          WWO_SHARED_WORLD      the room owner can change them from the client
          WWO_SHARED_ITEMS
          WWO_SHARED_STORY
          WWO_SHARED_BAIT     the bait bag's All-Purpose Bait and Hyoi Pears are one room total
          WWO_SHARED_SPOILS   the spoils bag's counts (Joy Pendants, Chu Jellies...) are one room total
          WWO_SHARED_DELIVERY the delivery bag's quest items (trade goods, letters...) are one room bag
          WWO_SHARED_PROJECTILES  other players' bombs, cannon shots and arrows are real in your world
          WWO_ALLOW_WARPING   players can warp to each other (Warp to on the Room page)
          WWO_OWNER_KEY       a player who enters this key in the client becomes the room owner
          WWO_LOG_FILE        also log to this file (the console log is always on)
        """;

    public int Port { get; init; } = HubConstants.DefaultPort;
    public string? LogFile { get; init; }
    public bool SharedWallet { get; init; } = true;
    public bool SharedWorld { get; init; } = true;
    public bool SharedItems { get; init; } = true;
    public bool SharedStory { get; init; } = true;
    public bool SharedProjectiles { get; init; } = true;
    public bool SharedBait { get; init; } = true;
    public bool SharedSpoils { get; init; } = true;
    public bool SharedDelivery { get; init; } = true;
    public bool AllowWarping { get; init; } = true;

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
            SharedProjectiles = EnvBool(getEnv, SharedProjectilesVar, o.SharedProjectiles, errors),
            SharedBait = EnvBool(getEnv, SharedBaitVar, o.SharedBait, errors),
            SharedSpoils = EnvBool(getEnv, SharedSpoilsVar, o.SharedSpoils, errors),
            SharedDelivery = EnvBool(getEnv, SharedDeliveryVar, o.SharedDelivery, errors),
            AllowWarping = EnvBool(getEnv, AllowWarpingVar, o.AllowWarping, errors),
            OwnerKey = Env(getEnv, OwnerKeyVar),
            LogFile = Env(getEnv, LogFileVar),
        };

        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--log-file":
                    if (Value(args, ref i, arg, errors) is { } logFile) o = o with { LogFile = logFile };
                    break;
                // --host-token: what a client passes to the server it hosts. --owner-key: the same,
                // for someone running a dedicated server by hand.
                case "--host-token" or "--owner-key":
                    if (Value(args, ref i, arg, errors) is { } key) o = o with { OwnerKey = NullIfBlank(key) };
                    break;
                case "--no-shared-wallet": o = o with { SharedWallet = false }; break;
                case "--no-shared-world": o = o with { SharedWorld = false }; break;
                case "--no-shared-items": o = o with { SharedItems = false }; break;
                case "--no-shared-story": o = o with { SharedStory = false }; break;
                case "--no-shared-projectiles": o = o with { SharedProjectiles = false }; break;
                case "--no-shared-bait": o = o with { SharedBait = false }; break;
                case "--no-shared-spoils": o = o with { SharedSpoils = false }; break;
                case "--no-shared-delivery": o = o with { SharedDelivery = false }; break;
                case "--no-warping": o = o with { AllowWarping = false }; break;
                default:
                    // A bare number is the port (an out-of-range or overflowing one is an error).
                    // Anything else is left for ASP.NET's own command-line configuration
                    // (WebApplication.CreateBuilder(args)).
                    if (IsNumber(arg))
                        o = o with { Port = ParsePort(arg, "port", errors) ?? o.Port };
                    break;
            }
        }

        if (o.OwnerKey is { Length: > OwnerKeyCheck.MaxKeyLength })
            errors.Add($"the owner key is longer than {OwnerKeyCheck.MaxKeyLength} characters");

        return new Result(o, errors);
    }

    /// <summary>The value after a flag; an error when it's missing (the last argument, or another --flag).</summary>
    private static string? Value(IReadOnlyList<string> args, ref int i, string flag, List<string> errors)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            errors.Add($"{flag} needs a value");
            return null;
        }
        return args[++i];
    }

    private static bool IsNumber(string s)
    {
        var digits = s.StartsWith('-') || s.StartsWith('+') ? s[1..] : s;
        return digits.Length > 0 && digits.All(char.IsAsciiDigit);
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
