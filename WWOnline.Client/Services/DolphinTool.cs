using System.Diagnostics;
using System.Text;
using Serilog;

namespace WWOnline.Services;

/// <summary>What a finished process printed and returned.</summary>
public sealed record ProcessResult(int ExitCode, string Output);

/// <summary>Runs a program and collects its output (a fake in tests).</summary>
public interface IProcessRunner
{
    /// <param name="exe">The program.</param>
    /// <param name="arguments">Its arguments, one per entry (no quoting needed).</param>
    /// <param name="onOutputLine">Called with each stdout/stderr line as it arrives (any thread).</param>
    /// <param name="ct">Kills the process when cancelled.</param>
    Task<ProcessResult> RunAsync(string exe, IReadOnlyList<string> arguments, Action<string>? onOutputLine, CancellationToken ct);
}

/// <summary>Runs real processes, hidden, with stdout and stderr captured.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string exe, IReadOnlyList<string> arguments, Action<string>? onOutputLine,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        var output = new StringBuilder();
        void OnLine(object? sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (output) output.AppendLine(e.Data);
            onOutputLine?.Invoke(e.Data);
        }

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += OnLine;
        process.ErrorDataReceived += OnLine;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000); // let it release its files (a cancelled extraction is cleaned up next)
            }
            catch (InvalidOperationException) { /* already gone */ }
            throw;
        }
        process.WaitForExit(); // flush the async output readers
        lock (output) return new ProcessResult(process.ExitCode, output.ToString());
    }
}

/// <summary>
/// Dolphin's command-line tool (DolphinTool.exe, next to Dolphin.exe in current builds), used by the
/// setup to extract the player's disc image (ISO, RVZ, GCM…) into the folder WW-Online patches from.
/// Its syntax, from <c>DolphinTool extract --help</c>: <c>extract -i FILE -o FOLDER</c> (plus
/// -p/-s/-l/-q/-g). A GameCube image extracts to FOLDER\sys and FOLDER\files.
///
/// Whether a build can extract is checked from that help text, not assumed from the version.
/// </summary>
public sealed class DolphinTool
{
    private static readonly ILogger Logger = Log.ForContext<DolphinTool>();

    public const string ExeName = "DolphinTool.exe";

    /// <summary>Disc image types DolphinTool reads that a GameCube game comes in.</summary>
    public static readonly string[] DiscImageExtensions = [".iso", ".gcm", ".rvz", ".ciso", ".gcz", ".wia", ".tgc"];

    private readonly IProcessRunner _runner;

    public DolphinTool(IProcessRunner runner) => _runner = runner;

    /// <summary>How long <c>extract --help</c> may take before the tool counts as unusable.</summary>
    public TimeSpan HelpTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>DolphinTool.exe in the same folder as <paramref name="dolphinExe"/>, or null.</summary>
    public static string? FindNextTo(string? dolphinExe)
    {
        if (string.IsNullOrWhiteSpace(dolphinExe)) return null;
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(dolphinExe.Trim()));
            if (dir == null) return null;
            var tool = Path.Combine(dir, ExeName);
            return File.Exists(tool) ? tool : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> HelpArguments { get; } = ["extract", "--help"];

    /// <summary><c>extract -i IMAGE -o FOLDER</c>.</summary>
    public static IReadOnlyList<string> ExtractArguments(string discImage, string outputFolder) =>
        ["extract", "-i", discImage, "-o", outputFolder];

    /// <summary>Does this help text describe an <c>extract</c> command with the -i/-o options we pass?</summary>
    public static bool HelpShowsExtract(ProcessResult help) =>
        help.ExitCode == 0 &&
        help.Output.Contains("--input", StringComparison.Ordinal) &&
        help.Output.Contains("--output", StringComparison.Ordinal) &&
        help.Output.Contains("-i FILE", StringComparison.Ordinal) &&
        help.Output.Contains("-o FOLDER", StringComparison.Ordinal);

