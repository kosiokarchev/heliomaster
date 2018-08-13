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

        public double Position => Positionable ? StepSize * Driver.Position : Speed;


        private bool? _absolute;
        public bool? Absolute {
            get => _absolute;
            private set {
                if (value == _absolute) return;
                _absolute = value;
                OnPropertyChanged();
            }
        }

        private double _maxSpeed;
        public double MaxSpeed {
            get => _maxSpeed;
            private set {
                if (value.Equals(_maxSpeed)) return;
                _maxSpeed = value;
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

        private double _speed;
        public double Speed {
            get => _speed;
            set {
                if (value.Equals(_speed)) return;
                _speed = value;
                OnPropertyChanged();
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

            Speed = StepSize;
            SliderValueFormat += "/nudge";

            MaxSpeed = StepSize * Driver.MaxIncrement;

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
            RefreshRaise();
            await Task.Run((Action) WaitStop);
        }

//        private async void DoRelativeMove(int delta) {
//            if (moving) return;
//
//            moving = true;
//            while (moving && delta != 0) {
//                var tomove = Math.Max(Math.Min(delta, Driver.MaxIncrement), -Driver.MaxIncrement);
//                await AwaitMove(tomove);
//                delta -= tomove;
//            }
//            moving = false;
//        }

//        public async void Move(int delta) {
//            if (Moveable && Absolute != null) {
//                if ((bool) Absolute)
//                    await AwaitMove(Math.Max(Math.Min(Driver.Position + delta, Driver.MaxStep), 0));
//                else
//                    DoRelativeMove(delta);
//            }
//        }

        public void Stop() {
            moving = false;
            Driver.Halt();
        }

        public async void Nudge(bool forward) {
            Console.WriteLine(Speed);
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

//        public void Nudge(bool forward) {
//            var delta = (int) ((forward ? 1 : -1) * (Positionable ? LargeChange / 10 / StepSize : speed / Driver.StepSize));
//            Move(delta);
//        }

        #endregion

        #region ISyncToDriver

        private readonly string[] _props = {
            nameof(Position), nameof(Moveable)
        };
        protected override IEnumerable<string> props => _props;

//        public override void SyncToDriver(TimeSpan period) {
//            if (Absolute != null && (bool) Absolute)
//                base.SyncToDriver(period);
//        }

        #endregion
    }
}
