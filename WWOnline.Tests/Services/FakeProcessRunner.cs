using WWOnline.Services;

namespace WWOnline.Tests.Services;

/// <summary>Records the programs it's asked to run and answers with <see cref="Handler"/>; never starts a process.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    /// <summary><c>DolphinTool extract --help</c> as printed by a current Dolphin build.</summary>
    public static readonly ProcessResult ExtractHelp = new(0, """
        Usage: extract [options]...

        Options:
          -h, --help            show this help message and exit
          -i FILE, --input=FILE
                                Path to disc image FILE.
          -o FOLDER, --output=FOLDER
                                Path to the destination FOLDER.
          -p PARTITION, --partition=PARTITION
                                Which specific partition you want to extract.
          -s SINGLE, --single=SINGLE
                                Which specific file/directory you want to extract.
          -l, --list            List all files in volume/partition. Will print the
                                directory/file specified with --single if defined.
          -q, --quiet           Mute all messages except for errors.
          -g, --gameonly        Only extracts the DATA partition.
        """);

    public List<(string Exe, List<string> Args)> Calls { get; } = [];

    /// <summary>(exe, arguments) → result. Answers the extract help by default.</summary>
    public Func<string, List<string>, ProcessResult> Handler { get; set; } = (_, _) => ExtractHelp;

    public Task<ProcessResult> RunAsync(string exe, IReadOnlyList<string> arguments, Action<string>? onOutputLine, CancellationToken ct)
    {
        var args = arguments.ToList();
        Calls.Add((exe, args));
        var result = Handler(exe, args);
        foreach (var line in result.Output.Split('\n')) onOutputLine?.Invoke(line.TrimEnd('\r'));
        return Task.FromResult(result);
    }
}
