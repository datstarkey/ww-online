using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Server.Hubs;

/// <summary>
/// Relay hub — all players are equal peers.
/// Tracks connections for player count/names but knows nothing about game logic.
/// </summary>
public class GameHub : Hub<IGameHubClient>
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<GameHub>();
    private static readonly ConcurrentDictionary<string, PlayerInfo> ConnectedPlayers = new();

    /// <summary>Named players in the room (the /health endpoint and the shutdown log).</summary>
    public static int PlayerCount => ConnectedPlayers.Values.Count(p => !string.IsNullOrEmpty(p.PlayerName));

    private static string GetPlayerName(string connectionId)
    {
        return ConnectedPlayers.TryGetValue(connectionId, out var p) && !string.IsNullOrEmpty(p.PlayerName)
            ? p.PlayerName
            : $"Player({connectionId[..8]})";
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var connectionId = Context.ConnectionId;

        ConnectedPlayers[connectionId] = new PlayerInfo
        {
            ConnectionId = connectionId,
            JoinedAt = DateTime.UtcNow
        };

        Logger.Information("Player connected ({Count} online)", ConnectedPlayers.Count);

        try
        {
            await Clients.All.UpdatePlayerCount(ConnectedPlayers.Count);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to broadcast player count on connect");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var name = GetPlayerName(connectionId);

        ConnectedPlayers.TryRemove(connectionId, out _);
        OwnerKeyGate.Forget(connectionId);
        ItemsSeedGate.Forget(connectionId);
        StorySeedGate.Forget(connectionId);
        WalletSeedGate.Forget(connectionId);

        if (exception != null)
            Logger.Warning("{Player} disconnected (error: {Error}) ({Count} online)",
                name, exception.Message, ConnectedPlayers.Count);
        else
            Logger.Information("{Player} disconnected ({Count} online)",
                name, ConnectedPlayers.Count);

        try
        {
            await Task.WhenAll(
                Clients.All.PlayerLeft(connectionId),
                Clients.All.UpdatePlayerCount(ConnectedPlayers.Count),
                Clients.All.ReceiveRoomSettings(CurrentRoomSettings()) // room owner may have changed
            );
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to broadcast disconnect for {Player}", name);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// First call on every (re)connection: the version check, then the player name. A client with
    /// a different <see cref="HubConstants.ProtocolVersion"/> is refused with a reason it shows the
    /// player (and it disconnects); it never becomes a named player, so it can't own the room.
    /// </summary>
    public async Task<JoinResult> Join(JoinRequest request)
    {
        var connectionId = Context.ConnectionId;
        var result = ProtocolCheck.Evaluate(request, HubConstants.ProtocolVersion, AppInfo.Version);
        var playerName = ProtocolCheck.SanitizeName(request?.PlayerName);
        var clientVersion = ProtocolCheck.SanitizeVersion(request?.AppVersion);

        if (!result.Accepted)
        {
            Logger.Warning("[join] refused {Player} ({ConnectionId}): {Reason}",
                playerName.Length > 0 ? playerName : "(unnamed)", connectionId, result.Reason);
            return result;
        }

        if (!ConnectedPlayers.TryGetValue(connectionId, out var existing))
        {
            Logger.Warning("Join called for unknown connection {ConnectionId}", connectionId);
            return JoinResult.Reject("The server lost track of this connection — reconnect.",
                HubConstants.ProtocolVersion, AppInfo.Version);
        }

        if (playerName.Length == 0)
        {
            Logger.Information("[join] unnamed connection {ConnectionId} passed the version check ({AppName} {Version}, protocol {Protocol})",
                connectionId, AppInfo.DisplayName, clientVersion, request!.ProtocolVersion);
            return result;
        }

        ConnectedPlayers[connectionId] = new PlayerInfo
        {
            ConnectionId = connectionId,
            PlayerName = playerName,
            JoinedAt = existing.JoinedAt
        };

        Logger.Information("{Player} joined the game ({AppName} {Version}, protocol {Protocol}) ({Count} online)",
            playerName, AppInfo.DisplayName, clientVersion, request!.ProtocolVersion, ConnectedPlayers.Count);

        await Clients.All.PlayerJoined(connectionId, playerName);
        await Clients.All.ReceiveRoomSettings(CurrentRoomSettings()); // first named player becomes room owner (unless claimed)
        return result;
    }

    /// <summary>
    /// The join call of clients from before the version check. They can't be told why in their
    /// own UI, so log it and drop the connection rather than let an incompatible client relay.
    /// </summary>
    public Task SetPlayerName(string playerName)
    {
        Logger.Warning("[join] refused {Player} ({ConnectionId}): client predates the version check (protocol < {Protocol}) — they need to update {AppName}",
            ProtocolCheck.SanitizeName(playerName) is { Length: > 0 } n ? n : "(unnamed)",
            Context.ConnectionId, HubConstants.ProtocolVersion, AppInfo.DisplayName);
        Context.Abort();
        return Task.CompletedTask;
    }

    // ── Room rules ───────────────────────────────────────────────────────────

    private static readonly object RoomLock = new();
    private static RoomSettings _roomSettings = new();

    /// <summary>Server defaults from the command line (Program.cs), before anyone connects.</summary>
    public static void ConfigureRoomDefaults(bool sharedWallet, bool sharedWorld, bool sharedItems, bool sharedStory)
    {
        lock (RoomLock)
            _roomSettings = new RoomSettings
            {
                SharedWallet = sharedWallet, SharedWorld = sharedWorld, SharedItems = sharedItems, SharedStory = sharedStory,
            };
    }

    private static RoomSettings RoomRules
    {
        get { lock (RoomLock) return _roomSettings.Clone(); }
    }

    // A client that launches this server (its "Host" button) passes a secret token on the
    // command line and claims room ownership with it — otherwise another client racing to
    // connect first would become the owner. A dedicated server's admin sets the same secret as
    // its owner key (WWO_OWNER_KEY), and a player who enters it in the client's Owner key field
    // claims the room the same way. Ownership is keyed by connection id, so the owner
    // re-claims with the same token after every reconnect (new id), which moves it over.
    // Without a claimed owner (no key set, or the owner left) the earliest-joined named
    // player is the room owner. OwnerKeyCheck compares in constant time and stops listening to a
    // connection after a few wrong keys.
    private static readonly OwnerKeyCheck OwnerKeyGate = new();
    private static volatile string? _claimedOwnerId;

    public static void ConfigureHostToken(string? token) => OwnerKeyGate.Configure(token);

    public async Task ClaimRoomOwner(string token)
    {
        var player = GetPlayerName(Context.ConnectionId);
        switch (OwnerKeyGate.Check(Context.ConnectionId, token))
        {
            case OwnerKeyCheck.Outcome.Ignored:
                return;
            case OwnerKeyCheck.Outcome.Rejected:
                Logger.Warning("ClaimRoomOwner rejected for {Player}: {Reason}", player,
                    OwnerKeyGate.IsConfigured ? "wrong owner key" : "this server has no owner key");
                return;
            case OwnerKeyCheck.Outcome.RejectedLastAttempt:
                Logger.Warning("ClaimRoomOwner rejected for {Player}: {Reason} — {Max} failed attempts, ignoring further claims from this connection",
                    player, OwnerKeyGate.IsConfigured ? "wrong owner key" : "this server has no owner key", OwnerKeyCheck.MaxFailedAttempts);
                return;
        }
        var previous = _claimedOwnerId;
        _claimedOwnerId = Context.ConnectionId;
        if (previous == null)
            Logger.Information("[room] {Player} claimed room ownership (host token / owner key)", player);
        else if (previous != Context.ConnectionId)
            Logger.Information("[room] {Player} re-claimed room ownership on a new connection", player);
        await Clients.All.ReceiveRoomSettings(CurrentRoomSettings());
    }

    /// <summary>The owner who claimed the room with the host token, if still connected.</summary>
    private static PlayerInfo? ClaimedOwner() =>
        _claimedOwnerId is { } id && ConnectedPlayers.TryGetValue(id, out var claimed) ? claimed : null;

    /// <summary>
    /// May the caller seed / join a store that the claimed owner normally seeds? Logs the wait
    /// (once per joiner) and returns null when the caller must wait, else how it may proceed.
    /// </summary>
    private OwnerSeedGate.Decision? CheckSeedGate(OwnerSeedGate gate, bool isSeeded, string tag, string what)
    {
        var owner = ClaimedOwner();
        var decision = gate.Check(isSeeded, owner?.ConnectionId, Context.ConnectionId, out bool firstWait);
        if (decision != OwnerSeedGate.Decision.Wait) return decision;
        if (firstWait)
            Logger.Information("[{Tag}] {Player} is waiting for room owner {Owner} to seed the {What}",
                tag, GetPlayerName(Context.ConnectionId), owner?.PlayerName, what);
        return null;
    }

    /// <summary>Log who seeded a store (and, if it wasn't the owner, why).</summary>
    private void LogSeeded(string tag, string what, OwnerSeedGate.Decision decision, OwnerSeedGate gate, string detail)
    {
        var player = GetPlayerName(Context.ConnectionId);
        if (decision == OwnerSeedGate.Decision.OwnerTimedOut)
            Logger.Information("[{Tag}] room owner {Owner} hasn't joined in {Seconds:F0}s — {What} seeded by {Player}: {Detail}",
                tag, ClaimedOwner()?.PlayerName, gate.Timeout.TotalSeconds, what, player, detail);
        else
            Logger.Information("[{Tag}] {What} seeded by {Player}{Source}: {Detail}", tag, what, player,
                ClaimedOwner()?.ConnectionId == Context.ConnectionId ? " (room owner)" : " (first joiner, no claimed owner)",
                detail);
    }

    private static PlayerInfo? CurrentOwner() =>
        ClaimedOwner() ?? ConnectedPlayers.Values
            .Where(p => !string.IsNullOrEmpty(p.PlayerName))
            .OrderBy(p => p.JoinedAt)
            .FirstOrDefault();

    private static RoomSettings CurrentRoomSettings()
    {
        var settings = RoomRules;
        var owner = CurrentOwner();
        settings.OwnerConnectionId = owner?.ConnectionId ?? "";
        settings.OwnerName = owner?.PlayerName ?? "";
        return settings;
    }

    public Task<RoomSettings> GetRoomSettings() => Task.FromResult(CurrentRoomSettings());

    /// <summary>Room-owner-only: change the room rules. Everyone gets the new rules.</summary>
    public async Task SetRoomSettings(RoomSettings requested)
    {
        if (requested == null) return;
        if (CurrentOwner()?.ConnectionId != Context.ConnectionId)
        {
            Logger.Warning("SetRoomSettings rejected: {Player} is not the room owner", GetPlayerName(Context.ConnectionId));
            await Clients.Caller.ReceiveRoomSettings(CurrentRoomSettings()); // snap their UI back
            return;
        }

        bool walletTurnedOff;
        lock (RoomLock)
        {
            walletTurnedOff = _roomSettings.SharedWallet && !requested.SharedWallet;
            _roomSettings.SharedWallet = requested.SharedWallet;
            _roomSettings.SharedWorld = requested.SharedWorld;
            _roomSettings.SharedItems = requested.SharedItems;
            _roomSettings.SharedStory = requested.SharedStory;
        }
        if (walletTurnedOff)
        {
            // Everyone keeps their own rupees now. When it's back on the wallet re-seeds (owner
            // first) and every client rejoins, rather than resuming a stale total.
            Wallet.Reset();
            WalletSeedGate.Reset();
            Logger.Information("[wallet] shared wallet turned off — total cleared; it re-seeds when turned back on");
        }
        var preset = requested.MatchingPreset();
        Logger.Information("[room] owner {Player} set rules: {Rules}{Preset}",
            GetPlayerName(Context.ConnectionId), requested.RulesSummary(),
            preset == RoomPreset.Custom ? "" : $" (preset: {(preset == RoomPreset.FullSync ? "Full sync" : "Co-op")})");
        await Clients.All.ReceiveRoomSettings(CurrentRoomSettings());
    }

    public async Task SendPlayerGameState(GameState gameState)
    {
        if (gameState == null)
        {
            Logger.Warning("SendPlayerGameState called with null from {ConnectionId}",
                Context.ConnectionId);
            return;
        }

        if (!gameState.ValidateChecksum())
        {
            Logger.Warning("SendPlayerGameState rejected: invalid checksum from {ConnectionId}",
                Context.ConnectionId);
            return;
        }

        if (!gameState.Player.ArePositionsFinite())
        {
            Logger.Warning("SendPlayerGameState rejected: NaN/Infinity position from {ConnectionId}",
                Context.ConnectionId);
            return;
        }

        var connectionId = Context.ConnectionId;
        gameState.PlayerId = connectionId;
        await Clients.Others.ReceivePlayerGameState(connectionId, gameState);
    }

    public Task<List<PlayerInfo>> GetPlayers()
    {
        // Return only players that have set a name — unnamed connections aren't "in the game" yet.
        var list = ConnectedPlayers.Values
            .Where(p => !string.IsNullOrEmpty(p.PlayerName))
            .Select(p => new PlayerInfo
            {
                ConnectionId = p.ConnectionId,
                PlayerName = p.PlayerName,
                JoinedAt = p.JoinedAt
            })
            .ToList();
        return Task.FromResult(list);
    }

    public async Task SendPuppetData(PuppetData puppetData)
    {
        if (puppetData == null)
        {
            Logger.Warning("SendPuppetData called with null from {ConnectionId}",
                Context.ConnectionId);
            return;
        }

        if (!puppetData.IsValid())
        {
            Logger.Warning("SendPuppetData rejected: invalid position/rotation from {ConnectionId}",
                Context.ConnectionId);
            return;
        }

        puppetData.ClampUnknownValues();
        puppetData.PlayerId = Context.ConnectionId;
        PuppetRelayCounts.AddOrUpdate(Context.ConnectionId, 1, (_, n) => n + 1);
        await Clients.Others.ReceivePuppetData(puppetData);
    }

    // ── Shared world flags (chests, switches, collected items...) ─────────────

    private static readonly WorldFlagStore WorldFlags = new();

    /// <summary>
    /// A client reports its flags for one stage slot. New bits are merged into the shared world
    /// and the merged slot is pushed to everyone else, who OR it into their game.
    /// </summary>
    public async Task SendStageFlags(StageFlags flags)
    {
        if (flags == null || !flags.IsValid())
        {
            Logger.Warning("SendStageFlags rejected: invalid payload from {ConnectionId}", Context.ConnectionId);
            return;
        }
        if (!RoomRules.SharedWorld) return; // room rule off

        var result = WorldFlags.Merge(flags);
        if (result == null) return;

        Logger.Information("[world] {Player} added {Bits} flag bit(s) — {Flags}",
            GetPlayerName(Context.ConnectionId), result.Value.NewBits, result.Value.Merged);
        await Clients.Others.ReceiveStageFlags(result.Value.Merged);
    }

    /// <summary>Every non-empty stage slot of the shared world — called by clients on connect.</summary>
    public Task<List<StageFlags>> GetWorldFlags() => Task.FromResult(WorldFlags.Snapshot());

    // ── Shared wallet ────────────────────────────────────────────────────────

    private static readonly WalletStore Wallet = new();
    private static readonly OwnerSeedGate WalletSeedGate = new();

    /// <summary>
    /// A client attaches its game. The room owner's game seeds the shared wallet with its own
    /// rupees (with no claimed owner the first joiner does; see <see cref="OwnerSeedGate"/>);
    /// everyone gets the current total back and sets their game to it. Null when the rule is off
    /// or the wallet is still waiting for its owner (the client retries).
    /// </summary>
    public Task<int?> JoinWallet(int currentRupees)
    {
        if (!RoomRules.SharedWallet) return Task.FromResult<int?>(null); // room rule off: keep your own
        if (CheckSeedGate(WalletSeedGate, Wallet.IsSeeded, "wallet", "shared wallet") is not { } decision)
            return Task.FromResult<int?>(null);

        var (total, seeded) = Wallet.Join(currentRupees);
        if (seeded)
        {
            WalletSeedGate.Reset();
            LogSeeded("wallet", "shared wallet", decision, WalletSeedGate, $"{total} rupees");
        }
        return Task.FromResult<int?>(total);
    }

    /// <summary>A client gained (+) or spent (-) rupees. Applied to the shared total and pushed to everyone.</summary>
    public async Task SendRupeeDelta(int delta)
    {
        if (!RoomRules.SharedWallet) return; // room rule off
        if (!WalletStore.IsValidDelta(delta))
        {
            Logger.Warning("[wallet] SendRupeeDelta rejected: {Delta} from {Player}", delta, GetPlayerName(Context.ConnectionId));
            return;
        }
        if (Wallet.ApplyDelta(delta) is not int total)
        {
            // Unseeded (e.g. just turned back on): the sender must rejoin first — its delta
            // alone must not become the total.
            Logger.Warning("[wallet] SendRupeeDelta {Delta:+#;-#} from {Player} rejected: the wallet isn't seeded (client must rejoin)",
                delta, GetPlayerName(Context.ConnectionId));
            return;
        }
        Logger.Information("[wallet] {Player} {Delta:+#;-#} → {Total}", GetPlayerName(Context.ConnectionId), delta, total);
        await Clients.All.ReceiveRupeeTotal(total);
    }

    // ── Room inventory (shared items) ────────────────────────────────────────

    private static readonly RoomInventoryStore RoomItems = new();

    /// <summary>Validate + normalize an inventory from a client; null (and a warning) if it's malformed.</summary>
    private RoomInventory? AcceptInventory(RoomInventory? inventory, string method)
    {
        if (inventory == null || !inventory.IsValid())
        {
            Logger.Warning("[items] {Method} rejected: invalid payload from {Player}", method, GetPlayerName(Context.ConnectionId));
            return null;
        }
        return inventory.Clone().Normalize();
    }

    private static readonly OwnerSeedGate ItemsSeedGate = new();

    /// <summary>
    /// A client attached its game. The room owner's game seeds an empty room (with no claimed
    /// owner — a dedicated server — the first joiner seeds it; if the owner hasn't joined within
    /// <see cref="OwnerSeedGate.Timeout"/> of the first joiner, a joiner does). After that, a joiner's items MERGE
    /// UP into the room (bits OR, levels MAX, empty slots filled) and it gets the resulting room
    /// back to apply exactly. Returns null when the rule is off, the payload is bad, or the room is
    /// still waiting for its owner to seed it (the client retries, or picks up the seed push).
    /// </summary>
    public async Task<RoomInventory?> JoinRoomInventory(RoomInventory local)
    {
        if (!RoomRules.SharedItems) return null; // room rule off: keep your own items
        var accepted = AcceptInventory(local, nameof(JoinRoomInventory));
        if (accepted == null) return null;

        if (CheckSeedGate(ItemsSeedGate, RoomItems.IsSeeded, "items", "room inventory") is not { } decision)
            return null;

        var player = GetPlayerName(Context.ConnectionId);
        var result = RoomItems.Merge(accepted);
        if (result.Seeded)
        {
            ItemsSeedGate.Reset();
            LogSeeded("items", "room inventory", decision, ItemsSeedGate, result.Room.Summary());
        }
        else if (result.Changes.Count > 0)
            Logger.Information("[items] {Player} joined and brought: {Changes} (rev {Rev})", player,
                string.Join(", ", result.Changes), result.Room.Revision);
        else
            Logger.Information("[items] {Player} joined the room inventory (nothing new, rev {Rev})", player, result.Room.Revision);

        if (result.Seeded || result.Changes.Count > 0)
            await Clients.Others.ReceiveRoomInventory(result.Room);
        return result.Room;
    }

    /// <summary>A client gained items / upgrades. Merged into the room; if anything is new everyone gets the room.</summary>
    public async Task SendInventoryGains(RoomInventory gains)
    {
        if (!RoomRules.SharedItems) return; // room rule off
        var accepted = AcceptInventory(gains, nameof(SendInventoryGains));
        if (accepted == null) return;
        if (!RoomItems.IsSeeded) return; // only joined clients send gains; never let one seed the room

        var result = RoomItems.Merge(accepted);
        if (result.Changes.Count == 0) return; // the room already had all of it
        Logger.Information("[items] {Player} gained: {Changes} (rev {Rev})",
            GetPlayerName(Context.ConnectionId), string.Join(", ", result.Changes), result.Room.Revision);
        await Clients.All.ReceiveRoomInventory(result.Room);
    }

    /// <summary>Room-owner-only: replace the room inventory exactly (can remove or downgrade). Everyone gets it.</summary>
    public async Task SetRoomInventory(RoomInventory exact)
    {
        if (!RoomRules.SharedItems) return; // room rule off
        var player = GetPlayerName(Context.ConnectionId);
        if (CurrentOwner()?.ConnectionId != Context.ConnectionId)
        {
            Logger.Warning("[items] SetRoomInventory rejected: {Player} is not the room owner", player);
            var current = RoomItems.Snapshot();
            if (current != null) await Clients.Caller.ReceiveRoomInventory(current); // snap their view back
            return;
        }
        var accepted = AcceptInventory(exact, nameof(SetRoomInventory));
        if (accepted == null) return;

        var result = RoomItems.Replace(accepted);
        if (result.Changes.Count == 0 && !result.Seeded) return;
        if (result.Seeded) ItemsSeedGate.Reset();
        Logger.Information("[items] room owner {Player} set room items: {Changes} (rev {Rev})",
            player, result.Changes.Count > 0 ? string.Join(", ", result.Changes) : "(seeded, empty)", result.Room.Revision);
        await Clients.All.ReceiveRoomInventory(result.Room);
    }

    /// <summary>The current room inventory (null if the rule is off or nobody has joined yet).</summary>
    public Task<RoomInventory?> GetRoomInventory() =>
        Task.FromResult(RoomRules.SharedItems ? RoomItems.Snapshot() : null);

    // ── Room story (shared event flags) ──────────────────────────────────────

    private static readonly StoryFlagStore RoomStory = new();

    private static readonly OwnerSeedGate StorySeedGate = new();

    /// <summary>Validate + mask story flags from a client; null (and a warning) if malformed.</summary>
    private StoryFlags? AcceptStory(StoryFlags? flags, string method)
    {
        if (flags == null || !flags.IsValid())
        {
            Logger.Warning("[story] {Method} rejected: invalid payload from {Player}", method, GetPlayerName(Context.ConnectionId));
            return null;
        }
        return flags.Clone().Normalize();
    }

    /// <summary>Log the flags a player added, plus a WARNING line for any with a known side effect.</summary>
    private static void LogStoryAdded(string player, string verb, StoryFlags added)
    {
        Logger.Information("[story] {Player} {Verb} {Count} flag(s): {Flags}", player, verb, added.BitCount, added.Describe());
        foreach (var f in added.RiskyFlags())
            Logger.Warning("[story] WARNING {Flag} from {Player}: {Effect} for players who receive it",
                f.Name, player, EventFlagCatalog.RiskEffect(f.Id));
    }

    /// <summary>
    /// A client attached its game. The room owner's game seeds an empty room story (with no claimed
    /// owner — a dedicated server — the first joiner seeds it; if the owner hasn't joined within
    /// <see cref="OwnerSeedGate.Timeout"/> of the first joiner, a joiner does). After that a joiner's flags MERGE UP
    /// (OR, syncable bits only) and it gets the whole room back to apply. Returns null when the rule
    /// is off, the payload is bad, or the room is still waiting for its owner (the client retries).
    /// </summary>
    public async Task<StoryFlags?> JoinRoomStory(StoryFlags local)
    {
        if (!RoomRules.SharedStory) return null; // room rule off: keep your own story
        var accepted = AcceptStory(local, nameof(JoinRoomStory));
        if (accepted == null) return null;

        if (CheckSeedGate(StorySeedGate, RoomStory.IsSeeded, "story", "room story") is not { } decision)
            return null;

        var player = GetPlayerName(Context.ConnectionId);
        var result = RoomStory.Merge(accepted);
        if (result.Seeded)
        {
            StorySeedGate.Reset();
            LogSeeded("story", "room story", decision, StorySeedGate, $"{result.Room.BitCount} flag(s)");
        }
        else if (!result.Added.IsEmpty)
            LogStoryAdded(player, "joined and brought", result.Added);
        else
            Logger.Information("[story] {Player} joined the room story (nothing new, {Count} flag(s) in room)",
                player, result.Room.BitCount);

        if (result.Seeded || !result.Added.IsEmpty)
            await Clients.Others.ReceiveStoryFlags(result.Room);
        return result.Room;
    }

    /// <summary>A client set new story flags. Merged into the room; if anything is new everyone else gets the room.</summary>
    public async Task SendStoryFlags(StoryFlags local)
    {
        if (!RoomRules.SharedStory) return; // room rule off
        var accepted = AcceptStory(local, nameof(SendStoryFlags));
        if (accepted == null) return;
        if (!RoomStory.IsSeeded) return; // nobody has joined yet: the sender's JoinRoomStory carries these

        var result = RoomStory.Merge(accepted);
        if (result.Added.IsEmpty) return; // the room already had all of it
        LogStoryAdded(GetPlayerName(Context.ConnectionId), "set", result.Added);
        await Clients.Others.ReceiveStoryFlags(result.Room);
    }

    // Per-connection count of relayed puppet packets since the last stats line.
    private static readonly ConcurrentDictionary<string, int> PuppetRelayCounts = new();

    /// <summary>
    /// Log one "[stats]" line summarising relayed puppet traffic since the last call, e.g.
    /// "Player1: 200 (20/s)". Silent when nobody is connected. Called on a timer by Program.
    /// </summary>
    public static void LogRelayStats(TimeSpan interval)
    {
        if (ConnectedPlayers.IsEmpty) return;

        var parts = ConnectedPlayers.Keys.Select(id =>
        {
            PuppetRelayCounts.TryRemove(id, out var n);
            return $"{GetPlayerName(id)}: {n} ({n / interval.TotalSeconds:F0}/s)";
        });
        Logger.Information("[stats] {Online} online, puppet packets relayed — {Counts}",
            ConnectedPlayers.Count, string.Join(", ", parts));
    }
}
