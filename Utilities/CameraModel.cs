using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using heliomaster.Properties;
using SmartFormat;

namespace heliomaster {
    public enum AutoExposureModes {
        [Description("AVG")] mean = 0,
        [Description("MAX")] max = 1,
        [Description("95%")] percentile = 2
    }

    /// <summary>
    /// Interface between the user interface and a <see cref="BaseCamera"/>
    /// </summary>
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CameraModel : BaseNotify {
        /// <summary>
        /// A method that serves as an action for the <see cref="Timelapse"/>.
        /// </summary>
        /// <param name="state">The timelapse state -- the CameraModel that started the <see cref="Timelapse"/>.</param>
        public static async void TimelapseAction(object state) {
            if (state is CameraModel m)
                await m.TakeImage();
        }
        
        private int _index;
        /// <summary>
        /// Identifier of the CameraModel within the collection of cameras used in the application.
        /// </summary>
        public int Index {
            get => _index;
            set {
                if (value == _index) return;
                _index = value;
                OnPropertyChanged();
            }
        }
        
        private BaseCamera _cam;
        /// <summary>
        /// The camera to which this CameraModel interfaces.
        /// </summary>
        /// <remarks>Setting this causes <see cref="Exposure"/> and <see cref="Gain"/> to be coerced.</remarks>
        [XmlIgnore] public BaseCamera Cam {
            get => _cam;
            set {
                if (Equals(value, _cam)) return;
                _cam = value;
                coerceExposure(_exposure);
                coerceGain(_gain);
                OnPropertyChanged();
            }
        }

        private Timelapse _timelapse;
        /// <summary>
        /// The timelapse associated to the camera.
        /// </summary>
        [XmlIgnore] public Timelapse Timelapse {
            get => _timelapse;
            set {
                if (Equals(value, _timelapse)) return;
                _timelapse = value;
                OnPropertyChanged();
            }
        }

        private string _cameraID;
        /// <summary>
        /// The ID string of the camera (passed to <see cref="BaseCamera.Connect"/>).
        /// </summary>
        public string CameraID {
            get => _cameraID;
            set {
                if (value == _cameraID) return;
                _cameraID = value;
                OnPropertyChanged();
            }
        }

        private CameraTypes _cameraType;
        /// <summary>
        /// The type of camera (identifier for the correct <see cref="BaseCamera"/> subclass).
        /// </summary>
        public CameraTypes CameraType {
            get => _cameraType;
            set {
                if (value == _cameraType) return;
                _cameraType = value;
                OnPropertyChanged();
            }
        }

        private string _name;
        /// <summary>
        /// A name for the camera set by the user.
        /// </summary>
        public string Name {
            get => _name;
            set {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }
        
        private string _focuserID;
        /// <summary>
        /// The id string of the <see cref="Focuser"/> associated with the camera.
        /// </summary>
        public string FocuserID {
            get => _focuserID;
            set {
                if (value == _focuserID) return;
                _focuserID = value;
                OnPropertyChanged();
            }
        }

        private string _localPathFormat;
        /// <summary>
        /// A format string used to generate the filenames under which images by the camera are saved on the local machine.
        /// </summary>
        /// <remarks>
        /// Formatting is done with <a href="https://github.com/axuno/SmartFormat.NET">SmartFormat.NET</a> and accepts
        /// the following named tokens:
        /// <list type="bullet">
        ///     <item>
        ///         <term>Cam</term>
        ///         <description>The <see cref="Name"/> of the CameraModel.</description>
        ///     </item>
        ///     <item>
        ///         <term>DateTime</term>
        ///         <description>The computer time when the image was captured.</description>
        ///     </item>
        /// </list>
        /// </remarks>
        public string LocalPathFormat {
            get => _localPathFormat;
            set {
                if (value == _localPathFormat) return;
                _localPathFormat = value;
                OnPropertyChanged();
            }
        }

        private string _remotePathFormat;
        /// <summary>
        /// A format string used to generate the filenames under which images are transferred to the remote host.
        /// </summary>
        /// <remarks>
        /// Formatting is done with <a href="https://github.com/axuno/SmartFormat.NET">SmartFormat.NET</a> and accepts
        /// the following named tokens:
        /// <list type="bullet">
        ///     <item>
        ///         <term>Cam</term>
        ///         <description>The <see cref="Name"/> of the CameraModel.</description>
        ///     </item>
        ///     <item>
        ///         <term>DateTime</term>
        ///         <description>The computer time when the image was captured.</description>
        ///     </item>
        ///     <item>
        ///         <term>LocalPath</term>
        ///         <description>The full <see cref="CapturedImage.LocalPath"/> of the image.</description>
        ///     </item>
        ///     <item>
        ///         <term>LocalBaseName</term>
        ///         <description>The base name extracted from <see cref="CapturedImage.LocalPath"/> by
        ///         <see cref="Path.GetFileName"/>.</description>
        ///     </item>
        /// </list>
        /// </remarks>
        public string RemotePathFormat {
            get => _remotePathFormat;
            set {
                if (value == _remotePathFormat) return;
                _remotePathFormat = value;
                OnPropertyChanged();
            }
        }

        private string _remoteCommandFormat;
        /// <summary>
        /// A format string used to generate the command to execute on the remote host for each image transferred to it.
        /// </summary>
        /// <remarks>
        /// Formatting is done with <a href="https://github.com/axuno/SmartFormat.NET">SmartFormat.NET</a> and accepts
        /// the following named tokens:
        /// <list type="bullet">
        ///     <item>
        ///         <term>Cam</term>
        ///         <description>The <see cref="Name"/> of the CameraModel.</description>
        ///     </item>
        ///     <item>
        ///         <term>DateTime</term>
        ///         <description>The computer time when the image was captured.</description>
        ///     </item>
        ///     <item>
        ///         <term>LocalPath</term>
        ///         <description>The full <see cref="CapturedImage.LocalPath"/> of the image.</description>
        ///     </item>
        ///     <item>
        ///         <term>LocalDir</term>
        ///         <description>The directory extracted from <see cref="CapturedImage.LocalPath"/> by
        ///         <see cref="Path.GetDirectoryName"/>.</description>
        ///     </item>
        ///     <item>
        ///         <term>LocalBaseName</term>
        ///         <description>The base name extracted from <see cref="CapturedImage.LocalPath"/> by
        ///         <see cref="Path.GetFileName"/>.</description>
        ///     </item>
        ///     <item>
        ///         <term>RemotePath</term>
        ///         <description>The full <see cref="CapturedImage.RemotePath"/> of the image.</description>
        ///     </item>
        ///     <item>
        ///         <term>LocalDir</term>
        ///         <description>The directory extracted from <see cref="CapturedImage.RemotePath"/> by
        ///         <see cref="Path.GetDirectoryName"/>.</description>
        ///     </item>
        ///     <item>
        ///         <term>LocalBaseName</term>
        ///         <description>The base name extracted from <see cref="CapturedImage.RemotePath"/> by
        ///         <see cref="Path.GetFileName"/>.</description>
        ///     </item>
        /// </list>
        /// </remarks>
        public string RemoteCommandFormat {
            get => _remoteCommandFormat;
            set {
                if (value == _remoteCommandFormat) return;
                _remoteCommandFormat = value;
                OnPropertyChanged();
            }
        }

        private double _resolution;
        /// <summary>
        /// The resolution of the camera in arcsec/px
        /// </summary>
        public double Resolution {
            get => _resolution;
            set {
                if (value.Equals(_resolution)) return;
                _resolution = value;
                OnPropertyChanged();
            }
        }

        private double _gain;
        /// <summary>
        /// Try to determine a valid value for the gain of the camera.
        /// </summary>
        /// <remarks>If <see cref="Cam"/> is not <c>null</c>, its <see cref="BaseCamera.Gain"/> is set to the given
        /// <paramref name="value"/>, and then <see cref="Gain"/> is set to the <see cref="BaseCamera.Gain"/>
        /// property of the camera, which might be different due to internal validation.
        /// </remarks>
        /// <param name="value">A suggested value for the gain.</param>
        private void coerceGain(double value) {
            if (Cam != null) {
                Cam.Gain = value;
                _gain    = Cam.Gain;
            } else _gain = value;
        }
        /// <summary>
        /// The gain setting of the camera (see <see cref="BaseCamera.Gain"/>).
        /// </summary>
        /// <remarks> If the <see cref="Cam"/> is set, values set this property is set to are propagated to the
        /// camera and then retrieved from it in <see cref="coerceGain"/>. This has the effect of delegating all range
        /// checks, etc. to the camera while still retaining the possibility to set and get values when the camera is
        /// not connected (but these are not guaranteed to be meaningful).</remarks>
        public double Gain {
            get => _gain;
            set {
                if (value.Equals(_gain)) return;
                coerceGain(value);
                OnPropertyChanged();
            }
        }

        private double _exposure;
        /// <summary>
        /// Try to determine a valid value for the exposure of the camera.
        /// </summary>
        /// <remarks>If <see cref="Cam"/> is not <c>null</c>, its <see cref="BaseCamera.Exposure"/> is set to the given
        /// <paramref name="value"/>, and then <see cref="Exposure"/> is set to the <see cref="BaseCamera.Exposure"/>
        /// property of the camera, which might be different due to internal validation.
        /// </remarks>
        /// <param name="value">A suggested value for the exposure.</param>
        private void coerceExposure(double value) {
            if (Cam != null) {
                Cam.Exposure = value;
                _exposure    = Cam.Exposure;
            } else _exposure = value;
        }
        /// <summary>
        /// The exposure setting of the camera (see <see cref="BaseCamera.Exposure"/>).
        /// </summary>
        /// <remarks> If the <see cref="Cam"/> is set, values set this property is set to are propagated to the
        /// camera and then retrieved from it in <see cref="coerceExposure"/>. This has the effect of delegating all
        /// range checks, etc. to the camera while still retaining the possibility to set and get values when the camera
        /// is not connected (but these are not guaranteed to be meaningful).</remarks>
        public double Exposure {
            get => _exposure;
            set {
                if (value.Equals(_exposure)) return;
                coerceExposure(value);
                OnPropertyChanged();
            }
        }
        
        
        /// <summary>
        /// A collection of the images taken with the camera in production mode.
        /// </summary>
        [XmlIgnore] public ObservableConcurrentList<CapturedImage> Images { get; } = new ObservableConcurrentList<CapturedImage>();
        
        
        #region TRANSFORMATION
        
        private bool _flip;
        /// <summary>
        /// Whether the camera image is flipped by the optical system of the telescope. 
        /// </summary>
        /// <remarks>See <see cref="Transform"/> for a full explanation.</remarks>
        public bool Flip {
            get => _flip;
            set {
                if (value == _flip) return;
                _flip = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Transform));
                OnPropertyChanged(nameof(FinalTransform));
            }
        }

