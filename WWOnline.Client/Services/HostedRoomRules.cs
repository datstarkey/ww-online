using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The rules a room this app hosts starts with: the ones the host last left it on
/// (<see cref="GameSettings.HostedRoomRules"/>, saved while hosting as the owner changes them), passed to the server
/// the app starts as its <c>--no-shared-*</c> / <c>--no-warping</c> flags (WWOnline.Server ServerOptions). Only the
/// rules are kept, never the room's state (items, story, wallet, bags): those come back from the players' saves.
/// </summary>
public static class HostedRoomRules
{
    /// <summary>The server flags for <paramref name="rules"/> (a leading space each), or "" when never saved: the
    /// server's own defaults, every rule on.</summary>
    public static string ServerArgs(RoomSettings? rules)
    {
        if (rules == null) return "";
        var flags = new List<string>();
        if (!rules.SharedWallet) flags.Add("--no-shared-wallet");
        if (!rules.SharedWorld) flags.Add("--no-shared-world");
        if (!rules.SharedItems) flags.Add("--no-shared-items");
        if (!rules.SharedStory) flags.Add("--no-shared-story");
        if (!rules.SharedBait) flags.Add("--no-shared-bait");
        if (!rules.SharedSpoils) flags.Add("--no-shared-spoils");
        if (!rules.SharedDelivery) flags.Add("--no-shared-delivery");
        if (!rules.SharedProjectiles) flags.Add("--no-shared-projectiles");
        if (!rules.AllowWarping) flags.Add("--no-warping");
        return string.Concat(flags.Select(f => " " + f));
    }

    /// <summary>The rules of <paramref name="settings"/> without the owner (what is saved).</summary>
    public static RoomSettings RulesOnly(RoomSettings settings)
    {
        var rules = settings.Clone();
        rules.OwnerConnectionId = "";
        rules.OwnerName = "";
        return rules;
    }

    /// <summary>The same nine rules (the owner doesn't count).</summary>
    public static bool SameRules(RoomSettings? a, RoomSettings? b) =>
        a != null && b != null &&
        a.SharedWallet == b.SharedWallet && a.SharedWorld == b.SharedWorld && a.SharedItems == b.SharedItems &&
        a.SharedStory == b.SharedStory && a.SharedBait == b.SharedBait && a.SharedSpoils == b.SharedSpoils &&
        a.SharedDelivery == b.SharedDelivery && a.SharedProjectiles == b.SharedProjectiles && a.AllowWarping == b.AllowWarping;
}
