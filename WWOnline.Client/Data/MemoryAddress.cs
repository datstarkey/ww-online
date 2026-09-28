using System.Runtime.InteropServices;

namespace WWOnline.Data;

/// <summary>
/// Represents a memory address with type information for safe reading/writing
/// </summary>
public readonly struct MemoryAddress<T> where T : struct
{
    public uint Address { get; }
    public int Size { get; }
    public string Name { get; }
    public string Description { get; }
    
    public MemoryAddress(uint address, string name, string description = "")
    {
        Address = address;
        Name = name;
        Description = description;
        
        // Automatically determine size based on type
        Size = typeof(T) switch
        {
            Type t when t == typeof(byte) => 1,
            Type t when t == typeof(sbyte) => 1,
            Type t when t == typeof(ushort) => 2,
            Type t when t == typeof(short) => 2,
            Type t when t == typeof(uint) => 4,
            Type t when t == typeof(int) => 4,
            Type t when t == typeof(float) => 4,
            Type t when t == typeof(ulong) => 8,
            Type t when t == typeof(long) => 8,
            Type t when t == typeof(double) => 8,
            _ => Marshal.SizeOf<T>()
        };
    }
    
    /// <summary>
    /// Create a memory address with an offset from this address
    /// </summary>
    public MemoryAddress<T> WithOffset(uint offset)
    {
        return new MemoryAddress<T>(Address + offset, Name, Description);
    }
    
    public override string ToString()
    {
        return $"{Name} @ 0x{Address:X8} ({typeof(T).Name}, {Size} bytes)";
    }
}

/// <summary>
/// Special type for reading strings from memory
/// </summary>
public readonly struct StringMemoryAddress
{
    public uint Address { get; }
    public int MaxLength { get; }
    public string Name { get; }
    public string Description { get; }
    
    public StringMemoryAddress(uint address, int maxLength, string name, string description = "")
    {
        Address = address;
        MaxLength = maxLength;
        Name = name;
        Description = description;
    }
    
    public override string ToString()
    {
        return $"{Name} @ 0x{Address:X8} (string, max {MaxLength} chars)";
    }
}

/// <summary>
/// Special type for reading byte arrays from memory
/// </summary>
public readonly struct ByteArrayMemoryAddress
{
    public uint Address { get; }
    public int Length { get; }
    public string Name { get; }
    public string Description { get; }
    
    public ByteArrayMemoryAddress(uint address, int length, string name, string description = "")
    {
        Address = address;
        Length = length;
        Name = name;
        Description = description;
    }
    
    public override string ToString()
    {
        return $"{Name} @ 0x{Address:X8} (byte[{Length}])";
    }
}