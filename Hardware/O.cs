using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
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
    public class AutoOperationsException : ObservatoryException {
        public AutoOperationsException() { }
        public AutoOperationsException(string message) : base(message) { }
    }


    public static class Logger {
        public static void put(string msg) {
            Console.WriteLine(msg);
        }

        public static void debug(string msg) {put($"DEBUG: {msg}");}
        public static void info(string msg) {put($"INFO: {msg}");}
        public static void warning(string msg) {put($"WARNING: {msg}");}
        public static void error(string msg) {put($"ERROR: {msg}");}
        public static void critical(string msg) {put($"CRITICAL: {msg}");}
    }

    public class Observatory : BaseNotify {

        public Telescope        Mount     { get; } = S.Mount.Telescope;
        public Dome             Dome      { get; } = S.Dome.Dome;
        public Weather          Weather   { get; } = new Weather();
        public Remote           Remote    { get; } = new Remote();

        public ObservableCollection<CameraModel> CameraModels { get; } = new ObservableCollection<CameraModel>();
        public CommonTimelapse CommonTimelapse { get; } = new CommonTimelapse();

        private BasePower _power;
        public BasePower Power {
            get => _power;
            set {
                var newval = value ?? (S.Power.PowerType == PowerTypes.Netio ? S.Power.Netio : null);
                if (Equals(newval, _power)) return;
                _power = newval;
                OnPropertyChanged();
            }
        }


        public Observatory() {
            Power = null;
            S.Power.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(S.Power.PowerType))
                    Power = null;
            };
            Console.WriteLine(S.Power.MountName);
            Console.WriteLine(S.Power.DomeName);

            Starting += StartingHandle;
            Shutting += ShuttingHandle;
        }


        public bool ConnectRemote() {
            return S.Remote.LoginMethod == RemoteLoginMethods.UserPass
                ? Remote.Init(S.Remote.Host, S.Remote.User, S.Remote.Pass, S.Remote.Port)
                : Remote.Init(S.Remote.Host, S.Remote.User, new PrivateKeyFile(S.Remote.PrivateKeyFilename), S.Remote.Port);
        }


        public void Emit(ObservatoryException e) {
            // TODO: Error handling, duh...
            Console.WriteLine(e.Message);
            MessageBox.Show(e.Message);
        }


        #region SLAVING

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
            if (double.IsNaN(az)) {
                Emit(new SlavingWarning("Mount state is invalid."));
                return;
            }

            var domaz = Dome.Azimuth;
            if (double.IsNaN(domaz)) {
                Emit(new SlavingWarning("Dome state is invalid."));
                return;
            }

            if (Math.Abs(domaz - az) > S.Dome.SlaveTolerance) {
                if (IsHardwareSlaving)
                    Emit(new SlavingWarning("Hardware slaving does not appear to be working correctly."));
                if (!await Dome.Slew(az, true))
                    Emit(new SlavingWarning("Dome slew was unsuccessful."));
            }
        }
        public async Task SlaveDomeToMount(TimeSpan? interval = null, TimeSpan? checkup = null) {
            if (!IsSlaving) {
                TimeSpan dt;
                if (!S.Dome.AlwaysSoftSlave && Dome.CanSlave) {
                    await Dome.Slave(true);
                    dt = checkup ?? S.Dome.SlaveCheckup;
                    IsHardwareSlaving = true;
                } else {
                    dt = interval ?? S.Dome.SlaveInterval;
                    IsHardwareSlaving = false;
                }

                slavingTimer = new Timer(o => slave(), null, dt, dt);
                Mount.Slewed += slave;

                IsSlaving = true;

                await Task.Run((Action) slave);
            }
        }
        public async Task UnSlaveDomeFromMount() {
            await Dome.Slave(false);
            slavingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            slavingTimer?.Dispose();
            slavingTimer = null;
            Mount.Slewed -= slave;
            IsSlaving = false;
        }

        #endregion


        public async void ConnectCameras() {
            foreach (var exmodel in CameraModels) {
                O.Refresh -= exmodel.Cam.RefreshRaise;
                await exmodel.Cam.Disconnect();
            }

            foreach (var model in S.Cameras.CameraModels) {
                var cam = BaseCamera.Create(model.CameraType);
                if (await cam.Connect(model.CameraID)) {
                    model.Cam = cam;

                    O.Refresh += cam.RefreshRaise;

                    CameraModels.Add(model);

                    if (await cam.Focuser.Connect(model.FocuserID)
                        && cam.Focuser.Absolute != null
                        && (bool) cam.Focuser.Absolute)
                        O.Refresh += cam.Focuser.RefreshRaise;
                } else
                    MessageBox.Show($"Connecting to camera {model.CameraID} failed.");
            }

            if (CameraModels.Count < 1) return;

            CommonTimelapse.Make(new Timelapse {
                StopMethod = S.Settings.timelapseStopMethod,
                Interval   = S.Settings.timelapseInterval,
                Nshots     = S.Settings.timelapseNshots,
            }, CameraModels.Count);
            for (var i = 0; i < CameraModels.Count; ++i) {
                CameraModels[i].Timelapse = CommonTimelapse[i];
            }
        }


        #region AUTOMATION

        public event Action Starting;
        public void StartingRaise() => Starting?.Invoke();
        private void StartingHandle() => Startup();

        public event Action StartupSuccess;
        public void StartupSuccessRaise() => StartupSuccess?.Invoke();

        public event Action<ObservatoryException> StartupFailure;
        public void StartupFailureRaise(ObservatoryException e) => StartupFailure?.Invoke(e);


        public event Action Shutting;
        public void ShuttingRaise() => Shutting?.Invoke();
        private void ShuttingHandle() => Shutdown();

        public event Action ShutdownSuccess;
        public void ShutdownSuccessRaise() => ShutdownSuccess?.Invoke();

        public event Action<ObservatoryException> ShutdownFailure;
        public void ShutdownFailureRaise(ObservatoryException e) => ShutdownFailure?.Invoke(e);


        private readonly CancellationTokenSource cancelwait = new CancellationTokenSource();

        public async Task<bool> Startup(bool autostart = false, DateTime? closeAt = null, TimeSpan? camMargin = null, TimeSpan? closeMargin = null) {
            try {
                if (!Mount.Valid && !await Mount.Connect(S.Mount.MountID))
                    throw new AutoOperationsException($"Could not connect telescope {S.Mount.MountID}");
                if (!Dome.Valid && !await Dome.Connect(S.Dome.DomeID))
                    throw new AutoOperationsException($"Could not connect dome {S.Dome.DomeID}");

                if (!(await Mount.Unpark() && await Mount.GoTo()))
                    throw new AutoOperationsException("Could not operate telescope.");

                if (!await Dome.SmartShutter(true))
                    throw new AutoOperationsException("Could not open dome.");

                await SlaveDomeToMount();


                foreach (var model in CameraModels)
                    model.Cam.StartLivePreview(30); // TODO: Unhardcode maxfps --> cam setting

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

                // TODO: Assert object is in view
                if (autostart)
                    CommonTimelapse.Start(CameraModel.TimelapseAction, CameraModels);


                StartupSuccessRaise();


                if (closeAt != null)
                    Task.Run(() => {
                        try {
                            Task.Delay(
                                (closeMargin == null
                                    ? (DateTime) closeAt
                                    : (DateTime) closeAt - (TimeSpan) closeMargin)
                                - DateTime.Now, cancelwait.Token);
                            ShuttingRaise();
                        } catch (Exception e) {
                            Console.WriteLine(e.GetType().Name);
                            Console.WriteLine(e.Message);
                        }
                    });

                return true;
            } catch (ObservatoryException e) {
                Emit(e);
                StartupFailureRaise(e);
                return false;
            }
        }

        public async Task<bool> Shutdown() {
            try {
                CommonTimelapse.Stop();

                var camtasks = new List<Task<List<List<QueueItem<SemaphoreSlim, CameraImage>>>>>();
                foreach (var model in CameraModels)
                    camtasks.Add(model.Cam.Stop());

                await UnSlaveDomeFromMount();

                var mounttask = Mount.Park();
                var dometask  = Dome.SmartShutter(false);

                if (!await mounttask)
                    Emit(new AutoOperationsException("Could not park mount"));

                if (!await dometask)
                    Emit(new AutoOperationsException("Could not close dome."));

                foreach (var camtask in camtasks)
                    camtask.Wait();

                if (mounttask.Result && dometask.Result) {
                    await Dome.HomeOrPark(home: false);

                    ShutdownSuccessRaise();
                    return true;
                } else throw new AutoOperationsException("Observatory shutdown with errors.");
            } catch (ObservatoryException e) {
                Emit(e);
                ShutdownFailureRaise(e);
                return false;
            }
        }

        public void Interrupt() {
            cancelwait.Cancel();
        }

        #endregion
    }

    public static class O {
        public static readonly Observatory Default = new Observatory();

        public static Telescope        Mount     => Default.Mount;
        public static Dome             Dome      => Default.Dome;
        public static Weather          Weather   => Default.Weather;
        public static Remote           Remote    => Default.Remote;

        public static ObservableCollection<CameraModel> CamModels => Default.CameraModels;
        public static CommonTimelapse Timelapse => Default.CommonTimelapse;

        public static BasePower Power => Default.Power;

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
