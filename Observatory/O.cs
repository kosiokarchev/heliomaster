using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using heliomaster.Annotations;
using heliomaster.Netio;
using heliomaster.Properties;
using Renci.SshNet;

namespace heliomaster {
    public class Observatory : BaseNotify {

        public Telescope        Mount     { get; } = S.Mount.Telescope;
        public Dome             Dome      { get; } = S.Dome.Dome;
        public Weather          Weather   { get; }
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
            Weather = S.Weather.UseFile ? new WeatherFromFile() : new Weather();

            InitPower();

            Starting += StartingHandle;
            Shutting += ShuttingHandle;

            WeatherSafeChanged += WeatherSafeChangedHandle;

            StartupFailure += (e) => Emit(new AutoOperationsWarning($"An exception has ocurred during startup: {e.GetType().Name}: {e.Message}"));
            ShutdownFailure += (e) => Emit(new AutoOperationsWarning($"An exception has ocurred during shutdown: {e.GetType().Name}: {e.Message}"));

            Fixing += FixingHandle;

            ObjectNotFound += ObjectNotFoundHandle;
        }


        public bool ConnectRemote() {
            return S.Remote.LoginMethod == RemoteLoginMethods.UserPass
                ? Remote.Init(S.Remote.Host, S.Remote.User, S.Remote.Pass, S.Remote.Port)
                : Remote.Init(S.Remote.Host, S.Remote.User, new PrivateKeyFile(S.Remote.PrivateKeyFilename), S.Remote.Port);
        }


        public void Emit(ObservatoryException e) {
            // TODO: Error handling, duh...
            if (e is ObservatoryWarning w)
                Logger.warning(e.Message);
            else if (e is AutoOperationsException) {
                Logger.error(e.Message);
                MessageBox.Show(e.Message);
            } else if (e is CriticalObservatoryError) {
                Logger.critical(e.Message);
                MessageBox.Show(e.Message);
            }
        }


