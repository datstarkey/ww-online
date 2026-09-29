using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

public sealed class DolphinToolTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-dtool-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public DolphinToolTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void FindsDolphinTool_NextToDolphin_Only()
    {
        var dolphin = Path.Combine(_dir, "Dolphin.exe");
        File.WriteAllBytes(dolphin, [0]);
        Assert.Null(DolphinTool.FindNextTo(dolphin));
        Assert.Null(DolphinTool.FindNextTo(""));

        File.WriteAllBytes(Path.Combine(_dir, DolphinTool.ExeName), [0]);
        Assert.Equal(Path.Combine(_dir, DolphinTool.ExeName), DolphinTool.FindNextTo(dolphin));
    }

    [Fact]
    public async Task ExtractSupport_ComesFromTheHelpText()
    {
        var tool = new DolphinTool(_runner);
        Assert.True(await tool.SupportsExtractAsync("DolphinTool.exe"));
        Assert.Equal(["extract", "--help"], Assert.Single(_runner.Calls).Args);

        // An older build without "extract": it prints its usage and fails.
        _runner.Handler = (_, _) => new ProcessResult(1, "usage: dolphin-tool COMMAND -h\ncommands supported: [convert, verify, header]");
        Assert.False(await tool.SupportsExtractAsync("DolphinTool.exe"));

        // Help that exits 0 but doesn't take -i/-o the way we pass them.
        _runner.Handler = (_, _) => new ProcessResult(0, "Usage: extract <image> <folder>");
        Assert.False(await tool.SupportsExtractAsync("DolphinTool.exe"));

        // Can't run at all.
        _runner.Handler = (_, _) => throw new System.ComponentModel.Win32Exception("blocked");
        Assert.False(await tool.SupportsExtractAsync("DolphinTool.exe"));
    }

    [Fact]
    public async Task AHungDolphinTool_TimesOut_AsNotSupported()
    {
        var tool = new DolphinTool(new HangingRunner()) { HelpTimeout = TimeSpan.FromMilliseconds(50) };
        var supported = await tool.SupportsExtractAsync("DolphinTool.exe").WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(supported);

        // The caller's own cancellation still surfaces as cancellation.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.SupportsExtractAsync("DolphinTool.exe", cts.Token));
    }

    /// <summary>A process that never answers (until cancelled).</summary>
    private sealed class HangingRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(string exe, IReadOnlyList<string> arguments, Action<string>? onOutputLine, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new ProcessResult(0, "");
        }
    }

    [Fact]
    public void ExtractArguments_AreInputThenOutput_OneArgumentEach()
    {
        Assert.Equal(["extract", "-i", @"C:\Games\The Wind Waker (USA).rvz", "-o", @"C:\Games\The Wind Waker (USA)"],
            DolphinTool.ExtractArguments(@"C:\Games\The Wind Waker (USA).rvz", @"C:\Games\The Wind Waker (USA)"));
    }

    [Fact]
    public void SuggestsAFolderBesideTheImage()
    {
        Assert.Equal(Path.Combine(_dir, "The Wind Waker (USA)"), DolphinTool.SuggestExtractFolder(Path.Combine(_dir, "The Wind Waker (USA).iso")));
        Assert.Equal(Path.Combine(_dir, "ww"), DolphinTool.SuggestExtractFolder(Path.Combine(_dir, "ww.nkit.iso")));
        Assert.Null(DolphinTool.SuggestExtractFolder(""));
    }

    [Fact]
    public void ExtractInputs_AreChecked()
    {
        var iso = Path.Combine(_dir, "ww.rvz");
        File.WriteAllBytes(iso, [0]);
        var txt = Path.Combine(_dir, "ww.txt");
        File.WriteAllBytes(txt, [0]);

        Assert.NotNull(DolphinTool.CheckExtractInputs("", Path.Combine(_dir, "out")));
        Assert.NotNull(DolphinTool.CheckExtractInputs(Path.Combine(_dir, "missing.iso"), Path.Combine(_dir, "out")));
        Assert.NotNull(DolphinTool.CheckExtractInputs(txt, Path.Combine(_dir, "out")));
        Assert.NotNull(DolphinTool.CheckExtractInputs(iso, ""));
        Assert.Null(DolphinTool.CheckExtractInputs(iso, Path.Combine(_dir, "out")));          // new folder
        Assert.NotNull(DolphinTool.CheckExtractInputs(iso, _dir));                           // not empty
        Assert.Null(DolphinTool.CheckExtractInputs(iso, FakeGameFolder.Create(Path.Combine(_dir, "done")))); // already extracted
    }

    [Fact]
    public async Task Extract_ReportsEachLine_AndFailuresWithTheToolsMessage()
    {
        var tool = new DolphinTool(_runner);
        var lines = new List<string>();
        _runner.Handler = (_, _) => new ProcessResult(0, "Extracting sys/main.dol\nExtracting files/RELS.arc\n");
        var output = Path.Combine(_dir, "out");

        Assert.Null(await tool.ExtractAsync("DolphinTool.exe", "ww.iso", output, new SyncProgress(lines.Add)));
        Assert.Equal(["Extracting sys/main.dol", "Extracting files/RELS.arc"], lines);
        Assert.True(Directory.Exists(output));

        _runner.Handler = (_, _) => new ProcessResult(1, "Error: Unable to open disc image\n");
        var error = await tool.ExtractAsync("DolphinTool.exe", "ww.iso", output, null);
        Assert.Contains("exit code 1", error);
        Assert.Contains("Unable to open disc image", error);
    }

    /// <summary>IProgress that reports inline (Progress&lt;T&gt; posts to the thread pool).</summary>
    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
