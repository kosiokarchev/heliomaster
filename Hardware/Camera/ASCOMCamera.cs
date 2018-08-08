using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace heliomaster_wpf {
    public class ASCOMCamera : BaseCamera {
        protected override Type driverType => typeof(ASCOM.DriverAccess.Camera);
        public ASCOM.DriverAccess.Camera Driver => (ASCOM.DriverAccess.Camera) driver;

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

            OnConnected();
        }

        protected override CameraImage capture() {
            if (Valid) {
//                var t = (new TimeSpan(DateTime.Now.Ticks)).TotalSeconds;

                Driver.Gain = (short) Gain;
                Driver.StartExposure(Exposure / 1000, true);
                SpinWait.SpinUntil(() => Driver.ImageReady);

//                Console.WriteLine(1.10 / ((new TimeSpan(DateTime.Now.Ticks)).TotalSeconds - t));

                var a = (Array) Driver.ImageArray;
                Channels = a.Rank == 2 ? 1 : a.GetLength(2);
                if (image == null || image.Channels != Channels)
                    image = new ASCOMImage((Array) Driver.ImageArray, Channels, Depth);
                else
                    image.Put((Array) Driver.ImageArray);

                return image.Copy();
            } else return null;
        }
    }
}
