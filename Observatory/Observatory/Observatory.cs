using System.Collections.ObjectModel;
using System.Linq;
using heliomaster.Properties;
using Renci.SshNet;

namespace heliomaster {
    /// <summary>
    /// An observatory with a mount, dome, cameras, a power controller, a remote processing host, and methods to
    /// automate their operation.
    /// </summary>
    public partial class Observatory : BaseNotify {
        /// <summary>
        /// The observatory's telescope mount controller initialised from saved settings
        /// (<see cref="MountSettings.Telescope"/> property of <see cref="S.Mount"/>).
        /// </summary>
        public Telescope Mount { get; } = S.Mount.Telescope;

        /// <summary>
        /// The observatory's dome controller initialised from saved settings (<see cref="DomeSettings.Dome"/> property
        /// of <see cref="S.Dome"/>).
        /// </summary>
        public Dome Dome { get; } = S.Dome.Dome;

        /// <summary>
        /// The observatory's weather watcher. Initialised in the constructor as either a <see cref="WeatherFromFile"/>
        /// or a <see cref="T:Weather"/>.
        /// </summary>
        public Weather Weather { get; }

        /// <summary>
        /// The observatory's remote host controller. Connected to by <see cref="ConnectRemote"/>.
        /// </summary>
        public Remote Remote { get; } = new Remote();

        /// <summary>
        /// The collection of the observatory's cameras wrapped in <see cref="CameraModel"/>s. Populated by
        /// <see cref="ConnectCameras"/> from saved settings (<see cref="CameraSettings.CameraModels"/> property of
        /// <see cref="S.Cameras"/>).
        /// </summary>
        public ObservableCollection<CameraModel> CameraModels { get; } = new ObservableCollection<CameraModel>();

        /// <summary>
        /// The common timelapse controller for the observatory's cameras. Populated with <see cref="Timelapse"/>s in
        /// <see cref="ConnectCameras"/>.
        /// </summary>
        public CommonTimelapse CommonTimelapse { get; } = new CommonTimelapse();

        private BasePower _power;
        /// <summary>
        /// The observatory's power controller.
        /// </summary>
        /// <remarks>The property's setter supports setting to a default value based on the
        /// <see cref="PowerSettings.PowerType"/> setting in <see cref="S.Power"/> when being passed the value
        /// <c>null</c>. The default value is the <see cref="PowerSettings.Netio"/> property of <see cref="S.Power"/> if
        /// the type is <see cref="PowerTypes.Netio"/>.</remarks>
        public BasePower Power {
            get => _power;
            set {
                var newval = value ?? (S.Power.PowerType == PowerTypes.Netio ? S.Power.Netio : null);
                if (Equals(newval, _power)) return;
                _power = newval;
                OnPropertyChanged();
            }
        }


        /// <summary>
        /// Create a new observatory, initialising the components as needed and registering the various event handlers.
        /// </summary>
        /// <remarks>
        /// The basic philosophy behind creating a (non-static) Observatory class is to enable it to be instantiated
        /// multiple times with (theoretically) different hardware controllers, i.e. to allow a single instance of the
        /// program to control multiple observatories. Although this is probably never going to be needed, and
        /// furthermore rather hard to implement UI-wise, it is still good programming practise. With that said, the
        /// current design falls just a bit short of achieving this goal because the hardware-related properties are
        /// instantiated or initialised from static settings. However, this can be rectified by simply pulling all
        /// these initialisations in a special constructor and introducing another one that returns a "blank"
        /// Observatory. (Overloaded versions of <see cref="InitPower"/>, <see cref="ConnectRemote"/>, and
        /// <see cref="ConnectCameras"/> are needed as well.)
        /// </remarks>
        public Observatory() {
            Weather = S.Weather.UseFile ? new WeatherFromFile() : new Weather();

            InitPower();

            WeatherSafeChanged += WeatherSafeChangedHandle;

            Starting += StartingHandle;
            Shutting += ShuttingHandle;
            Fixing   += FixingHandle;


            StartupFailure  += e => Emit(new AutoOperationsWarning($"An exception has occurred during startup: {Utilities.FormatException(e)}"));
            ShutdownFailure += e => Emit(new AutoOperationsWarning($"An exception has occurred during shutdown: {Utilities.FormatException(e)}"));


            StartupSuccess  += () => Inform("Startup successful.");
            StartupFailure  += args => Inform("Startup unsuccessful.");
            ShutdownSuccess += () => Inform("Shutdown successful.");
            StartupFailure  += args => Inform("Shutdown unsuccessful.");
            FixingSuccess   += () => Inform("Fixing successful");
            FixingFailure   += () => Inform("Fixing unsuccessful.");
        }
        
        
        /// <summary>
        /// Initialise the observatory's power controller (<see cref="Power"/>).
        /// </summary>
        /// <remarks>
        /// <para>Sets <see cref="Power"/> to the default by assigning <c>null</c> to it and registers an event handler
        /// to automatically reset it if the <see cref="PowerSettings.PowerType"/> property of <see cref="S.Power"/>
        /// changes.</para>
        /// <para>Registers the <see cref="Mount"/> and <see cref="Dome"/> with the power controller and links the
        /// <see cref="refreshPower"/> handler with <see cref="O.Refresh"/></para>
        /// </remarks>
        private void InitPower() {
            Power = null;
            S.Power.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(S.Power.PowerType)) {
                    Power = null;
                }
            };

