using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Serilog;

namespace WWOnline.Services;

/// <summary>
/// Restart WW-Online: start a new copy with the same arguments, then shut this one down the normal way (rooms
/// closed, the game stopped). The built-in game can't restart inside a process, so its restart is this.
/// </summary>
public static class AppRestart
{
    public static void Restart()
    {
        if (Environment.ProcessPath is not { } exe) return;
        try
        {
            System.Diagnostics.Process.Start(exe, Environment.GetCommandLineArgs().Skip(1));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[restart] couldn't start {Exe}", exe);
            return;
        }
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
