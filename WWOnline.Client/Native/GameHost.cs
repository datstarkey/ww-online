using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Serilog;

namespace WWOnline.Native;

/// <summary>
/// The embedded game's thread (tww-native-notes WW_ONLINE_EMBED.md 4.1, 4.4). It loads libtww first, so the
/// game's static initialisers and the SDK's main thread are its own, waits for the view from the UI thread
/// (<see cref="CreateView"/>, called by <see cref="Views.GameView"/>), then runs tww_init and one
/// tww_run_frame per game frame (the game paces itself). Between frames the game is quiescent: queued
/// <see cref="Post"/> / <see cref="Invoke{T}"/> work runs there, then a <see cref="Snapshot"/> is published.
/// The game starts at most once per process and cannot restart.
/// </summary>
public sealed unsafe partial class GameHost : INativeGame
{
    private static readonly ILogger Logger = Log.ForContext<GameHost>();
    private const int StackSize = 16 * 1024 * 1024;
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InvokeTimeout = TimeSpan.FromSeconds(5);

    // The fatal callback's target (a C function pointer carries no state; one game per process).
    private static GameHost? s_current;

    private readonly ConcurrentQueue<WorkItem> _queue = new();
    private readonly ManualResetEventSlim _loaded = new();
    private readonly ManualResetEventSlim _viewReady = new();
    private readonly ManualResetEventSlim _stopped = new();
    private readonly TwwApi _api = new();
    private IntPtr _lib;
    private IntPtr _view;
    private int _started;
    private int _gameThreadId = -1;
    private volatile bool _stop;
    private volatile bool _running;
    private volatile bool _failed;
    private volatile int _fatalCode = -1;
    private volatile NativeSnapshot? _snapshot;
    private DateTime _frameTime;
    private uint _frameTimeFrame;

    // The frame clock's drift from the wall clock past which it is set to it (a pause, a long stall).
    private static readonly TimeSpan MaxFrameClockDrift = TimeSpan.FromSeconds(0.5);

    private readonly record struct WorkItem(Action<ITwwApi> Run, Action<Exception>? Fail);

    /// <summary><see cref="Start"/> ran: the UI must now host the view (<see cref="CreateView"/>). Any thread.</summary>
    public event Action? Started;

    public event Action<bool>? RunningChanged;

    public bool IsStarted => Volatile.Read(ref _started) != 0;
    public bool IsRunning => _running;

    /// <summary>The library didn't load or fit, tww_init failed, or the game stopped with a fatal error.
    /// Final: the game cannot start again in this process.</summary>
    public bool HasFailed => _failed;

    /// <summary>The fatal callback's code, or -1.</summary>
    public int FatalCode => _fatalCode;

    public NativeSnapshot? Snapshot => _snapshot;

