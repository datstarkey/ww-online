using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// Represents the player's current state in Wind Waker
/// </summary>
public class PlayerState
{
    // Health and Resources
    [JsonPropertyName("maxHealth")]
    public ushort MaxHealth { get; set; }      // Each heart = 4 units

    [JsonPropertyName("currentHealth")]
    public ushort CurrentHealth { get; set; }

    [JsonPropertyName("rupeeCount")]
    public ushort RupeeCount { get; set; }

    [JsonPropertyName("maxMagic")]
    public byte MaxMagic { get; set; }

    [JsonPropertyName("currentMagic")]
    public byte CurrentMagic { get; set; }

    // Equipment
    [JsonPropertyName("currentSword")]
    public byte CurrentSword { get; set; }

    [JsonPropertyName("currentShield")]
    public byte CurrentShield { get; set; }

    [JsonPropertyName("powerBracelets")]
    public byte PowerBracelets { get; set; }

    [JsonPropertyName("wallet")]
    public byte Wallet { get; set; }

    // Time
    [JsonPropertyName("currentTime")]
    public float CurrentTime { get; set; }

    // Consumables
    [JsonPropertyName("currentArrows")]
    public byte CurrentArrows { get; set; }

    [JsonPropertyName("currentBombs")]
    public byte CurrentBombs { get; set; }

    [JsonPropertyName("maxArrows")]
    public byte MaxArrows { get; set; }

    [JsonPropertyName("maxBombs")]
    public byte MaxBombs { get; set; }

    // Location
    [JsonPropertyName("currentSector")]
    public byte CurrentSector { get; set; }

    [JsonPropertyName("previousSector")]
    public byte PreviousSector { get; set; }

    // Player Position
    [JsonPropertyName("positionX")]
    public float PositionX { get; set; }

    [JsonPropertyName("positionY")]
    public float PositionY { get; set; }

    [JsonPropertyName("positionZ")]
    public float PositionZ { get; set; }

    // Player Status
    [JsonPropertyName("statusFlags1")]
    public uint StatusFlags1 { get; set; }     // First player status bitfield

    [JsonPropertyName("statusFlags2")]
    public uint StatusFlags2 { get; set; }     // Second player status bitfield

    // Helper Properties
    public int HeartContainers => MaxHealth / 4;
    public int CurrentHearts => CurrentHealth / 4;
    public float CurrentHeartFraction => (CurrentHealth % 4) / 4f;

    public bool ArePositionsFinite() =>
        float.IsFinite(PositionX) && float.IsFinite(PositionY) && float.IsFinite(PositionZ);

    public PlayerState Clone() => (PlayerState)MemberwiseClone();
}
