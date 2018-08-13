using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ASCOM.DeviceInterface;

namespace heliomaster_wpf {
    public class Dome : BaseHardwareControl {
        public static readonly Dictionary<ShutterState, string> ShutterStateStrings = new Dictionary<ShutterState, string>() {
            {ShutterState.shutterOpen, "open"},
            {ShutterState.shutterClosed, "closed"},
            {ShutterState.shutterOpening, "opening"},
            {ShutterState.shutterClosing, "closing"},
            {ShutterState.shutterError, "error"}
        };

        [XmlIgnore] protected override Type driverType => typeof(ASCOM.DriverAccess.Dome);
        [XmlIgnore] public ASCOM.DriverAccess.Dome Driver => (ASCOM.DriverAccess.Dome) driver;

        private double _homePosition;
        public double HomePosition {
            get => _homePosition;
            set {
                if (value.Equals(_homePosition)) return;
                _homePosition = value;
                OnPropertyChanged();
            }
        }

        private double _parkPosition;
        public double ParkPosition {
            get => _parkPosition;
            set {
                if (value.Equals(_parkPosition)) return;
                _parkPosition = value;
                OnPropertyChanged();
            }
        }

        private bool _homeToOpen;
        public bool HomeToOpen {
            get => _homeToOpen;
            set {
                if (value == _homeToOpen) return;
                _homeToOpen = value;
                OnPropertyChanged();
            }
        }

        private bool _retryClose;
        public bool RetryClose {
            get => _retryClose;
            set {
                if (value == _retryClose) return;
                _retryClose = value;
                OnPropertyChanged();
            }
        }

        #region properties

        public bool Moveable => Valid && !Slaved && !Slewing;

        public double Azimuth => Valid ? Driver.Azimuth : double.NaN;
        public bool   AtHome  => Valid && Driver.AtHome;
        public bool   AtPark  => Valid && Driver.AtPark;
        public bool   Slewing => Valid && Driver.Slewing;
        public bool   Slaved  => Valid && Driver.Slaved;

        public bool CanShutter => Valid && Driver.CanSetShutter;
        public bool CanSlave   => Valid && Driver.CanSlave;
        public bool CanPark    => Moveable && Driver.CanPark && !AtPark;
        public bool CanHome    => Moveable && Driver.CanFindHome && !AtHome;

        private static readonly string[] _props = {
            nameof(Azimuth),
            nameof(AtHome), nameof(AtPark), nameof(Slewing), nameof(Slaved),
            nameof(CanShutter), nameof(CanSlave), nameof(CanPark), nameof(CanHome),
            nameof(Moveable)
        };
        protected override IEnumerable<string> props => _props;

        #endregion

        #region MOTION

        private void slew(double az) {
            if (Moveable)
                Driver.SlewToAzimuth(Utilities.PositiveModulo(az, 360));
        }

        public async Task<bool> Slew(double arg, bool absolute = false) {
            try {
                slew(absolute ? arg : Azimuth + arg);
                await Task.Run(() => SpinWait.SpinUntil(() => !Slewing));
                return true;
            } catch { return false; }
            finally { RefreshRaise(); }
        }

        public Task<bool> StopAllMotion() {
            return Task<bool>.Factory.StartNew(() => {
                try { Driver.AbortSlew(); return true; }
                catch { return false; }
                finally { RefreshRaise(); }
            });
        }

        public Task<bool> HomeOrPark(bool home) {
            return Task<bool>.Factory.StartNew(() => {
                try {
                    if (home) Driver.Park();
                    else Driver.FindHome();
                    return home ? AtHome : AtPark;
                } catch {
                    var t = Slew(home ? HomePosition : ParkPosition);
                    t.Wait();
                    return t.Exception != null && t.Result;
                } finally { RefreshRaise(); }
            });
        }

        public Task<bool> Slave(bool state) {
            return Task<bool>.Factory.StartNew(() => {
                try { Driver.Slaved = state; } catch {}
                RefreshRaise();
                return Driver.Slaved == state;
            });
        }

        public void Shutter(bool open) {
            if (Driver.CanSetShutter)
                Task.Run(open ? (Action) Driver.OpenShutter : (Action) Driver.CloseShutter);
        }

        public Task<bool> SmartShutter(bool open) {
            throw new NotImplementedException();
        }

        #endregion

        public override string ToString() {
            var shutstate = Valid ? ShutterStateStrings[Driver.ShutterStatus] : null;
            var homestate = AtHome ? "ät home" : "not at home";
            var parkstate = AtPark ? "parked" : "not parked";
            var slewstate = Slewing ? "slewing" : "not slewing";
            var slavestate = Slaved ? "slaved" : "not slaved";
            return $"Dome, Az={Azimuth}, {shutstate}, {homestate}, {parkstate}, {slewstate}, {slavestate}";
        }
    }
}
