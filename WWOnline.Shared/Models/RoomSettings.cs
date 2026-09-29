namespace WWOnline.Shared.Models;

/// <summary>
/// Room rules (shared wallet / world / items / story, other players' projectiles), owned by the server. Defaults come from the server's command line; the room owner (the
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

    /// <summary>
    /// Other players' bombs, boat-cannon shots and arrows are real in your world: they fly, explode and hit
    /// your enemies and walls. When off the server drops those events and clients don't spawn them; the
    /// carried-bomb model and aim poses still show. It shares no progress (only what players see each other
    /// do in combat), so Co-op keeps it on.
    /// </summary>
    public bool SharedProjectiles { get; set; } = true;

    /// <summary>Connection id of the current room owner (read-only for clients; set by the server).</summary>
    public string OwnerConnectionId { get; set; } = "";

    public string OwnerName { get; set; } = "";

    public RoomSettings Clone() => (RoomSettings)MemberwiseClone();

    /// <summary>
    /// "Full sync" = every rule on; "Co-op" = the four progress rules (wallet, world, items, story) off and
    /// other players' projectiles on (players see each other and fight together, progress stays per save).
    /// </summary>
    public RoomPreset MatchingPreset() => (SharedWallet, SharedWorld, SharedItems, SharedStory, SharedProjectiles) switch
    {
        (true, true, true, true, true) => RoomPreset.FullSync,
        (false, false, false, false, true) => RoomPreset.Coop,
        _ => RoomPreset.Custom,
    };

    /// <summary>
    /// Full sync: every rule on. Co-op: the progress rules off, projectiles on. Custom leaves the rules
    /// alone. Returns this.
    /// </summary>
    public RoomSettings ApplyPreset(RoomPreset preset)
    {
        if (preset == RoomPreset.Custom) return this;
        bool on = preset == RoomPreset.FullSync;
        SharedWallet = SharedWorld = SharedItems = SharedStory = on;
        SharedProjectiles = true;
        return this;
    }

    public string RulesSummary() =>
        $"shared wallet {OnOff(SharedWallet)}, shared world {OnOff(SharedWorld)}, shared items {OnOff(SharedItems)}, " +
        $"shared story {OnOff(SharedStory)}, other players' projectiles {OnOff(SharedProjectiles)}";

    private static string OnOff(bool b) => b ? "ON" : "OFF";
}

/// <summary>A named combination of the room rules, shown on the dashboard.</summary>
public enum RoomPreset
{
    /// <summary>Any other mix of rules.</summary>
    Custom,

    /// <summary>Wallet, world, items and story all shared; other players' projectiles on.</summary>
    FullSync,

    /// <summary>No progress shared (wallet, world, items, story off): players see each other, and
    /// other players' projectiles stay on.</summary>
    Coop,
}
