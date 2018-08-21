using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Xml.Serialization;
using ASCOM.DeviceInterface;
using heliomaster.Properties;
using heliomaster.Annotations;

namespace heliomaster {
    public enum guidingMode {
        pulseGuide,
        moveAxis,
        incapable
    }

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class Telescope : BaseHardwareControl {
        public static readonly Dictionary<string, GuideDirections> dirs = new Dictionary<string, GuideDirections> {
            {"up", GuideDirections.guideNorth},
            {"down", GuideDirections.guideSouth},
            {"mountLeft", GuideDirections.guideEast},
            {"mountRight", GuideDirections.guideWest},
        };

        [XmlIgnore] protected override Type driverType => typeof(ASCOM.DriverAccess.Telescope);
        [XmlIgnore] public ASCOM.DriverAccess.Telescope Driver => driver as ASCOM.DriverAccess.Telescope;

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

        private int _selectedPrimaryRateIndex;
        public int SelectedPrimaryRateIndex {
            get => _selectedPrimaryRateIndex;
            set {
                if (value == _selectedPrimaryRateIndex) return;
                _selectedPrimaryRateIndex = value;
                OnPropertyChanged();
            }
        }

        private int _selectedSecondaryRateIndex;
        public int SelectedSecondaryRateIndex {
            get => _selectedSecondaryRateIndex;
            set {
                if (value == _selectedSecondaryRateIndex) return;
                _selectedSecondaryRateIndex = value;
                OnPropertyChanged();
            }
        }

        public override void Initialize() {


            CanMoveAxes = Driver.CanMoveAxis(TelescopeAxes.axisPrimary) &&
                          Driver.CanMoveAxis(TelescopeAxes.axisSecondary);
            if (Driver.CanPulseGuide) {
                gMode = guidingMode.pulseGuide;
            }

            if (CanMoveAxes) {
                foreach (IRate rate in Driver.AxisRates(TelescopeAxes.axisPrimary))
                    PrimaryAxisRates.Add(rate);
                foreach (IRate rate in Driver.AxisRates(TelescopeAxes.axisSecondary))
                    SecondaryAxisRates.Add(rate);

                gMode = guidingMode.moveAxis;
            }

            base.Initialize();
        }


        #region MOTION

        public event Action Slewed;
        public void SlewedRaise() => Slewed?.Invoke();

        public void ControlMotion(GuideDirections dir, bool move=true) {
            if (move) {
                if (Moveable && gMode == guidingMode.moveAxis)
                    Driver.MoveAxis(ax(dir), mult(dir) * Rate(dir));
            } else {
                if (gMode == guidingMode.moveAxis)
                    Driver.MoveAxis(ax(dir), 0);
                SlewedRaise();
            }
        }

        public void StopAllMotion(bool stopTracking = false) {
            if (Valid) {
                if (CanMoveAxes) {
                    Driver.MoveAxis(TelescopeAxes.axisPrimary,   0);
                    Driver.MoveAxis(TelescopeAxes.axisSecondary, 0);
                }
                Driver.AbortSlew();
                if (stopTracking) Driver.Tracking = false;

                SlewedRaise();
            }
        }


        public Task<bool> Slew(double ra, double dec) {
            return Task<bool>.Factory.StartNew(() => {
                if (CanSlew) {
                    try {
                        var trackingState = Tracking;
                        Driver.Tracking = true;
                        Driver.SlewToCoordinates(ra, dec);
                        if (trackingState != null)
                            Driver.Tracking = (bool) trackingState;

                        SlewedRaise();
                        return true;
                    } catch {}
                }
                return false;
            });
        }

        public async Task<bool> GoTo(Pynder.Objects o = Pynder.Objects.Sun) {
            var coords = Pynder.find(o);
            return await Slew(coords.ra, coords.dec) && await Track(true);
        }

        public Task<bool> Park() {
            return Task<bool>.Factory.StartNew(() => {
                try {
                    if (CanPark) {
                        Track(false).Wait();
                        Driver.Park();
                    }
                } catch {}
                return Valid && AtPark==true;
            });
        }
        public Task<bool> Unpark() {
            return Task<bool>.Factory.StartNew(() => {
                try { if (CanPark) Driver.Unpark(); } catch {}
                return Valid && AtPark==false;
            });
        }

        #endregion


        public Task<bool> Track(bool state) {
            return Task<bool>.Factory.StartNew(() => {
                try { if (CanTrack) Driver.Tracking = state; } catch {}
                return Tracking == state;
            });
        }


        #region ADJUST

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

                    if (trackingState != null)
                        Driver.Tracking = (bool) trackingState;
                }
            });
        }

        #endregion

        #region PROPERTIES

        public override string Type => Resources.mount;

        public  bool   Moveable       => Valid && AtPark==false && Slewing==false;

        public double SiderealTime {
            get {
                if (Valid)
                    try {return Driver.SiderealTime;}
                    catch {return double.NaN;}
                else return double.NaN;
            }
        }
        public double Altitude {
            get {
                if (Valid)
                    try {return Driver.Altitude;}
                    catch {return double.NaN;}
                else return double.NaN;
            }
        }
        public double Azimuth {
            get {
                if (Valid)
                    try {return Driver.Azimuth;}
                    catch {return double.NaN;}
                else return double.NaN;
            }
        }
        public double RightAscension {
            get {
                if (Valid)
                    try {return Driver.RightAscension;}
                    catch {return double.NaN;}
                else return double.NaN;
            }
        }
        public double Declination {
            get {
                if (Valid)
                    try {return Driver.Declination;}
                    catch {return double.NaN;}
                else return double.NaN;
            }
        }
        public bool? AtPark {
            get {
                if (Valid)
                    try {return Driver.AtPark;}
                    catch {return null;}
                else return null;
            }
        }
        public bool? Slewing {
            get {
                if (Valid)
                    try {return Driver.Slewing;}
                    catch {return null;}
                else return null;
            }
        }
        public bool? Tracking {
            get {
                if (Valid)
                    try {return Driver.Tracking;}
                    catch {return null;}
                else return null;
            }
        }

        public bool   CanTrack   => Moveable && Driver.CanSetTracking;
        public bool   CanSlew    => Moveable && Driver.CanSlew;
        public bool   CanGoTo    => CanSlew;
        public bool   CanPark    => Valid && Driver.CanPark && Slewing==false;
        public string ParkAction => AtPark==true ? Resources.unpark : Resources.park;


        public event Action FlippedChanged;
        protected void FlippedChangedRaise() { FlippedChanged?.Invoke(); }
        private PierSide _sideOfPier = PierSide.pierUnknown;

        public PierSide SideOfPier {
            get {
                try {
                    return Driver.SideOfPier;
                } catch {
                    return PierSide.pierUnknown;
                }
            }
        }

        private bool _nsflip;
        public bool NSFlip {
            get => _nsflip;
            set {
                if (value == _nsflip) return;
                _nsflip = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFlipped));
                FlippedChangedRaise();
            }
        }
        public bool IsFlipped => Valid && !(NSFlip ^ (SideOfPier != PierSide.pierWest));


        private static readonly string[] _props = {
            nameof(SiderealTime), nameof(Altitude), nameof(Azimuth), nameof(RightAscension), nameof(Declination),
            nameof(AtPark), nameof(Slewing), nameof(Tracking),
            nameof(CanTrack), nameof(CanSlew), nameof(CanGoTo), nameof(CanPark),
            nameof(Moveable),
            nameof(ParkAction)
        };

        protected override void RefreshHandle() {
            var sop = SideOfPier;
            if (!_sideOfPier.Equals(sop)) {
                _sideOfPier = sop;
                OnPropertyChanged(nameof(SideOfPier));
                OnPropertyChanged(nameof(IsFlipped));
                FlippedChangedRaise();
            }

            base.RefreshHandle();
        }

        [XmlIgnore] protected override IEnumerable<string> props => _props;

        #endregion

        public override string ToString() {
            var parkedstate = AtPark==true ? "parked" : "not parked";
            var slewstate   = Slewing==true ? "slewing" : "not slewing";
            var trackstate  = Tracking==true ? "tracking" : "not tracking";
            return
                $"Telescope, LST={SiderealTime}, Alt={Altitude}, Az={Azimuth}, RA={RightAscension}, Dec={Declination}, {parkedstate}, {slewstate}, {trackstate}";
        }
    }
}
