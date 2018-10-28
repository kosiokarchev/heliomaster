using System.Collections.ObjectModel;
using System.Linq;
using heliomaster.Netio;
using heliomaster.Properties;
using Renci.SshNet;

namespace heliomaster {
    /// <summary>
    /// An observatory with a mount, dome, cameras, a power controller, a remote processing host, and methods to
    /// automate their operation.
    /// </summary>
    public partial class Observatory : BaseNotify {
        public Telescope Mount   { get; } = S.Mount.Telescope;
        public Dome      Dome    { get; } = S.Dome.Dome;
        public Weather   Weather { get; }
        public Remote    Remote  { get; } = new Remote();

        public ObservableCollection<CameraModel> CameraModels    { get; } = new ObservableCollection<CameraModel>();
        public CommonTimelapse                   CommonTimelapse { get; } = new CommonTimelapse();

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

            StartupFailure  += (e) => Emit(new AutoOperationsWarning($"An exception has ocurred during startup: {Utilities.FormatException(e)}"));
            ShutdownFailure += (e) => Emit(new AutoOperationsWarning($"An exception has ocurred during shutdown: {Utilities.FormatException(e)}"));

            Fixing += FixingHandle;

            ObjectNotFound += ObjectNotFoundHandle;

            StartupSuccess  += () => Inform("Startup successful.");
            StartupFailure  += (args) => Inform("Startup unsuccessful.");
            ShutdownSuccess += () => Inform("Shutdown successful.");
            StartupFailure  += (args) => Inform("Shutdown unsuccessful.");
            FixingSuccess   += () => Inform("Fixing successful");
            FixingFailure   += () => Inform("Fixing unsuccessful.");
        }
        
        
        private void InitPower() {
            Power = null;
            S.Power.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(S.Power.PowerType)) {
                    Power = null;
                }
            };

            Mount.HasPowerControl = Power?.Register(Mount, S.Power.MountName) ?? false;
            Dome.HasPowerControl  = Power?.Register(Dome,  S.Power.DomeName) ?? false;

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
                        } else {
                            hn.h.HasPowerControl = false;
                            hn.h.IsPowerOn       = null;
                        }
                } else {
                    Dome.HasPowerControl  = false;
                    Mount.HasPowerControl = false;
                }
            };
        }


        public bool ConnectRemote() {
            return S.Remote.LoginMethod == RemoteLoginMethods.UserPass
                       ? Remote.Init(S.Remote.Host, S.Remote.User, S.Remote.Pass, S.Remote.Port)
                       : Remote.Init(S.Remote.Host, S.Remote.User, new PrivateKeyFile(S.Remote.PrivateKeyFilename),
                                     S.Remote.Port);
        }
    }
}
