using System.Security.Cryptography;
using System.Text;
using WWOnline.Patcher.Pipeline;
using WWOnline.Services;

namespace WWOnline.Native;

/// <summary>
/// Builds libtww on this Mac from Seaborne's source (the repository's seaborne/, which the macOS app ships:
/// <see cref="NativePaths.BundledSource"/>) the way its README does (native/tools/fetch_recompcore.sh, cmake,
/// ninja tww), as <see cref="IPipelineStep"/>s run by <see cref="PipelineRunner.RunStepsAsync"/>. No disc is read:
/// the source holds the runtime-asset stubs, and the game reads its data from the player's disc when it starts.
/// Everything it makes goes in the game cache (<see cref="AppPaths.GameCacheDirectory"/>), never into the source.
/// Each step runs in a login shell, so Homebrew's tools are found from an app started by Finder too.
/// </summary>
public sealed class NativeBuild
{
    public const string Shell = "/bin/zsh";
    public const string NotSupported = "Building the game is only supported on macOS for now.";

    // Every script gets: $1 the source, $2 the RecompCore checkout, $3 the build folder.
    private const string PathSetup = "export PATH=\"/opt/homebrew/bin:/usr/local/bin:$PATH\"; cd \"$1\" || exit 1; ";

    private const string ToolCheck = PathSetup +
        "xcode-select -p >/dev/null 2>&1 || { echo \"Xcode's command line tools are missing: run xcode-select --install\"; exit 1; }; " +
        "for t in clang cmake ninja git; do command -v $t >/dev/null 2>&1 || " +
        "{ echo \"$t is missing (Homebrew: brew install cmake ninja)\"; exit 1; }; done; " +
        // The engine needs C++20's std::jthread, which older Xcode versions' libc++ lacks.
        "printf '#include <thread>\\nint main() { std::jthread t([] {}); }\\n' | clang++ -std=c++20 -x c++ - -o /dev/null 2>/dev/null || " +
        "{ echo \"Your Xcode is too old to build the game (it needs C++20's std::jthread): update Xcode or its command line tools\"; exit 1; }; " +
        "echo \"Xcode command line tools, cmake, ninja and git found\"";

    // A checkout at another pin (left by an older Seaborne) or a broken one is fetched again.
    private const string FetchRecompCore = PathSetup +
        "native/tools/fetch_recompcore.sh \"$2\" || { rm -rf \"$2\"; native/tools/fetch_recompcore.sh \"$2\"; }";

    private readonly IProcessRunner _runner;
    private readonly string _cache;

    /// <param name="cacheDirectory">Where the build goes (null = <see cref="AppPaths.GameCacheDirectory"/>).</param>
    public NativeBuild(IProcessRunner runner, string? cacheDirectory = null)
    {
        _runner = runner;
        _cache = cacheDirectory ?? AppPaths.GameCacheDirectory;
    }

    /// <summary>The DSP HLE sources the audio is compiled from (fetched by the second step, GPL, source only).</summary>
    public string RecompCoreDirectory => Path.Combine(_cache, "recompcore");

    /// <summary>One build folder per source folder: CMake refuses a build folder configured from another source.</summary>
    public string BuildDirectory(string source) => Path.Combine(_cache, "build",
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(source))))[..12]);

    public string LibraryPath(string source) => Path.Combine(BuildDirectory(source), "libtww.dylib");

    /// <summary>Why <paramref name="source"/> isn't Seaborne's source, or null.</summary>
    public static string? CheckSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
            return "Choose the folder of Seaborne's source.";
        string[] needed = ["native/CMakeLists.txt", "native/include/pc/tww.h", "native/tools/fetch_recompcore.sh"];
        return needed.All(f => File.Exists(Path.Combine(source, f)))
            ? null
            : "That folder isn't Seaborne's source (no native/CMakeLists.txt, native/include/pc/tww.h).";
    }

    /// <summary>What is missing to build on this machine (null: nothing), and what was found.</summary>
    public async Task<(string? Error, string Found)> CheckToolchainAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            return (NotSupported, "");
        var result = await RunShellAsync(ToolCheck, Path.GetTempPath(), null, ct);
        string last = LastLine(result.Output) ?? "";
        return result.ExitCode == 0 ? (null, last) : (last.Length > 0 ? last : $"The tool check failed (exit code {result.ExitCode}).", "");
    }

    /// <summary>The steps, in order (<see cref="BuildAsync"/> runs them).</summary>
    public IReadOnlyList<IPipelineStep> Steps(string source) =>
    [
        new ShellStep(this, 1, "Check the build tools", ToolCheck, source),
        new ShellStep(this, 2, "Fetch the audio sources", FetchRecompCore, source),
        new ShellStep(this, 3, "Configure", PathSetup +
            "cmake -S native -B \"$3\" -G Ninja -DCMAKE_BUILD_TYPE=RelWithDebInfo -DCOS_RECOMPCORE_DIR=\"$2\"", source),
        new ShellStep(this, 4, "Build the game", PathSetup + "ninja -C \"$3\" tww", source),
    ];

    /// <summary>Build libtww; returns its path. Throws with the failing step's last line on failure.</summary>
    public async Task<string> BuildAsync(string source, PipelineProgress progress, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException(NotSupported);
        await PipelineRunner.RunStepsAsync(Steps(source), progress, ct);
        var library = LibraryPath(source);
        if (!File.Exists(library))
            throw new InvalidOperationException($"The build finished without {library}.");
        return library;
    }

    private Task<ProcessResult> RunShellAsync(string script, string source, Action<string>? onLine, CancellationToken ct) =>
        _runner.RunAsync(Shell, ["-lc", script, "zsh", source, RecompCoreDirectory, BuildDirectory(source)], onLine, ct);

    private static string? LastLine(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();

    private sealed class ShellStep(NativeBuild build, int number, string name, string script, string source) : IPipelineStep
    {
        public string Name => name;
        public int StepNumber => number;

        public async Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
        {
            var result = await build.RunShellAsync(script, source, line => progress.Report(number, name, line), ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(LastLine(result.Output) ?? $"exit code {result.ExitCode}");
        }
    }
}