        private int _rotate;
        /// <summary>
        /// By how many multiples of 90-degrees is the camera image rotated. 
        /// </summary>
        /// <remarks>See <see cref="Transform"/> for a full explanation.</remarks>
        public int Rotate {
            get => _rotate;
            set {
                if (value == _rotate) return;
                _rotate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Transform));
                OnPropertyChanged(nameof(FinalTransform));
            }
        }
        
        /// <summary>
        /// The transformation that has to be applied to the image so that its orientation is correct.
        /// </summary>
        /// <remarks>
        /// <para>There are in total 8 ways an image could be transformed if it can only be rotated through
        /// 90-degrees a whole number of times: 4 for each rotated state (represented by <see cref="Rotate"/>)
        /// and 2 for each chirality (captured in <see cref="Flip"/>). The concrete description used here is inspired
        /// by the way rotations and flips are represented in Exif data. In order to get from the raw image to the
        /// corrected version, you need to first flip it along the y-axis (left becomes right), if <see cref="Flip"/> is
        /// <c>true</c>, then rotate the result counterclockwise by 90 degrees <see cref="Rotate"/> times.</para>
        /// <para>Thsi procedure is implemented as a <see cref="TransformGroup"/> consisting of a x-scaling of -1
        /// or 1 depending on <see cref="Flip"/> (<see cref="ScaleTransform"/>) and a <see cref="RotateTransform"/>
        /// by <c>-Rotate * 90</c> degrees clockwise.</para>
        /// </remarks>
        public Transform Transform => new TransformGroup {
            Children = new TransformCollection(new Transform[] {
                new ScaleTransform(Flip ? -1 : 1, 1),
                new RotateTransform(-Rotate * 90)
            })
        };
        
        /// <summary>
        /// The transformation that has to be applied to the image so that north is up and east is left (we're looking
        /// from the inside of the celestial sphere).
        /// </summary>
        /// <remarks>This consists of <see cref="Transform"/> with an additional 180 degrees rotation (flip of both
        /// axes) in case the mount <see cref="Telescope.IsFlipped"/>.</remarks>
        public Transform FinalTransform =>
            O.Mount.IsFlipped == true
            ? new TransformGroup {Children = new TransformCollection(new[] {
                Transform,
                new ScaleTransform(-1, -1)})}
            : Transform;
        private void updateFinalTransform() => OnPropertyChanged(nameof(FinalTransform));
        
        #endregion
        
        #region AUTOEXPOSURE
        
        private bool _autoExpose;
        /// <summary>
        /// Whether to automatically adjust the exposure and gain settings (see <see cref="AdjustExposure()"/>).
        /// </summary>
        /// <remarks>Setting this to turns the process on or off correspondingly by adding or removing
        /// <see cref="handleAutoExposure"/> to/from the global refresh event (<see cref="O.Refresh"/>).</remarks>
        [XmlIgnore]
        public bool AutoExpose {
            get => _autoExpose;
            set {
                if (value == _autoExpose) return;
                _autoExpose = value;
                // TODO: Separate timer for autoexposure?
                if (value)
                    O.Refresh += handleAutoExposure;
                else
                    O.Refresh -= handleAutoExposure;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether an exposure adjustment <see cref="Task"/> is currently running.
        /// </summary>
        /// <seealso cref="handleAutoExposure"/>
        private bool autoExposing;
        
        /// <summary>
        /// Event handler that executes <see cref="AdjustExposure()"/> in a <see cref="Task"/> if another one is not
        /// currently running.
        /// </summary>
        /// <remarks>Serialization is achieved through <see cref="autoExposing"/>, which is set to <c>true</c>
        /// in the beginning of an adjusting Task and to <c>false</c> in its end.</remarks>
        private void handleAutoExposure() {
            if (!autoExposing)
                Task.Run(async () => {
                    try {
                        autoExposing = true;
                        Console.WriteLine(await AdjustExposure());
                    } finally {
                        autoExposing = false;
                    }
                });
        }

        private AutoExposureModes _autoMode;
        /// <summary>
        /// The auto exposure mode (see <see cref="AdjustExposure()"/>).
        /// </summary>
        public AutoExposureModes AutoMode {
            get => _autoMode;
            set {
                if (value == _autoMode) return;
                _autoMode = value;
                OnPropertyChanged();
            }
        }

        private double _autoLevel;
        /// <summary>
        /// The desired level to which to try to set the exposure (see <see cref="AdjustExposure()"/>).
        /// </summary>
        public double AutoLevel {
            get => _autoLevel;
            set {
                if (value.Equals(_autoLevel)) return;
                _autoLevel = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Adjust the camera settings based on analysis of a current image.
        /// </summary>
        /// <remarks>
        /// The exposure adjustemnt process is as follows:
        /// <list type="number">
        ///     <item>
        ///         <description>An image is captured from the <see cref="BaseCamera.Priority.Tracking"/> queue,
        ///         which is passed to the <see cref="AdjustExposure(CameraImage)"/> overload for analysis.</description>
        ///     </item>
        ///     <item>
        ///         <description>
        ///             Based on the <see cref="AutoMode"/> the appropriate <see cref="Py.lib"/> function
        ///             is called:
        ///             <list type="bullet">
        ///                 <item>
        ///                     <term><see cref="AutoExposureModes.max"/></term>
        ///                     <description><c>expcorr_max(img, </c><see cref="AutoLevel"/><c>)</c>;</description>
        ///                 </item>
        ///                 <item>
        ///                     <term><see cref="AutoExposureModes.mean"/></term>
        ///                     <description><c>expcorr_mean(img, </c><see cref="AutoLevel"/><c>)</c>;</description>
        ///                 </item>
        ///                 <item>
        ///                     <term><see cref="AutoExposureModes.max"/></term>
        ///                     <description><c>expcorr_level(img, </c><see cref="AutoLevel"/><c>, 95)</c>.</description>
        ///                 </item>
        ///             </list>
        ///             These functions return a list of factors (one for each channel in the image) by which the
        ///             maximum/average/95-percentile pixel value has to be multiplied in order to match
        ///             <see cref="AutoLevel"/>.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <description>These factors are then reduced to one by currently simply chosing the one for the first
        ///         channel, which is then passed to <see cref="adjustExposure"/> which handles the actual adjustment
        ///         of the settings. Currently, the method to achieve the desired exposure value is to simply (attempt
        ///         to) scale the <see cref="Exposure"/> by the given factor (instead of scaling the <see cref="Gain"/>
        ///         as well. Apart from the internal in-camera validation of the new exposure value there is an
        ///         additional cap at 100ms.</description>
        ///     </item>
        /// </list>
        /// </remarks>
        /// <returns>Whether the process was completed successfully.</returns>
        public async Task<bool> AdjustExposure() {
            return await Cam.Capture(BaseCamera.Priority.Tracking, copy: true) is CameraImage img
                && AdjustExposure(img);
        }
        /// <summary>
        /// Overload that handles the image analysis and adjustment (see <see cref="AdjustExposure()"/>)
        /// </summary>
        /// <param name="img">The current image which to adjust.</param>
        /// <returns>Whether the process was completed successfully.</returns>
        public bool AdjustExposure(CameraImage img) {
            var npimg = img.to_numpy();
            if (npimg != null) {
                dynamic ret = null;
                Py.Run(() => {
                    switch (O.CamModels[0].AutoMode) {
                        case AutoExposureModes.max:
                            ret = Py.lib.expcorr_max(npimg, AutoLevel);
                            break;
                        case AutoExposureModes.mean:
                            ret = Py.lib.expcorr_mean(npimg, AutoLevel);
                            break;
                        case AutoExposureModes.percentile:
                            ret = Py.lib.expcorr_level(npimg, AutoLevel, 95); // TODO: unhardcode 95%?
                            break;
                    }
                });

                if (PythonGeneralExtensions.ToCLI(ret) is List<object> factors
                    && factors.Count == img.Channels
                    && factors.All(v => v is double)) {
                    var f = (double)factors[0]; // TODO: handle multi-channel case by e.g. getting max/min/avg value
                    adjustExposure(f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adjust the exposure (including <see cref="Gain"/> and <see cref="Exposure"/>) so that it is scaled by the
        /// given factor <paramref name="f"/>.
        /// </summary>
        /// <remarks>Currently this simply multiplies the <see cref="Exposure"/> by <paramref name="f"/> and then
        /// caps it at 100ms.</remarks>
        /// <returns>The factor by which the exposure was actually increased. This might be different from
        /// <paramref name="f"/> due to in-camera validation or the imposed cap.</returns>
        private double adjustExposure(double f) {
            // TODO: implement more sophisticated exposure adjustment
            var o = Exposure;
            var n = Exposure * f;
            Exposure = Math.Min(100, n); // TODO: Smarter limiting
            return Exposure / o;
        }

        #endregion
        

        /// <summary>
        /// Create a new CameraModel and register its <see cref="updateFinalTransform"/> method as a handler of the
        /// <see cref="Telescope.FlippedChanged"/> event of <see cref="O.Mount"/>.
        /// </summary>
        public CameraModel() {
            O.Mount.FlippedChanged += updateFinalTransform;
        }
        
        /// <summary>
        /// Disconnect the <see cref="Cam"/>, delete the images in <see cref="Images"/> and clear the refresh callbacks.
        /// </summary>
        public async Task Dispose() {
            O.Mount.FlippedChanged -= updateFinalTransform;
            O.Refresh -= handleAutoExposure;
            await Cam.Disconnect();
            Images.Clear();
        }

        
        /// <summary>
        /// Capture an image with the camera and return it as a <see cref="CapturedImage"/> with the appropriate metadata.
        /// </summary>
        /// <returns>The image as a <see cref="CapturedImage"/> or <c>null</c> if <see cref="BaseCamera.Capture"/>
        /// returned <c>null</c>.</returns>
        public async Task<CapturedImage> CaptureImage() {
            var img = await Cam.Capture(copy: true);
            if (img == null) {
                return null;
            }

            var t = FinalTransform.Clone();
            t.Freeze();
            return new CapturedImage {
                Image = img,
                LocalPath = Smart.Format(LocalPathFormat, new {
                    Cam = Name,
                    DateTime = DateTime.UtcNow,
                }),
                Transform = t
            };
        }

        /// <summary>
        /// Capture an image , save it, transfer it and process it on the remote host.
        /// </summary>
        /// <remarks>This function captures an image with <see cref="CaptureImage"/>, adds it to <see cref="Images"/>,
        /// saves it using <see cref="CapturedImage.Save"/> and sets callbacks for the <see cref="CapturedImage.Saved"/>
        /// and <see cref="CapturedImage.Transferred"/> events which respectively transfer to and process the image on
        /// the remote host if the appropriate settings are on (see <see cref="S.Remote"/>).</remarks>
        /// <returns>The <see cref="CapturedImage"/> or <c>null</c> if <see cref="CaptureImage"/> returned <c>null</c>.</returns>
        public async Task<CapturedImage> TakeImage() {
            var cimg = await CaptureImage();
            if (cimg != null) {
                cimg.Saved += c => {
                    c.Dispose();
                    if (S.Remote.DoTransfer)
                        c.Transfer(O.Remote, Smart.Format(RemotePathFormat, new {
                            Cam           = Name,
                            DateTime      = DateTime.UtcNow,
                            LocalPath     = c.LocalPath,
                            LocalBaseName = Path.GetFileName(c.LocalPath),
                        }));
                };
                cimg.Transferred += c => {
                    if (false && S.Remote.DoDeleteLocal)
                        File.Delete(c.LocalPath); // TODO: Deleting fails...

                    if (S.Remote.DoCommand)
                        c.Process(O.Remote, Smart.Format(RemoteCommandFormat.Replace(Environment.NewLine, " "), new {
                            Cam            = Name,
                            DateTime       = DateTime.UtcNow,
                            LocalPath      = c.LocalPath,
                            LocalDir       = Path.GetDirectoryName(c.LocalPath),
                            LocalBaseName  = Path.GetFileName(c.LocalPath),
                            RemotePath     = c.RemotePath,
                            RemoteDir      = Path.GetDirectoryName(c.RemotePath),
                            RemoteBaseName = Path.GetFileName(c.RemotePath),
                        }));
                };

                // Application.Current.Dispatcher.InvokeAsync(() => { Images.Add(cimg); });
                Images.Add(cimg);
                cimg.Save();
            }
            return cimg;
        }
    }
}
