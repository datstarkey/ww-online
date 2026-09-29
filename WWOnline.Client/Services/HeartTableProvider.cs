using Serilog;
using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>
/// The heart table (<see cref="HeartTable"/>): every Piece of Heart and Heart Container and the flag that records
/// it, built from the player's own stage files plus our reward catalogue on first use and kept in memory
/// (<see cref="StageTableProvider{T}"/>). Null until built, or when no game folder has stage data (then max
/// health keeps the old max-merge of the shared items).
/// </summary>
public sealed class HeartTableProvider : StageTableProvider<HeartTable>
{
    public HeartTableProvider(GameSettingsService settings) : this(() => DefaultGamePaths(settings), BuildFrom) { }

    public HeartTableProvider(Func<IEnumerable<string>> gamePaths, Func<string, HeartTable?> build)
        : base(Log.ForContext<HeartTableProvider>(), gamePaths, build) { }

    protected override string Tag => "[hearts]";

    protected override string MissingMessage =>
        "no game folder with stage data (files/res/Stage + sys/main.dol): max health can't be derived until one is set on the Settings page";

    private static HeartTable? BuildFrom(string gamePath)
    {
        var skipped = new List<string>();
        var table = HeartTableBuilder.BuildFromGame(gamePath, skipped.Add);
        if (skipped.Count > 0)
            Log.ForContext<HeartTableProvider>().Warning("[hearts] skipped {Count} stage(s) of {Path} that couldn't be read: {Stages}", skipped.Count, gamePath, string.Join("; ", skipped));
        return table;
    }

    /// <summary>A table with no source from the stage files is a folder without the game's stages.</summary>
    protected override bool IsUsable(HeartTable table) => table.Sources.Any(s => s.Type != HeartSourceType.Reward);

    protected override void LogBuilt(string path, HeartTable table)
    {
        Logger.Information("[hearts] heart table from {Path} ({Stages} stages): {Summary}", path, table.StageCount, table.Summary());
        if (table.PiecesTotal != HeartTable.VanillaPieces || table.ContainersTotal != HeartTable.VanillaContainers)
            Logger.Warning("[hearts] the table has {Pieces} Pieces of Heart and {Containers} Heart Containers, not the vanilla 44 and 6 (modded stage data?)",
                table.PiecesTotal, table.ContainersTotal);
        foreach (var u in table.Untracked)
            Logger.Warning("[hearts] a heart the save can't record, not counted: {What}", u);
    }
}
