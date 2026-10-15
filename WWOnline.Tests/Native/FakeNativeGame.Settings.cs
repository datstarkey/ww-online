using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The settings half of tww.h: a settable catalogue, and the values set through it.</summary>
internal sealed partial class FakeNativeGame : ITwwSettingsApi
{
    public GameSettingsCatalog SettingsCatalog = new("handheld", "user/settings.ini", []);
    public List<(string Key, string Value)> SettingsSet { get; } = [];

    public string SettingsCatalogue() => SettingsCatalog.ToJson();

    public string? GetSetting(string key) => SettingsCatalog.Settings.FirstOrDefault(s => s.Key == key)?.Value;

    public bool SetSetting(string key, string value)
    {
        var setting = SettingsCatalog.Settings.FirstOrDefault(s => s.Key == key);
        if (setting == null || setting.Locked || setting.Choices.All(c => c.Value != value)) return false;
        SettingsSet.Add((key, value));
        SettingsCatalog = SettingsCatalog with
        {
            Settings = SettingsCatalog.Settings
                .Select(s => s.Key == key ? s with { Value = value, RestartPending = s.Restart } : s).ToList(),
        };
        return true;
    }
}
