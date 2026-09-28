using WWOnline.Services;
using WWOnline.Shared;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Services;

public class UpdateServiceTests
{
    /// <summary>Scriptable IAppUpdater: no Velopack, no network.</summary>
    private sealed class FakeUpdater : IAppUpdater
    {
        public bool IsInstalled { get; set; } = true;
        public string? CurrentVersion { get; set; } = "1.0.0";
        public string? PendingRestartVersion { get; set; }
        public Func<CancellationToken, Task<AvailableUpdate?>> Check { get; set; } = _ => Task.FromResult<AvailableUpdate?>(null);
        public Func<AvailableUpdate, Action<int>, CancellationToken, Task> Download { get; set; } = (_, p, _) => { p(50); p(100); return Task.CompletedTask; };
        public int CheckCalls, DownloadCalls, ApplyOnExitCalls, ApplyAndRestartCalls;
        public AvailableUpdate? Applied;

        public Task<AvailableUpdate?> CheckAsync(CancellationToken ct) { Interlocked.Increment(ref CheckCalls); return Check(ct); }
        public Task DownloadAsync(AvailableUpdate update, Action<int> progress, CancellationToken ct) { Interlocked.Increment(ref DownloadCalls); return Download(update, progress, ct); }
        public void ApplyOnExit(AvailableUpdate? update) { ApplyOnExitCalls++; Applied = update; }
        public void ApplyAndRestart(AvailableUpdate? update) { ApplyAndRestartCalls++; Applied = update; }
    }

    private static readonly UpdateServiceOptions NoTimer = new()
    {
        StartupDelay = Timeout.InfiniteTimeSpan, CheckInterval = Timeout.InfiniteTimeSpan, AutoDownload = false,
    };

    private static Task<AvailableUpdate?> Found(string version) => Task.FromResult<AvailableUpdate?>(new AvailableUpdate(version));

    [Fact]
    public async Task NotInstalled_IsDisabled_AndNeverCallsTheUpdater()
    {
        var fake = new FakeUpdater { IsInstalled = false, CurrentVersion = null };
        using var svc = new UpdateService(fake, NoTimer);
        svc.Start();

        Assert.Equal(UpdateStatus.Disabled, svc.State.Status);
        Assert.False(svc.IsEnabled);
        Assert.False(await svc.CheckForUpdatesAsync());
        Assert.False(await svc.DownloadAsync());
        Assert.False(svc.ApplyOnExit());
        Assert.Equal(0, fake.CheckCalls + fake.DownloadCalls + fake.ApplyOnExitCalls);
        Assert.Equal(AppInfo.Version, svc.CurrentVersion);
    }

    [Fact]
    public async Task IsInstalledThrowing_MeansDisabled_NotACrash()
    {
        var fake = new ThrowingInstalledUpdater();
        using var svc = new UpdateService(fake, NoTimer);
        svc.Start();
        Assert.Equal(UpdateStatus.Disabled, svc.State.Status);
        Assert.False(await svc.CheckForUpdatesAsync());
    }

    private sealed class ThrowingInstalledUpdater : IAppUpdater
    {
        public bool IsInstalled => throw new InvalidOperationException("boom");
        public string? CurrentVersion => throw new InvalidOperationException("boom");
        public string? PendingRestartVersion => null;
        public Task<AvailableUpdate?> CheckAsync(CancellationToken ct) => throw new InvalidOperationException();
        public Task DownloadAsync(AvailableUpdate u, Action<int> p, CancellationToken ct) => throw new InvalidOperationException();
        public void ApplyOnExit(AvailableUpdate? u) => throw new InvalidOperationException();
        public void ApplyAndRestart(AvailableUpdate? u) => throw new InvalidOperationException();
    }

    [Fact]
    public async Task Check_UpToDate()
    {
        using var svc = new UpdateService(new FakeUpdater(), NoTimer);
        Assert.Equal(UpdateStatus.Idle, svc.State.Status);

        Assert.False(await svc.CheckForUpdatesAsync());
        Assert.Equal(UpdateStatus.UpToDate, svc.State.Status);
        Assert.NotNull(svc.State.LastChecked);
    }

