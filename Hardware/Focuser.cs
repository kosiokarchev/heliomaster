using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using heliomaster.Properties;
using ASCOM.DeviceInterface;

namespace heliomaster {
    /// <summary> A focuser controlled via its ASCOM interface. </summary>
    public class Focuser : BaseHardwareControl {
        protected override Type driverType => typeof(ASCOM.DriverAccess.Focuser);
        public IFocuserV2 Driver => (IFocuserV2) driver;
        
        public override string Type => Resources.focuser;

        #region PROPERTIES

        /// <summary> Whether the focuser can currently be set to a some absolute position. </summary>
        /// <remarks> Checks whether the hardware is connected and whether the focuser supports absolute positioning.</remarks>
        public bool Positionable => Valid && Absolute == true;
        /// <summary> Whether the focuser can currently be controlled. </summary>
        /// <remarks> Checks whether the hardware is connected and whether it is not already in motion. </remarks>
        public bool Moveable => Valid && !moving && !Driver.IsMoving;

        /// <summary> The position of a <see cref="Positionable"/> focuser, otherwise, its <see cref="Speed"/> setting. </summary>
        /// <remarks> The position is given in physical units (microns). </remarks>
        public double Position => Positionable ? StepSize * Driver.Position : Speed;
        
        private readonly string[] _props = {nameof(Position), nameof(Moveable)};
        protected override IEnumerable<string> props => _props;

        #endregion

        private bool? _absolute;
        /// <summary> Whether the focuser supports absolute positioning. </summary>
        /// <remarks> Checked only once during initialization. </remarks>
        public bool? Absolute {
            get => _absolute;
            private set {
                if (value == _absolute) return;
                _absolute = value;
                OnPropertyChanged();
            }
        }

        private double _maxSpeed;
        /// <summary> The maximum "speed" of the focuser, i.e. the <see cref="IFocuserV2.MaxIncrement"/> per nudge. </summary>
        /// <remarks> For drivers that report a <see cref="IFocuserV2.StepSize"/>, this value has units of microns per
        /// nudge, otherwise it is in steps per nudge.<seealso cref="SliderValueFormat"/></remarks>
        public double MaxSpeed {
            get => _maxSpeed;
            private set {
                if (value.Equals(_maxSpeed)) return;
                _maxSpeed = value;
                OnPropertyChanged();
            }
        }

        private double _stepSize = 1;
        /// <summary>
        /// The physical size of a step (<see cref="IFocuserV2.StepSize"/>). If this is not available, it is set to 1.
        /// </summary>
        public double StepSize {
            get => _stepSize;
            private set {
                if (value.Equals(_stepSize)) return;
                _stepSize = value;
                OnPropertyChanged();
            }
        }

        private double _speed;
        /// <summary> The "speed" setting of the focuser, the offset that a nudge will produce. </summary>
        /// <remarks>
        /// <para>This has the same units as <see cref="MaxSpeed"/>.</para>
        /// <para>It is always the case that <c>Speed / StepSize</c> is the number of steps a nudge will produce.</para>
        /// </remarks>
        public double Speed {
            get => _speed;
            set {
                if (value.Equals(_speed)) return;
                _speed = value;
                OnPropertyChanged();
            }
        }

        private string _sliderValueFormat = "{0:F0}";
        /// <summary> A format string to represent focuser speeds. </summary>
        /// <value> Either <c>"{0:F0} μm/nudge"</c> if a physical step size is available, or <c>"{0:F0}/nudge"</c>. </value>
        public string SliderValueFormat {
            get => _sliderValueFormat;
            set {
                if (value == _sliderValueFormat) return;
                _sliderValueFormat = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Determine the values of <see cref="Absolute"/>, <see cref="StepSize"/>, <see cref="Speed"/>,
        /// <see cref="MaxSpeed"/>, and <see cref="SliderValueFormat"/>.
        /// </summary>
        protected override void initialize() {
            Absolute = Driver.Absolute;

            try {
                StepSize = Driver.StepSize;
                SliderValueFormat += " μm";
            } catch (ASCOM.PropertyNotImplementedException) {}

            Speed = StepSize;
            SliderValueFormat += "/nudge";

            MaxSpeed = StepSize * Driver.MaxIncrement;
        }

        #region MOTION

        private bool _moving;
        /// <summary> Whether the focuser is currently executing software-controlled motion. </summary>
        public bool moving {
            get => _moving;
            set {
                if (value == _moving) return;
                _moving = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Moveable));
            }
        }

        /// <summary> Start and wait asynchronously the motion of the focuser. </summary>
        /// <param name="param">The parameter to pass to <see cref="IFocuserV2.Move"/></param>
        private async Task AwaitMove(int param) {
            Driver.Move(param);
            RefreshRaise();
            await Task.Run(() => SpinWait.SpinUntil(() => !(Valid && Driver.IsMoving)));
            RefreshRaise();
        }

        /// <summary> Move the focuser by the amount indicated by <see cref="Speed"/>. </summary>
        /// <param name="forward">Whether to move in the positive or negative direction.</param>
        /// <remarks> For both absolute and relative focusers, this method waits asynchronously for the motion to
        /// complete before returning. </remarks>
        public async void Nudge(bool forward) {
            if (Moveable && Absolute != null) {
                var delta = (forward ? 1 : -1) * (int) (Speed / StepSize);
                if ((bool) Absolute)
                    await AwaitMove(Math.Max(Math.Min(Driver.Position + delta, Driver.MaxStep), 0));
                else {
                    moving = true;
                    while (moving && delta != 0) {
                        var tomove = Math.Max(Math.Min(delta, Driver.MaxIncrement), -Driver.MaxIncrement);
                        await AwaitMove(tomove);
                        delta -= tomove;
                    }
                    moving = false;
                }
            }
        }

        #endregion
    }
}
