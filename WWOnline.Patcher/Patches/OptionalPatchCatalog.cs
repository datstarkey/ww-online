using System.Globalization;
using System.Text.RegularExpressions;

namespace WWOnline.Patcher.Patches;

/// <summary>A main.dol / REL location an optional patch writes, as declared by its .org line.</summary>
/// <param name="File">Game-relative file from the enclosing .open ("sys/main.dol", "files/rels/x.rel").</param>
/// <param name="Address">The literal .org address/offset, or null for .org @NextFreeSpace / a symbol.</param>
public sealed record PatchOrg(string File, uint? Address);

/// <summary>
/// One player-selectable patch: GameMod/src/patches/optional/&lt;id&gt;.asm. The file's header
/// comment is the catalogue entry (single source of truth), e.g.
/// <code>
/// ; @name        Skip intro movie
/// ; @description Skips the long intro movie when starting the game.
/// ; @category    Startup
/// ; @default     on
/// ; @match       no
/// ; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
/// </code>
/// Optional tags: <c>@multiplayer</c> (note shown to players), <c>@conflicts id, id</c>
/// (mutually exclusive patches), <c>@edit ...</c> (non-ASM file edits, see <see cref="PatchEdit"/>).
/// <c>@description</c>, <c>@multiplayer</c> and <c>@edit</c> may repeat.
/// </summary>
public sealed record OptionalPatch(
    string Id,
    string Name,
    string Description,
    string Category,
    bool DefaultEnabled,
    bool AllPlayersShouldMatch,
    string MultiplayerNote,
    string Credit,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<PatchEdit> Edits,
    IReadOnlyList<string> AsmTargetFiles,
    IReadOnlyList<PatchOrg> Orgs,
    string SourcePath)
{
    /// <summary>True when the .asm has code to assemble (at least one .open), not only @edit lines.</summary>
    public bool HasAsm => AsmTargetFiles.Count > 0;

    /// <summary>Every game file this patch changes (ASM targets + @edit targets).</summary>
    public IEnumerable<string> TouchedFiles => AsmTargetFiles.Concat(Edits.Select(e => e.TargetFile)).Distinct(StringComparer.Ordinal);
}

/// <summary>The resolved set of optional patches for one build.</summary>
/// <param name="Ids">Known, de-duplicated ids in ordinal order.</param>
/// <param name="UnknownIds">Requested ids the catalogue doesn't have (renamed/removed patches, typos).</param>
public sealed record PatchSelection(IReadOnlyList<string> Ids, IReadOnlyList<string> UnknownIds);

/// <summary>Loads and queries GameMod/src/patches/optional/*.asm (or the copy shipped in PatchData/optional/).</summary>
public sealed partial class OptionalPatchCatalog
{
    public const string DirectoryName = "optional";

    private static readonly HashSet<string> KnownTags =
        ["name", "description", "category", "default", "match", "credit", "multiplayer", "conflicts", "edit"];

    [GeneratedRegex(@"^;\s*@([a-z]+)\b\s*(.*)$")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"^[a-z0-9_]+$")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"^\s*\.open\s+""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex OpenRegex();

    [GeneratedRegex(@"^\s*\.org\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex OrgRegex();

    public IReadOnlyList<OptionalPatch> Patches { get; }

    public OptionalPatchCatalog(IEnumerable<OptionalPatch> patches)
    {
        Patches = patches.OrderBy(p => p.Category, StringComparer.Ordinal).ThenBy(p => p.Name, StringComparer.Ordinal).ToList();
        var byId = new Dictionary<string, OptionalPatch>(StringComparer.Ordinal);
        foreach (var p in Patches)
            if (!byId.TryAdd(p.Id, p)) throw new InvalidDataException($"Duplicate optional patch id {p.Id}");
        foreach (var p in Patches)
            foreach (var c in p.Conflicts)
                if (!byId.ContainsKey(c)) throw new InvalidDataException($"{p.Id}: @conflicts names unknown patch \"{c}\"");
        _byId = byId;
    }

    private readonly Dictionary<string, OptionalPatch> _byId;

    public static OptionalPatchCatalog Empty { get; } = new([]);

    /// <summary>Load every *.asm in <paramref name="directory"/>; a missing directory is an empty catalogue.</summary>
    public static OptionalPatchCatalog Load(string directory)
    {
        if (!Directory.Exists(directory)) return Empty;
        return new OptionalPatchCatalog(Directory.GetFiles(directory, "*.asm", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(ParseFile));
    }

    public static OptionalPatch ParseFile(string path) =>
        Parse(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path), path);

    public static OptionalPatch Parse(string id, string text, string sourcePath)
    {
        if (!IdRegex().IsMatch(id))
            throw new InvalidDataException($"{sourcePath}: optional patch file names must be lower_snake_case ids");

        var tags = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var lines = text.Replace("\r", "").Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (!line.StartsWith(';')) break; // the header ends at the first non-comment line
            var m = TagRegex().Match(line);
            if (!m.Success) continue;
            var tag = m.Groups[1].Value;
            if (!KnownTags.Contains(tag))
                throw new InvalidDataException($"{sourcePath}: unknown header tag @{tag}");
            var value = m.Groups[2].Value.Trim();
            if (value.Length == 0)
                throw new InvalidDataException($"{sourcePath}: @{tag} has no value");
            if (!tags.TryGetValue(tag, out var list)) tags[tag] = list = [];
            list.Add(value);
        }

        string Single(string tag, string? fallback = null)
        {
            if (!tags.TryGetValue(tag, out var values))
                return fallback ?? throw new InvalidDataException($"{sourcePath}: missing required header tag @{tag}");
            if (values.Count > 1) throw new InvalidDataException($"{sourcePath}: @{tag} given more than once");
            return values[0];
        }

        bool Flag(string tag, string yes, string no)
        {
            var v = Single(tag).ToLowerInvariant();
            if (v == yes) return true;
            if (v == no) return false;
            throw new InvalidDataException($"{sourcePath}: @{tag} must be \"{yes}\" or \"{no}\", not \"{v}\"");
        }

        var (targets, orgs) = ScanAsm(lines);
        var edits = tags.GetValueOrDefault("edit", []).Select(e =>
        {
            try { return PatchEdit.Parse(e); }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new InvalidDataException($"{sourcePath}: {ex.Message}", ex);
            }
        }).ToList();
        if (targets.Count == 0 && edits.Count == 0)
            throw new InvalidDataException($"{sourcePath}: an optional patch needs ASM (.open) or at least one @edit");

        var conflicts = tags.GetValueOrDefault("conflicts", [])
            .SelectMany(c => c.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal).ToList();
        if (conflicts.Contains(id)) throw new InvalidDataException($"{sourcePath}: a patch can't conflict with itself");

        return new OptionalPatch(
            Id: id,
            Name: Single("name"),
            Description: string.Join(' ', tags.GetValueOrDefault("description") ?? throw new InvalidDataException($"{sourcePath}: missing required header tag @description")),
            Category: Single("category"),
            DefaultEnabled: Flag("default", "on", "off"),
            AllPlayersShouldMatch: Flag("match", "yes", "no"),
            MultiplayerNote: string.Join(' ', tags.GetValueOrDefault("multiplayer", [])),
            Credit: Single("credit"),
            Conflicts: conflicts,
            Edits: edits,
            AsmTargetFiles: targets,
            Orgs: orgs,
            SourcePath: sourcePath);
    }

