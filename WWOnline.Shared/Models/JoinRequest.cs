namespace WWOnline.Shared.Models;

/// <summary>What a client sends first on every (re)connection (<see cref="Hubs.HubConstants.Join"/>).</summary>
public class JoinRequest
{
    /// <summary>The player's display name. May be empty (the connection then stays unnamed).</summary>
    public string PlayerName { get; set; } = "";

    /// <summary>The client's <see cref="Hubs.HubConstants.ProtocolVersion"/>.</summary>
    public int ProtocolVersion { get; set; }

    /// <summary>The client's app version (<see cref="AppInfo.Version"/>), for messages and logs only.</summary>
    public string AppVersion { get; set; } = "";
}

/// <summary>The server's answer to a <see cref="JoinRequest"/>.</summary>
public class JoinResult
{
    public bool Accepted { get; set; }

    /// <summary>Why the join was refused, written for the player ("…update the app"). Empty when accepted.</summary>
    public string Reason { get; set; } = "";

    /// <summary>The room's (server's) protocol and app version.</summary>
    public int ServerProtocolVersion { get; set; }
    public string ServerAppVersion { get; set; } = "";

    public static JoinResult Accept(int serverProtocol, string serverVersion) =>
        new() { Accepted = true, ServerProtocolVersion = serverProtocol, ServerAppVersion = serverVersion };

    public static JoinResult Reject(string reason, int serverProtocol, string serverVersion) =>
        new() { Accepted = false, Reason = reason, ServerProtocolVersion = serverProtocol, ServerAppVersion = serverVersion };
}
