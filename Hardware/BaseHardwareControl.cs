using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ASCOM.DriverAccess;

namespace heliomaster {
    /// <summary>
    /// The base class for all classes representing hardware.
    /// </summary>
    /// <remarks>
    /// <para>Includes the basic functionality and logic for connecting and disconnecting, synchronising properties with
    /// the driver as well as the power control for the hardware.</para>
    /// <para>The implemented functionality applies to ASCOM-controlled hardware because this is the case with the
    /// majority of it. This is also the reason why this class includes the members <see cref="driver"/> and
    /// <see cref="driverType"/>, which serve to automate the creation of a variety of drivers used by subclasses in a
    /// generic way.</para>
    /// </remarks>
    public abstract class BaseHardwareControl : BaseNotify {
        /// <summary> An instance of the driver. </summary>
        /// <remarks> Only useful for ASCOM or pseudo-ASCOM (see <see cref="FileOC"/>) controllers. </remarks>
        [XmlIgnore] protected AscomDriver driver;
        
        /// <summary> The type of the <see cref="driver"/> object. </summary>
        /// <remarks> Only useful for ASCOM or pseudo-ASCOM (see <see cref="FileOC"/>) controllers. </remarks>
        protected virtual Type driverType { get; set; }
        
        /// <summary> Whether the controller is connected to the hardware. </summary>
        /// <remarks> This property needs to be overridden in subclasses that control non-ASCOM hardware. </remarks>
        protected virtual bool valid => driver?.Connected == true;
        private           bool _valid;
        
        /// <summary> Whether the controller is connected to the hardware. </summary>
        /// <remarks> A true value indicates that (generally) the hardware's properties are accessible and it can be
        /// operated. </remarks>
        [XmlIgnore] public bool Valid {
            get {
                bool toret;
                try { toret = valid; }
                catch { toret = false; }
                if (_valid && !toret) // was previously valid, but is now invalid
                    InvalidatedRaise();
                _valid = toret;
                return _valid;
            }
        }
        
        /// <summary> The type of hardware - Dome, Mount, etc. </summary>
        public abstract string Type { get; }
        
        /// <summary> The name of the connected hardware, taken from its driver. </summary>
        public virtual string Name => Valid ? driver?.Name : null;
        
        /// <summary> The id used to connect to the hardware. </summary>
        /// <remarks>This is set by the <see cref="Connect"/> method and used when reconnecting.</remarks>
        [XmlIgnore] public virtual string id { get; protected set; }
        
        /// <summary> Raised when the controller is connected to the hardware. </summary>
        public    event Action Connected;
        protected void         ConnectedRaise() => Connected?.Invoke();
        
        /// <summary> Handle the <see cref="Connected"/> event. </summary>
        /// <remarks> This method simply updates the <see cref="Name"/> property, but it can be overridden to perform more
        /// sophisticated actions.</remarks>
        protected void ConnectedHandle() { OnPropertyChanged(nameof(Name)); }

        /// <summary> Raised when the controller is disconnected. </summary>
        public    event Action Disconnected;
        protected void         DisconnectedRaise() => Disconnected?.Invoke();
        
        /// <summary> Handle the <see cref="Disconnected"/> event. </summary>
        /// <remarks> This method updates the properties, so that they reflect the disconnected status of the
        /// controller, but it can be overridden to perform more sophisticated actions. </remarks>
        protected void DisconnectedHandle() {
            OnPropertyChanged(nameof(Name));
            foreach (var prop in props)
                try { OnPropertyChanged(prop);} catch {}
        }
        
        /// <summary> Raised when the <see cref="Valid"/> property switches from true to false. </summary>
        public event Action Invalidated;
        public void         InvalidatedRaise() => Invalidated?.Invoke();

        
        /// <summary>
        /// Create a new instance and register the event handlers for connection, disconnection and property refresh.
        /// </summary>
        protected BaseHardwareControl() {
            Connected    += RefreshHandle;
            Connected    += ConnectedHandle;
            Disconnected += DisconnectedHandle;
            Refresh      += RefreshHandle;
        }

