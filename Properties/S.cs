using System.Configuration;

namespace heliomaster_wpf.Properties {
    public static class S {
        public static readonly Settings        Settings = Settings.Default;
        public static readonly DomeSettings    Dome     = DomeSettings.Default;
        public static readonly MountSettings   Mount    = MountSettings.Default;
        public static readonly WeatherSettings Weather  = WeatherSettings.Default;
        public static readonly CameraSettings  Cameras  = CameraSettings.Default;
        public static readonly RemoteSettings  Remote   = RemoteSettings.Default;
        public static readonly PowerSettings   Power    = PowerSettings.Default;
        public static readonly PythonSettings  Python   = PythonSettings.Default;

        private static readonly ApplicationSettingsBase[] settings = {
            Settings, Dome, Mount, Weather, Cameras, Remote, Power, Python
        };

        public static void Save() {
            foreach (var s in settings)
                s.Save();
        }
    }
}
