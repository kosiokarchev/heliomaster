using System;
using System.Threading;
using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    public partial class Observatory {
        private bool _isSlaving;

        public bool IsSlaving {
            get => _isSlaving;
            set {
                if (value == _isSlaving) return;
                _isSlaving = value;
                OnPropertyChanged();
            }
        }

        private bool _isHardwareSlaving;

        public bool IsHardwareSlaving {
            get => _isHardwareSlaving;
            set {
                if (value == _isHardwareSlaving) return;
                _isHardwareSlaving = value;
                OnPropertyChanged();
            }
        }

        private Timer slavingTimer;

        private async Task slave() {
            if (!(Mount.Azimuth is double az) || double.IsNaN(az)) {
                Emit(new SlavingWarning("Mount state is invalid."));
                return;
            }

            if (!(Dome.Azimuth is double domaz) || double.IsNaN(domaz)) {
                Emit(new SlavingWarning("Dome state is invalid."));
                return;
            }

            if (Math.Abs(domaz - az) > S.Dome.SlaveTolerance) {
                if (IsHardwareSlaving)
                    Emit(new SlavingWarning("Hardware slaving does not appear to be working correctly."));
                if (!await Dome.Slew(az, true)) Emit(new SlavingWarning("Dome slew was unsuccessful."));
            }
        }

        private void _slave() => slave();

        public async Task SlaveDomeToMount(TimeSpan? interval = null, TimeSpan? checkup = null) {
            if (!IsSlaving) {
                await slave(); // TODO: Does not wait for initial sync

                TimeSpan dt;
                if (!S.Dome.AlwaysSoftSlave && Dome.CanSlave) {
                    await Dome.Slave(true);
                    dt                = checkup ?? S.Dome.SlaveCheckup;
                    IsHardwareSlaving = true;
                } else {
                    dt                = interval ?? S.Dome.SlaveInterval;
                    IsHardwareSlaving = false;
                }

                slavingTimer =  new Timer(o => slave(), null, dt, dt);
                Mount.Slewed += _slave;

                IsSlaving = true;
            }
        }

        public async Task UnSlaveDomeFromMount() {
            await Dome.Slave(false);
            slavingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            slavingTimer?.Dispose();
            slavingTimer =  null;
            Mount.Slewed -= _slave;
            IsSlaving    =  false;
        }
    }
}