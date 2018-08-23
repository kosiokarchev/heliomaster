using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using heliomaster.Properties;
using Newtonsoft.Json;
using SmartFormat;

namespace heliomaster {
    public enum AutoExposureModes {
        [Description("AVG")] mean = 0,
        [Description("MAX")] max = 1,
        [Description("95%")] percentile = 2
    }

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class CameraModel : BaseNotify {
        private int _index;
        public int Index {
            get => _index;
            set {
                if (value == _index) return;
                _index = value;
                OnPropertyChanged();
            }
        }

        private string _cameraID;
        public string CameraID {
            get => _cameraID;
            set {
                if (value == _cameraID) return;
                _cameraID = value;
                OnPropertyChanged();
            }
        }

        private CameraTypes _cameraType;
        public CameraTypes CameraType {
            get => _cameraType;
            set {
                if (value == _cameraType) return;
                _cameraType = value;
                OnPropertyChanged();
            }
        }

        private string _name;
        public string Name {
            get => _name;
            set {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        private string _localPathFormat;
        public string LocalPathFormat {
            get => _localPathFormat;
            set {
                if (value == _localPathFormat) return;
                _localPathFormat = value;
                OnPropertyChanged();
            }
        }

        private string _remotePathFormat;
        public string RemotePathFormat {
            get => _remotePathFormat;
            set {
                if (value == _remotePathFormat) return;
                _remotePathFormat = value;
                OnPropertyChanged();
            }
        }

        private string _remoteCommandFormat;
        public string RemoteCommandFormat {
            get => _remoteCommandFormat;
            set {
                if (value == _remoteCommandFormat) return;
                _remoteCommandFormat = value;
                OnPropertyChanged();
            }
        }

        private double _gain;
        private void coerceGain(double val) {
            if (Cam != null) {
                Cam.Gain = val;
                _gain    = Cam.Gain;
            } else _gain = val;
        }
        public double Gain {
            get => _gain;
            set {
                if (value.Equals(_gain)) return;
                coerceGain(value);
                OnPropertyChanged();
            }
        }

        private double _exposure;
        private void coerceEsposure(double val) {
            if (Cam != null) {
                Cam.Exposure = val;
                _exposure    = Cam.Exposure;
            } else _exposure = val;
        }
        public double Exposure {
            get => _exposure;
            set {
                if (value.Equals(_exposure)) return;
                coerceEsposure(value);
                OnPropertyChanged();
            }
        }

        private bool _autoExpose;
        public bool AutoExpose {
            get => _autoExpose;
            set {
                if (value == _autoExpose) return;
                _autoExpose = value;
                OnPropertyChanged();
            }
        }

        private AutoExposureModes _autoMode;
        public AutoExposureModes AutoMode {
            get => _autoMode;
            set {
                if (value == _autoMode) return;
                _autoMode = value;
                OnPropertyChanged();
            }
        }

        private double _autoLevel;
        public double AutoLevel {
            get => _autoLevel;
            set {
                if (value.Equals(_autoLevel)) return;
                _autoLevel = value;
                OnPropertyChanged();
            }
        }


        private string _focuserID;
        public string FocuserID {
            get => _focuserID;
            set {
                if (value == _focuserID) return;
                _focuserID = value;
                OnPropertyChanged();
            }
        }

        private bool _flip;
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
        public Transform Transform => new TransformGroup {
            Children = new TransformCollection(new Transform[] {
                new ScaleTransform(Flip ? -1 : 1, 1),
                new RotateTransform(-Rotate * 90)
            })
        };
        public Transform FinalTransform =>
            O.Mount.IsFlipped
            ? new TransformGroup {Children = new TransformCollection(new[] {
                Transform,
                new ScaleTransform(-1, -1)})}
            : Transform;

        private BaseCamera _cam;
        [XmlIgnore] public BaseCamera Cam {
            get => _cam;
            set {
                if (Equals(value, _cam)) return;
                _cam = value;
                coerceEsposure(_exposure);
                coerceGain(_gain);
                OnPropertyChanged();
            }
        }

        private Timelapse _timelapse;
        [XmlIgnore] public Timelapse Timelapse {
            get => _timelapse;
            set {
                if (Equals(value, _timelapse)) return;
                _timelapse = value;
                OnPropertyChanged();
            }
        }

        [XmlIgnore] public ObservableCollection<CapturedImage> Images { get; } = new ObservableCollection<CapturedImage>();

        public CameraModel() {
            O.Mount.FlippedChanged += () => {
                OnPropertyChanged(nameof(FinalTransform));
            };
        }

        public static async void TimelapseAction(object state) {
            if (state is CameraModel m)
                await m.TakeImage();
        }

        public async Task<CapturedImage> TakeImage() {
            var img  = await Cam.Capture(copy: true);
            if (img == null) {
                return null;
            }

            var t = FinalTransform.Clone();
            t.Freeze();
            var cimg = new CapturedImage {
                Image = img,
                LocalPath = Smart.Format(LocalPathFormat, new {
                    Cam = Name,
                    DateTime = DateTime.UtcNow,
                }),
                Transform = t
            };

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
                if (S.Remote.DoDeleteLocal)
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

            Application.Current.Dispatcher.InvokeAsync(() => { Images.Add(cimg); });
            cimg.Save();
            return cimg;
        }
    }
}
