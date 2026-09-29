using WWOnline.Data;

namespace WWOnline.Services;

public class GameSettings
{
    public string DolphinPath { get; set; } = "";
    public string GamePath { get; set; } = "";
    public string VanillaGamePath { get; set; } = "";

    /// <summary>Start Dolphin with the patched game and attach to it after hosting or joining a room
    /// (GameLaunchService). Only ever happens while the patched game is up to date.</summary>
    public bool AutoLaunchDolphin { get; set; } = true;

    /// <summary>
    /// The first-run setup has been finished or skipped (FirstRun). False in settings files from before
    /// the setup existed: FirstRun marks those done when they already hold a working setup.
    /// </summary>
    public bool SetupCompleted { get; set; }
    public string PlayerName { get; set; } = "";

    // Appearance (puppet_shared.h APPEARANCE_CLOTHES_* / TUNIC_COLOR_DEFAULT_*)
    public byte ClothesType { get; set; } = PuppetLayout.APPEARANCE_CLOTHES_DEFAULT; // 0 = hero, 1 = casual, 2 = follow the save
    public string TunicColorName { get; set; } = "Default";
    public byte TunicColorR { get; set; } = PuppetLayout.TUNIC_COLOR_DEFAULT_R;      // Default = vanilla tunic
    public byte TunicColorG { get; set; } = PuppetLayout.TUNIC_COLOR_DEFAULT_G;
    public byte TunicColorB { get; set; } = PuppetLayout.TUNIC_COLOR_DEFAULT_B;
    /// <summary>The boat hull colour's name on the Appearance page (AppearanceViewModel.BoatPresets).</summary>
    public string BoatColorName { get; set; } = "Match tunic";

    /// <summary>Draw the other players' names above their Links (Appearance page, live).</summary>
    public bool ShowPlayerNames { get; set; } = true;

    /// <summary>
    /// Optional game patches to apply when patching (ids from GameMod/src/patches/optional/).
    /// Null = never chosen: use each patch's default. See OptionalPatchCatalogService.
    /// </summary>
    public List<string>? OptionalPatches { get; set; }
}

public class GameSettingsService
{
    private const string FileName = "game-settings.json";
    private readonly JsonSettingsStore<GameSettings> _store;

    public GameSettingsService() => _store = new JsonSettingsStore<GameSettings>(FileName);

    /// <summary>Keep game-settings.json in <paramref name="directory"/> instead of the app data folder (tests).</summary>
    public GameSettingsService(string directory) => _store = new JsonSettingsStore<GameSettings>(FileName, directory);

    public GameSettings Load() => _store.Load();
    public void Save(GameSettings settings) => _store.Save(settings);

    public string? ValidatePaths(GameSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DolphinPath))
            return "Dolphin path is required";

        if (!File.Exists(settings.DolphinPath))
            return $"Dolphin executable not found: {settings.DolphinPath}";

        if (string.IsNullOrWhiteSpace(settings.VanillaGamePath))
            return "Vanilla game path is required";

        var vanillaDol = Path.Combine(settings.VanillaGamePath, "sys", "main.dol");
        if (!File.Exists(vanillaDol))
            return $"Vanilla game not found: expected sys/main.dol in {settings.VanillaGamePath}";

        if (string.IsNullOrWhiteSpace(settings.GamePath))
            return "Patched game output path is required";

        return null;
    }

    public bool IsConfigured(GameSettings settings) => ValidatePaths(settings) == null;
}
