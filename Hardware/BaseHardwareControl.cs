using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ASCOM.DriverAccess;
using heliomaster.Annotations;

namespace heliomaster {
    public abstract class BaseHardwareControl : BaseNotify {
        [XmlIgnore] protected AscomDriver driver;
        protected abstract Type driverType { get; }

        protected bool _hasPowerControl;
        public virtual bool HasPowerControl {
            get => _hasPowerControl;
            set {
                if (_hasPowerControl.Equals(value)) return;
                _hasPowerControl = value;
                OnPropertyChanged();
            }
        }

        private bool? _isPowerOn;
        public bool? IsPowerOn {
            get => _isPowerOn;
            set {
                if (_isPowerOn.Equals(value)) return;
                _isPowerOn = value;
                OnPropertyChanged();
            }
        }

        private async Task<bool> power(Func<object, Task<PowerStatus>> f, bool desired) {
            if (HasPowerControl) {
                IsPowerOn = (await f(this)).On;
                return IsPowerOn == desired;
            } else return false;
        }
        public Task<bool> On() => power(O.Power.On, true);
        public Task<bool> Off() => power(O.Power.Off, false);
        public Task<bool> Reset(TimeSpan? dt) => power(o => O.Power.Reset(o, dt), true);

        public async Task<bool> Reboot(TimeSpan? timeout = null) {
            var progID = Valid ? id : null;

            if (Valid) await Disconnect();

            if (await Off()) {
                await Task.Delay(timeout ?? TimeSpan.FromSeconds(10)); // TODO: Unhardcode
                if (await On()) {
                    if (progID == null) return true;
                    await Task.Delay(TimeSpan.FromSeconds(30)); // TODO: Unhardcode!!!!!
                    return await Connect(progID);
                }
            }

            return false;
        }

        public abstract string Type { get; }
        public virtual string Name => Valid ? driver?.Name : null;

        public event Action Connected;
        public void ConnectedRaise() => Connected?.Invoke();
        protected void ConnectedHandle() {
            OnPropertyChanged(nameof(Name));
        }

        public event Action Disconnected;
        public void DisconnectedRaise() => Disconnected?.Invoke();
        protected void DisconnectedHandle() {
            OnPropertyChanged(nameof(Name));
        }
        public event Action Invalidated;
        public void InvalidatedRaise() => Invalidated?.Invoke();

        protected virtual bool valid => driver?.Connected == true;
        private bool _valid;
        [XmlIgnore] public bool Valid {
            get {
                bool toret;
                try { toret = valid; }
                catch { toret = false; }
                if (_valid && !toret)
                    InvalidatedRaise();
                _valid = toret;
                return _valid;
            }
        }

        protected BaseHardwareControl() {
            Connected    += RefreshHandle;
            Connected    += ConnectedHandle;
            Disconnected += DisconnectedHandle;
            Refresh      += RefreshHandle;
        }

        protected virtual async Task<bool> connect(bool init = true, bool setup = false) {
            if (setup) driver.SetupDialog();
            var ret = await Task.Run(() => {
                try {
                    driver.Connected = true;
                    return driver.Connected == true;
                } catch (ASCOM.DriverException) {
                    return false;
                }
            });

            if (init && ret)
                Initialize();

            return ret;
        }

        [XmlIgnore] public virtual string id { get; protected set; }

        public virtual async Task<bool> Connect(string progID, bool init = true, bool setup = false) {
            if (string.IsNullOrEmpty(progID)) return false;
            id = progID;

            driver = (AscomDriver) Activator.CreateInstance(driverType, progID);
            return await connect(init, setup);
        }

        protected virtual Task disconnect() {
            return Task.Run(() => {
                if (Valid) {
                    driver.Connected = false;
                    driver.Dispose();
                    driver = null;
                }
            });
        }
        public virtual async Task Disconnect() {
            await disconnect();
            DisconnectedRaise();
        }

        public virtual void Initialize() {
            ConnectedRaise();
        }

        ~BaseHardwareControl() {
            Disconnect().Wait();
        }

        #region ISyncToDriver

        public event Action Refresh;
        public void RefreshRaise() => Refresh?.Invoke();

        protected virtual IEnumerable<string> props { get; } = new string[0];
        protected virtual void RefreshHandle() {
            OnPropertyChanged(nameof(Valid));
            if (Valid)
                foreach (var p in props)
                    OnPropertyChanged(p);
        }

        #endregion
    }
}