    /// <summary>The .open targets and .org locations of an .asm (comments stripped).</summary>
    private static (List<string> Targets, List<PatchOrg> Orgs) ScanAsm(IEnumerable<string> lines)
    {
        var targets = new List<string>();
        var orgs = new List<PatchOrg>();
        string? current = null;
        foreach (var raw in lines)
        {
            var line = raw;
            var comment = line.IndexOf(';');
            if (comment >= 0) line = line[..comment];
            line = line.Trim();
            if (line.Length == 0) continue;

            var open = OpenRegex().Match(line);
            if (open.Success)
            {
                current = open.Groups[1].Value;
                if (!targets.Contains(current)) targets.Add(current);
                continue;
            }
            if (line.Equals(".close", StringComparison.OrdinalIgnoreCase)) { current = null; continue; }

            var org = OrgRegex().Match(line);
            if (org.Success && current != null)
            {
                var value = org.Groups[1].Value;
                uint? address = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? uint.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    : null;
                orgs.Add(new PatchOrg(current, address));
            }
        }
        return (targets, orgs);
    }

    public OptionalPatch? Find(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<string> DefaultIds =>
        Patches.Where(p => p.DefaultEnabled).Select(p => p.Id).OrderBy(i => i, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Resolve a requested selection: null means "the catalogue defaults"; unknown ids are
    /// reported separately (callers decide whether that is an error).
    /// </summary>
    public PatchSelection Resolve(IEnumerable<string>? requested)
    {
        if (requested == null) return new PatchSelection(DefaultIds, []);
        var known = new SortedSet<string>(StringComparer.Ordinal);
        var unknown = new List<string>();
        foreach (var raw in requested)
        {
            var id = raw.Trim();
            if (id.Length == 0) continue;
            if (_byId.ContainsKey(id)) known.Add(id);
            else if (!unknown.Contains(id)) unknown.Add(id);
        }
        return new PatchSelection(known.ToList(), unknown);
    }

    /// <summary>Pairs of selected ids that declare each other (either direction) as conflicting.</summary>
    public IReadOnlyList<(string A, string B)> FindConflicts(IReadOnlyCollection<string> ids)
    {
        var set = new HashSet<string>(ids, StringComparer.Ordinal);
        var result = new List<(string, string)>();
        foreach (var id in ids.OrderBy(i => i, StringComparer.Ordinal))
        {
            var p = Find(id);
            if (p == null) continue;
            foreach (var other in Patches.Where(o => set.Contains(o.Id) && string.CompareOrdinal(o.Id, id) > 0))
                if (p.Conflicts.Contains(other.Id) || other.Conflicts.Contains(id))
                    result.Add((id, other.Id));
        }
        return result;
    }

    /// <summary>Ids that conflict with <paramref name="id"/> (declared on either side).</summary>
    public IReadOnlyList<string> ConflictsOf(string id) => Patches
        .Where(o => o.Id != id && (o.Conflicts.Contains(id) || (Find(id)?.Conflicts.Contains(o.Id) ?? false)))
        .Select(o => o.Id).ToList();

    /// <summary>
    /// Parse a command-line list: "a,b,c"; "none" = no optional patches; "default" (or empty) =
    /// the catalogue defaults (returned as null).
    /// </summary>
    public static IReadOnlyList<string>? ParseIdList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (t.Equals("default", StringComparison.OrdinalIgnoreCase) || t.Equals("defaults", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.Equals("none", StringComparison.OrdinalIgnoreCase)) return [];
        return t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Game files (other than main.dol / RELS.arc / bi2.bin, which every build resets) that any
    /// catalogue patch may change. A build restores them from vanilla first, so turning a patch
    /// off really undoes it. REL targets that live inside RELS.arc are resolved by the caller.
    /// </summary>
    public IReadOnlyList<string> TouchedGameFiles => Patches
        .SelectMany(p => p.TouchedFiles)
        .Where(f => f != "sys/main.dol")
        .Distinct(StringComparer.Ordinal)
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToList();
}
