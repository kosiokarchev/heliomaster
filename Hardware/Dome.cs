﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Xml.Serialization;
using ASCOM.DeviceInterface;
using heliomaster.Properties;
using Renci.SshNet.Messages.Authentication;
using SmartFormat;

namespace heliomaster {
    public class Dome : BaseHardwareControl {
        public static readonly Dictionary<ShutterState, string> ShutterStateStrings = new Dictionary<ShutterState, string>() {
            {ShutterState.shutterOpen, "open"},
            {ShutterState.shutterClosed, "closed"},
            {ShutterState.shutterOpening, "opening"},
            {ShutterState.shutterClosing, "closing"},
            {ShutterState.shutterError, "error"}
        };

        [XmlIgnore] protected override Type driverType => typeof(ASCOM.DriverAccess.Dome);
        [XmlIgnore] public ASCOM.DriverAccess.Dome Driver => driver as ASCOM.DriverAccess.Dome;

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

        public override string Type => Resources.dome;

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
            if (Moveable) {
                var target = Utilities.PositiveModulo(az, 360);
                Logger.debug($"Slewing: target={target}");
                Driver.SlewToAzimuth(target);
            } else throw new ASCOM.InvalidOperationException("Cannot slew dome.");
        }

        public Task<bool> Slew(double arg, bool absolute = false) {
            Logger.info($"Slewing to {(absolute ? "absolute" : "relative")} {arg}.");

            return Task<bool>.Factory.StartNew(() => {
                try {
                    slew(absolute ? arg : Azimuth + arg);
                    SpinWait.SpinUntil(() => Slewing, new TimeSpan(0, 0, 100)); // TODO: Unhardcode
                    SpinWait.SpinUntil(() => !Slewing);
                    Logger.info("Slewing complete.");
                    return true;
                } catch (Exception e) {
                    Logger.error(e.Message);
                    Logger.warning("Slewing failed.");
                    return false;
                }
                finally { RefreshRaise(); }
            });
        }

        public Task<bool> StopAllMotion() {
            Logger.info("Stopping all motion.");

            return Task<bool>.Factory.StartNew(() => {
                try {
                    Driver.AbortSlew();
                    Logger.info("AbortSlew complete.");
                    return true;
                }
                catch {
                    Logger.warning("AbortSlew failed.");
                    return false;
                }
                finally { RefreshRaise(); }
            });
        }

        public Task<bool> HomeOrPark(bool home) {
            var action = home ? "Slewing to home" : "Parking";
            Logger.info(action+".");

            return Task<bool>.Factory.StartNew(() => {
                try {
                    if (home) Driver.FindHome();
                    else Driver.Park();
                    var timeout = TimeSpan.FromSeconds(100); // TODO: Unhardcode
                    SpinWait.SpinUntil(() => (home && Driver.AtHome) || (!home && Driver.AtPark), timeout);
                    Logger.info($"Slewing stopped or timeout {timeout.TotalSeconds}s expired.");
                    return home ? AtHome : AtPark;
                } catch {
                    Logger.debug($"Attempting software {(home ? "slew to home" : "park")}.");
                    var t = Slew(home ? HomePosition : ParkPosition);
                    t.Wait();
                    if (t.Exception != null || !t.Result) {
                        Logger.warning($"{action} failed.");
                        return false;
                    } else {
                        Logger.info($"{action} complete.");
                        return true;
                    }
                } finally { RefreshRaise(); }
            });
        }

        public Task<bool> Slave(bool state) {
            var action = (state ? "Slaving " : "Unslaving") + " via hardware";
            Logger.info($"{action}.");

            return Task<bool>.Factory.StartNew(() => {
                try {
                    Driver.Slaved = state;
                } catch (Exception e) {
                    Logger.error(e.Message);
                }
                RefreshRaise();

                var ret = Driver.Slaved == state;
                if (!ret) Logger.warning($"{action} failed.");
                else      Logger.info($"{action} complete.");
                return ret;
            });
        }

        public Task<bool> Shutter(bool open) {
            var action = (open ? "Opening" : "Closing") + " shutter";
            Logger.info($"{action}.");

            return Task<bool>.Factory.StartNew(() => {
                var state = open ? ShutterState.shutterOpen : ShutterState.shutterClosed;
                try {
                    if (open) Driver.OpenShutter();
                    else Driver.CloseShutter();
                    SpinWait.SpinUntil(() => !Driver.Slewing && Driver.ShutterStatus == state, S.Dome.ShutterTimeout);
                } catch (Exception e) {
                    Logger.error(e.Message);
                }
                RefreshRaise();

                var ret = Driver.ShutterStatus == state;
                if (!ret) Logger.warning($"{action} failed.");
                else      Logger.info($"{action} complete.");
                return ret;
            });
        }

        public async Task<bool> ShutterWithHome(bool open, bool tryreboot = true) {
            var prefix = $"DOME: ShutterWithHome({open}): ";
            while (true) {
                if (!HomeToOpen || await HomeOrPark(home: true)) {
                    var ret = await Shutter(open);
                    Logger.info(prefix + (ret ? "success" : "failed: cannot control shutter"));
                    return ret;
                }

                if (tryreboot) {
                    Logger.info(prefix + "rebooting");
                    if (await Reboot()) {
                        tryreboot = false;
                        continue;
                    } else Logger.info(prefix + "rebooting failed");
                }

                Logger.info(prefix + "failed: cannot go to home");
                throw new Exception(prefix + "cannot go to home");
            }
        }

        public async Task<bool> SmartShutter(bool open) {
            var prefix = $"DOME: SmartShutter({open}): ";
            Logger.info(prefix + "starting");
            try {
                if (await ShutterWithHome(open)) goto success;

                if (HomeToOpen) {
                    Logger.info(prefix + "trying to re-go to home");
                    if (await Slew(20) && await SmartShutter(open)) goto success;
                }

                Logger.info(prefix + "trying to reboot");
                if (await Reboot()) {
                    Logger.info(prefix + "reboot successful");
                    if (await ShutterWithHome(open)) goto success;
                }

                Logger.info(prefix + "failed");
            } catch (Exception e) {
                Logger.info(prefix + $"failed due to error: {e.GetType().Name}: {e.Message}");
                return false;
            }

            success:
            Logger.info(prefix + "success");
            return true;
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