    [Fact]
    public async Task Check_FindsUpdate_ThenDownload_ReportsProgress_AndIsReadyToRestart()
    {
        var fake = new FakeUpdater { Check = _ => Found("1.1.0") };
        using var svc = new UpdateService(fake, NoTimer);
        var seen = new List<UpdateState>();
        svc.StateChanged += s => { lock (seen) seen.Add(s); };

        Assert.True(await svc.CheckForUpdatesAsync());
        Assert.Equal(new UpdateState(UpdateStatus.Available, "1.1.0", LastChecked: svc.State.LastChecked), svc.State);

        Assert.True(await svc.DownloadAsync());
        Assert.Equal(UpdateStatus.ReadyToRestart, svc.State.Status);
        Assert.Equal("1.1.0", svc.State.Version);
        Assert.Contains(seen, s => s.Status == UpdateStatus.Downloading && s.Progress == 50);

        Assert.True(svc.ApplyOnExit());
        Assert.Equal(1, fake.ApplyOnExitCalls);
        Assert.Equal("1.1.0", fake.Applied?.Version);
    }

    [Fact]
    public async Task CheckFailure_BecomesErrorState_AndDoesNotThrow()
    {
        var fake = new FakeUpdater { Check = _ => throw new HttpRequestException("rate limited") };
        using var svc = new UpdateService(fake, NoTimer);

        Assert.False(await svc.CheckForUpdatesAsync());
        Assert.Equal(UpdateStatus.Error, svc.State.Status);
        Assert.Contains("rate limited", svc.State.Error);

        // Recovers on the next successful check.
        fake.Check = _ => Found("2.0.0");
        Assert.True(await svc.CheckForUpdatesAsync());
        Assert.Equal(UpdateStatus.Available, svc.State.Status);
        Assert.Null(svc.State.Error);
    }

    [Fact]
    public async Task DownloadFailure_IsError_AndCanBeRetried()
    {
        var fake = new FakeUpdater { Check = _ => Found("1.1.0"), Download = (_, _, _) => throw new IOException("disk full") };
        using var svc = new UpdateService(fake, NoTimer);
        await svc.CheckForUpdatesAsync();

        Assert.False(await svc.DownloadAsync());
        Assert.Equal(UpdateStatus.Error, svc.State.Status);
        Assert.Equal("1.1.0", svc.State.Version);

        fake.Download = (_, _, _) => Task.CompletedTask;
        Assert.True(await svc.DownloadAsync());
        Assert.Equal(UpdateStatus.ReadyToRestart, svc.State.Status);
    }

    [Fact]
    public async Task ApplyFailure_IsError_NotAThrow()
    {
        var fake = new ThrowOnApplyUpdater();
        using var svc = new UpdateService(fake, NoTimer);
        await svc.CheckForUpdatesAsync();
        await svc.DownloadAsync();

        Assert.False(svc.ApplyOnExit());
        Assert.Equal(UpdateStatus.Error, svc.State.Status);
    }

    private sealed class ThrowOnApplyUpdater : IAppUpdater
    {
        public bool IsInstalled => true;
        public string? CurrentVersion => "1.0.0";
        public string? PendingRestartVersion => null;
        public Task<AvailableUpdate?> CheckAsync(CancellationToken ct) => Found("1.1.0");
        public Task DownloadAsync(AvailableUpdate u, Action<int> p, CancellationToken ct) => Task.CompletedTask;
        public void ApplyOnExit(AvailableUpdate? u) => throw new UnauthorizedAccessException("locked");
        public void ApplyAndRestart(AvailableUpdate? u) => throw new UnauthorizedAccessException("locked");
    }

    [Fact]
    public async Task ConcurrentCheck_IsSkipped_WhileOneIsRunning()
    {
        var gate = new TaskCompletionSource<AvailableUpdate?>();
        var fake = new FakeUpdater { Check = _ => gate.Task };
        using var svc = new UpdateService(fake, NoTimer);

        var first = svc.CheckForUpdatesAsync();
        Assert.Equal(UpdateStatus.Checking, svc.State.Status);
        Assert.False(await svc.CheckForUpdatesAsync()); // busy: returns at once
        Assert.Equal(1, fake.CheckCalls);

        gate.SetResult(null);
        Assert.False(await first);
        Assert.Equal(UpdateStatus.UpToDate, svc.State.Status);
    }

