namespace WWOnline.Shared.Models;

public class PlayerInfo
{
    public string ConnectionId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>From the player's join (<see cref="JoinRequest.Capabilities"/>, known flags only).</summary>
    public ClientCapabilities Capabilities { get; set; }
}
