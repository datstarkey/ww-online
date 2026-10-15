using Serilog;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.ViewModels;

namespace WWOnline.Native;

/// <summary>
/// Dev/CI (--test-shots, usually with --hidden; scripts/dev-test-native.sh --hidden): at each listed game frame, saves
/// the game's frame and the view's surface as PNGs in --test-dir and logs the location, the rupees and the shared wallet;
/// --test-rupees adds rupees at a frame (a wallet change for the other clients to receive); --test-warp presses the Room
/// page's "Warp to" on a player at a frame. Logs "[test] done" after the last step and keeps running: the script stops
/// the clients.
/// </summary>
public static class NativeTestRun
{
    private static readonly ILogger Logger = Log.ForContext(typeof(NativeTestRun));

    private readonly record struct Step(uint Frame, int Rupees, string? WarpTo = null);

    public static async Task RunAsync(StartupOptions options, GameHost host, IDolphinService game, SharedWalletService wallet,
        DashboardViewModel room)
    {
        var dir = Path.GetFullPath(options.TestDir ?? ".");
        Directory.CreateDirectory(dir);
        var steps = (options.TestShots ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => new Step(uint.Parse(f.Trim()), 0)).ToList();
        if (options.TestRupees is { } rupees)
        {
            var parts = rupees.Split(':');
            steps.Add(new Step(uint.Parse(parts[0]), int.Parse(parts[1])));
        }
        if (options.TestWarp is { } warp)
        {
            var parts = warp.Split(':', 2);
            steps.Add(new Step(uint.Parse(parts[0]), 0, parts[1]));
        }
        Logger.Information("[test] {Count} steps, PNGs in {Dir}", steps.Count, dir);

        foreach (var step in steps.OrderBy(s => s.Frame))
        {
            if (!await WaitFrame(host, step.Frame))
            {
                Logger.Error("[test] FAIL: frame {Frame} not reached (failed {Failed}, fatal {Fatal})", step.Frame, host.HasFailed, host.FatalCode);
                return;
            }
            if (step.WarpTo is { } name)
            {
                await Warp(room, name);
                continue;
            }
            if (step.Rupees != 0)
            {
                Logger.Information("[test] frame {Frame}: adding {Rupees} rupees: {Ok}", step.Frame, step.Rupees, GameActions.AddRupees(game, step.Rupees));
                continue;
            }
            Save(dir, $"frame-{step.Frame}", TwwNative.tww_save_frame_png);
            Save(dir, $"surface-{step.Frame}", TwwNative.tww_save_surface_png);
            var s = host.Snapshot;
            var scene = game.ReadScene();
            Logger.Information("[test] frame {Frame}: stage {Stage} room {Room} flags {Flags} pos ({X:0} {Y:0} {Z:0}), " +
                "scene gate: link {Link}, stage change pending {Pending}, idle {Idle}; rupees {Rupees}, wallet {Wallet}",
                s?.Frame, s?.Location.Stage.ToString(), s?.Location.Room, s?.Location.Flags, s?.Local.Pos[0], s?.Local.Pos[1], s?.Local.Pos[2],
                scene?.LinkPresent, scene?.NextStagePending, SceneStabilityGate.IsIdle(game),
                game.Read(GameMemoryAddresses.Player.RupeeCount), wallet.Total);
        }
        Logger.Information("[test] done");
    }

    private static async Task Warp(DashboardViewModel room, string name)
    {
        var (status, failed) = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (room.Server.Players.FirstOrDefault(p => p.PlayerName == name && !p.IsLocal) is not { } row)
                return ($"no player {name} in the room", true);
            await room.WarpToPlayerCommand.ExecuteAsync(row);
            return (room.WarpStatus, room.WarpFailed);
        });
        Logger.Information("[test] warp to {Name}: {Result}: {Status}", name, failed ? "failed" : "ok", status);
    }

    // The game starts after joining the room: wait for it however long that takes, unless it fails.
    private static async Task<bool> WaitFrame(GameHost host, uint frame)
    {
        while ((host.Snapshot?.Frame ?? 0) < frame)
        {
            if (host.HasFailed) return false;
            await Task.Delay(50);
        }
        return true;
    }

    private static void Save(string dir, string name, Func<string, int> save)
    {
        var path = Path.Combine(dir, name + ".png");
        if (save(path) == 1 && File.Exists(path))
            Logger.Information("[test] saved {Path}", path);
        else
            Logger.Error("[test] FAILED to save {Path}", path);
    }
}
