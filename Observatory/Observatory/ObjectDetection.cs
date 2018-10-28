using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace heliomaster {
    public partial class Observatory {
        public event Action ObjectNotFound;
        public void         ObjectNotFoundRaise() => ObjectNotFound?.Invoke();

        public void ObjectNotFoundHandle() {
            // TODO: Search for object
        }

        private CameraModel _trackingCamera;

        public CameraModel TrackingCamera {
            get => _trackingCamera;
            set {
                if (value == _trackingCamera) return;
                _trackingCamera = value;
                OnPropertyChanged();
            }
        }

        private class LocateResult {
            public double    X, Y, val, cX, cY;
            public Transform Transform;
            public Point     RawPoint => new Point(X, Y);
            public Point     Point    => Transform.Transform(RawPoint);
            public Point     Centre   => Transform.Transform(new Point(cX, cY));
            public bool      Found    => val > 0.5;

            public LocateResult() { }

            public LocateResult(CapturedImage img, double res) {
                Py.Run(() => {
                    if (img.Image is CameraImage camimg
                        && Py.detect_body(camimg.to_numpy(), Pynder.PyEphemObjects.Sun(), res) is
                            Python.Runtime.PyObject p // TODO: Allow to select which object
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

        private async Task<LocateResult> LocateObject() {
            return (TrackingCamera != null
                    && TrackingCamera.Resolution > 0
                    && await TrackingCamera.CaptureImage() is CapturedImage img)
                       ? new LocateResult(img, TrackingCamera.Resolution)
                       : new LocateResult();
        }

        public async Task Track() {
            var pre = "IMAGE TRACKING: ";

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

        public async Task<bool> ObjectIsInView()  => (await LocateObject()).Found;
        public       bool       SearchForObject() { return false; } // TODO: Implement object searching
    }
}