        /// <summary> Connect to the hardware. </summary>
        /// <remarks> This method needs to be overridden in subclasses that control non-ASCOM hardware. </remarks>
        /// <param name="init">Whether to initialize the hardware by calling <see cref="Initialize"/> after connection.</param>
        /// <param name="setup">Whether to call <see cref="AscomDriver.SetupDialog"/> before connection.</param>
        /// <returns>Whether the connection is successful (the value of driver.Connected).</returns>
        protected virtual async Task<bool> connect(bool init = true, bool setup = false) {
            if (setup) driver.SetupDialog();
            var ret = await Task.Run(() => {
                try {
                    driver.Connected = true;
                    return driver.Connected;
                } catch (ASCOM.DriverException) {
                    return false;
                }
            });

            if (init && ret)
                Initialize();

            return ret;
        }
        
        /// <summary> Connect to the hardware. </summary>
        /// <remarks>
        /// <para>The implementation here simply creates a new driver instance of the appropriate type using reflection
        /// and delegates the actual connection to the <see cref="connect"/> method.</para>
        /// <para>Classes controlling non-ASCOM hardware should override this method to avoid creating an AscomDriver!</para>
        /// </remarks>
        /// <param name="progID">The id to connect to</param>
        /// <param name="init">See <see cref="connect"/>.</param>
        /// <param name="setup">See <see cref="connect"/>.</param>
        /// <returns>False if <paramref name="progID"/> is empty of null and the result of <see cref="connect"/>
        /// otherwise.</returns>
        public virtual async Task<bool> Connect(string progID, bool init = true, bool setup = false) {
            if (string.IsNullOrEmpty(progID)) return false;
            id = progID;

            driver = (AscomDriver) Activator.CreateInstance(driverType, progID);
            return await connect(init, setup);
        }

        /// <summary> Disconnect from the hardware. </summary>
        /// <remarks> This method needs to be overridden in subclasses that control non-ASCOM hardware. </remarks>
        protected virtual Task disconnect() {
            return Task.Run(() => {
                if (Valid) {
                    driver.Connected = false;
                    driver.Dispose();
                    driver = null;
                }
            });
        }
        
        /// <summary> Disconnect from the hardware. </summary>
        /// <remarks> This method simply delegates to <see cref="disconnect"/> and raises the
        /// <see cref="Disconnected" /> event. </remarks>
        public virtual async Task Disconnect() {
            await disconnect();
            DisconnectedRaise();
        }
        
        /// <summary> Perform hardware-specific initialization logic. See <see cref="Initialize"/>. </summary>
        protected virtual void initialize() {}

        /// <summary> Initialize the class after the hardware has been connected. </summary>
        /// <remarks> Subclasses should override <see cref="initialize"/> in order to implement hardware-specific
        /// initialization.</remarks>
        protected void Initialize() {
            initialize();
            ConnectedRaise();
        }

        /// <summary> Ensures the device is disconnecting before destroying the controller. </summary>
        ~BaseHardwareControl() {
            Disconnect().Wait();
        }

        #region REFRESH
        
        /// <summary> Raised when a refresh of the properties is requested. See <see cref="RefreshHandle"/>. </summary>
        public event Action Refresh;
        
        /// <summary> Request a refresh of the properties. See <see cref="RefreshHandle"/>. </summary>
        public void RefreshRaise() => Refresh?.Invoke();

        /// <summary> The names of the properties to refresh. See <see cref="RefreshHandle"/>. </summary>
        /// <remarks> Each subclass should define an appropriate list of property names to be updated when requested. </remarks>
        protected virtual IEnumerable<string> props { get; } = new string[0];
        
        /// <summary> Perform hardware-specific refresh logic. See <see cref="RefreshHandle"/>. </summary>
        protected virtual void refresh() {}
        
