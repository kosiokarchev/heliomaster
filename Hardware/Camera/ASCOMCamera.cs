using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using ASCOM.DeviceInterface;
using heliomaster.Properties;

namespace heliomaster {
    public class ASCOMCamera : BaseCamera {
        protected override Type driverType => typeof(ASCOM.DriverAccess.Camera);
        public ASCOM.DriverAccess.Camera Driver => driver as ASCOM.DriverAccess.Camera;

        public override string Type => Resources.cameraASCOM;

        #region properties

        public override uint Width  => (uint) Driver.CameraXSize;
        public override uint Height => (uint) Driver.CameraYSize;

        private         BitDepth _depth;
        public override BitDepth Depth => _depth;

        public override double MinExposure => Driver.ExposureMin;
        public override double MaxExposure => Driver.ExposureMax;

        private         int _minGain;
        private         int _maxGain;
        public override double MinGain => _minGain;
        public override double MaxGain => _maxGain;

        public List<string> Gains { get; private set; }

        protected override double gain {
            get => Driver.Gain;
            set => Driver.Gain = (short) value;
        }

        private static readonly string[] _props = {
            nameof(Gain), nameof(Exposure)
        };
        protected override IEnumerable<string> props => _props;

        #endregion

        public override void Initialize() {
            _depth = Driver.MaxADU > UInt16.MaxValue ? BitDepth.depth32 :
                Driver.MaxADU > byte.MaxValue ? BitDepth.depth16 : BitDepth.depth8;

            try {
                _minGain = Driver.GainMin;
                _maxGain = Driver.GainMax;
            } catch (ASCOM.PropertyNotImplementedException) {
                _minGain = 0;
                _maxGain = Driver.Gains.Count;
                Gains    = new List<string>();
                foreach (var g in Driver.Gains)
                    Gains.Add((string) g);
            }

            // TODO: hack
            // The following should not be necessary, but QHY cameras sometimes have a problem
            // starting up with an exception like "StartExposure NumX set - '0' is an invalid
            // value. The valid range is: 1 to 0"
            // The ASCOM specs say "it should default to CameraXSize"
            Driver.NumX = Driver.CameraXSize;
            Driver.NumY = Driver.CameraYSize;

            base.Initialize();
        }

        protected override CameraImage capture() {
            var prefix = $"CAMERA {DisplayName}: "; // TODO: move to a better place

            if (Valid) {
                try {
                    Driver.Gain = (short) Gain; // TODO: hack
                    Driver.StartExposure(Exposure / 1000, true);

                    Task.Run(() => SpinWait.SpinUntil(() => Driver?.ImageReady == true))
                        .Wait(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token); // TODO: Unhardcode

                    if (!Driver.ImageReady)
                        throw new TimeoutException("Frame not returned in 1s"); // TODO: Unhardcode

                    var a = (Array) Driver.ImageArray;
                    Channels = a.Rank == 2 ? 1 : a.GetLength(2);
                    if (image == null || image.Channels != Channels) {
                        image?.Dispose();
                        image = new ASCOMImage((Array) Driver.ImageArray, Channels, Depth);
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
