using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.ViewModels;

/// <summary>A tunic colour preset: the RGB sent to the game and the swatch shown in the UI.</summary>
public record TunicColorPreset(string Name, byte R, byte G, byte B, string HexDisplay);

/// <summary>One colour swatch on the Appearance page.</summary>
public partial class TunicSwatchViewModel : ObservableObject
{
    public TunicSwatchViewModel(TunicColorPreset preset)
    {
        Preset = preset;
        Brush = TunicColors.FromHex(preset.HexDisplay);
        CheckBrush = TunicColors.ForegroundFor(Brush);
    }

    public TunicColorPreset Preset { get; }
    public string Name => Preset.Name;
    public IBrush Brush { get; }

    /// <summary>Tick colour that reads on this swatch.</summary>
    public IBrush CheckBrush { get; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary>One of the three outfit choices.</summary>
public partial class ClothesChoiceViewModel : ObservableObject
{
    public ClothesChoiceViewModel(int index, string title, string description)
    {
        Index = index;
        Title = title;
        Description = description;
    }

    /// <summary>Position in <see cref="AppearanceViewModel.ClothesChoices"/> (= SelectedClothesIndex).</summary>
    public int Index { get; }
    public string Title { get; }
    public string Description { get; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary>
/// The local player's look (Appearance page): clothes and tunic colour, plus whether the other
/// players' names show above their Links. The single owner of that state: a pick is published at
/// once (PuppetSyncService.LocalAppearance / ShowPlayerNames, which the sync loop sends to the game
/// and, for the look, to peers) and saved to GameSettings right away.
/// </summary>
public partial class AppearanceViewModel : ViewModelBase
{
    public const int ClothesGameDefault = 0;
    public const int ClothesHero = 1;
    public const int ClothesCasual = 2;

    /// <summary>What the casual outfit shows in the preview (its blue shirt; never recoloured).</summary>
    private static readonly IBrush PajamaBrush = new SolidColorBrush(Color.Parse("#4A7BC8"));

    private readonly GameSettingsService _settings;
    private readonly Action<AppearanceState> _publish;
    private readonly Action<bool>? _publishShowNames;
    private bool _loading;

    // Clothes are a 3-way choice exposed as an index:
    //   index 0 = Game default (follow the save), 1 = Hero's tunic, 2 = Pajamas.
    // The wire value (GameSettings.ClothesType / AppearanceState) is APPEARANCE_CLOTHES_*:
    //   0 = hero, 1 = casual, 2 = game default.
    public ReadOnlyCollection<ClothesChoiceViewModel> ClothesChoices { get; } = new(
    [
        new(ClothesGameDefault, "Game default", "Follow your save: pajamas until you get the hero's clothes."),
        new(ClothesHero, "Hero's tunic", "Always wear the hero's clothes, in the colour below."),
        new(ClothesCasual, "Pajamas", "Always wear your Outset Island pajamas."),
    ]);

    // Colours are the main tunic colour (the REL recolours the green of the hero tunic, keeping its
    // shading). "Default" is the vanilla texture: its RGB is the shared TUNIC_COLOR_DEFAULT_* marker
    // (older settings called it "Green", which FindColorIndex maps back to index 0).
    public ReadOnlyCollection<TunicColorPreset> ColorPresets { get; } = new(
    [
        new("Default", PuppetLayout.TUNIC_COLOR_DEFAULT_R, PuppetLayout.TUNIC_COLOR_DEFAULT_G,
            PuppetLayout.TUNIC_COLOR_DEFAULT_B, TunicColors.DefaultHex),
        new("Red",    180, 30,  30,  "#B41E1E"),
        new("Blue",   30,  60,  180, "#1E3CB4"),
        new("Purple", 120, 30,  160, "#781EA0"),
        new("Black",  20,  20,  20,  "#141414"),
        new("White",  230, 230, 230, "#E6E6E6"),
        new("Gold",   200, 170, 40,  "#C8AA28"),
        new("Orange", 210, 120, 30,  "#D2781E"),
        new("Pink",   220, 100, 150, "#DC6496"),
        new("Teal",   30,  160, 150, "#1EA096"),
    ]);

    public ReadOnlyCollection<TunicSwatchViewModel> Swatches { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedColor), nameof(PreviewBrush), nameof(LookText), nameof(ColorNote))]
    private int _selectedColorIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBrush), nameof(LookText), nameof(ColorNote), nameof(IsCasual))]
    private int _selectedClothesIndex = ClothesHero;

    /// <summary>"Show player names": the other players' names above their Links, in game. Live.</summary>
    [ObservableProperty]
    private bool _showPlayerNames = true;

    public TunicColorPreset SelectedColor => ColorPresets[SelectedColorIndex];

    public bool IsCasual => SelectedClothesIndex == ClothesCasual;

    /// <summary>Preview fill: the tunic colour, or the pajama blue when always in pajamas.</summary>
    public IBrush PreviewBrush => IsCasual ? PajamaBrush : Swatches[SelectedColorIndex].Brush;

    /// <summary>"Hero's tunic · Red", "Pajamas", "Game default · Teal tunic".</summary>
    public string LookText => SelectedClothesIndex switch
    {
        ClothesCasual => "Pajamas",
        ClothesGameDefault => $"Game default · {SelectedColor.Name} tunic",
        _ => $"Hero's tunic · {SelectedColor.Name}",
    };

    /// <summary>When the colour shows, for the current outfit choice.</summary>
    public string ColorNote => SelectedClothesIndex switch
    {
        ClothesCasual => "You're always in pajamas, so the tunic colour won't show. Pick Hero's tunic or Game default to wear it.",
        ClothesGameDefault => "The colour shows whenever your save has you in the hero's clothes.",
        _ => "Everyone in your room sees this colour on your Link.",
    };

    public AppearanceViewModel(GameSettingsService settings, PuppetSyncService puppetSync)
        : this(settings, look => puppetSync.LocalAppearance = look, show => puppetSync.ShowPlayerNames = show)
    {
    }

    /// <param name="settings">Where the choice is loaded from and saved to.</param>
    /// <param name="publish">Receives every look (PuppetSyncService.LocalAppearance in the app).</param>
    /// <param name="publishShowNames">Receives every "Show player names" value (PuppetSyncService.ShowPlayerNames in the app).</param>
    public AppearanceViewModel(GameSettingsService settings, Action<AppearanceState> publish, Action<bool>? publishShowNames = null)
    {
        _settings = settings;
        _publish = publish;
        _publishShowNames = publishShowNames;
        Swatches = new ReadOnlyCollection<TunicSwatchViewModel>(ColorPresets.Select(p => new TunicSwatchViewModel(p)).ToList());

        var saved = settings.Load();
        _loading = true;
        SelectedClothesIndex = ClothesTypeToIndex(saved.ClothesType);
        SelectedColorIndex = FindColorIndex(saved.TunicColorName);
        ShowPlayerNames = saved.ShowPlayerNames;
        _loading = false;

        UpdateSelectionFlags();
        Publish(save: false);
        _publishShowNames?.Invoke(ShowPlayerNames);
    }

    /// <summary>What goes to the game and to peers.</summary>
    public AppearanceState CurrentLook => new()
    {
        ClothesType = IndexToClothesType(SelectedClothesIndex),
        ColorR = SelectedColor.R,
        ColorG = SelectedColor.G,
        ColorB = SelectedColor.B,
    };

    private static int ClothesTypeToIndex(byte value) => value switch
    {
        PuppetLayout.APPEARANCE_CLOTHES_DEFAULT => ClothesGameDefault,
        PuppetLayout.APPEARANCE_CLOTHES_CASUAL => ClothesCasual,
        _ => ClothesHero,
    };

    private static byte IndexToClothesType(int index) => index switch
    {
        ClothesGameDefault => (byte)PuppetLayout.APPEARANCE_CLOTHES_DEFAULT,
        ClothesCasual => (byte)PuppetLayout.APPEARANCE_CLOTHES_CASUAL,
        _ => (byte)PuppetLayout.APPEARANCE_CLOTHES_HERO,
    };

    partial void OnSelectedColorIndexChanged(int value) => OnLookChanged();

    partial void OnSelectedClothesIndexChanged(int value) => OnLookChanged();

    private void OnLookChanged()
    {
        if (_loading) return;
        UpdateSelectionFlags();
        Publish(save: true);
    }

    // Live like the look: the sync loop publishes it on its next tick. Saved right away.
    partial void OnShowPlayerNamesChanged(bool value)
    {
        if (_loading) return;
        _publishShowNames?.Invoke(value);
        var settings = _settings.Load();
        settings.ShowPlayerNames = value;
        _settings.Save(settings);
    }

    private void UpdateSelectionFlags()
    {
        for (int i = 0; i < Swatches.Count; i++) Swatches[i].IsSelected = i == SelectedColorIndex;
        foreach (var c in ClothesChoices) c.IsSelected = c.Index == SelectedClothesIndex;
    }

    // Live: the sync service publishes it to the game (local Link) and to peers on its next tick.
    // Persisted right away too: it's a personal preference, independent of the Settings page's Save.
    private void Publish(bool save)
    {
        var look = CurrentLook;
        _publish(look);
        if (!save) return;

        var settings = _settings.Load();
        settings.ClothesType = look.ClothesType;
        settings.TunicColorName = SelectedColor.Name;
        settings.TunicColorR = look.ColorR;
        settings.TunicColorG = look.ColorG;
        settings.TunicColorB = look.ColorB;
        _settings.Save(settings);
    }

    private int FindColorIndex(string? name)
    {
        for (int i = 0; i < ColorPresets.Count; i++)
        {
            if (string.Equals(ColorPresets[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0; // Default (also the old "Green" name)
    }

    [RelayCommand]
    private void SelectColor(TunicSwatchViewModel? swatch)
    {
        if (swatch == null) return;
        int idx = Swatches.IndexOf(swatch);
        if (idx >= 0) SelectedColorIndex = idx;
    }

    [RelayCommand]
    private void SelectClothes(ClothesChoiceViewModel? choice)
    {
        if (choice != null) SelectedClothesIndex = choice.Index;
    }
}
