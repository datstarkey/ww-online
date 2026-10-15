using System;
using System.Threading.Tasks;
using Serilog;

namespace WWOnline.Services;

/// <summary>
/// Unified Dolphin service - the ONLY way to read/write game memory
/// All memory operations use MemoryAddress structs for type safety
/// </summary>
public class DolphinService : GameMemoryService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<DolphinService>();
    private readonly WindowsMemoryReader _memoryReader;

    public override bool IsConnected => _memoryReader.IsConnected;
    public override int? ConnectedProcessId => _memoryReader.ConnectedProcessId;
    public override long? EmulatedMemorySize => _memoryReader.Mem1Size;

    public override event EventHandler<bool>? ConnectionChanged;

    public DolphinService()
    {
        _memoryReader = new WindowsMemoryReader();
    }

    public override Task<bool> ConnectAsync() => ConnectInternalAsync(null);

    public override Task<bool> ConnectAsync(int processId) => ConnectInternalAsync(processId);

    public override IReadOnlyList<DolphinProcessInfo> EnumerateProcesses()
        => WindowsMemoryReader.EnumerateDolphinProcesses();

    private async Task<bool> ConnectInternalAsync(int? processId)
    {
        var wasConnected = IsConnected;

        if (processId.HasValue)
            Logger.Information("Connecting to Dolphin PID {Pid}...", processId.Value);
        else
            Logger.Information("Connecting to first available Dolphin...");

        var result = await _memoryReader.ConnectAsync(processId);

        if (result)
            Logger.Information("Successfully connected to Dolphin (PID {Pid})", _memoryReader.ConnectedProcessId);
        else
            Logger.Warning("Failed to connect to Dolphin");

        if (result != wasConnected)
            ConnectionChanged?.Invoke(this, result);
        return result;
    }
    
    // === Core Memory Operations (Synchronous) ===
    
    public override byte[]? ReadMemory(uint address, int size)
    {
        if (!IsConnected)
            return null;
        
        // Convert GameCube address to full address if needed
        ulong fullAddress = address;
        if (address < 0x80000000)
        {
            fullAddress = 0x80000000 + address;
        }
        
        return _memoryReader.ReadBytes(fullAddress, size);
    }
    
    public override bool WriteMemory(uint address, byte[] data)
    {
        if (!IsConnected || data == null)
            return false;
            
        // Convert address to ulong for the memory reader
        ulong addr = address;
        if (address < 0x80000000)
        {
            addr = 0x80000000 + address;
        }
        
        return _memoryReader.WriteBytes(addr, data);
    }
    
    public override void Disconnect()
    {
        var wasConnected = IsConnected;
        _memoryReader.Disconnect();
        if (wasConnected)
            ConnectionChanged?.Invoke(this, false);
    }
    
    public override void Dispose()
    {
        _memoryReader.Dispose();
    }
}