using System;
using System.Collections.Generic;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ASCOM;
using ASCOM.DeviceInterface;
using heliomaster_wpf.Properties;
using Microsoft.SqlServer.Server;

namespace heliomaster_wpf {
    public class Dome : BaseHardwareControl {
        public static readonly Dictionary<ShutterState, string> ShutterStateStrings = new Dictionary<ShutterState, string>() {
            {ShutterState.shutterOpen, "open"},
            {ShutterState.shutterClosed, "closed"},
            {ShutterState.shutterOpening, "opening"},
            {ShutterState.shutterClosing, "closing"},
            {ShutterState.shutterError, "error"}
        };

        protected override Type driverType => typeof(ASCOM.DriverAccess.Dome);
        public ASCOM.DriverAccess.Dome Driver => (ASCOM.DriverAccess.Dome) driver;

        public bool Moveable => Valid && !Slaved && !Slewing;

        #region properties

        public double Azimuth => Valid ? Driver.Azimuth : double.NaN;
        public bool   AtHome  => Valid && Driver.AtHome;
        public bool   AtPark  => Valid && Driver.AtPark;
        public bool   Slewing => Valid && Driver.Slewing;
        public bool   Slaved  => Valid && Driver.Slaved;

        public string ShutterStatusString => Valid ? ShutterStateStrings[Driver.ShutterStatus] : null;

        public bool CanShutter => Valid && Driver.CanSetShutter;
        public bool CanSlave   => Valid && Driver.CanSlave;
        public bool CanPark    => Moveable && Driver.CanPark && !AtPark;
        public bool CanHome    => Moveable && Driver.CanFindHome && !AtHome;

//        public string SlaveAction => Slaved ? Resources.unslave : Resources.slave;

        private static readonly string[] _props = {
            nameof(Azimuth), nameof(ShutterStatusString),
            nameof(AtHome), nameof(AtPark), nameof(Slewing), nameof(Slaved),
            nameof(CanShutter), nameof(CanSlave), nameof(CanPark), nameof(CanHome)
        };
        protected override IEnumerable<string> props => _props;

        #endregion

        public override void Initialize() {
            OnConnected();
        }

        #region MOTION

        public Task PulseMove(double offset) {
            return Task.Run(() => {
                if (Moveable)
                    Driver.SlewToAzimuth(Azimuth + offset);
            });
        }




        public enum MotionState {
            Stopped, Movingleft, MovingRight
        }
        private MotionState _mState = MotionState.Stopped;
        private MotionState MState {
            get => _mState;
            set { _mState = value; OnPropertyChanged(); OnPropertyChanged(nameof(MStateString)); }
        }
        public string MStateString {
            get {
                switch (MState) {
                    case MotionState.Movingleft:
                        return "left";
                    case MotionState.Stopped:
                        return "still";
                    case MotionState.MovingRight:
                        return "right";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private double slewGoal =>
            MState == MotionState.MovingRight ? (Azimuth + 90) % 360 :
            MState == MotionState.Movingleft  ? (Azimuth < 90 ? Azimuth + 270 : Azimuth - 90) % 360 : 0;
        private Task motionTask;

        public async void SetInMotion(MotionState dir) {
            await StopAllMotion();
            if (Moveable) {
                MState = dir;
                motionTask = Task.Run(() => {
                    while (MState != MotionState.Stopped) {
                        Driver.SlewToAzimuth(slewGoal);
                        while (Driver.Slewing) Task.Delay(100);
                    }
                });
            }

            Refresh();
        }

        public async Task StopAllMotion() {
            MState = MotionState.Stopped;
            if (true || Slewing) {
                Driver.AbortSlew();
            }
            if (motionTask != null) {
                await motionTask;
                motionTask = null;
            }

            Refresh();
        }

        #endregion

        public void Park() {
            if (CanPark) Task.Run((Action) Driver.Park);
        }

        public void Home() {
            if (CanHome) Task.Run((Action) Driver.FindHome);
        }

        public void Slave(bool state) {
            if (CanSlave) Driver.Slaved = state;
        }

        public void Shutter(bool open) {
            if (Driver.CanSetShutter)
                Task.Run(open ? (Action) Driver.OpenShutter : (Action) Driver.CloseShutter);
        }

        public override string ToString() {
            var shutstate = ShutterStatusString;
            var homestate = AtHome ? "ät home" : "not at home";
            var parkstate = AtPark ? "parked" : "not parked";
            var slewstate = Slewing ? "slewing" : "not slewing";
            var slavestate = Slaved ? "slaved" : "not slaved";
            return $"Dome, Az={Azimuth}, {shutstate}, {homestate}, {parkstate}, {slewstate}, {slavestate}";
        }
    }
}
