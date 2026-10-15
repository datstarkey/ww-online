using WWOnline.Native;
using WWOnline.Patcher.Patches;
using WWOnline.Services;
using WWOnline.Shared;
using WWOnline.Tests.Native;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

/// <summary>The Settings page's tabs: the app's own, then (native backend) the game's.</summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-settings-tabs-" + Guid.NewGuid().ToString("N"));
    private readonly GameSettingsService _settings;
    private readonly List<IDisposable> _owned = [];

    public SettingsViewModelTests()
    {
        _settings = new GameSettingsService(Path.Combine(_dir, "settings"));
    }

    public void Dispose()
    {
        foreach (var d in _owned) d.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>Updates off: no Velopack, no network.</summary>
    private sealed class NoUpdater : IAppUpdater
    {
        public bool IsInstalled => false;
        public string? CurrentVersion => null;
        public string? PendingRestartVersion => null;
        public Task<AvailableUpdate?> CheckAsync(CancellationToken ct) => Task.FromResult<AvailableUpdate?>(null);
        public Task DownloadAsync(AvailableUpdate update, Action<int> progress, CancellationToken ct) => Task.CompletedTask;
        public void ApplyOnExit(AvailableUpdate? update) { }
        public void ApplyAndRestart(AvailableUpdate? update) { }
    }

    private async Task<SettingsViewModel> Vm(GameBackend backend, bool withGame)
    {
        var saved = _settings.Load();
        saved.Backend = backend;
        _settings.Save(saved);

        GameSettingsViewModel? game = null;
        if (withGame)
        {
            var native = new FakeNativeGame { SettingsCatalog = GameSettingsViewModelTests.Catalog };
            game = new GameSettingsViewModel(new NativeGameSettings(native, Path.Combine(_dir, "user"), _ => null), a => a());
            await game.Pending;
        }
        var catalog = new OptionalPatchCatalogService(_settings, new OptionalPatchCatalog([]));
        var patchOptions = new PatchOptionsViewModel(catalog, _settings, null, a => a(), TimeSpan.Zero);
        var updates = new UpdateService(new NoUpdater(), new UpdateServiceOptions());
        var icons = new ItemIconService(_settings, null, Path.Combine(_dir, "icons"));
        var vm = new SettingsViewModel(_settings, new GamePatcherService(catalog), patchOptions,
            new UpdateViewModel(updates, a => a()), icons, new ItemIcons(icons, a => a(), _ => null), game: game);
        _owned.Add(vm);
        _owned.Add(updates);
        return vm;
    }

    [Fact]
    public async Task Native_TheGamesTabsFollowTheAppsOwn()
    {
        var vm = await Vm(GameBackend.Native, withGame: true);
        var game = vm.Game!;
        var controls = game.Sections.Single(s => s.Title == "Controls");

        Assert.True(vm.HasGame);
        Assert.True(vm.IsGameFilesTab);
        Assert.DoesNotContain(game.Sections, s => s.IsSelected);

        vm.ShowGameSectionCommand.Execute(controls);
        Assert.True(vm.IsGameTab);
        Assert.True(game.IsShown);
        Assert.Equal([controls], game.Sections.Where(s => s.IsSelected));

        vm.ShowTabCommand.Execute(SettingsTab.General);
        Assert.True(vm.IsGeneralTab);
        Assert.DoesNotContain(game.Sections, s => s.IsSelected);

        // Back to the game's tabs: the one picked last.
        vm.ShowTabCommand.Execute(SettingsTab.Game);
        Assert.Equal([controls], game.Sections.Where(s => s.IsSelected));
    }

    [Fact]
    public async Task Dolphin_HasNoGameTabs_AndItsPatchesTabGoesWithIt()
    {
        var vm = await Vm(GameBackend.Dolphin, withGame: false);

        Assert.False(vm.HasGame);
        vm.ShowTabCommand.Execute(SettingsTab.Game);
        Assert.True(vm.IsGameFilesTab);

        vm.ShowTabCommand.Execute(SettingsTab.Patches);
        Assert.True(vm.IsPatchesTab);
        vm.IsNative = true; // the other backend picked (it applies after a restart)
        Assert.True(vm.IsGameFilesTab);
    }
}
