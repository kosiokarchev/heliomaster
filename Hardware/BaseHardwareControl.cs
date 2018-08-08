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

        public event Action Connected;
        protected void OnConnected() { Refresh(); Connected?.Invoke(); }

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

        public abstract void Initialize();

        public virtual bool Valid => driver != null && driver.Connected;

        #region ISyncToDriver

        protected virtual IEnumerable<string> props { get; } = new string[0];
        public virtual void Refresh() {
            OnPropertyChanged(nameof(Valid));
            if (Valid)
                foreach (var p in props)
                    OnPropertyChanged(p);
        }

        #endregion
    }
}
