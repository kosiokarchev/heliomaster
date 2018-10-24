using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using heliomaster.Properties;

namespace heliomaster {
    public class QHYCCDCamera : BaseCamera {
//        private readonly QHYCCDLocalizer libqhyccd = new QHYCCDLocalizer();

        protected override Type driverType => null;
        public override string Type => Resources.cameraQHYCCD;

        private         uint     _width;
        public override uint     Width => _width;
        private         uint     _height;
        public override uint     Height => _height;
        private         BitDepth _depth;
        public override BitDepth Depth => _depth;
        private         double _minExposure = Double.Epsilon;
        public override double MinExposure => _minExposure;
        private         double _maxExposure = 1000;
        public override double MaxExposure => _maxExposure;
        private         double   _minGain = 0;
        public override double   MinGain => _minGain;
        private         double   _maxGain = 100;
        public override double   MaxGain => _maxGain;

        protected override double gain {
            get => libqhyccd.GetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_GAIN);
            set => libqhyccd.SetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_GAIN, value);
        }

        // QHYCCD EXPOSURE IS IN MICROSECONDS!!!
        protected override double exposure {
            get => libqhyccd.GetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_EXPOSURE) / 1000;
            set => libqhyccd.SetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_EXPOSURE, 1000 * value);
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

        public static void initResource() {
//            var libqhyccd = new QHYCCDLocalizer();
            libqhyccd.InitQHYCCDResource();
            var ncams = libqhyccd.ScanQHYCCD();
            CameraIDs.Clear();
            for (uint i = 0; i < ncams; i++) {
                var s = new StringBuilder(32);
                libqhyccd.GetQHYCCDId(i, s);
                CameraIDs.Add(s.ToString());
            }
        }
        public static readonly ObservableCollection<string> CameraIDs = new ObservableCollection<string>();

        protected override void initialize() {
            double step = 0;
            libqhyccd.GetQHYCCDParamMinMaxStep(camhandle, libqhyccd.CONTROL_ID.CONTROL_GAIN, ref _minGain, ref _maxGain, ref step);
            libqhyccd.GetQHYCCDParamMinMaxStep(camhandle, libqhyccd.CONTROL_ID.CONTROL_EXPOSURE, ref _minExposure, ref _maxExposure, ref step);
            _minExposure /= 1000;
            _maxExposure /= 1000;
        }

        private StringBuilder id;

        public override Task<bool> Connect(string progID, bool init = true, bool setup = false) {
            return Task<bool>.Factory.StartNew(() => {
                id = new StringBuilder(progID, 32);
                camhandle = libqhyccd.OpenQHYCCD(id);

                Console.WriteLine(libqhyccd.SetQHYCCDStreamMode(camhandle, 0));
                Console.WriteLine(libqhyccd.InitQHYCCD(camhandle));

                double chipw = 0, chiph = 0, pixelw = 0, pixelh = 0;
                Console.WriteLine(libqhyccd.GetQHYCCDChipInfo(camhandle, ref chipw, ref chiph, ref _width, ref _height,
                                                              ref pixelw, ref pixelh, ref bpp));

                Channels = (int) (libqhyccd.GetQHYCCDMemLength(camhandle) / (Width * Height));

                _depth = bpp > 16 ? BitDepth.depth32 : bpp > 8 ? BitDepth.depth16 : BitDepth.depth8;

                ret = libqhyccd.SetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.QHYCCD_3A_AUTOEXPOSURE, 0);
                ret = libqhyccd.SetQHYCCDParam(camhandle, libqhyccd.CONTROL_ID.CONTROL_TRANSFERBIT, bpp);
                ret = libqhyccd.SetQHYCCDBinMode(camhandle, 1, 1);
                ret = libqhyccd.SetQHYCCDResolution(camhandle, 0, 0, Width, Height);
                ret = libqhyccd.BeginQHYCCDLive(camhandle);

                IsConnected = true;

                Initialize();
                return true;
            });
        }

        protected override async Task disconnect() {
            await base.disconnect();
            libqhyccd.CloseQHYCCD(camhandle);
        }

        protected override CameraImage capture() {
            if (Valid) {
                if (image == null) image = new QHYCCDImage((int) Width, (int) Height, Channels, Depth);

                var t = DateTime.Now;

                Console.WriteLine(libqhyccd.ExpQHYCCDSingleFrame(camhandle));
                ret = 1;
                image.rwlock.EnterWriteLock();

                uint _channels = 0;
                unsafe {
                    while (ret != 0)
                        ret = libqhyccd.GetQHYCCDSingleFrame(camhandle, ref _width, ref _height, ref bpp,
                                                             ref _channels, (byte*) image.raw);
                    //                libqhyccd.GetQHYCCDLiveFrame(camhandle, ref _width, ref _height, ref bpp, ref _channels, (byte*) image.raw);
                }

                image.rwlock.ExitWriteLock();

                image.BufferUpdated();

                Console.WriteLine($"fps: {1 / (DateTime.Now - t).TotalSeconds}");

                return image;
            } else return null;
        }
    }
}
