using System.Runtime.InteropServices;

namespace WWOnline.Native;

// tww.h "settings (fan-out: settings)": the options menu's settings as JSON, and by key. Game thread only, between
// tww_run_frame calls.
public static unsafe partial class TwwNative
{
    [LibraryImport(LibraryName)] public static partial uint tww_settings_catalogue(byte* buf, uint size);
    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial uint tww_settings_get(string key, byte* buf, uint size);
    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tww_settings_set(string key, string value);
}

/// <summary>The settings half of tww.h: reach it through <see cref="INativeGame.Invoke{T}"/> (the API object implements it).</summary>
public interface ITwwSettingsApi
{
    /// <summary>tww_settings_catalogue's JSON (<see cref="GameSettingsCatalog.Parse"/>).</summary>
    string SettingsCatalogue();
    /// <summary>The value in effect, or null for a key not in the catalogue.</summary>
    string? GetSetting(string key);
    /// <summary>Set as the in-game menu does (saved, applied). False: not in the catalogue, locked, or not a choice.</summary>
    bool SetSetting(string key, string value);
}
