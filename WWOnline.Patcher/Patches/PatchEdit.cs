using System.Globalization;
using System.Text.RegularExpressions;

namespace WWOnline.Patcher.Patches;

/// <summary>
/// A non-ASM change an optional patch makes to a game file, declared in the patch header with
/// <c>; @edit &lt;kind&gt; &lt;args&gt;</c>. Every kind is implemented in C# (GameFileEditor):
/// <list type="bullet">
/// <item><c>bmg-instant-text</c> — every message in files/res/Msg/bmgres.arc zel_00.bmg draws
/// instantly and loses its wait / wait-for-prompt control codes (wwrando make_all_text_instant).</item>
/// <item><c>bmg-message &lt;id&gt; &lt;text&gt;</c> — replace one message's text (plain ASCII).</item>
/// <item><c>replace-file &lt;game path&gt; &lt;asset&gt;</c> — replace a whole game file with a file
/// from GameMod/assets/ (shipped as PatchData/assets/).</item>
/// <item><c>dzb-face-property &lt;arc&gt; &lt;dzb entry&gt; &lt;face index&gt; &lt;property index&gt;</c> —
/// set one collision triangle's property index in a stage/room archive's .dzb.</item>
/// </list>
/// </summary>
public abstract record PatchEdit
{
    public const string BmgArchive = "files/res/Msg/bmgres.arc";
    public const string BmgEntry = "zel_00.bmg";

    /// <summary>Game-relative path of the file this edit changes.</summary>
    public abstract string TargetFile { get; }

    /// <summary>The edit as written in the header (for logs and the build stamp).</summary>
    public abstract string Describe();

    public static PatchEdit Parse(string text)
    {
        var parts = Regex.Split(text.Trim(), @"\s+");
        var kind = parts[0].ToLowerInvariant();
        string Rest(int from) => string.Join(' ', parts.Skip(from));

        return kind switch
        {
            "bmg-instant-text" when parts.Length == 1 => new BmgInstantTextEdit(),
            "bmg-message" when parts.Length >= 3 => new BmgMessageTextEdit(ushort.Parse(parts[1], CultureInfo.InvariantCulture), Rest(2)),
            "replace-file" when parts.Length == 3 => new ReplaceFileEdit(NormalisePath(parts[1]), parts[2]),
            "dzb-face-property" when parts.Length == 5 => new DzbFacePropertyEdit(
                NormalisePath(parts[1]), parts[2], (int)ParseNumber(parts[3]), (ushort)ParseNumber(parts[4])),
            _ => throw new FormatException($"Unknown or malformed @edit: \"{text}\""),
        };
    }

    private static uint ParseNumber(string s) => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? uint.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
        : uint.Parse(s, CultureInfo.InvariantCulture);

    private static string NormalisePath(string path)
    {
        var p = path.Replace('\\', '/').TrimStart('/');
        if (p.Contains("..") || !p.StartsWith("files/", StringComparison.Ordinal))
            throw new FormatException($"@edit paths must be game-relative under files/: \"{path}\"");
        return p;
    }
}

public sealed record BmgInstantTextEdit : PatchEdit
{
    public override string TargetFile => BmgArchive;
    public override string Describe() => "bmg-instant-text";
}

public sealed record BmgMessageTextEdit(ushort MessageId, string Text) : PatchEdit
{
    public override string TargetFile => BmgArchive;
    public override string Describe() => $"bmg-message {MessageId} {Text}";
}

public sealed record ReplaceFileEdit(string GamePath, string AssetName) : PatchEdit
{
    public override string TargetFile => GamePath;
    public override string Describe() => $"replace-file {GamePath} {AssetName}";
}

public sealed record DzbFacePropertyEdit(string ArchivePath, string EntryName, int FaceIndex, ushort PropertyIndex) : PatchEdit
{
    public override string TargetFile => ArchivePath;
    public override string Describe() => $"dzb-face-property {ArchivePath} {EntryName} 0x{FaceIndex:X} 0x{PropertyIndex:X}";
}
