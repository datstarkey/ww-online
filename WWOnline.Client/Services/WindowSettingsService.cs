namespace WWOnline.Services;

public class WindowSettings
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 800;
    public bool IsMaximized { get; set; }
}

public class WindowSettingsService
{
    private readonly JsonSettingsStore<WindowSettings> _store = new("window-settings.json");

    public WindowSettings Load() => _store.Load();
    public void Save(WindowSettings settings) => _store.Save(settings);
}