using System.Diagnostics;
using WWOnline.Patcher.BinaryFormats.Png;
using Xunit;

namespace WWOnline.Patcher.Tests.Icons;

/// <summary>
/// scripts/check-no-nintendo-files.ps1 must reject the item icons the client decodes from the game,
/// wherever they end up and whatever they are called. Runs the real script (pwsh, else Windows
/// PowerShell); skipped where neither exists.
/// </summary>
public sealed class NintendoFilesGuardTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-guard-" + Guid.NewGuid().ToString("N"));

    public NintendoFilesGuardTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static readonly string? Shell = FindShell();

    private static string? FindShell()
    {
        foreach (var candidate in new[] { "pwsh", "powershell" })
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(candidate, "-NoProfile -Command exit 0")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                });
                if (p != null && p.WaitForExit(30_000) && p.ExitCode == 0) return candidate;
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
        return null;
    }

    private sealed class PowerShellFactAttribute : FactAttribute
    {
        public PowerShellFactAttribute()
        {
            if (Shell == null) Skip = "Neither pwsh nor powershell is available";
        }
    }

    /// <summary>Runs the guard over <paramref name="path"/>: its exit code and output.</summary>
    private static (int ExitCode, string Output) RunGuard(string path)
    {
        var script = Path.Combine(TestRepo.Root, "scripts", "check-no-nintendo-files.ps1");
        var psi = new ProcessStartInfo(Shell!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Path", path })
            psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        Assert.True(p.WaitForExit(120_000), "the guard script timed out");
        return (p.ExitCode, output.Result + error.Result);
    }

    private static byte[] MarkedPng() => PngWriter.Encode(new byte[4 * 4 * 4], 4, 4);

    [PowerShellFact]
    public void MarkedPng_IsRejected_WhateverItIsCalled()
    {
        File.WriteAllBytes(Path.Combine(_dir, "logo.png"), MarkedPng());
        var (code, output) = RunGuard(_dir);
        Assert.Equal(1, code);
        Assert.Contains("logo.png", output);
    }

    [PowerShellFact]
    public void IconCacheFolder_IsRejected()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "stuff", "GameIcons")).FullName;
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not even an image");
        var (code, output) = RunGuard(_dir);
        Assert.Equal(1, code);
        Assert.Contains("GameIcons", output);
    }

    [PowerShellFact]
    public void ExtractorStagingFolder_IsRejected()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "GameIcons.new-1234abcd")).FullName;
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "x");
        var (code, output) = RunGuard(_dir);
        Assert.Equal(1, code);
        Assert.Contains("GameIcons.new-1234abcd", output);
    }

    [PowerShellFact]
    public void IconCacheFolder_PassedDirectly_IsRejected()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "GameIcons")).FullName;
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "x");
        var (code, output) = RunGuard(folder);
        Assert.Equal(1, code);
        Assert.Contains("GameIcons", output);
    }

    [PowerShellFact]
    public void OrdinaryFiles_Pass()
    {
        // A PNG from anywhere else (no marker), and a file whose name merely mentions icons.
        var plain = MarkedPng();
        plain[PngWriter.MarkerOffset] = (byte)'z';
        File.WriteAllBytes(Path.Combine(_dir, "screenshot.png"), plain);
        File.WriteAllText(Path.Combine(_dir, "GameIcons.cs"), "class GameIcons {}");
        var (code, output) = RunGuard(_dir);
        Assert.True(code == 0, output);
    }
}
