using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ASCOM.DriverAccess;
using heliomaster_wpf.Annotations;

namespace heliomaster_wpf {
    public abstract class BaseHardwareControl : BaseNotify {
        [XmlIgnore] protected AscomDriver driver;
        protected abstract Type driverType { get; }

        protected async Task<bool> connect(bool state = true, bool init = true, bool setup = false) {
            if (setup) driver.SetupDialog();
            var ret = await Task.Run(() => {
                try {
                    driver.Connected = state;
                    return driver.Connected == state;
                } catch (ASCOM.DriverException) {
                    return false;
                }
            });

            if (init && state && ret)
                Initialize();

            return ret;
        }

        public virtual async Task<bool> Connect(string progID, bool state = true, bool init = true, bool setup = false) {
            if (string.IsNullOrEmpty(progID)) return false;

            driver = (AscomDriver) Activator.CreateInstance(driverType, progID);
            return await connect(state, init, setup);
        }

        public virtual void Initialize() {
            ConnectedRaise();
        }

        protected virtual bool valid => driver != null && driver.Connected;
        [XmlIgnore] public bool Valid {
            get {
                try { return valid; }
                catch { return false; }
            }
        }

        #region ISyncToDriver

        public event Action Refresh;
        public void RefreshRaise() => Refresh?.Invoke();
        public event Action Connected;
        public void ConnectedRaise() => Connected?.Invoke();
        public event Action Disconnected;
        public void DisconnectedRaise() => Disconnected?.Invoke();

        protected BaseHardwareControl() {
            Connected += RefreshHandle;
            Refresh += RefreshHandle;
        }


        protected virtual IEnumerable<string> props { get; } = new string[0];
        protected virtual void RefreshHandle() {
            OnPropertyChanged(nameof(Valid));
            if (Valid)
                foreach (var p in props)
                    OnPropertyChanged(p);
            else DisconnectedRaise();
        }

        #endregion
    }
}
