using WWOnline.Shared.Models;

namespace WWOnline.Shared.Hubs;

/// <summary>
/// Decides whether a client may join a room: same <see cref="HubConstants.ProtocolVersion"/> or
/// not. Pure (no hub state) so the server and tests share it, and the wording lives in one place.
/// </summary>
public static class ProtocolCheck
{
    public const int MaxPlayerNameLength = 32;
    public const int MaxAppVersionLength = 64;

    /// <summary>
    /// Check a join request against this server's protocol/version. Never throws; a null or
    /// malformed request is rejected with a reason.
    /// </summary>
    public static JoinResult Evaluate(JoinRequest? request, int serverProtocol, string serverVersion)
    {
        if (request == null)
            return JoinResult.Reject("Invalid join request.", serverProtocol, serverVersion);

        int clientProtocol = request.ProtocolVersion;
        string clientVersion = SanitizeVersion(request.AppVersion);

        if (clientProtocol == serverProtocol)
            return JoinResult.Accept(serverProtocol, serverVersion);

        // Range-check before comparing (a client int can be anything); <= 0 means a broken or
        // hand-rolled client, not an "older app".
        if (clientProtocol <= 0)
            return JoinResult.Reject(
                $"Room runs {AppInfo.DisplayName} {serverVersion} (protocol {serverProtocol}); your client sent no valid protocol version — update the app.",
                serverProtocol, serverVersion);

        string room = $"Room runs {AppInfo.DisplayName} {serverVersion} (protocol {serverProtocol}); " +
                      $"you have {AppInfo.DisplayName} {clientVersion} (protocol {clientProtocol})";
        string reason = clientProtocol < serverProtocol
            ? room + " — update the app."
            : room + " — the room's host needs to update WW-Online.";
        return JoinResult.Reject(reason, serverProtocol, serverVersion);
    }

    /// <summary>A client-supplied version string made safe to log and show ("unknown" if absent/bad).</summary>
    public static string SanitizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "unknown";
        version = version.Trim();
        if (version.Length > MaxAppVersionLength) version = version[..MaxAppVersionLength];
        foreach (var c in version)
            if (!(char.IsLetterOrDigit(c) || c is '.' or '-' or '+'))
                return "unknown";
        return version;
    }

    /// <summary>
    /// A client-supplied player name without control characters (newlines would forge log lines
    /// and break the player list), trimmed and capped without splitting a surrogate pair; empty if absent.
    /// </summary>
    public static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        if (name.Any(char.IsControl))
            name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        name = name.Trim();
        if (name.Length <= MaxPlayerNameLength) return name;
        int cut = char.IsHighSurrogate(name[MaxPlayerNameLength - 1]) ? MaxPlayerNameLength - 1 : MaxPlayerNameLength;
        return name[..cut].TrimEnd();
    }
}
