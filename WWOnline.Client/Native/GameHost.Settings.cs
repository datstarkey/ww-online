using System.Text;

namespace WWOnline.Native;

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwSettingsApi
    {
        public string SettingsCatalogue()
        {
            uint need = TwwNative.tww_settings_catalogue(null, 0);
            var buffer = new byte[need];
            fixed (byte* p = buffer) TwwNative.tww_settings_catalogue(p, need);
            return Decode(buffer);
        }

        public string? GetSetting(string key)
        {
            uint need = TwwNative.tww_settings_get(key, null, 0);
            if (need == 0) return null;
            var buffer = new byte[need];
            fixed (byte* p = buffer) TwwNative.tww_settings_get(key, p, need);
            return Decode(buffer);
        }

        public bool SetSetting(string key, string value) => TwwNative.tww_settings_set(key, value) == 1;

        // The bytes before the NUL.
        private static string Decode(byte[] buffer) => Encoding.UTF8.GetString(buffer, 0, Math.Max(0, buffer.Length - 1));
    }
}