    /// <param name="libraryPath">libtww (<see cref="NativePaths.Library(Services.GameSettings)"/>).</param>
    /// <param name="disc">The GZLE01 disc image.</param>
    /// <param name="config">tww_init's "KEY=VALUE" pairs (';'-separated), or null.</param>
    public void Start(string libraryPath, string disc, string? config)
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            throw new InvalidOperationException("The game was already started in this process.");
        s_current = this;
        NativeLibrary.SetDllImportResolver(typeof(TwwNative).Assembly,
            (name, _, _) => name == TwwNative.LibraryName ? _lib : IntPtr.Zero);
        Logger.Information("[native] starting the game: {Lib}, disc {Disc}", libraryPath, disc);
        new Thread(() => Run(libraryPath, disc, config), StackSize) { Name = "Game", IsBackground = true }.Start();
        try { Started?.Invoke(); }
        catch (Exception ex) { Logger.Error(ex, "[native] a Started handler failed"); }
    }

    /// <summary>UI thread: the game's view (an NSView). Created once; later calls return the same view.</summary>
    /// <exception cref="InvalidOperationException">libtww didn't load.</exception>
    public IntPtr CreateView()
    {
        if (_view != IntPtr.Zero) return _view;
        if (!_loaded.Wait(LoadTimeout) || _failed)
            throw new InvalidOperationException("libtww did not load");
        _view = TwwNative.tww_view_create();
        _viewReady.Set();
        return _view;
    }

    /// <summary>Stop running frames (app shutdown) and wait up to <paramref name="timeout"/> for the game thread.</summary>
    public void Stop(TimeSpan timeout)
    {
        _stop = true;
        if (_running) _stopped.Wait(timeout);
    }

    public void Post(Action<ITwwApi> action)
    {
        if (_running) _queue.Enqueue(new WorkItem(action, null));
    }

    public T Invoke<T>(Func<ITwwApi, T> func)
    {
        if (Environment.CurrentManagedThreadId == _gameThreadId) return func(_api);
        if (!_running) throw new InvalidOperationException("The game isn't running.");
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(new WorkItem(api => done.TrySetResult(func(api)), ex => done.TrySetException(ex)));
        if (!((IAsyncResult)done.Task).AsyncWaitHandle.WaitOne(InvokeTimeout))
            throw new TimeoutException($"The game thread didn't run the call within {InvokeTimeout.TotalSeconds:0}s.");
        return done.Task.GetAwaiter().GetResult();
    }

    [UnmanagedCallersOnly]
    private static void OnFatal(int code)
    {
        Logger.Error("[native] the game stopped: fatal code {Code}", code);
        if (s_current is not { } host) return;
        host._fatalCode = code;
        host._failed = true;
        host.SetRunning(false);
    }

    private void Run(string libraryPath, string disc, string? config)
    {
        try
        {
            _lib = NativeLibrary.Load(libraryPath);
            uint version = TwwNative.tww_api_version();
            if (version != TwwNative.ApiVersion)
            {
                Logger.Error("[native] {Lib} has API version {Version}; this client needs {Needed}", libraryPath, version, TwwNative.ApiVersion);
                _failed = true;
                return;
            }
            TwwNative.tww_set_fatal_callback(&OnFatal);
            Logger.Information("[native] libtww API {Version} loaded on the game thread", version);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            Logger.Error(ex, "[native] couldn't load {Lib}", libraryPath);
            _failed = true;
            return;
        }
        finally
        {
            _loaded.Set();
        }

        _viewReady.Wait();
        _gameThreadId = Environment.CurrentManagedThreadId;
        // On failure tww_init reports through the fatal callback and parks this thread (it may never return).
        int result = TwwNative.tww_init(_view, disc, config);
        if (result != 0)
        {
            Logger.Error("[native] tww_init failed: {Result}", result);
            _failed = true;
            return;
        }
        Logger.Information("[native] the game is running");
        SetRunning(true);
        try
        {
            while (!_stop && _fatalCode < 0)
            {
                Drain();
                if (TwwNative.tww_run_frame() != 0) break;
                Publish();
            }
        }
        finally
        {
            SetRunning(false);
            while (_queue.TryDequeue(out var item))
                item.Fail?.Invoke(new InvalidOperationException("The game stopped."));
            if (_fatalCode < 0) TwwNative.tww_shutdown();
            _stopped.Set();
            Logger.Information("[native] the game thread stopped");
        }
    }

    private void Drain()
    {
        while (_queue.TryDequeue(out var item))
        {
            try
            {
                item.Run(_api);
            }
            catch (Exception ex)
            {
                if (item.Fail != null) item.Fail(ex);
                else Logger.Error(ex, "[native] a posted call failed");
            }
        }
    }

    private void Publish()
    {
        _api.GetLocalState(out var local);
        bool inStage = _api.GetLocation(out var location);
        uint frame = TwwNative.tww_frame_count();
        _frameTime = FrameClock(_frameTime, frame - _frameTimeFrame, DateTime.UtcNow);
        _frameTimeFrame = frame;
        _snapshot = new NativeSnapshot(frame, local, location, inStage) { Time = _frameTime };
        PublishPuppet();
    }

    /// <summary>
    /// The frame clock: 1/30 s a game frame (the game's step), eased 1% a frame toward the wall clock and set to it when
    /// further off than <see cref="MaxFrameClockDrift"/>. Peers play our states back by these times, so frames sent at
    /// uneven wall times still come out evenly spaced.
    /// </summary>
    public static DateTime FrameClock(DateTime last, uint frames, DateTime now)
    {
        var t = last.AddTicks(frames * TimeSpan.TicksPerSecond / 30);
        return (t - now).Duration() > MaxFrameClockDrift ? now : t + (now - t) / 100;
    }

    private void SetRunning(bool running)
    {
        if (_running == running) return;
        _running = running;
        try { RunningChanged?.Invoke(running); }
        catch (Exception ex) { Logger.Error(ex, "[native] a RunningChanged handler failed"); }
    }

    private sealed partial class TwwApi : ITwwApi
    {
        public bool GetLocalState(out TwwLocalState state)
        {
            state = default;
            fixed (TwwLocalState* p = &state) return TwwNative.tww_get_local_state(p) == 1;
        }

        public bool GetLocation(out TwwLocation location)
        {
            location = default;
            fixed (TwwLocation* p = &location) return TwwNative.tww_get_location(p) == 1;
        }

        public void SetPeer(int slot, TwwPeerState? peer)
        {
            if (peer is not { } state)
            {
                TwwNative.tww_set_peer(slot, null);
                return;
            }
            TwwNative.tww_set_peer(slot, &state);
        }

        public uint SaveSize() => TwwNative.tww_save_size();

        public uint SaveRead(uint offset, Span<byte> destination)
        {
            fixed (byte* p = destination) return TwwNative.tww_save_read(offset, p, (uint)destination.Length);
        }

        public uint SaveWrite(uint offset, ReadOnlySpan<byte> source)
        {
            fixed (byte* p = source) return TwwNative.tww_save_write(offset, p, (uint)source.Length);
        }
    }
}
