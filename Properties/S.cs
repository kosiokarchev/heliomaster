using System.Configuration;

namespace heliomaster.Properties {
    /// <summary>
    /// A static class that allows shortcut access to all application settings and saving them simultaneously.
    /// </summary>
    public static class S {
        public static readonly Settings        Settings = Settings.Default;
        public static readonly DomeSettings    Dome     = DomeSettings.Default;
        public static readonly MountSettings   Mount    = MountSettings.Default;
        public static readonly WeatherSettings Weather  = WeatherSettings.Default;
        public static readonly CameraSettings  Cameras  = CameraSettings.Default;
        public static readonly RemoteSettings  Remote   = RemoteSettings.Default;
        public static readonly PowerSettings   Power    = PowerSettings.Default;
        public static readonly PythonSettings  Python   = PythonSettings.Default;
        public static readonly LoggingSettings Logging  = LoggingSettings.Default;

        /// <summary>
        /// A collection of the settings instances. Useful for looping over them in <see cref="Save"/>
        /// </summary>
        private static readonly ApplicationSettingsBase[] settings = {
            Settings, Dome, Mount, Weather, Cameras, Remote, Power, Python, Logging
        };

        /// <summary>
        /// Saves all settings listed in <see cref="settings"/> after transferring the default weather settings from
        /// <see cref="O.Weather"/> into the <see cref="WeatherSettings"/> instance.
        /// </summary>
        public static void Save() {
            O.Weather.SaveInSettings(Weather);
            foreach (var s in settings)
                s.Save();
        }
    }
}
