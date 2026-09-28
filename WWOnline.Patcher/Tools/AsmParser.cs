using System.Text;
using System.Text.RegularExpressions;
using WWOnline.Patcher.BinaryFormats.Elf;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;

namespace WWOnline.Patcher.Tools;

/// <summary>
/// Parses .asm patch files and assembles them into binary diffs.
/// Ports assemble_patches.py's PatchAssembler class.
/// </summary>
public sealed partial class AsmParser : IDisposable
{
    private readonly DevKitPpcToolchain _toolchain;
    private readonly PatcherConfig _config;
    private readonly string _tempDir;

    private readonly Dictionary<string, Dictionary<string, uint>> _customSymbols = new();
    private readonly Dictionary<string, uint> _nextFreeSpaceOffsets;
    private readonly string _linkerScript;
    private readonly string _asmMacros;

    // Compiled regex patterns — avoid re-creating per line
    [GeneratedRegex(@";.+$")]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"^\s*\.open\s+""([^""]+)""$", RegexOptions.IgnoreCase)]
    private static partial Regex OpenRegex();

    [GeneratedRegex(@"^\s*\.org\s+0x([0-9a-fA-F]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex OrgHexRegex();

    [GeneratedRegex(@"^\s*\.org\s+([\._a-zA-Z][\._a-zA-Z0-9]+|@NextFreeSpace)$", RegexOptions.IgnoreCase)]
    private static partial Regex OrgSymbolRegex();

    [GeneratedRegex(@"^\s*(?:b|beq|bne|blt|bgt|ble|bge)\s+0x([0-9a-fA-F]+)(?:$|\s)", RegexOptions.IgnoreCase)]
    private static partial Regex BranchRegex();

    [GeneratedRegex(@"^\s*\.include\s+""([^""]+)""\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex IncludeRegex();

    [GeneratedRegex(@"^ .\S+ +0x")]
    private static partial Regex MapSectionHeaderRegex();

    [GeneratedRegex(@"^ +0x(?:00000000)?([0-9a-f]{8}) {16,}(\S+)")]
    private static partial Regex MapSymbolRegex();

    [GeneratedRegex(@"^\s*([A-Za-z_.$][\w.$]*)\s*=\s*0x([0-9A-Fa-f]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex LinkerSymbolRegex();

    [GeneratedRegex(@"^branch_label_([0-9A-F]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex BranchLabelRegex();

    private Dictionary<string, uint>? _linkerSymbols;

    public AsmParser(DevKitPpcToolchain toolchain, PatcherConfig config)
    {
        _toolchain = toolchain;
        _config = config;
        _tempDir = Path.Combine(Path.GetTempPath(), "wwo_patcher_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _customSymbols["sys/main.dol"] = new Dictionary<string, uint>();
        _nextFreeSpaceOffsets = PatcherConfig.LoadFreeSpaceOffsets(config.FreeSpaceOffsetsPath);
        _linkerScript = File.ReadAllText(config.LinkerScriptPath);
        _asmMacros = File.Exists(config.AsmMacrosPath) ? File.ReadAllText(config.AsmMacrosPath) : string.Empty;
    }

    public class PatchDiff
    {
        public Dictionary<string, Dictionary<uint, PatchChunk>> FilePatches { get; } = new();
    }

    public class PatchChunk
    {
        public byte[] Data { get; set; } = [];

        /// <summary>REL chunks only: branches into main.dol the REL loader must resolve.</summary>
        public List<PatchRelocation> Relocations { get; } = [];
    }

    /// <summary>
    /// Assemble a single .asm patch file and return the diffs.
    /// </summary>
    public async Task<PatchDiff> AssemblePatchAsync(string patchPath, CancellationToken ct = default)
    {
        var patchName = Path.GetFileNameWithoutExtension(patchPath);
        var asm = await File.ReadAllTextAsync(patchPath, ct);
        var asmWithIncludes = await ParseIncludesAsync(asm, Path.GetDirectoryName(patchPath)!, ct);

        // Parse the ASM into code chunks
        var codeChunks = new Dictionary<string, List<(object OrgKey, string Code)>>();
        var localBranchesLinker = new StringBuilder();
        var nextFreeSpaceId = new Dictionary<string, int>();
        var globalDefines = new StringBuilder();

        string? mostRecentFilePath = null;
        object? mostRecentOrgOffset = null;

        foreach (var rawLine in asmWithIncludes.Split('\n'))
        {
            var line = CommentRegex().Replace(rawLine, "").Trim();

            var openMatch = OpenRegex().Match(line);
            var orgMatch = OrgHexRegex().Match(line);
            var orgSymbolMatch = OrgSymbolRegex().Match(line);
            var branchMatch = BranchRegex().Match(line);

            if (openMatch.Success)
            {
                var relPath = openMatch.Groups[1].Value;
                if (mostRecentFilePath != null)
                    throw new InvalidOperationException(
                        $"File {mostRecentFilePath} not closed before opening {relPath}");
                if (!codeChunks.ContainsKey(relPath))
                    codeChunks[relPath] = new List<(object, string)>();
                mostRecentFilePath = relPath;
                continue;
            }

            if (orgMatch.Success)
            {
                if (mostRecentFilePath == null)
                    throw new InvalidOperationException("Found .org directive when no file was open");
                var orgOffset = Convert.ToUInt32(orgMatch.Groups[1].Value, 16);
                codeChunks[mostRecentFilePath].Add((orgOffset, ""));
                mostRecentOrgOffset = orgOffset;
                continue;
            }

            if (orgSymbolMatch.Success)
            {
                if (mostRecentFilePath == null)
                    throw new InvalidOperationException("Found .org directive when no file was open");
                var orgSymbol = orgSymbolMatch.Groups[1].Value;

                if (orgSymbol == "@NextFreeSpace")
                {
                    if (!nextFreeSpaceId.ContainsKey(mostRecentFilePath))
                        nextFreeSpaceId[mostRecentFilePath] = 1;
                    orgSymbol = $"@FreeSpace_{nextFreeSpaceId[mostRecentFilePath]}";
                    nextFreeSpaceId[mostRecentFilePath]++;
                }

                codeChunks[mostRecentFilePath].Add((orgSymbol, ""));
                mostRecentOrgOffset = orgSymbol;
                continue;
            }

            if (branchMatch.Success)
            {
                var branchDest = Convert.ToUInt32(branchMatch.Groups[1].Value, 16);
                var branchTempLabel = $"branch_label_{branchDest:X}";
                localBranchesLinker.AppendLine($"{branchTempLabel} = 0x{branchDest:X};");
                line = line.Replace("0x" + branchMatch.Groups[1].Value, branchTempLabel);
            }
            else if (line == ".close")
            {
                mostRecentFilePath = null;
                mostRecentOrgOffset = null;
                continue;
            }
            else if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (mostRecentFilePath == null)
            {
                if (line.StartsWith(';') || line == ".section \".text\"")
                    continue;
                if (line.StartsWith(".set "))
                {
                    globalDefines.AppendLine(line);
                    continue;
                }
                throw new InvalidOperationException($"Found code when no file was open:\n{line}");
            }

            if (mostRecentOrgOffset == null)
            {
                if (line.StartsWith(';') || line == ".section \".text\"")
                    continue;
                throw new InvalidOperationException($"Found code before any .org directive:\n{line}");
            }

            // Append to most recent chunk
            var chunks = codeChunks[mostRecentFilePath];
            var last = chunks[^1];
            chunks[^1] = (last.OrgKey, last.Code + line + "\n");
        }

        if (mostRecentFilePath != null)
            throw new InvalidOperationException($"File {mostRecentFilePath} was not closed");

        // Assemble each code chunk
        var diff = new PatchDiff();
        var localBranchesStr = localBranchesLinker.ToString();
        var globalDefinesStr = globalDefines.ToString();

        foreach (var (filePath, chunks) in codeChunks)
        {
            if (!_customSymbols.ContainsKey(filePath))
                _customSymbols[filePath] = new Dictionary<string, uint>();

            var tempLinkerScript = BuildLinkerScript(filePath, localBranchesStr);

            // Sort: free space chunks first
            var sortedChunks = chunks.OrderBy(c =>
                c.OrgKey is string s && s.StartsWith("@FreeSpace") ? -1 : 0).ToList();

            foreach (var (orgKey, tempAsm) in sortedChunks)
            {
                var usingFreeSpace = false;
                uint orgOffset;

                if (orgKey is uint uintKey)
                {
                    orgOffset = uintKey;
                }
                else
                {
                    var orgSymbol = (string)orgKey;
                    if (orgSymbol.StartsWith("@FreeSpace"))
                    {
                        orgOffset = _nextFreeSpaceOffsets.GetValueOrDefault(filePath, HookAsmGenerator.CodeAddress);
                        usingFreeSpace = true;
                    }
                    else if (_customSymbols.TryGetValue(filePath, out var syms) &&
                             syms.TryGetValue(orgSymbol, out var addr))
                    {
                        orgOffset = addr;
                    }
                    else
                    {
                        throw new InvalidOperationException($".org specified invalid symbol: {orgSymbol}");
                    }
                }

                // Write temp linker script
                var tempLinkerPath = Path.Combine(_tempDir, "tmp_linker.ld");
                await File.WriteAllTextAsync(tempLinkerPath, tempLinkerScript, ct);

                // Write temp asm file
                var tempAsmPath = Path.Combine(_tempDir, $"tmp_{patchName}_{orgOffset:X8}.asm");
                var asmContent = new StringBuilder();
                asmContent.AppendLine(_asmMacros);
                if (globalDefinesStr.Length > 0)
                    asmContent.AppendLine(globalDefinesStr);
                asmContent.Append(tempAsm);
                await File.WriteAllTextAsync(tempAsmPath, asmContent.ToString(), ct);

                // Assemble
                var objPath = Path.Combine(_tempDir, $"tmp_{patchName}_{orgOffset:X8}.o");
                var assembleResult = await _toolchain.AssembleAsync(tempAsmPath, objPath, ct);
                assembleResult.EnsureSuccess($"Assemble {patchName} at 0x{orgOffset:X8}");

                // Read ELF to get section alignments
                var elf = new ElfFile();
                elf.ReadFromFile(objPath);
                var orgOffsetForSection = new Dictionary<string, uint>();
                var currOrg = orgOffset;

                foreach (var section in elf.Sections)
                {
                    if (((uint)section.Flags & (uint)ElfSectionFlags.SHF_ALLOC) != 0)
                    {
                        var align = section.AddrAlign;
                        if (align > 0)
                            currOrg = currOrg + (align - currOrg % align) % align;
                        orgOffsetForSection[section.Name] = currOrg;
                        currOrg += section.Size;
                    }
                }

                var codeChunkSize = currOrg - orgOffset;

                // Check for duplicate symbols
                if (elf.Symbols.ContainsKey(".symtab"))
                {
                    foreach (var elfSymbol in elf.Symbols[".symtab"])
                    {
                        if (elfSymbol.SectionIndex < elf.Sections.Count &&
                            elf.Sections[(int)elfSymbol.SectionIndex].Name == ".text" &&
                            elfSymbol.Binding == ElfSymbolBinding.STB_GLOBAL &&
                            _customSymbols.TryGetValue(filePath, out var existingSyms) &&
                            existingSyms.ContainsKey(elfSymbol.Name))
                        {
                            throw new InvalidOperationException($"Duplicate symbol {elfSymbol.Name}");
                        }
                    }
                }

                if (filePath.EndsWith(".rel", StringComparison.OrdinalIgnoreCase))
                {
                    // RELs are relocated at load time, so they can't be linked to a fixed address:
                    // take the assembled .text and resolve its relocations ourselves.
                    if (usingFreeSpace)
                        throw new InvalidOperationException(
                            $"{patchName}: .org @NextFreeSpace in {filePath} isn't supported (REL free space needs a new REL section). " +
                            "Put custom code in main.dol free space and call it from the REL.");
                    var relChunk = ExtractRelChunk(elf, orgOffset, patchName, filePath);
                    if (!diff.FilePatches.ContainsKey(filePath))
                        diff.FilePatches[filePath] = new Dictionary<uint, PatchChunk>();
                    if (diff.FilePatches[filePath].ContainsKey(orgOffset))
                        throw new InvalidOperationException($"{patchName}: duplicate .org 0x{orgOffset:X} in {filePath}");
                    diff.FilePatches[filePath][orgOffset] = relChunk;
                    continue;
                }

                // Link to binary
                var binPath = Path.Combine(_tempDir, $"tmp_{patchName}_{orgOffset:X8}.bin");
                var mapPath = Path.Combine(_tempDir, $"tmp_{patchName}_{orgOffset:X8}.map");

                var linkResult = await _toolchain.LinkToBinaryAsync(
                    tempLinkerPath, objPath, binPath, mapPath,
                    orgOffsetForSection, ct);
                linkResult.EnsureSuccess($"Link {patchName} at 0x{orgOffset:X8}");

                // Read custom symbols from map file
                if (File.Exists(mapPath))
                {
                    var onCustomSymbols = false;
                    foreach (var mapLine in await File.ReadAllLinesAsync(mapPath, ct))
                    {
                        if (MapSectionHeaderRegex().IsMatch(mapLine))
                        {
                            onCustomSymbols = true;
                            continue;
                        }

                        if (onCustomSymbols)
                        {
                            var match = MapSymbolRegex().Match(mapLine);
                            if (match.Success)
                            {
                                var symbolAddress = Convert.ToUInt32(match.Groups[1].Value, 16);
                                var symbolName = match.Groups[2].Value;
                                _customSymbols[filePath][symbolName] = symbolAddress;
                                tempLinkerScript += $"{symbolName} = 0x{symbolAddress:X8};\n";
                            }
                        }
                    }
                }

                // Read the linked binary output directly (linker resolves all relocations)
                var binaryData = await File.ReadAllBytesAsync(binPath, ct);

                if (usingFreeSpace)
                    _nextFreeSpaceOffsets[filePath] = orgOffset + codeChunkSize;

                if (!diff.FilePatches.ContainsKey(filePath))
                    diff.FilePatches[filePath] = new Dictionary<uint, PatchChunk>();

                diff.FilePatches[filePath][orgOffset] = new PatchChunk { Data = binaryData };
            }
        }

        return diff;
    }

    /// <summary>
    /// Turn an assembled REL chunk (.o, not linked) into bytes + relocations. Branch labels (the
    /// "b 0x1234" form) and absolute main.dol references are resolved here; a branch/call into
    /// main.dol stays a relocation for RelBytePatcher to point an existing REL relocation at.
    /// Ported from wwrando's assemble.py (get_code_and_relocations_from_elf), MIT.
    /// </summary>
    private PatchChunk ExtractRelChunk(ElfFile elf, uint orgOffset, string patchName, string filePath)
    {
        foreach (var section in elf.Sections)
        {
            if (((uint)section.Flags & (uint)ElfSectionFlags.SHF_ALLOC) != 0 && section.Size > 0 && section.Name != ".text")
                throw new InvalidOperationException($"{patchName}: REL patches may only emit .text (found {section.Name} in {filePath})");
        }
        var data = elf.SectionsByName.TryGetValue(".text", out var text) ? (byte[])text.Data.Clone() : [];
        var chunk = new PatchChunk { Data = data };
        if (!elf.Relocations.TryGetValue(".rela.text", out var relocations)) return chunk;

        var symbols = elf.Symbols[".symtab"];
        foreach (var reloc in relocations)
        {
            var symbol = symbols[(int)reloc.SymbolIndex];
            var fieldOffset = (int)reloc.RelocationOffset;
            var branchLabel = BranchLabelRegex().Match(symbol.Name);
            if (branchLabel.Success || (symbol.SectionIndex != 0 && symbol.SectionIndex < elf.Sections.Count &&
                                        elf.Sections[symbol.SectionIndex].Name == ".text"))
            {
                // A branch to another place in the same REL (file offsets), resolved now.
                var dest = branchLabel.Success
                    ? Convert.ToUInt32(branchLabel.Groups[1].Value, 16)
                    : orgOffset + symbol.Address;
                var delta = (long)dest + (int)reloc.Addend - (orgOffset + reloc.RelocationOffset);
                PatchRelativeBranch(data, fieldOffset, reloc.Type, delta, patchName);
                continue;
            }

            if (!TryResolveMainSymbol(symbol.Name, out var address))
                throw new InvalidOperationException($"{patchName}: {filePath} references unknown symbol {symbol.Name}");
            address += reloc.Addend;
            switch (reloc.Type)
            {
                case ElfRelocationType.R_PPC_ADDR32:
                    WriteU32(data, fieldOffset, address);
                    break;
                case ElfRelocationType.R_PPC_ADDR16_LO:
                    WriteU16(data, fieldOffset, (ushort)(address & 0xFFFF));
                    break;
                case ElfRelocationType.R_PPC_ADDR16_HI:
                    WriteU16(data, fieldOffset, (ushort)(address >> 16));
                    break;
                case ElfRelocationType.R_PPC_ADDR16_HA:
                    WriteU16(data, fieldOffset, (ushort)((address >> 16) + ((address & 0x8000) != 0 ? 1 : 0)));
                    break;
                case ElfRelocationType.R_PPC_REL24:
                case ElfRelocationType.R_PPC_REL14:
                    chunk.Relocations.Add(new PatchRelocation(reloc.RelocationOffset, reloc.Type.ToString(), symbol.Name, address));
                    break;
                default:
                    throw new InvalidOperationException($"{patchName}: unsupported relocation {reloc.Type} to {symbol.Name} in {filePath}");
            }
        }
        return chunk;
    }

    private static void PatchRelativeBranch(byte[] data, int offset, ElfRelocationType type, long delta, string patchName)
    {
        var instruction = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
        switch (type)
        {
            case ElfRelocationType.R_PPC_REL24:
                if (delta > 0x1FFFFFF || delta < -0x2000000) throw new InvalidOperationException($"{patchName}: 24-bit branch out of range");
                instruction = (instruction & ~0x03FFFFFCu) | ((uint)delta & 0x03FFFFFCu);
                break;
            case ElfRelocationType.R_PPC_REL14:
            case ElfRelocationType.R_PPC_REL14_BRTAKEN:
            case ElfRelocationType.R_PPC_REL14_BRNTAKEN:
                if (delta > 0x7FFF || delta < -0x8000) throw new InvalidOperationException($"{patchName}: 14-bit branch out of range");
                instruction = (instruction & ~0x0000FFFCu) | ((uint)delta & 0x0000FFFCu);
                break;
            default:
                throw new InvalidOperationException($"{patchName}: unsupported local relocation {type}");
        }
        WriteU32(data, offset, instruction);
    }

    private static void WriteU32(byte[] data, int offset, uint value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);

    private static void WriteU16(byte[] data, int offset, ushort value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, 2), value);

    /// <summary>main.dol address of a custom symbol (from earlier patches) or a vanilla one (ww_linker.ld).</summary>
    private bool TryResolveMainSymbol(string name, out uint address)
    {
        if (_customSymbols.TryGetValue("sys/main.dol", out var custom) && custom.TryGetValue(name, out address))
            return true;
        _linkerSymbols ??= LinkerSymbolRegex().Matches(_linkerScript)
            .GroupBy(m => m.Groups[1].Value)
            .ToDictionary(g => g.Key, g => Convert.ToUInt32(g.First().Groups[2].Value, 16));
        return _linkerSymbols.TryGetValue(name, out address);
    }

    private string BuildLinkerScript(string filePath, string localBranchesLinker)
    {
        var sb = new StringBuilder(_linkerScript);
        sb.AppendLine();

        foreach (var (symName, symAddr) in _customSymbols[filePath])
            sb.AppendLine($"{symName} = 0x{symAddr:X8};");
        sb.Append(localBranchesLinker);

        if (filePath != "sys/main.dol" && _customSymbols.ContainsKey("sys/main.dol"))
        {
            foreach (var (symName, symAddr) in _customSymbols["sys/main.dol"])
                sb.AppendLine($"{symName} = 0x{symAddr:X8};");
        }

        return sb.ToString();
    }

    private async Task<string> ParseIncludesAsync(string asm, string baseDir, CancellationToken ct)
    {
        var result = new StringBuilder();
        foreach (var line in asm.Split('\n'))
        {
            var includeMatch = IncludeRegex().Match(line);
            if (includeMatch.Success)
            {
                var relativePath = includeMatch.Groups[1].Value;
                var filePath = Path.Combine(baseDir, relativePath);
                var ext = Path.GetExtension(filePath);

                if (ext == ".asm")
                {
                    var content = await File.ReadAllTextAsync(filePath, ct);
                    result.AppendLine(await ParseIncludesAsync(content, Path.GetDirectoryName(filePath)!, ct));
                }
                else if (ext == ".c")
                {
                    var compiledAsm = await CompileCToAsmAsync(filePath, ct);
                    result.AppendLine(compiledAsm);
                }
                else
                {
                    throw new InvalidOperationException($"Included file with unknown extension: {relativePath}");
                }

                result.AppendLine(".section \".text\"");
            }
            else
            {
                result.AppendLine(line);
            }
        }
        return result.ToString();
    }

    private async Task<string> CompileCToAsmAsync(string cSrcPath, CancellationToken ct)
    {
        var basename = Path.GetFileNameWithoutExtension(cSrcPath);
        var asmPath = Path.Combine(_tempDir, basename + ".asm");

        var result = await _toolchain.RunToolAsync("powerpc-eabi-gcc",
        [
            "-mcpu=750", "-fno-inline", "-Wall", "-Og", "-fshort-enums", "-fno-jump-tables",
            "-I", _config.IncludePath,
            "-S", "-fno-asynchronous-unwind-tables",
            "-c", cSrcPath, "-o", asmPath,
        ], ct: ct);

        result.EnsureSuccess($"Compile {cSrcPath} to ASM");
        return await File.ReadAllTextAsync(asmPath, ct);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { /* ignore cleanup failures */ }
    }

    public Dictionary<string, Dictionary<string, uint>> CustomSymbols => _customSymbols;
}
