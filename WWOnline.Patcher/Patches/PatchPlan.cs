namespace WWOnline.Patcher.Patches;

/// <summary>
/// Which assembled diffs a build applies. Layout (same in GameMod/src/patches/patch_diffs/ and
/// the client's PatchData/patch_diffs/):
/// <code>
/// patch_diffs/&lt;required&gt;_diff.yaml           always applied
/// patch_diffs/optional/&lt;id&gt;_diff.yaml        applied only when &lt;id&gt; is selected
/// </code>
/// Every optional patch is assembled on every build, so the pre-built fallback can apply any
/// selection and free-space addresses don't depend on what is selected.
/// </summary>
public static class PatchPlan
{
    public static string OptionalDiffsDirectory(string patchDiffsDir) => Path.Combine(patchDiffsDir, OptionalPatchCatalog.DirectoryName);

    public static string OptionalDiffPath(string patchDiffsDir, string id) =>
        Path.Combine(OptionalDiffsDirectory(patchDiffsDir), id + PatchDiffFile.Suffix);

    public static IReadOnlyList<string> RequiredDiffFiles(string patchDiffsDir) =>
        Directory.Exists(patchDiffsDir)
            ? Directory.GetFiles(patchDiffsDir, "*" + PatchDiffFile.Suffix, SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.Ordinal).ToList()
            : [];

    /// <summary>
    /// The diffs to apply for <paramref name="selection"/>: required first, then optional in id order.
    /// Optional patches with no ASM (only @edit lines) have no diff. Throws if a selected patch's
    /// diff is missing (the assemble step didn't run, or PatchData is out of date).
    /// </summary>
    public static IReadOnlyList<(string Path, bool IsOptional)> GetDiffFiles(
        string patchDiffsDir, OptionalPatchCatalog catalog, IEnumerable<string> selection)
    {
        var required = RequiredDiffFiles(patchDiffsDir);
        if (required.Count == 0)
            throw new FileNotFoundException($"No required patch diffs in {patchDiffsDir}. Run the assemble step first.");

        // A top-level diff named after a catalogue patch is a leftover from before that patch became
        // optional (e.g. an old skip_intro_diff.yaml a PreserveNewest copy never deleted). Applying it
        // as required would make the patch impossible to turn off, so it is ignored.
        var result = required
            .Where(p => catalog.Find(PatchDiffFile.PatchNameFromPath(p)) == null)
            .Select(p => (p, false)).ToList();
        foreach (var id in selection.OrderBy(i => i, StringComparer.Ordinal))
        {
            var patch = catalog.Find(id) ?? throw new InvalidOperationException($"Unknown optional patch \"{id}\"");
            if (!patch.HasAsm) continue;
            var path = OptionalDiffPath(patchDiffsDir, id);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Optional patch \"{id}\" is selected but its diff is missing: {path}. Re-assemble the patches.");
            result.Add((path, true));
        }
        return result;
    }

    public static IReadOnlyList<PatchDiffFile> LoadDiffs(string patchDiffsDir, OptionalPatchCatalog catalog, IEnumerable<string> selection) =>
        GetDiffFiles(patchDiffsDir, catalog, selection).Select(f => PatchDiffFile.Load(f.Path, f.IsOptional)).ToList();

    /// <summary>Throws if <paramref name="selection"/> contains unknown ids or declared conflicts.</summary>
    public static PatchSelection ResolveStrict(OptionalPatchCatalog catalog, IEnumerable<string>? requested)
    {
        var selection = catalog.Resolve(requested);
        if (selection.UnknownIds.Count > 0)
            throw new InvalidOperationException(
                $"Unknown optional patch(es): {string.Join(", ", selection.UnknownIds)}. Known: {string.Join(", ", catalog.Patches.Select(p => p.Id))}");
        var conflicts = catalog.FindConflicts(selection.Ids);
        if (conflicts.Count > 0)
            throw new InvalidOperationException(
                "Conflicting optional patches selected: " + string.Join("; ", conflicts.Select(c => $"{c.A} + {c.B}")));
        return selection;
    }
}
