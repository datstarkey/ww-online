using System.Runtime.CompilerServices;
using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>tww.h's overlay block: struct layouts, events to and from the REL's records, and PuppetSyncService
/// publishing name tags and sea chart markers on the native backend.</summary>
public sealed class NativeOverlayTests : IDisposable
{
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), "wwo-native-overlay-" + Guid.NewGuid().ToString("N"));
    private readonly FakeNativeGame _game = new();
    private PuppetSyncService? _sync;

    public void Dispose()
    {
        _sync?.Dispose();
        try { Directory.Delete(_settingsDir, recursive: true); } catch (IOException) { }
    }

    private static int Offset<TStruct, TField>(ref TStruct s, ref TField field) =>
        (int)Unsafe.ByteOffset(ref Unsafe.As<TStruct, byte>(ref s), ref Unsafe.As<TField, byte>(ref field));

    private static readonly PlayerEventBlock.Raw Arrow = new(
        PuppetLayout.PUPPET_FX_KIND_ARROW, 2, 3, 0xDEADBEEF, 1.5f, -2.5f, 3.5f, 4f, 5f, -6f, 100, -200, 300, 42, -1);

    [Fact]
    public void TwwOverlaySeaMarker_Is16Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwOverlaySeaMarker();
        Assert.Equal(16, Unsafe.SizeOf<TwwOverlaySeaMarker>());
        Assert.Equal(0, Offset(ref s, ref s.X));
        Assert.Equal(4, Offset(ref s, ref s.Z));
        Assert.Equal(8, Offset(ref s, ref s.AngleY));
        Assert.Equal(10, Offset(ref s, ref s.R));
        Assert.Equal(12, Offset(ref s, ref s.B));
        Assert.Equal(13, Offset(ref s, ref s.Pad0));
    }

    [Fact]
    public void TwwOverlayEvent_Is40Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwOverlayEvent();
        Assert.Equal(40, Unsafe.SizeOf<TwwOverlayEvent>());
        Assert.Equal(0, Offset(ref s, ref s.Kind));
        Assert.Equal(1, Offset(ref s, ref s.Slot));
        Assert.Equal(2, Offset(ref s, ref s.Variant));
        Assert.Equal(3, Offset(ref s, ref s.Room));
        Assert.Equal(4, Offset(ref s, ref s.Id));
        Assert.Equal(8, Offset(ref s, ref s.Pos));
        Assert.Equal(20, Offset(ref s, ref s.SpeedF));
        Assert.Equal(24, Offset(ref s, ref s.SpeedY));
        Assert.Equal(28, Offset(ref s, ref s.Gravity));
        Assert.Equal(32, Offset(ref s, ref s.Angle));
        Assert.Equal(38, Offset(ref s, ref s.Timer));
    }

    [Fact]
    public void Events_RoundTripThroughTheNativeStruct()
    {
        Assert.Equal(Arrow, NativeOverlay.ToRaw(NativeOverlay.FromRaw(Arrow)));
    }

    [Fact]
    public void ReadEvents_SetsTheSendFlag_TakesTheLocalEventsInOrder()
    {
        _game.LocalEvents.Enqueue(NativeOverlay.FromRaw(Arrow));
        _game.LocalEvents.Enqueue(NativeOverlay.FromRaw(Arrow with { Kind = PuppetLayout.PUPPET_FX_KIND_EXPLODE, Id = 7 }));

        var local = _game.Invoke(api => NativeOverlay.ReadEvents(api, send: true));

        Assert.True(_game.EventSend);
        Assert.Equal([Arrow, Arrow with { Kind = PuppetLayout.PUPPET_FX_KIND_EXPLODE, Id = 7 }], local);
        Assert.Empty(_game.LocalEvents);
        _game.Invoke(api => NativeOverlay.ReadEvents(api, send: false));
        Assert.False(_game.EventSend);
    }

    [Fact]
    public void WriteEvents_StopsAtTheFirstRefusal_AndCountsWhatWasTaken()
    {
        _game.PeerEventCapacity = 2;
        PlayerEventBlock.Raw[] batch = [Arrow, Arrow with { Id = 2 }, Arrow with { Id = 3 }];

        int written = _game.Invoke(api => NativeOverlay.WriteEvents(api, batch));

        Assert.Equal(2, written);
        Assert.Equal([Arrow.Id, 2u], _game.PeerEvents.Select(e => e.Id));
    }

    private static PuppetData Peer(string stage, float x, float z, string name) => new()
    {
        Position = new Vector3(x, 0, z),
        Rotation = 0x1000,
        StageName = stage,
        PlayerName = name,
        Appearance = new AppearanceState { ColorR = 200, ColorG = 10, ColorB = 20 },
    };

    private PuppetSyncService StartSync()
    {
        _sync = new PuppetSyncService(new NativeGameService(_game), new GameSettingsService(_settingsDir), new DespawnWorker(),
            new LiveWorldPoke(), _game);
        return _sync;
    }

    private void WaitForMarkers(int calls)
    {
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref _game.SeaMarkerCalls) > calls, TimeSpan.FromSeconds(5)),
            "no overlay publish");
    }

    [Fact]
    public void NativePuppetSync_PublishesNames_AndEveryPeerOnTheSea_ToTheChart()
    {
        var sync = StartSync();
        _game.SetLocal(FakeNativeGame.Local("sea", 11, 0, 0, 0));
        sync.UpdateRemotePuppet("sailor", Peer("sea", 12345f, -6789f, "Tetra"));
        sync.UpdateRemotePuppet("indoors", Peer("M_NewD2", 0, 0, "Medli"));
        sync.TryGetSlot("sailor", out int sailor);
        sync.TryGetSlot("indoors", out int indoors);

        sync.Start();
        WaitForMarkers(0);

        Assert.Equal("Tetra", _game.PeerNames[sailor]);
        Assert.Equal("Medli", _game.PeerNames[indoors]);
        var marker = Assert.Single(_game.SeaMarkers);
        Assert.Equal((12345f, -6789f, (short)0x1000), (marker.X, marker.Z, marker.AngleY));
        Assert.Equal((200, 10, 20), (marker.R, marker.G, marker.B));
    }

    [Fact]
    public void NativePuppetSync_WithNamesOff_ClearsEveryName()
    {
        var sync = StartSync();
        _game.SetLocal(FakeNativeGame.Local("sea", 11, 0, 0, 0));
        sync.UpdateRemotePuppet("sailor", Peer("sea", 0, 0, "Tetra"));
        sync.ShowPlayerNames = false;

        sync.Start();
        WaitForMarkers(0);

        Assert.All(_game.PeerNames, Assert.Null);
    }
}
