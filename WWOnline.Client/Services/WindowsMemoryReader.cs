using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace WWOnline.Services;

public class WindowsMemoryReader : IDisposable
{
    private Process? _process;
    private IntPtr _processHandle;
    private IntPtr _dolphinBaseAddress = IntPtr.Zero;
    private IntPtr _emulatedMemoryAddress = IntPtr.Zero;
    private readonly Serilog.ILogger _logger = Log.ForContext<WindowsMemoryReader>();
    
    public bool IsConnected => _processHandle != IntPtr.Zero && _emulatedMemoryAddress != IntPtr.Zero && IsProcessRunning();

    public int? ConnectedProcessId => _process != null && !_process.HasExited ? _process.Id : null;

    // Windows API imports
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);
    
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool QueryWorkingSetEx(IntPtr hProcess, IntPtr pv, uint cb);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    // Process access flags
    private const int PROCESS_VM_READ = 0x0010;
    private const int PROCESS_VM_WRITE = 0x0020;
    private const int PROCESS_VM_OPERATION = 0x0008;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int PROCESS_ALL_ACCESS = 0x1F0FFF;

    // Memory protection constants
    private const uint MEM_COMMIT = 0x1000;
    private const uint PAGE_READWRITE = 0x04;

    public Task<bool> ConnectAsync() => ConnectAsync(null);

    public async Task<bool> ConnectAsync(int? processId)
    {
        // Release any prior handle first so reconnecting to a different PID works cleanly
        Disconnect();

        try
        {
            Process? target = null;
            if (processId.HasValue)
            {
                try
                {
                    target = Process.GetProcessById(processId.Value);
                    if (target.HasExited || !string.Equals(target.ProcessName, "Dolphin", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Warning("PID {Pid} is not a running Dolphin process", processId.Value);
                        return false;
                    }
                }
                catch (ArgumentException)
                {
                    _logger.Warning("No process found with PID {Pid}", processId.Value);
                    return false;
                }
            }
            else
            {
                var processes = Process.GetProcessesByName("Dolphin");
                if (processes.Length == 0)
                {
                    _logger.Warning("Dolphin process not found");
                    return false;
                }
                target = processes[0];
            }

            _process = target;
            _logger.Information("Found Dolphin process with PID: {ProcessId}", _process.Id);

            // Open process with required access
            _processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, _process.Id);
            if (_processHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.Error("Failed to open Dolphin process. Error code: {ErrorCode}", error);
                return false;
            }

            _logger.Information("Successfully opened Dolphin process handle");

            // Get Dolphin's base address
            _dolphinBaseAddress = _process.MainModule?.BaseAddress ?? IntPtr.Zero;
            if (_dolphinBaseAddress == IntPtr.Zero)
            {
                _logger.Error("Failed to get Dolphin base address");
                CloseHandle(_processHandle);
                return false;
            }
            _logger.Information("Dolphin base address: 0x{Address:X}", _dolphinBaseAddress.ToInt64());

            // Find emulated memory using Dolphin's memory structure
            if (!await FindEmulatedMemory())
            {
                _logger.Error("Failed to find emulated memory");
                CloseHandle(_processHandle);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize Windows memory reader");
            return false;
        }
    }

    private async Task<bool> FindEmulatedMemory()
    {
        try
        {
            // First, try direct memory scan which is more reliable
            _logger.Information("Searching for Dolphin's emulated memory regions");
            if (await FindMemoryByScan())
            {
                return true;
            }
            
            // If that fails, try pointer chain approach
            _logger.Information("Trying pointer chain approach");
            
            // Common pointer offsets for different Dolphin versions
            // These offsets point to where Dolphin stores the emulated memory base address
            long[] pointerOffsets = new long[] { 
                0x2BFFC58,  // Dolphin 5.0 stable
                0x2C5FC58,  // Dolphin 5.0-12xxx
                0x2D1FC58,  // Dolphin 5.0-13xxx
                0x2D6FC58,  // Dolphin 5.0-14xxx
                0x2E1FC58,  // Dolphin 5.0-15xxx
                0x2E7FC58,  // Dolphin 5.0-16xxx
                0x2EDFC58,  // Dolphin 5.0-17xxx
                0x2F3FC58,  // Dolphin 5.0-18xxx
                0x2F9FC58,  // Dolphin 5.0-19xxx
                0x2FFFC58,  // Dolphin 5.0-20xxx
                0x305FC58,  // Dolphin 5.0-21xxx
                0xE1A9C8,   // Older Dolphin versions
                0x2040E08,  // Some newer builds
                0x2040E10,  // Alternative offset
            };
            
            foreach (var offset in pointerOffsets)
            {
                IntPtr potentialAddress = IntPtr.Add(_dolphinBaseAddress, (int)offset);
                // Trying offset
                
                // Read the pointer at this offset
                byte[] buffer = new byte[8];
                if (ReadProcessMemory(_processHandle, potentialAddress, buffer, 8, out int bytesRead) && bytesRead == 8)
                {
                    // Get the address that this points to
                    long pointedAddress = BitConverter.ToInt64(buffer, 0);
                    
                    // Check if this looks like a valid pointer (should be in reasonable range)
                    if (pointedAddress > 0x10000000 && pointedAddress < 0x7FFFFFFFFFFF)
                    {
                        // Found potential pointer
                        
                        // Verify this is a valid memory region by trying to read from it
                        IntPtr memoryBase = new IntPtr(pointedAddress);
                        byte[] testBuffer = new byte[4];
                        if (ReadProcessMemory(_processHandle, memoryBase, testBuffer, 4, out _))
                        {
                            _emulatedMemoryAddress = memoryBase;
                            _logger.Information("Successfully found emulated memory via pointer at: 0x{Address:X}", pointedAddress);
                            
                            // Test reading a known address (e.g., rupee count at 0x803C4C08)
                            var rupeeTest = ReadBytes(0x803C4C08, 2);
                            if (rupeeTest != null)
                            {
                                _logger.Information("Verified memory access - test read successful");
                            }
                            
                            return true;
                        }
                    }
                }
            }
            
            _logger.Error("Failed to find Dolphin's emulated memory");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error finding emulated memory");
            return false;
        }
    }
    
    private Task<bool> ScanForGameData()
    {
        try
        {
            // The 2PUS patch may shift memory addresses, so we need to search
            // We'll scan through possible offsets where the game data might be
            
            // Common memory shifts with 2PUS patch
            uint[] possibleOffsets = new uint[] {
                0x00000000,  // No shift (standard)
                0x00800000,  // 8MB shift
                0x01000000,  // 16MB shift
                0x01800000,  // 24MB shift (common with 2PUS)
                0x00400000,  // 4MB shift
                0x00200000,  // 2MB shift
                0x00100000,  // 1MB shift
            };
            
            foreach (uint offset in possibleOffsets)
            {
                _logger.Debug("Testing with offset 0x{Offset:X}...", offset);
                
                // Test 1: Check for game ID "GZLE01" at 0x80000000 + offset
                var gameIdTest = ReadBytes(0x80000000 + offset, 6);
                if (gameIdTest != null)
                {
                    string gameId = System.Text.Encoding.ASCII.GetString(gameIdTest);
                    _logger.Debug("Game ID at offset 0x{Offset:X}: {GameId}", offset, gameId);
                    
                    if (gameId.StartsWith("GZL"))
                    {
                        _logger.Information("Found Wind Waker game ID '{GameId}' with offset 0x{Offset:X}", gameId, offset);
                        
                        // Now verify with game data at the shifted addresses
                        var rupeeTest = ReadBytes(0x803C4C0C + offset, 2);
                        var healthTest = ReadBytes(0x803C4C08 + offset, 2);
                        var maxHealthTest = ReadBytes(0x803C4C0A + offset, 2);
                        
                        if (rupeeTest != null && healthTest != null && maxHealthTest != null)
                        {
                            Array.Reverse(rupeeTest);
                            Array.Reverse(healthTest);
                            Array.Reverse(maxHealthTest);
                            
                            ushort rupees = BitConverter.ToUInt16(rupeeTest, 0);
                            ushort health = BitConverter.ToUInt16(healthTest, 0);
                            ushort maxHealth = BitConverter.ToUInt16(maxHealthTest, 0);
                            
                            _logger.Information("Game data with offset 0x{Offset:X}: Rupees={Rupees}, Health={Health}/{MaxHealth}", 
                                offset, rupees, health, maxHealth);
                            
                            // Validate the data
                            bool validRupees = rupees <= 9999;
                            bool validHealth = health <= 200 && maxHealth >= 12 && maxHealth <= 200;

                            if (validRupees && validHealth)
                            {
                                _logger.Information("✓ Valid game data found! Memory offset is 0x{Offset:X}", offset);

                                // Store the offset for future use
                                _memoryOffset = offset;
                                return Task.FromResult(true);
                            }
                        }
                    }
                }
            }
            
            _logger.Warning("Could not find valid Wind Waker data with any offset");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error scanning for game data");
            return Task.FromResult(false);
        }
    }
    
    private uint _memoryOffset = 0;
    
    private async Task<bool> FindMemoryByScan()
    {
        try
        {
            // Scanning for Dolphin's emulated memory using DME method
            
            const long MEM1_SIZE_24MB = 0x1800000; // 24MB standard GameCube
            const long MEM1_SIZE_48MB = 0x3000000; // 48MB for multiplayer mods
            
            MEMORY_BASIC_INFORMATION memInfo;
            IntPtr address = IntPtr.Zero;
            int regionCount = 0;
            List<IntPtr> candidateRegions = new List<IntPtr>();
            
            // First pass: Find all regions that could be memory
            while (VirtualQueryEx(_processHandle, address, out memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))))
            {
                regionCount++;
                
                // Check if this is a committed, readable region
                if (memInfo.State == MEM_COMMIT && 
                    (memInfo.Protect & (PAGE_READWRITE | 0x40 | 0x20 | 0x04 | 0x02)) != 0)
                {
                    long regionSize = memInfo.RegionSize.ToInt64();
                    
                    // Check for exact sizes OR close matches
                    if (regionSize == MEM1_SIZE_48MB)
                    {
                        _logger.Information("Found exact 48MB region at 0x{Address:X}", memInfo.BaseAddress.ToInt64());
                        candidateRegions.Insert(0, memInfo.BaseAddress); // Priority to 48MB
                    }
                    else if (regionSize == MEM1_SIZE_24MB)
                    {
                        _logger.Information("Found exact 24MB region at 0x{Address:X}", memInfo.BaseAddress.ToInt64());
                        candidateRegions.Add(memInfo.BaseAddress);
                    }
                    else if (regionSize >= 20 * 1024 * 1024 && regionSize <= 64 * 1024 * 1024)
                    {
                        // Any region between 20MB and 64MB could be game memory
                        _logger.Debug("Found potential memory region ({Size}MB) at 0x{Address:X}", 
                            regionSize / (1024 * 1024), memInfo.BaseAddress.ToInt64());
                        candidateRegions.Add(memInfo.BaseAddress);
                    }
                }
                
                // Move to next region
                address = IntPtr.Add(memInfo.BaseAddress, (int)Math.Min(memInfo.RegionSize.ToInt64(), int.MaxValue));
                
                // Prevent infinite loop
                if (regionCount > 100000)
                {
                    _logger.Warning("Scanned 100000 regions, stopping");
                    break;
                }
            }
            
            _logger.Information("Found {Count} candidate memory regions to test", candidateRegions.Count);
            
            // Now test each candidate region to find the right one
            foreach (var candidate in candidateRegions)
            {
                _logger.Information("Testing region at 0x{Address:X}...", candidate.ToInt64());
                _emulatedMemoryAddress = candidate;
                
                // Try to find the game by scanning for valid game data
                bool foundValidGame = await ScanForGameData();
                
                if (foundValidGame)
                {
                    _logger.Information("✓ Found valid Wind Waker game data in region at 0x{Address:X}", candidate.ToInt64());
                    return true;
                }
            }
            
            // If exact match not found, try regions close to 24MB or 48MB
            _logger.Information("No exact 24MB/48MB region found, trying close matches");
            address = IntPtr.Zero;
            regionCount = 0;
            
            while (VirtualQueryEx(_processHandle, address, out memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))))
            {
                regionCount++;
                if (memInfo.State == MEM_COMMIT && 
                    (memInfo.Protect & (PAGE_READWRITE | 0x40 | 0x20 | 0x04 | 0x02)) != 0)
                {
                    long regionSize = memInfo.RegionSize.ToInt64();
                    
                    // Look for regions between 24-32MB or around 48MB
                    if ((regionSize >= MEM1_SIZE_24MB && regionSize <= 0x2000000L) || 
                        (regionSize >= 0x2800000L && regionSize <= 0x3800000L)) // 40-56MB range for 48MB variants
                    {
                        _logger.Information("Testing {Size}MB region at 0x{Address:X}", 
                            regionSize / (1024 * 1024), memInfo.BaseAddress.ToInt64());
                        
                        byte[] testBuffer = new byte[256];
                        if (ReadProcessMemory(_processHandle, memInfo.BaseAddress, testBuffer, 256, out _))
                        {
                            _emulatedMemoryAddress = memInfo.BaseAddress;
                            
                            // Test by reading multiple known values for better validation
                            bool isValidRegion = false;
                            
                            // Test 1: Read rupee count (should be 0-9999)
                            var rupeeTest = ReadBytes(0x803C4C0C, 2);
                            if (rupeeTest != null)
                            {
                                Array.Reverse(rupeeTest); // Convert from big-endian
                                ushort rupees = BitConverter.ToUInt16(rupeeTest, 0);
                                _logger.Information("Test read from this region - Rupees: {Rupees}", rupees);
                                
                                // Test 2: Read health (should be 0-20 hearts * 4 = 0-80 quarter hearts typically)
                                var healthTest = ReadBytes(0x803C4C08, 2);
                                if (healthTest != null)
                                {
                                    Array.Reverse(healthTest);
                                    ushort health = BitConverter.ToUInt16(healthTest, 0);
                                    _logger.Information("Health: {Health}", health);
                                    
                                    // Test 3: Read magic meter (should be 0-48 typically)
                                    var magicTest = ReadBytes(0x803C4C1B, 1);
                                    if (magicTest != null)
                                    {
                                        byte magic = magicTest[0];
                                        _logger.Information("Magic: {Magic}", magic);
                                        
                                        // Validate all values are in reasonable ranges
                                        // Rupees: 0-9999, Health: 0-200 (allowing for extended), Magic: 0-100
                                        if (rupees < 10000 && health <= 200 && magic <= 100)
                                        {
                                            // With 48MB configuration, game ID validation may fail
                                        // So we'll be more lenient and accept based on data validity
                                        var gameIdTest = ReadBytes(0x80000000, 6);
                                        if (gameIdTest != null)
                                        {
                                            string gameId = System.Text.Encoding.ASCII.GetString(gameIdTest);
                                            _logger.Information("Game ID: {GameId}", gameId);
                                            
                                            // Check if it's Wind Waker (GZLE01 for USA)
                                            if (gameId.StartsWith("GZL"))
                                            {
                                                isValidRegion = true;
                                                _logger.Information("Verified Wind Waker game ID!");
                                            }
                                        }
                                        
                                        // More lenient validation for 48MB configurations
                                        // Accept if game values look reasonable
                                        if (!isValidRegion)
                                        {
                                            // Validate based on actual game state
                                            bool hasReasonableRupees = rupees >= 0 && rupees <= 9999;
                                            bool hasMinimumHealth = health >= 12; // At least 3 hearts (4 health per heart)
                                            bool hasReasonableMagic = magic <= 255;
                                            
                                            // Accept if we have reasonable rupees AND minimum health
                                            if (hasReasonableRupees && hasMinimumHealth)
                                            {
                                                isValidRegion = true;
                                                _logger.Information("Found valid game region!");
                                                _logger.Information("Rupees: {Rupees}, Health: {Health}, Magic: {Magic}", rupees, health, magic);
                                                
                                                // Extra validation - if we have exactly 5 rupees, that's our region!
                                                if (rupees == 5)
                                                {
                                                    _logger.Information("✓ Found exact rupee match (5) - this is definitely the right region!");
                                                }
                                            }
                                        }
                                        }
                                    }
                                }
                                
                                if (isValidRegion)
                                {
                                    _logger.Information("Using memory region at 0x{Address:X} ({Size}MB)", 
                                        memInfo.BaseAddress.ToInt64(), regionSize / (1024 * 1024));
                                    return true;
                                }
                                else
                                {
                                    _logger.Warning("Region validation failed, trying next region");
                                    _emulatedMemoryAddress = IntPtr.Zero;
                                }
                            }
                        }
                    }
                }
                
                address = IntPtr.Add(memInfo.BaseAddress, (int)Math.Min(memInfo.RegionSize.ToInt64(), int.MaxValue));
                if (regionCount > 100000) break;
            }
            
            _logger.Error("Failed to find valid MEM1 region after scanning {Count} regions", regionCount);
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in memory scan");
            return false;
        }
    }

    public byte[]? ReadBytes(ulong address, int size)
    {
        if (_processHandle == IntPtr.Zero || _emulatedMemoryAddress == IntPtr.Zero)
        {
            _logger.Warning("Process handle or memory base not initialized");
            return null;
        }

        try
        {
            // Apply discovered memory offset (for 2PUS patch compatibility)
            ulong adjustedAddress = address + _memoryOffset;
            
            // Convert GameCube address to process memory address
            // GameCube addresses start at 0x80000000, we need to subtract that and add to our base
            ulong gcOffset = adjustedAddress - 0x80000000;
            IntPtr targetAddress = IntPtr.Add(_emulatedMemoryAddress, (int)gcOffset);

            var buffer = new byte[size];
            if (ReadProcessMemory(_processHandle, targetAddress, buffer, size, out int bytesRead) && bytesRead == size)
            {
                return buffer;
            }

            _logger.Warning("Failed to read {Size} bytes from address 0x{Address:X} (adjusted: 0x{Adjusted:X}, target: 0x{Target:X})", 
                size, address, adjustedAddress, targetAddress.ToInt64());
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading memory at address 0x{Address:X}", address);
            return null;
        }
    }

    public bool WriteBytes(ulong address, byte[] data)
    {
        if (_processHandle == IntPtr.Zero || _emulatedMemoryAddress == IntPtr.Zero)
        {
            _logger.Warning("Process handle or memory base not initialized");
            return false;
        }

        try
        {
            // Apply discovered memory offset (for 2PUS patch compatibility)
            ulong adjustedAddress = address + _memoryOffset;
            
            // Convert GameCube address to process memory address
            ulong gcOffset = adjustedAddress - 0x80000000;
            IntPtr targetAddress = IntPtr.Add(_emulatedMemoryAddress, (int)gcOffset);

            if (WriteProcessMemory(_processHandle, targetAddress, data, data.Length, out int bytesWritten) && bytesWritten == data.Length)
            {
                _logger.Debug("Successfully wrote {Size} bytes to address 0x{Address:X} (adjusted: 0x{Adjusted:X})", 
                    data.Length, address, adjustedAddress);
                return true;
            }

            _logger.Warning("Failed to write {Size} bytes to address 0x{Address:X}", data.Length, address);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing memory at address 0x{Address:X}", address);
            return false;
        }
    }

    public void Disconnect()
    {
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
        _emulatedMemoryAddress = IntPtr.Zero;
        _memoryOffset = 0;
        _dolphinBaseAddress = IntPtr.Zero;
        _process?.Dispose();
        _process = null;
    }

    public void Dispose()
    {
        Disconnect();
    }

    public static IReadOnlyList<DolphinProcessInfo> EnumerateDolphinProcesses()
    {
        var result = new List<DolphinProcessInfo>();
        foreach (var p in Process.GetProcessesByName("Dolphin"))
        {
            try
            {
                if (p.HasExited) continue;
                result.Add(new DolphinProcessInfo(p.Id, p.MainWindowTitle ?? string.Empty));
            }
            catch
            {
                // Process may have exited between enumeration and access — skip
            }
            finally
            {
                p.Dispose();
            }
        }
        return result;
    }

    private bool IsProcessRunning()
    {
        return _process != null && !_process.HasExited;
    }
}