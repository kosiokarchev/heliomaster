using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace heliomaster_wpf {
    public enum CameraTypes {
        [Description("ASCOM")]ASCOM,
        [Description("QHYCCD")] QHYCCD
    }
    
    public enum BitDepth {
        depth8,
        depth16,
        depth32
    }

    public unsafe class CameraImage {
        public enum ImageFileFormat {
            __fromExtension,
            png, jpeg, tiff,
        }
        private static readonly Dictionary<ImageFileFormat, Type> Encoders = new Dictionary<ImageFileFormat, Type> {
            {ImageFileFormat.png, typeof(PngBitmapEncoder)},
            {ImageFileFormat.jpeg, typeof(JpegBitmapEncoder)},
            {ImageFileFormat.tiff, typeof(TiffBitmapEncoder)},
        };

        public readonly void* raw;

        public readonly int Width;
        public readonly int Height;
        public readonly int Channels;
        public readonly BitDepth Depth;
        public int Length => Width * Height * Channels;
        public int Size => Length * BaseCamera.DepthSizes[Depth];

        protected CameraImage(int width, int height, int channels, BitDepth depth) {
            Width    = width;
            Height   = height;
            Channels = channels;
            Depth    = depth;
            raw      = (void*) Marshal.AllocHGlobal(Size);
        }

        protected readonly ReaderWriterLockSlim rwlock = new ReaderWriterLockSlim();

        public void PurgeCache() {
            _bitmapCache = null;
        }

        public CameraImage Copy() {
            var ret = new CameraImage(Width, Height, Channels, Depth);

            rwlock.EnterReadLock();

            ret.rwlock.EnterWriteLock();
            for (var i = 0; i < Size; ++i)
                ret.raw[i] = raw[i];
            ret.rwlock.ExitWriteLock();

            ret._bitmapCache = _bitmapCache;
            rwlock.ExitReadLock();

            return ret;
        }

        public BitmapSource GetBitmap() {
            rwlock.EnterReadLock();
            var img = BitmapSource.Create(
                Width, Height, dpiX: 96, dpiY: 96,
                pixelFormat: (Channels == 1) ?
                    ((Depth == BitDepth.depth8)  ? PixelFormats.Gray8 :
                     (Depth == BitDepth.depth16) ? PixelFormats.Gray16 :
                                                   PixelFormats.Gray32Float)
                    : ((Depth == BitDepth.depth8)  ? PixelFormats.Rgb24 :
                       (Depth == BitDepth.depth16) ? PixelFormats.Rgb48 :
                                                     PixelFormats.Rgb128Float),
                palette: null, buffer: (IntPtr) raw, bufferSize: Size,
                stride: Width * Channels * BaseCamera.DepthSizes[Depth]);
            img.Freeze();
            rwlock.ExitReadLock();
            return img;
        }

        private BitmapSource _bitmapCache;
        public BitmapSource BitmapSource => _bitmapCache ?? (_bitmapCache = GetBitmap());


        public Task<bool> Save(string fname, ImageFileFormat? fmt=ImageFileFormat.__fromExtension) {
            return Task<bool>.Factory.StartNew(() => {
                if (fmt == ImageFileFormat.__fromExtension)
                    fmt = Utilities.FormatFromExtension(fname);
                if (fmt == null) return false;

                var encoder = (BitmapEncoder) Activator.CreateInstance(Encoders[(ImageFileFormat) fmt]);
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource));
                using (var fileStream = new FileStream(fname, FileMode.Create))
                    encoder.Save(fileStream);
                return true;
            });
        }

        ~CameraImage() {
            rwlock.EnterWriteLock();
            Marshal.FreeHGlobal((IntPtr) raw);
            rwlock.ExitWriteLock();
            rwlock.Dispose();
        }
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

        public CameraImage  image { get; protected set; }
        public BitmapSource View => image?.BitmapSource;

        public event EventHandler<CameraImage> Captured;

        #endregion


        public static BaseCamera Create(CameraTypes type, params object[] args) {
            return (BaseCamera) Activator.CreateInstance(CameraTypes[type], args);
        }


        protected BaseCamera(bool autoupdate=true) {
            if (autoupdate) Captured += (s, res) => { 
                image = res;
                OnPropertyChanged(nameof(View)); };
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

        
        private Task livePreviewTask;
        private bool previewOn;
        
        public void StartLivePreview(double maxfps) {
            if (livePreviewTask == null && !previewOn) {
                livePreviewTask = Task.Run(() => {
                    var dt = new TimeSpan((long) (TimeSpan.TicksPerSecond / maxfps));
                    
                    previewOn = true;
                    while (previewOn) {
                        var nextTime = DateTime.Now + dt;
                        try {
                            Capture().Wait();
                        } catch {}

                        var towait = nextTime - DateTime.Now;
                        if (towait > TimeSpan.Zero)
                            Task.Delay(towait).Wait();
                    }
                });
            }
        }

        public async Task StopLivePreview() {
            previewOn = false;
            if (livePreviewTask != null) {
                await livePreviewTask;
                livePreviewTask.Dispose();
                livePreviewTask = null;
            }
        }

        public async Task<List<List<QueueItem<SemaphoreSlim, CameraImage>>>> Stop() {
            await StopLivePreview();
            return queue.Clear();
        }


        #region FOCUS

        public Focuser Focuser { get; } = new Focuser();

        #endregion
    }
}
