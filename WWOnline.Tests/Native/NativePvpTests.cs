using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR.Client;
using WWOnline.Hubs;
using WWOnline.Native;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using WWOnline.Tests.Services;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>tww.h's pvp block: the struct layout, and two native games (plus a Dolphin player) hitting each other
/// through the real hub with <see cref="PvpService"/>.</summary>
[Collection("GameHub static state")]
public sealed class NativePvpTests : IDisposable
{
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), "wwo-native-pvp-" + Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
        try { Directory.Delete(_settingsDir, recursive: true); } catch (IOException) { }
    }

    private static int Offset<TStruct, TField>(ref TStruct s, ref TField field) =>
        (int)Unsafe.ByteOffset(ref Unsafe.As<TStruct, byte>(ref s), ref Unsafe.As<TField, byte>(ref field));

    [Fact]
    public void TwwPvpHit_Is32Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwPvpHit();
        Assert.Equal(32, Unsafe.SizeOf<TwwPvpHit>());
        Assert.Equal(0, Offset(ref s, ref s.Slot));
        Assert.Equal(1, Offset(ref s, ref s.Damage));
        Assert.Equal(2, Offset(ref s, ref s.Spl));
        Assert.Equal(3, Offset(ref s, ref s.Pad0));
        Assert.Equal(4, Offset(ref s, ref s.AtType));
        Assert.Equal(8, Offset(ref s, ref s.Pos));
        Assert.Equal(20, Offset(ref s, ref s.AttackerPos));
        Assert.Equal(TwwNative.PvpMaxDamage, PvpHit.MaxDamage);
        Assert.Equal(TwwNative.PvpSplMax, PvpHit.MaxSpl);
    }

    private static async Task Until(Func<bool> done, Func<Task>? tick = null, int ms = 8000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ms);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out");
            if (tick != null) await tick();
            await Task.Delay(40);
        }
    }

    private static PuppetData At(float x) => new() { StageName = "sea", Position = new Vector3(x, 0, 0) };

    /// <summary>A native game in the sea at <paramref name="x"/> with its puppet sync running (for the local stage).</summary>
    private async Task<(FakeNativeGame Game, PuppetSyncService Sync)> Game(float x)
    {
        var game = new FakeNativeGame();
        game.SetLocal(FakeNativeGame.Local("sea", 0, x, 0, 0));
        var sync = new PuppetSyncService(new NativeGameService(game), new GameSettingsService(Path.Combine(_settingsDir, x.ToString())),
            new DespawnWorker(), new LiveWorldPoke(), game);
        _disposables.Add(sync);
        sync.Start();
        await Until(() => sync.LocalLocation.Stage == "sea");
        return (game, sync);
    }

    [Fact]
    public async Task AHitOnAPuppet_ReachesThatPlayersGame_FromTheAttackersSlot_AndDolphinPlayersAreNeverTargets()
    {
        GameHub.ConfigureHostToken("pvp-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, pvp: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var hubA = new SignalRClientService { Capabilities = ClientCapabilities.PvpHits };
            await using var hubB = new SignalRClientService { Capabilities = ClientCapabilities.PvpHits };
            await using var hubD = new SignalRClientService(); // a Dolphin player
            using var roomA = new RoomSettingsService(hubA);
            using var roomB = new RoomSettingsService(hubB);
            Assert.True((await hubA.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("A"), "pvp-key")).success);
            Assert.True((await hubB.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("B"))).success);
            Assert.True((await hubD.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("D"))).success);
            string a = hubA.Connection!.ConnectionId!, b = hubB.Connection!.ConnectionId!, d = hubD.Connection!.ConnectionId!;
            await Until(() => roomA.Current.Pvp && roomB.Current.Pvp && hubA.PlayerCapabilities.ContainsKey(b) &&
                              hubA.PlayerCapabilities.ContainsKey(d) && hubB.PlayerCapabilities.ContainsKey(a));
            Assert.Equal(ClientCapabilities.PvpHits, hubB.PlayerCapabilities[a]);
            Assert.Equal(ClientCapabilities.None, hubA.PlayerCapabilities[d]);

            // A at 0, B at 100, D at 50 on the sea; each game holds the others as puppets.
            var (gameA, syncA) = await Game(0);
            var (gameB, syncB) = await Game(100);
            syncA.UpdateRemotePuppet(b, At(100));
            syncA.UpdateRemotePuppet(d, At(50));
            syncB.UpdateRemotePuppet(a, At(0));
            await hubA.Connection.InvokeAsync(HubConstants.SendPuppetData, At(0));
            await hubB.Connection.InvokeAsync(HubConstants.SendPuppetData, At(100));
            using var pvpA = new PvpService(hubA, syncA, roomA, native: gameA);
            using var pvpB = new PvpService(hubB, syncB, roomB, native: gameB);
            pvpB.Start();

            // Only B's slot takes part in A's game: D's game can't take hits.
            await pvpA.TickAsync();
            Assert.True(syncA.TryGetSlot(b, out int slotB));
            Assert.True(syncA.TryGetSlot(d, out int slotD));
            Assert.Equal(1u << slotB, gameA.PvpSlots);
            Assert.NotEqual(slotB, slotD);

            var hit = new TwwPvpHit { Slot = (byte)slotB, Damage = 4, Spl = 1, AtType = 0x2 };
            (hit.Pos[0], hit.AttackerPos[0]) = (100f, 0f);
            gameA.PvpLocalHits.Enqueue(hit);
            await pvpA.TickAsync();

            // B's game applies it from A's slot there.
            await Until(() => gameB.PvpApplied.Count == 1);
            Assert.True(syncB.TryGetSlot(a, out int slotA));
            var applied = gameB.PvpApplied[0];
            Assert.Equal(((byte)slotA, (byte)4, (byte)1, 0x2u, 100f, 0f), (applied.Slot, applied.Damage, applied.Spl, applied.AtType, applied.Pos[0], applied.AttackerPos[0]));

            // The rule off: no slot takes part.
            await roomA.SetPvpAsync(false);
            await Until(() => !roomA.Current.Pvp);
            await pvpA.TickAsync();
            Assert.Equal(0u, gameA.PvpSlots);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true);
        }
    }
}
