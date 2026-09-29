using WWOnline.Patcher.Patches;
using WWOnline.Services;
using WWOnline.Tests.Services;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

public sealed class SetupWizardViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-setup-" + Guid.NewGuid().ToString("N"));
    private readonly GameSettingsService _settings;
    private readonly FakeCatalog _catalog;
    private readonly FakeProcessRunner _runner = new();
    private readonly List<(string Vanilla, string Game)> _patchCalls = [];
    private string? _patchResult;
    private IReadOnlyList<string> _dolphins = [];

    public SetupWizardViewModelTests()
    {
        _settings = new GameSettingsService(Path.Combine(_dir, "settings"));
        _catalog = new FakeCatalog(_settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>One optional patch, "swift" (off by default).</summary>
    private static readonly OptionalPatchCatalog OnePatch = new(
    [
        new OptionalPatch("swift", "Swift Sail", "Sail faster", "Sailing", false, true, "", "test", [], [], [], [], "swift.asm"),
    ]);

    /// <summary>The catalogue above; the build check answers <see cref="Result"/>.</summary>
    private sealed class FakeCatalog(GameSettingsService settings) : OptionalPatchCatalogService(settings, OnePatch)
    {
        public OptionalPatchBuildStatus Result { get; set; } = new(true, "no build stamp", []);
        public override OptionalPatchBuildStatus CheckGameBuild(string gamePath, IReadOnlyList<string> desired) => Result;
    }

    private SetupWizardViewModel Vm()
    {
        var patchOptions = new PatchOptionsViewModel(_catalog, _settings, null, a => a(), TimeSpan.Zero);
        var appearance = new AppearanceViewModel(_settings, _ => { });
        return new SetupWizardViewModel(_settings,
            (vanilla, game, progress) =>
            {
                _patchCalls.Add((vanilla, game));
                progress?.Report("working");
                return Task.FromResult(_patchResult);
            },
            patchOptions, appearance, () => _dolphins, new DolphinTool(_runner), a => a());
    }

    private string Vanilla() => FakeGameFolder.Create(Path.Combine(_dir, "ww"));

    /// <summary>A Dolphin.exe (and, when asked, a DolphinTool.exe next to it) in a temp folder.</summary>
    private string Dolphin(bool withTool = false)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "Dolphin-x64")).FullName;
        var exe = Path.Combine(folder, "Dolphin.exe");
        File.WriteAllBytes(exe, [0]);
        if (withTool) File.WriteAllBytes(Path.Combine(folder, DolphinTool.ExeName), [0]);
        return exe;
    }

    /// <summary>Welcome → Dolphin (with a Dolphin found) → Game.</summary>
    private async Task ToGameStep(SetupWizardViewModel vm, bool withTool = false)
    {
        _dolphins = [Dolphin(withTool)];
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Dolphin, vm.Step);
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Game, vm.Step);
        await vm.ToolDetection;
    }

    [Fact]
    public void Steps_RunDolphinBeforeTheGame()
    {
        using var vm = Vm();
        Assert.Equal(
            [SetupStep.Welcome, SetupStep.Dolphin, SetupStep.Game, SetupStep.Patches, SetupStep.Player, SetupStep.Patch, SetupStep.Done],
            vm.Steps.Select(s => s.Step));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], vm.Steps.Select(s => s.Number));
        Assert.Equal(Enum.GetValues<SetupStep>(), vm.Steps.Select(s => s.Step)); // the enum's order is the step order

        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Dolphin, vm.Step);
        Assert.Equal("Step 2 of 7", vm.StepCaption);
    }

    [Fact]
    public async Task GameStep_NeedsValidFolders_AndSavesThem()
    {
        using var vm = Vm();
        await ToGameStep(vm);
        Assert.False(vm.CanGoNext);
        Assert.Null(vm.VanillaError); // nothing chosen yet: no red text

        vm.VanillaGamePath = FakeGameFolder.Create(Path.Combine(_dir, "pal"), "GZLP01");
        Assert.Contains("European", vm.VanillaError);
        Assert.False(vm.CanGoNext);

        vm.VanillaGamePath = Vanilla();
        Assert.Null(vm.VanillaError);
        Assert.Contains("GZLE01", vm.DetectedGameText);
        Assert.False(vm.CanGoNext); // no patched folder yet

        vm.GamePath = vm.VanillaGamePath;
        Assert.NotNull(vm.GamePathError);
        Assert.False(vm.CanGoNext);

        vm.GamePath = Path.Combine(_dir, "patched");
        Assert.True(vm.CanGoNext);

        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Patches, vm.Step);
        var saved = _settings.Load();
        Assert.Equal(vm.VanillaGamePath, saved.VanillaGamePath);
        Assert.Equal(Path.Combine(_dir, "patched"), saved.GamePath);
    }

    [Fact]
    public void GameStep_OffersTheGameFolderInsideAPickedParent_AndSuggestsAPatchedFolder()
    {
        using var vm = Vm();
        var inner = FakeGameFolder.Create(Path.Combine(_dir, "dump", "WindWaker"));
        vm.VanillaGamePath = Path.Combine(_dir, "dump");
        Assert.Equal(inner, vm.SuggestedVanillaPath);

        vm.UseSuggestedVanillaPathCommand.Execute(null);
        Assert.Equal(inner, vm.VanillaGamePath);
        Assert.Null(vm.VanillaError);
        Assert.Equal(inner + "-WWOnline", vm.GamePath);
    }

    [Fact]
    public void GameStep_ExplainsThatAnIsoMustBeExtracted()
    {
        using var vm = Vm();
        var iso = Path.Combine(_dir, "WindWaker.iso");
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(iso, [0]);
        vm.VanillaGamePath = iso;
        Assert.Contains("Extract Entire Disc", vm.VanillaError);
    }

    [Fact]
    public async Task GameStep_WithoutDolphinTool_ShowsDolphinsOwnSteps()
    {
        using var vm = Vm();
        await ToGameStep(vm, withTool: false);
        Assert.False(vm.CanExtract);
        Assert.True(vm.ShowManualExtractSteps);
        Assert.Empty(_runner.Calls); // nothing to run
    }

    [Fact]
    public async Task GameStep_DolphinToolWithoutExtract_FallsBackToDolphinsOwnSteps()
    {
        _runner.Handler = (_, _) => new ProcessResult(1, "usage: dolphin-tool COMMAND -h\ncommands supported: [convert, verify, header]");
        using var vm = Vm();
        await ToGameStep(vm, withTool: true);
        Assert.False(vm.CanExtract);
        Assert.True(vm.ShowManualExtractSteps);
        Assert.Equal(DolphinTool.HelpArguments, Assert.Single(_runner.Calls).Args);
    }

    [Fact]
    public async Task GameStep_ExtractsTheDiscImage_ThenValidatesIt()
    {
        using var vm = Vm();
        await ToGameStep(vm, withTool: true);
        Assert.True(vm.CanExtract);
        Assert.False(vm.ShowManualExtractSteps);

        var iso = Path.Combine(_dir, "roms", "The Wind Waker (USA).iso");
        Directory.CreateDirectory(Path.GetDirectoryName(iso)!);
        File.WriteAllBytes(iso, [0]);
        vm.DiscImagePath = iso;
        var expectedFolder = Path.Combine(_dir, "roms", "The Wind Waker (USA)");
        Assert.Equal(expectedFolder, vm.ExtractFolder); // suggested beside the image
        Assert.True(vm.CanExtractNow);

        // "Extract": the fake tool writes a GameCube layout (sys + files) into -o.
        _runner.Handler = (_, args) =>
        {
            if (args[0] != "extract" || args.Contains("--help")) return FakeProcessRunner.ExtractHelp;
            FakeGameFolder.Create(args[args.IndexOf("-o") + 1]);
            return new ProcessResult(0, "Extracting sys/main.dol\nExtracting files/RELS.arc");
        };
        await vm.ExtractDiscCommand.ExecuteAsync(null);

        Assert.Null(vm.ExtractError);
        Assert.True(vm.ExtractSucceeded);
        var call = _runner.Calls[^1];
        Assert.EndsWith(DolphinTool.ExeName, call.Exe);
        Assert.Equal(["extract", "-i", iso, "-o", expectedFolder], call.Args);
        Assert.Equal(expectedFolder, vm.VanillaGamePath);
        Assert.Contains("GZLE01", vm.DetectedGameText);
        Assert.Equal(expectedFolder + "-WWOnline", vm.GamePath);
        Assert.True(vm.CanGoNext);
    }

    [Fact]
    public async Task GameStep_ExtractedPalDisc_IsRefusedLikeAPickedFolder()
    {
        using var vm = Vm();
        await ToGameStep(vm, withTool: true);
        _runner.Handler = (_, args) =>
        {
            if (args.Contains("--help")) return FakeProcessRunner.ExtractHelp;
            FakeGameFolder.Create(args[args.IndexOf("-o") + 1], "GZLP01");
            return new ProcessResult(0, "");
        };
        var iso = Path.Combine(_dir, "pal.rvz");
        File.WriteAllBytes(iso, [0]);
        vm.DiscImagePath = iso;
        await vm.ExtractDiscCommand.ExecuteAsync(null);

        Assert.Contains("European", vm.VanillaError);
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public async Task GameStep_ExtractFailure_IsShown_AndNothingIsChosen()
    {
        using var vm = Vm();
        await ToGameStep(vm, withTool: true);
        _runner.Handler = (_, args) => args.Contains("--help")
            ? FakeProcessRunner.ExtractHelp
            : new ProcessResult(1, "Error: Unable to open disc image");
        var iso = Path.Combine(_dir, "broken.iso");
        File.WriteAllBytes(iso, [0]);
        vm.DiscImagePath = iso;
        await vm.ExtractDiscCommand.ExecuteAsync(null);

        Assert.Contains("Unable to open disc image", vm.ExtractError);
        Assert.False(vm.ExtractSucceeded);
        Assert.Equal("", vm.VanillaGamePath);
        Assert.False(vm.IsExtracting);
    }

    [Fact]
    public async Task GameStep_ExtractRefusesANonEmptyFolder_WithoutRunningTheTool()
    {
        using var vm = Vm();
        await ToGameStep(vm, withTool: true);
        var iso = Path.Combine(_dir, "ww.iso");
        File.WriteAllBytes(iso, [0]);
        var busy = Directory.CreateDirectory(Path.Combine(_dir, "busy")).FullName;
        File.WriteAllText(Path.Combine(busy, "notes.txt"), "hi");
        vm.DiscImagePath = iso;
        vm.ExtractFolder = busy;
        int calls = _runner.Calls.Count;

        await vm.ExtractDiscCommand.ExecuteAsync(null);
        Assert.Contains("isn't empty", vm.ExtractError);
        Assert.Equal(calls, _runner.Calls.Count);
    }

    [Fact]
    public void DolphinStep_PicksAFoundDolphin_AndCanBeSkipped()
    {
        var exe = Dolphin();
        _dolphins = [exe];
        using var vm = Vm();
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Dolphin, vm.Step);

        Assert.Equal(exe, vm.DolphinPath); // found and filled in on entering the step
        Assert.True(vm.CanGoNext);

        vm.DolphinPath = Path.Combine(_dir, "nope.exe");
        Assert.False(vm.CanGoNext);
        Assert.True(vm.CanSkipStep);

        vm.AutoLaunchDolphin = false;
        vm.SkipStepCommand.Execute(null);
        Assert.Equal(SetupStep.Game, vm.Step);
        var saved = _settings.Load();
        Assert.Equal("", saved.DolphinPath); // skipped: the bad path isn't saved
        Assert.False(saved.AutoLaunchDolphin);
    }

    [Fact]
    public void DolphinStep_SavesAValidPath()
    {
        var exe = Dolphin();
        using var vm = Vm();
        vm.NextCommand.Execute(null);
        Assert.True(vm.NoDolphinFound);
        Assert.False(vm.CanGoNext);

        vm.UseDolphinCandidateCommand.Execute(exe);
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Game, vm.Step);
        Assert.Equal(exe, _settings.Load().DolphinPath);
    }

    /// <summary>Welcome → Dolphin → Game (valid folders) → Patches → Player.</summary>
    private async Task ToPlayerStep(SetupWizardViewModel vm)
    {
        await ToGameStep(vm);
        vm.VanillaGamePath = Vanilla();
        vm.GamePath = Path.Combine(_dir, "patched");
        vm.NextCommand.Execute(null); // Game → Patches
        vm.NextCommand.Execute(null); // Patches → Player
        Assert.Equal(SetupStep.Player, vm.Step);
    }

    [Fact]
    public async Task PlayerStep_NeedsAName_AndSavesIt()
    {
        using var vm = Vm();
        await ToPlayerStep(vm);

        vm.PlayerName = "   ";
        Assert.False(vm.CanGoNext);
        Assert.NotNull(vm.PlayerNameError);

        vm.PlayerName = "  Tetra ";
        Assert.True(vm.CanGoNext);
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Patch, vm.Step);
        Assert.Equal("Tetra", _settings.Load().PlayerName);
    }

    private async Task<SetupWizardViewModel> AtPatchStep()
    {
        var vm = Vm();
        await ToPlayerStep(vm);
        vm.PlayerName = "Link";
        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Patch, vm.Step);
        await vm.PatchOptions.BuildStatusRefresh;
        return vm;
    }

    [Fact]
    public async Task PatchStep_WaitsForASuccessfulPatch()
    {
        using var vm = await AtPatchStep();
        Assert.False(vm.CanGoNext); // not patched yet
        Assert.True(vm.CanSkipStep);

        _patchResult = "RELS.arc not found";
        await vm.PatchGameCommand.ExecuteAsync(null);
        Assert.Equal("RELS.arc not found", vm.PatchError);
        Assert.False(vm.CanGoNext);
        Assert.Equal("Try again", vm.PatchButtonText);

        _patchResult = null;
        await vm.PatchGameCommand.ExecuteAsync(null);
        Assert.True(vm.PatchSucceeded);
        Assert.Null(vm.PatchError);
        Assert.True(vm.CanGoNext);
        Assert.False(vm.CanSkipStep);
        Assert.Equal(2, _patchCalls.Count);
        Assert.Equal((vm.VanillaGamePath, vm.GamePath), _patchCalls[^1]);

        vm.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Done, vm.Step);
        Assert.True(vm.IsGameReady);
    }

    [Fact]
    public async Task PatchStep_AnUpToDateGame_NeedsNoPatch()
    {
        _catalog.Result = new OptionalPatchBuildStatus(false, "game build is up to date", []);
        using var vm = await AtPatchStep();

        Assert.True(vm.ShowAlreadyUpToDate);
        Assert.True(vm.CanGoNext);
        Assert.Empty(_patchCalls);
    }

    [Fact]
    public async Task PatchLater_ReachesTheEnd_WithTheNotPatchedWarning()
    {
        using var vm = await AtPatchStep();
        vm.SkipStepCommand.Execute(null);
        Assert.Equal(SetupStep.Done, vm.Step);
        Assert.False(vm.IsGameReady);
        Assert.Empty(_patchCalls);
    }

    [Fact]
    public async Task Finishing_MarksSetupCompleted_AndCloses()
    {
        using var vm = await AtPatchStep();
        vm.SkipStepCommand.Execute(null);
        int closed = 0;
        vm.Closed += () => closed++;

        Assert.False(_settings.Load().SetupCompleted);
        vm.NextCommand.Execute(null); // Finish
        Assert.Equal(1, closed);
        Assert.True(_settings.Load().SetupCompleted);
    }

    [Fact]
    public void SkippingSetup_AlsoCountsAsCompleted()
    {
        using var vm = Vm();
        int closed = 0;
        vm.Closed += () => closed++;
        vm.SkipSetupCommand.Execute(null);
        Assert.Equal(1, closed);
        Assert.True(_settings.Load().SetupCompleted);
    }

    [Fact]
    public async Task Back_ReturnsToThePreviousStep_AndOpenStartsOverWithSavedValues()
    {
        using var vm = Vm();
        await ToGameStep(vm);
        vm.BackCommand.Execute(null);
        Assert.Equal(SetupStep.Dolphin, vm.Step);
        Assert.True(vm.Steps[0].IsDone);
        Assert.True(vm.Steps[1].IsCurrent);

        var saved = _settings.Load().DolphinPath;
        vm.DolphinPath = "";
        vm.Open();
        Assert.Equal(SetupStep.Welcome, vm.Step);
        Assert.Equal(saved, vm.DolphinPath);
    }

    [Fact]
    public void ChoosingPatchesInSetup_SavesTheSameSelectionAsSettings()
    {
        using var vm = Vm();
        var swift = Assert.Single(vm.PatchOptions.Options);
        Assert.False(swift.IsSelected);
        swift.IsSelected = true;
        Assert.Equal(new[] { "swift" }, _settings.Load().OptionalPatches);
        Assert.Equal(new[] { "swift" }, _catalog.GetSelectedIds()); // what Settings and Patch game use
    }
}
