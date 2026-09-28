using System.Text;
using WWOnline.Patcher.Config;

namespace WWOnline.Patcher.Tools;

/// <summary>
/// Compiles link_draw_hook.c, extracts binary, and generates .asm patch file.
/// Ports generate_hook_asm.py.
/// </summary>
public class HookAsmGenerator
{
    private readonly DevKitPpcToolchain _toolchain;
    private readonly PatcherConfig _config;

    public const uint HookAddress = 0x80108204;   // daPy_Draw function
    public const uint CodeAddress = 0x80286000;    // Injection location in Text1

    /// <summary>
    /// 0x80286000 is NOT free space: it overwrites JASystem TBasicWaveBank code (dead in WW — all
    /// WSYS banks use TSimpleWaveBank). The live TSimpleWaveBank::__ct starts at 0x802864C8, so the
    /// hook must stay below that or it corrupts audio code.
    /// </summary>
    public const uint CodeLimit = 0x802864C8;
    public const int MaxCodeSize = (int)(CodeLimit - CodeAddress);

    public HookAsmGenerator(DevKitPpcToolchain toolchain, PatcherConfig config)
    {
        _toolchain = toolchain;
        _config = config;
    }

    /// <summary>
    /// Generate a linker script that places code at the target address.
    /// </summary>
    private string CreateLinkerScript()
    {
        var wwLinkerContent = File.ReadAllText(_config.LinkerScriptPath);

        return
            $"/* Wind Waker function addresses */\n" +
            $"{wwLinkerContent}\n" +
            "\n" +
            "/* Entry point - keep our hook function */\n" +
            "ENTRY(link_draw_hook)\n" +
            "\n" +
            "/* Place our code at the target address */\n" +
            "SECTIONS\n" +
            "{\n" +
            $"    . = 0x{CodeAddress:X8};\n" +
            "    .text : {\n" +
            "        KEEP(*(.text.link_draw_hook))\n" +
            "        *(.text*)\n" +
            "    }\n" +
            "    .rodata : { *(.rodata*) }\n" +
            "    .data : { *(.data*) }\n" +
            "    .bss : { *(.bss*) }\n" +
            "}\n";
    }

    /// <summary>
    /// Compile link_draw_hook.c and return the binary code bytes.
    /// </summary>
    public async Task<byte[]> CompileHookAsync(CancellationToken ct = default)
    {
        var sourceFile = Path.Combine(_config.PuppetLinkSrcPath, "link_draw_hook.c");
        var tempDir = Path.Combine(Path.GetTempPath(), $"wwo_hook_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var objFile = Path.Combine(tempDir, "link_draw_hook_temp.o");
        var linkedFile = Path.Combine(tempDir, "link_draw_hook_linked.elf");
        var binFile = Path.Combine(tempDir, "link_draw_hook_temp.bin");
        var linkerScriptFile = Path.Combine(tempDir, "link_draw_hook_linker.ld");

        try
        {
            // Step 1: Create linker script
            var linkerScript = CreateLinkerScript();
            await File.WriteAllTextAsync(linkerScriptFile, linkerScript, ct);

            // Step 2: Compile to object file
            var compileResult = await _toolchain.CompileAsync(
                sourceFile, objFile,
                includeDirectories: [_config.IncludePath],
                extraFlags: ["-DGAMECUBE"],
                ct: ct);
            compileResult.EnsureSuccess("Compile link_draw_hook.c");

            // Step 3: Link at target address
            var linkResult = await _toolchain.LinkElfAsync(
                linkerScriptFile, objFile, linkedFile, noStdLib: true, ct: ct);
            linkResult.EnsureSuccess("Link link_draw_hook");

            // Step 4: Extract binary code from .text section
            var extractResult = await _toolchain.ObjCopyExtractSectionAsync(
                linkedFile, binFile, ".text", ct: ct);
            extractResult.EnsureSuccess("Extract .text section");

            var code = await File.ReadAllBytesAsync(binFile, ct);
            if (code.Length == 0)
                throw new InvalidOperationException("No code extracted from link_draw_hook");
            if (code.Length > MaxCodeSize)
                throw new InvalidOperationException(
                    $"link_draw_hook is {code.Length} bytes (0x{code.Length:X}) but only 0x{MaxCodeSize:X} fit before " +
                    $"live game code at 0x{CodeLimit:X8}. Move logic into the puppet REL instead of the hook.");

            return code;
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { /* cleanup best-effort */ }
        }
    }

    /// <summary>
    /// Generate link_draw_hook.asm with the compiled code as .byte directives.
    /// </summary>
    public static string GenerateAsm(byte[] code)
    {
        // Calculate branch instruction from hook point to our code
        var branchOffset = CodeAddress - HookAddress;
        // PowerPC branch: 0x48000000 | (offset & 0x03FFFFFC)
        var branchInstruction = 0x48000000u | (branchOffset & 0x03FFFFFC);

        var sb = new StringBuilder();
        sb.AppendLine($"; WW-Online - Link Draw Hook");
        sb.AppendLine($"; Auto-generated from link_draw_hook.c by HookAsmGenerator");
        sb.AppendLine($"; DO NOT EDIT MANUALLY - changes will be overwritten!");
        sb.AppendLine($";");
        sb.AppendLine($"; This patch hooks into daPy_Draw to spawn and manage the puppet player");
        sb.AppendLine($";");
        sb.AppendLine($"; Hook point: 0x{HookAddress:X8} (daPy_Draw)");
        sb.AppendLine($"; Code location: 0x{CodeAddress:X8} (Text1 section, 8KB available)");
        sb.AppendLine($"; Code size: {code.Length} bytes");
        sb.AppendLine();
        sb.AppendLine(".open \"sys/main.dol\"");
        sb.AppendLine();
        sb.AppendLine($"; Hook the daPy_Draw function at 0x{HookAddress:X8}");
        sb.AppendLine($"; Replace the first instruction with a branch to our custom code");
        sb.AppendLine($".org 0x{HookAddress:X8}");
        sb.AppendLine($"  .int 0x{branchInstruction:X8}  ; b 0x{CodeAddress:X8}");
        sb.AppendLine();
        sb.AppendLine($"; Place our compiled C code at 0x{CodeAddress:X8}");
        sb.AppendLine($".org 0x{CodeAddress:X8}");

        // Add compiled code as .byte directives (16 bytes per line)
        const int bytesPerLine = 16;
        for (var i = 0; i < code.Length; i += bytesPerLine)
        {
            var chunk = code.AsSpan(i, Math.Min(bytesPerLine, code.Length - i));
            var byteStr = string.Join(", ", chunk.ToArray().Select(b => $"0x{b:X2}"));
            sb.AppendLine($"  .byte {byteStr}");
        }

        sb.AppendLine();
        sb.AppendLine(".close");

        return sb.ToString();
    }

    /// <summary>
    /// Full pipeline: compile hook C code and generate the .asm file.
    /// </summary>
    public async Task<string> GenerateHookAsmFileAsync(CancellationToken ct = default)
    {
        var code = await CompileHookAsync(ct);
        var asmContent = GenerateAsm(code);

        var asmPath = _config.GeneratedHookAsmPath;
        Directory.CreateDirectory(Path.GetDirectoryName(asmPath)!);
        await File.WriteAllTextAsync(asmPath, asmContent, ct);

        return asmPath;
    }
}
