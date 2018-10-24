using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASCOM.DeviceInterface;
using heliomaster.Properties;

namespace heliomaster {
    /// <summary>
    /// A camera controlled through its ASCOM interface.
    /// </summary>
    public class ASCOMCamera : BaseCamera {
        protected override Type driverType => typeof(ASCOM.DriverAccess.Camera);
        public ICameraV2 Driver => driver as ASCOM.DriverAccess.Camera;

        public override string Type => Resources.cameraASCOM;

        #region properties

        public override uint Width  => (uint) Driver.CameraXSize;
        public override uint Height => (uint) Driver.CameraYSize;

        private         BitDepth _depth;
        public override BitDepth Depth => _depth;

        // ASCOM (sensibly) works in seconds, but heliomaster prefers milliseconds
        public override double MinExposure => 1000*Driver.ExposureMin;
        public override double MaxExposure => 1000*Driver.ExposureMax;

        private         int _minGain;
        private         int _maxGain;
        public override double MinGain => _minGain;
        public override double MaxGain => _maxGain;

        /// <summary>
        /// A list of the names of the different gain settings (see <see cref="ICameraV2.Gains"/>)
        /// </summary>
        public List<string> Gains { get; private set; }

        protected override double gain {
            get => Driver.Gain;
            set => Driver.Gain = (short) value;
        }

        #endregion

        ///<inheritdoc />
        /// <remarks>
        /// Extracts the <see cref="Depth"/> based on the <see cref="ICameraV2.MaxADU"/> property. If the driver
        /// supports arbitrary gains, extracts the permitted range. Otherwise, extracts the supported named gain
        /// settings, stores them in <see cref="Gains"/>, and sets <see cref="MinGain"/> to 0 and <see cref="MaxGain"/>
        /// to one less than the number of available gains (in this case the <see cref="gain"/> property reflects an
        /// index into <see cref="Gains"/>).
        /// </remarks>
        protected override void initialize() {
            _depth = Driver.MaxADU > UInt16.MaxValue ? BitDepth.depth32 :
                Driver.MaxADU > byte.MaxValue ? BitDepth.depth16 : BitDepth.depth8;

            try {
                _minGain = Driver.GainMin;
                _maxGain = Driver.GainMax;
            } catch (ASCOM.PropertyNotImplementedException) {
                _minGain = 0;
                _maxGain = Driver.Gains.Count-1;
                Gains    = new List<string>();
                foreach (var g in Driver.Gains)
                    Gains.Add((string) g);
            }

            // TODO: hack
            // The following should not be necessary, but QHY cameras sometimes have a problem
            // starting up with an exception like "StartExposure NumX set - '0' is an invalid
            // value. The valid range is: 1 to 0".
            // The ASCOM specs say "it should default to CameraXSize"
            Driver.NumX = Driver.CameraXSize;
            Driver.NumY = Driver.CameraYSize;
        }

        
        /// <inheritdoc />
        /// <remarks>
        /// <para>Attempts to take an image, timing out in 5 seconds. If capturing is successful within this interval,
        /// tries to reuse the memory in <see cref="BaseCamera.image"/> or creates a new <see cref="ASCOMImage"/> and
        /// stores it in <see cref="BaseCamera.image"/> if the current one is uninitialised or has different properties.
        /// </para>
        /// <para>Currently includes a hack which resets the <see cref="ICameraV2.Gain"/> value on the driver just
        /// before starting an exposure in order to prevent QHY cameras from inevitably adjusting the setting to
        /// achieve automatic exposure.</para>
        /// </remarks>
        protected override CameraImage capture() {
            var prefix = $"CAMERA {DisplayName}: "; // TODO: move to a better place

            if (Valid) {
                try {
                    Driver.Gain = (short) Gain; // TODO: hack to stop QHY inevitable autoexposure
                    Driver.StartExposure(Exposure / 1000, true);

                    Task.Run(() => SpinWait.SpinUntil(() => Driver?.ImageReady == true))
                        .Wait(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token); // TODO: Unhardcode

                    if (!Driver.ImageReady)
                        throw new TimeoutException("Frame not returned in 5s"); // TODO: Unhardcode

                    var a = (Array) Driver.ImageArray;
                    Channels = a.Rank == 2 ? 1 : a.GetLength(2);
                    if (image == null || image.Channels != Channels) {
                        image?.Dispose();
                        image = new ASCOMImage((Array) Driver.ImageArray, Depth);
                    } else
                        image.Put((Array) Driver.ImageArray);

                    return image;
                } catch (OperationCanceledException) {
                    Logger.debug(prefix + "capture timed out");
                } catch (Exception e) {
                    Logger.warning($"CAMERA {DisplayName}: Error in capture: {Utilities.FormatException(e)}");
                } finally {
                    if (Driver.CanAbortExposure)
                        Driver.AbortExposure();
                }
            }
            return null;
        }
    }
}
