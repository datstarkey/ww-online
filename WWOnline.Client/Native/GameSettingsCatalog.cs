using System.Text.Json;

namespace WWOnline.Native;

/// <summary>One value a setting can take, with what the page shows for it.</summary>
public sealed record GameSettingChoice(string Value, string Label);

/// <summary>
/// One setting of the native game's options menu (tww.h, tww_settings_catalogue). Values are strings, as in
/// settings.ini; a "bool" is "0" / "1".
/// </summary>
/// <param name="Tab">"graphics", "performance", "qol", "controls" or "debug".</param>
/// <param name="Group">"keyboard" / "controller" for a binding, else "".</param>
/// <param name="Type">"bool", "choice" or "binding".</param>
/// <param name="Restart">It applies the next time the game starts.</param>
/// <param name="PerMode">One value per operation mode: the catalogue's mode is the one shown and set.</param>
/// <param name="Locked">The environment sets it: it can't be changed here.</param>
/// <param name="RestartPending">A restart setting changed since the game started.</param>
public sealed record GameSettingInfo(
    string Key,
    string Label,
    string Help,
    string Tab,
    string TabLabel,
    string Group,
    string Type,
    string Default,
    string Value,
    bool Restart,
    bool PerMode,
    bool Locked,
    bool RestartPending,
    IReadOnlyList<GameSettingChoice> Choices);

/// <summary>The native game's settings catalogue (tww_settings_catalogue's JSON).</summary>
/// <param name="Mode">The operation mode a per-mode setting's value is for: "handheld" or "docked".</param>
/// <param name="Path">The settings file the game reads and writes.</param>
public sealed record GameSettingsCatalog(string Mode, string Path, IReadOnlyList<GameSettingInfo> Settings)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The catalogue, or null when the text isn't one.</summary>
    public static GameSettingsCatalog? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var catalog = JsonSerializer.Deserialize<GameSettingsCatalog>(json, Options);
            return catalog?.Settings is { } settings && settings.All(s => s.Key != null && s.Choices != null) ? catalog : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}
