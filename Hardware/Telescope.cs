using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ASCOM.DeviceInterface;
using heliomaster.Properties;

namespace heliomaster {
    /// <summary> A телесцопе моунт controlled via its ASCOM interface. </summary>
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class Telescope : BaseHardwareControl {
        [XmlIgnore] protected override Type driverType => typeof(ASCOM.DriverAccess.Telescope);
        [XmlIgnore] public ITelescopeV3 Driver => driver as ASCOM.DriverAccess.Telescope;
        public override string Type => Resources.mount;
        
        #region PROPERTIES

        /// <summary>
        /// See <see cref="ITelescopeV3.SiderealTime"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public double? SiderealTime {
            get {
                if (Valid)
                    try {return Driver.SiderealTime;}
                    catch {return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.Altitude"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public double? Altitude {
            get {
                if (Valid) try {return Driver.Altitude;} catch {return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.Azimuth"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public double? Azimuth {
            get {
                if (Valid) try { return Driver.Azimuth; } catch { return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.RightAscension"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public double? RightAscension {
            get {
                if (Valid) try { return Driver.RightAscension; } catch { return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.Declination"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public double? Declination {
            get {
                if (Valid) try { return Driver.Declination; } catch { return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.AtPark"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public bool? AtPark {
            get {
                if (Valid)
                    try {return Driver.AtPark;}
                    catch {return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.Slewing"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public bool? Slewing {
            get {
                if (Valid)
                    try {return Driver.Slewing;}
                    catch {return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.Tracking"/>
        /// </summary>
        /// <remarks> A null value indicates that the hardware is not connected. </remarks>
        public bool? Tracking {
            get {
                if (Valid)
                    try {return Driver.Tracking;}
                    catch {return null;}
                else return null;
            }
        }
        /// <summary>
        /// See <see cref="ITelescopeV3.SideOfPier"/>
        /// </summary>
        /// <remarks> If the hardware is disconnected, this returns <see cref="PierSide.pierUnknown"/>. </remarks>
        /// <seealso cref="IsFlipped"/>
        public PierSide SideOfPier {
            get { try { return Driver.SideOfPier; } catch { return PierSide.pierUnknown; } }
        }
        
        /// <summary> Whether the mount can currently be set in motion. </summary>
        /// <remarks> Checks whether the hardware is connected and whether it is not currently parked or moving. </remarks>
        public bool Moveable => AtPark==false && Slewing==false;

        /// <summary>
        /// Whether the mount's tracking function can currently be controlled.
        /// </summary>
        /// <remarks> Checks whether tracking is supported and the mount is <see cref="Moveable"/>. </remarks>
        public bool CanTrack => Moveable && Driver.CanSetTracking;
        
        /// <summary>
        /// Whether the mount can currently be slewed.
        /// </summary>
        /// <remarks> Checks whether slewing is supported and the mount is <see cref="Moveable"/>. </remarks>
        public bool CanSlew => Moveable && Driver.CanSlew;
        
        /// <summary>
        /// Whether the mount can currently be pointed at an object.
        /// </summary>
        /// <remarks> Currently equivalent to <see cref="CanSlew"/> </remarks>
        public bool CanGoTo => CanSlew;
        
        /// <summary>
        /// Whether the mount can currently be parked or unparked.
        /// </summary>
        /// <remarks> Checks whether the hardware is connected, the driver supports parking, and the mount is not
        /// slewing. </remarks>
        public bool CanPark => Valid && Driver.CanPark && Slewing==false;
        
        /// <summary>
        /// A string indicating the opposite of the current park state for display purposes.
        /// </summary>
        public string ParkAction => AtPark==true ? Resources.unpark : Resources.park;
        // TODO: do away with the need for this property


        /// <summary>
        /// Raised when the <see cref="IsFlipped"/> property might have changed.
        /// </summary>
        public event Action FlippedChanged;
        protected void FlippedChangedRaise() { FlippedChanged?.Invoke(); }
        private PierSide _sideOfPier = PierSide.pierUnknown;

        private bool _nsflip;
        /// <summary>
        /// Whether a positive motion along the declination axis would move the pointing north or south when the
        /// telescope is east of the pier.
        /// </summary>
        /// <remarks> Since this value depends on the physical placement of the telescope on the mount (or for that
        /// matter simply on which side of the tube is the open one), this property cannot be detected by the hardware
        /// and has to be set by the user. </remarks>
        /// <seealso cref="IsFlipped"/>
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
        
        /// <summary>
        /// Whether a positive motion along the declination axis would currently move the pointing south or north.
        /// </summary>
        /// <remarks> Flipping of the telescope could come from two sources. The first is the physical placement of the
        /// tube on the mount, which is represented by <see cref="NSFlip"/>. The other factor is the side of the pier
        /// that the telescope is on (<see cref="SideOfPier"/>). The ASCOM standard in fact defines
        /// <see cref="PierSide.pierEast"/>" as a configuration in which moving the declination motor in the positive
        /// direction should move the pointing north, and so a "flipped" state would be one when <see cref="SideOfPier"/>
        /// is <see cref="PierSide.pierWest"/>. However, the situation is reversed if the tube is physically flipped,
        /// i.e. the open end of the telescope is where the driver thinks the camera is. This leads to a a flipped state
        /// which is an exclusive combination of <see cref="NSFlip"/> and <see cref="SideOfPier"/> being
        /// <see cref="PierSide.pierEast"/>. The complete boolean table is: (NSFlip, SoP=E) -> T, (NSFlip, SoP=W) -> F,
        /// (!NSFlip, SoP=E) -> F, (!NSFlip, SoP=W) -> T. The latter two are the normal behaviour of
        /// <see cref="SideOfPier"/>, whereas <see cref="NSFlip"/> simply flips this. Thus,
        /// IsFlipped = NOT (NSFlip XOR (SideOfPier is East)). The implementation actually uses "SideOfPier is not West"
        /// in order to have an unflipped default when the side of pier is not known. 
        /// </remarks>
        public bool? IsFlipped => Valid ? !(NSFlip ^ (SideOfPier != PierSide.pierWest)) : (bool?) null;


        private static readonly string[] _props = {
            nameof(SiderealTime), nameof(Altitude), nameof(Azimuth), nameof(RightAscension), nameof(Declination),
            nameof(AtPark), nameof(Slewing), nameof(Tracking),
            nameof(CanTrack), nameof(CanSlew), nameof(CanGoTo), nameof(CanPark),
            nameof(Moveable),
            nameof(ParkAction)
        };
        [XmlIgnore] protected override IEnumerable<string> props => _props;

        /// <inheritdoc />
        /// <remarks> If <see cref="SideOfPier" /> has changed, refreshes it and <see cref="IsFlipped" /> and raises
        /// <see cref="FlippedChanged" />. </remarks>
        protected override void refresh() {
            var sop = SideOfPier;
            if (!_sideOfPier.Equals(sop)) {
                _sideOfPier = sop;
                OnPropertyChanged(nameof(SideOfPier));
                OnPropertyChanged(nameof(IsFlipped));
                FlippedChangedRaise();
            }
        }

        #endregion

        private bool canMoveAxes;
        /// <summary>
        /// Whether the mount can currently be controlled via the <see cref="ITelescopeV3.MoveAxis"/> interface for
        /// both axes.
        /// </summary>
        /// <remarks> Whether the driver supports the interface is checked once on initialization. If so, then accessing
        /// this property also checks whether the mount is <see cref="Moveable"/>. </remarks>
        public bool CanMoveAxes => canMoveAxes && Moveable;

        /// <summary>
        /// A collection of supported driving rates for the primary axis. (See <see cref="ITelescopeV3.AxisRates"/>).
        /// </summary>
        [XmlIgnore] public ObservableCollection<IRate> PrimaryAxisRates { get; } = new ObservableCollection<IRate>();
        
        /// <summary>
        /// A collection of supported driving rates for the secondary axis. (See <see cref="ITelescopeV3.AxisRates"/>).
        /// </summary>
        [XmlIgnore] public ObservableCollection<IRate> SecondaryAxisRates { get; } = new ObservableCollection<IRate>();
        
        /// <summary>
        /// A collection of supported tracking rates. (See <see cref="ITelescopeV3.TrackingRates"/>).
        /// </summary>
        [XmlIgnore] public ObservableCollection<DriveRates> TrackingRates { get; } = new ObservableCollection<DriveRates>();

        private int _selectedPrimaryRateIndex;
        /// <summary>
        /// Index into <see cref="PrimaryAxisRates"/> of the rate selected by the user.
        /// </summary>
        public int SelectedPrimaryRateIndex {
            get => _selectedPrimaryRateIndex;
            set {
                if (value == _selectedPrimaryRateIndex) return;
                _selectedPrimaryRateIndex = value;
                OnPropertyChanged();
            }
        }

        private int _selectedSecondaryRateIndex;
        /// <summary>
        /// Index into <see cref="SecondaryAxisRates"/> of the rate selected by the user.
        /// </summary>
        public int SelectedSecondaryRateIndex {
            get => _selectedSecondaryRateIndex;
            set {
                if (value == _selectedSecondaryRateIndex) return;
                _selectedSecondaryRateIndex = value;
                OnPropertyChanged();
            }
        }

        private int _selectedTrackingRateIndex;
        /// <summary>
        /// Index into <see cref="TrackingRates"/> of the rate selected by the user.
        /// </summary>
        public int SelectedTrackingRateIndex {
            get => _selectedTrackingRateIndex;
            set {
                if (value == _selectedTrackingRateIndex) return;
                _selectedTrackingRateIndex = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Determine the value of <see cref="CanMoveAxes"/> and populate the axis (<see cref="PrimaryAxisRates"/>,
        /// <see cref="SecondaryAxisRates"/>) and tracking (<see cref="TrackingRates"/>) rates.
        /// </summary>
        protected override void initialize() {
            canMoveAxes = Driver.CanMoveAxis(TelescopeAxes.axisPrimary) &&
                          Driver.CanMoveAxis(TelescopeAxes.axisSecondary);

            if (canMoveAxes) {
                foreach (IRate rate in Driver.AxisRates(TelescopeAxes.axisPrimary))
                    PrimaryAxisRates.Add(rate);
                foreach (IRate rate in Driver.AxisRates(TelescopeAxes.axisSecondary))
                    SecondaryAxisRates.Add(rate);
            }

            if (Driver.CanSetTracking)
                foreach (DriveRates rate in Driver.TrackingRates)
                    TrackingRates.Add(rate);
        }

        
        #region TRACKING

        /// <summary>
        /// See <see cref="ITelescopeV3.TrackingRate"/>.
        /// </summary>
        /// <remarks>
        /// <para> A null value indicates that the hardware is not connected. </para>
        /// <para> This property adds a setter that catches errors and notifies on changes. </para>
        /// </remarks>
        [XmlIgnore] public DriveRates? RateTracking {
            get {
                if (Valid) try { return Driver.TrackingRate; } catch { return null;}
                else return null;
            }
            set {
                if (value.Equals(RateTracking)) return;
                try {
                    if (value is DriveRates val) Driver.TrackingRate = val;
                } catch { }
                OnPropertyChanged(nameof(RateTracking));
            }
        }

        /// <summary>
        /// Set the tracking to the desired state, if tracking control is currently available.
        /// </summary>
        /// <param name="state"> The desired tracking state. </param>
        /// <returns>Whether the operation was successful.</returns>
        public Task<bool> Track(bool state) {
            return Task<bool>.Factory.StartNew(() => {
                try { if (CanTrack) Driver.Tracking = state; } catch {}
                return Tracking == state;
            });
        }

        #endregion

        #region MOTION

        /// <summary>
        /// Raised when the mount has finished a software induced motion (does not include tracking).
        /// </summary>
        public event Action Slewed;
        public void SlewedRaise() => Slewed?.Invoke();

        private double _ratePrimary;
        /// <summary>
        /// The selected rate for the primary axes. One of the rates in <see cref="PrimaryAxisRates"/>.
        /// </summary>
        public double RatePrimary {
            get => _ratePrimary;
            set {
                if (value.Equals(_ratePrimary)) return;
                _ratePrimary = value;
                OnPropertyChanged();
            }
        }

        private double _rateSecondary;
        /// <summary>
        /// The selected rate for the secondary axes. One of the rates in <see cref="SecondaryAxisRates"/>.
        /// </summary>
        public double RateSecondary {
            get => _rateSecondary;
            set {
                if (value.Equals(_rateSecondary)) return;
                _rateSecondary = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Return the (absolute value of the) motor rate in the given direction: <see cref="RatePrimary"/> or
        /// <see cref="RateSecondary"/>. 
        /// </summary>
        /// <param name="dir">The desired direction.</param>
        /// <returns>Either <see cref="RatePrimary"/> or <see cref="RateSecondary"/> depending on <paramref name="dir"/>.</returns>
        public double Rate(GuideDirections dir) {
            return (dir == GuideDirections.guideEast || dir == GuideDirections.guideWest) ? RatePrimary : RateSecondary;
        }
        
        /// <summary>
        /// Return the axis corresponding to the given direction: either the primary or secondary axis.
        /// </summary>
        /// <param name="dir">The desired direction.</param>
        /// <returns>Either <see cref="TelescopeAxes.axisPrimary"/> or <see cref="TelescopeAxes.axisSecondary"/>
        /// depending on <paramref name="dir"/>.</returns>
        private static TelescopeAxes ax(GuideDirections dir) {
            return (dir == GuideDirections.guideEast || dir == GuideDirections.guideWest)
                ? TelescopeAxes.axisPrimary
                : TelescopeAxes.axisSecondary;
        }

        /// <summary>
        /// Return the sign for the motor rate in the given direction: positive or negative.
        /// </summary>
        /// <param name="dir">The desired direction.</param>
        /// <returns> Always positive for <see cref="GuideDirections.guideWest"/>, always negative for
        /// <see cref="GuideDirections.guideEast"/>, while for north-south it takes into account whether the mount
        /// <see cref="IsFlipped"/>: if not, <see cref="GuideDirections.guideNorth"/> is positive, and
        /// <see cref="GuideDirections.guideSouth"/> is negative, and vice versa if the mount is flipped. </returns>
        private int mult(GuideDirections dir) {
            return (dir == (IsFlipped == true
                        ? GuideDirections.guideSouth
                        : GuideDirections.guideNorth)
                    || dir == GuideDirections.guideWest)
                ? 1 : -1;
        }

        /// <summary>
        /// Set the mount in motion or stop it.
        /// </summary>
        /// <remarks> If <paramref name="move"/> is <c>true</c>, set the driver in motion in the direction given by
        /// <paramref name="dir"/>, figuring out which axis it corresponds to and which are the correct rate and sign.
        /// If <paramref name="move"/> is <c>false</c>, stop the motion of the corresponding axis. </remarks>
        /// <param name="dir">The desired direction.</param>
        /// <param name="move">Whether to set in motion or stop.</param>
        public void ControlMotion(GuideDirections dir, bool move=true) {
            if (move) {
                if (CanMoveAxes)
                    Driver.MoveAxis(ax(dir), mult(dir) * Rate(dir));
            } else {
                if (CanMoveAxes)
                    Driver.MoveAxis(ax(dir), 0);
                SlewedRaise();
            }
        }

        /// <summary>
        /// Stop slewing, software induced axis rotation and, if <paramref name="stopTracking"/>, tracking as well.
        /// </summary>
        /// <param name="stopTracking">Whether to stop tracking as well.</param>
        public void StopAllMotion(bool stopTracking = false) {
            if (Valid) {
                if (CanMoveAxes) {
                    Driver.MoveAxis(TelescopeAxes.axisPrimary,   0);
                    Driver.MoveAxis(TelescopeAxes.axisSecondary, 0);
                }
                Driver.AbortSlew();
                if (stopTracking) Track(false);

                SlewedRaise();
            }
        }

        /// <summary>
        /// Slew to the given right ascension and declination.
        /// </summary>
        /// <remarks> Delegates to <see cref="ITelescopeV3.SlewToCoordinates"/> but also handles turning off and on the
        /// tracking, which seems to be required by the driver. Also, raises the <see cref="Slewed"/> event. </remarks>
        /// <param name="ra">The target right ascension.</param>
        /// <param name="dec">The target declination.</param>
        /// <returns>Whether the operation was successful.</returns>
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

        /// <summary>
        /// Slew to the coordinates given as a <see cref="Pynder.Coords"/> object.
        /// </summary>
        /// <param name="c">The desired coordinates.</param>
        /// <seealso cref="Slew(double, double)"/>
        public Task<bool> Slew(Pynder.Coords c) => Slew(c.ra, c.dec);

        /// <summary>
        /// Go to the given object and start tracking if successful.
        /// </summary>
        /// <param name="o">The desired object.</param>
        /// <returns>Whether the operation was succesful.</returns>
        public async Task<bool> GoTo(Pynder.Objects o = Pynder.Objects.Sun) // TODO: Generic goto (diff. object)
            => CanGoTo && await Slew(Pynder.find(o)) && await Track(true);

        /// <summary>
        /// Park the mount after stopping tracking.
        /// </summary>
        /// <returns>Whether the operation was successful.</returns>
        public Task<bool> Park() {
            return Task<bool>.Factory.StartNew(() => {
                try {
                    if (CanPark) {
                        Track(false).Wait();
                        Driver.Park();
                    }
                } catch {}
                return AtPark==true;
            });
        }
        
        /// <summary>
        /// Unpark the mount.
        /// </summary>
        /// <returns>Whether the operation was successful.</returns>
        public Task<bool> Unpark() {
            return Task<bool>.Factory.StartNew(() => {
                try { if (CanPark) Driver.Unpark(); } catch {}
                return AtPark==false;
            });
        }

        #endregion

        #region ADJUST

        private double _adjustDuration;
        /// <summary>
        /// The desired duration in seconds of the adjustment procedure. 
        /// </summary>
        /// <remarks> The actual duration will probably be around twice as long due to the incremental adjustment
        /// strategy. </remarks>
        /// <seealso cref="Adjust"/>
        public double AdjustDuration {
            get => _adjustDuration;
            set {
                if (_adjustDuration.Equals(value)) return;
                _adjustDuration = value;
                OnPropertyChanged();
            }
        }

        private int _adjustNTrials;
        /// <summary>
        /// The maximum number of iterations to perform while adjusting.
        /// </summary>
        /// <seealso cref="Adjust"/>
        public int AdjustNTrials {
            get => _adjustNTrials;
            set {
                if (_adjustNTrials.Equals(value)) return;
                _adjustNTrials = value;
                OnPropertyChanged();
            }
        }

        private double _adjustToleranceDec;
        /// <summary>
        /// The tolerance in declination for the adjustment procedure.
        /// </summary>
        /// <seealso cref="Adjust"/>
        public double AdjustToleranceDec {
            get => _adjustToleranceDec;
            set {
                if (_adjustToleranceDec.Equals(value)) return;
                _adjustToleranceDec = value;
                OnPropertyChanged();
            }
        }

        private double _adjustToleranceRaCosDec;
        /// <summary>
        /// The desired tolerance in the projected east-west direction for the adjustment procedure.
        /// </summary>
        /// <remarks> The tolerance in right ascension is <c>rcosdec * AdjustToleranceRaCosDec</c>. </remarks>
        /// <seealso cref="Adjust"/>
        public double AdjustToleranceRaCosDec {
            get => _adjustToleranceRaCosDec;
            set {
                if (_adjustToleranceRaCosDec.Equals(value)) return;
                _adjustToleranceRaCosDec = value;
                OnPropertyChanged();
            }
        }


        /// <summary>
        /// Return the maximum allowed rate for the given axis smaller than or equal to the requested rate.
        /// </summary>
        /// <param name="r">The requested rate.</param>
        /// <param name="axes">The axis whose rates to check.</param>
        /// <returns> If <paramref name="r"/> falls within an allowed rate range (see <see cref="PrimaryAxisRates"/>
        /// and <see cref="SecondaryAxisRates"/>), return <paramref name="r"/>. Else, return the maximum allowed
        /// rate that is not larger that <paramref name="r"/>. </returns>
        private double allowedRate(double r, TelescopeAxes axes) {
            var sign = Math.Sign(r);
            r = Math.Abs(r);
            var ret = 0.0;
            foreach (var rate in axes == TelescopeAxes.axisPrimary ? PrimaryAxisRates : SecondaryAxisRates) {
                if (rate.Minimum <= r && r < rate.Maximum) return sign * r;
                if (rate.Maximum < r && ret < rate.Maximum) ret = rate.Maximum;
            }
            return sign * ret;
        }
        
        /// <summary>
        /// Get an <see cref="allowedRate"/> which would cover the <paramref name="offset"/> in the shortest time (but
        /// not more quickly than <see cref="AdjustDuration"/>).
        /// </summary>
        /// <remarks> Also takes care of the correct sign for the specified offset in the specified axis, assuming
        /// positive offsets point north or east. </remarks>
        /// <param name="offset">The desired offset to cover.</param>
        /// <param name="ax">An indication of the direction of the offset.</param>
        /// <returns> Returns an <see cref="allowedRate"/> for <c>offset / AdjustDuration</c> with the appropriate
        /// sign assuming the north and east directions are the positive ones. </returns>
        private double adjustRate(double offset, TelescopeAxes ax)
            => mult(ax == TelescopeAxes.axisPrimary ? GuideDirections.guideEast : GuideDirections.guideNorth)
               * allowedRate(offset / AdjustDuration, ax);

        /// <summary>
        /// Right ascension in degrees or <see cref="double.NaN"/> if unavailable.
        /// </summary>
        /// <seealso cref="RightAscension"/>
        private double ra => RightAscension * 15 ?? double.NaN;
        
        /// <summary>
        /// Declination in degrees or <see cref="double.NaN"/> if unavailable.
        /// </summary>
        /// <seealso cref="Declination"/>
        private double dec => Declination ?? double.NaN;
        
        /// <summary>
        /// The reciprocal of cosine the declination.
        /// </summary>
        public double rcosdec => 1.0 / Math.Cos(Utilities.deg2rad(dec));

        /// <summary>
        /// Adjust the pointing of the telescope by the given amounts.
        /// </summary>
        /// <remarks> This does a maximum of <see cref="AdjustNTrials"/> of iterations of calculating the current
        /// offset from the desired position, setting the axis rates so that the position is reached withing
        /// <see cref="AdjustDuration"/> and readjusting after half the time. The algorithm, as implemented, should
        /// only be able to adjust to within 1/1024 of the initial distance, but imperfections as well as the
        /// necessary suspension of sidereal tracking are expected to have a larger impact. The adjustment of a given
        /// axis is also stopped when the offset becomes smaller than <see cref="AdjustToleranceDec"/> or
        /// <see cref="AdjustToleranceRaCosDec"/> respectively. The latter gives the tolerance along the projected
        /// east-west direction, which is equal to cos(dec) times the offset in right ascension and hence at each
        /// iteration the offset in right ascension is compared to
        /// <c>rcosdec * AdjustToleranceRaCosDec</c>.</remarks>
        /// <param name="dra">The desired offset in right ascension in degrees.</param>
        /// <param name="ddec">The desired offset in declination in degrees.</param>
        /// <returns>A <see cref="Task"/> which completes when adjusting is done.</returns>
        public Task Adjust(double dra, double ddec) {
            return Task.Run(() => {
                if (CanMoveAxes) {
                    var trackingState = Tracking;
                    Driver.Tracking = true;

                    var destRa  = ra + dra;
                    var destDec = dec + ddec;

                    var initime = DateTime.Now;

                    var adjusted = true;
                    for (var i = 0; i < AdjustNTrials && adjusted; ++i) {
                        adjusted = false;

                        var offsetRa = Utilities.SymModulo(destRa - ra, 360);
                        if (Math.Abs(offsetRa) > rcosdec * AdjustToleranceRaCosDec) {
                            Driver.MoveAxis(TelescopeAxes.axisPrimary,
                                            adjustRate(offsetRa, TelescopeAxes.axisPrimary));
                            adjusted = true;
                        } else Driver.MoveAxis(TelescopeAxes.axisPrimary, 0);

                        var offsetDec = destDec - dec;
                        if (Math.Abs(offsetDec) > AdjustToleranceDec) {
                            Driver.MoveAxis(TelescopeAxes.axisSecondary,
                                            adjustRate(offsetDec, TelescopeAxes.axisSecondary));
                            adjusted = true;
                        } else Driver.MoveAxis(TelescopeAxes.axisSecondary, 0);

                        // Sleep half the time, then readjust.
                        Thread.Sleep((int) (500 * AdjustDuration));
                    }

                    Driver.MoveAxis(TelescopeAxes.axisPrimary, 0);
                    Driver.MoveAxis(TelescopeAxes.axisSecondary, 0);

                    var timetaken = DateTime.Now - initime;

                    if (trackingState != null)
                        Driver.Tracking = (bool) trackingState;

                    Logger.debug($"Adjusted to within {Math.Abs(destRa-ra)*rcosdec / AdjustToleranceRaCosDec:P0}, {(destDec-dec) / AdjustToleranceDec:P0} in {timetaken.TotalSeconds:F3}s (vs {AdjustDuration}s nominal)");
                }
            });
        }

        #endregion
    }
}
