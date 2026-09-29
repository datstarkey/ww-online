using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Unified interface for all Dolphin memory access - the ONLY way to read/write game memory
/// All memory operations must use MemoryAddress structs for type safety
/// </summary>
public interface IDolphinService : IDisposable
{
    /// <summary>
    /// Whether the service is connected/initialized
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// PID of the Dolphin process currently attached, or null when disconnected.
    /// </summary>
    int? ConnectedProcessId { get; }

    /// <summary>
    /// Size of the attached game's MEM1 in bytes (0x1800000 retail, 0x3000000 with Dolphin's 48 MB override), or
    /// null when not attached or not known.
    /// </summary>
    long? EmulatedMemorySize { get; }

    /// <summary>
    /// Raised when IsConnected transitions. Argument is the new IsConnected value.
    /// Fires on an arbitrary thread — subscribers must marshal to UI thread if needed.
    /// </summary>
    event EventHandler<bool>? ConnectionChanged;

    /// <summary>
    /// Connect to the first available Dolphin process (legacy behavior).
    /// </summary>
    Task<bool> ConnectAsync();

    /// <summary>
    /// Connect to a specific Dolphin process by PID.
    /// </summary>
    Task<bool> ConnectAsync(int processId);

    /// <summary>
    /// Enumerate currently running Dolphin processes.
    /// </summary>
    IReadOnlyList<DolphinProcessInfo> EnumerateProcesses();

    /// <summary>
    /// Disconnect or cleanup
    /// </summary>
    void Disconnect();
    
    // === Core Memory Operations (Synchronous) ===
    
    /// <summary>
    /// Read raw bytes from memory
    /// </summary>
    byte[]? ReadMemory(uint address, int size);
    
    /// <summary>
    /// Write raw bytes to memory
    /// </summary>
    bool WriteMemory(uint address, byte[] data);
    
    // === Typed Memory Operations with MemoryAddress ===
    
    /// <summary>
    /// Read a value from a typed memory address
    /// </summary>
    T? Read<T>(MemoryAddress<T> address) where T : struct;
    
    /// <summary>
    /// Write a value to a typed memory address
    /// </summary>
    bool Write<T>(MemoryAddress<T> address, T value) where T : struct;
    
    /// <summary>
    /// Read a value from a memory address with an offset (useful for entity offsets)
    /// </summary>
    T? ReadWithOffset<T>(uint baseAddress, MemoryAddress<T> offset) where T : struct;
    
    /// <summary>
    /// Write a value to a memory address with an offset (useful for entity offsets)
    /// </summary>
    bool WriteWithOffset<T>(uint baseAddress, MemoryAddress<T> offset, T value) where T : struct;
    
    /// <summary>
    /// Read a byte array from a memory address with an offset (useful for entity offsets)
    /// </summary>
    byte[]? ReadBytesWithOffset(uint baseAddress, ByteArrayMemoryAddress offset);
    
    /// <summary>
    /// Write a byte array to a memory address with an offset (useful for entity offsets)
    /// </summary>
    bool WriteBytesWithOffset(uint baseAddress, ByteArrayMemoryAddress offset, byte[] data);
    
    /// <summary>
    /// Read a string from memory
    /// </summary>
    string? ReadString(StringMemoryAddress address);
    
    /// <summary>
    /// Write a string to memory
    /// </summary>
    bool WriteString(StringMemoryAddress address, string value);
    
    /// <summary>
    /// Read a byte array from memory
    /// </summary>
    byte[]? ReadBytes(ByteArrayMemoryAddress address);
    
    /// <summary>
    /// Write a byte array to memory
    /// </summary>
    bool WriteBytes(ByteArrayMemoryAddress address, byte[] data);
    
    /// <summary>Read complete game state</summary>
    Task<GameState?> ReadGameStateAsync();
}