using WWOnline.Data;
using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>
/// The embedded game without libtww: a save window of <see cref="SaveSize"/> bytes, a settable snapshot and the
/// peers last set. Posted / invoked calls run inline on the caller's thread.
/// </summary>
internal sealed partial class FakeNativeGame : INativeGame, ITwwApi
{
    public const uint SaveSizeBytes = 0x12A0;

    public byte[] Save { get; } = new byte[SaveSizeBytes];
    public TwwPeerState?[] Peers { get; } = new TwwPeerState?[TwwNative.MaxPeers];
    public int SetPeerCalls;
    public int SaveCalls;

    private bool _running = true;

    public bool IsRunning
    {
        get => _running;
        set
        {
            _running = value;
            RunningChanged?.Invoke(value);
        }
    }

    public event Action<bool>? RunningChanged;
    public NativeSnapshot? Snapshot { get; set; }

    public void Post(Action<ITwwApi> action)
    {
        lock (this) action(this);
    }

    public T Invoke<T>(Func<ITwwApi, T> func)
    {
        if (!_running) throw new InvalidOperationException("not running");
        lock (this) return func(this);
    }

    public bool GetLocalState(out TwwLocalState state)
    {
        state = Snapshot?.Local ?? default;
        return state.Valid != 0;
    }

    public bool GetLocation(out TwwLocation location)
    {
        location = Snapshot?.Location ?? default;
        return Snapshot?.InStage ?? false;
    }

    public void SetPeer(int slot, TwwPeerState? peer)
    {
        SetPeerCalls++;
        Peers[slot] = peer;
    }

    public uint SaveSize() => SaveSizeBytes;

    public uint SaveRead(uint offset, Span<byte> destination)
    {
        SaveCalls++;
        if (offset + destination.Length > Save.Length) return 0;
        Save.AsSpan((int)offset, destination.Length).CopyTo(destination);
        return (uint)destination.Length;
    }

    public uint SaveWrite(uint offset, ReadOnlySpan<byte> source)
    {
        SaveCalls++;
        if (offset + source.Length > Save.Length) return 0;
        source.CopyTo(Save.AsSpan((int)offset));
        return (uint)source.Length;
    }

    /// <summary>The local Link at <paramref name="stage"/>:<paramref name="room"/>, idle (in play).</summary>
    public static TwwLocalState Local(string stage, sbyte room, float x, float y, float z, short angleY = 0,
        float speed = 0, uint proc = 0, TwwLocationFlags flags = TwwLocationFlags.Idle)
    {
        var state = new TwwLocalState
        {
            Valid = 1,
            SpeedF = speed,
            Proc = proc,
            Loc = new TwwLocation { Stage = TwwStageName.From(stage), Room = room, Layer = -1, Flags = flags },
        };
        state.Pos[0] = x;
        state.Pos[1] = y;
        state.Pos[2] = z;
        state.Angle[1] = angleY;
        return state;
    }

    public void SetLocal(TwwLocalState local, uint frame = 1) =>
        Snapshot = new NativeSnapshot(frame, local, local.Loc, local.Valid != 0);

    /// <summary>An address in the save window (GameInfo + <paramref name="offset"/>).</summary>
    public static uint SaveAddress(uint offset) => GameMemoryAddresses.GameInfo + offset;
}
