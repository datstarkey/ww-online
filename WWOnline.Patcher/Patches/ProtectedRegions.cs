using System.Globalization;
using System.Text.RegularExpressions;
using WWOnline.Patcher.BinaryFormats.Dol;
using WWOnline.Patcher.Tools;

namespace WWOnline.Patcher.Patches;

/// <summary>A half-open RAM range [Start, End).</summary>
public sealed record AddressRange(uint Start, uint End, string Name)
{
    public bool Overlaps(uint start, uint end) => start < End && end > Start;
    public override string ToString() => $"{Name} 0x{Start:X8}..0x{End - 1:X8}";
}

/// <summary>
/// main.dol RAM that patches must not write (GameMod/CLAUDE.md "Memory map"):
/// <list type="bullet">
/// <item>the DOL's .sbss2 (live game constants) and our scratch/sync region — no patch at all;</item>
/// <item>the draw-hook code and its branch site — only the required link_draw_hook patch.</item>
/// </list>
/// The scratch region bounds come from puppet_shared.h (the single source of truth): the pipeline
/// reads the header, the client passes its generated PuppetLayout constants.
/// </summary>
public sealed class ProtectedRegions
{
    public IReadOnlyList<AddressRange> NeverWritten { get; }
    public IReadOnlyList<AddressRange> RequiredPatchesOnly { get; }

    /// <summary>First main.dol address custom code may be placed at (@NextFreeSpace).</summary>
    public uint FirstFreeSpaceAddress { get; }

    public ProtectedRegions(uint scratchRegionStart, uint scratchRegionEnd)
    {
        if (scratchRegionEnd <= scratchRegionStart)
            throw new ArgumentException("Scratch region end must be after its start");
        NeverWritten =
        [
            new AddressRange(DolPatcher.Sbss2RamAddress, DolPatcher.OriginalFreeSpaceRamAddress, ".sbss2 (game constants)"),
            new AddressRange(scratchRegionStart, scratchRegionEnd, "scratch/sync region (puppet_shared.h)"),
        ];
        RequiredPatchesOnly =
        [
            new AddressRange(HookAsmGenerator.CodeAddress, HookAsmGenerator.CodeLimit, "draw hook code"),
            new AddressRange(HookAsmGenerator.HookAddress, HookAsmGenerator.HookAddress + 4, "draw hook branch"),
        ];
        FirstFreeSpaceAddress = Math.Max(scratchRegionEnd, DolPatcher.OriginalFreeSpaceRamAddress);
    }

    public static ProtectedRegions FromPuppetSharedHeader(string headerPath) => new(
        PuppetSharedHeader.ReadDefine(headerPath, "SCRATCH_REGION_START"),
        PuppetSharedHeader.ReadDefine(headerPath, "SCRATCH_REGION_END"));

    /// <summary>The first protected range a main.dol write hits, or null.</summary>
    public AddressRange? FindViolation(uint start, uint end, bool isOptionalPatch)
    {
        var hit = NeverWritten.FirstOrDefault(r => r.Overlaps(start, end));
        if (hit != null || !isOptionalPatch) return hit;
        return RequiredPatchesOnly.FirstOrDefault(r => r.Overlaps(start, end));
    }
}

/// <summary>Reads integer #defines from GameMod/src/puppet_link/puppet_shared.h (read-only).</summary>
public static partial class PuppetSharedHeader
{
    [GeneratedRegex(@"^\s*#define\s+([A-Za-z_][A-Za-z0-9_]*)\s+(0[xX][0-9A-Fa-f]+|[0-9]+)[uU]?\b")]
    private static partial Regex DefineRegex();

    public static uint ReadDefine(string headerPath, string name)
    {
        foreach (var line in File.ReadLines(headerPath))
        {
            var m = DefineRegex().Match(line);
            if (!m.Success || m.Groups[1].Value != name) continue;
            var value = m.Groups[2].Value;
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : uint.Parse(value, CultureInfo.InvariantCulture);
        }
        throw new InvalidDataException($"#define {name} not found in {headerPath}");
    }
}
