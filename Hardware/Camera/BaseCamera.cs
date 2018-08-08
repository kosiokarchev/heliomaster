using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace heliomaster_wpf {
    public enum CameraTypes {
        [Description("ASCOM")]ASCOM,
        [Description("QHYCCD")] QHYCCD
    }

    public abstract class BaseCamera : BaseHardwareControl {
        public static Dictionary<BitDepth, Type> DepthTypes = new Dictionary<BitDepth, Type> {
            {BitDepth.depth8, typeof(byte)},
            {BitDepth.depth16, typeof(UInt16)},
            {BitDepth.depth32, typeof(UInt32)}
        };
        public static Dictionary<BitDepth, Type> DepthPointerTypes = new Dictionary<BitDepth, Type> {
            {BitDepth.depth8, typeof(byte*)},
            {BitDepth.depth16, typeof(UInt16*)},
            {BitDepth.depth32, typeof(UInt32*)}
        };
        public static readonly Dictionary<BitDepth, int> DepthSizes = new Dictionary<BitDepth, int> {
            {BitDepth.depth8, 1},
            {BitDepth.depth16, 2},
            {BitDepth.depth32, 4}
        };

        public static Dictionary<CameraTypes, Type> CameraTypes = new Dictionary<CameraTypes, Type> {
            {heliomaster_wpf.CameraTypes.ASCOM, typeof(ASCOMCamera)},
        };

        #region properties

        public abstract uint     Width    { get; }
        public abstract uint     Height   { get; }
        public abstract BitDepth Depth    { get; }
        public virtual  int      Channels { get; protected set; } = 1;

        public virtual  double MinExposure => double.Epsilon;
        public abstract double MaxExposure { get; }

        protected virtual double exposure { get; set; }
        public virtual double Exposure {
            get => exposure < MinExposure ? MinExposure : exposure > MaxExposure ? MaxExposure : exposure;
            set {
                if (MinExposure < value && value < MaxExposure)
                    exposure = value;
                OnPropertyChanged();
            }
        }

        public    abstract double MinGain { get; }
        public    abstract double MaxGain { get; }
        protected virtual  double gain    { get; set; }
        public double Gain {
            get => gain;
            set {
                if (MinGain < value && value < MaxGain)
                    gain = value;
                OnPropertyChanged();
            }
        }

        public CameraImage  image;
        public BitmapSource View => image?.BitmapSource;

        public event EventHandler<CameraImage> Captured;

        #endregion


        public static BaseCamera Create(CameraTypes type, params object[] args) {
            return (BaseCamera) Activator.CreateInstance(CameraTypes[type], args);
        }


        protected BaseCamera(bool autoupdate=true) {
            if (autoupdate) Captured += (s, res) => { OnPropertyChanged(nameof(View)); };
        }


        public enum Priority {
            Production = 0,
            Tracking   = 1,
            LiveView   = 2
        }
        protected readonly QueueConsumer<SemaphoreSlim, CameraImage> queue = new QueueConsumer<SemaphoreSlim, CameraImage>(Enum.GetValues(typeof(Priority)).Length);

        protected abstract CameraImage capture();
        public async Task<CameraImage> Capture(Priority p=Priority.Production) {
            if (Valid) {
                var item = new QueueItem<SemaphoreSlim, CameraImage> {
                    Func = o => capture(),
                    Param = new SemaphoreSlim(0, 1)
                };
                item.Complete += (s, res) => {
                    (s as QueueItem<SemaphoreSlim, CameraImage>)?.Param.Release();
                };

                queue.Enqueue(item, (int) p);
                await item.Param.WaitAsync();

                if (item.Result != null)
                    Captured?.Invoke(this, item.Result);
                return item.Result;
            } else return null;
        }


        #region FOCUS

        public Focuser Focuser { get; } = new Focuser();

        #endregion
    }
}
