using System.Collections.Concurrent;
using Serilog;
using WWOnline.Data;
using WWOnline.Native;

namespace WWOnline.Services;

/// <summary>
/// The native backend behind <see cref="IDolphinService"/> (phase-1 shim, tww-native-notes WW_ONLINE_EMBED.md 4.4).
/// The save window, dSv_info_c from <see cref="GameMemoryAddresses.GameInfo"/> for tww_save_size() bytes, goes
/// to tww_save_read / tww_save_write on the game thread: the same offsets and big-endian bytes as on the
/// GameCube, so the typed reads in <see cref="GameMemoryService"/> work unchanged. Every other address fails
/// (null / false), logged once per address: those are the services still to move to typed native calls.
/// "Connected" means the embedded game is running.
/// </summary>
public sealed partial class NativeGameService : GameMemoryService
{
    private static readonly ILogger Logger = Log.ForContext<NativeGameService>();

    private readonly INativeGame _game;
    private readonly ConcurrentDictionary<uint, byte> _reported = new();
    private uint _saveSize;

    public NativeGameService(INativeGame game)
    {
        _game = game;
        _game.RunningChanged += OnRunningChanged;
        Play = new NativePlayInventory(game);
    }

    public override bool IsConnected => _game.IsRunning;
    public override int? ConnectedProcessId => null;
    public override long? EmulatedMemorySize => null;
    public override event EventHandler<bool>? ConnectionChanged;
    public override IPlayInventory Play { get; }

    // The game is started by GameLaunchService, never attached to: "connecting" just reports whether it runs.
    public override Task<bool> ConnectAsync() => Task.FromResult(IsConnected);
    public override Task<bool> ConnectAsync(int processId) => Task.FromResult(IsConnected);
    public override IReadOnlyList<DolphinProcessInfo> EnumerateProcesses() => [];
    public override void Disconnect() { }

    public override byte[]? ReadMemory(uint address, int size)
    {
        if (!IsConnected || size <= 0) return null;
        if (SaveOffset(address, size, "read") is not uint offset) return null;
        return Call(api =>
        {
            var data = new byte[size];
            return api.SaveRead(offset, data) == size ? data : null;
        });
    }

    public override bool WriteMemory(uint address, byte[] data)
    {
        if (!IsConnected || data == null || data.Length == 0) return false;
        if (SaveOffset(address, data.Length, "write") is not uint offset) return false;
        return Call(api => api.SaveWrite(offset, data) == data.Length);
    }

    public override void Dispose() => _game.RunningChanged -= OnRunningChanged;

    /// <summary>The save offset of [address, address + size), or null (logged once) when it isn't all in the save window.</summary>
    private uint? SaveOffset(uint address, int size, string access)
    {
        if (address < 0x80000000) address += 0x80000000;
        if (_saveSize == 0 && (_saveSize = Call(api => api.SaveSize())) == 0) return null;
        ulong offset = (ulong)address - GameMemoryAddresses.GameInfo;
        if (address >= GameMemoryAddresses.GameInfo && offset + (ulong)size <= _saveSize)
            return (uint)offset;
        if (_reported.TryAdd(address, 0))
            Logger.Warning("[native] {Access} of 0x{Address:X8} ({Size} bytes) is outside the save window: not on the native game yet",
                access, address, size);
        return null;
    }

    private T? Call<T>(Func<ITwwApi, T> func)
    {
        try
        {
            return _game.Invoke(func);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            Logger.Debug(ex, "[native] game call failed");
            return default;
        }
    }

    private void OnRunningChanged(bool running) => ConnectionChanged?.Invoke(this, running);
}
