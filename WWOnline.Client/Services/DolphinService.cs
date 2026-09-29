using System;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Unified Dolphin service - the ONLY way to read/write game memory
/// All memory operations use MemoryAddress structs for type safety
/// </summary>
public class DolphinService : IDolphinService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<DolphinService>();
    private readonly WindowsMemoryReader _memoryReader;
    private byte _lastSword, _lastShield; // last equip ids read outside a REL equip swap

    public bool IsConnected => _memoryReader.IsConnected;
    public int? ConnectedProcessId => _memoryReader.ConnectedProcessId;
    public long? EmulatedMemorySize => _memoryReader.Mem1Size;

    public event EventHandler<bool>? ConnectionChanged;

    public DolphinService()
    {
        _memoryReader = new WindowsMemoryReader();
    }

    public Task<bool> ConnectAsync() => ConnectInternalAsync(null);

    public Task<bool> ConnectAsync(int processId) => ConnectInternalAsync(processId);

    public IReadOnlyList<DolphinProcessInfo> EnumerateProcesses()
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
    
    public byte[]? ReadMemory(uint address, int size)
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
    
    public bool WriteMemory(uint address, byte[] data)
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
    
    // === Typed Memory Operations with MemoryAddress ===
    
    public T? Read<T>(MemoryAddress<T> address) where T : struct
    {
        if (!IsConnected)
            return null;
            
        var type = typeof(T);
        
        // Handle different types
        if (type == typeof(byte))
        {
            var data = ReadMemory(address.Address, 1);
            return data != null ? (T)(object)data[0] : null;
        }
        else if (type == typeof(ushort))
        {
            var data = ReadMemory(address.Address, 2);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToUInt16(data, 0);
            }
        }
        else if (type == typeof(short))
        {
            var data = ReadMemory(address.Address, 2);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToInt16(data, 0);
            }
        }
        else if (type == typeof(uint))
        {
            var data = ReadMemory(address.Address, 4);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToUInt32(data, 0);
            }
        }
        else if (type == typeof(int))
        {
            var data = ReadMemory(address.Address, 4);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToInt32(data, 0);
            }
        }
        else if (type == typeof(float))
        {
            var data = ReadMemory(address.Address, 4);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToSingle(data, 0);
            }
        }
        else if (type == typeof(ulong))
        {
            var data = ReadMemory(address.Address, 8);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToUInt64(data, 0);
            }
        }
        else if (type == typeof(long))
        {
            var data = ReadMemory(address.Address, 8);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToInt64(data, 0);
            }
        }
        else if (type == typeof(double))
        {
            var data = ReadMemory(address.Address, 8);
            if (data != null)
            {
                Array.Reverse(data); // Convert from big-endian
                return (T)(object)BitConverter.ToDouble(data, 0);
            }
        }
        
        return null;
    }
    
    public bool Write<T>(MemoryAddress<T> address, T value) where T : struct
    {
        if (!IsConnected)
            return false;
            
        var type = typeof(T);
        byte[] data;
        
        // Convert value to bytes based on type
        if (type == typeof(byte))
        {
            data = new[] { (byte)(object)value };
        }
        else if (type == typeof(ushort))
        {
            data = BitConverter.GetBytes((ushort)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else if (type == typeof(short))
        {
            data = BitConverter.GetBytes((short)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else if (type == typeof(uint))
        {
            data = BitConverter.GetBytes((uint)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else if (type == typeof(int))
        {
            data = BitConverter.GetBytes((int)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else if (type == typeof(float))
        {
            data = BitConverter.GetBytes((float)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else if (type == typeof(ulong))
        {
            data = BitConverter.GetBytes((ulong)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else if (type == typeof(long))
        {
            data = BitConverter.GetBytes((long)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else if (type == typeof(double))
        {
            data = BitConverter.GetBytes((double)(object)value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(data); // Convert to big-endian
        }
        else
        {
            return false; // Unsupported type
        }
        
        return WriteMemory(address.Address, data);
    }
    
    public T? ReadWithOffset<T>(uint baseAddress, MemoryAddress<T> offset) where T : struct
    {
        var adjustedAddress = new MemoryAddress<T>(baseAddress + offset.Address, offset.Name, offset.Description);
        return Read(adjustedAddress);
    }
    
    public bool WriteWithOffset<T>(uint baseAddress, MemoryAddress<T> offset, T value) where T : struct
    {
        var adjustedAddress = new MemoryAddress<T>(baseAddress + offset.Address, offset.Name, offset.Description);
        return Write(adjustedAddress, value);
    }
    
    public byte[]? ReadBytesWithOffset(uint baseAddress, ByteArrayMemoryAddress offset)
    {
        var adjustedAddress = new ByteArrayMemoryAddress(baseAddress + offset.Address, offset.Length, offset.Name);
        return ReadBytes(adjustedAddress);
    }
    
    public bool WriteBytesWithOffset(uint baseAddress, ByteArrayMemoryAddress offset, byte[] data)
    {
        var adjustedAddress = new ByteArrayMemoryAddress(baseAddress + offset.Address, offset.Length, offset.Name);
        return WriteBytes(adjustedAddress, data);
    }
    
    public string? ReadString(StringMemoryAddress address)
    {
        if (!IsConnected)
            return null;
            
        var data = ReadMemory(address.Address, address.MaxLength);
        if (data == null)
            return null;
            
        // Find null terminator
        int length = Array.IndexOf(data, (byte)0);
        if (length == -1)
            length = data.Length;
            
        return Encoding.ASCII.GetString(data, 0, length);
    }
    
    public bool WriteString(StringMemoryAddress address, string value)
    {
        if (!IsConnected)
            return false;
            
        // Convert string to bytes with null terminator
        var bytes = Encoding.ASCII.GetBytes(value);
        var data = new byte[Math.Min(bytes.Length + 1, address.MaxLength)];
        Array.Copy(bytes, data, Math.Min(bytes.Length, data.Length - 1));
        data[data.Length - 1] = 0; // Null terminator
        
        return WriteMemory(address.Address, data);
    }
    
    public byte[]? ReadBytes(ByteArrayMemoryAddress address)
    {
        if (!IsConnected)
            return null;
            
        return ReadMemory(address.Address, address.Length);
    }
    
    public bool WriteBytes(ByteArrayMemoryAddress address, byte[] data)
    {
        if (!IsConnected)
            return false;
            
        // Ensure we don't write more than the specified length
        var writeData = data;
        if (data.Length > address.Length)
        {
            writeData = new byte[address.Length];
            Array.Copy(data, writeData, address.Length);
        }
        
        return WriteMemory(address.Address, writeData);
    }
    
    public Task<GameState?> ReadGameStateAsync()
    {
        if (!IsConnected)
            return Task.FromResult<GameState?>(null);
        
        try
        {
            var state = new GameState
            {
                Timestamp = DateTime.UtcNow
            };
            
            // Batch read player health and rupees (they're close in memory)
            // Max Health: 0x803C4C08, Current Health: 0x803C4C0A, Rupees: 0x803C4C0C
            var healthAndRupees = ReadMemory(0x803C4C08, 6);
            if (healthAndRupees != null && healthAndRupees.Length == 6)
            {
                state.Player.MaxHealth = (ushort)((healthAndRupees[0] << 8) | healthAndRupees[1]);
                state.Player.CurrentHealth = (ushort)((healthAndRupees[2] << 8) | healthAndRupees[3]);
                state.Player.RupeeCount = (ushort)((healthAndRupees[4] << 8) | healthAndRupees[5]);
            }
            
            // Read Link actor pointer for position
            var linkPtr = Read(GameMemoryAddresses.Player.LinkActorPointer);
            Logger.Debug("Link actor pointer: 0x{LinkPtr:X8}", linkPtr ?? 0);
            
            if (linkPtr.HasValue && linkPtr.Value != 0 && linkPtr.Value >= 0x80000000)
            {
                uint positionAddress = linkPtr.Value + GameMemoryAddresses.Player.LinkPositionXOffset.Address;
                Logger.Debug("Reading position from: 0x{PosAddr:X8}", positionAddress);
                
                // Read all 3 position floats in one go (X, Y, Z are consecutive)
                var positionData = ReadMemory(positionAddress, 12);
                if (positionData != null && positionData.Length == 12)
                {
                    // Convert big-endian floats
                    var xBytes = new byte[4];
                    var yBytes = new byte[4];
                    var zBytes = new byte[4];
                    
                    Array.Copy(positionData, 0, xBytes, 0, 4);
                    Array.Copy(positionData, 4, yBytes, 0, 4);
                    Array.Copy(positionData, 8, zBytes, 0, 4);
                    
                    Array.Reverse(xBytes);
                    Array.Reverse(yBytes);
                    Array.Reverse(zBytes);
                    
                    state.Player.PositionX = BitConverter.ToSingle(xBytes, 0);
                    state.Player.PositionY = BitConverter.ToSingle(yBytes, 0);
                    state.Player.PositionZ = BitConverter.ToSingle(zBytes, 0);
                    
                    // Log position occasionally for debugging
                    if (DateTime.UtcNow.Second % 10 == 0 && DateTime.UtcNow.Millisecond < 20)
                    {
                        Logger.Information("Player position: ({X:F1}, {Y:F1}, {Z:F1})", 
                            state.Player.PositionX, state.Player.PositionY, state.Player.PositionZ);
                    }
                }
            }
            
            // Read status flags (2 bytes)
            var statusFlags = ReadMemory(GameMemoryAddresses.Player.PlayerStatusBitfield0.Address, 2);
            if (statusFlags != null && statusFlags.Length == 2)
            {
                state.Player.StatusFlags1 = statusFlags[0];
                state.Player.StatusFlags2 = statusFlags[1];
            }
            
            // Equipment: the puppet REL swaps a peer's ids into these bytes while it runs their
            // puppet — keep the last value read outside a swap.
            if (EquipSwapGuard.ReadEquipped(this) is { } equipped)
                (_lastSword, _lastShield) = equipped;
            state.Player.CurrentSword = _lastSword;
            state.Player.CurrentShield = _lastShield;
            
            // Read magic meters (max and current are consecutive)
            var magic = ReadMemory(GameMemoryAddresses.Player.MaxMagicMeter.Address, 2);
            if (magic != null && magic.Length == 2)
            {
                state.Player.MaxMagic = magic[0];
                state.Player.CurrentMagic = magic[1];
            }
            
            // Read consumables
            state.Player.CurrentArrows = Read(GameMemoryAddresses.Player.CurrentArrowCount) ?? 0;
            state.Player.CurrentBombs = Read(GameMemoryAddresses.Player.CurrentBombCount) ?? 0;
            state.Player.MaxArrows = Read(GameMemoryAddresses.Inventory.MaxArrows) ?? 0;
            state.Player.MaxBombs = Read(GameMemoryAddresses.Inventory.MaxBombs) ?? 0;
            
            // Read location
            state.Player.CurrentSector = Read(GameMemoryAddresses.Sea.CurrentSeaSector) ?? 0;

            // Read power bracelets and wallet
            state.Player.PowerBracelets = Read(GameMemoryAddresses.Player.PowerBracelets) ?? 0;
            state.Player.Wallet = Read(GameMemoryAddresses.Player.CurrentWallet) ?? 0;

            // Batch read inventory items + ownership (contiguous: 0x803C4C44 to 0x803C4C6E = 42 bytes)
            var inventoryBlock = ReadMemory(0x803C4C44, 42);
            if (inventoryBlock != null && inventoryBlock.Length == 42)
            {
                // Items: bytes 0-20 (21 bytes at 0x803C4C44)
                Array.Copy(inventoryBlock, 0, state.Inventory.Items, 0, 21);
                // Item ownership: bytes 21-41 (21 bytes at 0x803C4C59)
                Array.Copy(inventoryBlock, 21, state.Inventory.ItemOwnership, 0, 21);
            }

            // Equipment / songs / shards / pearls: dSv_player_collect_c, one block read.
            const uint collectBase = GameMemoryAddresses.Inventory.CollectBase;
            var collect = ReadMemory(collectBase, (int)(GameMemoryAddresses.Inventory.CollectEnd - collectBase));
            if (collect != null && collect.Length == GameMemoryAddresses.Inventory.CollectEnd - collectBase)
            {
                byte At(MemoryAddress<byte> a) => collect[a.Address - collectBase];
                state.Inventory.SwordBitfield = At(GameMemoryAddresses.Inventory.SwordsBitfield);
                state.Inventory.ShieldBitfield = At(GameMemoryAddresses.Inventory.ShieldsBitfield);
                state.Inventory.PowerBraceletsBitfield = At(GameMemoryAddresses.Inventory.PowerBraceletsBitfield);
                state.Inventory.PiratesCharmBitfield = At(GameMemoryAddresses.Inventory.PiratesCharmBitfield);
                state.Inventory.HerosCharmBitfield = At(GameMemoryAddresses.Inventory.HerosCharmBitfield);
                state.Inventory.SongsBitfield = At(GameMemoryAddresses.Inventory.SongsBitfield);
                state.Inventory.TriforceShards = At(GameMemoryAddresses.Inventory.TriforceShards);
                state.Inventory.PearlsBitfield = At(GameMemoryAddresses.Inventory.PearlsBitfield);
            }

            // Read bag contents (24 bytes at 0x803C4C7E)
            var bagContents = ReadBytes(GameMemoryAddresses.Inventory.BagContents);
            if (bagContents != null)
                Array.Copy(bagContents, 0, state.Inventory.BagContents, 0, Math.Min(bagContents.Length, 24));

            // Read event flags (256 bytes at 0x803C522C)
            var eventFlags = ReadBytes(GameMemoryAddresses.Events.EventBitfield);
            if (eventFlags != null)
                Array.Copy(eventFlags, 0, state.EventFlags, 0, Math.Min(eventFlags.Length, 256));

            // Read stage info (576 bytes at 0x803C4F88)
            var stageInfo = ReadBytes(GameMemoryAddresses.Stage.StageInfoList);
            if (stageInfo != null)
                Array.Copy(stageInfo, 0, state.StageInfo, 0, Math.Min(stageInfo.Length, 576));

            // Read current stage name (8-byte string at 0x803C9D3C) — used to distinguish title menu from in-game
            var stageName = ReadString(GameMemoryAddresses.Stage.CurrentStageName);
            state.StageName = stageName?.TrimEnd('\0') ?? "";

            // Calculate checksum
            state.Checksum = state.CalculateChecksum();

            return Task.FromResult<GameState?>(state);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read game state");
            return Task.FromResult<GameState?>(null);
        }
    }
    
    public void Disconnect()
    {
        var wasConnected = IsConnected;
        _memoryReader.Disconnect();
        if (wasConnected)
            ConnectionChanged?.Invoke(this, false);
    }
    
    public void Dispose()
    {
        _memoryReader.Dispose();
    }
}