        private void InitPower() {
            Power = null;
            S.Power.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(S.Power.PowerType)) {
                    Power = null;
                }
            };

            Mount.HasPowerControl = Power?.Register(Mount, S.Power.MountName) ?? false;
            Dome.HasPowerControl  = Power?.Register(Dome, S.Power.DomeName) ?? false;

            O.Refresh += async () => {
                if (Power is Netio.Power p && await p.Get() is Netio.Netio s) {
                    foreach (var hn in new[] {
                        new {h = (BaseHardwareControl) Dome,  n = S.Power.DomeName},
                        new {h = (BaseHardwareControl) Mount, n = S.Power.MountName}
                    })
                        if ((hn.h.HasPowerControl || p.Register(hn.h, hn.n))
                            && p.GetID(hn.h) is int id
                            && s.Outputs.FirstOrDefault(i => i.ID == id) is Output o) {
                            hn.h.HasPowerControl = true;
                            hn.h.IsPowerOn       = o.State == States.On;
                        }
                } else {
                    Dome.HasPowerControl = false;
                    Mount.HasPowerControl = false;
                }
            };
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

        private async Task slave() {
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
        private void _slave() => slave();
        public async Task SlaveDomeToMount(TimeSpan? interval = null, TimeSpan? checkup = null) {
            if (!IsSlaving) {
                await slave(); // TODO: Does not wait for initial sync

                TimeSpan dt;
                if (!S.Dome.AlwaysSoftSlave && Dome.CanSlave) {
                    await Dome.Slave(true);
                    dt                = checkup ?? S.Dome.SlaveCheckup;
                    IsHardwareSlaving = true;
                } else {
                    dt                = interval ?? S.Dome.SlaveInterval;
                    IsHardwareSlaving = false;
                }

                slavingTimer =  new Timer(o => slave(), null, dt, dt);
                Mount.Slewed += _slave;

                IsSlaving = true;
            }
        }
        public async Task UnSlaveDomeFromMount() {
            await Dome.Slave(false);
            slavingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            slavingTimer?.Dispose();
            slavingTimer = null;
            Mount.Slewed -= _slave;
            IsSlaving = false;
        }

        #endregion


        #region CAMERAS

        public async void ConnectCameras() {
            DisconnectCameras();

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

        public async void DisconnectCameras() {
            foreach (var exmodel in CameraModels) {
                O.Refresh -= exmodel.Cam.RefreshRaise;
                exmodel.Images.Clear();
                CommonTimelapse.Stop();
                await exmodel.Cam.Disconnect();
            }

            CameraModels.Clear();
        }

        #endregion


        #region OBJECT_DETECTION

        public event Action ObjectNotFound;
        public void         ObjectNotFoundRaise() => ObjectNotFound?.Invoke();
        public void ObjectNotFoundHandle() {
            // TODO: Search for object
        }

        public bool ObjectIsInView() => false;
        public bool SearchForObject() { return false; }

        #endregion


        #region WEATHER_PROTECTION

        private bool _weatherProtectionOn;
        public bool WeatherProtectionOn {
            get => _weatherProtectionOn;
            set {
                if (_weatherProtectionOn.Equals(value)) return;
                _weatherProtectionOn = value;
                OnPropertyChanged();
            }
        }

        private bool? currSafeState;
        public event Action<bool?> WeatherSafeChanged;
        private void WeatherSafeChangedHandle(bool? safe) {
            if (safe != true) Shutdown();
        }
        public event Action WeatherIsUnsafe;

        private void weatherProtection(object o, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(Weather.Safe)) {
                var safe = Weather.Safe;
                if (safe != currSafeState) {
                    currSafeState = safe;
                    WeatherSafeChanged?.Invoke(currSafeState);
                }
                if (currSafeState != true)
                    WeatherIsUnsafe?.Invoke();
            }
        }

        public bool StartWeatherProtection() {
            Weather.PropertyChanged += weatherProtection;
            WeatherProtectionOn = true;
            return Weather.Safe == true;
        }
        public void StopWeatherProtection() {
            Weather.PropertyChanged -= weatherProtection;
            WeatherProtectionOn = false;
        }

        #endregion


        #region AUTOMATION

        public class StartupArguments {
            public bool RequireInView;
            public bool Autostart;
            public DateTime? CloseAt;
            public TimeSpan? CamMargin;
            public TimeSpan? CloseMargin;

            private DateTime? Marginalize(TimeSpan? margin) {
                return CloseAt == null ? (DateTime?) null
                    : (margin == null
                        ? (DateTime) CloseAt
                        : (DateTime) CloseAt - (TimeSpan) margin);
            }

            public DateTime? CamShutdownTime => Marginalize(CloseMargin);
            public DateTime? ShutdownTime => Marginalize(CloseMargin);
        }

        public event Action<StartupArguments> Starting;
        public void StartingRaise(StartupArguments args = null) => Starting?.Invoke(args ?? new StartupArguments());
        private void StartingHandle(StartupArguments args) => Startup(args);

        public event Action StartupSuccess;
        public void StartupSuccessRaise() => StartupSuccess?.Invoke();

        public event Action<ObservatoryException> StartupFailure;
        public void StartupFailureRaise(ObservatoryException e) => StartupFailure?.Invoke(e);


        public event Action Shutting;
        public void ShuttingRaise() => Shutting?.Invoke();
        private void ShuttingHandle() => Shutdown();

        public event Action ShutdownSuccess;
        public void ShutdownSuccessRaise() => ShutdownSuccess?.Invoke();

        public event Action<Exception> ShutdownFailure;
        public void ShutdownFailureRaise(Exception e) => ShutdownFailure?.Invoke(e);


        public event Action Fixing;
        public void FixingRaise() => Fixing?.Invoke();
        private void FixingHandle() => Fix();

        public event Action FixingSuccess;
        public void FixingSuccessRaise() => FixingSuccess?.Invoke();

        public event Action FixingFailure;
        public void FixingFailureRaise() => FixingFailure?.Invoke();


        private CancellationTokenSource closewait;
        private Task closewaittask;

        private bool perform(Task<bool> t, string msg) {
            t.Wait();
            if (t.Exception != null || t.Result) {
                Emit(new AutoOperationsException(
                         $"{msg}: {(t.Exception == null ? "no details" : t.Exception.Message)}"));
                return false;
            } else
                return true;
        }

        private async Task<bool> connect(BaseHardwareControl h, string id) {
            if (h.Valid || await h.Connect(id)) return true;
            else throw new ConnectionError($"Could not connect hardware type {h.GetType().Name} with id \"{id}\"");
        }
        private Task<bool> connect(BaseHardwareControl h) {
            string id;
            if (h.Equals(Dome)) id = S.Dome.DomeID;
            else if (h.Equals(Mount)) id = S.Mount.MountID;
            else if (h.Equals(Weather)) id = O.WeatherID;
            else throw new ArgumentException();
            return connect(h, id);
        }

        public enum AutomationStates {
            Idle,
            Starting,
            InOperation,
            Closing,
            WaitingForWeather,
            Faulted,
            Fixing
        }

        private AutomationStates _automationState;
        public AutomationStates AutomationState {
            get => _automationState;
            set {
                if (_automationState.Equals(value)) return;
                _automationState = value;

                Logger.info($"Transitioning to {_automationState}");

                OnPropertyChanged();
            }
        }

        private readonly SemaphoreSlim autosem = new SemaphoreSlim(1, 1);

        public async Task<bool> Startup(StartupArguments args) {
            await autosem.WaitAsync();

            try {
                if (AutomationState != AutomationStates.Idle)
                    throw new RefuseAutomationWarning("The state should be idle for startup");
                AutomationState = AutomationStates.Starting;

                await connect(Mount);
                await connect(Dome);
                await connect(Weather);

                if (!StartWeatherProtection())
                    throw new RefuseAutomationWarning("Good weather not confirmed.");

                if (!await Dome.SmartShutter(true))
                    throw new AutoOperationsException("Could not open dome.");

                if (!(await Mount.Unpark() && await Mount.GoTo()))
                    throw new AutoOperationsException("Could not operate telescope.");


                await SlaveDomeToMount();


                foreach (var model in CameraModels)
                    model.Cam.StartLivePreview(30); // TODO: Unhardcode maxfps --> cam setting

                CommonTimelapse.TieAll();
                if (CommonTimelapse.Main is Timelapse m) {
                    if (args.CamShutdownTime is DateTime cst) {
                        m.StopMethod = 2;
                        m.End = cst;
                    } else { } // TODO: What to do when no end time given? Until object sets?
                }


                if (args.RequireInView && !SearchForObject())
                    throw new ObjectNotLocatedError();


                if (args.Autostart)
                    CommonTimelapse.Start(CameraModel.TimelapseAction, CameraModels);

                AutomationState = AutomationStates.InOperation;
                StartupSuccessRaise();

                closewait = new CancellationTokenSource();
                if (args.ShutdownTime is DateTime st)
                    closewaittask = Task.Run(() => {
                        try {
                            Task.Delay(st - DateTime.Now, closewait.Token).Wait();
                            ShuttingRaise();
                        } catch (Exception e) {
                            Console.WriteLine(e.GetType().Name);
                            Console.WriteLine(e.Message);
                        }

                        closewaittask = null;
                        closewait = null;
                    });

                return true;
            } catch (ObservatoryException e) {
                StartupFailureRaise(e);
                AutomationState = AutomationStates.Faulted;
                FixingRaise();
                return false;
            } finally {
                autosem.Release();
            }
        }

        public async Task<bool> Shutdown() {
            await autosem.WaitAsync();
            try {
                Interrupt();

                await connect(Mount);
                await connect(Dome);

                AutomationState = AutomationStates.Closing;

                StopWeatherProtection();

                CommonTimelapse.Stop();

                var camtasks = CameraModels.Select(i => i.Cam.Stop());

                await UnSlaveDomeFromMount();

                var mounttask = Mount.Park();
                var dometask  = Dome.SmartShutter(false);

                if (!await mounttask)
                    Emit(new AutoOperationsException("Could not park mount."));

                if (!await dometask)
                    Emit(new AutoOperationsException("Could not close dome."));

                foreach (var camtask in camtasks)
                    camtask.Wait();

                if (mounttask.Result && dometask.Result) {
                    await Dome.HomeOrPark(home: false);

                    AutomationState = AutomationStates.Idle;
                    ShutdownSuccessRaise();
                    return true;
                } else throw new AutoOperationsException("Observatory shutdown with errors.");
            } catch (Exception e) {
                ShutdownFailureRaise(e);
                AutomationState = AutomationStates.Faulted;
                FixingRaise();
                return false;
            } finally {
                autosem.Release();
            }
        }

        public void Interrupt() {
            closewait?.Cancel();
        }

        public async Task<bool> Fix() {
            AutomationState = AutomationStates.Fixing;
            Logger.info("Attempting to fix observatory state.");

            bool domesuccess = false, mountsuccess = false;
            try {
                await connect(Dome);
                domesuccess = await Dome.SmartShutter(false);
                if (!domesuccess)
                    throw new FixingFailedError("Dome state could not be fixed.");
                else if (!await Dome.HomeOrPark(false))
                    Emit(new AutoOperationsWarning("Could not park dome."));
            } catch (Exception e) {
                Emit(new AutoOperationsException($"Error while fixing dome: {e.GetType().Name}: {e.Message}"));
            }

            try {
                await connect(Mount);
                mountsuccess = await Mount.Park();
                if (!mountsuccess)
                    throw new FixingFailedError("Mount state could not be fixed.");
            } catch (Exception e) {
                Logger.error($"Error while fixing mount: {e.GetType().Name}: {e.Message}");
            }

            if (!(domesuccess && mountsuccess)) {
                Emit(new CriticalObservatoryError("Could not fix observatory state"));
                FixingFailureRaise();
                AutomationState = AutomationStates.Faulted;
                return false;
            } else {
                Logger.info("State was successfully fixed.");
                AutomationState = AutomationStates.Idle;
                FixingSuccessRaise();
                return true;
            }
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

        public static string WeatherID => S.Weather.UseFile ? S.Weather.FilePath : S.Weather.WeatherID;

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
