using WWOnline.Native;
using WWOnline.Tests.Native;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

public sealed class GameSettingsViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-game-settings-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> _environment = [];
    private readonly FakeNativeGame _game = new() { SettingsCatalog = Catalog };

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static readonly GameSettingChoice[] OnOff = [new("0", "Off"), new("1", "On")];
    private static readonly GameSettingChoice[] Keys = [new("none", "None"), new("J", "J"), new("K", "K"), new("Space", "Space")];

    private static GameSettingInfo Setting(string key, string tab, string type, string value, GameSettingChoice[] choices,
        string group = "", bool restart = false, bool perMode = false) =>
        new(key, "Label " + key, "Help " + key, tab, tab switch
        {
            "graphics" => "Graphics",
            "qol" => "Quality of life",
            _ => "Controls",
        }, group, type, value, value, restart, perMode, Locked: false, RestartPending: false, choices);

    /// <summary>The shape of tww_settings_catalogue's answer: menu order, the bindings last (the controller's first).</summary>
    internal static readonly GameSettingsCatalog Catalog = new("handheld", "user/settings.ini",
    [
        Setting("COS_FB_SCALE", "graphics", "choice", "0", [new("0", "Window size"), new("1.5", "1280x720 (1.5)")], perMode: true),
        Setting("COS_MSAA", "graphics", "choice", "4", [new("1", "Off"), new("4", "4x")], restart: true),
        Setting("COS_QOL_INSTANT_TEXT", "qol", "bool", "0", OnOff),
        Setting("COS_CAMERA_INVERT_X", "controls", "bool", "0", OnOff),
        Setting("COS_BIND_PAD_A", "controls", "binding", "", [new("", "Controller default"), new("none", "None"), new("a", "South face button")],
            group: "controller"),
        Setting("COS_BIND_KEY_A", "controls", "binding", "J", Keys, group: "keyboard"),
        Setting("COS_BIND_KEY_B", "controls", "binding", "K", Keys, group: "keyboard"),
    ]);

    private NativeGameSettings Settings() =>
        new(_game, _dir, key => _environment.TryGetValue(key, out var v) ? v : null);

    private async Task<GameSettingsViewModel> Vm()
    {
        var vm = new GameSettingsViewModel(Settings(), a => a());
        await vm.Pending;
        return vm;
    }

    private static GameSettingItemViewModel Item(GameSettingsViewModel vm, string key) =>
        vm.Sections.SelectMany(s => s.Groups).SelectMany(g => g.Items).Single(i => i.Key == key);

    [Fact]
    public void Catalogue_ParsesTheGamesJson()
    {
        const string json = """
            {"mode":"handheld","path":"/u/settings.ini","settings":[{"key":"COS_MSAA","label":"Anti-aliasing (MSAA)",
            "help":"Smooths \"edges\".","tab":"graphics","tabLabel":"Graphics","group":"","type":"choice","default":"4",
            "value":"2","restart":true,"perMode":false,"locked":false,"restartPending":true,
            "choices":[{"value":"1","label":"Off"},{"value":"2","label":"2x"}]}]}
            """;

        var catalog = GameSettingsCatalog.Parse(json)!;

        var msaa = Assert.Single(catalog.Settings);
        Assert.Equal(("handheld", "/u/settings.ini"), (catalog.Mode, catalog.Path));
        Assert.Equal(("COS_MSAA", "Smooths \"edges\".", "choice", "4", "2"), (msaa.Key, msaa.Help, msaa.Type, msaa.Default, msaa.Value));
        Assert.True(msaa.Restart && msaa.RestartPending && !msaa.PerMode && !msaa.Locked);
        Assert.Equal([new("1", "Off"), new("2", "2x")], msaa.Choices);
        Assert.Null(GameSettingsCatalog.Parse("not json"));
        Assert.Null(GameSettingsCatalog.Parse(""));
    }

    [Fact]
    public async Task Live_ShowsTheCatalogueByTab_WithTheBindingsInControls()
    {
        var vm = await Vm();

        Assert.True(vm.HasCatalog);
        Assert.True(vm.IsLive);
        Assert.Equal(["Graphics", "Quality of life", "Controls"], vm.Sections.Select(s => s.Title));
        Assert.Equal(["Controls", "Keyboard", "Controller"], vm.Sections[2].Groups.Select(g => g.Title));
        Assert.True(Item(vm, "COS_QOL_INSTANT_TEXT").IsToggle);
        Assert.True(Item(vm, "COS_BIND_KEY_A").IsList);
        Assert.Equal("J", Item(vm, "COS_BIND_KEY_A").Selected!.Value);
        Assert.Equal("Controller default", Item(vm, "COS_BIND_PAD_A").Selected!.Label);
        Assert.True(Item(vm, "COS_MSAA").NeedsRestart);
        Assert.False(Item(vm, "COS_FB_SCALE").NeedsRestart);
        Assert.True(File.Exists(Path.Combine(_dir, NativeGameSettings.CatalogFileName)));
    }

    [Fact]
    public async Task Live_AChangeGoesThroughTheGame()
    {
        var vm = await Vm();

        Item(vm, "COS_QOL_INSTANT_TEXT").IsOn = true;
        await vm.Pending;
        Item(vm, "COS_MSAA").Selected = Item(vm, "COS_MSAA").Choices.Single(c => c.Value == "1");
        await vm.Pending;

        Assert.Equal([("COS_QOL_INSTANT_TEXT", "1"), ("COS_MSAA", "1")], _game.SettingsSet);
        Assert.True(Item(vm, "COS_QOL_INSTANT_TEXT").IsOn);
        Assert.True(Item(vm, "COS_MSAA").RestartPending);
        Assert.False(File.Exists(Path.Combine(_dir, NativeGameSettings.FileName))); // the game saves its own file
    }

    [Fact]
    public async Task Binding_AKeyAnotherInputHasSwaps()
    {
        var vm = await Vm();

        Item(vm, "COS_BIND_KEY_A").Selected = Item(vm, "COS_BIND_KEY_A").Choices.Single(c => c.Value == "K");
        await vm.Pending;

        Assert.Equal([("COS_BIND_KEY_B", "J"), ("COS_BIND_KEY_A", "K")], _game.SettingsSet);
        Assert.Equal("K", Item(vm, "COS_BIND_KEY_A").Selected!.Value);
        Assert.Equal("J", Item(vm, "COS_BIND_KEY_B").Selected!.Value);
    }

    [Fact]
    public async Task Offline_ReadsAndWritesTheSettingsFile()
    {
        var vm = await Vm(); // the game lists its settings once
        Directory.CreateDirectory(_dir);
        File.WriteAllLines(Path.Combine(_dir, NativeGameSettings.FileName), ["COS_FB_SCALE@handheld=1.5", "[dev]", "COS_TRACE=1"]);

        _game.IsRunning = false;
        await vm.Pending;
        Assert.False(vm.IsLive);
        Assert.Equal("1.5", Item(vm, "COS_FB_SCALE").Selected!.Value);
        Assert.Equal("0", Item(vm, "COS_QOL_INSTANT_TEXT").Info.Value);

        Item(vm, "COS_QOL_INSTANT_TEXT").IsOn = true;
        await vm.Pending;
        Item(vm, "COS_FB_SCALE").Selected = Item(vm, "COS_FB_SCALE").Choices.Single(c => c.Value == "0");
        await vm.Pending;

        Assert.Empty(_game.SettingsSet);
        Assert.Equal(["COS_FB_SCALE@handheld=0", "COS_QOL_INSTANT_TEXT=1", "[dev]", "COS_TRACE=1"],
            File.ReadAllLines(Path.Combine(_dir, NativeGameSettings.FileName)));
        Assert.True(Item(vm, "COS_QOL_INSTANT_TEXT").IsOn);
    }

    [Fact]
    public async Task Offline_BeforeTheFirstRunThereIsNoCatalogue()
    {
        _game.IsRunning = false;

        var vm = await Vm();

        Assert.False(vm.HasCatalog);
        Assert.Empty(vm.Sections);
        Assert.Null(vm.SelectedSection);
    }

    [Fact]
    public async Task Sections_ThePickedTabIsMarkedWhileShown_AndKeptAcrossAReload()
    {
        var vm = await Vm();
        Assert.Equal("Graphics", vm.SelectedSection!.Title);
        Assert.DoesNotContain(vm.Sections, s => s.IsSelected); // Settings shows one of its own tabs

        vm.SelectedSection = vm.Sections.Single(s => s.Title == "Controls");
        vm.IsShown = true;
        Assert.Equal(["Controls"], vm.Sections.Where(s => s.IsSelected).Select(s => s.Title));

        // A new setting rebuilds every section: the same tab stays picked.
        _game.SettingsCatalog = Catalog with { Settings = [.. Catalog.Settings, Setting("COS_BIND_KEY_X", "controls", "binding", "Space", Keys, group: "keyboard")] };
        await vm.ReloadAsync();
        Assert.Equal(3, vm.Sections.Single(s => s.Title == "Controls").Groups.Single(g => g.Title == "Keyboard").Items.Count);
        Assert.Equal(["Controls"], vm.Sections.Where(s => s.IsSelected).Select(s => s.Title));
        Assert.Same(vm.Sections.Single(s => s.Title == "Controls"), vm.SelectedSection);

        vm.IsShown = false;
        Assert.DoesNotContain(vm.Sections, s => s.IsSelected);
    }

    [Fact]
    public async Task Offline_AnEnvironmentVariableLocksItsSetting()
    {
        var vm = await Vm();
        _environment["COS_MSAA"] = "1";

        _game.IsRunning = false;
        await vm.Pending;
        var msaa = Item(vm, "COS_MSAA");
        msaa.Selected = msaa.Choices.Single(c => c.Value == "4");
        await vm.Pending;

        Assert.False(msaa.IsEditable);
        Assert.Equal("1", msaa.Info.Value);
        Assert.NotEqual("", vm.Error);
        Assert.False(File.Exists(Path.Combine(_dir, NativeGameSettings.FileName)));
    }
}
