using Serilog;
using WWOnline.Data;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Pipeline;

namespace WWOnline.Services;

/// <summary>Whether the patched game matches the player's optional patch selection.</summary>
/// <param name="NeedsRepatch">True when the game has no build stamp, was built with another selection, or (dev) is stale.</param>
/// <param name="Message">One line for the UI / log.</param>
/// <param name="BuiltWith">Optional patch ids the game was built with (empty when unknown).</param>
public sealed record OptionalPatchBuildStatus(bool NeedsRepatch, string Message, IReadOnlyList<string> BuiltWith);

/// <summary>
/// The optional game patches a player can choose when patching, and their saved selection.
///
/// The catalogue is the header block of each GameMod/src/patches/optional/&lt;id&gt;.asm (the one
/// source of truth, parsed by <see cref="OptionalPatchCatalog"/>). Dev builds read it from GameMod/;
/// release builds read the copy shipped in PatchData/optional/.
///
/// The selection is <see cref="GameSettings.OptionalPatches"/>: null means "never chosen", which
/// resolves to each patch's default. Ids that no longer exist are dropped.
/// </summary>
public class OptionalPatchCatalogService
{
    private static readonly ILogger Logger = Log.ForContext<OptionalPatchCatalogService>();

    private readonly GameSettingsService _settingsService;
    private readonly Lazy<(OptionalPatchCatalog Catalog, string Source)> _catalog;

    public OptionalPatchCatalogService(GameSettingsService settingsService)
    {
        _settingsService = settingsService;
        _catalog = new Lazy<(OptionalPatchCatalog, string)>(LoadCatalog);
    }

    /// <summary>With a given catalogue instead of GameMod/ or PatchData/ (tests).</summary>
    public OptionalPatchCatalogService(GameSettingsService settingsService, OptionalPatchCatalog catalog)
    {
        _settingsService = settingsService;
        _catalog = new Lazy<(OptionalPatchCatalog, string)>(() => (catalog, ""));
    }

    public OptionalPatchCatalog Catalog => _catalog.Value.Catalog;

    /// <summary>Folder the catalogue was read from (GameMod/src/patches/optional or PatchData/optional).</summary>
    public string CatalogSource => _catalog.Value.Source;

    /// <summary>Every optional patch, ordered by category then name.</summary>
    public IReadOnlyList<OptionalPatch> Patches => Catalog.Patches;

    public IReadOnlyList<string> DefaultIds => Catalog.DefaultIds;

    /// <summary>The saved selection resolved against the catalogue (defaults when nothing is saved).</summary>
    public IReadOnlyList<string> GetSelectedIds() => Resolve(_settingsService.Load().OptionalPatches);

    /// <summary>
    /// Resolve a stored selection: null = defaults, unknown ids dropped (and logged). Conflicting ids
    /// (e.g. a hand-edited settings file) keep the newest pick: the one later in <paramref name="stored"/>.
    /// </summary>
    public IReadOnlyList<string> Resolve(IEnumerable<string>? stored)
    {
        var requested = stored?.Select(id => id.Trim()).Where(id => id.Length > 0).ToList();
        var selection = Catalog.Resolve(requested);
        if (selection.UnknownIds.Count > 0)
            Logger.Warning("[patches] ignoring unknown optional patch ids in settings: {Ids}", string.Join(", ", selection.UnknownIds));
        return requested == null ? selection.Ids : KeepNewestPicks(requested, selection.Ids);
    }

    /// <summary>
    /// Drop conflicting ids from <paramref name="ids"/>, keeping whichever was picked last
    /// (later in <paramref name="pickOrder"/>). Returns the survivors in ordinal order.
    /// </summary>
    private IReadOnlyList<string> KeepNewestPicks(IReadOnlyList<string> pickOrder, IReadOnlyList<string> ids)
    {
        if (Catalog.FindConflicts(ids).Count == 0) return ids;

        var wanted = new HashSet<string>(ids, StringComparer.Ordinal);
        var kept = new List<string>();
        var dropped = new List<string>();
        // Newest first; Distinct keeps each id's last position.
        foreach (var id in pickOrder.Where(wanted.Contains).Reverse().Distinct(StringComparer.Ordinal))
        {
            var conflicts = Catalog.ConflictsOf(id);
            if (kept.Any(conflicts.Contains)) dropped.Add(id);
            else kept.Add(id);
        }
        Logger.Warning("[patches] conflicting optional patches selected; keeping the newest pick, dropping {Ids}", string.Join(", ", dropped));
        return kept.OrderBy(i => i, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Save the selection (sorted, known ids only) and return what was saved. Conflicting ids keep
    /// the newest pick (later in <paramref name="ids"/>), so a bad selection never blocks saving.
    /// </summary>
    public IReadOnlyList<string> SaveSelectedIds(IEnumerable<string> ids)
    {
        var resolved = Resolve(ids);
        var settings = _settingsService.Load();
        settings.OptionalPatches = resolved.ToList();
        _settingsService.Save(settings);
        return resolved;
    }

    /// <summary>Forget the saved selection so every patch uses its default again.</summary>
    public void ResetToDefaults()
    {
        var settings = _settingsService.Load();
        settings.OptionalPatches = null;
        _settingsService.Save(settings);
    }

    /// <summary>
    /// Does the patched game at <paramref name="gamePath"/> match the saved selection and the game
    /// code this app would patch in? See <see cref="GameBuildCheck"/>.
    /// </summary>
    public OptionalPatchBuildStatus CheckGameBuild(string gamePath) => CheckGameBuild(gamePath, GetSelectedIds());

    /// <summary>
    /// As above for an explicit selection. Hashes the GameMod sources in dev, so call it off the UI
    /// thread (PatchOptionsViewModel does).
    /// </summary>
    public virtual OptionalPatchBuildStatus CheckGameBuild(string gamePath, IReadOnlyList<string> desired)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            return new OptionalPatchBuildStatus(true, "The patched game folder doesn't exist yet. Patch the game.", []);

        var result = GameBuildCheck.Check(gamePath, desired, GamePatcherService.FindGameModFolder(), GamePatcherService.DefaultPatchDataPath);
        return new OptionalPatchBuildStatus(result.Status != BuildStamp.Status.Fresh, result.Describe(), result.RecordedPatches);
    }

    private static (OptionalPatchCatalog, string) LoadCatalog()
    {
        var candidates = new List<string>();
        var gameMod = GamePatcherService.FindGameModFolder();
        if (gameMod != null) candidates.Add(Path.Combine(gameMod, "src", "patches", OptionalPatchCatalog.DirectoryName));
        candidates.Add(new PatchDataFolder(GamePatcherService.DefaultPatchDataPath).CatalogPath);

        foreach (var dir in candidates.Where(Directory.Exists))
        {
            try
            {
                var catalog = OptionalPatchCatalog.Load(dir);
                Logger.Information("[patches] {Count} optional patches from {Dir}", catalog.Patches.Count, dir);
                return (catalog, dir);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                Logger.Error(ex, "[patches] could not read the optional patch catalogue in {Dir}", dir);
            }
        }
        Logger.Warning("[patches] no optional patch catalogue found (looked in {Dirs})", string.Join(", ", candidates));
        return (OptionalPatchCatalog.Empty, "");
    }

    /// <summary>main.dol regions no patch may write, from the generated puppet_shared.h constants.</summary>
    internal static ProtectedRegions ProtectedRegions =>
        new(PuppetLayout.SCRATCH_REGION_START, PuppetLayout.SCRATCH_REGION_END);
}
