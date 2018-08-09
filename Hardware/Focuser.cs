using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xceed.Wpf.DataGrid;

namespace heliomaster_wpf {
    public class Focuser : BaseHardwareControl {
        protected override Type driverType => typeof(ASCOM.DriverAccess.Focuser);
        public ASCOM.DriverAccess.Focuser Driver => (ASCOM.DriverAccess.Focuser) driver;

        public bool Positionable => Valid && Absolute != null && (bool) Absolute;
        public bool Moveable => Valid && !moving && !Driver.IsMoving;


        private bool? _absolute;
        public bool? Absolute {
            get => _absolute;
            private set {
                if (value == _absolute) return;
                _absolute = value;
                OnPropertyChanged();
            }
        }

        private double _sliderMax;
        public double SliderMax {
            get => _sliderMax;
            private set {
                if (value.Equals(_sliderMax)) return;
                _sliderMax = value;
                OnPropertyChanged();
            }
        }

        private double _stepSize = 1;
        public double StepSize {
            get => _stepSize;
            private set {
                if (value.Equals(_stepSize)) return;
                _stepSize = value;
                OnPropertyChanged();
            }
        }

        private double speed;
        public double LargeChange => SliderMax / 10;

        public double SliderValue {
            get => Positionable ? StepSize * Driver.Position : speed;
            set {
                if (Positionable) {
                    Driver.Move((int) Math.Max(Math.Min(value / StepSize, Driver.MaxIncrement), -Driver.MaxIncrement));
                    WaitStop();
                    OnPropertyChanged();
                } else if (!value.Equals(speed)) {
                    speed = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _sliderValueFormat = "{0:F0}";
        public string SliderValueFormat {
            get => _sliderValueFormat;
            set {
                if (value == _sliderValueFormat) return;
                _sliderValueFormat = value;
                OnPropertyChanged();
            }
        }

        public override void Initialize() {
            Absolute = Driver.Absolute;

            try {
                StepSize          = Driver.StepSize;
                SliderValueFormat = "{0:F0} μm";
            } catch (ASCOM.PropertyNotImplementedException) {}

            speed = StepSize;

            if (!Driver.Absolute)
                SliderValueFormat += "/nudge";

            SliderMax = StepSize * (Driver.Absolute ? Driver.MaxStep : Driver.MaxIncrement);

            base.Initialize();
        }

        #region MOTION

        private bool _moving;
        public bool moving {
            get => _moving;
            set {
                if (value == _moving) return;
                _moving = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Moveable));
            }
        }

        private void WaitStop() {
            SpinWait.SpinUntil(() => !(Valid && Driver.IsMoving));
        }

        private async Task AwaitMove(int param) {
            Driver.Move(param);
            await Task.Run((Action) WaitStop);
        }

        private async void DoRelativeMove(int delta) {
            if (moving) return;

            moving = true;
            while (moving && delta != 0) {
                var tomove = Math.Max(Math.Min(delta, Driver.MaxIncrement), -Driver.MaxIncrement);
                Console.WriteLine(tomove);
                await AwaitMove(tomove);
                delta -= tomove;
            }
            moving = false;
            Console.WriteLine(moving);
            Console.WriteLine(Driver.IsMoving);
            Console.WriteLine(Moveable);
        }

        public async void Move(int delta) {
            if (Moveable && Absolute != null) {
                if ((bool) Absolute)
                    await AwaitMove(Math.Max(Math.Min(Driver.Position + delta, Driver.MaxStep), 0));
                else
                    DoRelativeMove(delta);
            }
        }

        public void Stop() {
            moving = false;
            Driver.Halt();
        }

        public void Nudge(bool forward) {
            var delta = (int) ((forward ? 1 : -1) * (Positionable ? LargeChange / 10 / StepSize : speed / Driver.StepSize));
            Move(delta);
        }

        #endregion

        #region ISyncToDriver

        private readonly string[] _props = {
            nameof(SliderValue), nameof(Moveable)
        };
        protected override IEnumerable<string> props => _props;

//        public override void SyncToDriver(TimeSpan period) {
//            if (Absolute != null && (bool) Absolute)
//                base.SyncToDriver(period);
//        }

        #endregion

        #region utilities

        public Func<double, double> SliderToSpeed => x => Utilities.ScaleLinToLog(x, StepSize, SliderMax);
        public Func<double, double> SpeedToSlider => Y => Utilities.ScaleLogToLin(Y, StepSize, SliderMax);

        #endregion
    }
}
