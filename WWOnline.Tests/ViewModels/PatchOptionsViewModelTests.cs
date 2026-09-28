using WWOnline.Patcher.Patches;
using WWOnline.Services;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

public sealed class PatchOptionsViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-patchopts-" + Guid.NewGuid().ToString("N"));
    private readonly GameSettingsService _settings;

    public PatchOptionsViewModelTests() => _settings = new GameSettingsService(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static OptionalPatch Patch(string id, string category, bool on = false, bool match = false, string[]? conflicts = null) =>
        new(id, "Patch " + id, "Does " + id, category, on, match, "", "test", conflicts ?? [], [], [], [], id + ".asm");

    /// <summary>a (default on), b ⟂ c, d (all players should match).</summary>
    private static readonly OptionalPatchCatalog Catalog = new(
    [
        Patch("a", "Sailing", on: true),
        Patch("b", "Sailing", conflicts: ["c"]),
        Patch("c", "Sailing"),
        Patch("d", "Text", match: true),
    ]);

    /// <summary>Counts build checks and can hold them until released.</summary>
    private sealed class FakeCatalogService(GameSettingsService settings, OptionalPatchCatalog catalog)
        : OptionalPatchCatalogService(settings, catalog)
    {
        private int _checks;
        public int Checks => Volatile.Read(ref _checks);
        public ManualResetEventSlim Gate { get; } = new(initialState: true);
        public OptionalPatchBuildStatus Result { get; set; } = new(false, "up to date", []);
        public string? LastPath { get; private set; }
        public IReadOnlyList<string>? LastDesired { get; private set; }

        public override OptionalPatchBuildStatus CheckGameBuild(string gamePath, IReadOnlyList<string> desired)
        {
            Interlocked.Increment(ref _checks);
            Gate.Wait(TimeSpan.FromSeconds(10));
            LastPath = gamePath;
            LastDesired = desired;
            return Result;
        }
    }

    private FakeCatalogService Service(OptionalPatchCatalog? catalog = null) => new(_settings, catalog ?? Catalog);

    private PatchOptionsViewModel Vm(FakeCatalogService service, TimeSpan? debounce = null, GamePatcherService? patcher = null) =>
        new(service, _settings, patcher, a => a(), debounce ?? TimeSpan.Zero);

    private PatchOptionItemViewModel Item(PatchOptionsViewModel vm, string id) => vm.Options.Single(o => o.Id == id);

    private void SaveSelection(params string[]? ids)
    {
        var s = _settings.Load();
        s.OptionalPatches = ids?.ToList();
        _settings.Save(s);
    }

    [Fact]
    public async Task NothingSaved_ShowsDefaults_GroupedByCategory()
    {
        using var vm = Vm(Service());
        await vm.BuildStatusRefresh;

        Assert.Equal(new[] { "a" }, vm.SelectedIds);
        Assert.Equal(new[] { "Sailing", "Text" }, vm.Groups.Select(g => g.Category));
        Assert.Equal("1 of 4 optional patches selected", vm.SelectionSummary);
        Assert.Equal("Patch c", Item(vm, "b").ConflictsText);
        Assert.True(Item(vm, "d").AllPlayersShouldMatch);
        Assert.Null(_settings.Load().OptionalPatches); // showing isn't choosing
    }

    [Fact]
    public async Task Ticking_SavesAtOnce_AndUnticksConflicts()
    {
        using var vm = Vm(Service());
        Item(vm, "b").IsSelected = true;
        Assert.Equal(new[] { "a", "b" }, _settings.Load().OptionalPatches);

        Item(vm, "c").IsSelected = true; // newest pick wins
        Assert.False(Item(vm, "b").IsSelected);
        Assert.Equal(new[] { "a", "c" }, _settings.Load().OptionalPatches);
        await vm.BuildStatusRefresh;
    }

    [Fact]
    public async Task SavedConflictingSelection_KeepsNewestPick_AndDoesNotBlockLaterSaves()
    {
        SaveSelection("c", "b"); // hand-edited: b is the later (newer) pick
        var service = Service();
        Assert.Equal(new[] { "b" }, service.GetSelectedIds());

        using var vm = Vm(service);
        Assert.True(Item(vm, "b").IsSelected);
        Assert.False(Item(vm, "c").IsSelected);

        Item(vm, "d").IsSelected = true;
        Assert.Equal(new[] { "b", "d" }, _settings.Load().OptionalPatches);

        // The service settles a conflicting save itself too, instead of throwing.
        Assert.Equal(new[] { "a", "c" }, service.SaveSelectedIds(new[] { "b", "a", "c" }));
        Assert.Equal(new[] { "a", "c" }, _settings.Load().OptionalPatches);
        await vm.BuildStatusRefresh;
    }

    [Fact]
    public async Task SelectNone_WithEmptyCatalogue_DoesNotSave()
    {
        using var vm = Vm(Service(OptionalPatchCatalog.Empty));
        Assert.False(vm.HasOptions);

        vm.SelectNoneCommand.Execute(null);

        Assert.Null(_settings.Load().OptionalPatches); // still "never chosen" = defaults
        await vm.BuildStatusRefresh;
    }

    [Fact]
    public async Task SelectNone_ThenReset_RoundTrips()
    {
        using var vm = Vm(Service());
        vm.SelectNoneCommand.Execute(null);
        Assert.Empty(vm.SelectedIds);
        Assert.Empty(Assert.IsType<List<string>>(_settings.Load().OptionalPatches));

        vm.ResetToDefaultsCommand.Execute(null);
        Assert.Equal(new[] { "a" }, vm.SelectedIds);
        Assert.Null(_settings.Load().OptionalPatches);
        await vm.BuildStatusRefresh;
    }

    [Fact]
    public async Task BurstOfToggles_ChecksTheBuildOnce_WithTheFinalSelection()
    {
        var service = Service();
        using var vm = Vm(service, debounce: TimeSpan.FromMilliseconds(200));
        for (int i = 0; i < 5; i++) Item(vm, "d").IsSelected = !Item(vm, "d").IsSelected;
        Item(vm, "b").IsSelected = true;
        Assert.True(vm.IsCheckingBuild);

        await vm.BuildStatusRefresh;

        Assert.Equal(1, service.Checks); // the constructor's check and each toggle's were superseded
        Assert.Equal(new[] { "a", "b", "d" }, service.LastDesired);
        Assert.False(vm.IsCheckingBuild);
    }

    [Fact]
    public async Task BuildCheck_RunsOffTheCallingThread()
    {
        var service = Service();
        using var vm = Vm(service);
        await vm.BuildStatusRefresh;
        service.Gate.Reset();

        // With the check blocked, toggling still returns at once.
        Item(vm, "c").IsSelected = true;
        Assert.True(vm.IsCheckingBuild);

        service.Result = new OptionalPatchBuildStatus(true, "built with another selection", []);
        service.Gate.Set();
        await vm.BuildStatusRefresh;

        Assert.True(vm.NeedsRepatch);
        Assert.Equal("built with another selection", vm.BuildStatusText);
        Assert.False(vm.IsUpToDate);
        Assert.False(vm.IsCheckingBuild);
    }

    [Fact]
    public async Task GamePath_FromTheSettingsField_IsWhatGetsChecked()
    {
        var s = _settings.Load();
        s.GamePath = @"C:\saved";
        _settings.Save(s);
        var service = Service();
        using var vm = Vm(service);
        await vm.BuildStatusRefresh;
        Assert.Equal(@"C:\saved", service.LastPath);
        Assert.True(vm.IsUpToDate);

        vm.SetGamePath(@"C:\typed");
        await vm.BuildStatusRefresh;
        Assert.Equal(@"C:\typed", service.LastPath);
    }

    [Fact]
    public async Task FinishedPatch_RefreshesTheBuildStatus()
    {
        var service = Service();
        var patcher = new GamePatcherService(service);
        using var vm = Vm(service, debounce: TimeSpan.FromMinutes(10), patcher: patcher);
        Assert.Equal(0, service.Checks);

        // Fails validation (no vanilla game) but still ends a patch run.
        var error = await patcher.PatchGameAsync(Path.Combine(_dir, "no-vanilla"), Path.Combine(_dir, "out"));
        Assert.NotNull(error);
        await vm.BuildStatusRefresh;

        Assert.Equal(1, service.Checks);
        Assert.False(vm.IsCheckingBuild);
    }
}