    public static bool IsDiscImage(string? path) =>
        !string.IsNullOrWhiteSpace(path) && DiscImageExtensions.Contains(Path.GetExtension(path.Trim()).ToLowerInvariant());

    /// <summary>Where to extract <paramref name="discImage"/> by default: a folder named after it, beside it.</summary>
    public static string? SuggestExtractFolder(string? discImage)
    {
        if (string.IsNullOrWhiteSpace(discImage)) return null;
        try
        {
            var full = Path.GetFullPath(discImage.Trim());
            var dir = Path.GetDirectoryName(full);
            var name = Path.GetFileNameWithoutExtension(full);
            if (dir == null || name.Length == 0) return null;
            // "Game.nkit.iso" → "Game"
            if (name.EndsWith(".nkit", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
            return Path.Combine(dir, name);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Can the DolphinTool at <paramref name="toolPath"/> extract discs? (Runs its extract help.)</summary>
    /// <remarks>Gives up (false) after <see cref="HelpTimeout"/>, so a hung DolphinTool can't stall the setup.</remarks>
    public async Task<bool> SupportsExtractAsync(string toolPath, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(HelpTimeout);
        try
        {
            var help = await _runner.RunAsync(toolPath, HelpArguments, null, timeout.Token).ConfigureAwait(false);
            var ok = HelpShowsExtract(help);
            Logger.Information("[setup] {Tool}: extract {Supported}", toolPath, ok ? "supported" : "not supported");
            return ok;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Logger.Warning("[setup] {Tool} didn't answer within {Timeout}s; not using it", toolPath, HelpTimeout.TotalSeconds);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Warning(ex, "[setup] couldn't run {Tool}", toolPath);
            return false;
        }
    }

    /// <summary>Why <paramref name="discImage"/> can't be extracted into <paramref name="outputFolder"/> (null = go ahead).</summary>
    public static string? CheckExtractInputs(string? discImage, string? outputFolder)
    {
        if (string.IsNullOrWhiteSpace(discImage))
            return "Choose your disc image (ISO, RVZ, GCM…).";
        if (!File.Exists(discImage.Trim()))
            return $"File not found: {discImage.Trim()}";
        if (!IsDiscImage(discImage))
            return "That isn't a disc image Dolphin can read. Choose the .iso, .rvz, .gcm, .ciso, .gcz or .wia file.";
        if (string.IsNullOrWhiteSpace(outputFolder))
            return "Choose a folder to extract into.";
        var folder = outputFolder.Trim();
        if (File.Exists(folder))
            return "That's a file. Choose a folder to extract into.";
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any() &&
            !File.Exists(Path.Combine(folder, "sys", "main.dol")))
            return "That folder isn't empty. Choose an empty folder (or a new one) to extract into.";
        return null;
    }

    /// <summary>
    /// Extract <paramref name="discImage"/> into <paramref name="outputFolder"/>. Each line DolphinTool
    /// prints goes to <paramref name="progress"/>.
    /// </summary>
    /// <returns>An error, or null on success.</returns>
    public async Task<string?> ExtractAsync(string toolPath, string discImage, string outputFolder, IProgress<string>? progress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);
        Logger.Information("[setup] extracting {Image} into {Folder}", discImage, outputFolder);
        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(toolPath, ExtractArguments(discImage, outputFolder),
                line => { if (!string.IsNullOrWhiteSpace(line)) progress?.Report(line.Trim()); }, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Logger.Error(ex, "[setup] DolphinTool failed to run");
            return $"Couldn't run DolphinTool: {ex.Message}";
        }

        if (result.ExitCode != 0)
        {
            var tail = string.Join(" ", result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(3));
            Logger.Warning("[setup] DolphinTool extract exited with {Code}: {Output}", result.ExitCode, tail);
            return $"DolphinTool couldn't extract the disc (exit code {result.ExitCode}). {tail}".Trim();
        }
        Logger.Information("[setup] extracted {Image}", discImage);
        return null;
    }
}
