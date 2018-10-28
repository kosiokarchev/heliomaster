using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    /// <summary>
    /// A static class that initialises and allows access to a default <see cref="Observatory"/> instance.
    /// </summary>
    public static class O {
        /// <summary>
        /// The application-default <see cref="Observatory"/> instance.
        /// </summary>
        public static readonly Observatory Default = new Observatory();

        /// <summary>
        /// Shortcut to the default telescope mount.
        /// </summary>
        public static Telescope Mount => Default.Mount;
        /// <summary>
        /// Shortcut to the default dome.
        /// </summary>
        public static Dome Dome => Default.Dome;
        /// <summary>
        /// Shortcut to the default weather controller.
        /// </summary>
        public static Weather Weather => Default.Weather;
        /// <summary>
        /// Shortcut to the default remote host controller.
        /// </summary>
        public static Remote Remote => Default.Remote;

        /// <summary>
        /// Shortcut to the default collection of camera controllers.
        /// </summary>
        public static ObservableCollection<CameraModel> CamModels => Default.CameraModels;
        /// <summary>
        /// Shortcut to the default common timelapse controller.
        /// </summary>
        public static CommonTimelapse Timelapse => Default.CommonTimelapse;

        /// <summary>
        /// Shortcut to the default power controller.
        /// </summary>
        public static BasePower Power => Default.Power;

        /// <summary>
        /// Shortcut to the correct weather controller identifier, taking into account whether the user has selected to
        /// use a <see cref="WeatherFromFile"/>.
        /// </summary>
        public static string WeatherID => S.Weather.UseFile ? S.Weather.FilePath : S.Weather.WeatherID;

        /// <summary>
        /// An event that triggers a global refresh of various properties of connected hardware. Raised periodically.
        /// </summary>
        public static event Action Refresh;
        public static void RefreshRaise(object o) {
            if (Refresh != null)
                Parallel.ForEach(Refresh.GetInvocationList(), d => d.DynamicInvoke());
        }

        /// <summary>
        /// Automatically link the <see cref="RefreshRaise"/> methods of the hardware to the <see cref="Refresh"/> event.
        /// </summary>
        /// <returns><c>true</c> always. The return value is used to enable the method to be run automatically
        /// on initialisation of the static class since it needs to be stored in <see cref="subscribed"/>.</returns>
        private static bool SubscribeRefresh() {
            Refresh += Mount.RefreshRaise;
            Refresh += Dome.RefreshRaise;
            Refresh += Weather.RefreshRaise;
            foreach (var model in CamModels)
                Refresh += model.Cam.RefreshRaise;
            return true;
        }
        /// <summary>
        /// A dummy field that serves to force execution of <see cref="SubscribeRefresh"/> upon initialisation of the class.
        /// </summary>
        private static bool subscribed = SubscribeRefresh();

        /// <summary>
        /// A timer that raises <see cref="Refresh"/> periodically.
        /// </summary>
        private static Timer refreshTimer;
        /// <summary>
        /// Whether the <see cref="refreshTimer"/> is currently running.
        /// </summary>
        private static bool refreshing;
        
        /// <summary>
        /// Start the timer that raises <see cref="Refresh"/> periodically.
        /// </summary>
        /// <param name="dt"></param>
        public static void StartRefresh(TimeSpan dt) {
            if (refreshing) StopRefresh();
            refreshTimer = new Timer(RefreshRaise, null, TimeSpan.Zero, dt);
            refreshing = true;
        }

        /// <summary>
        /// Stop the timer that raises <see cref="Refresh"/> periodically.
        /// </summary>
        public static void StopRefresh() {
            refreshTimer?.Dispose();
            refreshTimer = null;
            refreshing = false;
        }


        /// <summary>
        /// A shortcut to the software syncing interface of the default observatory.
        /// </summary>
        /// <param name="state">The desired syncing state</param>
        public static void Slave(bool? state = null) {
            if (state ?? !Default.IsSlaving) Default.SlaveDomeToMount();
            else Default.UnSlaveDomeFromMount();
        }
    }
}