    [Fact]
    public void PendingDownloadFromLastSession_StartsReadyToRestart()
    {
        var fake = new FakeUpdater { PendingRestartVersion = "1.2.0" };
        using var svc = new UpdateService(fake, NoTimer);
        svc.Start();

        Assert.Equal(UpdateStatus.ReadyToRestart, svc.State.Status);
        Assert.Equal("1.2.0", svc.State.Version);
    }

    [Fact]
    public async Task ScheduledCheck_AutoDownloads()
    {
        var fake = new FakeUpdater { Check = _ => Found("1.1.0") };
        using var svc = new UpdateService(fake, NoTimer with { AutoDownload = true });

        await svc.CheckAndMaybeDownloadAsync(CancellationToken.None);

        Assert.Equal(UpdateStatus.ReadyToRestart, svc.State.Status);
        Assert.Equal(1, fake.DownloadCalls);

        // Once downloaded, later scheduled checks leave it alone.
        await svc.CheckAndMaybeDownloadAsync(CancellationToken.None);
        Assert.Equal(1, fake.CheckCalls);
    }

    [Fact]
    public async Task Start_RunsTheFirstCheckAfterTheStartupDelay()
    {
        var checkedOnce = new TaskCompletionSource();
        var fake = new FakeUpdater { Check = _ => { checkedOnce.TrySetResult(); return Task.FromResult<AvailableUpdate?>(null); } };
        using var svc = new UpdateService(fake, NoTimer with { StartupDelay = TimeSpan.FromMilliseconds(10) });

        svc.Start();
        svc.Start(); // idempotent

        await checkedOnce.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(SpinWait.SpinUntil(() => svc.State.Status == UpdateStatus.UpToDate, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task RealVelopackUpdater_OutsideAnInstall_IsANoOp()
    {
        // The test host (like dev runs from bin/) is not a Velopack install.
        var updater = new VelopackAppUpdater(new UpdateServiceOptions());
        Assert.False(updater.IsInstalled);
        Assert.False(VelopackAppUpdater.IsRunningInstalled);

        using var svc = new UpdateService(updater, NoTimer);
        svc.Start();
        Assert.Equal(UpdateStatus.Disabled, svc.State.Status);
        Assert.False(await svc.CheckForUpdatesAsync());
    }

    [Fact]
    public void Options_DefaultToTheRepoFromDirectoryBuildProps()
    {
        var options = new UpdateServiceOptions();
        Assert.Equal(AppInfo.RepositoryUrl, options.Source);
        Assert.StartsWith("https://github.com/", AppInfo.RepositoryUrl);
    }

    [Fact]
    public async Task ViewModel_TracksServiceState()
    {
        var fake = new FakeUpdater { Check = _ => Found("1.1.0") };
        using var svc = new UpdateService(fake, NoTimer);
        using var vm = new UpdateViewModel(svc, a => a());

        Assert.True(vm.IsEnabled);
        Assert.False(vm.ShowBanner);
        Assert.True(vm.CheckForUpdatesCommand.CanExecute(null));
        Assert.False(vm.RestartToUpdateCommand.CanExecute(null));

        await svc.CheckForUpdatesAsync();
        Assert.True(vm.IsUpdateAvailable);
        Assert.True(vm.ShowBanner);
        Assert.Equal("1.1.0", vm.AvailableVersion);
        Assert.True(vm.DownloadCommand.CanExecute(null));

        await svc.DownloadAsync();
        Assert.True(vm.IsReadyToRestart);
        Assert.True(vm.RestartToUpdateCommand.CanExecute(null));
        Assert.Contains("1.1.0", vm.StatusText);
    }

    [Fact]
    public void ViewModel_Disabled_ForDevBuilds()
    {
        using var svc = new UpdateService(new FakeUpdater { IsInstalled = false }, NoTimer);
        using var vm = new UpdateViewModel(svc, a => a());

        Assert.False(vm.IsEnabled);
        Assert.False(vm.ShowBanner);
        Assert.False(vm.CheckForUpdatesCommand.CanExecute(null));
    }
}
