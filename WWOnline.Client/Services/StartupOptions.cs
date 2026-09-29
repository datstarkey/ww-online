namespace WWOnline.Services;

/// <summary>
/// Command-line options that auto-drive the client on startup.
///
/// Supported flags:
///   --player &lt;name&gt;       set player name (also saves to settings)
///   --host &lt;host&gt;         server host to connect to   (default: localhost)
///   --port &lt;port&gt;         server port                 (default: 6969)
///   --auto-connect          connect to server on launch (retries while the host starts up)
///   --host-server           start a server from this client and join it ("Host" button)
///   --auto-attach           attach to Dolphin on launch (after connect)
///   --dolphin-pid &lt;N&gt;     specific Dolphin PID to attach to (default: first found)
///   --allow-stale           let --auto-attach attach although the game build is stale
///                           (dev-test.ps1 -AllowStale; otherwise it refuses)
///   --log-file &lt;path&gt;     write this instance's log to exactly this file
///
/// Headless modes (no UI; the process exits with a status code — see <see cref="HeadlessCommands"/>):
///   --check-build           is the patched game up to date? Dev: against the GameMod sources
///                           (config.json's game_path). Installed: against the app's
///                           PatchData/patchdata-stamp.json (the settings' game path).
///   --patch                 run the full patch pipeline (compile C + patch the game)
///   --patches &lt;ids&gt;      optional patches for --patch / --check-build: "a,b,c", "none" or
///                           "default" (--patch without it uses the defaults; --check-build
///                           without it: dev doesn't compare the selection, installed uses the
///                           saved one)
///   --build-patchdata &lt;dir&gt; build the release PatchData folder from GameMod sources with
///                           devkitPPC (DEVKITPPC or the default path); no config.json, no game
///                           files. Runs on Linux too (the release workflow uses it).
///   --gamemod &lt;dir&gt;       GameMod folder for --build-patchdata (default: found above the
///                           working directory or the app)
///
/// Example:
///   WWOnline.Client.exe --player Jakez --auto-connect --auto-attach --dolphin-pid 12345
/// </summary>
public sealed record StartupOptions(
    string? PlayerName,
    string? Host,
    int? Port,
    bool AutoConnect,
    bool AutoAttach,
    int? DolphinPid,
    string? LogFile = null,
    bool CheckBuild = false,
    bool Patch = false,
    bool HostServer = false,
    string? Patches = null,
    string? BuildPatchData = null,
    string? GameModPath = null,
    bool AllowStale = false)
{
    public bool IsHeadless => CheckBuild || Patch || BuildPatchData != null;

    public static StartupOptions Parse(string[] args)
    {
        string? playerName = null;
        string? host = null;
        int? port = null;
        bool autoConnect = false;
        bool autoAttach = false;
        int? dolphinPid = null;
        string? logFile = null;
        bool checkBuild = false;
        bool patch = false;
        bool hostServer = false;
        string? patches = null;
        string? buildPatchData = null;
        string? gameModPath = null;
        bool allowStale = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--player" when i + 1 < args.Length:
                    playerName = args[++i];
                    break;
                case "--host" when i + 1 < args.Length:
                    host = args[++i];
                    break;
                case "--port" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var p)) port = p;
                    break;
                case "--auto-connect":
                    autoConnect = true;
                    break;
                case "--auto-attach":
                    autoAttach = true;
                    break;
                case "--dolphin-pid" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var pid)) dolphinPid = pid;
                    break;
                case "--log-file" when i + 1 < args.Length:
                    logFile = args[++i];
                    break;
                case "--check-build":
                    checkBuild = true;
                    break;
                case "--patch":
                    patch = true;
                    break;
                case "--host-server":
                    hostServer = true;
                    break;
                case "--patches" when i + 1 < args.Length:
                    patches = args[++i];
                    break;
                case "--build-patchdata":
                    // Still headless without a folder (""), so the mode reports the mistake
                    // instead of starting the UI.
                    buildPatchData = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "";
                    break;
                case "--allow-stale":
                    allowStale = true;
                    break;
                case "--gamemod" when i + 1 < args.Length:
                    gameModPath = args[++i];
                    break;
            }
        }

        // Logging isn't configured yet at parse time — Program logs the result once it is.
        return new StartupOptions(playerName, host, port, autoConnect, autoAttach, dolphinPid, logFile, checkBuild, patch, hostServer, patches,
            buildPatchData, gameModPath, allowStale);
    }
}
