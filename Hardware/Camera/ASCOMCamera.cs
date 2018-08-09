using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace heliomaster_wpf {
    public sealed class ASCOMImage : CameraImage {
        public ASCOMImage(Array data, int channels, BitDepth depth) : base(
            data.GetLength(0), data.GetLength(1), channels, depth) {
            Put(data);
        }

        public void Put(Array data) {
            rwlock.EnterWriteLock();

            var gch = GCHandle.Alloc(data, GCHandleType.Pinned);
            unsafe {
                Int32 * d = (Int32*) gch.AddrOfPinnedObject();
                int     r = -1;

                switch (Depth) {
                    case BitDepth.depth32:
                        for (var i = 0; i < Height; ++i)
                            for (var j = 0; j < Width; ++j)
                                for (var k=0; k<Channels; ++k)
                                    ((UInt32*) raw)[++r] = (UInt32) d[j*Height*Channels + i*Channels + k];
                        break;
                    case BitDepth.depth16:
                        for (var i = 0; i < Height; ++i)
                            for (var j = 0; j < Width; ++j)
                                for (var k=0; k<Channels; ++k)
                                    ((UInt16*) raw)[++r] = (UInt16) d[j*Height*Channels + i*Channels + k];
                        break;
                    case BitDepth.depth8:
                        for (var i = 0; i < Height; ++i)
                            for (var j = 0; j < Width; ++j)
                                for (var k=0; k<Channels; ++k)
                                    ((byte*) raw)[++r] = (byte) d[j*Height*Channels + i*Channels + k];
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            gch.Free();

            rwlock.ExitWriteLock();
        }
    }
    
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

            base.Initialize();
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
                    ((ASCOMImage) image).Put((Array) Driver.ImageArray);

                return image.Copy();
            } else return null;
        }
    }
}
