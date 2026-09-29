using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.Tests.Services;

/// <summary>
/// Byte-addressed fake of emulated GameCube memory (big-endian, unset bytes read as 0), for
/// testing code that reads / writes game memory through <see cref="IDolphinService"/>.
/// </summary>
internal sealed class FakeDolphin : IDolphinService
{
    private readonly Dictionary<uint, byte> _mem = new();

    /// <summary>Called before every WriteMemory / Write lands (e.g. to simulate the game racing us).</summary>
    public Action<uint>? BeforeWrite { get; set; }

    public void Set(uint addr, params byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++) _mem[addr + (uint)i] = bytes[i];
    }

    public void SetU32(uint addr, uint v) => Set(addr, (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);

    public byte Get(uint addr) => _mem.TryGetValue(addr, out var b) ? b : (byte)0;

    public uint GetU32(uint addr) => (uint)(Get(addr) << 24 | Get(addr + 1) << 16 | Get(addr + 2) << 8 | Get(addr + 3));

    public bool IsConnected => true;
    public int? ConnectedProcessId => 1;
    public long? EmulatedMemorySize => null;
    public event EventHandler<bool>? ConnectionChanged { add { } remove { } }

    public byte[]? ReadMemory(uint address, int size)
    {
        var b = new byte[size];
        for (int i = 0; i < size; i++) b[i] = Get(address + (uint)i);
        return b;
    }

    public bool WriteMemory(uint address, byte[] data)
    {
        BeforeWrite?.Invoke(address);
        Set(address, data);
        return true;
    }

    public T? Read<T>(MemoryAddress<T> address) where T : struct
    {
        uint a = address.Address;
        if (typeof(T) == typeof(byte)) return (T)(object)Get(a);
        if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)(Get(a) << 8 | Get(a + 1));
        if (typeof(T) == typeof(short)) return (T)(object)(short)(Get(a) << 8 | Get(a + 1));
        if (typeof(T) == typeof(uint)) return (T)(object)GetU32(a);
        if (typeof(T) == typeof(int)) return (T)(object)(int)GetU32(a);
        throw new NotSupportedException(typeof(T).Name);
    }

    public bool Write<T>(MemoryAddress<T> address, T value) where T : struct
    {
        BeforeWrite?.Invoke(address.Address);
        switch (value)
        {
            case byte b: Set(address.Address, b); break;
            case ushort u: Set(address.Address, (byte)(u >> 8), (byte)u); break;
            case short s16: Set(address.Address, (byte)(s16 >> 8), (byte)s16); break;
            case uint u32: SetU32(address.Address, u32); break;
            case int i32: SetU32(address.Address, (uint)i32); break;
            default: throw new NotSupportedException(typeof(T).Name);
        }
        return true;
    }

    public Task<bool> ConnectAsync() => Task.FromResult(true);
    public Task<bool> ConnectAsync(int processId) => Task.FromResult(true);
    public IReadOnlyList<DolphinProcessInfo> EnumerateProcesses() => [];
    public void Disconnect() { }
    public T? ReadWithOffset<T>(uint baseAddress, MemoryAddress<T> offset) where T : struct => throw new NotSupportedException();
    public bool WriteWithOffset<T>(uint baseAddress, MemoryAddress<T> offset, T value) where T : struct => throw new NotSupportedException();
    public byte[]? ReadBytesWithOffset(uint baseAddress, ByteArrayMemoryAddress offset) => throw new NotSupportedException();
    public bool WriteBytesWithOffset(uint baseAddress, ByteArrayMemoryAddress offset, byte[] data) => throw new NotSupportedException();
    public string? ReadString(StringMemoryAddress address) => throw new NotSupportedException();
    public bool WriteString(StringMemoryAddress address, string value) => throw new NotSupportedException();
    public byte[]? ReadBytes(ByteArrayMemoryAddress address) => ReadMemory(address.Address, address.Length);
    public bool WriteBytes(ByteArrayMemoryAddress address, byte[] data) => WriteMemory(address.Address, data);
    public Task<GameState?> ReadGameStateAsync() => Task.FromResult<GameState?>(null);
    public void Dispose() { }
}
