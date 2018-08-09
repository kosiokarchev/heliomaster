using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace heliomaster_wpf {
    public class QHYCCDImage : CameraImage {
        public QHYCCDImage(int width, int height, int channels, BitDepth depth) : base(width, height, channels, depth) { }
    }
    
    public class QHYCCDCamera : BaseCamera {
        protected override Type driverType { get; } = null;

        private         uint     _width = 0;
        public override uint     Width => _width;
        private         uint     _height = 0;
        public override uint     Height => _height;
        private         BitDepth _depth;
        public override BitDepth Depth => _depth;
        private         double   _maxExposure = 1000;
        public override double   MaxExposure => _maxExposure;
        private         double   _minGain = 0;
        public override double   MinGain => _minGain;
        private         double   _maxGain = 100;
        public override double   MaxGain => _maxGain;

        protected override double gain {
            get => libqhyccd.GetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_GAIN);
            set => libqhyccd.SetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_GAIN, value);
        }

        protected override double exposure {
            get => libqhyccd.GetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_EXPOSURE);
            set => libqhyccd.SetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_EXPOSURE, value);
        }
        public bool IsConnected;
        protected override bool valid => IsConnected;

        private static readonly string[] _props = {
            nameof(Gain), nameof(Exposure)
        };
        protected override IEnumerable<string> props => _props;

        private IntPtr camhandle;
        private UInt32 ret;
        private uint bpp;

        static uint initResource() {
            libqhyccd.InitQHYCCDResource();
            return libqhyccd.ScanQHYCCD();
        }
        public static uint ncams = initResource();

        public override Task<bool> Connect(string progID, bool state = true, bool init = true, bool setup = false) {
            return Task<bool>.Factory.StartNew(() => {
                camhandle = libqhyccd.OpenQHYCCD(new StringBuilder(progID));

                Console.WriteLine(libqhyccd.SetQHYCCDStreamMode(camhandle, 1));
                Console.WriteLine(libqhyccd.InitQHYCCD(camhandle));

                double chipw = 0, chiph = 0, pixelw = 0, pixelh = 0;
                Console.WriteLine(libqhyccd.GetQHYCCDChipInfo(camhandle, ref chipw, ref chiph, ref _width, ref _height,
                                                              ref pixelw, ref pixelh, ref bpp));

                Channels = (int) (libqhyccd.GetQHYCCDMemLength(camhandle) / (Width * Height));

                _depth = bpp > 16 ? BitDepth.depth32 : bpp > 8 ? BitDepth.depth16 : BitDepth.depth8;

                ret = libqhyccd.SetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.QHYCCD_3A_AUTOEXPOSURE, 0);
                ret = libqhyccd.SetQHYCCDBinMode(camhandle, 1, 1);
                ret = libqhyccd.SetQHYCCDResolution(camhandle, 0, 0, Width, Height);
                ret = libqhyccd.BeginQHYCCDLive(camhandle);

                IsConnected = true;
                return true;
            });
        }

        protected override unsafe CameraImage capture() {
            if (Valid) {
                if (image == null) image = new QHYCCDImage((int) Width, (int) Height, Channels, Depth);
                else image.PurgeCache();

                uint _channels = 0;
                libqhyccd.GetQHYCCDLiveFrame(camhandle, ref _width, ref _height, ref bpp, ref _channels, (byte*) image.raw);
                OnPropertyChanged(nameof(View));
                return image;
            } else return null;
        }

        ~QHYCCDCamera() {
            if (IsConnected) {
                libqhyccd.CloseQHYCCD(camhandle);
//                libqhyccd.ReleaseQHYCCDResource();
            }
        }
    }
}
