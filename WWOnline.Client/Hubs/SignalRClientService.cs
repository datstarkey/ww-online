using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Hubs;

public class SignalRClientService : IAsyncDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<SignalRClientService>();
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private HubConnection? _hubConnection;

    public HubConnection? Connection => _hubConnection;
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    // Events for hub callbacks
    public event Action<PuppetData>? PuppetDataReceived;
    public event Action<string, GameState>? PlayerGameStateReceived;
    public event Action<string, string>? PlayerJoined;
    /// <summary>Everyone else in the room (connection id → name), kept up to date from the moment we
    /// connect: services that start later (GameSyncService starts when Dolphin attaches, after the
    /// join events already fired) read names from here instead of relying on PlayerJoined.</summary>
    public IReadOnlyDictionary<string, string> Players => _players;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _players = new();
    public event Action<string>? PlayerLeft;
    public event Action<int>? PlayerCountUpdated;
    public event Action<StageFlags>? StageFlagsReceived;
    public event Action<int>? RupeeTotalReceived;
    public event Action<BaitCounts>? BaitTotalReceived;
    public event Action<SpoilsCounts>? SpoilsTotalReceived;
    public event Action<RoomSettings>? RoomSettingsReceived;
    public event Action<RoomInventory>? RoomInventoryReceived;
    public event Action<StoryFlags>? StoryFlagsReceived;
    public event Action<RoomSwitches>? RoomSwitchesReceived;
    public event Action<PlayerEvent>? PlayerEventReceived;

    // Connection lifecycle. ConnectionLost fires from SignalR's Reconnecting / Closed callbacks
    // (wipe per-connection state). Connected fires after EVERY successful connect — the initial
    // ConnectAsync and each automatic reconnect — once this connection is fully set up (player
    // name sent, room ownership re-claimed, existing players announced), so sync services can
    // rejoin / refetch from a clean per-connection state.
    public event Action? ConnectionLost;
    public event Action? Connected;

    /// <summary>
    /// The room refused us (different protocol version, e.g. "Room runs WW-Online 0.3.0 (protocol 5);
    /// you have ... - update the app"). Fires on the initial connect (ConnectAsync also returns the
    /// reason as its failure message) and on an automatic reconnect to a server that was updated
    /// meanwhile; either way this client disconnects. The string is ready to show the player.
    /// </summary>
    public event Action<string>? JoinRejected;

    /// <summary>The last refusal reason (null after a successful join).</summary>
    public string? LastJoinRejection { get; private set; }

    /// <summary>The room's answer to our last join: its app version and protocol (null before the first join).</summary>
    public JoinResult? LastJoinResult { get; private set; }

    // Remembered so an automatic reconnect (new connection id) can re-identify us to the server.
    private string _playerName = "";
    private string? _hostToken;

    /// <summary>True when the host field holds a URL ("https://...") rather than a host name or IP.</summary>
    public static bool IsUrl(string? host) => host?.Contains("://", StringComparison.Ordinal) == true;

    /// <summary>
    /// The hub URL for what the player typed. A plain host ("192.168.1.20", "wwo.example.com")
    /// means http://host:port. An http:// or https:// URL ("https://wwo.example.com", for a server
    /// behind a TLS reverse proxy; docs/self-hosting.md) is used as it is and <paramref name="port"/>
    /// is ignored: without a port in the URL that's 443 for https and 80 for http. The hub path is
    /// added when the URL has no path. Any other scheme (ws://...) is refused with a reason.
    /// </summary>
    public static bool TryBuildHubUrl(string host, int port, out string url, out string error)
    {
        host = host.Trim();
        url = "";
        error = "";
        if (host.Length == 0)
        {
            error = "Enter the server's address.";
            return false;
        }
        if (IsUrl(host))
        {
            if (!Uri.TryCreate(host, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host))
            {
                error = $"'{host}' isn't a server address WW-Online can use. Enter a host name or IP (and the port), " +
                        "or a URL starting with http:// or https://.";
                return false;
            }
            var path = uri.AbsolutePath.TrimEnd('/');
            url = uri.GetLeftPart(UriPartial.Authority) + (path.Length == 0 ? HubConstants.HubPath : path);
            return true;
        }
        if (port is < 1 or > 65535)
        {
            error = "Invalid port number";
            return false;
        }
        url = $"http://{host}:{port}{HubConstants.HubPath}";
        return true;
    }

    /// <param name="hostToken">Claims room ownership: the token passed to a server this client
    /// launched, or a dedicated server's owner key.</param>
    public async Task<(bool success, string message)> ConnectAsync(string host, int port, string playerName = "", string? hostToken = null)
    {
        if (!TryBuildHubUrl(host, port, out var hubUrl, out var addressError))
        {
            Logger.Warning("Not connecting to '{Host}' (port {Port}): {Error}", host, port, addressError);
            return (false, addressError);
        }

        await _connectionSemaphore.WaitAsync();
        try
        {
            Logger.Information("Attempting to connect to SignalR server at {Url} as player: {PlayerName}", hubUrl, playerName);

            if (_hubConnection != null)
            {
                Logger.Debug("Existing connection found, disconnecting first");
                await DisconnectInternalAsync();
            }

            _playerName = playerName;
            _hostToken = string.IsNullOrWhiteSpace(hostToken) ? null : hostToken.Trim();
            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();
            _hubConnection = connection;

            SetupConnectionHandlers(connection);

            await connection.StartAsync();
            if (await OnConnectionEstablishedAsync(connection) is { } rejection)
            {
                await DisconnectInternalAsync();
                return (false, rejection);
            }

            Logger.Information("Successfully connected to SignalR server at {Url}", hubUrl);
            return (true, $"Connected to server at {hubUrl}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to connect to SignalR server at {Url}", hubUrl);
            return (false, $"Failed to connect: {ex.Message}");
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Set up a freshly started (or automatically reconnected) connection: join (version check +
    /// player name), re-claim room ownership if we host, announce the players already there, then
    /// raise <see cref="Connected"/>. Returns the refusal reason if the room turned us away (nothing
    /// else is done then); otherwise each step is best-effort (logged), so one failure doesn't block the rest.
    /// </summary>
    private async Task<string?> OnConnectionEstablishedAsync(HubConnection connection)
    {
        if (await JoinAsync(connection) is { } rejection)
        {
            LastJoinRejection = rejection;
            Logger.Error("[join] the room refused this client: {Reason}", rejection);
            try { JoinRejected?.Invoke(rejection); }
            catch (Exception ex) { Logger.Error(ex, "A JoinRejected handler failed"); }
            return rejection;
        }
        LastJoinRejection = null;

        // Ownership is keyed by connection id on the server, so it must be re-claimed after every
        // (re)connect — before Connected, so the sync services rejoin as the owner.
        if (!string.IsNullOrEmpty(_hostToken))
        {
            try { await connection.InvokeAsync(HubConstants.ClaimRoomOwner, _hostToken); }
            catch (Exception ex) { Logger.Warning(ex, "Failed to claim room ownership"); }
        }

        // Fetch existing players so we see peers who joined before us (or while we were away).
        try
        {
            var existing = await connection.InvokeAsync<List<PlayerInfo>>(HubConstants.GetPlayers);
            var selfId = connection.ConnectionId;
            foreach (var p in existing)
            {
                if (p.ConnectionId == selfId) continue;
                Logger.Information("Existing player: {ConnectionId} ({PlayerName})", p.ConnectionId, p.PlayerName);
                _players[p.ConnectionId] = p.PlayerName;
                PlayerJoined?.Invoke(p.ConnectionId, p.PlayerName);
            }
        }
        catch (Exception ex) { Logger.Warning(ex, "Failed to fetch existing player list"); }

        try { Connected?.Invoke(); }
        catch (Exception ex) { Logger.Error(ex, "A Connected handler failed"); }
        return null;
    }

    /// <summary>
    /// The version handshake. Returns null when admitted, else the reason to show the player.
    /// Transport errors are logged and treated as admitted (the connection is live; the old
    /// best-effort behaviour), except a server without the Join method, which is too old to play with.
    /// </summary>
    private async Task<string?> JoinAsync(HubConnection connection)
    {
        var request = new JoinRequest
        {
            PlayerName = _playerName,
            ProtocolVersion = HubConstants.ProtocolVersion,
            AppVersion = AppInfo.Version,
        };
        try
        {
            var result = await connection.InvokeAsync<JoinResult?>(HubConstants.Join, request);
            if (result == null) return "The room sent an empty answer to the join request.";
            LastJoinResult = result;
            if (!result.Accepted)
                return string.IsNullOrWhiteSpace(result.Reason) ? "The room refused this client." : result.Reason;

            Logger.Information("[join] joined as '{PlayerName}': room runs {AppName} {ServerVersion} (protocol {Protocol}), this client {ClientVersion}",
                _playerName, AppInfo.DisplayName, result.ServerAppVersion, result.ServerProtocolVersion, AppInfo.Version);
            return null;
        }
        catch (HubException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            // Servers from before the version check have no Join method.
            return $"The room runs an older {AppInfo.DisplayName} without a version check; you have {AppInfo.Version} " +
                   $"(protocol {HubConstants.ProtocolVersion}) — the room's host needs to update {AppInfo.DisplayName}.";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[join] join call failed");
            return null;
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            await DisconnectInternalAsync();
            _players.Clear();
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Internal disconnect without acquiring the semaphore. Called from within ConnectAsync
    /// where the semaphore is already held.
    /// </summary>
    private async Task DisconnectInternalAsync()
    {
        if (_hubConnection != null)
        {
            Logger.Information("Disconnecting from SignalR server");
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            Logger.Debug("SignalR connection disposed");
        }
    }

    /// <summary>Disconnect, but only if <paramref name="connection"/> is still the live one (a new ConnectAsync may have replaced it).</summary>
    private async Task DisconnectIfCurrentAsync(HubConnection connection)
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (ReferenceEquals(_hubConnection, connection))
                await DisconnectInternalAsync();
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private void SetupConnectionHandlers(HubConnection connection)
    {
        connection.Reconnecting += (error) =>
        {
            Logger.Warning(error, "SignalR connection lost, attempting to reconnect");
            _players.Clear();
            ConnectionLost?.Invoke();
            return Task.CompletedTask;
        };

        connection.Reconnected += async (connectionId) =>
        {
            Logger.Information("SignalR connection restored. ConnectionId: {ConnectionId}", connectionId);
            try
            {
                if (await OnConnectionEstablishedAsync(connection) != null)
                {
                    // The server was replaced by an incompatible version while we were away. Drop the
                    // connection off this callback (disposing it from inside its own handler can deadlock).
                    _ = Task.Run(async () =>
                    {
                        try { await DisconnectIfCurrentAsync(connection); }
                        catch (Exception ex) { Logger.Warning(ex, "Failed to drop the refused connection"); }
                    });
                }
            }
            catch (Exception ex) { Logger.Warning(ex, "Failed to set up the reconnected connection"); }
        };

        connection.Closed += (error) =>
        {
            if (error != null)
                Logger.Error(error, "SignalR connection closed with error");
            else
                Logger.Information("SignalR connection closed");
            _players.Clear();
            ConnectionLost?.Invoke();
            return Task.CompletedTask;
        };

        // Register hub callback handlers
        connection.On<PuppetData>("ReceivePuppetData", data =>
        {
            PuppetDataReceived?.Invoke(data);
        });

        connection.On<string, GameState>("ReceivePlayerGameState", (senderId, state) =>
        {
            PlayerGameStateReceived?.Invoke(senderId, state);
        });

        connection.On<string, string>("PlayerJoined", (connectionId, playerName) =>
        {
            Logger.Information("Player joined: {ConnectionId} ({PlayerName})", connectionId, playerName);
            if (connectionId != connection.ConnectionId)
                _players[connectionId] = playerName;
            PlayerJoined?.Invoke(connectionId, playerName);
        });

        connection.On<string>("PlayerLeft", connectionId =>
        {
            Logger.Information("Player left: {ConnectionId}", connectionId);
            _players.TryRemove(connectionId, out _);
            PlayerLeft?.Invoke(connectionId);
        });

        connection.On<int>("UpdatePlayerCount", count =>
        {
            Logger.Debug("Player count updated: {Count}", count);
            PlayerCountUpdated?.Invoke(count);
        });

        connection.On<StageFlags>(HubConstants.ReceiveStageFlags, flags =>
        {
            StageFlagsReceived?.Invoke(flags);
        });

        connection.On<int>(HubConstants.ReceiveRupeeTotal, total =>
        {
            RupeeTotalReceived?.Invoke(total);
        });

        connection.On<BaitCounts>(HubConstants.ReceiveBaitTotal, total =>
        {
            BaitTotalReceived?.Invoke(total);
        });

        connection.On<SpoilsCounts>(HubConstants.ReceiveSpoilsTotal, total =>
        {
            SpoilsTotalReceived?.Invoke(total);
        });

        connection.On<RoomSettings>(HubConstants.ReceiveRoomSettings, settings =>
        {
            RoomSettingsReceived?.Invoke(settings);
        });

        connection.On<RoomInventory>(HubConstants.ReceiveRoomInventory, room =>
        {
            RoomInventoryReceived?.Invoke(room);
        });

        connection.On<RoomSwitches>(HubConstants.ReceiveRoomSwitches, switches =>
        {
            RoomSwitchesReceived?.Invoke(switches);
        });

        connection.On<StoryFlags>(HubConstants.ReceiveStoryFlags, flags =>
        {
            StoryFlagsReceived?.Invoke(flags);
        });

        connection.On<PlayerEvent>(HubConstants.ReceivePlayerEvent, evt =>
        {
            PlayerEventReceived?.Invoke(evt);
        });
    }

    public async Task<RoomSettings?> GetRoomSettingsAsync()
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
    }

    /// <summary>
    /// Invoke a fire-and-forget hub method. Returns false (nothing sent) when not connected, so
    /// callers only mark data as sent once it actually went out; server errors throw.
    /// </summary>
    private async Task<bool> SendAsync(string method, object arg)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return false;
        await connection.InvokeAsync(method, arg);
        return true;
    }

    /// <summary>Room-owner-only (the server rejects anyone else and re-sends the real rules).</summary>
    public async Task SetRoomSettingsAsync(RoomSettings settings)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return;
        await connection.InvokeAsync(HubConstants.SetRoomSettings, settings);
    }

    /// <summary>
    /// Join the shared wallet; returns the wallet total to adopt, or null if offline, the rule is
    /// off, or the wallet is still waiting for its owner to seed it (the client retries).
    /// </summary>
    public async Task<int?> JoinWalletAsync(int currentRupees)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<int?>(HubConstants.JoinWallet, currentRupees);
    }

    /// <summary>Report a local rupee change; false if not connected (nothing sent).</summary>
    public Task<bool> SendRupeeDeltaAsync(int delta) => SendAsync(HubConstants.SendRupeeDelta, delta);

    /// <summary>
    /// Join the shared bait bag with this game's counts; returns the counts to adopt, or null if offline,
    /// the rule is off, or the bag is still waiting for its owner to seed it (the client retries).
    /// </summary>
    public async Task<BaitCounts?> JoinBaitAsync(BaitCounts current)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<BaitCounts?>(HubConstants.JoinBait, current);
    }

    /// <summary>Report a local bait bag change (signed per type); false if not connected (nothing sent).</summary>
    public Task<bool> SendBaitDeltaAsync(BaitCounts delta) => SendAsync(HubConstants.SendBaitDelta, delta);

    /// <summary>
    /// Join the shared spoils bag with this game's counts; returns the counts to adopt, or null if offline,
    /// the rule is off, or the bag is still waiting for its owner to seed it (the client retries).
    /// </summary>
    public async Task<SpoilsCounts?> JoinSpoilsAsync(SpoilsCounts current)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<SpoilsCounts?>(HubConstants.JoinSpoils, current);
    }

    /// <summary>Report a local spoils bag change (signed per type); false if not connected (nothing sent).</summary>
    public Task<bool> SendSpoilsDeltaAsync(SpoilsCounts delta) => SendAsync(HubConstants.SendSpoilsDelta, delta);

    /// <summary>Report this game's flags for one stage slot to the shared world; false if not connected.</summary>
    public Task<bool> SendStageFlagsAsync(StageFlags flags) => SendAsync(HubConstants.SendStageFlags, flags);

    /// <summary>The server's whole shared world (non-empty stage slots), or null if offline.</summary>
    public async Task<List<StageFlags>?> GetWorldFlagsAsync()
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<List<StageFlags>>(HubConstants.GetWorldFlags);
    }

    /// <summary>
    /// Join the room inventory with this game's items; returns the room to apply, or null if
    /// offline, the rule is off, or the room is still waiting for its owner to seed it.
    /// </summary>
    public async Task<RoomInventory?> JoinRoomInventoryAsync(RoomInventory local)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<RoomInventory?>(HubConstants.JoinRoomInventory, local);
    }

    /// <summary>Report items / upgrades this game gained (gains only) to the room; false if not connected.</summary>
    public Task<bool> SendInventoryGainsAsync(RoomInventory gains) => SendAsync(HubConstants.SendInventoryGains, gains);

    /// <summary>Room-owner-only: replace the room inventory exactly (the server rejects anyone else).</summary>
    public async Task SetRoomInventoryAsync(RoomInventory exact)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return;
        await connection.InvokeAsync(HubConstants.SetRoomInventory, exact);
    }

    /// <summary>The room inventory, or null if offline / rule off / not seeded yet.</summary>
    public async Task<RoomInventory?> GetRoomInventoryAsync()
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<RoomInventory?>(HubConstants.GetRoomInventory);
    }

    /// <summary>
    /// Join the room story with this game's flags; returns the room's flags to apply, or null if
    /// offline, the rule is off, or the room is still waiting for its owner to seed it.
    /// </summary>
    public async Task<StoryFlags?> JoinRoomStoryAsync(StoryFlags local)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<StoryFlags?>(HubConstants.JoinRoomStory, local);
    }

    /// <summary>Report this game's syncable story flags (the server merges any new ones into the room); false if not connected.</summary>
    public Task<bool> SendStoryFlagsAsync(StoryFlags flags) => SendAsync(HubConstants.SendStoryFlags, flags);

    /// <summary>
    /// Tell the room this game is now at <paramref name="local"/>'s place with those switches set; returns
    /// everything the room holds for that place, or null if offline or the shared-world rule is off.
    /// </summary>
    public async Task<RoomSwitches?> JoinRoomSwitchesAsync(RoomSwitches local)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<RoomSwitches?>(HubConstants.JoinRoomSwitches, local);
    }

    /// <summary>
    /// Report switches this game set at the place it joined. Null if not connected; false when the server
    /// says this client isn't joined there any more (rejoin), true when taken.
    /// </summary>
    public async Task<bool?> SendRoomSwitchesAsync(RoomSwitches gains)
    {
        var connection = _hubConnection;
        if (connection?.State != HubConnectionState.Connected) return null;
        return await connection.InvokeAsync<bool>(HubConstants.SendRoomSwitches, gains);
    }
    /// <summary>Report one of our projectiles (bomb thrown / exploded, cannon fired); false if not connected.</summary>
    public Task<bool> SendPlayerEventAsync(PlayerEvent evt) => SendAsync(HubConstants.SendPlayerEvent, evt);

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
