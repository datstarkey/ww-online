using Serilog;
using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.Native;

/// <summary>
/// <see cref="IWorldGame"/> on the native backend: tww_world_* on the game thread. The game runs the placed-item
/// despawn and the live world itself every 4th frame, so no worker puppet is needed for them
/// (<see cref="DespawnWorker"/> and <see cref="LiveWorldPoke.IsNeeded"/> are Dolphin-only).
/// </summary>
public sealed class NativeWorldGame(INativeGame game) : IWorldGame
{
    private static readonly ILogger Logger = Log.ForContext<NativeWorldGame>();

    // The game has one live-world block for its lifetime (it cannot restart in this process).
    private const uint BlockAddress = 1;
    private const ulong BlockBoot = 1;

    public int? CurrentSlot() => Call(api => (int?)api.WorldSaveSlot()) is int slot and >= 0 and < StageFlags.SlotCount ? slot : null;

    public RoomSwitches? ReadPlace() => Call(api =>
    {
        int slot = api.WorldSaveSlot();
        if (slot < 0 || !((ITwwApi)api).GetLocation(out var location)) return null;
        var place = new RoomSwitches { Stage = location.Stage.ToString(), Slot = slot, Room = location.Room };
        return place.IsValid() ? place : null;
    });

    public int? RoomZoneNo(int room) => Call(api => (int?)api.WorldRoomZone(room));

    public IReadOnlyList<SalvagePoint>? ReadSalvagePoints() => Call(api =>
    {
        Span<TwwWorldSalvagePoint> points = stackalloc TwwWorldSalvagePoint[TwwNative.WorldSalvageMax];
        int n = api.WorldSalvageRead(points);
        if (n == 0) return null;
        var result = new SalvagePoint[n];
        for (int i = 0; i < n; i++)
            result[i] = new SalvagePoint(points[i].Room, points[i].SaveNo, points[i].Kind, points[i].Flag);
        return result;
    });

    public bool OrSalvageFlag(int index, byte bits) => Call(api => api.WorldSalvageSetFlag(index, bits));

    public void SetRemoteItems(int slot, uint mask) => Call(api =>
    {
        api.WorldSetRemoteItems(slot, mask);
        return true;
    });

    public (uint Count, uint Last)? ReadDespawns() => Call(api =>
    {
        var stats = api.WorldStats();
        return ((uint, uint)?)(stats.DespawnCount, stats.LastDespawn);
    });

    public LiveWorldPoke.BlockState? ReadLiveWorld() => Call(api =>
    {
        var s = api.WorldLiveStatus();
        return (LiveWorldPoke.BlockState?)new LiveWorldPoke.BlockState(BlockAddress, BlockBoot, s.Seq, s.DoneSeq, s.PokeCount, s.Retry);
    });

    public void PublishLiveWorld(LiveWorldPoke.BlockState block, uint seq, int slot, int zoneRoom, uint[] bits)
    {
        var batch = new TwwWorldLiveBatch { Seq = seq, Slot = slot, ZoneRoom = zoneRoom };
        bits.AsSpan(0, TwwNative.WorldLiveWords).CopyTo(batch.Bits);
        Call(api =>
        {
            api.WorldLivePost(batch);
            return true;
        });
    }

    private T? Call<T>(Func<ITwwWorldApi, T> func)
    {
        if (!game.IsRunning) return default;
        try
        {
            return game.Invoke(api => func((ITwwWorldApi)api));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            Logger.Debug(ex, "[native] world call failed");
            return default;
        }
    }
}
