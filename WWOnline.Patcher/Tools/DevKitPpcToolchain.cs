using System.Diagnostics;
using WWOnline.Patcher.Config;

namespace WWOnline.Patcher.Tools;

/// <summary>
/// Process.Start wrapper for devkitPPC tools (gcc, ld, as, objcopy, objdump).
/// </summary>
public class DevKitPpcToolchain
{
    private readonly PatcherConfig _config;

    public DevKitPpcToolchain(PatcherConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Run a devkitPPC tool with the given arguments.
    /// </summary>
    public async Task<ToolResult> RunToolAsync(string toolName, IEnumerable<string> arguments,
        string? workingDirectory = null, CancellationToken ct = default)
    {
        var toolPath = _config.GetToolPath(toolName);
        if (!File.Exists(toolPath))
            throw new FileNotFoundException($"devkitPPC tool not found: {toolPath}");

        var psi = new ProcessStartInfo
        {
            FileName = toolPath,
            WorkingDirectory = workingDirectory ?? _config.GameModPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout, not user cancellation
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Tool '{toolName}' timed out after 120 seconds");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ToolResult(process.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Compile a C source file to an object file.
    /// </summary>
    public async Task<ToolResult> CompileAsync(string sourcePath, string outputPath,
        string[]? includeDirectories = null, string[]? extraFlags = null,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "-mcpu=750",
            "-std=c99",
            "-fno-inline",
            "-Wall",
            "-Og",
            "-g",
            "-fshort-enums",
            "-fno-jump-tables",
        };

        if (includeDirectories != null)
        {
            foreach (var dir in includeDirectories)
            {
                args.Add("-I");
                args.Add(dir);
            }
        }

        if (extraFlags != null)
            args.AddRange(extraFlags);

        args.AddRange(["-c", sourcePath, "-o", outputPath]);

        return await RunToolAsync("powerpc-eabi-gcc", args, ct: ct);
    }

    /// <summary>
    /// Link object files using a linker script.
    /// </summary>
    public async Task<ToolResult> LinkRelocatableAsync(string linkerScript, string inputPath,
        string outputPath, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "--relocatable",
            "-T", linkerScript,
            "-o", outputPath,
            inputPath,
        };

        return await RunToolAsync("powerpc-eabi-ld", args, ct: ct);
    }

    /// <summary>
    /// Link to binary output with optional section start addresses and map file.
    /// </summary>
    public async Task<ToolResult> LinkToBinaryAsync(string linkerScript, string inputPath,
        string outputPath, string? mapPath = null,
        Dictionary<string, uint>? sectionStartAddresses = null,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "-T", linkerScript,
        };

        if (mapPath != null)
            args.Add($"-Map={mapPath}");

        args.AddRange([inputPath, "-o", outputPath, "--oformat", "binary"]);

        if (sectionStartAddresses != null)
        {
            foreach (var (name, offset) in sectionStartAddresses)
                args.Add($"--section-start={name}={offset:X}");
        }

        return await RunToolAsync("powerpc-eabi-ld", args, ct: ct);
    }

    /// <summary>
    /// Link an ELF with a linker script (for hook generation).
    /// </summary>
    public async Task<ToolResult> LinkElfAsync(string linkerScript, string inputPath,
        string outputPath, bool noStdLib = false, CancellationToken ct = default)
    {
        var args = new List<string> { "-T", linkerScript };

        if (noStdLib)
            args.Add("-nostdlib");

        args.AddRange([inputPath, "-o", outputPath]);

        return await RunToolAsync("powerpc-eabi-ld", args, ct: ct);
    }

    /// <summary>
    /// Assemble an ASM file to an object file.
    /// </summary>
    public async Task<ToolResult> AssembleAsync(string inputPath, string outputPath,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "-mregnames",
            "-m750cl",
            inputPath,
            "-o", outputPath,
        };

        return await RunToolAsync("powerpc-eabi-as", args, ct: ct);
    }

    /// <summary>
    /// Extract binary code from an ELF section.
    /// </summary>
    public async Task<ToolResult> ObjCopyExtractSectionAsync(string inputPath, string outputPath,
        string sectionName, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "-O", "binary",
            "-j", sectionName,
            inputPath,
            outputPath,
        };

        return await RunToolAsync("powerpc-eabi-objcopy", args, ct: ct);
    }

    /// <summary>
    /// Disassemble a file (for debugging).
    /// </summary>
    public async Task<ToolResult> ObjDumpAsync(string inputPath, bool isBinary = false,
        bool showRelocations = false, CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "-m", "powerpc",
            "-D",
            "-EB",
            "--disassemble-zeroes",
        };

        if (showRelocations) args.Add("--reloc");
        if (isBinary) args.AddRange(["-b", "binary"]);

        args.Add(inputPath);

        return await RunToolAsync("powerpc-eabi-objdump", args, ct: ct);
    }
}
