using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace heliomaster {
    /// <summary>
    /// Enumerates the implemented camera types.
    /// </summary>
    public enum CameraTypes {
        [Description("ASCOM")] ASCOM,
        [Description("QHYCCD")] QHYCCD
    }

    /// <summary>
    /// The base class for all camera hardware (ASCOM controlled or not).
    /// </summary>
    /// <remarks>Implements the basic interface for controlling exposure and gain settings, for taking imaging,
    /// serializing access using queues, and obtaining a live preview in-software.</remarks>
    public abstract class BaseCamera : BaseHardwareControl {
        public static readonly Dictionary<CameraTypes, Type> CameraTypes = new Dictionary<CameraTypes, Type> {
            {heliomaster.CameraTypes.ASCOM, typeof(ASCOMCamera)},
            {heliomaster.CameraTypes.QHYCCD, typeof(QHYCCDCamera)}
        };

        /// <summary>
        /// Instantiate a new object of the subclass referred to by <paramref name="type"/>.
        /// </summary>
        /// <param name="type">The type of camera to instantiate.</param>
        /// <param name="args">The arguments to pass to the constructor.</param>
        /// <returns>An instance of <see cref="CameraTypes"/>[<paramref name="type"/>]</returns>
        public static BaseCamera Create(CameraTypes type, params object[] args) {
            return (BaseCamera) Activator.CreateInstance(CameraTypes[type], args);
        }

        #region properties

        /// <summary>
        /// The width of the raw camera image in pixels.
        /// </summary>
        public abstract uint Width { get; }
        /// <summary>
        /// The height of the raw camera image in pixels.
        /// </summary>
        public abstract uint Height { get; }
        /// <summary>
        /// The bit depth of the camera image in pixels.
        /// </summary>
        public abstract BitDepth Depth { get; }
        /// <summary>
        /// The number of channels of the camera image.
        /// </summary>
        public virtual int Channels { get; protected set; } = 1;

        /// <summary>
        /// The minimal exposure (in ms) supported by the camera.
        /// </summary>
        /// <remarks>Subclasses should override this property to extract the relevant value from the hardware.</remarks>
        public virtual double MinExposure => double.Epsilon;
        /// <summary>
        /// The maximal exposure (in ms) supported by the camera.
        /// </summary>
        /// <remarks>Subclasses should override this property to extract the relevant value from the hardware.</remarks>
        public abstract double MaxExposure { get; }

        /// <summary>
        /// An internally used property for getting and setting the exposure value.
        /// </summary>
        /// <remarks>Subclasses should override this property to get and set the value with the hardware or store
        /// the set value until it can be used (e.g. when starting an exposure).</remarks>
        protected virtual double exposure { get; set; }
        /// <summary>
        /// The current exposure setting of the camera (in ms).
        /// </summary>
        /// <remarks>This is the public interface to controlling the exposure. As a most superficial level, it
        /// clamps (<see cref="Utilities.Clamp"/>) any provided values any provided values to the valid range
        /// [<see cref="MinGain"/>, <see cref="MaxGain"/>] before passing them to the subclass-specific
        /// <see cref="exposure"/> property, which handles the actual setting.</remarks>
        public virtual double Exposure {
            get => exposure;
            set {
                exposure = Utilities.Clamp(value, MinExposure, MaxExposure);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The minimal gain value supported by the camera.
        /// </summary>
        /// <remarks>Subclasses should override this property to extract the relevant value from the hardware.</remarks>
        public abstract double MinGain { get; }
        /// <summary>
        /// The maximal gain value supported by the camera.
        /// </summary>
        /// <remarks>Subclasses should override this property to extract the relevant value from the hardware.</remarks>
        public abstract double MaxGain { get; }
        
        /// <summary>
        /// An internally used property for getting and setting the gain value.
        /// </summary>
        /// <remarks>Subclasses should override this property to get and set the value with the hardware or store
        /// the set value until it can be used (e.g. when starting an exposure).</remarks>
        protected virtual double gain { get; set; }
        /// <summary>
        /// The current exposure setting of the camera.
        /// </summary>
        /// <remarks>This is the public interface to controlling the gain. As a most superficial level, it
        /// clamps (<see cref="Utilities.Clamp"/>) any provided values to the valid range [<see cref="MinGain"/>,
        /// <see cref="MaxGain"/>] before passing them to the subclass-specific <see cref="gain"/> property, which
        /// handles the actual setting.</remarks>
        public virtual double Gain {
            get => gain;
            set {
                gain = Utilities.Clamp(value, MinGain, MaxGain);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The last image captured by the camera.
        /// </summary>
        /// <remarks>It is allowed (and preferred) for subclasses to reuse the memory once it has been allocated
        /// instead of creating new <see cref="CameraImage"/> instances and relying on the Garbage Collector. In
        /// view of this, however, if a copy of the current last image is required to be kept for a prolonged period,
        /// it should be explicitly made with <see cref="CameraImage.Copy"/></remarks>.
        public CameraImage image { get; protected set; }
        /// <summary>
        /// The last image captured by the camera as a <see cref="BitmapSource"/>.
        /// </summary>
        /// <remarks>This simply gets the <see cref="CameraImage.BitmapSource"/> from <see cref="image"/>.</remarks>
        public BitmapSource View => image?.BitmapSource;

        /// <summary>
        /// Raised when an image has been captured.
        /// </summary>
        public event EventHandler<CameraImage> Captured;

        private string _displayName;
        public string DisplayName {
            get => _displayName ?? Name;
            set => _displayName = value;
        }

        private static readonly string[] _props = {
            nameof(Gain), nameof(Exposure)
        };
        protected override IEnumerable<string> props => _props;
        
        #endregion

        /// <summary>
        /// Create a new camera instance.
        /// </summary>
        /// <param name="autoupdate">Whether to register a callback to update the <see cref="image"/> and
        /// <see cref="View"/> properties when an image has been <see cref="Captured"/>. If this is <c>false</c>,
        /// the constructor does nothing.</param>
        protected BaseCamera(bool autoupdate=true) {
            if (autoupdate) Captured += (s, res) => {
                image = res;
                OnPropertyChanged(nameof(View));
            };
        }


        /// <summary>
        /// Names for the different queues (see <see cref="queue"/>). 
        /// </summary>
        public enum Priority {
            Production = 0,
            Tracking   = 1,
            LiveView   = 2
        }
        
        /// <summary>
        /// An instance of the queuing scheduler used to serialize capturing images.
        /// </summary>
        /// <remarks>
        /// <para>This is used by the public <see cref="Capture"/> method, so subclasses should not worry about
        /// serialization.</para>
        /// <para>The number of queues is controlled by the number of entries in <see cref="Priority"/>, one
        /// created for each.</para>
        /// </remarks>
        protected readonly QueueConsumer<SemaphoreSlim, CameraImage> queue = new QueueConsumer<SemaphoreSlim, CameraImage>(Enum.GetValues(typeof(Priority)).Length);

        /// <summary>
        /// Capture an image.
        /// </summary>
        /// <remarks>This method needs to be overriden in subclasses to actually capture the image. Serialization
        /// however is already ensured by <see cref="Capture"/>.</remarks>
        protected abstract CameraImage capture();
        
        /// <summary>
        /// Capture an image.
        /// </summary>
        /// <remarks>This method performs all tasks that do not depend on the hardware: mainly serialization of
        /// the capturing requests using <see cref="queue"/>. It also raises the <see cref="Captured"/> event and
        /// handles proper copying of the resulting image, if required. Normally, the function will return the
        /// result of <see cref="capture"/>, which is recommended to be the object in <see cref="image"/>, the image
        /// data in which might subsequently change. In order to preserve the image data, the caller should request a copy.
        /// </remarks>
        /// <param name="priority">The queue to put the request on. See <see cref="QueueConsumer"/> for an explanation
        /// of how exactly work items are dequeued and executed.</param>
        /// <param name="copy">Whether to return a copy of the image.</param>
        /// <returns>(A copy of) The captured image, or null if capturing has failed.</returns>
        public async Task<CameraImage> Capture(Priority priority=Priority.Production, bool copy = false) {
            if (Valid) {
                CameraImage ret = null;
                var item = new QueueItem<SemaphoreSlim, CameraImage> {
                    Func = o => capture(),
                    Param = new SemaphoreSlim(0, 1)
                };
                item.Complete += (s, res) => {
                    if (item.Result != null)
                        Captured?.Invoke(this, item.Result);
                    ret = copy ? item.Result?.Copy() : item.Result;
                    
                    (s as QueueItem<SemaphoreSlim, CameraImage>)?.Param.Release();
                };

                queue.Enqueue(item, (int) priority);
                await item.Param.WaitAsync();          
                return ret;
            } else return null;
        }

        /// <summary>
        /// Start and run an in-software live preview.
        /// </summary>
        /// <remarks>
        /// <para>This method repeatedly requests new images on the <see cref="Priority.LiveView"/> queue with
        /// a frequency that does not exceed <see cref="maxfps"/> but is otherwise as high as possible.</para>
        /// <para>It should not be called explicitly. Instead, starting and stopping the live preview is done through
        /// <see cref="StartLivePreview"/> and <see cref="StopLivePreview"/>.</para>
        /// </remarks>
        /// <param name="maxfps">The maximum frequency with which to request new frames.</param>
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


        private bool _previewOn;
        /// <summary>
        /// Whether the live preview is currently on.
        /// </summary>
        /// <remarks>This property is set itside <see cref="livePreview"/>, so it reflects whether that method
        /// is currently running.</remarks>
        public bool PreviewOn {
            get => _previewOn;
            protected set {
                if (value == _previewOn) return;
                _previewOn = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// A task which runs <see cref="livePreview"/>.
        /// </summary>
        private Task livePreviewTask;
        
        /// <summary>
        /// Used to cancel the live preview.
        /// </summary>
        private CancellationTokenSource cancelPreviewSource;

        /// <summary>
        /// Start the live preview if it is not currently on.
        /// </summary>
        /// <remarks>This method runs <see cref="livePreview"/> in a new <see cref="Task"/>, stored in
        /// <see cref="livePreviewTask"/> and initialises the <see cref="cancelPreviewSource"/>.</remarks>
        /// <param name="maxfps">The maximum frequency with which to request new frames.</param>
        public void StartLivePreview(double maxfps) {
            if (livePreviewTask == null && !PreviewOn) {
                cancelPreviewSource = new CancellationTokenSource();
                livePreviewTask = Task.Run(() => livePreview(maxfps));
            }
        }

        /// <summary>
        /// Stop the live preview if one is on and wait for it to finish.
        /// </summary>
        public async Task StopLivePreview() {
            cancelPreviewSource?.Cancel();
            if (livePreviewTask != null) {
                await livePreviewTask;
                livePreviewTask?.Dispose();
            }
            livePreviewTask = null;
            PreviewOn = false;
        }

        /// <summary>
        /// Stop all camera operations and clear all capturing requests.
        /// </summary>
        /// <returns>The list of unexecuted <see cref="QueueItem"/>s.</returns>
        public async Task<List<List<QueueItem<SemaphoreSlim, CameraImage>>>> Stop() {
            await StopLivePreview();
            return await queue.ClearTask();
        }

        protected override async Task disconnect() => await Stop();

        #region FOCUS

        public Focuser Focuser { get; } = new Focuser();

        #endregion
    }
}