            Mount.HasPowerControl = Power?.Register(Mount, S.Power.MountName) ?? false;
            Dome.HasPowerControl  = Power?.Register(Dome,  S.Power.DomeName) ?? false;

            O.Refresh += refreshPower; // TODO: dedicated power refresh less often!?
        }

        /// <summary>
        /// Refreshes the power state of the <see cref="Mount"/> and <see cref="Dome"/>
        /// (<see cref="BaseHardwareControl.HasPowerControl"/> and <see cref="BaseHardwareControl.IsPowerOn"/>
        /// properties).
        /// </summary>
        /// <remarks>
        /// First checks whether the power controller is connected, and then for each of <see cref="Mount"/> and
        /// <see cref="Dome"/> and their respective identifiers (<see cref="PowerSettings.MountName"/> and
        /// <see cref="PowerSettings.DomeName"/>) checks whether it <see cref="BaseHardwareControl.HasPowerControl"/>
        /// and whether its state is reported by the controller. If so, updates its
        /// <see cref="BaseHardwareControl.IsPowerOn"/> property. If not, attempts to <see cref="M:Power.Register"/> it,
        /// and if that fails as well, sets its <see cref="BaseHardwareControl.HasPowerControl"/> to <c>false</c>. 
        /// </remarks>
        private async void refreshPower() {
            if (Power is Netio.Power p && await p.Get() is Netio.Netio s) {
                foreach (var hn in new[] {
                    new {h = (BaseHardwareControl) Dome,  n = S.Power.DomeName},
                    new {h = (BaseHardwareControl) Mount, n = S.Power.MountName}
                })
                    if ((hn.h.HasPowerControl || p.Register(hn.h, hn.n))
                        && p.GetID(hn.h) is int id
                        && s.Outputs.FirstOrDefault(i => i.ID == id) is Netio.Output o) {
                        hn.h.HasPowerControl = true;
                        hn.h.IsPowerOn       = o.State == Netio.States.On;
                    } else {
                        hn.h.HasPowerControl = false;
                        hn.h.IsPowerOn       = null;
                    }
            } else {
                Dome.HasPowerControl  = false;
                Mount.HasPowerControl = false;
            }
        }


        /// <summary>
        /// Connect to the remote host controller (<see cref="Remote"/>) using the settings in <see cref="S.Remote"/>.
        /// </summary>
        /// <returns></returns>
        public bool ConnectRemote() {
            return S.Remote.LoginMethod == RemoteLoginMethods.UserPass
                       ? Remote.Init(S.Remote.Host, S.Remote.User, S.Remote.Pass, S.Remote.Port)
                       : Remote.Init(S.Remote.Host, S.Remote.User, new PrivateKeyFile(S.Remote.PrivateKeyFilename),
                                     S.Remote.Port);
        }
    }
}
