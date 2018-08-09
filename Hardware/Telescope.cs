﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Xml.Serialization;
using ASCOM.DeviceInterface;
using heliomaster_wpf.Annotations;
using heliomaster_wpf.Properties;

namespace heliomaster_wpf {
    public enum guidingMode {
        pulseGuide,
        moveAxis,
        incapable
    }

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class Telescope : BaseHardwareControl {
        public static readonly Dictionary<string, GuideDirections> dirs = new Dictionary<string, GuideDirections> {
            {"mountUp", GuideDirections.guideNorth},
            {"mountDown", GuideDirections.guideSouth},
            {"mountLeft", GuideDirections.guideEast},
            {"mountRight", GuideDirections.guideWest},
        };

        [XmlIgnore] protected override Type driverType => typeof(ASCOM.DriverAccess.Telescope);
        [XmlIgnore] public ASCOM.DriverAccess.Telescope Driver => (ASCOM.DriverAccess.Telescope) driver;

        public Telescope() {
//            InitializeScalers();
        }

        public bool CanMoveAxes;
        public bool CanPulseGuide => Valid && Driver.CanPulseGuide;


        private double _ratePrimary;
        public double RatePrimary {
            get => _ratePrimary;
            set {
                if (value.Equals(_ratePrimary)) return;
                _ratePrimary = value;
                OnPropertyChanged();
            }
        }

        private double _rateSecondary;
        public double RateSecondary {
            get => _rateSecondary;
            set {
                if (value.Equals(_rateSecondary)) return;
                _rateSecondary = value;
                OnPropertyChanged();
            }
        }

        public double Rate(GuideDirections dir) {
            return (dir == GuideDirections.guideEast || dir == GuideDirections.guideWest) ? RatePrimary : RateSecondary;
        }
        private static TelescopeAxes ax(GuideDirections dir) {
            return (dir == GuideDirections.guideEast || dir == GuideDirections.guideWest)
                ? TelescopeAxes.axisPrimary
                : TelescopeAxes.axisSecondary;
        }

        private int mult(GuideDirections dir) {
            return (dir == (IsFlipped
                        ? GuideDirections.guideSouth
                        : GuideDirections.guideNorth)
                    || dir == GuideDirections.guideWest)
                ? 1 : -1;
        }

        [XmlIgnore] private guidingMode                 gMode = guidingMode.incapable;
        [XmlIgnore] public  ObservableCollection<IRate> PrimaryAxisRates      { get; } = new ObservableCollection<IRate>();
        [XmlIgnore] public  ObservableCollection<IRate> SecondaryAxisRates    { get; } = new ObservableCollection<IRate>();
        [XmlIgnore] public  IRate                       SelectedPrimaryRate   { get; set; }
        [XmlIgnore] public  IRate                       SelectedSecondaryRate { get; set; }
        public int SelectedPrimaryRateIndex { get; set; }
        public int SelectedSecondaryRateIndex { get; set; }

        public override void Initialize() {
            CanMoveAxes = Driver.CanMoveAxis(TelescopeAxes.axisPrimary) &&
                          Driver.CanMoveAxis(TelescopeAxes.axisSecondary);
            if (Driver.CanPulseGuide) {
                gMode = guidingMode.pulseGuide;
            }

            if (CanMoveAxes) {
                foreach (IRate rate in Driver.AxisRates(TelescopeAxes.axisPrimary))
                    PrimaryAxisRates.Add(rate);
                SelectedPrimaryRate = PrimaryAxisRates[SelectedPrimaryRateIndex < PrimaryAxisRates.Count ? SelectedPrimaryRateIndex : 0];
                OnPropertyChanged(nameof(SelectedPrimaryRate));

                foreach (IRate rate in Driver.AxisRates(TelescopeAxes.axisSecondary))
                    SecondaryAxisRates.Add(rate);
                SelectedSecondaryRate = SecondaryAxisRates[SelectedSecondaryRateIndex < SecondaryAxisRates.Count ? SelectedSecondaryRateIndex : 0];
                OnPropertyChanged(nameof(SelectedSecondaryRate));

                gMode = guidingMode.moveAxis;
            }

            base.Initialize();
        }


        public void ControlMotion(GuideDirections dir, bool move=true) {
            if (move) {
                if (Moveable && gMode == guidingMode.moveAxis)
                    Driver.MoveAxis(ax(dir), mult(dir) * Rate(dir));
            } else {
                if (gMode == guidingMode.moveAxis && CanMoveAxes)
                    Driver.MoveAxis(ax(dir), 0);
            }
        }

        public void StopAllMotion(bool stopTracking = false) {
            if (CanMoveAxes) {
                Driver.MoveAxis(TelescopeAxes.axisPrimary,   0);
                Driver.MoveAxis(TelescopeAxes.axisSecondary, 0);
            }
            Driver.AbortSlew();
            if (stopTracking) Driver.Tracking = false;
        }


        public async void Slew(double ra, double dec) {
            if (CanSlew) {
                await Task.Run(() => {
                    var trackingState = Tracking;
                    Driver.Tracking = true;
                    Driver.SlewToCoordinates(ra, dec);
                    Driver.Tracking = trackingState;
                });
            }
        }

        public void GoTo(Pynder.Objects o = Pynder.Objects.Sun) {
            var coords = Pynder.find(o);
            Slew(coords.ra, coords.dec);
            Track(true);
        }

//        public void Unpark() {
//            if (CanPark) Task.Run(Driver.Unpark);
//        }
//
//        public void Park() {
//            if (CanPark) Task.Run((Action) Driver.Park);
////            if (CanPark) {
////                Driver.Park();
////                var stop = DateTime.Now + S.Mount.ParkTimeout;
////                while (DateTime.Now < stop && !AtPark)
////                    await Task.Delay(S.Settings.Refresh);
////                return AtPark;
////            } else return false;
//        }

        public Task HandlePark() {
            return Task.Run(() => {
                if (CanPark) {
                    if (Driver.AtPark)
                        Driver.Unpark();
                    else
                        Driver.Park();
                }
            });
            
        }

        public void Track(bool state) {
            if (CanTrack) Driver.Tracking = state;
        }


        public double AdjustDuration          { get; set; }
        public int    AdjustNTrials           { get; set; }
        public double AdjustToleranceDec      { get; set; }
        public double AdjustToleranceRaCosDec { get; set; }

        private double rcosphi => 1.0 / Math.Cos(Utilities.deg2rad(Declination));

        private double allowedRate(double r, TelescopeAxes axes) {
            var sign = Math.Sign(r);
            r = Math.Abs(r);
            var ret = 0.0;
            foreach (var rate in axes == TelescopeAxes.axisPrimary ? PrimaryAxisRates : SecondaryAxisRates)
                if (rate.Maximum <= r && ret < rate.Maximum)
                    ret = rate.Maximum;
            return sign * ret;
        }

        public Task Adjust(double dra, double ddec) {
            return Task.Run(() => {
                if (Moveable) {
                    var trackingState = Tracking;
                    Driver.Tracking = true;

                    var destRa  = RightAscension + dra;
                    var destDec = Declination + ddec;

                    var adjusted = true;
                    for (var i = 0; i < AdjustNTrials && adjusted; ++i) {
                        adjusted = false;

                        var offsetRa = Utilities.SymModulo(destRa - RightAscension, 360);
                        if (Math.Abs(offsetRa) * rcosphi > AdjustToleranceRaCosDec) {
                            Driver.MoveAxis(TelescopeAxes.axisPrimary,
                                            allowedRate(offsetRa / AdjustDuration, TelescopeAxes.axisPrimary));
                            adjusted = true;
                        }

                        var offsetDec = destDec - Declination;
                        if (Math.Abs(offsetDec) > AdjustToleranceDec) {
                            Driver.MoveAxis(TelescopeAxes.axisSecondary,
                                            allowedRate(offsetDec / AdjustDuration, TelescopeAxes.axisSecondary));
                            adjusted = true;
                        }

                        Task.Delay((int) (500 * AdjustDuration)); // Sleep for half of the time, then readjust.
                    }

                    Driver.MoveAxis(TelescopeAxes.axisPrimary,   0);
                    Driver.MoveAxis(TelescopeAxes.axisSecondary, 0);

                    Driver.Tracking = trackingState;
                }
            });
        }


        #region properties

        private bool   Moveable       => Valid && !AtPark && !Slewing;

        public  double SiderealTime   => Valid ? Driver.SiderealTime : double.NaN;
        public  double Altitude       => Valid ? Driver.Altitude : double.NaN;
        public  double Azimuth        => Valid ? Driver.Azimuth : double.NaN;
        public  double RightAscension => Valid ? Driver.RightAscension : double.NaN;
        public  double Declination    => Valid ? Driver.Declination : double.NaN;
        public  bool   AtPark         => Valid && Driver.AtPark;
        public  bool   Slewing        => Valid && Driver.Slewing;
        public  bool   Tracking       => Valid && Driver.Tracking;

        public PierSide SideOfPier       => Valid ? Driver.SideOfPier : PierSide.pierUnknown;
        private bool _nsflip;
        public bool NSFlip {
            get => _nsflip;
            set {
                if (value == _nsflip) return;
                _nsflip = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFlipped));
            }
        }
        public bool     IsFlipped        => !(NSFlip ^ (SideOfPier != PierSide.pierWest));

        public bool   CanTrack   => Moveable && Driver.CanSetTracking;
        public bool   CanSlew    => Moveable && Driver.CanSlew;
        public bool   CanGoTo    => CanSlew;
        public bool   CanPark    => Valid && Driver.CanPark && !Slewing;
        public string ParkAction => AtPark ? Resources.unpark : Resources.park;


        private static readonly string[] _props = {
            nameof(SiderealTime), nameof(Altitude), nameof(Azimuth), nameof(RightAscension), nameof(Declination),
            nameof(SideOfPier), nameof(IsFlipped),
            nameof(AtPark), nameof(Slewing), nameof(Tracking),
            nameof(CanTrack), nameof(CanSlew), nameof(CanGoTo), nameof(CanPark),
            nameof(ParkAction)
        };

        [XmlIgnore] protected override IEnumerable<string> props => _props;

        #endregion

        #region utilities

