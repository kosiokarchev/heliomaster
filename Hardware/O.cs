using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using heliomaster_wpf.Annotations;
using heliomaster_wpf.Properties;
using Renci.SshNet;

namespace heliomaster_wpf {
    public class ObservatoryException : Exception {
        public ObservatoryException() { }
        public ObservatoryException(string message) : base(message) { }
    }
    public class ObservatoryWarning : ObservatoryException {
        public ObservatoryWarning() { }
        public ObservatoryWarning(string message) : base(message) { }
    }
    public class SlavingWarning : ObservatoryWarning {
        public SlavingWarning() { }
        public SlavingWarning(string message) : base(message) { }
    }

    public class Observatory : BaseNotify {
        public Telescope        Mount     { get; } = S.Mount.Telescope;
        public Dome             Dome      { get; } = S.Dome.Dome;
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


        public void Emit(ObservatoryException e) {
            // TODO: Error handling, guh...
        }


        private bool _isSlaving;
        public bool IsSlaving {
            get => _isSlaving;
            set {
                if (value == _isSlaving) return;
                _isSlaving = value;
                OnPropertyChanged();
            }
        }

        private bool _isHardwareSlaving;
        public bool IsHardwareSlaving {
            get => _isHardwareSlaving;
            set {
                if (value == _isHardwareSlaving) return;
                _isHardwareSlaving = value;
                OnPropertyChanged();
            }
        }

        private Timer slavingTimer;

        private async void slave() {
            var az = Mount.Azimuth;
            if (double.IsNaN(az)) Emit(new SlavingWarning("Mount state is invalid."));

            var domaz = Dome.Azimuth;
            if (double.IsNaN(domaz)) Emit(new SlavingWarning("Dome state is invalid."));

            if (Math.Abs(domaz - az) > S.Dome.SlaveTolerance) {
                if (!await Dome.Slew(az, true))
                    Emit(new SlavingWarning("Dome slew was unsuccessful."));

                if (IsHardwareSlaving)
                    Emit(new SlavingWarning("Hardware slaving does not appear to be working correctly."));
            }
            if (Math.Abs(domaz - az) > S.Dome.SlaveTolerance
                && !(await Dome.Slew(az, true)))
                Emit(new SlavingWarning("Dome slew was unsuccessful."));
        }
        public async void SlaveDomeToMount(TimeSpan? interval = null, TimeSpan? checkup = null) {
            if (!IsSlaving) {
                TimeSpan dt;
                if (Dome.CanSlave) {
                    await Dome.Slave(true);
                    dt = checkup ?? S.Dome.SlaveCheckup;
                    IsHardwareSlaving = true;
                } else {
                    dt = interval ?? S.Dome.SlaveInterval;
                    IsHardwareSlaving = false;
                }

                slavingTimer = new Timer(o => slave(), null, TimeSpan.Zero, dt);
                Mount.Slewed += slave;

                IsSlaving = true;
            }
        }

        public async void UnSlaveDomeFromMount() {
            await Dome.Slave(false);
            slavingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            slavingTimer?.Dispose();
            slavingTimer = null;
            Mount.Slewed -= slave;
            IsSlaving = false;
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

        public async void Open(bool autostart = false, DateTime? closeAt = null, TimeSpan? camMargin = null, TimeSpan? closeMargin = null) {
            isOpen = true;
            try {
                // Point telescope:
                //     Unpark; Go-to

                // Open dome
                //     SmartShutter(true)

                // Rotate dome to telescope --> in next line
                // Slave dome to telescope
                SlaveDomeToMount();

                // Turn on cameras
                foreach (var model in CameraModels)
                    model.Cam.StartLivePreview(30); // TODO: Unhardcode maxfps --> cam setting

                // Sync timelapses and prepare
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
                // TODO: Assert object is in view
                if (autostart)
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
            UnSlaveDomeFromMount();

            CommonTimelapse.Stop();

            var camtasks = new List<Task<List<List<QueueItem<SemaphoreSlim, CameraImage>>>>>();
            foreach (var model in CameraModels)
                camtasks.Add(model.Cam.Stop());

            var mounttask = Mount.Park();

            var dometask = Dome.SmartShutter(false);
        }
    }

    public static class O {
        public static readonly Observatory Default = new Observatory();

        public static Telescope        Mount     => Default.Mount;
        public static Dome             Dome      => Default.Dome;
        public static Weather          Weather   => Default.Weather;
        public static Remote           Remote    => Default.Remote;

        public static ObservableCollection<CameraModel> CamModels => Default.CameraModels;

        public static event Action Refresh;
        public static void OnRefresh(object o) { Refresh?.Invoke(); }

        private static bool SubscribeRefresh() {
            Refresh += Mount.RefreshRaise;
            Refresh += Dome.RefreshRaise;
            Refresh += Weather.RefreshRaise;
            foreach (var model in CamModels)
                Refresh += model.Cam.RefreshRaise;
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


        public static bool Slave(bool? state = null) {
            if (state ?? !Default.IsSlaving) Default.SlaveDomeToMount();
            else Default.UnSlaveDomeFromMount();
            return Default.IsSlaving == state;
        }
    }
}
