using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using heliomaster_wpf.Annotations;
using heliomaster_wpf.Properties;
using Renci.SshNet;

namespace heliomaster_wpf {
    public class Observatory : BaseNotify {
        public List<BaseCamera> Cams      { get; } = new List<BaseCamera>();
        public Telescope        Mount     { get; } = S.Mount.Telescope;
        public Dome             Dome      { get; } = new Dome();
        public Weather          Weather   { get; } = new Weather();
        public Remote           Remote    { get; } = new Remote();
        
        public ObservableCollection<CameraModel> CameraModels { get; } = new ObservableCollection<CameraModel>();
        public CommonTimelapse CommonTimelapse { get; set; }


        public Observatory() {
            Closing += Close;
        }
        

        public bool ConnectRemote() {
            return S.Remote.LoginMethod == RemoteLoginMethods.UserPass
                ? Remote.Init(S.Remote.Host, S.Remote.User, S.Remote.Pass, S.Remote.Port)
                : Remote.Init(S.Remote.Host, S.Remote.User, new PrivateKeyFile(S.Remote.PrivateKeyFilename),
                              S.Remote.Port);
        }

        private bool isOpen;
        
        public event Action Opening;
        private void OpeningRaise() => Opening?.Invoke();
        public event Action OpenSuccess;
        private void OpenSuccessRaise() => OpenSuccess?.Invoke();
        public event Action OpenFailure;
        private void OpenFailureRaise() => OpenFailure?.Invoke();

        public event Action Closing;
        private void ClosingRaise() => Closing?.Invoke();
        public event Action CloseSuccess;
        private void CloseSuccessRaise() => CloseSuccess?.Invoke();
        public event Action CloseFailure;
        private void CloseFailureRaise() => CloseFailure?.Invoke();
        
        public async void Open(DateTime? closeAt = null, TimeSpan? camMargin = null, TimeSpan? closeMargin = null) {
            isOpen = true;
            try {
                // Point telescope:
                //     Unpark; Go-to

                // Open dome
                //     SmartShutter(true)

                // Rotate dome to telescope
                //     Slew ?

                // Slave dome to telescope
                //     Slave(true)

                // Turn on cameras
                //     BaseCamera.StartLivePreview
                //     Assert the object is in view!!
                //         Fail if not

                // Set timelapse to end at end time - margin
                CommonTimelapse.TieAll();
                if (closeAt != null) {
                    CommonTimelapse.Main.StopMethod = 2;
                    CommonTimelapse.Main.End        = camMargin==null
                                                          ? (DateTime) closeAt
                                                          : (DateTime) closeAt - (TimeSpan) camMargin;
                } else {
                    CommonTimelapse.Main.StopMethod = 0;
                    CommonTimelapse.Main.Nshots     = 1000;
                }

                // Start timelapse
                CommonTimelapse.Start(CameraModel.TimelapseAction, CameraModels);

                OpenSuccessRaise();
                
                if (closeAt != null) {
                    await Task.Delay((closeMargin == null
                                          ? (DateTime) closeAt
                                          : (DateTime) closeAt - (TimeSpan) closeMargin)
                                     - DateTime.Now);
                    ClosingRaise();
                }
            } catch {
                isOpen = false;
                OpenFailureRaise();
                throw;
            }
        }

        public async void Close() {
            // Stop dome slaving
            //     Slave(false)

            
            
            if (Weather.Condition == WeatherItem.Conditions.Bad) {
                var tasks = new List<Task>();
                
                // TODO: Close dome (concurrent)
                //     SmartShutter(false, critical = true)
                
                CommonTimelapse.Stop();
                foreach (var cam in Cams)
                    tasks.Add(cam.Stop());
                
                if (!Mount.AtPark)
                    tasks.Add(Mount.HandlePark()); // TODO: Telescope.Park / Unpark separate methods or HandlePark(bool? park)

                foreach (var task in tasks)
                    await task;
            } else {
                CommonTimelapse.Stop();
                foreach (var cam in Cams)
                    await cam.Stop();
                
                if (!Mount.AtPark)
                    await Mount.HandlePark(); // TODO: Telescope.Park / Unpark separate methods or HandlePark(bool? park)
                
                // TODO: Close dome (concurrent)
                //     SmartShutter(false, critical = true)
            }
            
            
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

        private static bool SubscribeRefresh() {
            Refresh += Mount.RefreshRaise;
            Refresh += Dome.RefreshRaise;
            Refresh += Weather.RefreshRaise;
            foreach (var cam in Cams)
                Refresh += cam.RefreshRaise;
            return true;
        }
        [UsedImplicitly] private static bool subscribed = SubscribeRefresh();

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
