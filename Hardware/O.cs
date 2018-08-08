using System;
using System.Collections.Generic;
using System.Threading;
using heliomaster_wpf.Properties;
using Renci.SshNet;

namespace heliomaster_wpf {
    public class Observatory : BaseNotify {
        public List<BaseCamera> Cams      { get; } = new List<BaseCamera>();
        public Telescope        Mount     { get; } = S.Mount.Telescope;
        public Dome             Dome      { get; } = new Dome();
        public Weather          Weather   { get; } = new Weather();
        public Remote           Remote    { get; } = new Remote();

        public bool ConnectRemote() {
            return S.Remote.LoginMethod == RemoteLoginMethods.UserPass
                ? Remote.Init(S.Remote.Host, S.Remote.User, S.Remote.Pass, S.Remote.Port)
                : Remote.Init(S.Remote.Host, S.Remote.User, new PrivateKeyFile(S.Remote.PrivateKeyFilename),
                              S.Remote.Port);
        }
    }

    public static class O {
        public static readonly Observatory Default = new Observatory();

        public static List<BaseCamera> Cams      => Default.Cams;
        public static Telescope        Mount     => Default.Mount;
        public static Dome             Dome      => Default.Dome;
        public static Weather          Weather   => Default.Weather;
        public static Remote           Remote    => Default.Remote;

        public static event Action Refresh;
        public static void OnRefresh(object o) { Refresh?.Invoke(); }

        private static Timer RefreshTimer;
        private static bool refreshing;
        public static void StartRefresh(TimeSpan dt) {
            if (refreshing) StopRefresh();
            RefreshTimer = new Timer(OnRefresh, null, TimeSpan.Zero, dt);
            refreshing = true;
        }

        public static void StopRefresh() {
            RefreshTimer?.Dispose();
            RefreshTimer = null;
            refreshing = false;
        }
    }
}