//        [XmlIgnore]
//        public Dictionary<string, Dictionary<string, Func<double, double>>> Scalers { get; } = new Dictionary<string, Dictionary<string, Func<double, double>>> {
//            {"primary", new Dictionary<string, Func<double, double>> {
//                {"slide2rate", null},
//                {"rate2slide", null}}},
//            {"secondary", new Dictionary<string, Func<double, double>> {
//                {"slide2rate", null},
//                {"rate2slide", null}}}
//        };
//
//        private void InitializeScalers() {
//            Scalers["primary"]["slider2rate"] = x => SliderToRate(x, SelectedPrimaryRate);
//            Scalers["primary"]["rate2slider"] = x => RateToSlider(x, SelectedPrimaryRate);
//            Scalers["secondary"]["slider2rate"] = x => SliderToRate(x, SelectedSecondaryRate);
//            Scalers["secondary"]["rate2slider"] = x => RateToSlider(x, SelectedSecondaryRate);
//        }
//
//
        public static Func<double, string> RateFormatter => r => Utilities.RateFormatter(r, "{0:0.#}") + "/s";
//
//        private static double SliderToRate(double x, IRate rate) {
//            if (rate == null) return double.NaN;
//            else return Utilities.ScaleLinToLog(x, Utilities.NonZero(rate.Minimum), Utilities.NonZero(rate.Maximum));
//        }
//        private static double RateToSlider(double Y, IRate rate) {
//            if (rate == null) return double.NaN;
//            else return Utilities.ScaleLogToLin(Y, Utilities.NonZero(rate.Minimum), Utilities.NonZero(rate.Maximum));
//        }

        #endregion

        public override string ToString() {
            var parkedstate = AtPark ? "parked" : "not parked";
            var slewstate   = Slewing ? "slewing" : "not slewing";
            var trackstate  = Tracking ? "tracking" : "not tracking";
            return
                $"Telescope, LST={SiderealTime}, Alt={Altitude}, Az={Azimuth}, RA={RightAscension}, Dec={Declination}, {parkedstate}, {slewstate}, {trackstate}";
        }
    }
}