﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace heliomaster {
    public enum CameraTypes {
        [Description("ASCOM")] ASCOM,
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

        public static readonly Dictionary<CameraTypes, Type> CameraTypes = new Dictionary<CameraTypes, Type> {
            {heliomaster.CameraTypes.ASCOM, typeof(ASCOMCamera)},
            {heliomaster.CameraTypes.QHYCCD, typeof(QHYCCDCamera)}
        };

        public static BaseCamera Create(CameraTypes type, params object[] args) {
            return (BaseCamera) Activator.CreateInstance(CameraTypes[type], args);
        }

        #region properties

        public abstract uint     Width    { get; }
        public abstract uint     Height   { get; }
        public abstract BitDepth Depth    { get; }
        public virtual  int      Channels { get; protected set; } = 1;

        public virtual  double MinExposure => double.Epsilon;
        public abstract double MaxExposure { get; }

        protected virtual double exposure { get; set; }
        public virtual double Exposure {
            get => exposure;
            set {
                exposure = Utilities.Clamp(value, MinExposure, MaxExposure);
                OnPropertyChanged();
            }
        }

        public    abstract double MinGain { get; }
        public    abstract double MaxGain { get; }
        protected virtual  double gain    { get; set; }
        public virtual double Gain {
            get => gain;
            set {
                gain = Utilities.Clamp(value, MinGain, MaxGain);
                OnPropertyChanged();
            }
        }

        public CameraImage  image { get; protected set; }
        public BitmapSource View => image?.BitmapSource;

        public event EventHandler<CameraImage> Captured;

        private string _displayName;
        public string DisplayName {
            get => _displayName ?? Name;
            set => _displayName = value;
        }

        #endregion

        protected BaseCamera(bool autoupdate=true) {
            if (autoupdate) Captured += (s, res) => {
                image = res;
                OnPropertyChanged(nameof(View));
            };
        }


        public enum Priority {
            Production = 0,
            Tracking   = 1,
            LiveView   = 2
        }
        protected readonly QueueConsumer<SemaphoreSlim, CameraImage> queue = new QueueConsumer<SemaphoreSlim, CameraImage>(Enum.GetValues(typeof(Priority)).Length);

        protected abstract CameraImage capture();
        public async Task<CameraImage> Capture(Priority p=Priority.Production, bool copy = false) {
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
                return copy ? item.Result?.Copy() : item.Result;
            } else return null;
        }

        private void livePreview(double maxfps) {
            var dt = TimeSpan.FromSeconds(1 / maxfps);

            PreviewOn = true;
            try {
                Logger.debug("CAMERA: Starting live preview.");
                while (!cancelPreviewSource.IsCancellationRequested) {
                    var nextTime = DateTime.Now + dt;
                    try {
                        Capture(Priority.LiveView).Wait();
                        var towait = nextTime - DateTime.Now;
                        if (towait > TimeSpan.Zero)
                            Task.Delay(towait, cancelPreviewSource.Token).Wait();
                    } catch (Exception e) {
                        if (!(e is OperationCanceledException))
                            Logger.debug($"Exception during live preview: {Utilities.FormatException(e)}");
                    }
                }
            } finally {
                Logger.debug("CAMERA: Live preview ending.");
                PreviewOn = false;
            }
        }


        private Task livePreviewTask;
        private bool _previewOn;
        public bool PreviewOn {
            get => _previewOn;
            set {
                if (value == _previewOn) return;
                _previewOn = value;
                OnPropertyChanged();
            }
        }

        private CancellationTokenSource cancelPreviewSource;

        public void StartLivePreview(double maxfps) {
            if (livePreviewTask == null && !PreviewOn) {
                cancelPreviewSource = new CancellationTokenSource();
                livePreviewTask = Task.Run(() => livePreview(maxfps));
            }
        }

        public async Task StopLivePreview() {
            cancelPreviewSource?.Cancel();
            if (livePreviewTask != null) {
                await livePreviewTask;
                livePreviewTask?.Dispose();
            }
            livePreviewTask = null;
            PreviewOn = false;
        }

        public async Task<List<List<QueueItem<SemaphoreSlim, CameraImage>>>> Stop() {
            await StopLivePreview();
            return await queue.ClearTask();
        }

        public override async Task Disconnect() {
            await Stop();
            await base.Disconnect();
        }

        #region FOCUS

        public Focuser Focuser { get; } = new Focuser();

        #endregion
    }
}