        /// <summary> Handles a request to refresh the properties. </summary>
        /// <remarks> The preferred refreshing strategy is to implement the properties as simply retrieving the values
        /// from the driver in the getters, so that the driver is polled only when the property is actually required.
        /// On the other hand, a property "refresh" only signifies a notification that a new value might be available,
        /// and so is implemented by simply calling the <see cref="BaseNotify.OnPropertyChanged"/> method on the
        /// controller for each property in <see cref="props"/>, as well as the <see cref="Valid"/> property. In order
        /// to implement more sophisticated logic, subclasses should override <see cref="refresh"/>. 
        /// </remarks>
        protected void RefreshHandle() {
            refresh();
            
            OnPropertyChanged(nameof(Valid));
            if (Valid)
                foreach (var p in props)
                    OnPropertyChanged(p);
        }
        
        #endregion REFRESH
        
        #region POWER
        
        protected bool _hasPowerControl;
        /// <summary> Whether controlling the power of the hardware is currently available. </summary>
        public virtual bool HasPowerControl {
            get => _hasPowerControl;
            set {
                if (_hasPowerControl.Equals(value)) return;
                _hasPowerControl = value;
                OnPropertyChanged();
            }
        }

        private bool? _isPowerOn;
        /// <summary> Whether currently the hardware is powered. A null value indicates no information. </summary>
        public bool? IsPowerOn {
            get => _isPowerOn;
            set {
                if (_isPowerOn.Equals(value)) return;
                _isPowerOn = value;
                OnPropertyChanged();
            }
        }

        /// <summary> Helper function for controlling the power of the hardware. </summary>
        /// <param name="f">Arbitrary function to execute and await. It is passed the hardware controller and should
        /// return its <see cref="PowerStatus"/>.</param>
        /// <param name="desired">The desired power status after the operation.</param>
        /// <returns>Whether the current power status is the same as the desired one.</returns>
        /// <seealso cref="On"/><seealso cref="Off"/><seealso cref="Reset"/>
        private async Task<bool> power(Func<object, Task<PowerStatus>> f, bool desired) {
            if (HasPowerControl) {
                IsPowerOn = (await f(this)).On;
                return IsPowerOn == desired;
            } else return false;
        }

        /// <summary> Turn the controller's power on. </summary>
        /// <returns> Whether now <c>IsPowerOn == true</c>.</returns>
        public Task<bool> On() => power(O.Power.On, true);

        /// <summary> Turn the controller's power off. </summary>
        /// <returns> Whether now <c>IsPowerOn == false</c>.</returns>
        public Task<bool> Off() => power(O.Power.Off, false);

        /// <summary> Turn off, wait <paramref name="dt"/> and turn on the power of the hardware. </summary>
        /// <param name="dt">The interval to wait in the off state.</param>
        /// <returns>Whether now <c>IsPowerOn == true</c>.</returns>
        public Task<bool> Reset(TimeSpan? dt) => power(o => O.Power.Reset(o, dt), true);

        /// <summary> Restart the hardware, attempting to reconnect afterwards. </summary>
        /// <param name="resetTimeout">The time to wait in the off state (default 10s).</param>
        /// <param name="reconnectTimeout">The time to wait before attempting to reconnect (default 30s).</param>
        /// <returns>Whether the reboot was executed successfully.</returns>
        public async Task<bool> Reboot(TimeSpan? resetTimeout = null, TimeSpan? reconnectTimeout = null) {
            var progID = Valid ? id : null;

            if (Valid) await Disconnect();

            if (await Off()) {
                await Task.Delay(resetTimeout ?? TimeSpan.FromSeconds(10)); // TODO: Unhardcode
                if (await On()) {
                    if (progID == null) return true;
                    await Task.Delay(reconnectTimeout ?? TimeSpan.FromSeconds(30)); // TODO: Unhardcode!!!!!
                    return await Connect(progID);
                }
            }

            return false;
        }

        #endregion POWER
    }
}
