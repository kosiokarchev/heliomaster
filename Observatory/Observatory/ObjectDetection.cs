using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace heliomaster {
    public partial class Observatory {
        /// <summary>
        /// Determine whether the target of observations can be seen in the <see cref="TrackingCamera"/>.
        /// </summary>
        /// <remarks>Implemented through <see cref="LocateObject"/>.</remarks>
        public async Task<bool> ObjectIsInView() => (await LocateObject()).Found;

        /// <summary>
        /// Try to ensure that the target of observations can be seen in the <see cref="TrackingCamera"/>.
        /// </summary>
        /// <remarks>
        /// <para>NOT IMPLEMENTED: always returns whether the <see cref="ObjectIsInView"/> currently.</para>
        /// <para>Should repeatedly check whether <see cref="ObjectIsInView"/> and move the telescope around, until
        /// it is found or a certain condition is met.</para>
        /// </remarks>
        /// <returns>Whether the search was successful.</returns>
        public async Task<bool> SearchForObject() => await ObjectIsInView(); // TODO: Implement object searching

        private CameraModel _trackingCamera;
        /// <summary>
        /// The camera (model) to use for tracking and object detection.
        /// </summary>
        public CameraModel TrackingCamera {
            get => _trackingCamera;
            set {
                if (value == _trackingCamera) return;
                _trackingCamera = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// A class that stores data from the object detection procedure and provides the interface to the Python object
        /// detection routines.
        /// </summary>
        /// <remarks>If the object is found, the difference between <see cref="Point"/> and <see cref="Centre"/> will
        /// represent the offset of the located object from the pointing of the telescope. The <see cref="Vector.X"/> of
        /// the <see cref="Vector"/> <see cref="Point"/>-<see cref="Centre"/> will represent the negative offset in
        /// right ascension (times the cosine of the declination), and its <see cref="Vector.Y"/> the negative offset
        /// in declination.</remarks>
        private class LocateResult {
            /// <summary>
            /// The x-coordinate of the located object in the raw (non-transformed) image.
            /// </summary>
            public double X;

            /// <summary>
            /// The y-coordinate of the located object in the raw (non-transformed) image.
            /// </summary>
            public double Y;

            /// <summary>
            /// An indication of the "goodness" of the detection.
            /// </summary>
            public double val;

            /// <summary>
            /// The x-coordinate of the telescope pointing (the centre of the image) in the raw (non-transformed image).
            /// </summary>
            public double cX;

            /// <summary>
            /// The y-coordinate of the telescope pointing (the centre of the image) in the raw (non-transformed image).
            /// </summary>
            public double cY;

            /// <summary>
            /// The <see cref="CapturedImage.Transform"/> of the image.
            /// </summary>
            public Transform Transform;

            /// <summary>
            /// The coordinates of the located object in the raw (non-transformed) image.
            /// </summary>
            public Point RawPoint => new Point(X, Y);

            /// <summary>
            /// The transformed coordinates of the located object.
            /// </summary>
            public Point Point => Transform.Transform(RawPoint);

            /// <summary>
            /// The transformed coordinates of the telescope pointing (the centre of the image).
            /// </summary>
            public Point Centre => Transform.Transform(new Point(cX, cY));

            /// <summary>
            /// Whether this LocateResult represents a detection. Currently checks the <see cref="val"/> is greater than 0.5.
            /// </summary>
            public bool Found => val > 0.5;

            /// <summary>
            /// Create a new LocateResult which represents a failed object detection (<see cref="Found"/><c> == false</c>). 
            /// </summary>
            public LocateResult() { }

            /// <summary>
            /// Create a new LocateResult by running <see cref="Py.detect_body"/> on the provided image and processing the results.
            /// </summary>
            /// <param name="img">The image to use for detection.</param>
            /// <param name="res">The resolution of <paramref name="img"/> in arcsec/px
            /// (see <see cref="CameraModel.Resolution"/>).</param>
            /// <remarks>
            /// Creates a NumPy image array from the <see cref="CapturedImage.Image"/> in <paramref name="img"/> via
            /// <see cref="PythonExtensions.to_numpy"/> and passes it to <see cref="Py.detect_body"/> along with
            /// an instance of <c>ephem.Sun</c> (created from <see cref="Pynder.PyEphemObjects.Sun"/>) and
            /// <paramref name="res"/>. Then checks the result is an iterable consisting of three <see cref="double"/>s
            /// and extracts them as (<see cref="X"/>, <see cref="Y"/>, <see cref="val"/>). Also saves the
            /// <see cref="CapturedImage.Transform"/> of <paramref name="img"/> and figures out the coordinates of the
            /// centre of the image, which should correspond to the pointing of the telescope.
            /// </remarks>
            public LocateResult(CapturedImage img, double res) {
                Py.Run(() => {
                    if (img.Image is CameraImage camimg
                        && Py.detect_body(camimg.to_numpy(), Pynder.PyEphemObjects.Sun(), res)
                            is Python.Runtime.PyObject p // TODO: Allow to select which object
                        && p.ToCLI() is List<object> l
                        && l.Count == 3 && l.All(i => i is double)) {
                        X         = (double) l[0];
                        Y         = (double) l[1];
                        val       = (double) l[2];
                        Transform = img.Transform;
                        cX        = camimg.Width / 2.0;
                        cY        = camimg.Height / 2.0;
                    }
                });
            }
        }

        /// <summary>
        /// Returns a new <see cref="LocateResult"/> from an image captured by the <see cref="TrackingCamera"/> via
        /// <see cref="CameraModel.CaptureImage"/>, or an empty (<see cref="LocateResult.Found"/><c> == false</c>)
        /// result if either <see cref="TrackingCamera"/> is not set or it is unable to capture an image.
        /// </summary>
        private async Task<LocateResult> LocateObject() {
            return (TrackingCamera != null
                    && TrackingCamera.Resolution > 0
                    && await TrackingCamera.CaptureImage() is CapturedImage img)
                       ? new LocateResult(img, TrackingCamera.Resolution)
                       : new LocateResult();
        }

        /// <summary>
        /// Try to bring the object into the centre of the <see cref="TrackingCamera"/>.
        /// </summary>
        /// <remarks>Locates the object via <see cref="LocateObject"/> and then <see cref="Telescope.Adjust"/>s
        /// the mount by the offset calculated from <see cref="LocateResult.Point"/> and
        /// <see cref="LocateResult.Centre"/>.</remarks>
        public async Task Track() {
            const string pre = "IMAGE TRACKING: ";

            Logger.debug(pre + "Locating target.");
            var locres = await LocateObject();
            if (locres.Found) {
                var rawloc = locres.RawPoint;
                var loc    = locres.Point;
                Logger.debug(pre + $"Found: ({rawloc.X}, {rawloc.Y}) --> ({loc.X}, {loc.Y})");

                var d    = loc - locres.Centre;
                var ddec = -d.Y / (3600 * TrackingCamera.Resolution);
                var dra  = -d.X / (3600 * TrackingCamera.Resolution) * Mount.rcosdec;
                Logger.debug(pre + $"Offset: ({d.X}, {d.Y}) => dRA={dra}, dDec={ddec}");

                // await Mount.Adjust(dra, ddec);

                Logger.debug(pre + "Completed.");
            } else
                Logger.debug(pre + "Target not found.");
        }
    }
}
