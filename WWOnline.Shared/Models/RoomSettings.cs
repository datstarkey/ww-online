namespace WWOnline.Shared.Models;

/// <summary>
/// Room rules (shared wallet / world / items / story), owned by the server. Defaults come from the server's command line; the room owner (the
/// player who started the server, else the earliest-joined player still connected) can change them at runtime. Clients pause the matching
/// sync while a rule is off, and the server ignores that sync's messages.
/// </summary>
public class RoomSettings
{
    /// <summary>Everyone shares one rupee total.</summary>
    public bool SharedWallet { get; set; } = true;

    /// <summary>Chests, switches, collected items etc. are OR-merged across players.</summary>
    public bool SharedWorld { get; set; } = true;

    /// <summary>Items, equipment, songs, pearls, shards and upgrades are owned by the whole room (RoomInventory).</summary>
    public bool SharedItems { get; set; } = true;

    /// <summary>Story / cutscene / side-quest / NPC event flags are OR-merged across players (StoryFlags).</summary>
    public bool SharedStory { get; set; } = true;

    /// <summary>Connection id of the current room owner (read-only for clients; set by the server).</summary>
    public string OwnerConnectionId { get; set; } = "";

    public string OwnerName { get; set; } = "";

    public RoomSettings Clone() => (RoomSettings)MemberwiseClone();

    /// <summary>"Full sync" = every rule on; "Co-op" = every rule off (players just see each other).</summary>
    public RoomPreset MatchingPreset() => (SharedWallet, SharedWorld, SharedItems, SharedStory) switch
    {
        (true, true, true, true) => RoomPreset.FullSync,
        (false, false, false, false) => RoomPreset.Coop,
        _ => RoomPreset.Custom,
    };

    /// <summary>Set every rule on (Full sync) or off (Co-op). Custom leaves the rules alone. Returns this.</summary>
    public RoomSettings ApplyPreset(RoomPreset preset)
    {
        if (preset == RoomPreset.Custom) return this;
        bool on = preset == RoomPreset.FullSync;
        SharedWallet = SharedWorld = SharedItems = SharedStory = on;
        return this;
    }

    public string RulesSummary() =>
        $"shared wallet {OnOff(SharedWallet)}, shared world {OnOff(SharedWorld)}, shared items {OnOff(SharedItems)}, " +
        $"shared story {OnOff(SharedStory)}";

    private static string OnOff(bool b) => b ? "ON" : "OFF";
}

/// <summary>A named combination of all four room rules, shown on the dashboard.</summary>
public enum RoomPreset
{
    /// <summary>Any other mix of rules.</summary>
    Custom,

    /// <summary>Wallet, world, items and story all shared.</summary>
    FullSync,

    /// <summary>Nothing shared: players just see each other.</summary>
    Coop,
}
