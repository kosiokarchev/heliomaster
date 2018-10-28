using System;
using System.Threading;
using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    public partial class Observatory {
        private bool _isSlaving;
        /// <summary>
        /// Whether the dome is supposed to be syncing to the mount (either via hardware or software, i.e. whether
        /// <see cref="SlaveDomeToMount"/>) has been called.
        /// </summary>
        public bool IsSlaving {
            get => _isSlaving;
            set {
                if (value == _isSlaving) return;
                _isSlaving = value;
                OnPropertyChanged();
            }
        }

        private bool _isHardwareSlaving;
        /// <summary>
        /// Whether the dome is supposed to be in hardware syncing mode.
        /// </summary>
        /// <remarks>This does not correspond to <see cref="heliomaster.Dome.Slaved"/> but rather to whether
        /// <see cref="SlaveDomeToMount"/> has been called with <see cref="DomeSettings.AlwaysSoftSlave"/> <c>false</c>.
        /// </remarks>
        public bool IsHardwareSlaving {
            get => _isHardwareSlaving;
            set {
                if (value == _isHardwareSlaving) return;
                _isHardwareSlaving = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// A timer that handles the software syncing (or checkups).
        /// </summary>
        private Timer slavingTimer;

        /// <summary>
        /// Realise a software sync of the <see cref="Dome"/> azimuth to that of the <see cref="Mount"/>. 
        /// </summary>
        /// <remarks>Warns if the values reported by <see cref="heliomaster.Telescope.Azimuth"/> or
        /// <see cref="heliomaster.Dome.Azimuth"/> are invalid or if the <see cref="heliomaster.Dome.Slew"/> was
        /// unsuccessful. Also warns if the dome <see cref="IsHardwareSlaving"/> but the
        /// <see cref="DomeSettings.SlaveTolerance"/> is not achieved and attempts to correct it (checkup mode).</remarks>
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
        
        /// <summary>
        /// A utility which allows setting <see cref="slave"/> as a handler of the <see cref="Telescope.Slewed"/> event. 
        /// </summary>
        private void _slave() => slave();

        /// <summary>
        /// Start the dome syncing service.
        /// </summary>
        /// <param name="interval">The time between software syncs if <see cref="DomeSettings.AlwaysSoftSlave"/>. If
        /// <c>null</c>, defaults to <see cref="DomeSettings.SlaveCheckup"/> of <see cref="S.Dome"/>.
        /// </param>
        /// <param name="checkup">The time between software checkups if <see cref="DomeSettings.AlwaysSoftSlave"/>. If
        /// <c>null</c>, defaults to <see cref="DomeSettings.SlaveInterval"/> of <see cref="S.Dome"/>.</param>
        /// <returns></returns>
        /// <remarks>
        /// <para> Syncing the <see cref="Dome"/> to the <see cref="Mount"/> can be achieved either in-hardware, via the
        /// <see cref="ASCOM.DeviceInterface.IDomeV2.Slaved"/> property, or programatically. Usually this method turns
        /// on the hardware version through <see cref="heliomaster.Dome.Slave"/> and sets up a programmatic "checkup",
        /// a correction that is applied periodically each <paramref name="checkup"/> timespan if needed. However, if
        /// <see cref="DomeSettings.AlwaysSoftSlave"/> is <c>true</c>, instead it sets up the programmatic syncing
        /// method <see cref="slave"/> to be executed periodically each <paramref name="interval"/> timespan instead.
        /// The <see cref="IsHardwareSlaving"/> property then reflects which option was chosen.</para>
        /// <para>In either case registers <see cref="slave"/> (through <see cref="_slave"/>) to handle the
        /// <see cref="Mount"/> <see cref="heliomaster.Telescope.Slewed"/> event to immediately follow the telescope
        /// on manual slews.</para>
        /// </remarks>
        public async Task SlaveDomeToMount(TimeSpan? interval = null, TimeSpan? checkup = null) {
            if (!IsSlaving) {
                await slave(); // TODO: Does it wait for initial sync?

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

        /// <summary>
        /// Stop (both hardware and software) syncing of the dome.
        /// </summary>
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
