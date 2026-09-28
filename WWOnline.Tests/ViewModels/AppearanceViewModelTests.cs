using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.ViewModels;

public sealed class AppearanceViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-appearance-" + Guid.NewGuid().ToString("N"));
    private readonly GameSettingsService _settings;
    private readonly List<AppearanceState> _published = [];

    public AppearanceViewModelTests() => _settings = new GameSettingsService(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private AppearanceViewModel Vm() => new(_settings, _published.Add);

    private static (int R, int G, int B) Rgb(AppearanceState a) => (a.ColorR, a.ColorG, a.ColorB);

    private void Save(byte clothes, string colorName)
    {
        var s = _settings.Load();
        s.ClothesType = clothes;
        s.TunicColorName = colorName;
        _settings.Save(s);
    }

    [Fact]
    public void Loads_TheSavedLook_AndPublishesIt_WithoutSaving()
    {
        Save(PuppetLayout.APPEARANCE_CLOTHES_CASUAL, "Purple");
        var file = Path.Combine(_dir, "game-settings.json");
        var before = File.GetLastWriteTimeUtc(file);

        var vm = Vm();

        Assert.Equal(AppearanceViewModel.ClothesCasual, vm.SelectedClothesIndex);
        Assert.Equal("Purple", vm.SelectedColor.Name);
        Assert.True(vm.IsCasual);
        Assert.Equal("Pajamas", vm.LookText);
        Assert.True(vm.Swatches.Single(s => s.Name == "Purple").IsSelected);
        Assert.Single(vm.Swatches, s => s.IsSelected);
        Assert.True(vm.ClothesChoices[AppearanceViewModel.ClothesCasual].IsSelected);
        Assert.Single(vm.ClothesChoices, c => c.IsSelected);

        var look = Assert.Single(_published);
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_CASUAL, look.ClothesType);
        Assert.Equal((120, 30, 160), Rgb(look));
        Assert.Equal(before, File.GetLastWriteTimeUtc(file)); // loading never writes
    }

    [Fact]
    public void Defaults_WhenNothingSaved_AndOldGreenName_MapsToDefault()
    {
        var fresh = Vm();
        Assert.Equal("Default", fresh.SelectedColor.Name);
        Assert.Equal(AppearanceViewModel.ClothesGameDefault, fresh.SelectedClothesIndex); // GameSettings default = follow the save
        Assert.False(File.Exists(Path.Combine(_dir, "game-settings.json")));

        Save(PuppetLayout.APPEARANCE_CLOTHES_HERO, "Green");
        var old = Vm();
        Assert.Equal(0, old.SelectedColorIndex);
        Assert.Equal(AppearanceViewModel.ClothesHero, old.SelectedClothesIndex);
        Assert.Equal("Hero's tunic · Default", old.LookText);
        var look = _published[^1];
        Assert.Equal((PuppetLayout.TUNIC_COLOR_DEFAULT_R, PuppetLayout.TUNIC_COLOR_DEFAULT_G, PuppetLayout.TUNIC_COLOR_DEFAULT_B),
            Rgb(look));
    }

    [Fact]
    public void PickingAColour_PublishesAndSaves_KeepingOtherSettings()
    {
        var s = _settings.Load();
        s.GamePath = @"C:\game";
        s.ClothesType = PuppetLayout.APPEARANCE_CLOTHES_HERO;
        _settings.Save(s);
        var vm = Vm();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SelectColorCommand.Execute(vm.Swatches.Single(sw => sw.Name == "Black"));

        Assert.Equal("Black", vm.SelectedColor.Name);
        Assert.Contains(nameof(AppearanceViewModel.SelectedColor), changed); // MainViewModel recolours the avatar on this
        Assert.Contains(nameof(AppearanceViewModel.PreviewBrush), changed);
        Assert.True(vm.Swatches.Single(sw => sw.Name == "Black").IsSelected);
        Assert.Single(vm.Swatches, sw => sw.IsSelected);

        var look = _published[^1];
        Assert.Equal((20, 20, 20), Rgb(look));
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_HERO, look.ClothesType);

        var saved = _settings.Load();
        Assert.Equal("Black", saved.TunicColorName);
        Assert.Equal((20, 20, 20), ((int)saved.TunicColorR, (int)saved.TunicColorG, (int)saved.TunicColorB));
        Assert.Equal(@"C:\game", saved.GamePath);
    }

    [Fact]
    public void PickingClothes_MapsToTheWireValue_AndSaves()
    {
        var vm = Vm();

        vm.SelectClothesCommand.Execute(vm.ClothesChoices[AppearanceViewModel.ClothesHero]);
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_HERO, _published[^1].ClothesType);
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_HERO, _settings.Load().ClothesType);
        Assert.False(vm.IsCasual);

        vm.SelectClothesCommand.Execute(vm.ClothesChoices[AppearanceViewModel.ClothesCasual]);
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_CASUAL, _published[^1].ClothesType);
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_CASUAL, _settings.Load().ClothesType);
        Assert.True(vm.IsCasual);
        Assert.Contains("won't show", vm.ColorNote);

        vm.SelectClothesCommand.Execute(vm.ClothesChoices[AppearanceViewModel.ClothesGameDefault]);
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_DEFAULT, _published[^1].ClothesType);
        Assert.Equal(PuppetLayout.APPEARANCE_CLOTHES_DEFAULT, _settings.Load().ClothesType);
        Assert.Single(vm.ClothesChoices, c => c.IsSelected);
    }

    [Fact]
    public void ShowPlayerNames_DefaultsOn_AndIsPublishedOnLoad()
    {
        var shown = new List<bool>();
        var vm = new AppearanceViewModel(_settings, _published.Add, shown.Add);

        Assert.True(vm.ShowPlayerNames);
        Assert.Equal(new[] { true }, shown);
        Assert.False(File.Exists(Path.Combine(_dir, "game-settings.json"))); // loading never writes
    }

    [Fact]
    public void ShowPlayerNames_Toggle_PublishesLive_AndSaves()
    {
        var shown = new List<bool>();
        var vm = new AppearanceViewModel(_settings, _published.Add, shown.Add);

        vm.ShowPlayerNames = false;

        Assert.Equal(new[] { true, false }, shown);
        Assert.False(_settings.Load().ShowPlayerNames);
        Assert.Single(_published); // the look isn't re-sent for a names toggle

        var reloaded = new AppearanceViewModel(_settings, _ => { }, shown.Add);
        Assert.False(reloaded.ShowPlayerNames);
        Assert.False(shown[^1]);
    }
}
