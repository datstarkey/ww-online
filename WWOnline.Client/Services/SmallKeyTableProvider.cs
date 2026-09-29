using Serilog;
using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>
/// The small-key table (<see cref="SmallKeyTable"/>): every dungeon's key sources and key doors, built from
/// the player's own stage files on first use (about 0.3 s for the whole game) and kept in memory
/// (<see cref="StageTableProvider{T}"/>). Null until built, or when no game folder has stage data (then
/// small keys don't sync).
/// </summary>
public sealed class SmallKeyTableProvider : StageTableProvider<SmallKeyTable>
{
    public SmallKeyTableProvider(GameSettingsService settings) : this(() => DefaultGamePaths(settings), BuildFrom) { }

    public SmallKeyTableProvider(Func<IEnumerable<string>> gamePaths, Func<string, SmallKeyTable?> build)
        : base(Log.ForContext<SmallKeyTableProvider>(), gamePaths, build) { }

    protected override string Tag => "[keys]";

    protected override string MissingMessage =>
        "no game folder with stage data (files/res/Stage + sys/main.dol): small keys won't be shared until one is set on the Settings page";

    private static SmallKeyTable? BuildFrom(string gamePath)
    {
        var skipped = new List<string>();
        var table = SmallKeyTableBuilder.BuildFromGame(gamePath, skipped.Add);
        if (skipped.Count > 0)
            Log.ForContext<SmallKeyTableProvider>().Warning("[keys] skipped {Count} stage(s) of {Path} that couldn't be read: {Stages}", skipped.Count, gamePath, string.Join("; ", skipped));
        return table;
    }

    protected override bool IsUsable(SmallKeyTable table) => table.Dungeons.Count > 0;

    protected override void LogBuilt(string path, SmallKeyTable table)
    {
        Logger.Information("[keys] small-key table from {Path} ({Stages} stages): {Summary}", path, table.StageCount, table.Summary());
        foreach (var d in table.Dungeons.Values.Where(d => d.Doors.Count > 0 && d.Sources.Count != d.Doors.Count))
            Logger.Warning("[keys] slot {Slot} has {Keys} key(s) for {Doors} key door(s)", d.Slot, d.Sources.Count, d.Doors.Count);
        foreach (var d in table.Dungeons.Values.Where(d => d.Untracked.Count > 0))
            Logger.Information("[keys] slot {Slot}: key source(s) the save can't show, not counted: {What}", d.Slot, string.Join("; ", d.Untracked));
    }
}
